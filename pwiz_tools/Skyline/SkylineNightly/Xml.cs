/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

using System.Xml;

namespace SkylineNightly
{
    /// <summary>
    /// Convenience class for simplifying XML handling.
    /// </summary>
    public class Xml
    {
        private readonly XmlNode _root;

        // Load from XML string.
        public static Xml FromString(string xmlString)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xmlString);
            return new Xml(doc.FirstChild);
        }

        // Create an XML element.
        public Xml(string root)
        {
            var doc = new XmlDocument();
            _root = doc.CreateElement(root);
            doc.AppendChild(_root);
        }

        // Create from node.
        private Xml(XmlNode root)
        {
            _root = root;
        }

        // Set the value of the element.
        public void Set(string value)
        {
            _root.InnerText = value;
        }

        public string Value
        {
            get { return _root.InnerText; }
        }

        // Get a child node.
        public Xml GetChild(string child)
        {
            return new Xml(_root.SelectSingleNode(child));
        }

        // Create/append a child node.
        public Xml Append(string element)
        {
            // ReSharper disable once PossibleNullReferenceException
            var child = _root.AppendChild(_root.OwnerDocument.CreateElement(element));
            return new Xml(child);
        }

        // Set an attribute value.
        public object this[string key]
        {
            set
            {
                ((XmlElement)_root).SetAttribute(key, value.ToString());
            }
        }

        // Count how many child nodes are within this element.
        public int Count
        {
            get { return _root.ChildNodes.Count; }
        }

        // Save XML to disk file.
        public void Save(string file)
        {
            if (_root.OwnerDocument != null)
                _root.OwnerDocument.Save(file);
        }

        public override string ToString()
        {
            using (var sw = new System.IO.StringWriter())
            {
                using (var xw = new XmlTextWriter(sw))
                {
                    xw.Formatting = Formatting.Indented;
                    xw.Indentation = 4;
                    _root.WriteTo(xw);
                }
                return sw.ToString();
            }
        }
    }
}
