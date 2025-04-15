namespace PayRunIO.QueryBuilder.ViewModels
{
    using PayRunIO.v2.Models.Reporting.Conditions;

    public class ConditionViewModel : SelectableElementViewModel<CompareConditionBase>
    {
        public ConditionViewModel(CompareConditionBase element, SelectableBase parent)
            : base(element, parent)
        {
        }
    }
}