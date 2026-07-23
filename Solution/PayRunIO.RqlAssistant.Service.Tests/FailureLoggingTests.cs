namespace PayRunIO.RqlAssistant.Service.Tests
{
    using System.Security.Claims;

    using log4net;
    using log4net.Appender;
    using log4net.Core;
    using log4net.Repository.Hierarchy;

    using NUnit.Framework;

    using PayRunIO.ReportBuilder.Logging;
    using PayRunIO.RqlAssistant.Service.Models;

    /// <summary>
    /// Covers the diagnostic logging that records report query failures. The value of these logs is
    /// entirely in the structured properties — the BetterStack appender ships them as queryable
    /// fields, and analysis of failure trends depends on them being present and correctly named.
    /// </summary>
    [TestFixture]
    public class FailureLoggingTests
    {
        private MemoryAppender appender = null!;

        private DiagnosticContext diagnostics = null!;

        [SetUp]
        public void SetUp()
        {
            this.appender = new MemoryAppender { Threshold = Level.All };

            var hierarchy = (Hierarchy)LogManager.GetRepository();
            hierarchy.Root.AddAppender(this.appender);
            hierarchy.Root.Level = Level.All;
            hierarchy.Configured = true;
            hierarchy.RaiseConfigurationChanged(EventArgs.Empty);

            this.diagnostics = new DiagnosticContext();
        }

        [TearDown]
        public void TearDown()
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            hierarchy.Root.RemoveAppender(this.appender);
            this.appender.Close();
        }

        private LoggingEvent SingleEvent()
        {
            var events = this.appender.GetEvents();

            Assert.That(events, Has.Length.EqualTo(1), "Expected exactly one log event.");

            return events[0];
        }

        /// <summary>Reads the property straight off the event, exactly as the BetterStack appender
        /// does — not via GetLoggingEventData(), which fixes the event and would mask a property
        /// that only resolves from the ambient context.</summary>
        private static object? Property(LoggingEvent loggingEvent, string key) =>
            loggingEvent.Properties[key];

        [Test]
        public void QueryRejected_RecordsFullQueryXmlAndStatus()
        {
            const string QueryXml = "<Query><Entity Group=\"Employe\" /></Query>";

            var log = new QueryFailureLog(this.diagnostics);

            log.QueryRejected(QueryXml, 400, "Bad Request", "Unknown entity 'Employe'", QueryOrigin.Assistant);

            var loggingEvent = this.SingleEvent();

            Assert.That(loggingEvent.Level, Is.EqualTo(Level.Error));
            Assert.That(loggingEvent.RenderedMessage, Does.Contain("400 Bad Request"));
            Assert.That(loggingEvent.RenderedMessage, Does.Contain("Unknown entity 'Employe'"));

            // The whole query, untruncated: a failure that cannot be replayed cannot be fixed.
            Assert.That(Property(loggingEvent, "queryXml"), Is.EqualTo(QueryXml));
            Assert.That(Property(loggingEvent, "statusCode"), Is.EqualTo(400));
            Assert.That(Property(loggingEvent, "failureKind"), Is.EqualTo("ApiRejected"));
            Assert.That(Property(loggingEvent, "queryOrigin"), Is.EqualTo("Assistant"));
        }

        [Test]
        public void QueryFaulted_RecordsExceptionAndQuery()
        {
            var log = new QueryFailureLog(this.diagnostics);
            var exception = new HttpRequestException("connection reset");

            log.QueryFaulted("<Query />", exception, QueryOrigin.UserEdited);

            var loggingEvent = this.SingleEvent();

            Assert.That(loggingEvent.Level, Is.EqualTo(Level.Error));
            Assert.That(loggingEvent.ExceptionObject, Is.SameAs(exception));
            Assert.That(Property(loggingEvent, "failureKind"), Is.EqualTo("Faulted"));
            Assert.That(Property(loggingEvent, "queryOrigin"), Is.EqualTo("UserEdited"));
        }

        [Test]
        public void CorrelationId_IsStampedOnEveryEvent()
        {
            var log = new QueryFailureLog(this.diagnostics);

            log.QueryRejected("<Query />", 500, "Server Error", "boom", QueryOrigin.Assistant);

            Assert.That(
                Property(this.SingleEvent(), "correlationId"),
                Is.EqualTo(this.diagnostics.CorrelationId),
                "Failures must be tied to a session so a user's whole run can be reconstructed.");
        }

        [Test]
        public void UserSubject_IsRecordedWhenSignedIn()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "user-123") }));

            this.diagnostics.Capture(principal);

            var log = new QueryFailureLog(this.diagnostics);

            log.QueryRejected("<Query />", 400, "Bad Request", "boom", QueryOrigin.Assistant);

            Assert.That(Property(this.SingleEvent(), "userSubject"), Is.EqualTo("user-123"));
        }

        [Test]
        public void AmbientContext_IsNotMutated()
        {
            var log = new QueryFailureLog(this.diagnostics);

            log.QueryRejected("<Query />", 400, "Bad Request", "boom", QueryOrigin.Assistant);

            // Properties are attached to the event itself rather than pushed onto an ambient store.
            // Blazor circuits pool async continuations across users, so a value left behind in the
            // context would leak one user's query onto an unrelated log event.
            Assert.That(LogicalThreadContext.Properties["queryXml"], Is.Null);
            Assert.That(LogicalThreadContext.Properties["correlationId"], Is.Null);
            Assert.That(LogicalThreadContext.Properties["userSubject"], Is.Null);
        }

        [Test]
        public void Properties_SurviveWithoutFixingTheEvent()
        {
            var log = new QueryFailureLog(this.diagnostics);

            log.QueryRejected("<Query />", 400, "Bad Request", "boom", QueryOrigin.Assistant);

            // The BetterStack appender reads LoggingEvent.Properties directly without fixing the
            // event, and log4net only merges the ambient context stores into that collection on
            // fix. Properties therefore have to be set on the event or they never reach the wire.
            var properties = this.SingleEvent().Properties;

            Assert.That(properties["queryXml"], Is.EqualTo("<Query />"));
            Assert.That(properties["correlationId"], Is.EqualTo(this.diagnostics.CorrelationId));
        }

        [Test]
        public void ValidationFailed_RecordsDiagnosticCodes()
        {
            var log = new AssistantFailureLog(this.diagnostics);

            var diagnosticList = new[]
                {
                    new ValidationDiagnostic
                        {
                            Severity = ValidationSeverity.Error,
                            Code = "RQL001",
                            Line = 3,
                            Message = "Unknown route"
                        },
                    new ValidationDiagnostic
                        {
                            Severity = ValidationSeverity.Warning,
                            Code = "RQL014",
                            Line = 7,
                            Message = "Deprecated property"
                        }
                };

            log.ValidationFailed(1, diagnosticList, "<Query />", "list all employees");

            var loggingEvent = this.SingleEvent();

            // Codes are the aggregation key: "which rules does the model break most often".
            Assert.That(Property(loggingEvent, "diagnosticCodes"), Is.EqualTo("RQL001,RQL014"));
            Assert.That(Property(loggingEvent, "diagnostics")?.ToString(), Does.Contain("Unknown route"));
            Assert.That(Property(loggingEvent, "diagnosticCount"), Is.EqualTo(2));
            Assert.That(Property(loggingEvent, "correctionAttempt"), Is.EqualTo(1));
            Assert.That(Property(loggingEvent, "prompt"), Is.EqualTo("list all employees"));
            Assert.That(loggingEvent.Level, Is.EqualTo(Level.Info));
        }

        [Test]
        public void ValidationFailed_MessageCarriesTheFullFailureDetail()
        {
            var log = new AssistantFailureLog(this.diagnostics);

            var diagnosticList = new[]
                {
                    new ValidationDiagnostic
                        {
                            Severity = ValidationSeverity.Error,
                            Code = "RQL001",
                            Line = 3,
                            Message = "Unknown route '/Employer/{id}/Employes'"
                        },
                    new ValidationDiagnostic
                        {
                            Severity = ValidationSeverity.Warning,
                            Code = "RQL014",
                            Line = 7,
                            Message = "Deprecated property 'EmployeeCount'"
                        }
                };

            log.ValidationFailed(1, diagnosticList, "<Query><Entity /></Query>", "count employees per employer");

            var loggingEvent = this.SingleEvent();
            var message = loggingEvent.RenderedMessage;

            // The whole point of the change: a reviewer must be able to see what was wrong and what
            // was asked for without pulling the structured fields up separately.
            Assert.That(message, Does.Contain("Unknown route '/Employer/{id}/Employes'"));
            Assert.That(message, Does.Contain("Deprecated property 'EmployeeCount'"));
            Assert.That(message, Does.Contain("line 3"));
            Assert.That(message, Does.Contain("line 7"));
            Assert.That(message, Does.Contain("count employees per employer"));

            // The compact code summary survives alongside the detail so BetterStack still has a
            // short, stable fragment to group failures on.
            Assert.That(message, Does.Contain("Error/RQL001"));

            // The query body is the largest part of the event and already travels as a property, so
            // the message notes it rather than repeating it.
            Assert.That(message, Does.Not.Contain("<Query><Entity /></Query>"));
            Assert.That(message, Does.Contain("redacted"));
            Assert.That(Property(loggingEvent, "queryXml"), Is.EqualTo("<Query><Entity /></Query>"));
        }

        [Test]
        public void ValidationRecovered_RecordsWhatWasCorrected()
        {
            var log = new AssistantFailureLog(this.diagnostics);

            var diagnosticList = new[]
                {
                    new ValidationDiagnostic
                        {
                            Severity = ValidationSeverity.Error,
                            Code = "RQL007",
                            Line = 2,
                            Message = "Invalid loop expression"
                        }
                };

            log.ValidationRecovered(1, diagnosticList, "<Query />");

            var loggingEvent = this.SingleEvent();

            // A rule that is broken then fixed is still a grounding gap — the model only got there
            // on the second ask — so the codes have to be recorded on the recovered path too.
            Assert.That(Property(loggingEvent, "diagnosticCodes"), Is.EqualTo("RQL007"));
            Assert.That(Property(loggingEvent, "correctionsApplied"), Is.EqualTo(1));
            Assert.That(loggingEvent.RenderedMessage, Does.Contain("Invalid loop expression"));
        }

        [Test]
        public void NoQueryProduced_RecordsThePromptAndTheReply()
        {
            var log = new AssistantFailureLog(this.diagnostics);

            log.NoQueryProduced("show me gross pay", "I can help with that. What period?");

            var loggingEvent = this.SingleEvent();

            Assert.That(Property(loggingEvent, "prompt"), Is.EqualTo("show me gross pay"));

            // Whether the model answered conversationally can only be judged from the reply text.
            Assert.That(loggingEvent.RenderedMessage, Does.Contain("What period?"));

            // The message is the only copy — with no query to separate out, a "response" property
            // would be the same text stored twice.
            Assert.That(Property(loggingEvent, "response"), Is.Null);
            Assert.That(Property(loggingEvent, "responseLength"), Is.EqualTo(34));
        }

        [Test]
        public void ValidationUnresolved_IsLoggedAsWarning()
        {
            var log = new AssistantFailureLog(this.diagnostics);

            var diagnosticList = new[]
                {
                    new ValidationDiagnostic
                        {
                            Severity = ValidationSeverity.Error,
                            Code = "RQL002",
                            Line = 1,
                            Message = "Bad property"
                        }
                };

            log.ValidationUnresolved(diagnosticList, 2, "<Query />");

            var loggingEvent = this.SingleEvent();

            // Warning rather than info: these reached the user and are alertable.
            Assert.That(loggingEvent.Level, Is.EqualTo(Level.Warn));
            Assert.That(Property(loggingEvent, "correctionsApplied"), Is.EqualTo(2));
            Assert.That(Property(loggingEvent, "diagnosticCodes"), Is.EqualTo("RQL002"));
        }

        [Test]
        public void ProviderFailed_IsSeparatedFromValidationFailures()
        {
            var log = new AssistantFailureLog(this.diagnostics);

            log.ProviderFailed(new InvalidOperationException("rate limited"), "user turn");

            var loggingEvent = this.SingleEvent();

            Assert.That(loggingEvent.Level, Is.EqualTo(Level.Error));
            Assert.That(Property(loggingEvent, "phase"), Is.EqualTo("user turn"));

            // A provider outage says nothing about grounding quality, so it must not carry
            // diagnostic codes that would pull it into assistant-accuracy analysis.
            Assert.That(Property(loggingEvent, "diagnosticCodes"), Is.Null);
        }

        [Test]
        public void UserRequest_RecordsBothTheTypedAndTheComposedPrompt()
        {
            var log = new AssistantConversationLog(this.diagnostics);

            log.UserRequest(
                "add a column for net pay",
                "This is the current report query:\n<Query />\n\nadd a column for net pay",
                "<Query />",
                "400 Bad Request: unknown property",
                4);

            var loggingEvent = this.SingleEvent();

            Assert.That(loggingEvent.Level, Is.EqualTo(Level.Debug));
            Assert.That(Property(loggingEvent, "eventKind"), Is.EqualTo("UserRequest"));
            Assert.That(Property(loggingEvent, "prompt"), Is.EqualTo("add a column for net pay"));

            // The composed prompt is what the model actually saw, and the context folded into it is
            // frequently what steers the model wrong — so it is kept separately from the question.
            Assert.That(Property(loggingEvent, "effectivePrompt")?.ToString(), Does.Contain("current report query"));
            Assert.That(Property(loggingEvent, "currentQueryXml"), Is.EqualTo("<Query />"));
            Assert.That(Property(loggingEvent, "lastError")?.ToString(), Does.Contain("unknown property"));
            Assert.That(Property(loggingEvent, "historyCount"), Is.EqualTo(4));
            Assert.That(loggingEvent.RenderedMessage, Does.Contain("add a column for net pay"));
        }

        [Test]
        public void AssistantResponse_RecordsTheReplyVerbatim()
        {
            var log = new AssistantConversationLog(this.diagnostics);

            const string Response = "Here is the query you asked for:\n```xml\n<Query />\n```";

            log.AssistantResponse(Response, "<Query />", "user turn", TimeSpan.FromMilliseconds(1234));

            var loggingEvent = this.SingleEvent();

            Assert.That(loggingEvent.Level, Is.EqualTo(Level.Debug));
            Assert.That(Property(loggingEvent, "eventKind"), Is.EqualTo("AssistantResponse"));

            // A reply whose prose and XML disagree is a distinct failure mode, and it is only
            // visible from the reply text — which lives in the message, not a property.
            Assert.That(loggingEvent.RenderedMessage, Does.Contain("Here is the query you asked for"));
            Assert.That(Property(loggingEvent, "response"), Is.Null);
            Assert.That(Property(loggingEvent, "responseLength"), Is.EqualTo(Response.Length));

            Assert.That(Property(loggingEvent, "extractedQueryXml"), Is.EqualTo("<Query />"));
            Assert.That(Property(loggingEvent, "queryExtracted"), Is.True);
            Assert.That(Property(loggingEvent, "elapsedMs"), Is.EqualTo(1234L));
            Assert.That(Property(loggingEvent, "phase"), Is.EqualTo("user turn"));
        }

        [Test]
        public void AssistantResponse_RedactsTheExtractedQueryFromTheMessage()
        {
            var log = new AssistantConversationLog(this.diagnostics);

            const string QueryXml = "<Query><Entity Group=\"Employee\" /></Query>";
            const string Response = "Here is the query you asked for:\n```xml\n" + QueryXml + "\n```\nRun it to check.";

            log.AssistantResponse(Response, QueryXml, "user turn", TimeSpan.FromMilliseconds(500));

            var loggingEvent = this.SingleEvent();

            // A query is typically the bulk of a reply, and it already ships as its own property —
            // repeating it inside the rendered message doubles the volume of every turn for nothing.
            Assert.That(loggingEvent.RenderedMessage, Does.Not.Contain(QueryXml));
            Assert.That(loggingEvent.RenderedMessage, Does.Contain("[query redacted"));

            // The prose around the query is what the property does not carry, so it must survive.
            Assert.That(loggingEvent.RenderedMessage, Does.Contain("Here is the query you asked for"));
            Assert.That(loggingEvent.RenderedMessage, Does.Contain("Run it to check."));

            // Each half is stored exactly once — the prose in the message, the query in the
            // property — so the whole reply is still recoverable by joining them on the event.
            Assert.That(Property(loggingEvent, "extractedQueryXml"), Is.EqualTo(QueryXml));
            Assert.That(Property(loggingEvent, "response"), Is.Null);
            Assert.That(Property(loggingEvent, "responseLength"), Is.EqualTo(Response.Length));
        }

        [Test]
        public void AssistantResponse_LeavesTheReplyIntactWhenNoQueryWasExtracted()
        {
            var log = new AssistantConversationLog(this.diagnostics);

            const string Response = "Which tax year did you mean?";

            log.AssistantResponse(Response, null, "user turn", TimeSpan.FromSeconds(1));

            // Nothing to redact against: a reply with no query is short, and it is the case most
            // worth reading in full.
            Assert.That(this.SingleEvent().RenderedMessage, Does.Contain(Response));
        }

        [Test]
        public void AssistantResponse_FlagsAReplyWithNoQuery()
        {
            var log = new AssistantConversationLog(this.diagnostics);

            log.AssistantResponse("Which tax year did you mean?", null, "user turn", TimeSpan.FromSeconds(2));

            var loggingEvent = this.SingleEvent();

            Assert.That(Property(loggingEvent, "queryExtracted"), Is.False);
            Assert.That(Property(loggingEvent, "extractedQueryXml"), Is.Null);
            Assert.That(loggingEvent.RenderedMessage, Does.Contain("not extracted"));
        }

        [Test]
        public void CorrectionRequest_RecordsTheInstructionSentToTheModel()
        {
            var log = new AssistantConversationLog(this.diagnostics);

            log.CorrectionRequest(2, "The RQL query in your last reply failed validation: fix RQL001.");

            var loggingEvent = this.SingleEvent();

            Assert.That(Property(loggingEvent, "eventKind"), Is.EqualTo("CorrectionRequest"));
            Assert.That(Property(loggingEvent, "correctionAttempt"), Is.EqualTo(2));

            // Pairing the instruction with the reply it produced is what separates "the model
            // ignored the diagnostic" from "the diagnostic did not say enough to act on".
            Assert.That(Property(loggingEvent, "effectivePrompt")?.ToString(), Does.Contain("fix RQL001"));
        }

        [Test]
        public void TurnCompleted_RecordsTheQueryHandedBackToTheDesigner()
        {
            var log = new AssistantConversationLog(this.diagnostics);

            log.TurnCompleted("<Query />", 1, 0);

            var loggingEvent = this.SingleEvent();

            Assert.That(Property(loggingEvent, "eventKind"), Is.EqualTo("TurnCompleted"));
            Assert.That(Property(loggingEvent, "queryXml"), Is.EqualTo("<Query />"));
            Assert.That(Property(loggingEvent, "correctionsApplied"), Is.EqualTo(1));
            Assert.That(Property(loggingEvent, "outstandingDiagnostics"), Is.EqualTo(0));
        }

        [Test]
        public void ConversationEvents_CarryTheSessionCorrelationId()
        {
            var log = new AssistantConversationLog(this.diagnostics);

            log.UserRequest("anything", "anything", null, null, 0);

            // The transcript is only useful for failure analysis if it can be joined to the failure
            // events raised by the same session.
            Assert.That(
                Property(this.SingleEvent(), "correlationId"),
                Is.EqualTo(this.diagnostics.CorrelationId));
        }
    }
}
