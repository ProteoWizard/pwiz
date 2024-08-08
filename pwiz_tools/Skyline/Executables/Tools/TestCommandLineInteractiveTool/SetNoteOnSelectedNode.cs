/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
