namespace PayRunIO.QueryBuilder.ViewModels
{
    using PayRunIO.Models.Reporting;

    public class GroupViewModel : SelectableElementWithCollectionViewModel<EntityGroup>
    {
        public GroupViewModel(EntityGroup entityGroup, SelectableBase parent)
            : base(entityGroup, parent)
        {
            this.Children.Add(new ConditionCollectionViewModel(entityGroup, this));
            this.Children.Add(new FilterCollectionViewModel(entityGroup, this));
            this.Children.Add(new OutputCollectionViewModel(entityGroup, this));
            this.Children.Add(new OrderingCollectionViewModel(entityGroup, this));
            this.Children.Add(new GroupCollectionViewModel(entityGroup, this));
        }
    }
}