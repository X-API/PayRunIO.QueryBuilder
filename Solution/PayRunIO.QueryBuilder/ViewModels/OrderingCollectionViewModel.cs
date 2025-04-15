namespace PayRunIO.QueryBuilder.ViewModels
{
    using System;

    using PayRunIO.v2.Models.Reporting;
    using PayRunIO.v2.Models.Reporting.Sorting;

    public class OrderingCollectionViewModel : SelectableCollectionViewModel
    {
        public OrderingCollectionViewModel(EntityGroup entityGroup, SelectableBase parent)
            : base(parent)
        {
            this.SourceCollection = entityGroup.Ordering;
            foreach (var child in this.SourceCollection)
            {
                this.AddChild(child);
            }
        }

        public override Type ChildType { get; } = typeof(OrderByBase);

        public override void AddChild(object child)
        {
            if (child is OrderByBase childToAdd)
            {
                var viewModel = new OrderingViewModel(childToAdd, this);

                this.Children.Add(viewModel);

                if (!this.SourceCollection.Contains(childToAdd))
                {
                    this.SourceCollection.Add(childToAdd);
                }
            }
        }
    }
}