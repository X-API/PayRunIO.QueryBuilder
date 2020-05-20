namespace PayRunIO.QueryBuilder.ViewModels
{
    using System.Collections.ObjectModel;

    public abstract class SelectableElementWithCollectionViewModel<TElement> : SelectableElementViewModel<TElement>, IHaveChildren
    {
        protected SelectableElementWithCollectionViewModel(TElement source, SelectableBase parent)
            : base(source, parent)
        {
        }

        public ObservableCollection<SelectableBase> Children { get; } = new ObservableCollection<SelectableBase>();
    }
}