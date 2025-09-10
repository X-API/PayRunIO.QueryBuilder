namespace PayRunIO.QueryBuilder.Configuration
{
    /// <summary>
    /// User-specific settings that persist across application sessions.
    /// </summary>
    public class UserSettings
    {
        /// <summary>
        /// Gets or sets the window settings.
        /// </summary>
        public WindowSettings Window { get; set; } = new WindowSettings();

        /// <summary>
        /// Gets or sets the file settings.
        /// </summary>
        public FileSettings File { get; set; } = new FileSettings();

        /// <summary>
        /// Gets or sets the connection settings.
        /// </summary>
        public ConnectionSettings Connection { get; set; } = new ConnectionSettings();

        /// <summary>
        /// Gets or sets the OpenAI settings.
        /// </summary>
        public OpenAISettings OpenAI { get; set; } = new OpenAISettings();
    }
}
