namespace PayRunIO.QueryBuilder.ViewModels
{
    using System;

    using PayRunIO.v2.Models.Reporting;
    using PayRunIO.v2.Models.Reporting.Conditions;

    public class ConditionCollectionViewModel : SelectableCollectionViewModel
    {
        public ConditionCollectionViewModel(EntityGroup entityGroup, SelectableBase parent)
            : base(parent)
        {
            this.SourceCollection = entityGroup.Conditions;
            foreach (var child in this.SourceCollection)
            {
                this.AddChild(child);
            }
        }

        public override Type ChildType { get; } = typeof(CompareConditionBase);

        public override void AddChild(object child)
        {
            if (child is CompareConditionBase childToAdd)
            {
                var viewModel = new ConditionViewModel(childToAdd, this);

                this.Children.Add(viewModel);

                if (!this.SourceCollection.Contains(childToAdd))
                {
                    this.SourceCollection.Add(childToAdd);
                }
            }
        }
    }
}