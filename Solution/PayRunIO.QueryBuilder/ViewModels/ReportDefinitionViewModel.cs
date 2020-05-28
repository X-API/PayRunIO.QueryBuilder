namespace PayRunIO.QueryBuilder.ViewModels
{
    using PayRunIO.Models;

    public class ReportDefinitionViewModel : SelectableElementWithCollectionViewModel<ReportDefinition>
    {
        public ReportDefinitionViewModel(ReportDefinition source)
            : base(source, null)
        {
            this.Children.Add(new QueryViewModel(source.ReportQuery, this));
        }
    }
}
