namespace PayRunIO.QueryBuilder.Configuration
{
    /// <summary>
    /// Configuration settings for OpenAI integration.
    /// </summary>
    public class OpenAISettings
    {
        /// <summary>
        /// Gets or sets the OpenAI API key.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the OpenAI endpoint URL.
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the OpenAI model name.
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the AI provider (e.g. "OpenAI", "Anthropic").
        /// </summary>
        public string Provider { get; set; } = "OpenAI";

        /// <summary>
        /// Gets or sets the temperature setting for OpenAI requests.
        /// </summary>
        public string Temperature { get; set; } = "0.2";

        /// <summary>
        /// Gets or sets the HTTP client timeout for OpenAI requests.
        /// </summary>
        public string HttpClientTimeout { get; set; } = "00:10:00";
    }
}
