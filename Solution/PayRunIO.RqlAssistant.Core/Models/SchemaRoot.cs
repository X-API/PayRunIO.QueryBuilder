namespace PayRunIO.RqlAssistant.Service.Models
{
    /// <summary>
    /// The schema root.
    /// </summary>
    public class SchemaRoot
    {
        /// <summary>
        /// Gets or sets the data.
        /// </summary>
        public List<ClassDefinition> Data { get; set; } = new List<ClassDefinition>();
    }
}
