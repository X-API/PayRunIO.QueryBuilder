namespace PayRunIO.QueryBuilder.ViewModels
{
    using PayRunIO.v2.Models.Reporting.Filtering;

    public class FilterViewModel : SelectableElementViewModel<FilterBase>
    {
        public FilterViewModel(FilterBase element, SelectableBase parent)
            : base(element, parent)
        {
        }
    }
}