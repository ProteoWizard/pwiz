using System;
using CommandLine;
using SkylineTool;

namespace ToolServiceCmd
{
    [Verb("GetReport", HelpText = "Outputs a report from Skyline. Defaults to reading the report definition from stdin.")]
    internal class GetReportCommand : BaseCommand
    {
        [Option(HelpText = "Use the named report instead of reading from stdin.")]
        public string ReportName { get; set; }

        public override int PerformCommand()
        {
            string strReportCsv;
            using (var client = GetSkylineToolClient())
            {
                if (!string.IsNullOrEmpty(ReportName))
                {
                    strReportCsv = client.GetReport(Program.TOOL_NAME, ReportName);
                }
                else
                {
                    string reportDefinition = Console.In.ReadToEnd();
                    strReportCsv = client.GetReportFromDefinition(reportDefinition);
                }
            }
            Console.Out.Write(strReportCsv);
            return 0;
        }
    }
}
