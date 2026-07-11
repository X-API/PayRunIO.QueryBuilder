namespace PayRunIO.QueryBuilder.Services
{
    using System;
    using System.IO;
    using System.Text.Json;

    using PayRunIO.QueryBuilder.Configuration;

    /// <summary>
    /// Service for managing application settings with JSON persistence.
    /// </summary>
    public class SettingsService : ISettingsService
    {
        /// <summary>
        /// The user settings path.
        /// </summary>
        private readonly string userSettingsPath;

        /// <summary>
        /// The user settings.
        /// </summary>
        private readonly UserSettings userSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsService"/> class.
        /// </summary>
        public SettingsService()
        {
            // Store user settings in user's AppData folder
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "PayRunIO", "QueryBuilder");

            Directory.CreateDirectory(appFolder);

            this.userSettingsPath = Path.Combine(appFolder, "usersettings.json");

            // Load user settings
            this.userSettings = this.LoadUserSettings();
        }

        /// <inheritdoc />
        public event EventHandler SettingsChanged;

        /// <inheritdoc />
        public UserSettings UserSettings => this.userSettings;

        /// <inheritdoc />
        public void SaveUserSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(
                    this.userSettings,
                    new JsonSerializerOptions
                        {
                            WriteIndented = true
                        });

                File.WriteAllText(this.userSettingsPath, json);

                this.SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                // Log error but don't crash the application
                System.Diagnostics.Debug.WriteLine($"Failed to save user settings: {ex.Message}");
            }
        }

        /// <summary>
        /// The load user settings method.
        /// </summary>
        /// <returns>
        /// The <see cref="UserSettings"/>.
        /// </returns>
        private UserSettings LoadUserSettings()
        {
            try
            {
                if (File.Exists(this.userSettingsPath))
                {
                    var json = File.ReadAllText(this.userSettingsPath);
                    var settings = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
                    MigrateOpenAiEndpoint(settings.OpenAI);
                    return settings;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load user settings: {ex.Message}");
            }

            return new UserSettings();
        }

        /// <summary>
        /// Old settings stored the full chat-completions URL in <see cref="OpenAISettings.Endpoint"/>
        /// (e.g. "https://api.openai.com/v1/chat/completions"). The connection dialog now only takes a
        /// host, and the completions path is inferred per provider — so on load, strip a recognised full-path
        /// suffix down to host-only and, if <see cref="OpenAISettings.Provider"/> was never set, infer it
        /// from which suffix matched. The migrated value is not written back to disk here; it is only
        /// persisted the next time the user explicitly saves AI Settings.
        /// </summary>
        private static void MigrateOpenAiEndpoint(OpenAISettings openAi)
        {
            if (string.IsNullOrWhiteSpace(openAi.Endpoint))
            {
                return;
            }

            var trimmedEndpoint = openAi.Endpoint.TrimEnd('/');
            var hasProvider = !string.IsNullOrWhiteSpace(openAi.Provider);

            if (trimmedEndpoint.EndsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                openAi.Endpoint = trimmedEndpoint.Substring(0, trimmedEndpoint.Length - "/v1/chat/completions".Length);

                if (!hasProvider)
                {
                    openAi.Provider = "OpenAI";
                }
            }
            else if (trimmedEndpoint.EndsWith("/v1/messages", StringComparison.OrdinalIgnoreCase))
            {
                openAi.Endpoint = trimmedEndpoint.Substring(0, trimmedEndpoint.Length - "/v1/messages".Length);

                if (!hasProvider)
                {
                    openAi.Provider = "Anthropic";
                }
            }
            else if (trimmedEndpoint.EndsWith("/v1/responses", StringComparison.OrdinalIgnoreCase))
            {
                openAi.Endpoint = trimmedEndpoint.Substring(0, trimmedEndpoint.Length - "/v1/responses".Length);

                if (!hasProvider)
                {
                    openAi.Provider = "OpenAI (Responses)";
                }
            }
        }
    }
}
