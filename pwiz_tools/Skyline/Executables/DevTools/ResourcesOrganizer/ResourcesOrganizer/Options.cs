using System.Security.Policy;
using CommandLine;

namespace ResourcesOrganizer
{
    public class Options
    {
        [Option("db", Default = "resources.db")]
        public string DbFile { get; set; }
    }

    [Verb("add", HelpText = "Adds resources to a database")]
    public class AddVerb : Options
    {
        [Value(0, MetaName = "path", Required = true, HelpText = ".resx, directory, or resources.db")]
        public IEnumerable<string> Path { get; set; }
        [Option("exclude")]
        public IEnumerable<string> Exclude { get; set; }
        [Option("createnew", Default = false)]
        public bool CreateNew { get; set; }
        [Option("output")]
        public string? Output { get; set; }

    }

    [Verb("importtranslations")]
    public class ImportTranslations : Options
    {
        [Value(0, MetaName = "oldDb", Required = true)]
        public string OldDb { get; set; }
        [Option("language")]
        public IEnumerable<string> Language { get; set; }
        [Option("output")]
        public string? Output { get; set; }

    }

    [Verb("exportResx", HelpText = "Export .resx files to a .zip")]
    public class ExportResx : Options
    {
        [Value(0, MetaName = "output", Required = true)]
        public string Output { get; set; }
        [Option("overrideAll", Default = false)]
        public bool OverrideAll { get; set; }
    }

    [Verb("exportLocalizationCsv")]
    public class ExportLocalizationCsv : Options
    {
        [Value(0, MetaName = "output", Required = true)]
        public string Output { get; set; }

        [Option("language")]
        public string Language { get; set; }
}
}
