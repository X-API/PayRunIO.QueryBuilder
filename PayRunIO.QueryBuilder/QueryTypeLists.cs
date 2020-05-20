namespace PayRunIO.QueryBuilder
{
    using System.Linq;

    using PayRunIO.Models.Reporting.Conditions;
    using PayRunIO.Models.Reporting.Filtering;
    using PayRunIO.Models.Reporting.Outputs;
    using PayRunIO.Models.Reporting.Sorting;

    public class QueryTypeLists
    {
        public string[] ConditionTypes { get; } =
            typeof(CompareConditionBase).Assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && typeof(CompareConditionBase).IsAssignableFrom(t))
                .Select(t => t.Name)
                .ToArray();

        public string[] FilterTypes { get; } =
            typeof(FilterBase).Assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && typeof(FilterBase).IsAssignableFrom(t))
                .Select(t => t.Name)
                .ToArray();

        public string[] OutputTypes { get; } =
            typeof(OutputBase).Assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && typeof(OutputBase).IsAssignableFrom(t))
                .Select(t => t.Name)
                .ToArray();

        public string[] OrderByTypes { get; } =
            typeof(OrderByBase).Assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && typeof(OrderByBase).IsAssignableFrom(t))
                .Select(t => t.Name)
                .ToArray();
    }
}
