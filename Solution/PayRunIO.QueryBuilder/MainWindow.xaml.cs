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
    using System.Text;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Xml;
    using System.Threading.Tasks;

    using ICSharpCode.AvalonEdit.Document;

    using Microsoft.Extensions.DependencyInjection;
    using PayRunIO.ConnectionControls;
    using PayRunIO.QueryBuilder.Helpers;
    using PayRunIO.QueryBuilder.ViewModels;
    using PayRunIO.QueryBuilder.Services;
    using PayRunIO.v2.CSharp.SDK;
    using PayRunIO.v2.Models;
    using PayRunIO.v2.Models.Reporting;
    using PayRunIO.v2.Models.Reporting.Outputs;
    using PayRunIO.v2.Models.Reporting.Outputs.Singular;
    using PayRunIO.v2.Models.Reporting.Sorting;

    using Button = System.Windows.Controls.Button;
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

        private readonly ISettingsService settingsService;

        private Query source;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        /// <param name="settingsService">The settings service.</param>
        public MainWindow(ISettingsService settingsService)
        {
            this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            this.InitializeComponent();
            this.QueryResultViewer.ConnectionPicker.SelectConnectionByName(this.settingsService.UserSettings.Connection.LastConnection);
        }

        /// <summary>
        /// Creates a new MainWindow instance using the application's service provider.
        /// </summary>
        /// <returns>A new MainWindow instance.</returns>
        public static MainWindow Create()
        {
            var app = (App)Application.Current;
            var settingsService = app.ServiceProvider.GetRequiredService<ISettingsService>();
            return new MainWindow(settingsService);
        }

        /// <summary>
        /// Gets the settings service from the application's service provider.
        /// </summary>
        /// <returns>The settings service instance.</returns>
        private ISettingsService GetSettingsService()
        {
            var app = (App)Application.Current;
            return app.ServiceProvider.GetRequiredService<ISettingsService>();
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

            this.settingsService.UserSettings.File.LastFileName = string.Empty;
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
            if (this.settingsService.UserSettings.Window.Top > 0)
            {
                this.Top = this.settingsService.UserSettings.Window.Top;
            }

            if (this.settingsService.UserSettings.Window.Left > 0)
            {
                this.Left = this.settingsService.UserSettings.Window.Left;
            }

            if (this.settingsService.UserSettings.Window.Height > 0)
            {
                this.Height = this.settingsService.UserSettings.Window.Height;
            }

            if (this.settingsService.UserSettings.Window.Width > 0)
            {
                this.Width = this.settingsService.UserSettings.Window.Width;
            }

            if (this.settingsService.UserSettings.Window.IsMaximized)
            {
                this.WindowState = WindowState.Maximized;
            }

            var fileHistory = this.settingsService.UserSettings.File.FileHistory ?? new List<string>();

            foreach (var file in fileHistory)
            {
                this.FileHistory.Add(file);
            }

            if (!string.IsNullOrEmpty(this.settingsService.UserSettings.File.LastFileName))
            {
                this.FileName = this.settingsService.UserSettings.File.LastFileName;

                this.LoadFromFile(this.settingsService.UserSettings.File.LastFileName);
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

            var selectedItem = this.FindByIndex(root, this.settingsService.UserSettings.File.LastTreeIndex);

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
            this.settingsService.UserSettings.Connection.LastConnection = this.QueryResultViewer.ConnectionPicker.SelectedConnection?.Name ?? string.Empty;

            ConnectionCollectionHelper.SaveConnections();

            if (this.WindowState == WindowState.Maximized)
            {
                this.settingsService.UserSettings.Window.Top = this.RestoreBounds.Top;
                this.settingsService.UserSettings.Window.Left = this.RestoreBounds.Left;
                this.settingsService.UserSettings.Window.Height = this.RestoreBounds.Height;
                this.settingsService.UserSettings.Window.Width = this.RestoreBounds.Width;
                this.settingsService.UserSettings.Window.IsMaximized = true;
            }
            else
            {
                this.settingsService.UserSettings.Window.Top = this.Top;
                this.settingsService.UserSettings.Window.Left = this.Left;
                this.settingsService.UserSettings.Window.Height = this.Height;
                this.settingsService.UserSettings.Window.Width = this.Width;
                this.settingsService.UserSettings.Window.IsMaximized = false;
            }

            if (!string.IsNullOrEmpty(this.FileName))
            {
                this.settingsService.UserSettings.File.LastFileName = this.FileName;
            }

            this.settingsService.UserSettings.File.FileHistory = new List<string>();

            foreach (var file in this.FileHistory.Distinct().Take(10))
            {
                this.settingsService.UserSettings.File.FileHistory.Add(file);
            }

            if (this.QueryTreeView.SelectedItem != null)
            {
                this.settingsService.UserSettings.File.LastTreeIndex = this.QueryTreeView.SelectedItem.Index;
            }

            this.settingsService.SaveUserSettings();

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

            this.settingsService.UserSettings.File.LastFileName = filePath;

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
            if (this.settingsService.UserSettings.File.LastFileName == this.FileName)
            {
                // No change in selected file name
                return;
            }

            if (!this.ConfirmReplaceSource())
            {
                // Cancel file load
                this.FileName = this.settingsService.UserSettings.File.LastFileName;
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

            this.UpdateQuery(query);
        }

        public void UpdateQuery(Query query)
        {
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

        private void NewAiQueryCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void NewAiQueryCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var aiAssistantWindow =
                new AiAssistantWindow(this.GetSettingsService())
                    {
                        Owner = this,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        QuestionBox = { Text = "Create a new RQL statement that:\r\n- Lists all employees\r\n\r\nParameters:\r\n* Employer Key: ER001" },
                        Query = null
                    };

            aiAssistantWindow.ShowDialog();
        }

        private void EditAiQueryCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var aiAssistantWindow =
                new AiAssistantWindow(this.GetSettingsService())
                    {
                        Owner = this,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        QuestionBox = { Text = "Please amended this RQL statement as follows:\r\n\r\n- My change goes here..." },
                        Query = this.Source
                    };

            aiAssistantWindow.ShowDialog();
        }

        private void EditAiQueryCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void QuestionAiQueryCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void QuestionAiQueryCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var aiAssistantWindow =
                new AiAssistantWindow(this.GetSettingsService())
                    {
                        Owner = this,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        QuestionBox = { Text = "Please clearly describe what this query does." },
                        Query = this.Source
                    };

            aiAssistantWindow.ShowDialog();
        }

        private void ErrorAiQueryCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var questionText =
                $"Please help me understand this error message:\r\n\r\n```xml\r\n{XmlSerialiserHelper.SerialiseToXmlDoc(this.QueryResultViewer.LastErrorModel).Beautify()}\r\n```";

            var aiAssistantWindow =
                new AiAssistantWindow(this.GetSettingsService())
                    {
                        Owner = this,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        QuestionBox = { Text = questionText },
                        Query = this.Source,
                        AutoProcessQuestion = true
                    };

            aiAssistantWindow.ShowDialog();
        }

        private void ErrorAiQueryCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.QueryResultViewer.LastErrorModel != null;
        }
    }
}
