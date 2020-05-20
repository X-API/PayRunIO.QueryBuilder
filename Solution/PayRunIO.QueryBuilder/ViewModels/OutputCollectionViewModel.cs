namespace PayRunIO.QueryBuilder.ViewModels
{
    using System.Linq;

    using PayRunIO.Models.Reporting;

    public class OutputCollectionViewModel : SelectableCollectionViewModel
    {
        public OutputCollectionViewModel(EntityGroup entityGroup, SelectableBase parent)
            : base(parent)
        {
            var viewModels = entityGroup.Outputs.Select(x => new OutputViewModel(x, this));

            foreach (var viewModel in viewModels)
            {
                this.Children.Add(viewModel);
            }

            this.SourceCollection = entityGroup.Outputs;
        }
    }
}