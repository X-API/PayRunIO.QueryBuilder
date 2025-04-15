namespace PayRunIO.QueryBuilder.ViewModels
{
    using PayRunIO.v2.Models.Reporting;

    public class QueryViewModel : SelectableElementWithCollectionViewModel<Query>
    {
        public QueryViewModel(Query query, SelectableBase parent)
            : base(query, parent)
        {
            this.Children.Add(new GroupCollectionViewModel(query, this));
        }

        public QueryViewModel(Query query)
            : this(query, null)
        {
        }
    }
}
