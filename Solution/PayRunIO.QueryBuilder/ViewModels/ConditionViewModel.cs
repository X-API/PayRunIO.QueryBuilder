namespace PayRunIO.QueryBuilder.ViewModels
{
    using PayRunIO.Models.Reporting.Conditions;

    public class ConditionViewModel : SelectableElementViewModel<CompareConditionBase>
    {
        public ConditionViewModel(CompareConditionBase element, SelectableBase parent)
            : base(element, parent)
        {
        }
    }
}