using System.IO;
using System.Linq;
using SkylineTool;

namespace TestCommandLineInteractiveTool
{
    public class SetNoteOnSelectedNode : AbstractCommand
    {
        public SetNoteOnSelectedNode(SkylineToolClient client) : base(client)
        {
        }

        public override void RunCommand()
        {
            string locator = SkylineToolClient.GetSelectedElementLocator("Transition")??
                             SkylineToolClient.GetSelectedElementLocator("Precursor") ??
                             SkylineToolClient.GetSelectedElementLocator("Molecule") ??
                             SkylineToolClient.GetSelectedElementLocator("MoleculeGroup");
            if (locator != null)
            {
                SetNote(locator, "Test Interactive Tool Note");
            }
        }

        public void SetNote(string locator, string noteValue)
        {
            var stringWriter = new StringWriter();
            AppendCsvLine(stringWriter, "ElementLocator", "property_Note");
            AppendCsvLine(stringWriter, locator, noteValue);
            SkylineToolClient.ImportProperties(stringWriter.ToString());
        }

        public static void AppendCsvLine(TextWriter writer, params string[] fields)
        {
            writer.WriteLine(string.Join(",", fields.Select(EscapeCsvField)));
        }

        public static string EscapeCsvField(string text)
        {
            if (text == null)
            {
                return string.Empty;
            }

            if (text.IndexOfAny(new[] { '"', ',', '\r', '\n' }) == -1)
            {
                return text;
            }
            return '"' + text.Replace("\"", "\"\"") + '"';
        }
    }
}
