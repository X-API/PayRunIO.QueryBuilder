﻿namespace PayRunIO.QueryBuilder
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;

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
            var selectedItem = this.MyTreeView.SelectedItem;

            if (!(selectedItem is SelectableObjectViewModel viewModel))
            {
                e.CanExecute = false;
                return;
            }

            if (!(viewModel.Parent is SelectableCollectionViewModel parent))
            {
                e.CanExecute = false;
                return;
            }

            var indexOf = parent.Children.IndexOf(viewModel);

            if (indexOf < 1)
            {
                e.CanExecute = false;
                return;
            }

            e.CanExecute = true;
        }

        private void MoveUpCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var selectedItem = this.MyTreeView.SelectedItem;

            if (!(selectedItem is SelectableObjectViewModel viewModel))
            {
                return;
            }

            if (!(viewModel.Parent is SelectableCollectionViewModel parent))
            {
                return;
            }

            var indexOf = parent.Children.IndexOf(viewModel);

            if (indexOf < 1)
            {
                return;
            }

            indexOf--;

            parent.Children.Remove(viewModel);
            parent.Children.Insert(indexOf, viewModel);
            parent.SourceCollection.Remove(viewModel.Source);
            parent.SourceCollection.Insert(indexOf, viewModel.Source);
        }

        private void MoveDownCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var selectedItem = this.MyTreeView.SelectedItem;

            if (!(selectedItem is SelectableObjectViewModel viewModel))
            {
                e.CanExecute = false;
                return;
            }

            if (!(viewModel.Parent is SelectableCollectionViewModel parent))
            {
                e.CanExecute = false;
                return;
            }

            var indexOf = parent.Children.IndexOf(viewModel);

            if (indexOf >= parent.Children.Count - 1)
            {
                e.CanExecute = false;
                return;
            }

            e.CanExecute = true;
        }

        private void MoveDownCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var selectedItem = this.MyTreeView.SelectedItem;

            if (!(selectedItem is SelectableObjectViewModel viewModel))
            {
                return;
            }

            if (!(viewModel.Parent is SelectableCollectionViewModel parent))
            {
                return;
            }

            var indexOf = parent.Children.IndexOf(viewModel);

            if (indexOf >= parent.Children.Count - 1)
            {
                return;
            }

            indexOf++;

            parent.Children.Remove(viewModel);
            parent.Children.Insert(indexOf, viewModel);
            parent.SourceCollection.Remove(viewModel.Source);
            parent.SourceCollection.Insert(indexOf, viewModel.Source);
        }


        private void DeleteCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var selectedItem = this.MyTreeView.SelectedItem;

            if (!(selectedItem is SelectableObjectViewModel viewModel))
            {
                e.CanExecute = false;
                return;
            }

            if (!(viewModel.Parent is SelectableCollectionViewModel collectionViewModel))
            {
                e.CanExecute = false;
                return;
            }

            e.CanExecute = collectionViewModel.SourceCollection.Contains(viewModel.Source);
        }

        private void DeleteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var selectedItem = this.MyTreeView.SelectedItem;

            if (selectedItem is SelectableObjectViewModel viewModel && viewModel.Parent is SelectableCollectionViewModel collectionViewModel)
            {
                collectionViewModel.SourceCollection.Remove(viewModel.Source);

                collectionViewModel.Children.Remove(viewModel);

                collectionViewModel.IsSelected = true;
            }
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
    }
}