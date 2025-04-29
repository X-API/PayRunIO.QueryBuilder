namespace PayRunIO.QueryBuilder
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Xml;

    using ICSharpCode.AvalonEdit.Document;

    using PayRunIO.ConnectionControls;
    using PayRunIO.QueryBuilder.ViewModels;
    using PayRunIO.v2.CSharp.SDK;
    using PayRunIO.v2.Models;
    using PayRunIO.v2.Models.Reporting;
    using PayRunIO.v2.Models.Reporting.Outputs;
    using PayRunIO.v2.Models.Reporting.Outputs.Singular;
    using PayRunIO.v2.Models.Reporting.Sorting;

    using Button = System.Windows.Controls.Button;
    using File = PayRunIO.v2.Models.File;
    using ListBox = System.Windows.Controls.ListBox;
    using MessageBox = System.Windows.MessageBox;
    using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
    using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const string FileTypeFilterXmlDefault = "XML|*.xml|JSON|*.json";

        private const string FileTypeFilterJsonDefault = "JSON|*.json|XML|*.xml";

        private string fileName;

        private string originalState;

        private SelectableBase[] treeViewSource;

        private Query source;

        public MainWindow()
        {
            this.InitializeComponent();
            this.QueryResultViewer.ConnectionPicker.SelectConnectionByName(AppSettings.Default.LastConnection);
        }

        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register("SelectedItem", typeof(SelectableBase), typeof(MainWindow), new PropertyMetadata(default(SelectableBase), OnSelectedItemChanged));

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var mainWindow = (MainWindow)Application.Current.MainWindow;

            if (mainWindow != null)
            {
                mainWindow.OnPropertyChanged(nameof(MainWindow.XmlDocument));
                mainWindow.OnPropertyChanged(nameof(MainWindow.JsonDocument));
            }
        }

        public SelectableBase SelectedItem
        {
            get => (SelectableBase)this.GetValue(SelectedItemProperty);
            set => this.SetValue(SelectedItemProperty, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string FileName
        {
            get => this.fileName;
            set
            {
                this.fileName = value;
                this.OnPropertyChanged(nameof(this.FileName));
            }
        }

        public string OriginalState
        {
            get => this.originalState;
            private set
            {
                this.originalState = value;
                this.OnPropertyChanged(nameof(this.OriginalState));
            }
        }

        public ObservableCollection<string> FileHistory { get; } = new ObservableCollection<string>();

        public TextDocument XmlDocument => new TextDocument(this.source?.ToXml() ?? string.Empty);

        public TextDocument JsonDocument => new TextDocument(this.Source?.ToJson() ?? string.Empty);

        public Query Source
        {
            get => this.source;
            set
            {
                this.source = value;
                this.OnPropertyChanged(nameof(this.Source));
            }
        }

        public SelectableBase[] TreeViewSource
        {
            get => this.treeViewSource;
            set
            {
                this.treeViewSource = value;
                this.OnPropertyChanged(nameof(this.TreeViewSource));
            }
        }

        public string FileTypeFilter
        {
            get
            {
                if (string.IsNullOrEmpty(this.FileName))
                {
                    return FileTypeFilterXmlDefault;
                }

                var fileInfo = new FileInfo(this.FileName);

                return 
                    fileInfo.Extension.Equals(".json", StringComparison.InvariantCultureIgnoreCase) 
                        ? FileTypeFilterJsonDefault 
                        : FileTypeFilterXmlDefault;
            }
        }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private Query CreateNewQuery()
        {
            var newQuery = new Query { RootNodeName = "ResultSet" };

            this.Source = newQuery;

            newQuery.Variables.Add(NameValuePair.New("[EmployerKey]", "ER001"));

            var entityGroup = EntityGroup.New("Employees", "Employee", "/Employer/[EmployerKey]/Employees", "[EmployeeKey]");
            newQuery.Groups.Add(entityGroup);

            var outputA = RenderProperty.New("PayrollId", nameof(Employee.Code), output: OutputType.Attribute);
            var outputB = RenderProperty.New("FirstName", nameof(Employee.FirstName));
            var outputC = RenderProperty.New("LastName", nameof(Employee.LastName));
            entityGroup.Outputs.Add(outputA);
            entityGroup.Outputs.Add(outputB);
            entityGroup.Outputs.Add(outputC);

            var orderA = Ascending.New(nameof(Employee.LastName));
            var orderB = Ascending.New(nameof(Employee.FirstName));
            entityGroup.Ordering.Add(orderA);
            entityGroup.Ordering.Add(orderB);

            return newQuery;
        }

        private void AddVariable_OnClick(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;

            var list = (ListBox)button.CommandParameter;

            var nameValuePair = NameValuePair.New("[Name]", "Value");

            var targetQuery = this.Source;

            targetQuery.Variables.Add(nameValuePair);

            list.Items.Refresh();

            list.SelectedItem = nameValuePair;
        }

        private void RemoveVariable_OnClick(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;

            var list = (ListBox)button.CommandParameter;

            var selectedItems = list.SelectedItems.OfType<NameValuePair>().ToArray();

            foreach (var nameValuePair in selectedItems)
            {
                this.Source.Variables.Remove(nameValuePair);
            }

            list.Items.Refresh();
        }

        private void AddCondition_OnClick(object sender, RoutedEventArgs e)
        {
            ElementFactory.CreateCondition(sender, this.QueryTreeView.SelectedItem);
        }

        private void AddGroup_OnClick(object sender, RoutedEventArgs e)
        {
            ElementFactory.CreateNewEntityGroup(this.QueryTreeView.SelectedItem);
        }

        private void AddFilter_OnClick(object sender, RoutedEventArgs e)
        {
            ElementFactory.CreateFilter(sender, this.QueryTreeView.SelectedItem);
        }

        private void AddOutput_OnClick(object sender, RoutedEventArgs e)
        {
            ElementFactory.CreateOutput(sender, this.QueryTreeView.SelectedItem);
        }

        private void AddOrdering_OnClick(object sender, RoutedEventArgs e)
        {
            ElementFactory.CreateOrdering(sender, this.QueryTreeView.SelectedItem);
        }

        private void NewQuery_OnClick(object sender, RoutedEventArgs e)
        {
            if (!this.ConfirmReplaceSource())
            {
                return;
            }

            this.Source = this.CreateNewQuery();

            AppSettings.Default.LastFileName = string.Empty;
            this.FileName = string.Empty;
            this.OriginalState = string.Empty;

            this.TreeViewSource = new SelectableBase[] { new QueryViewModel((Query)this.Source) };

            this.OnPropertyChanged(nameof(this.XmlDocument));
            this.OnPropertyChanged(nameof(this.JsonDocument));
        }

        private bool ConfirmReplaceSource()
        {
            if (!this.IsDirty())
            {
                return true;
            }

            var result = MessageBox.Show(
                this,
                $"WARNING Unsaved changes will be lost!{Environment.NewLine}{Environment.NewLine}Continue and lose unsaved changes?",
                "Unsaved Changes Detected",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            return result == MessageBoxResult.OK;
        }

        private void SaveQueryAs_OnClick(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog { Filter = this.FileTypeFilter };

            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }

            var filePath = saveFileDialog.FileName;

            this.SaveSource(filePath);
        }

        private void SaveCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.IsDirty();
        }

        private bool IsDirty()
        {
            return this.OriginalState != (this.Source?.ToXml() ?? string.Empty);
        }

        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(this.FileName))
            {
                this.SaveQueryAs_OnClick(sender, e);
                return;
            }

            this.SaveSource(this.fileName);
        }

        private void Window_OnSourceInitialised(object? sender, EventArgs e)
        {
            if (AppSettings.Default.WindowTop > 0)
            {
                this.Top = AppSettings.Default.WindowTop;
            }

            if (AppSettings.Default.WindowLeft > 0)
            {
                this.Left = AppSettings.Default.WindowLeft;
            }

            if (AppSettings.Default.WindowHeight > 0)
            {
                this.Height = AppSettings.Default.WindowHeight;
            }

            if (AppSettings.Default.WindowWidth > 0)
            {
                this.Width = AppSettings.Default.WindowWidth;
            }

            if (AppSettings.Default.WindowMaximised)
            {
                this.WindowState = WindowState.Maximized;
            }

            var fileHistory = AppSettings.Default.FileHistory?.OfType<string>() ?? new string[0];

            foreach (var file in fileHistory)
            {
                this.FileHistory.Add(file);
            }

            if (!string.IsNullOrEmpty(AppSettings.Default.LastFileName))
            {
                this.FileName = AppSettings.Default.LastFileName;

                this.LoadFromFile(AppSettings.Default.LastFileName);
            }
            else
            {
                this.Source = this.CreateNewQuery();
                this.OriginalState = XmlSerialiserHelper.SerialiseToXmlDoc(this.source).InnerXml;
            }

            if (this.Source == null)
            {
                return;
            }

            SelectableBase root = new QueryViewModel(this.Source);

            var selectedItem = this.FindByIndex(root, AppSettings.Default.LastTreeIndex);

            if (selectedItem != null)
            {
                var parent = selectedItem.Parent;

                while (parent != null)
                {
                    parent.IsSelected = false;
                    parent.IsExpanded = true;
                    parent = parent.Parent;
                }

                selectedItem.IsSelected = true;
                selectedItem.IsExpanded = true;
            }

            this.TreeViewSource = new[] { root };

            this.OnPropertyChanged(nameof(this.FileName));
            this.OnPropertyChanged(nameof(this.TreeViewSource));
            this.OnPropertyChanged(nameof(this.XmlDocument));
            this.OnPropertyChanged(nameof(this.JsonDocument));
        }

        private SelectableBase FindByIndex(SelectableBase viewModel, int index)
        {
            if (viewModel.Index == index)
            {
                return viewModel;
            }

            if (viewModel is IHaveChildren viewModelCollection)
            {
                foreach (var child in viewModelCollection.Children)
                {
                    var result = this.FindByIndex(child, index);

                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        private void Window_OnClosing(object sender, CancelEventArgs e)
        {
            AppSettings.Default.LastConnection = this.QueryResultViewer.ConnectionPicker.SelectedConnection?.Name;

            ConnectionCollectionHelper.SaveConnections();

            if (this.WindowState == WindowState.Maximized)
            {
                AppSettings.Default.WindowTop = this.RestoreBounds.Top;
                AppSettings.Default.WindowLeft = this.RestoreBounds.Left;
                AppSettings.Default.WindowHeight = this.RestoreBounds.Height;
                AppSettings.Default.WindowWidth = this.RestoreBounds.Width;
                AppSettings.Default.WindowMaximised = true;
            }
            else
            {
                AppSettings.Default.WindowTop = this.Top;
                AppSettings.Default.WindowLeft = this.Left;
                AppSettings.Default.WindowHeight = this.Height;
                AppSettings.Default.WindowWidth = this.Width;
                AppSettings.Default.WindowMaximised = false;
            }

            if (!string.IsNullOrEmpty(this.FileName))
            {
                AppSettings.Default.LastFileName = this.FileName;
            }

            AppSettings.Default.FileHistory = new StringCollection();

            foreach (var file in this.FileHistory.Distinct().Take(10))
            {
                AppSettings.Default.FileHistory.Add(file);
            }

            if (this.QueryTreeView.SelectedItem != null)
            {
                AppSettings.Default.LastTreeIndex = this.QueryTreeView.SelectedItem.Index;
            }

            AppSettings.Default.Save();

            if (this.IsDirty())
            {
                if (!this.ConfirmReplaceSource())
                {
                    e.Cancel = true;
                }
            }
        }

        private void LoadFromFile(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                if (this.FileHistory.Contains(filePath))
                {
                    this.FileHistory.Remove(filePath);
                }

                return;
            }

            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open))
                {
                    XmlDocument xmlDoc;
                    if (filePath.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase))
                    {
                        xmlDoc = JsonSerialiserHelper.ConvertJsonStreamToXmlDocument(fileStream);
                    }
                    else
                    {
                        xmlDoc = new XmlDocument { PreserveWhitespace = true };
                        xmlDoc.Load(fileStream);
                    }

                    if (xmlDoc.DocumentElement?.Name == nameof(Query))
                    {
                        this.Source = XmlSerialiserHelper.Deserialise<Query>(xmlDoc.InnerXml);
                    }
                    else
                    {
                        throw new NotSupportedException($"Unable to load source data from file: '{filePath}'. Unexpected data type found.");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"An exception occured while loading file: {ex.Message}", "Error loading file", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            this.OriginalState = XmlSerialiserHelper.SerialiseToXmlDoc(this.Source).InnerXml;

            this.TreeViewSource = new SelectableBase[] { new QueryViewModel(this.Source) };

            AppSettings.Default.LastFileName = filePath;

            if (this.FileHistory.Contains(filePath))
            {
                var oldIndex = this.FileHistory.IndexOf(filePath);

                if (oldIndex != 0)
                {
                    this.FileHistory.Move(oldIndex, 0);
                }
            }
            else
            {
                this.FileHistory.Insert(0, filePath);
            }

            this.FileName = filePath;

            this.OnPropertyChanged(nameof(this.XmlDocument));
            this.OnPropertyChanged(nameof(this.JsonDocument));
        }

        private void SaveSource(string filePath)
        {
            if (filePath.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase))
            {
                System.IO.File.WriteAllText(filePath, this.source.ToJson());
            }
            else
            {
                System.IO.File.WriteAllText(filePath, this.source.ToXml());
            }

            if (this.FileHistory.Contains(filePath))
            {
                var oldIndex = this.FileHistory.IndexOf(filePath);

                if (oldIndex != 0)
                {
                    this.FileHistory.Move(oldIndex, 0);
                }
            }
            else
            {
                this.FileHistory.Insert(0, filePath);
            }

            this.FileName = filePath;
            this.OriginalState = this.Source.ToXml();
        }

        private void RefreshQuery_OnClick(object sender, RoutedEventArgs e)
        {
            this.OnPropertyChanged(nameof(this.XmlDocument));
            this.OnPropertyChanged(nameof(this.JsonDocument));
        }

        private void MoveUpCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            CommonTreeViewItemCommands.CanMoveSelectedItemUp(e);
        }

        private void MoveUpCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            CommonTreeViewItemCommands.MoveSelectedItemUp(e);
        }

        private void MoveDownCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            CommonTreeViewItemCommands.MoveSelectedItemDown(e);
        }

        private void MoveDownCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            CommonTreeViewItemCommands.CanMoveSelectedItemDown(e);
        }

        private void DeleteCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            CommonTreeViewItemCommands.CanDeleteSelectedItem(e);
        }

        private void DeleteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            CommonTreeViewItemCommands.DeleteSelectedItem(e);
        }

        private void Exit_OnClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OpenCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void OpenCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (!this.ConfirmReplaceSource())
            {
                return;
            }

            var openFileDialog = new OpenFileDialog { Filter = this.FileTypeFilter };

            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }

            this.LoadFromFile(openFileDialog.FileName);
        }

        private void About_OnClick(object sender, RoutedEventArgs e)
        {
            var assemblyName = Assembly.GetExecutingAssembly().GetName();

            var fullName = assemblyName.Name;

            var version = assemblyName.Version?.ToString() ?? "?????";

            MessageBox.Show(
                this,
                $"PayRun.io Query Builder - Version: {version}",
                $"About - {fullName}",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void FileSelection_OnChange(object sender, SelectionChangedEventArgs e)
        {
            if (AppSettings.Default.LastFileName == this.FileName)
            {
                // No change in selected file name
                return;
            }

            if (!this.ConfirmReplaceSource())
            {
                // Cancel file load
                this.FileName = AppSettings.Default.LastFileName;
            }
            else if (!string.IsNullOrEmpty(this.FileName))
            {
                // Load selected file
                this.LoadFromFile(this.FileName);
            }
        }

        private void UpdateQuery_OnClick(object sender, RoutedEventArgs e)
        {
            Query query = null;

            if (e.OriginalSource is Button button)
            {
                Func<Query> builder;

                if (button == this.UploadXml)
                {
                    builder = () => XmlSerialiserHelper.Deserialise<Query>(this.XmlTextEditor.Text);
                }
                else if (button == this.UploadJson)
                {
                    builder = () =>
                        {
                            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(this.JsonTextEditor.Text)))
                            {
                                var jsonToXml = JsonSerialiserHelper.ConvertJsonStreamToXmlDocument(stream);
                                return XmlSerialiserHelper.Deserialise<Query>(jsonToXml.InnerXml);
                            }
                        };
                }
                else
                {
                    return;
                }

                try
                {
                    query = builder();
                }
                catch (Exception ex)
                {
                    var messageFirstLine = ex.Message.Split(Environment.NewLine).First();

                    MessageBox.Show(this, $"Unable to apply XML query: {messageFirstLine}", "Invalid Query Definition", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                return;
            }

            var originalItemsFlatList = this.SelectableItemsAsFlatList(this.TreeViewSource).ToArray();

            var newTreeViewSource = new SelectableBase[] { new QueryViewModel(query) };

            var newItemsFlatList = this.SelectableItemsAsFlatList(newTreeViewSource).ToArray();

            for (int i = 0; i < originalItemsFlatList.Length; i++)
            {
                var original = originalItemsFlatList[i];

                if (i >= newItemsFlatList.Length)
                {
                    break;
                }

                var newItem = newItemsFlatList[i];

                newItem.IsExpanded = original.IsExpanded;
                newItem.IsSelected = original.IsSelected;
            }

            this.Source = query;
            this.TreeViewSource = newTreeViewSource;
        }

        private IEnumerable<SelectableBase> SelectableItemsAsFlatList(SelectableBase[] source)
        {
            foreach (var item in source)
            {
                yield return item;

                if (item is IHaveChildren parent && parent.Children.Any())
                {
                    foreach (var child in this.SelectableItemsAsFlatList(parent.Children.ToArray()))
                    {
                        yield return child;
                    }
                }
            }
        }
    }
}
