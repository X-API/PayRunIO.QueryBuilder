namespace PayRunIO.QueryBuilder.ViewModels
{
    using PayRunIO.v2.Models.Reporting.Sorting;

    public class OrderingViewModel : SelectableElementViewModel<OrderByBase>
    {
        public OrderingViewModel(OrderByBase element, SelectableBase parent)
            : base(element, parent)
        {
        }
    }
}