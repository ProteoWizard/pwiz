﻿using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace SortRESX
{
    public class ResxSorter
    {
        public XDocument SortResxDocument(XDocument resx)
        {
            return new XDocument(
                new XElement(resx.Root.Name,
                    from comment in resx.Root.Nodes() where comment.NodeType == XmlNodeType.Comment select comment,
                    from schema in resx.Root.Elements() where schema.Name.LocalName == "schema" select schema,
                    from resheader in resx.Root.Elements("resheader") orderby (string)resheader.Attribute("name") select resheader,
                    from assembly in resx.Root.Elements("assembly") orderby (string)assembly.Attribute("name") select assembly,
                    from metadata in resx.Root.Elements("metadata") orderby (string)metadata.Attribute("name") select metadata,
                    from data in resx.Root.Elements("data") where !FilterOutDataElement(data) orderby (string)data.Attribute("name") select data
                )
            );
        }

        public bool SortResxFile(string filePath)
        {
            XDocument originalDocument;
            XDocument sortedDoc;
            var inputBytes = File.ReadAllBytes(filePath);
            using (var xmlReader = XmlReader.Create(new MemoryStream(inputBytes)))
            {
                originalDocument = XDocument.Load(xmlReader);
            }

            // Create a sorted version of the XML
            sortedDoc = SortResxDocument(originalDocument);
            MemoryStream outputMemoryStream = new MemoryStream();
            sortedDoc.Save(outputMemoryStream);
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

            return SortResxFile(path);
        }

        private bool FilterOutDataElement(XElement dataElement)
        {
            string name = (string)dataElement.Attribute("name");
            if (name.StartsWith(">>") && name.EndsWith(".Type"))
            {
                return true;
            }

            if (name.EndsWith(".TrayLocation"))
            {
                return true;
            }

            return false;
        }
    }
}
