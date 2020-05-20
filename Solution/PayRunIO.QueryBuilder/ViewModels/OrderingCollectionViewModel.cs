namespace PayRunIO.QueryBuilder.ViewModels
{
    using System.Linq;

    using PayRunIO.Models.Reporting;

    public class OrderingCollectionViewModel : SelectableCollectionViewModel
    {
        public OrderingCollectionViewModel(EntityGroup entityGroup, SelectableBase parent)
            : base(parent)
        {
            var viewModels = entityGroup.Ordering.Select(x => new OrderingViewModel(x, this));

            foreach (var viewModel in viewModels)
            {
                this.Children.Add(viewModel);
            }

            this.SourceCollection = entityGroup.Ordering;
        }
    }
}