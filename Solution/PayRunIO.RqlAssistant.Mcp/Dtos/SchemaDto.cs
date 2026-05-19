namespace PayRunIO.RqlAssistant.Mcp.Dtos
{
    using System.ComponentModel;

    /// <summary>
    /// Full wire-format DTO for a PayRunIO entity schema, including all property definitions.
    /// </summary>
    public sealed class SchemaDto
    {
        [Description("The schema type name, e.g. 'Employee'.")]
        public string Name { get; set; } = string.Empty;

        [Description("Human-readable description of the schema.")]
        public string Description { get; set; } = string.Empty;

        [Description("The properties exposed by this schema.")]
        public IReadOnlyList<PropertyDto> Properties { get; set; } = Array.Empty<PropertyDto>();
    }
}
