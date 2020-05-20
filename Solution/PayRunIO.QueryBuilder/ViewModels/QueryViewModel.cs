namespace PayRunIO.QueryBuilder.ViewModels
{
    using PayRunIO.Models.Reporting;

    public class QueryViewModel : SelectableElementWithCollectionViewModel<Query>
    {
        public QueryViewModel(Query query)
            : base(query, null)
        {
            this.Children.Add(new GroupCollectionViewModel(query, this));
        }
    }
}
