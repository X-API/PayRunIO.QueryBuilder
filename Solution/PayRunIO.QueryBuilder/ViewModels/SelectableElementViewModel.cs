namespace PayRunIO.QueryBuilder.ViewModels
{
    public abstract class SelectableElementViewModel<TElement> : SelectableObjectViewModel
    {
        protected SelectableElementViewModel(TElement source, SelectableBase parent)
            : base(source, parent)
        {
            this.Element = source;
        }

        public TElement Element { get; }
    }
}