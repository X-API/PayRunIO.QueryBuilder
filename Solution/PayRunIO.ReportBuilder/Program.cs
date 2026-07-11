namespace PayRunIO.ReportBuilder
{
    using System.Security.Claims;

    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authentication.Cookies;
    using Microsoft.AspNetCore.Authentication.OpenIdConnect;
    using Microsoft.IdentityModel.Protocols.OpenIdConnect;

    using PayRunIO.ReportBuilder.Auth;
    using PayRunIO.ReportBuilder.Components;
    using PayRunIO.ReportBuilder.Services;
    using PayRunIO.RqlAssistant.Service;

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddRazorComponents().AddInteractiveServerComponents();
            builder.Services.AddCascadingAuthenticationState();

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
            builder.Services.AddScoped<ReportSession>();
            builder.Services.AddScoped<ReportSessionStore>();
            builder.Services.AddScoped<ReportChatService>();

            // One RAG service per circuit: creation loads the schema/grammar resources, and per-circuit
            // instances avoid sharing the tool dispatcher between concurrent user conversations.
            builder.Services.AddScoped<IRqlRagService>(
                serviceProvider => ServiceFactory.CreateService(serviceProvider.GetRequiredService<IConfiguration>()));

            builder.Services.AddScoped<IRqlQueryReviewer>(_ => ServiceFactory.CreateQueryReviewer());

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/error", createScopeForErrors: true);
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();

            app.MapGet(
                    "/auth/login",
                    (string? returnUrl) => Results.Challenge(
                        new AuthenticationProperties { RedirectUri = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl }))
                .AllowAnonymous();

            app.MapGet(
                "/auth/logout",
                (HttpContext httpContext, IUserTokenStore tokenStore) =>
                    {
                        var subject = FindSubject(httpContext.User);

                        if (subject != null)
                        {
                            tokenStore.Remove(subject);
                        }

                        return Results.SignOut(
                            new AuthenticationProperties { RedirectUri = "/" },
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
                        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                    })
                .AddCookie(options =>
                    {
                        options.ExpireTimeSpan = TimeSpan.FromHours(12);
                        options.SlidingExpiration = true;
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
                                                    tokenResponse.ExpiresIn));
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
