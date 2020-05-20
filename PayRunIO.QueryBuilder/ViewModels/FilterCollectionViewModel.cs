namespace PayRunIO.QueryBuilder.ViewModels
{
    using System.Linq;

    using PayRunIO.Models.Reporting;
    using PayRunIO.Models.Reporting.Outputs.Aggregate;

    public class FilterCollectionViewModel : SelectableCollectionViewModel
    {
        public FilterCollectionViewModel(EntityGroup entityGroup, SelectableBase parent)
            : base(parent)
        {
            var viewModels = entityGroup.Filters.Select(x => new FilterViewModel(x, this));

            foreach (var viewModel in viewModels)
            {
                this.Children.Add(viewModel);
            }

            this.SourceCollection = entityGroup.Filters;
        }

        public FilterCollectionViewModel(AggregateOutputBase aggregateOutput, SelectableBase parent)
            : base(parent)
        {
            var viewModels = aggregateOutput.Filters.Select(x => new FilterViewModel(x, this));

            foreach (var viewModel in viewModels)
            {
                this.Children.Add(viewModel);
            }

            this.SourceCollection = aggregateOutput.Filters;
        }
    }
}