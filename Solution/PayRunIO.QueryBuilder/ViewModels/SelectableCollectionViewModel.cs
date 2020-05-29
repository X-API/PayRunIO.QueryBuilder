namespace PayRunIO.QueryBuilder.ViewModels
{
    using System;
    using System.Collections;
    using System.Collections.ObjectModel;

    public abstract class SelectableCollectionViewModel : SelectableBase, IHaveChildren
    {
        protected SelectableCollectionViewModel(SelectableBase parent)
            : base(parent)
        {
        }

        public ObservableCollection<SelectableBase> Children { get; } = new ObservableCollection<SelectableBase>();

        public IList SourceCollection { get; protected set; }

        public abstract Type ChildType { get; }

        public abstract void AddChild(object child);
    }
}