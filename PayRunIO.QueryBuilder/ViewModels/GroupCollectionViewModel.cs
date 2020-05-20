namespace PayRunIO.QueryBuilder.ViewModels
{
    using System.Linq;

    using PayRunIO.Models.Reporting;

    public class GroupCollectionViewModel : SelectableCollectionViewModel
    {
        public GroupCollectionViewModel(EntityGroup entityGroup, SelectableBase parent)
            : base(parent)
        {
            var viewModels = entityGroup.Groups.Select(x => new GroupViewModel(x, this));

            foreach (var viewModel in viewModels)
            {
                this.Children.Add(viewModel);
            }

            this.SourceCollection = entityGroup.Groups;
        }

        public GroupCollectionViewModel(Query query, SelectableBase parent)
            : base(parent)
        {
            var viewModels = query.Groups.Select(x => new GroupViewModel(x, this));

            foreach (var viewModel in viewModels)
            {
                this.Children.Add(viewModel);
            }

            this.SourceCollection = query.Groups;
        }
    }
}