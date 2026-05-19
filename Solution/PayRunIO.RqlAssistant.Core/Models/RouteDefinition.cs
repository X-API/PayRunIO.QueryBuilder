namespace PayRunIO.RqlAssistant.Service.Models
{
    using System.Text;

    /// <summary>
    /// The route definition.
    /// </summary>
    public class RouteDefinition
    {
        /// <summary>
        /// Gets or sets the class name.
        /// </summary>
        public string ClassName { get; set; }

        /// <summary>
        /// Gets or sets the route.
        /// </summary>
        public string Route { get; set; }

        /// <summary>
        /// Gets or sets the route signature.
        /// </summary>
        public string RouteSignature { get; set; }

        /// <summary>
        /// Gets or sets the operation id.
        /// </summary>
        public string OperationId { get; set; }

        /// <summary>
        /// Gets or sets the verb.
        /// </summary>
        public string Verb { get; set; }

        /// <summary>
        /// Gets or sets the summary.
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the tags.
        /// </summary>
        public List<string> Tags { get; set; }

        /// <summary>
        /// Gets or sets the response code.
        /// </summary>
        public int ResponseCode { get; set; }

        /// <summary>
        /// Gets or sets the response type.
        /// </summary>
        public string ResponseType { get; set; }

        /// <summary>
        /// The to string override.
        /// </summary>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"# API Route Name: {this.ClassName}");
            sb.AppendLine($"* API Route Signature: {this.RouteSignature}");
            sb.AppendLine($"* Description: {this.Description}");
            sb.AppendLine($"* ResponseType: {this.ResponseType}");
            sb.AppendLine($"---");

            return sb.ToString();
        }
    }
}
