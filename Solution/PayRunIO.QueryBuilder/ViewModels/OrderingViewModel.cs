namespace PayRunIO.QueryBuilder.ViewModels
{
    using PayRunIO.Models.Reporting.Sorting;

    public class OrderingViewModel : SelectableElementViewModel<OrderByBase>
    {
        public OrderingViewModel(OrderByBase element, SelectableBase parent)
            : base(element, parent)
        {
        }
    }
}