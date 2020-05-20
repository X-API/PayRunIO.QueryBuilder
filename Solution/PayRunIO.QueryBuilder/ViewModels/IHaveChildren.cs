namespace PayRunIO.QueryBuilder.ViewModels
{
    using System.Collections.ObjectModel;

    public interface IHaveChildren
    {
        ObservableCollection<SelectableBase> Children { get; }
    }
}