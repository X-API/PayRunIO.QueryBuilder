namespace PayRunIO.QueryBuilder
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using System.Windows.Input;
    using System.Xml;

    using ICSharpCode.AvalonEdit.Document;

    using PayRunIO.CSharp.SDK;
    using PayRunIO.Models;
    using PayRunIO.Models.Reporting;
    using PayRunIO.Models.Reporting.Conditions;
    using PayRunIO.Models.Reporting.Filtering;
    using PayRunIO.Models.Reporting.Outputs.Singular;
    using PayRunIO.Models.Reporting.Sorting;
    using PayRunIO.QueryBuilder.ViewModels;

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

        private Query query;

        private SelectableBase[] treeViewSource;

        public MainWindow()
        {
            this.InitializeComponent();
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

        public TextDocument XmlDocument => new TextDocument(this.QueryAsXmlDoc().OuterXml);

        public TextDocument JsonDocument => new TextDocument(new StreamReader(JsonSerialiserHelper.Serialise(this.QueryAsXmlDoc(false))).ReadToEnd());

        public Query Query
        {
            get => this.query;
            private set
            {
                this.query = value;
                this.OnPropertyChanged(nameof(this.Query));
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

        private XmlDocument QueryAsXmlDoc(bool preserveWhiteSpace = true)
        {
            var xmlDoc = new XmlDocument { PreserveWhitespace = preserveWhiteSpace };

            if (this.Query != null)
            {
                xmlDoc.Load(XmlSerialiserHelper.Serialise(this.Query));
            }

            return xmlDoc;
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

        private void CreateNewQuery()
        {
            this.Query = new Query { RootNodeName = "MyQuery" };

            this.Query.Variables.Add(NameValuePair.New("[EmployerKey]", "ER001"));

            var entityGroup = EntityGroup.New("Employees", "Employee", "/Employer/[EmployerKey]/Employees", "[EmployeeKey]");
            this.Query.Groups.Add(entityGroup);

            var condition = When.New("ValueA", "ValueA");
            entityGroup.Conditions.Add(condition);

            var filterA = EqualTo.New(nameof(Employee.FirstName), "John");
            var filterB = GreaterThanEqualTo.New(nameof(Employee.StartDate), "2020-04-06");
            entityGroup.Filters.Add(filterA);
            entityGroup.Filters.Add(filterB);

            var outputA = RenderProperty.New("PayrollId", nameof(Employee.Code));
            var outputB = RenderProperty.New("FirstName", nameof(Employee.FirstName));
            var outputC = RenderProperty.New("LastName", nameof(Employee.LastName));
            entityGroup.Outputs.Add(outputA);
            entityGroup.Outputs.Add(outputB);
            entityGroup.Outputs.Add(outputC);

            var orderA = Ascending.New(nameof(Employee.LastName));
            var orderB = Ascending.New(nameof(Employee.FirstName));
            entityGroup.Ordering.Add(orderA);
            entityGroup.Ordering.Add(orderB);
        }

        private void AddVariable_OnClick(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;

            var list = (ListBox)button.CommandParameter;

            var nameValuePair = NameValuePair.New("[Name]", "Value");

            this.Query.Variables.Add(nameValuePair);

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
                this.Query.Variables.Remove(nameValuePair);
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
            if (!this.ConfirmReplaceQuery())
            {
                return;
            }

            this.CreateNewQuery();

            this.FileName = string.Empty;
            this.OriginalState = string.Empty;

            this.TreeViewSource = new SelectableBase[] { new QueryViewModel(this.Query) };

            this.OnPropertyChanged(nameof(this.XmlDocument));
            this.OnPropertyChanged(nameof(this.JsonDocument));
        }

        private bool ConfirmReplaceQuery()
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

        private void LoadQuery_OnClick(object sender, RoutedEventArgs e)
        {
            if (!this.ConfirmReplaceQuery())
            {
                return;
            }

            var openFileDialog = new OpenFileDialog { Filter = this.FileTypeFilter };

            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }

            this.LoadQueryFromFile(openFileDialog.FileName);
        }

        private void SaveQueryAs_OnClick(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog { Filter = this.FileTypeFilter };

            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }

            var filePath = saveFileDialog.FileName;

            this.SaveQuery(filePath);
        }

        private void SaveCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.IsDirty();
        }

        private bool IsDirty()
        {
            var currentState = 
                this.Query == null 
                    ? string.Empty 
                    : XmlSerialiserHelper.SerialiseToXmlDoc(this.Query).InnerXml;

            return this.OriginalState != currentState;
        }

        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(this.FileName))
            {
                this.SaveQueryAs_OnClick(sender, e);
                return;
            }

            this.SaveQuery(this.fileName);
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

            if (!string.IsNullOrEmpty(AppSettings.Default.LastFileName))
            {
                this.FileName = AppSettings.Default.LastFileName;

                this.LoadQueryFromFile(AppSettings.Default.LastFileName);
            }
            else
            {
                this.CreateNewQuery();
            }

            var root = new QueryViewModel(this.Query);

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

            this.TreeViewSource = new SelectableBase[] { root };

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

            if (this.QueryTreeView.SelectedItem != null)
            {
                AppSettings.Default.LastTreeIndex = ((SelectableBase)this.QueryTreeView.SelectedItem).Index;
            }

            ApiProfiles.Instance.Save();
        }

        private void LoadQueryFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open))
                {
                    if (filePath.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var xmlDoc = JsonSerialiserHelper.ConvertJsonStreamToXmlDocument(fileStream);
                        this.Query = XmlSerialiserHelper.Deserialise<Query>(xmlDoc.InnerXml);
                    }
                    else
                    {
                        this.Query =
                            XmlSerialiserHelper.DeserialiseDtoStream<Query>(fileStream);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"An exception occured while loading file: {ex.Message}", "Error loading file", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            this.OriginalState = XmlSerialiserHelper.SerialiseToXmlDoc(this.Query).InnerXml;
            this.TreeViewSource = new SelectableBase[] { new QueryViewModel(this.Query) };
            this.FileName = filePath;

            this.OnPropertyChanged(nameof(this.XmlDocument));
            this.OnPropertyChanged(nameof(this.JsonDocument));
        }

        private void SaveQuery(string filePath)
        {
            var xmlDoc = this.QueryAsXmlDoc(false);

            if (filePath.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase))
            {
                using (var sw = new StreamWriter(filePath))
                using (var sr = new StreamReader(JsonSerialiserHelper.Serialise(xmlDoc)))
                {
                    sw.Write(sr.ReadToEnd());
                    sw.Flush();
                }
            }
            else
            {
                xmlDoc.Save(filePath);
            }

            this.FileName = filePath;
            this.OriginalState = XmlSerialiserHelper.SerialiseToXmlDoc(this.Query).InnerXml;
        }

        private void RefreshQuery_OnClick(object sender, RoutedEventArgs e)
        {
            this.OnPropertyChanged(nameof(this.XmlDocument));
            this.OnPropertyChanged(nameof(this.JsonDocument));
        }
    }
}
