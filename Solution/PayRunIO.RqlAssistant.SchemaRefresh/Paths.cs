namespace PayRunIO.RqlAssistant.SchemaRefresh
{
    internal static class Paths
    {
        public static string ResolveResourceDirectory()
        {
            var candidate = AppContext.BaseDirectory;

            for (var i = 0; i < 8; i++)
            {
                var probe = Path.Combine(
                    candidate,
                    "PayRunIO.RqlAssistant.Core",
                    "Resources");

                if (Directory.Exists(probe))
                {
                    return Path.GetFullPath(probe);
                }

                var parent = Directory.GetParent(candidate);
                if (parent == null)
                {
                    break;
                }

                candidate = parent.FullName;
            }

            throw new DirectoryNotFoundException(
                "Could not locate PayRunIO.RqlAssistant.Core/Resources directory. "
                + $"Started search from {AppContext.BaseDirectory}.");
        }

        public static string DefaultRoutesCsv()
        {
            // Standard repo layout: this tool lives at
            // ...\X-API\PayRunIO.QueryBuilder\Solution\PayRunIO.RqlAssistant.SchemaRefresh\bin\Debug\net8.0\
            // CSV lives at:
            // ...\X-API\PayRunIO_v2\Solutions\PayRunIO.v2\PayRunIO.v2.Metadata\Routes.csv
            var resourceDir = ResolveResourceDirectory();
            var solutionDir = Directory.GetParent(Path.GetDirectoryName(resourceDir)!)!.FullName;
            var queryBuilderDir = Directory.GetParent(solutionDir)!.FullName;
            var xApiDir = Directory.GetParent(queryBuilderDir)!.FullName;

            return Path.Combine(
                xApiDir,
                "PayRunIO_v2", "Solutions", "PayRunIO.v2", "PayRunIO.v2.Metadata", "Routes.csv");
        }

        // Write content to a final path via a sibling .tmp file then atomic rename.
        // Guards against half-written output when a step fails mid-write. Falls back
        // to direct overwrite if the OS refuses the rename (Windows can deny File.Replace
        // when the target is opened by another process for read).
        public static void WriteAtomic(string finalPath, string content)
        {
            var encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            var tempPath = finalPath + ".tmp";
            File.WriteAllText(tempPath, content, encoding);

            try
            {
                if (File.Exists(finalPath))
                {
                    File.Replace(tempPath, finalPath, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(tempPath, finalPath);
                }
            }
            catch (IOException)
            {
                // Fallback: best-effort direct overwrite. Not atomic, but unblocks the
                // common case where another process holds the target open for read.
                File.WriteAllText(finalPath, content, encoding);
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
    }
}
