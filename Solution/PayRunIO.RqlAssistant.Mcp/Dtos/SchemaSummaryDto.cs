namespace PayRunIO.RqlAssistant.Mcp.Dtos
{
    using System.ComponentModel;

    /// <summary>
    /// Lightweight wire-format DTO for a schema listing (name + description only, no properties).
    /// </summary>
    public sealed class SchemaSummaryDto
    {
        [Description("The schema type name, e.g. 'Employee'.")]
        public string Name { get; set; } = string.Empty;

        [Description("Human-readable description of the schema.")]
        public string Description { get; set; } = string.Empty;
    }
}
