namespace PayRunIO.ReportBuilder.Auth
{
    /// <summary>
    /// The KeyCloak issued token set held server side for a signed in user. The access token is
    /// presented as the bearer credential on PayRun.io API calls; the refresh token is used to
    /// obtain a replacement when the access token expires.
    /// </summary>
    public sealed record UserTokens(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt)
    {
        private const int DefaultLifetimeSeconds = 300;

        public static UserTokens FromTokenResponse(string accessToken, string? refreshToken, string? expiresIn)
        {
            var seconds = int.TryParse(expiresIn, out var parsed) ? parsed : DefaultLifetimeSeconds;

            return new UserTokens(accessToken, refreshToken, DateTimeOffset.UtcNow.AddSeconds(seconds));
        }
    }
}
