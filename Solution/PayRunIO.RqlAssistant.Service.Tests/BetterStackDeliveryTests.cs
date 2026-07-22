namespace PayRunIO.RqlAssistant.Service.Tests
{
    using System.Net;
    using System.Text;
    using System.Text.Json;

    using log4net;
    using log4net.Core;
    using log4net.Repository.Hierarchy;

    using NUnit.Framework;

    using PayRunIO.Logging.BetterStack;
    using PayRunIO.ReportBuilder.Logging;

    /// <summary>
    /// End to end check that a failed query reaches the wire intact. The unit tests cover the
    /// log4net properties; this one drives the real BetterStack appender against a local listener to
    /// confirm the query XML and diagnostic fields survive serialisation into the shipped JSON —
    /// the payload that failure analysis will actually be run against.
    /// </summary>
    [TestFixture]
    public class BetterStackDeliveryTests
    {
        private HttpListener listener = null!;

        private string endpoint = null!;

        private readonly List<string> received = new();

        private ManualResetEventSlim delivered = null!;

        [SetUp]
        public void SetUp()
        {
            this.received.Clear();
            this.delivered = new ManualResetEventSlim(false);

            // Port 0 is not supported by HttpListener, so take a free port from the OS first.
            var port = GetFreePort();
            this.endpoint = $"http://localhost:{port}/";

            this.listener = new HttpListener();
            this.listener.Prefixes.Add(this.endpoint);
            this.listener.Start();

            Task.Run(this.AcceptLoop);
        }

        [TearDown]
        public void TearDown()
        {
            this.listener.Stop();
            this.listener.Close();
            this.delivered.Dispose();
        }

        private async Task AcceptLoop()
        {
            while (this.listener.IsListening)
            {
                HttpListenerContext context;

                try
                {
                    context = await this.listener.GetContextAsync();
                }
                catch (Exception)
                {
                    return;
                }

                using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                {
                    this.received.Add(await reader.ReadToEndAsync());
                }

                context.Response.StatusCode = 202;
                context.Response.Close();

                this.delivered.Set();
            }
        }

        private static int GetFreePort()
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            return port;
        }

        [Test]
        public void FailedQuery_IsShippedWithQueryXmlAndCorrelationId()
        {
            var appender = new BetterStackLog4NetAppender
                {
                    Name = "TestBetterStack",
                    SourceToken = "test-token",
                    Endpoint = this.endpoint,
                    Service = "payrun-report-builder",
                    Environment = "test",
                    BatchSize = 1,
                    FlushPeriodMilliseconds = 100,
                    Layout = new log4net.Layout.PatternLayout("%message"),
                };

            appender.ActivateOptions();

            var hierarchy = (Hierarchy)LogManager.GetRepository();
            hierarchy.Root.AddAppender(appender);
            hierarchy.Root.Level = Level.All;
            hierarchy.Configured = true;
            hierarchy.RaiseConfigurationChanged(EventArgs.Empty);

            try
            {
                const string QueryXml = "<Query><Entity Group=\"Employe\" /></Query>";

                var diagnostics = new DiagnosticContext();
                var log = new QueryFailureLog(diagnostics);

                log.QueryRejected(QueryXml, 400, "Bad Request", "Unknown entity 'Employe'", QueryOrigin.Assistant);

                Assert.That(
                    this.delivered.Wait(TimeSpan.FromSeconds(10)),
                    Is.True,
                    "The appender did not deliver the log event.");

                var payload = JsonDocument.Parse(this.received[0]).RootElement;

                // Batches of one are posted as a bare object rather than an array.
                var entry = payload.ValueKind == JsonValueKind.Array ? payload[0] : payload;

                Assert.That(entry.GetProperty("level").GetString(), Is.EqualTo("error"));
                Assert.That(entry.GetProperty("service").GetString(), Is.EqualTo("payrun-report-builder"));
                Assert.That(entry.GetProperty("message").GetString(), Does.Contain("Unknown entity 'Employe'"));

                // Flattened to the top level by the appender, so a whole session can be pulled up.
                Assert.That(entry.GetProperty("correlationId").GetString(), Is.EqualTo(diagnostics.CorrelationId));

                var properties = entry.GetProperty("properties");

                Assert.That(properties.GetProperty("queryXml").GetString(), Is.EqualTo(QueryXml));
                Assert.That(properties.GetProperty("statusCode").GetInt32(), Is.EqualTo(400));
                Assert.That(properties.GetProperty("failureKind").GetString(), Is.EqualTo("ApiRejected"));
                Assert.That(properties.GetProperty("queryOrigin").GetString(), Is.EqualTo("Assistant"));
            }
            finally
            {
                hierarchy.Root.RemoveAppender(appender);
                appender.Close();
            }
        }
    }
}
