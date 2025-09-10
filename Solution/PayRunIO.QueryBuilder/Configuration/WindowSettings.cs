namespace PayRunIO.QueryBuilder.Configuration
{
    /// <summary>
    /// Configuration settings for the main window position and state.
    /// </summary>
    public class WindowSettings
    {
        /// <summary>
        /// Gets or sets the window top position.
        /// </summary>
        public double Top { get; set; } = 0;

        /// <summary>
        /// Gets or sets the window left position.
        /// </summary>
        public double Left { get; set; } = 0;

        /// <summary>
        /// Gets or sets the window width.
        /// </summary>
        public double Width { get; set; } = 800;

        /// <summary>
        /// Gets or sets the window height.
        /// </summary>
        public double Height { get; set; } = 600;

        /// <summary>
        /// Gets or sets a value indicating whether the window is maximized.
        /// </summary>
        public bool IsMaximized { get; set; } = false;
    }
}
