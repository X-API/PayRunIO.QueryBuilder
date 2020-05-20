namespace PayRunIO.QueryBuilder.ViewModels
{
    public abstract class SelectableObjectViewModel : SelectableBase
    {
        protected SelectableObjectViewModel(object source, SelectableBase parent)
            : base(parent)
        {
            this.Source = source;
        }

        public object Source { get; }
    }
}