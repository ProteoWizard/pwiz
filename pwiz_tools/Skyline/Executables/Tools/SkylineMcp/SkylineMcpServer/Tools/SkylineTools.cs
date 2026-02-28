/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using ModelContextProtocol.Server;

namespace SkylineMcpServer.Tools;

[McpServerToolType]
public static class SkylineTools
{
    [McpServerTool(Name = "skyline_get_document_path"),
     Description("Get the file path of the currently open Skyline document.")]
    public static string GetDocumentPath()
    {
        using var connection = SkylineConnection.Connect();
        string path = connection.Call("GetDocumentPath");
        return path ?? "No document is open in Skyline.";
    }

    [McpServerTool(Name = "skyline_get_version"),
     Description("Get the version of the running Skyline instance.")]
    public static string GetVersion()
    {
        using var connection = SkylineConnection.Connect();
        string version = connection.Call("GetVersion");
        return version ?? "Unknown version";
    }

    [McpServerTool(Name = "skyline_get_selection"),
     Description("Get the currently selected element in Skyline (protein, peptide, precursor, transition, etc.). Returns the name/description of whatever is selected in the document tree.")]
    public static string GetSelection()
    {
        using var connection = SkylineConnection.Connect();
        string selection = connection.Call("GetDocumentLocationName");
        return string.IsNullOrEmpty(selection)
            ? "Nothing is currently selected in Skyline."
            : selection;
    }

    [McpServerTool(Name = "skyline_get_replicate"),
     Description("Get the name of the currently active replicate in Skyline.")]
    public static string GetReplicate()
    {
        using var connection = SkylineConnection.Connect();
        string replicate = connection.Call("GetReplicateName");
        return string.IsNullOrEmpty(replicate)
            ? "No replicate is currently selected."
            : replicate;
    }

    [McpServerTool(Name = "skyline_get_report"),
     Description("Run a named Skyline report and return results. For large reports, saves full data to a CSV file and returns a summary with preview rows and the file path. Use the Read tool to explore the full dataset.")]
    public static string GetReport(
        [Description("The name of a Skyline report to run (e.g., 'Peak Area', 'Transition Results')")] string reportName)
    {
        using var connection = SkylineConnection.Connect();
        string csv = connection.Call("GetReport", reportName);
        return FormatReportResult(reportName, csv);
    }

    [McpServerTool(Name = "skyline_get_report_from_definition"),
     Description("Run a custom Skyline report from an XML report definition and return results. Use this when you need specific columns not available in predefined reports. The XML format follows Skyline's report schema.")]
    public static string GetReportFromDefinition(
        [Description("XML report definition in Skyline report schema format")] string reportDefinitionXml)
    {
        using var connection = SkylineConnection.Connect();
        string csv = connection.Call("GetReportFromDefinition", reportDefinitionXml);
        return FormatReportResult("Custom", csv);
    }

    private static string FormatReportResult(string reportName, string csv)
    {
        if (string.IsNullOrEmpty(csv))
            return "Report returned no data. The report may not exist or the document may be empty.";

        // Parse CSV to get row count and column names
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return "Report returned no data.";

        string headerLine = lines[0];
        int rowCount = lines.Length - 1;

        // Save full data to file for large reports
        string filePath = SaveReportFile(reportName, csv);

        var sb = new StringBuilder();
        sb.AppendLine($"Report: {reportName}");
        sb.AppendLine($"Rows: {rowCount}");
        sb.AppendLine($"Columns: {headerLine}");
        sb.AppendLine();

        // Preview first 5 rows
        int previewRows = Math.Min(rowCount + 1, 6); // header + up to 5 data rows
        sb.AppendLine("Preview:");
        for (int i = 0; i < previewRows && i < lines.Length; i++)
        {
            sb.AppendLine(lines[i]);
        }
        if (rowCount > 5)
        {
            sb.AppendLine($"... ({rowCount - 5} more rows)");
        }

        if (filePath != null)
        {
            sb.AppendLine();
            sb.AppendLine($"Full data saved to: {filePath}");
            sb.AppendLine("Use the Read tool to explore the full dataset.");
        }

        return sb.ToString();
    }

    private static string SaveReportFile(string reportName, string csv)
    {
        try
        {
            // Use SKYLINE_MCP_TMP_DIR env var, fallback to %LOCALAPPDATA%/Skyline/mcp/tmp/
            string tmpDir = Environment.GetEnvironmentVariable("SKYLINE_MCP_TMP_DIR");
            if (string.IsNullOrEmpty(tmpDir))
            {
                tmpDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Skyline", "mcp", "tmp");
            }
            Directory.CreateDirectory(tmpDir);

            string safeName = string.Join("_", reportName.Split(Path.GetInvalidFileNameChars()));
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string fileName = $"skyline-report-{safeName}-{timestamp}.csv";
            string filePath = Path.Combine(tmpDir, fileName);

            File.WriteAllText(filePath, csv);
            return filePath.Replace('\\', '/');
        }
        catch
        {
            return null; // If saving fails, just return the preview
        }
    }
}
