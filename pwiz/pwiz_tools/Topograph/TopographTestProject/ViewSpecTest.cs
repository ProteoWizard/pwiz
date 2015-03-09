/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;

namespace pwiz.Topograph.Test
{
    /// <summary>
    /// Summary description for ViewInfoTest
    /// </summary>
    [TestClass]
    public class ViewSpecTest
    {
        [TestMethod]
        public void TestWriteReadXml()
        {
            var stringBuilder = new StringBuilder();
            var viewSpec = new ViewSpec();
            var xmlSerializer = new XmlSerializer(typeof (ViewSpecList));
            xmlSerializer.Serialize(XmlWriter.Create(stringBuilder), new ViewSpecList(new[] {viewSpec}));
            var viewSpecCompare = ((ViewSpecList) xmlSerializer.Deserialize(new StringReader(stringBuilder.ToString()))).ViewSpecs[0];
            Assert.AreEqual(viewSpec, viewSpecCompare);
            var viewSpecEmpty = ((ViewSpecList) xmlSerializer.Deserialize(new StringReader("<views><view /></views>"))).ViewSpecs[0];
            Assert.AreEqual(viewSpec, viewSpecEmpty);
            viewSpec = viewSpec
                .SetName("ViewName")
                .SetColumns(new[] {new ColumnSpec().SetName("foo"), new ColumnSpec().SetName("bar"),});
            Assert.AreNotEqual(viewSpecCompare, viewSpec);
            stringBuilder.Length = 0;
            xmlSerializer.Serialize(XmlWriter.Create(stringBuilder), new ViewSpecList(new[] {viewSpec}));
            viewSpecCompare = ((ViewSpecList) xmlSerializer.Deserialize(new StringReader(stringBuilder.ToString()))).ViewSpecs[0];
            Assert.AreEqual(viewSpec, viewSpecCompare);
        }
    }
}
