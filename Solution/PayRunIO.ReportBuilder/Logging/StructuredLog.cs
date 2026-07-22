namespace PayRunIO.ReportBuilder.Logging
{
    using System.Reflection;

    using log4net;
    using log4net.Core;

    /// <summary>
    /// Raises log4net events with properties attached to the event itself.
    ///
    /// The context stores (<see cref="LogicalThreadContext"/> and friends) are only merged into
    /// <see cref="LoggingEvent.Properties"/> once an event has been "fixed", and the BetterStack
    /// appender reads that property directly without fixing. Setting the values on the event means
    /// the structured fields survive to the wire whether or not an appender fixes the event, which
    /// is what makes the failure log queryable rather than just a line of text.
    /// </summary>
    public static class StructuredLog
    {
        private static readonly Type DeclaringType = typeof(StructuredLog);

        /// <summary>
        /// Writes an event carrying the supplied structured properties.
        /// </summary>
        /// <param name="log">The logger to write to.</param>
        /// <param name="level">The severity.</param>
        /// <param name="message">The rendered message.</param>
        /// <param name="properties">The structured properties to attach.</param>
        /// <param name="exception">The exception to record, if any.</param>
        public static void Write(
            ILog log,
            Level level,
            string message,
            IReadOnlyDictionary<string, object?> properties,
            Exception? exception = null)
        {
            if (!log.Logger.IsEnabledFor(level))
            {
                return;
            }

            var loggingEvent = new LoggingEvent(
                DeclaringType,
                log.Logger.Repository,
                log.Logger.Name,
                level,
                message,
                exception);

            foreach (var pair in properties)
            {
                if (pair.Value != null)
                {
                    loggingEvent.Properties[pair.Key] = pair.Value;
                }
            }

            log.Logger.Log(loggingEvent);
        }
    }
}
