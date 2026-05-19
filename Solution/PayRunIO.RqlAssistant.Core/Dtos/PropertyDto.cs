namespace PayRunIO.RqlAssistant.Service.Dtos
{
    using System.ComponentModel;

    /// <summary>
    /// Wire-format DTO for a single property on a PayRunIO entity schema.
    /// </summary>
    public sealed class PropertyDto
    {
        [Description("The property name as it appears on the entity.")]
        public string Name { get; set; } = string.Empty;

        [Description("The property type as a verbatim string, e.g. 'string', 'DateTime?', 'List<PayLine>'.")]
        public string Type { get; set; } = string.Empty;

        [Description("Human-readable description of the property.")]
        public string Description { get; set; } = string.Empty;
    }
}
