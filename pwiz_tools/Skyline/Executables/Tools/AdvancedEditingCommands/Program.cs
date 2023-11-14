using System.CommandLine;
using SkylineTool;

namespace AdvancedEditingCommands
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static async Task Main(string[] args)
        {
            var rootCommand = new RootCommand("Advanced editing commands");
            var skylineConnectionOption = new Option<string>("--SkylineConnection")
            {
                IsRequired = true
            };
            rootCommand.AddGlobalOption(skylineConnectionOption);
            var markTruncatedNonQuantitative = new Command("markTruncatedNonQuantitative",
                "Mark truncated transitions as non-quantitative");
            markTruncatedNonQuantitative.SetHandler(MarkTransitionsNonQuantitative, skylineConnectionOption);
            rootCommand.AddCommand(markTruncatedNonQuantitative);
            await rootCommand.InvokeAsync(args);
        }

        static void MarkTransitionsNonQuantitative(string skylineConnection)
        {
            var client = new RemoteClient(skylineConnection);
            
            var version = (SkylineTool.Version) client.RemoteCallName(nameof(IToolService.GetVersion), Array.Empty<object>());
            if (version.Major < 22 || version.Major == 22 && version.Minor <= 2 && version.Build == 0)
            {
                Console.Error.WriteLine("Error: This command requires Skyline version 22.2.1 or greater");
                return;
            }

            Console.Error.WriteLine("Fetching list of quantifiable truncated transitions from Skyline");
            var truncatedTransitionsReport = GetStringFromResource("TruncatedTransitions.skyr");
            var report = (string) client.RemoteCallName(nameof(IToolService.GetReportFromDefinition), new object[]{truncatedTransitionsReport});
            var locators = SplitLines(report).Skip(1).Distinct().ToList();
            if (locators.Count == 0)
            {
                Console.Error.WriteLine("No transitions to change");
                return;
            }

            Console.Error.WriteLine("Marking {0} transitions non-quantitative", locators.Count);
            var lines = new List<string> { "ElementLocator,property_Quantitative" };
            lines.AddRange(locators.Select(locator=>locator + ",False"));
            client.RemoteCallName(nameof(IToolService.ImportProperties),
                new object[] { string.Join(Environment.NewLine, lines) });
        }

        private static string GetStringFromResource(string resourceName)
        {
            using (var stream = typeof(Program).Assembly.GetManifestResourceStream(typeof(Program), resourceName))
            {
                if (stream == null)
                {
                    throw new FileNotFoundException($"Unable to find resource {resourceName}");
                }

                return new StreamReader(stream).ReadToEnd();
            }
        }

        private static string UnescapeCsv(string value)
        {
            if (value.StartsWith('"') && value.EndsWith('"'))
            {
                return value.Substring(1, value.Length - 2).Replace("\"\"", "\"");
            }

            return value;
        }

        private static IEnumerable<string> SplitLines(string value)
        {
            var allLines = value.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            if (allLines.Length > 0 && allLines[^1] == string.Empty)
            {
                return allLines.Take(allLines.Length - 1);
            }

            return allLines;
        }
    }
}