namespace PayRunIO.RqlAssistant.Service.Models
{
    using System.Text;

    /// <summary>
    /// The property definition.
    /// </summary>
    public class PropertyDefinition
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The to string override.
        /// </summary>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append($" * Name: {this.Name}");
            sb.Append($" * Type: {this.Type}");
            sb.Append($" * Description: {this.Description}");

            return sb.ToString();
        }
    }
}