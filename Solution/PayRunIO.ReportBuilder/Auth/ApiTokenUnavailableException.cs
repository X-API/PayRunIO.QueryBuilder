namespace PayRunIO.ReportBuilder.Auth
{
    /// <summary>
    /// Raised when no usable API access token exists for the current user (never signed in on this
    /// server instance, or the refresh grant was rejected). The UI should prompt a fresh sign in.
    /// </summary>
    public sealed class ApiTokenUnavailableException : Exception
    {
        public ApiTokenUnavailableException(string message)
            : base(message)
        {
        }
    }
}
