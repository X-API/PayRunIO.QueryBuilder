namespace PayRunIO.RqlAssistant.SchemaRefresh
{
    internal static class Program
    {
        private const string Usage = """
            schema-refresh — regenerate PayRunIO.RqlAssistant.Core/Resources from the
            PayRunIO.Models NuGet package and the API metadata CSV.

            Usage:
              schema-refresh <command> [<command> ...] [options]

            Commands:
              xsd       Rebuild QuerySchema.xsd from embedded XSDs in PayRunIO.v2.Models.
              routes    Convert Routes.csv to routes.json.
              dtos      Extract DTO definitions from PayRunIO.v2.Models (CLR + XSD).
              all       Run xsd, routes and dtos in that order.

            Options:
              --routes-csv <path>   Override the Routes.csv source path (defaults to the
                                    standard repo layout).
              --force               Allow xsd to overwrite even when type count regresses.
              -h, --help            Show this help.
            """;

        public static int Main(string[] args)
        {
            if (args.Length == 0 || args.Contains("-h") || args.Contains("--help"))
            {
                Console.WriteLine(Usage);
                return args.Length == 0 ? 1 : 0;
            }

            string? routesCsv = null;
            var force = false;
            var commands = new List<string>();

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--routes-csv":
                        if (i + 1 >= args.Length)
                        {
                            Console.Error.WriteLine("error: --routes-csv requires a path");
                            return 2;
                        }
                        routesCsv = args[++i];
                        break;
                    case "--force":
                        force = true;
                        break;
                    case "xsd":
                    case "routes":
                    case "dtos":
                    case "all":
                        commands.Add(args[i]);
                        break;
                    default:
                        Console.Error.WriteLine($"error: unknown argument '{args[i]}'");
                        Console.Error.WriteLine();
                        Console.Error.WriteLine(Usage);
                        return 2;
                }
            }

            if (commands.Contains("all"))
            {
                commands = new List<string> { "xsd", "routes", "dtos" };
            }

            try
            {
                foreach (var command in commands)
                {
                    var rc = command switch
                    {
                        "xsd" => XsdRebuilder.Run(force),
                        "routes" => RoutesConverter.Run(routesCsv),
                        "dtos" => DtoExtractor.Run(),
                        _ => throw new InvalidOperationException($"unhandled command: {command}")
                    };

                    if (rc != 0)
                    {
                        return rc;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                return 1;
            }

            return 0;
        }
    }
}
