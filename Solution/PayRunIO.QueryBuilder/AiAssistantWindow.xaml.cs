namespace PayRunIO.QueryBuilder
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using System.Windows;
    using System.Windows.Input;

    using Microsoft.Extensions.Configuration;

    using PayRunIO.RqlAssistant.Service;
    using PayRunIO.RqlAssistant.Service.Models;
    using PayRunIO.v2.CSharp.SDK;
    using PayRunIO.v2.Models.Reporting;

    /// <summary>
    /// Interaction logic for AiAssistantWindow.xaml
    /// </summary>
    public partial class AiAssistantWindow : Window, INotifyPropertyChanged
    {
        private readonly IRqlRagService rqlRagService;

        private string initialQueryAsXml;

        private bool isBusy;

        public static readonly DependencyProperty QueryProperty =
            DependencyProperty.Register(
                nameof(Query),
                typeof(Query),
                typeof(AiAssistantWindow),
                new PropertyMetadata(default(Query)));
        
        public static readonly DependencyProperty IncludeSchemasProperty = 
            DependencyProperty.Register(
                nameof(IncludeSchemas), 
                typeof(bool), 
                typeof(AiAssistantWindow), 
                new PropertyMetadata(default(bool)));

        public static readonly DependencyProperty IncludeRoutesProperty = 
            DependencyProperty.Register(
                nameof(IncludeRoutes), 
                typeof(bool), 
                typeof(AiAssistantWindow), 
                new PropertyMetadata(default(bool)));

        public static readonly DependencyProperty TabularQueryProperty = DependencyProperty.Register(nameof(TabularQuery), typeof(bool), typeof(AiAssistantWindow), new PropertyMetadata(default(bool)));

        public bool TabularQuery
        {
            get => (bool)GetValue(TabularQueryProperty);
            set => this.SetValue(TabularQueryProperty, value);
        }

        public bool IncludeRoutes
        {
            get => (bool)GetValue(IncludeRoutesProperty);
            set => this.SetValue(IncludeRoutesProperty, value);
        }

        public bool IncludeSchemas
        {
            get => (bool)GetValue(IncludeSchemasProperty);
            set => this.SetValue(IncludeSchemasProperty, value);
        }

        public Query Query
        {
            get => (Query)GetValue(QueryProperty);
            set
            {
                this.SetValue(QueryProperty, value);

                if (this.initialQueryAsXml == null)
                {
                    this.initialQueryAsXml = value?.ToXml() ?? string.Empty;
                }
            }
        }

        public bool IsBusy
        {
            get => this.isBusy;
            set
            {
                this.isBusy = value;
                this.OnPropertyChanged();
            }
        }

        public AiAssistantWindow()
        {
            // Load settings
            var configuration =
                new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

            // Create the services
            this.rqlRagService = ServiceFactory.CreateService(configuration);

            this.InitializeComponent();

            // Clear any existing chat history
            this.ChatHistoryControl.MessagesSource.Clear();
        }

        private async void OnAskClick(object sender, RoutedEventArgs e)
        {
            var question = this.QuestionBox.Text.Trim();

            if (string.IsNullOrEmpty(question))
            {
                return;
            }

            var queryAsXml = this.Query?.ToXml() ?? string.Empty;

            this.IsBusy = true;

            this.ChatHistoryControl.MessagesSource.Add(new ChatMessage { Role = ParticipantType.User, Text = question });

            question += "\r\n\r\n" + queryAsXml;

            this.QuestionBox.Text = string.Empty;

            string response;
            try
            {
                response = 
                    await
                        this.rqlRagService
                            .AskQuestion(
                                question,
                                includeSchemas: this.IncludeSchemas,
                                includeApiRoutes: this.IncludeRoutes,
                                chatHistory: this.ChatHistoryControl.MessagesSource,
                                format: this.TabularQuery ? ResponseType.TabularQuery : ResponseType.Conversation);
            }
            catch (OpenAiException exception)
            {
                this.ChatHistoryControl.MessagesSource.Add(new ChatMessage { Role = ParticipantType.System, Text = $"[{exception.GetType().Name}] - {exception.StatusCode} - {exception.Message}" });
                this.IsBusy = false;
                return;
            }

            // Find RQL within response:
            var xmlSections = Regex.Matches(response, "```xml\\s*([\\s\\S]*?)\\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match match in xmlSections)
            {
                var innerCode = match.Groups[1].Value;

                if (innerCode.Contains("<Query", StringComparison.InvariantCultureIgnoreCase))
                {
                    try
                    {
                        var sourceXml = SetUtf8(innerCode);

                        this.Query = XmlSerialiserHelper.Deserialise<Query>(sourceXml);

                        response = response.Replace(match.Value, string.Empty);
                    }
                    catch (InvalidOperationException ex)
                    {
                        MessageBox.Show(
                            this,
                            "The assistants response could not be parsed into a valid query.\r\n\r\n" + ex.Message,
                            "Invalid Query Response",
                            MessageBoxButton.OK,
                            MessageBoxImage.Exclamation);
                    }
                }
            }

            // Record the chat history for additional questions
            this.ChatHistoryControl.MessagesSource.Add(new ChatMessage { Role = ParticipantType.Assistant, Text = response });

            this.IsBusy = false;
        }

        private static string SetUtf8(string xml) => xml.Replace(" encoding=\"utf-16\"", " encoding=\"utf-8\"");

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnApplyClick(object sender, RoutedEventArgs e)
        {
            if (this.Query != null)
            {
                var owner = (MainWindow)this.Owner;

                owner.UpdateQuery(this.Query);
            }

            this.Close();
        }

        private bool HasQueryChanges()
        {
            var currentQueryAsXml = this.Query?.ToXml() ?? string.Empty;
            return this.initialQueryAsXml != currentQueryAsXml;
        }

        private void SaveCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.HasQueryChanges();
        }

        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.Query != null)
            {
                var owner = (MainWindow)this.Owner;

                owner.UpdateQuery(this.Query);
            }

            this.Close();
        }

        private void CloseCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.HasQueryChanges())
            {
                var msgBox = MessageBox.Show("The query has been updated. Do you want to discard the changes?", "Query Changes Detected", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (msgBox == MessageBoxResult.No)
                {
                    return;
                }
            }

            this.Close();
        }
    }
}
