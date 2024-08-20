using System;
using CommandLine;

namespace ToolServiceCmd
{
    class Program
    {
        public const string TOOL_NAME = "ToolServiceCmd";
        static int Main(string[] args)
        {
            var parseResult = Parser.Default.ParseArguments(args, 
                typeof(GetVersionCommand),
                typeof(GetReportCommand),
                typeof(GetDocumentPath)
            );
            Parsed<object> parsed = parseResult as Parsed<object>;
            if (parsed == null)
                return 1;
            return ((BaseCommand) parsed.Value).PerformCommand();
        }

        [Verb("GetVersion")]
        public class GetVersionCommand : BaseCommand
        {
            public override int PerformCommand()
            {
                using (var client = GetSkylineToolClient())
                {
                    Console.Out.WriteLine("{0}", client.GetVersion());
                }
                return 0;
            }
        }

        [Verb("GetDocumentPath")]
        public class GetDocumentPath : BaseCommand
        {
            public override int PerformCommand()
            {
                using (var client = GetSkylineToolClient())
                {
                    Console.Out.WriteLine("{0}", client.GetDocumentPath());
                }
                return 0;
            }
        }
    }
}
