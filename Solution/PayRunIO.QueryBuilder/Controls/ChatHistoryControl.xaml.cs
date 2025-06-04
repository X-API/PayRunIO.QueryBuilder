namespace PayRunIO.QueryBuilder
{
    using System;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Threading;

    using PayRunIO.RqlAssistant.Service.Models;

    /// <summary>
    /// Interaction logic for ChatHistoryControl.xaml
    /// </summary>
    public partial class ChatHistoryControl : UserControl
    {
        public static readonly DependencyProperty MessagesSourceProperty = 
            DependencyProperty.Register(
                nameof(MessagesSource), 
                typeof(ObservableCollection<ChatMessage>), 
                typeof(ChatHistoryControl), 
                new PropertyMetadata(new ObservableCollection<ChatMessage>()));

        public ObservableCollection<ChatMessage> MessagesSource
        {
            get => (ObservableCollection<ChatMessage>)GetValue(MessagesSourceProperty);
            set => this.SetValue(MessagesSourceProperty, value);
        }

        public ChatHistoryControl()
        {
            this.InitializeComponent();

            if (this.MessagesSource != null)
            {
                this.AttachCollectionChangedHandler(this.MessagesSource);
            }
        }

        /// <summary>
        /// Attaches our CollectionChanged event handler to an ObservableCollection.
        /// </summary>
        private void AttachCollectionChangedHandler(ObservableCollection<ChatMessage> collection)
        {
            collection.CollectionChanged += this.Messages_CollectionChanged;
        }

        /// <summary>
        /// Every time the bound collection changes (items added/removed/moved), we scroll to bottom.
        /// We dispatch to the UI thread so that the visual elements have already been updated.
        /// </summary>
        private void Messages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                this.Dispatcher.BeginInvoke(
                    this.ScrollToBottom,
                    DispatcherPriority.Background);
            }
        }

        /// <summary>
        /// Helper to scroll the outer ScrollViewer all the way to the bottom.
        /// </summary>
        private void ScrollToBottom()
        {
            if (this.PART_ScrollViewer != null)
            {
                this.PART_ScrollViewer.ScrollToBottom();
            }
        }
    }
}
