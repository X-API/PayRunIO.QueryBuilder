namespace PayRunIO.QueryBuilder
{
    using System;
    using System.Linq;

    using PayRunIO.v2.Models;
    using PayRunIO.v2.Models.Reporting.Conditions;
    using PayRunIO.v2.Models.Reporting.Filtering;
    using PayRunIO.v2.Models.Reporting.Outputs;
    using PayRunIO.v2.Models.Reporting.Sorting;

    public class QueryTypeLists
    {
        public static Type[] QueryTypes { get; } =
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(asm => asm.FullName.StartsWith("PayRunIO."))
                .SelectMany(asm => asm.GetTypes())
                .Where(t => !t.IsAbstract && t.Namespace != null && t.Namespace.StartsWith("PayRunIO.v2.Models.Reporting"))
                .ToArray();

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
