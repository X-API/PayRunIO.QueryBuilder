namespace PayRunIO.ReportBuilder
{
    using System.Security.Claims;

    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authentication.Cookies;
    using Microsoft.AspNetCore.Authentication.OpenIdConnect;
    using Microsoft.AspNetCore.HttpOverrides;
    using Microsoft.IdentityModel.Protocols.OpenIdConnect;

    using PayRunIO.ReportBuilder.Auth;
    using PayRunIO.ReportBuilder.Components;
    using PayRunIO.ReportBuilder.Services;
    using PayRunIO.RqlAssistant.Service;

    public class Program
    {
        /// <summary>
        /// KeyCloak identity provider hint parameter. When present on the authorization request,
        /// KeyCloak skips its own login form and forwards straight to the named federated provider.
        /// </summary>
        private const string IdpHintParameter = "kc_idp_hint";

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(
                new WebApplicationOptions
                    {
                        Args = args,

                        // sc.exe-launched services start with C:\Windows\System32 as the current
                        // directory, which breaks appsettings.json and Razor component discovery
                        // unless the content root is pinned to the published app's own folder.
                        ContentRootPath = AppContext.BaseDirectory,
                    });

            // No-op outside a Windows Service (e.g. under IIS or `dotnet run`); when launched by
            // the Service Control Manager it swaps in the Windows Service lifetime/logging so
            // SCM start/stop requests are honoured and console logging doesn't fail with no console.
            builder.Host.UseWindowsService(options => options.ServiceName = "PayRunIO Report Builder");

            builder.Services.AddRazorComponents().AddInteractiveServerComponents();
            builder.Services.AddCascadingAuthenticationState();

            // The app is only ever reached via the load balancer, which terminates HTTPS and
            // forwards to this instance over plain HTTP. Without this, every absolute URL the
            // app builds (including the KeyCloak OIDC redirect_uri) is stamped http://, which
            // KeyCloak then redirects back to instead of the load balancer's https:// origin.
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                    // The load balancer is not on a fixed, known address, so trust the proxy
                    // hop unconditionally. Safe here because the app is not directly internet
                    // reachable - the load balancer is the only path in.
                    options.KnownNetworks.Clear();
                    options.KnownProxies.Clear();
                });

            ConfigureAuthentication(builder);

            builder.Services.AddHttpClient(
                PayRunQueryService.HttpClientName,
                client =>
                    {
                        client.BaseAddress = new Uri(
                            builder.Configuration["PayRunApi:EndPoint"]
                            ?? throw new InvalidOperationException("Missing configuration value 'PayRunApi:EndPoint'."));
                        client.Timeout = TimeSpan.FromMinutes(5);
                    });

            builder.Services.AddHttpClient(ApiTokenAccessor.HttpClientName);

            builder.Services.AddSingleton<IUserTokenStore, InMemoryUserTokenStore>();
            builder.Services.AddScoped<ApiTokenAccessor>();
            builder.Services.AddScoped<PayRunQueryService>();
            builder.Services.AddScoped<ReportDefinitionService>();
            builder.Services.AddScoped<ReportSession>();
            builder.Services.AddScoped<ReportSessionStore>();
            builder.Services.AddScoped<LocalReportStore>();
            builder.Services.AddScoped<DesignerModeStore>();
            builder.Services.AddScoped<ReportChatService>();

            // One RAG service per circuit: creation loads the schema/grammar resources, and per-circuit
            // instances avoid sharing the tool dispatcher between concurrent user conversations.
            builder.Services.AddScoped<IRqlRagService>(
                serviceProvider => ServiceFactory.CreateService(serviceProvider.GetRequiredService<IConfiguration>()));

            builder.Services.AddScoped<IRqlQueryReviewer>(_ => ServiceFactory.CreateQueryReviewer());

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                // Must run first: everything after this (HSTS, auth challenge URL generation, etc.)
                // needs HttpContext.Request.Scheme/Host to already reflect the load balancer's
                // original https:// request rather than the internal http:// hop. Only applied when
                // deployed behind the load balancer - locally there is no proxy, and honouring an
                // X-Forwarded-Proto: https header would rewrite the scheme to https, breaking the
                // OIDC redirect_uri and preventing the app from running under plain http.
                app.UseForwardedHeaders();

                app.UseExceptionHandler("/error", createScopeForErrors: true);
                app.UseHsts();
            }

            // No UseHttpsRedirection(): the load balancer terminates TLS and this instance only
            // ever receives HTTP. Redirecting here would target the internal http host instead
            // of enforcing HTTPS at the public edge, which the load balancer already does.
            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();

            app.MapGet(
                    "/auth/login",
                    (string? returnUrl, bool? federated, IConfiguration configuration) =>
                        {
                            var properties = new AuthenticationProperties { RedirectUri = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl };

                            if (federated == true)
                            {
                                var idpHint = configuration["KeyCloak:FederatedIdpHint"];

                                if (!string.IsNullOrEmpty(idpHint))
                                {
                                    properties.Items[IdpHintParameter] = idpHint;
                                }
                            }

                            return Results.Challenge(properties, new[] { OpenIdConnectDefaults.AuthenticationScheme });
                        })
                .AllowAnonymous();

            app.MapGet(
                "/auth/logout",
                (HttpContext httpContext, IUserTokenStore tokenStore) =>
                    {
                        var subject = FindSubject(httpContext.User);
                        var properties = new AuthenticationProperties { RedirectUri = "/" };

                        if (subject != null)
                        {
                            // KeyCloak requires an id_token_hint alongside the post logout redirect.
                            // The identity token lives in the token store (SaveTokens is off), so carry
                            // it into the sign out properties before the store entry is removed.
                            if (tokenStore.TryGet(subject, out var tokens) && tokens.IdToken != null)
                            {
                                properties.StoreTokens(
                                    new[] { new AuthenticationToken { Name = OpenIdConnectParameterNames.IdToken, Value = tokens.IdToken } });
                            }

                            tokenStore.Remove(subject);
                        }

                        return Results.SignOut(
                            properties,
                            new[] { CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme });
                    });

            app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

            app.Run();
        }

        private static void ConfigureAuthentication(WebApplicationBuilder builder)
        {
            var keyCloak = builder.Configuration.GetSection("KeyCloak");

            builder.Services
                .AddAuthentication(options =>
                    {
                        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;

                        // The cookie scheme owns the default challenge: [Authorize] pages are enforced
                        // at the endpoint level on the initial request, and the cookie challenge sends
                        // the user to the local sign in chooser rather than straight to KeyCloak. The
                        // /auth/login endpoint challenges the OIDC scheme explicitly once the user has
                        // picked direct or federated sign in.
                        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    })
                .AddCookie(options =>
                    {
                        options.ExpireTimeSpan = TimeSpan.FromHours(12);
                        options.SlidingExpiration = true;
                        options.LoginPath = "/signin";
                    })
                .AddOpenIdConnect(options =>
                    {
                        options.Authority = keyCloak["Authority"];
                        options.ClientId = keyCloak["ClientId"];
                        options.ClientSecret = keyCloak["ClientSecret"];
                        options.ResponseType = OpenIdConnectResponseType.Code;
                        options.UsePkce = true;
                        options.GetClaimsFromUserInfoEndpoint = true;
                        options.TokenValidationParameters.NameClaimType = "preferred_username";

                        // Tokens live in IUserTokenStore rather than the auth cookie: keeps the cookie
                        // under header size limits and lets refreshed tokens reach long-lived circuits.
                        options.SaveTokens = false;

                        options.Scope.Clear();

                        var scopes = keyCloak["Scopes"] ?? "openid offline_access profile email";

                        foreach (var scope in scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        {
                            options.Scope.Add(scope);
                        }

                        options.Events = new OpenIdConnectEvents
                            {
                                OnRedirectToIdentityProvider = context =>
                                    {
                                        // The login endpoint stashes the federated provider hint in the
                                        // challenge properties; forward it to KeyCloak so the user lands
                                        // directly on the Azure AD sign in instead of the KeyCloak form.
                                        if (context.Properties.Items.TryGetValue(IdpHintParameter, out var idpHint)
                                            && !string.IsNullOrEmpty(idpHint))
                                        {
                                            context.ProtocolMessage.SetParameter(IdpHintParameter, idpHint);
                                        }

                                        return Task.CompletedTask;
                                    },
                                OnTokenValidated = context =>
                                    {
                                        var subject = FindSubject(context.Principal);
                                        var tokenResponse = context.TokenEndpointResponse;

                                        if (subject != null && tokenResponse?.AccessToken != null)
                                        {
                                            var tokenStore = context.HttpContext.RequestServices.GetRequiredService<IUserTokenStore>();

                                            tokenStore.Save(
                                                subject,
                                                UserTokens.FromTokenResponse(
                                                    tokenResponse.AccessToken,
                                                    tokenResponse.RefreshToken,
                                                    tokenResponse.ExpiresIn,
                                                    tokenResponse.IdToken));
                                        }

                                        return Task.CompletedTask;
                                    },
                                OnRedirectToIdentityProviderForSignOut = context =>
                                    {
                                        var idToken = context.Properties?.GetTokenValue(OpenIdConnectParameterNames.IdToken);

                                        if (!string.IsNullOrEmpty(idToken))
                                        {
                                            context.ProtocolMessage.IdTokenHint = idToken;
                                        }

                                        return Task.CompletedTask;
                                    }
                            };
                    });

            builder.Services.AddAuthorization();
        }

        private static string? FindSubject(ClaimsPrincipal? principal) =>
            principal?.FindFirst("sub")?.Value ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
