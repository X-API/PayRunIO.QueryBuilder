namespace PayRunIO.QueryBuilder.ViewModels
{
    using System.ComponentModel;

    public abstract class SelectableBase : INotifyPropertyChanged
    {
        private static int NextIndex;

        private bool isSelected;

        private bool isExpanded;

        protected SelectableBase(SelectableBase parent)
        {
            this.Parent = parent;
            this.Index = NextIndex++;
        }

        public int Index { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        public SelectableBase Parent { get; }

        public bool IsSelected
        {
            get => this.isSelected;
            set
            {
                this.isSelected = value;
                this.OnPropertyChanged(nameof(this.IsSelected));
            }
        }

        public bool IsExpanded
        {
            get => this.isExpanded;
            set
            {
                this.isExpanded = value;
                this.OnPropertyChanged(nameof(this.IsExpanded));
            }
        }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}