namespace PayRunIO.QueryBuilder.ViewModels
{
    using System.Linq;

    using PayRunIO.Models.Reporting;

    public class ConditionCollectionViewModel : SelectableCollectionViewModel
    {
        public ConditionCollectionViewModel(EntityGroup entityGroup, SelectableBase parent)
            : base(parent)
        {
            var conditionViewModels = entityGroup.Conditions.Select(x => new ConditionViewModel(x, this));

            foreach (var viewModel in conditionViewModels)
            {
                this.Children.Add(viewModel);
            }

            this.SourceCollection = entityGroup.Conditions;
        }
    }
}