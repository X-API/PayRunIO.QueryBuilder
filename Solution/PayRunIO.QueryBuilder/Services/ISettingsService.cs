using PayRunIO.QueryBuilder.Configuration;

namespace PayRunIO.QueryBuilder.Services
{
    /// <summary>
    /// Service interface for managing application settings.
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Gets the current user settings.
        /// </summary>
        UserSettings UserSettings { get; }

        /// <summary>
        /// Saves the current user settings to persistent storage.
        /// </summary>
        void SaveUserSettings();

    }
}
