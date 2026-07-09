namespace PayRunIO.RqlAssistant.Service
{
    using System;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Diagnostic capture of RQL tool invocations, used to identify high-use example requests
    /// so the curated example bank can be expanded to match real usage. Wired into
    /// <see cref="RqlToolDispatcher.Dispatch"/> so both the WPF app and the MCP server are
    /// captured from one place. Off by default — set the RQL_TOOL_CAPTURE environment variable
    /// to "1" or "true" to re-activate.
    /// </summary>
    internal static class RqlToolCapture
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PayRunIO",
            "RqlAssistantMcp",
            "tool-capture.log");

        private static readonly object Lock = new();

        private static readonly bool Enabled = IsEnabled();

        private static bool IsEnabled()
        {
            var value = Environment.GetEnvironmentVariable("RQL_TOOL_CAPTURE");
            return string.Equals(value, "1", StringComparison.Ordinal)
                   || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        public static void Log(string tool, params (string Name, string? Value)[] args)
        {
            if (!Enabled)
            {
                return;
            }

            try
            {
                var sb = new StringBuilder();
                sb.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                sb.Append('\t').Append(tool);

                foreach (var (name, value) in args)
                {
                    sb.Append('\t').Append(name).Append('=').Append(string.IsNullOrEmpty(value) ? "<none>" : value);
                }

                lock (Lock)
                {
                    var dir = Path.GetDirectoryName(LogPath);
                    if (dir != null)
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.AppendAllText(LogPath, sb.ToString() + Environment.NewLine);
                }
            }
            catch
            {
                // Diagnostic capture must never break a tool call.
            }
        }
    }
}
