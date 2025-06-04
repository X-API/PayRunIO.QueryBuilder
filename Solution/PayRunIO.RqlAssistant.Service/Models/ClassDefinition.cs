namespace PayRunIO.RqlAssistant.Service.Models
{
    using System.Text;

    /// <summary>
    /// The class definition.
    /// </summary>
    public class ClassDefinition
    {
        /// <summary>
        /// Gets or sets the class name.
        /// </summary>
        public string ClassName { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the properties.
        /// </summary>
        public List<PropertyDefinition> Properties { get; set; } = new List<PropertyDefinition>();

        /// <summary>
        /// The to string override.
        /// </summary>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine($" * Class Name: {this.ClassName}");
            sb.AppendLine($" * Description: {this.Description}");
            sb.AppendLine($" * Properties: ");

            foreach (var property in this.Properties)
            {
                sb.AppendLine($"   {property}");
            }

            return sb.ToString();
        }
    }
}