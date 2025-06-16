namespace PayRunIO.QueryBuilder.Helpers
{
    using System.Windows.Input;

    public static class CommonAiCommands
    {
        public static RoutedUICommand NewAiQueryCommand = new RoutedUICommand(
            "Create new query",
            "NewAiQuery",
            typeof(CommonAiCommands));

        public static RoutedUICommand EditAiQueryCommand = new RoutedUICommand(
            "Edit query",
            "EditAiQuery",
            typeof(CommonAiCommands));

        public static RoutedUICommand QuestionAiQueryCommand = new RoutedUICommand(
            "Question query",
            "QuestionAiQuery",
            typeof(CommonAiCommands));

        public static RoutedUICommand ErrorAiQueryCommand = new RoutedUICommand(
            "Error query",
            "ErrorAiQuery",
            typeof(CommonAiCommands));

        public static RoutedUICommand AskAiQueryCommand = new RoutedUICommand(
            "Ask question",
            "AskAiQuery",
            typeof(CommonAiCommands));
    }
}
