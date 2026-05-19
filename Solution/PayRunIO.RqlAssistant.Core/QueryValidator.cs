namespace PayRunIO.RqlAssistant.Service
{
    using System;
    using System.IO;
    using System.Xml;
    using System.Xml.Schema;

    using PayRunIO.RqlAssistant.Service.Models;

    public interface IQueryValidator
    {
        ValidationResult Validate(string xml);
    }

    public class QueryValidator : IQueryValidator
    {
        private readonly object syncLock = new object();

        private XmlSchemaSet? schemaSet;

        public ValidationResult Validate(string xml)
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(xml))
            {
                result.Diagnostics.Add(new ValidationDiagnostic
                    {
                        Severity = ValidationSeverity.Error,
                        Line = 0,
                        Column = 0,
                        Code = "EmptyInput",
                        Message = "The query XML is empty."
                    });

                return result;
            }

            var settings = new XmlReaderSettings
                               {
                                   ValidationType = ValidationType.Schema,
                                   Schemas = this.EnsureSchemaSet(),
                                   ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings
                                                     | XmlSchemaValidationFlags.ProcessIdentityConstraints
                               };

            settings.ValidationEventHandler += (_, e) =>
                {
                    result.Diagnostics.Add(new ValidationDiagnostic
                        {
                            Severity = e.Severity == XmlSeverityType.Warning
                                           ? ValidationSeverity.Warning
                                           : ValidationSeverity.Error,
                            Line = e.Exception?.LineNumber ?? 0,
                            Column = e.Exception?.LinePosition ?? 0,
                            Code = "XsdValidation",
                            Message = e.Message
                        });
                };

            try
            {
                using var stringReader = new StringReader(xml);
                using var xmlReader = XmlReader.Create(stringReader, settings);

                while (xmlReader.Read())
                {
                }
            }
            catch (XmlException xmlException)
            {
                result.Diagnostics.Add(new ValidationDiagnostic
                    {
                        Severity = ValidationSeverity.Error,
                        Line = xmlException.LineNumber,
                        Column = xmlException.LinePosition,
                        Code = "MalformedXml",
                        Message = xmlException.Message
                    });
            }

            return result;
        }

        private XmlSchemaSet EnsureSchemaSet()
        {
            if (this.schemaSet != null)
            {
                return this.schemaSet;
            }

            lock (this.syncLock)
            {
                if (this.schemaSet == null)
                {
                    var xsd = ResourceHelper
                        .LoadResourceAsStringAsync(ResourceHelper.QuerySchema)
                        .GetAwaiter()
                        .GetResult();

                    // The canonical QuerySchema.xsd references a 'RoundingOption' simpleType
                    // that is never declared in the file, which makes Compile() abort and
                    // leaves GlobalElements empty. Substitute the undeclared base with
                    // xsd:string so the rest of the schema compiles. The 'Rounding' attribute
                    // then accepts any string — finer enum-style checking belongs to a future
                    // Layer 2 semantic validator anyway.
                    xsd = xsd.Replace("base=\"RoundingOption\"", "base=\"xsd:string\"");

                    var set = new XmlSchemaSet();

                    // Schema-compile diagnostics fire on the set's ValidationEventHandler.
                    // The canonical QuerySchema.xsd has at least one unresolved type reference
                    // ('RoundingOption'); without a handler, Compile throws and the whole
                    // validator becomes unusable. Swallow schema-compile diagnostics so the
                    // rest of the schema remains usable for document validation.
                    set.ValidationEventHandler += (_, _) => { };

                    using var reader = XmlReader.Create(new StringReader(xsd));
                    set.Add(string.Empty, reader);
                    set.Compile();

                    this.schemaSet = set;
                }
            }

            return this.schemaSet;
        }
    }
}
