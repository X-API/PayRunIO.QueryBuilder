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
                    var settings = JsonSerializer.Deserialize<UserSettings>(json);
                    return settings ?? new UserSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load user settings: {ex.Message}");
            }

            return new UserSettings();
        }
    }
}
