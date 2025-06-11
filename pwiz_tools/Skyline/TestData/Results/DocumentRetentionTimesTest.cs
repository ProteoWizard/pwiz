/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// Summary description for RetentionTimeAlignmentsTest
    /// </summary>
    [TestClass]
    public class DocumentRetentionTimesTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestXmlSerialize()
        {
            var retentionTimeAlignments = new FileRetentionTimeAlignments("Test", new[]
                                                                                  {
                                                                                      new RetentionTimeAlignment(
                                                                                          "First", 
                                                                                          new RegressionLine(1.5, 3.0)),
                                                                                      new RetentionTimeAlignment(
                                                                                          "Second",
                                                                                          new RegressionLine(.75, -1.5))
                                                                                      ,
                                                                                  });
            var stringBuilder = new StringBuilder();
            using (var xmlWriter = XmlWriter.Create(stringBuilder))
            {
                // Some versions of ReSharper think XmlWriter.Create can return a null, others don't, disable this check to satisfy either
                // ReSharper disable PossibleNullReferenceException
                xmlWriter.WriteStartDocument();
                xmlWriter.WriteStartElement("TestDocument");
                xmlWriter.WriteElements(new[] {retentionTimeAlignments}, new XmlElementHelper<FileRetentionTimeAlignments>());
                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndDocument();
                // ReSharper restore PossibleNullReferenceException
            }
            var xmlReader = XmlReader.Create(new StringReader(stringBuilder.ToString()));
            xmlReader.ReadStartElement();
            var deserializedObjects = new List<FileRetentionTimeAlignments>();
            xmlReader.ReadElements(deserializedObjects);
            Assert.AreEqual(1, deserializedObjects.Count);
            var compare = deserializedObjects[0];
            Assert.AreEqual(retentionTimeAlignments, compare);
            Assert.AreEqual(retentionTimeAlignments.GetHashCode(), compare.GetHashCode());
            Assert.AreEqual(2, compare.RetentionTimeAlignments.Count);
            Assert.AreNotEqual(compare.RetentionTimeAlignments.Values[0], compare.RetentionTimeAlignments.Values[1]);
        }

    }
}
