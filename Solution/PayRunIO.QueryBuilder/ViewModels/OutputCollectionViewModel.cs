namespace PayRunIO.QueryBuilder.ViewModels
{
    using System;

    using PayRunIO.Models.Reporting;
    using PayRunIO.Models.Reporting.Outputs;

    public class OutputCollectionViewModel : SelectableCollectionViewModel
    {
        public OutputCollectionViewModel(EntityGroup entityGroup, SelectableBase parent)
            : base(parent)
        {
            this.SourceCollection = entityGroup.Outputs;
            foreach (var child in this.SourceCollection)
            {
                this.AddChild(child);
            }
        }

        public override Type ChildType { get; } = typeof(OutputBase);

        public override void AddChild(object child)
        {
            if (child is OutputBase childToAdd)
            {
                var viewModel = new OutputViewModel(childToAdd, this);

                this.Children.Add(viewModel);

                if (!this.SourceCollection.Contains(childToAdd))
                {
                    this.SourceCollection.Add(childToAdd);
                }
            }
        }
    }
}