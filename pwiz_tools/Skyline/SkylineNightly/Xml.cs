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

using System;
using System.Xml;

namespace SkylineNightly
{
    /// <summary>
    /// Convenience class for simplifying XML handling.
    /// </summary>
    public class Xml
    {
        private readonly XmlNode _rootNode;

        private XmlNode GetRoot()
        {
            if (_rootNode == null)
            {
                throw new Exception(@"XML root node is null");
            }
            return _rootNode;
        }

        // Caller isn't expected to handle this - these exceptions are rare, and this isn't
        // user facing so writing a bunch of error recovery code isn't a good use of time.
        public static void ShowXmlExceptionDetailsAndDie(Exception e, string hint)
        {
            var message = $@"XML exception while handling this text:\n{hint}";
            throw new Exception(message, e); 
        }

        // Load from XML string.
        public static Xml FromString(string xmlString)
        {
            var doc = new XmlDocument();
            try
            {
                doc.LoadXml(xmlString);
            }
            catch (Exception e)
            {
                ShowXmlExceptionDetailsAndDie(e, xmlString);
            }
            return new Xml(doc.FirstChild);
        }

        // Create an XML element.
        public Xml(string root)
        {
            var doc = new XmlDocument();
            try
            {
                _rootNode = doc.CreateElement(root);
            }
            catch (Exception e)
            {
                ShowXmlExceptionDetailsAndDie(e, root);
            }
            doc.AppendChild(GetRoot());
        }

        // Create from node.
        private Xml(XmlNode root)
        {
            _rootNode = root;
        }

        // Set the value of the element.
        public void Set(string value)
        {
            GetRoot().InnerText = value;
        }

        public string Value
        {
            get { return GetRoot().InnerText; }
        }

        // Get a child node.
        public Xml GetChild(string child)
        {
            return new Xml(GetRoot().SelectSingleNode(child));
        }

        // Create/append a child node.
        public Xml Append(string element)
        {
            // ReSharper disable once PossibleNullReferenceException
            var child = GetRoot().AppendChild(GetRoot().OwnerDocument.CreateElement(element));
            return new Xml(child);
        }

        // Set an attribute value.
        public object this[string key]
        {
            set
            {
                ((XmlElement)GetRoot()).SetAttribute(key, value.ToString());
            }
        }

        // Count how many child nodes are within this element.
        public int Count
        {
            get { return GetRoot().ChildNodes.Count; }
        }

        // Save XML to disk file.
        public void Save(string file)
        {
            var root = GetRoot();
            if (root.OwnerDocument != null)
                root.OwnerDocument.Save(file);
        }

        public override string ToString()
        {
            using (var sw = new System.IO.StringWriter())
            {
                using (var xw = new XmlTextWriter(sw))
                {
                    xw.Formatting = Formatting.Indented;
                    xw.Indentation = 4;
                    GetRoot().WriteTo(xw);
                }
                return sw.ToString();
            }
        }
    }
}
