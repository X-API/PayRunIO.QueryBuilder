using System.Collections.Generic;

namespace PayRunIO.QueryBuilder.Configuration
{
    /// <summary>
    /// Configuration settings for file operations and history.
    /// </summary>
    public class FileSettings
    {
        /// <summary>
        /// Gets or sets the last opened file name.
        /// </summary>
        public string LastFileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the last selected tree index.
        /// </summary>
        public int LastTreeIndex { get; set; } = 0;

        /// <summary>
        /// Gets or sets the file history list.
        /// </summary>
        public List<string> FileHistory { get; set; } = new List<string>();
    }
}
