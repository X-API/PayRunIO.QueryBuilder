namespace PayRunIO.QueryBuilder.ViewModels
{
    using System;
    using System.Linq;

    using PayRunIO.Models.Reporting;

    public class GroupCollectionViewModel : SelectableCollectionViewModel
    {
        public GroupCollectionViewModel(EntityGroup entityGroup, SelectableBase parent)
            : base(parent)
        {
            this.SourceCollection = entityGroup.Groups;
            foreach (var child in this.SourceCollection)
            {
                this.AddChild(child);
            }
        }

        public GroupCollectionViewModel(Query query, SelectableBase parent)
            : base(parent)
        {
            this.SourceCollection = query.Groups;
            foreach (var child in this.SourceCollection)
            {
                this.AddChild(child);
            }
        }

        public override Type ChildType { get; } = typeof(EntityGroup);

        public override void AddChild(object child)
        {
            if (child is EntityGroup childToAdd)
            {
                var viewModel = new GroupViewModel(childToAdd, this);

                this.Children.Add(viewModel);

                if (!this.SourceCollection.Contains(childToAdd))
                {
                    this.SourceCollection.Add(childToAdd);
                }
            }
        }
    }
}