using CommandLine;

namespace ResourcesOrganizer
{
    public class Options
    {
        [Option("db", Default = "resources.db")]
        public string DbFile { get; set; } = "resources.db";
    }

    [Verb("add", HelpText = "Adds resx files to a database")]
    public class AddVerb : Options
    {
        [Value(0, MetaName = "path", Required = true, HelpText = ".resx, directory, or resources.db")]
        public IEnumerable<string> Path { get; set; }

        [Option("exclude")] public IEnumerable<string> Exclude { get; set; } = [];
        [Option("createnew", Default = false)] public bool CreateNew { get; set; } = false;
        [Option("output")] public string? Output { get; set; }

    }

    [Verb("importLastVersion", HelpText = "Import translations from a database. Localized resources not found in the imported database are marked as needing review.")]
    public class ImportLastVersion : Options
    {
        [Value(0, MetaName = "oldDb", Required = true)]
        public string? OldDb { get; set; }

        [Option("language")] public IEnumerable<string> Language { get; set; } = [];
        [Option("output")] public string? Output { get; set; }="output";
    }

    [Verb("exportResx", HelpText = "Export .resx files to a .zip")]
    public class ExportResx : Options
    {
        [Value(0, MetaName = "output", Required = true)]
        public string Output { get; set; }

        [Option("overrideAll", Default = false)]
        public bool OverrideAll { get; set; } = false;
    }

    [Verb("exportLocalizationCsv")]
    public class ExportLocalizationCsv : Options
    {
        [Option("output", Default = "localization.csv")]
        public string? Output { get; set; } = "localization.csv";

        [Option("language")] public IEnumerable<string> Language { get; set; } = [];
    }

    [Verb("importLocalizationCsv")]
    public class ImportLocalizationCsv : Options
    {
        [Option("input", Default = "localization.csv")]
        public string? Input { get; set; } = "localization.csv";

        [Option("language")] public IEnumerable<string> Language { get; set; } = [];
    }
}
