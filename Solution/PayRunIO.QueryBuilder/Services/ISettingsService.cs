using System;

using PayRunIO.QueryBuilder.Configuration;

namespace PayRunIO.QueryBuilder.Services
{
    /// <summary>
    /// Service interface for managing application settings.
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Raised after <see cref="SaveUserSettings"/> persists settings, so components holding
        /// settings-derived state (e.g. a configured LLM client) can rebuild themselves immediately.
        /// </summary>
        event EventHandler SettingsChanged;

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
