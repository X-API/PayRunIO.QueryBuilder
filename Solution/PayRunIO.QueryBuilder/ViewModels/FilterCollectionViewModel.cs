namespace PayRunIO.QueryBuilder.ViewModels
{
    using System;

    using PayRunIO.v2.Models.Reporting;
    using PayRunIO.v2.Models.Reporting.Filtering;
    using PayRunIO.v2.Models.Reporting.Outputs.Aggregate;

    public class FilterCollectionViewModel : SelectableCollectionViewModel
    {
        public FilterCollectionViewModel(EntityGroup entityGroup, SelectableBase parent)
            : base(parent)
        {
            this.SourceCollection = entityGroup.Filters;
            foreach (var child in this.SourceCollection)
            {
                this.AddChild(child);
            }
        }

        public FilterCollectionViewModel(AggregateOutputBase aggregateOutput, SelectableBase parent)
            : base(parent)
        {
            this.SourceCollection = aggregateOutput.Filters;
            foreach (var child in this.SourceCollection)
            {
                this.AddChild(child);
            }
        }

        public override Type ChildType { get; } = typeof(FilterBase);

        public override void AddChild(object child)
        {
            if (child is FilterBase childToAdd)
            {
                var viewModel = new FilterViewModel(childToAdd, this);

                this.Children.Add(viewModel);

                if (!this.SourceCollection.Contains(childToAdd))
                {
                    this.SourceCollection.Add(childToAdd);
                }
            }
        }
    }
}