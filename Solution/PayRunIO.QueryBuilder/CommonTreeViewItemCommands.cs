namespace PayRunIO.QueryBuilder
{
    using System.Windows;
    using System.Windows.Input;

    using PayRunIO.QueryBuilder.ViewModels;

    public static class CommonTreeViewItemCommands
    {
        public static void CanMoveSelectedItemUp(CanExecuteRoutedEventArgs e)
        {
            var originalSource = (FrameworkElement)e.OriginalSource;

            var selectedItem = (SelectableBase)originalSource.DataContext;

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

        public static void MoveSelectedItemUp(ExecutedRoutedEventArgs e)
        {
            var originalSource = (FrameworkElement)e.OriginalSource;

            var selectedItem = (SelectableBase)originalSource.DataContext;

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

        public static void CanMoveSelectedItemDown(CanExecuteRoutedEventArgs e)
        {
            var originalSource = (FrameworkElement)e.OriginalSource;

            var selectedItem = (SelectableBase)originalSource.DataContext;

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

        public static void MoveSelectedItemDown(ExecutedRoutedEventArgs e)
        {
            var originalSource = (FrameworkElement)e.OriginalSource;

            var selectedItem = (SelectableBase)originalSource.DataContext;

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

        public static void CanDeleteSelectedItem(CanExecuteRoutedEventArgs e)
        {
            var originalSource = (FrameworkElement)e.OriginalSource;

            var selectedItem = (SelectableBase)originalSource.DataContext;

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

        public static void DeleteSelectedItem(ExecutedRoutedEventArgs e)
        {
            var originalSource = (FrameworkElement)e.OriginalSource;

            var selectedItem = (SelectableBase)originalSource.DataContext;

            if (selectedItem is SelectableObjectViewModel viewModel
                && viewModel.Parent is SelectableCollectionViewModel collectionViewModel)
            {
                collectionViewModel.SourceCollection.Remove(viewModel.Source);

                collectionViewModel.Children.Remove(viewModel);

                collectionViewModel.IsSelected = true;
            }
        }
    }
}