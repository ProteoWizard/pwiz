/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Xml;
using System.Xml.XPath;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Serialization
{
    /// <summary>
    /// Helper class to allow readahead of current element and its children,
    /// while providing for re-reading that element as well.
    /// Also provides for altering selected XML attributes for consumption during the re-read.
    /// N.B. ctor leaves leaves parent reader at the end of current element
    /// </summary>
    public class XmlReadAhead
    {
        private string XmlText { get; set; } // Text representation of the subtree consumed by the ctor

        /// <summary>
        /// Construct the readahead object - leaves parent reader at the end of current element
        /// Works by reading current element in to an XPathDocument, so don't use on 
        /// very large or complicated elements!
        /// </summary>
        /// <param name="reader">XML reader, positioned at the start of an element</param>
        public XmlReadAhead(XmlReader reader)
        {
            var xPathDoc = new XPathDocument(reader.ReadSubtree()); // Leaves reader at end of current element
            XmlText = xPathDoc.CreateNavigator().OuterXml;
        }

        /// <summary>
        /// Create an XmlReader for reparsing of the element that was consumed by the ctor
        /// </summary>
        public XmlReader CreateXmlReader()
        {
            var reader = new XmlTextReader(new StringReader(XmlText));
            reader.Read();
            return reader;
        }

        /// <summary>
        /// Find all occurances of the (presumed to be double) element and return maximum precision in use
        /// </summary>
        public double ElementPrecision(string elementName)
        {
            var element = string.Format(@"<{0}>", elementName);
            int? precision = null;
            for (var index = 0; index >= 0; index++)
            {
                var elementStart = XmlText.IndexOf(element, index, StringComparison.InvariantCulture);
                if (elementStart < 0)
                {
                    break;
                }
                index = elementStart + element.Length;
                var dotIndex = XmlText.IndexOf(@".", index, StringComparison.InvariantCulture)+1;
                var endIndex = XmlText.IndexOf(@"<", index, StringComparison.InvariantCulture);
                if (endIndex > dotIndex && XmlText.Substring(dotIndex, endIndex-dotIndex).All(char.IsDigit)) // Don't deal with exp notation
                {
                    precision = Math.Max(precision??0, endIndex - dotIndex);
                }
                else if (XmlText.Substring(index, endIndex - index).All(char.IsDigit))
                {
                    precision = 0; // integer value
                }
            }
            return precision.HasValue ? Math.Pow(10.0, -precision.Value) : .001; // Reasonable guess if the format is unexpected
        }

        /// <summary>
        /// Modify the XML that CreateXmlReader will parse for next caller. Finds each
        /// occuraece of elementName and changes or adds attribute name from attributesList.
        /// Expects as many occurences of elementName as there are members in attributesList
        /// </summary>
        public void ModifyAttributesInElement(string elementName, string attributeName, IList<string> attributesList)
        {
            var elementOpenA = string.Format(@"<{0} ", elementName);
            var elementOpenB = string.Format(@"<{0}>", elementName);
            var elementsA = XmlText.Split(new[] { elementOpenA }, StringSplitOptions.None);
            var elementsB = XmlText.Split(new[] { elementOpenB }, StringSplitOptions.None);
            var elements = elementsA.Length > elementsB.Length ? elementsA : elementsB;
            var elementOpen = elementsA.Length > elementsB.Length ? elementOpenA : elementOpenB;
            var attr = string.Format(@" {0}=", attributeName);
            Assume.AreEqual(elements.Length, attributesList.Count+1, @"Trouble in XML lookahead");
            for (var e = 1; e < elements.Length; e++)
            {
                var element = elements[e];
                var attrStart = element.IndexOf(attr, StringComparison.InvariantCulture);
                var tagEnd = element.IndexOf(@">", StringComparison.InvariantCulture);
                if (attrStart > tagEnd)
                {
                    attrStart = -1; // That attr belonged to a child element
                }
                // ReSharper disable LocalizableElement
                var newAttr = string.Format(" {0}\"{1}\" ", attr, attributesList[e - 1]);
                // ReSharper restore LocalizableElement
                if (attrStart == -1)
                {
                    elements[e] = newAttr + elements[e]; 
                }
                else
                {
                    // ReSharper disable LocalizableElement
                    var attrEnd = element.IndexOf("\"", attrStart + attr.Length + 1, StringComparison.InvariantCulture);
                    // ReSharper restore LocalizableElement
                    elements[e] = elements[e].Substring(0, attrStart) + newAttr + elements[e].Substring(attrEnd + 1);
                }
            }
            XmlText = string.Join(elementOpen, elements);
        }
    }
}
