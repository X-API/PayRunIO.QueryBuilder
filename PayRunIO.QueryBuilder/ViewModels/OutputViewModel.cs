namespace PayRunIO.QueryBuilder.ViewModels
{
    using PayRunIO.Models.Reporting.Outputs;
    using PayRunIO.Models.Reporting.Outputs.Aggregate;

    public class OutputViewModel : SelectableElementWithCollectionViewModel<OutputBase>
    {
        public OutputViewModel(OutputBase element, SelectableBase parent)
            : base(element, parent)
        {
            if (element is AggregateOutputBase aggregateOutput)
            {
                this.Children.Add(new FilterCollectionViewModel(aggregateOutput, this));
            }
        }
    }
}