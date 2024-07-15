/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SortRESX
{
    public class ResxSorter
    {
        private static readonly byte[] Utf8Preamble = new UTF8Encoding(true).GetPreamble();
        private readonly XmlWriterSettings _xmlWriterSettingsNoPreamble = new XmlWriterSettings
        {
            Indent = true,
            // Unicode encoding with no Byte Order Mark, and throw on invalid characters
            Encoding = new UTF8Encoding(false, true)
        };
        private readonly XmlWriterSettings _xmlWriterSettingsPreamble = new XmlWriterSettings
        {
            Indent = true,
            // Unicode encoding with Byte Order Mark, and throw on invalid characters
            Encoding = new UTF8Encoding(true, true)
        };
        public XDocument SortResxDocument(XDocument resx, bool preserveElementOrder)
        {
            if (resx.Root == null)
            {
                return resx;
            }

            return new XDocument(
                new XElement(resx.Root.Name,
                    from comment in resx.Root.Nodes() where comment.NodeType == XmlNodeType.Comment select comment,
                    from schema in resx.Root.Elements() where schema.Name.LocalName == "schema" select schema,
                    from resheader in resx.Root.Elements("resheader") select resheader,
                    SelectElements(resx.Root, "assembly", preserveElementOrder),
                    SelectElements(resx.Root, "metadata", preserveElementOrder).Where(KeepMetadataElement),
                    SelectElements(resx.Root, "data", preserveElementOrder).Where(KeepDataElement)
                        .Select(data=>FixWhitespace(data, 1))));
        }

        public bool SortResxFile(string filePath, bool preserveElementOrder)
        {
            XDocument originalDocument;
            var inputBytes = File.ReadAllBytes(filePath);
            using (var xmlReader = XmlReader.Create(new MemoryStream(inputBytes)))
            {
                originalDocument = XDocument.Load(xmlReader);
            }

            // Create a sorted version of the XML
            var sortedDoc = SortResxDocument(originalDocument, preserveElementOrder);
            MemoryStream outputMemoryStream = new MemoryStream();
            var xmlWriterSettings = HasUtf8Preamble(inputBytes) 
                ? _xmlWriterSettingsPreamble : _xmlWriterSettingsNoPreamble;
            using (var xmlWriter = XmlWriter.Create(outputMemoryStream, xmlWriterSettings))
            {
                sortedDoc.Save(xmlWriter);
            }
            var outputBytes = outputMemoryStream.ToArray();
            if (outputBytes.SequenceEqual(inputBytes))
            {
                return false;
            }
            using (var fileSaver = new FileSaver(filePath))
            {
                File.WriteAllBytes(fileSaver.SafeName, outputBytes);
                fileSaver.Commit();
                return true;
            }
        }

        public bool ProcessFile(string path)
        {
            if (!Path.GetExtension(path).Equals(".resx", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            bool preserveElementOrder = false;
            if (PreserveOrderInResourcesResx)
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
                if (fileNameWithoutExtension == "Resources" || fileNameWithoutExtension != null &&
                    Path.GetFileNameWithoutExtension(fileNameWithoutExtension) == "Resources")
                {
                    preserveElementOrder = true;
                }
            }
            
            return SortResxFile(path, preserveElementOrder);
        }

        /// <summary>
        /// Whether, if the filename is "Resources" to skip sorting elements by the name attribute
        /// </summary>
        public bool PreserveOrderInResourcesResx { get; set; }

        private bool KeepMetadataElement(XElement metadataElement)
        {
            string name = (string) metadataElement.Attribute("name");
            if (name.EndsWith(".TrayLocation") || name.EndsWith(".TrayHeight"))
            {
                return false;
            }

            return true;
        }

        private bool KeepDataElement(XElement dataElement)
        {
            string name = (string)dataElement.Attribute("name");
            if (name.StartsWith(">>") && name.EndsWith(".Type"))
            {
                return false;
            }

            return true;
        }

        private IEnumerable<XElement> SelectElements(XElement parent, string elementName, bool preserveOrder)
        {
            var elements = parent.Elements(elementName);
            if (!preserveOrder)
            {
                elements = elements.OrderBy(element => element.Attribute("name")?.ToString());
            }

            return elements;
        }

        /// <summary>
        /// Changes the whitespace so that each child element is indented with two spaces.
        /// </summary>
        /// <param name="element">Element whose children will be indented</param>
        /// <param name="depth">Distance of element from the document element.</param>
        private XElement FixWhitespace(XElement element, int depth)
        {
            var childNodes = new List<XNode>();
            foreach (var childElement in element.Elements())
            {
                childNodes.Add(new XText(Environment.NewLine + new string(' ', depth * 2 + 2)));
                childNodes.Add(childElement);
            }
            childNodes.Add(new XText(Environment.NewLine + new string(' ', depth * 2)));
            return new XElement(element.Name, element.Attributes(), childNodes);
        }

        private bool HasUtf8Preamble(byte[] bytes)
        {
            return Utf8Preamble.SequenceEqual(bytes.Take(Utf8Preamble.Length));
        }
    }
}
