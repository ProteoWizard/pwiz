/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ResourcesOrganizer
{
    public static class TextUtil
    {
        public static Encoding Utf8Encoding = new UTF8Encoding(false, true);
        public static readonly XmlWriterSettings XmlWriterSettings = new XmlWriterSettings()
        {
            Indent = true,
            Encoding = Utf8Encoding
        };


        public static readonly string NewLine = "\n";
        public static string Quote(string? s)
        {
            if (s == null)
            {
                return "null";
            }

            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        public static string ToDsvField(char separator, string? text)
        {
            if (text == null)
                return string.Empty;
            if (text.IndexOfAny(['"', separator, '\r', '\n']) == -1)
                return text;
            return '"' + text.Replace("\"", "\"\"") + '"';
        }


        public static string ToCsvRow(IEnumerable<string?> fields)
        {
            return string.Join(",", fields.Select(field => ToDsvField(',', field?.ToString())));
        }

        public static string ToCsvRow(params string?[] fields)
        {
            return ToCsvRow(fields.AsEnumerable());
        }

        public static string SerializeDocument(XDocument document)
        {
            using var memoryStream = new MemoryStream();
            using (var xmlWriter = XmlWriter.Create(memoryStream, XmlWriterSettings))
            {
                document.Save(xmlWriter);
            }
            var documentText = Utf8Encoding.GetString(memoryStream.ToArray());
            return NormalizeXml(documentText);
        }

        public static string NormalizeXml(string xml)
        {
            var lines = new List<string>();
            using var stringReader = new StringReader(xml);
            while (stringReader.ReadLine() is { } line)
            {
                lines.Add(line);
            }
            return string.Join(Environment.NewLine, lines);
        }

        public static string LineSeparate(IEnumerable<string> lines)
        {
            return string.Join(NewLine, lines);
        }

        public static string LineSeparate(params string[] lines)
        {
            return string.Join(NewLine, lines);
        }

        public static string QuoteAndSeparate(IEnumerable<string> values)
        {
            return string.Join(",", values.Select(Quote));
        }
    }
}
