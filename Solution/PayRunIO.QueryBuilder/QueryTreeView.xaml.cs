namespace PayRunIO.QueryBuilder
{
    using System;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Xml;

    using PayRunIO.v2.CSharp.SDK;
    using PayRunIO.QueryBuilder.ViewModels;

    /// <summary>
    /// Interaction logic for QueryTreeView.xaml
    /// </summary>
    public partial class QueryTreeView : UserControl
    {
        public QueryTreeView()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty TreeViewSourceProperty = DependencyProperty.Register("TreeViewSource", typeof(SelectableBase[]), typeof(QueryTreeView), new PropertyMetadata(default));

        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register("SelectedItem", typeof(SelectableBase), typeof(QueryTreeView), new PropertyMetadata(default(SelectableBase)));

        public SelectableBase SelectedItem
        {
            get => (SelectableBase)this.GetValue(SelectedItemProperty);
            set => this.SetValue(SelectedItemProperty, value);
        }

        public SelectableBase[] TreeViewSource
        {
            get => (SelectableBase[])this.GetValue(TreeViewSourceProperty);
            set => this.SetValue(TreeViewSourceProperty, value);
        }

        static TreeViewItem VisualUpwardSearch(DependencyObject source)
        {
            while (source != null && !(source is TreeViewItem))
            {
                source = VisualTreeHelper.GetParent(source);
            }

            return source as TreeViewItem;
        }

        private void TreeView_SelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            this.SelectedItem = this.MyTreeView.SelectedItem as SelectableBase;
        }

        private void MoveUpCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            CommonTreeViewItemCommands.CanMoveSelectedItemUp(e);
        }

        private void MoveUpCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            CommonTreeViewItemCommands.MoveSelectedItemUp(e);
        }

        private void MoveDownCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            CommonTreeViewItemCommands.CanMoveSelectedItemDown(e);
        }

        private void MoveDownCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            CommonTreeViewItemCommands.MoveSelectedItemDown(e);
        }

        private void DeleteCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            CommonTreeViewItemCommands.CanDeleteSelectedItem(e);
        }

        private void DeleteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            CommonTreeViewItemCommands.DeleteSelectedItem(e);
        }

        private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = VisualUpwardSearch(e.OriginalSource as DependencyObject);

            if (treeViewItem != null)
            {
                treeViewItem.Focus();
                e.Handled = true;
            }
        }

        private void CopyCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.SelectedItem is SelectableObjectViewModel;
        }

        private void CopyCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.SelectedItem is SelectableObjectViewModel selectedViewModel)
            {
                Clipboard.SetText(XmlSerialiserHelper.SerialiseToXmlDoc(selectedViewModel.Source).InnerXml);
            }
        }

        private void CutCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.SelectedItem is SelectableObjectViewModel viewModel && viewModel.Parent != null;
        }

        private void CutCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.SelectedItem is SelectableObjectViewModel selectedViewModel)
            {
                Clipboard.SetText(XmlSerialiserHelper.SerialiseToXmlDoc(selectedViewModel.Source).InnerXml);

                var parentCollection = selectedViewModel.Parent as SelectableCollectionViewModel;

                parentCollection.Children.Remove(selectedViewModel);
                parentCollection.SourceCollection.Remove(selectedViewModel.Source);
            }
        }

        private void PasteCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            object child = null;

            if (this.SelectedItem is SelectableCollectionViewModel selectedCollection)
            {
                this.TryGetPasteBufferAsObject(selectedCollection.ChildType, out child);
            }

            e.CanExecute = child != null;
        }

        private void PasteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.SelectedItem is SelectableCollectionViewModel selectedCollection)
            {
                if (this.TryGetPasteBufferAsObject(selectedCollection.ChildType, out var child))
                {
                    selectedCollection.AddChild(child);
                }
            }
        }

        private bool TryGetPasteBufferAsObject(Type desiredType, out object data)
        {
            data = null;

            var xml = Clipboard.GetText();

            if (!string.IsNullOrEmpty(xml))
            {
                try
                {
                    var xmlDoc = new XmlDocument();

                    xmlDoc.LoadXml(xml);

                    var typeName = xmlDoc.DocumentElement.Name;

                    var type = QueryTypeLists.QueryTypes.FirstOrDefault(t => t.Name == typeName);

                    if (type != null && desiredType.IsAssignableFrom(type))
                    {
                        data = XmlSerialiserHelper.Deserialise(type, xmlDoc.InnerXml);
                    }
                }
                catch (XmlException)
                {
                    // Invalid XML loaded
                }
            }

            return data != null;
        }
    }
}
