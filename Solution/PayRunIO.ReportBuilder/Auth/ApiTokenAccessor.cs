namespace PayRunIO.ReportBuilder.Auth
{
    using System.Security.Claims;
    using System.Text.Json;

    using Microsoft.AspNetCore.Components.Authorization;

    /// <summary>
    /// Resolves the current user's PayRun.io API access token, transparently refreshing it against
    /// the KeyCloak token endpoint when it is about to expire. Scoped to the Blazor circuit so it
    /// can read the circuit's <see cref="AuthenticationStateProvider"/>.
    /// </summary>
    public sealed class ApiTokenAccessor
    {
        public const string HttpClientName = "KeyCloakToken";

        private static readonly TimeSpan ExpiryLeeway = TimeSpan.FromSeconds(60);

        private readonly AuthenticationStateProvider authenticationStateProvider;

        private readonly IUserTokenStore tokenStore;

        private readonly IHttpClientFactory httpClientFactory;

        private readonly IConfiguration configuration;

        public ApiTokenAccessor(
            AuthenticationStateProvider authenticationStateProvider,
            IUserTokenStore tokenStore,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            this.authenticationStateProvider = authenticationStateProvider;
            this.tokenStore = tokenStore;
            this.httpClientFactory = httpClientFactory;
            this.configuration = configuration;
        }

        public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            var authState = await this.authenticationStateProvider.GetAuthenticationStateAsync();

            var subject = authState.User.FindFirst("sub")?.Value
                          ?? authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(subject) || !this.tokenStore.TryGet(subject, out var tokens))
            {
                throw new ApiTokenUnavailableException(
                    "No API access token is available for your session. Please sign in again.");
            }

            if (tokens.ExpiresAt - ExpiryLeeway > DateTimeOffset.UtcNow)
            {
                return tokens.AccessToken;
            }

            if (string.IsNullOrEmpty(tokens.RefreshToken))
            {
                throw new ApiTokenUnavailableException(
                    "Your API access token has expired and cannot be refreshed. Please sign in again.");
            }

            var refreshed = await this.RefreshAsync(tokens.RefreshToken, cancellationToken);

            this.tokenStore.Save(subject, refreshed);

            return refreshed.AccessToken;
        }

        private async Task<UserTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            var authority = this.configuration["KeyCloak:Authority"]?.TrimEnd('/')
                            ?? throw new InvalidOperationException("Missing configuration value 'KeyCloak:Authority'.");

            var tokenUrl = authority + "/protocol/openid-connect/token";

            var body = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = this.configuration["KeyCloak:ClientId"] ?? string.Empty,
                    ["client_secret"] = this.configuration["KeyCloak:ClientSecret"] ?? string.Empty,
                });

            var httpClient = this.httpClientFactory.CreateClient(HttpClientName);

            using var response = await httpClient.PostAsync(tokenUrl, body, cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiTokenUnavailableException(
                    "Your session could not be refreshed. Please sign in again.");
            }

            using var document = JsonDocument.Parse(content);

            var root = document.RootElement;

            var accessToken = root.GetProperty("access_token").GetString()
                              ?? throw new ApiTokenUnavailableException("The token refresh response did not contain an access token.");

            var newRefreshToken = root.TryGetProperty("refresh_token", out var refreshProperty)
                                      ? refreshProperty.GetString()
                                      : refreshToken;

            var expiresIn = root.TryGetProperty("expires_in", out var expiresProperty)
                                ? expiresProperty.GetInt32()
                                : 300;

            return new UserTokens(accessToken, newRefreshToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
        }
    }
}
