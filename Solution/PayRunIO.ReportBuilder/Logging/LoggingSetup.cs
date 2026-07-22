namespace PayRunIO.ReportBuilder.Logging
{
    using System.Diagnostics;

    using global::PayRunIO.Logging.BetterStack;

    using log4net;
    using log4net.Appender;
    using log4net.Repository.Hierarchy;

    /// <summary>
    /// Configures log4net for the Report Builder, following the pattern used by the PayRun.io API
    /// host: a static log4net.config declares the appenders, and the BetterStack credentials are
    /// applied at start up from configuration so no tokens are committed to source control.
    /// </summary>
    public static class LoggingSetup
    {
        private const string BetterStackAppenderName = "BetterStackAppender";

        /// <summary>
        /// The placeholder written into log4net.config in place of each BetterStack setting.
        /// </summary>
        private const string NotSet = "not-set";

        /// <summary>
        /// Registers log4net as the logging provider and applies the BetterStack settings from the
        /// "BetterStack" configuration section. When no source token is configured the BetterStack
        /// appender is removed, leaving console and debug output only — this is the expected state
        /// for local development and for deployments that do not ship logs centrally.
        /// </summary>
        /// <param name="builder">The web application builder.</param>
        public static void AddApplicationLogging(this WebApplicationBuilder builder)
        {
            // Matches the API host's "%P{pinfo}" layout token: process name and id, so log lines
            // can be attributed to an instance when several are behind the load balancer.
            var process = Process.GetCurrentProcess();
            GlobalContext.Properties["pinfo"] = $"{process.ProcessName}-{process.Id}";

            // Routes framework ILogger output through the same log4net configuration. The failure
            // logs in this namespace write to log4net directly, so their structured properties do
            // not depend on this bridge.
            builder.Logging.ClearProviders();
            builder.Logging.AddLog4Net("log4net.config");

            ConfigureBetterStackAppender(builder.Configuration.GetSection("BetterStack"), builder.Environment.EnvironmentName);
        }

        /// <summary>
        /// Flushes any queued log events. Called on application shutdown so failures recorded in the
        /// final seconds of a run are not lost with the process — BetterStack delivery is batched
        /// and asynchronous, so without this an unflushed batch is silently dropped.
        /// </summary>
        public static void ShutdownLogging() => LogManager.Shutdown();

        private static void ConfigureBetterStackAppender(IConfiguration settings, string environmentName)
        {
            var appender = LogManager
                .GetRepository()
                .GetAppenders()
                .OfType<BetterStackLog4NetAppender>()
                .FirstOrDefault(a => a.Name == BetterStackAppenderName);

            if (appender == null)
            {
                return;
            }

            var sourceToken = settings["SourceToken"];

            if (string.IsNullOrWhiteSpace(sourceToken) || sourceToken == NotSet)
            {
                RemoveBetterStackAppender(appender);
                return;
            }

            appender.SourceToken = sourceToken;
            appender.Endpoint = Coalesce(settings["Endpoint"], "https://in.logs.betterstack.com");
            appender.Service = Coalesce(settings["Service"], "payrun-report-builder");
            appender.Environment = Coalesce(settings["Environment"], environmentName);

            // Re-run the appender's own start up so the new endpoint/token are picked up by the
            // delivery loop rather than the "not-set" values it activated with.
            appender.ActivateOptions();
        }

        /// <summary>
        /// Detaches the unconfigured appender and closes it, so the delivery loop does not sit
        /// posting batches to the "not-set" endpoint for the lifetime of the process.
        /// </summary>
        private static void RemoveBetterStackAppender(IAppender appender)
        {
            if (LogManager.GetRepository() is Hierarchy hierarchy)
            {
                // Only the loggers it is actually attached to: log4net throws rather than no-opping
                // when asked to remove an appender a logger does not have.
                Detach(hierarchy.Root, appender);

                foreach (var logger in hierarchy.GetCurrentLoggers().OfType<Logger>())
                {
                    Detach(logger, appender);
                }
            }

            appender.Close();
        }

        private static void Detach(Logger logger, IAppender appender)
        {
            if (logger.Appenders.Cast<IAppender>().Contains(appender))
            {
                logger.RemoveAppender(appender);
            }
        }

        private static string Coalesce(string? value, string fallback) =>
            string.IsNullOrWhiteSpace(value) || value == NotSet ? fallback : value;
    }
}
