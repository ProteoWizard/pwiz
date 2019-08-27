/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Reporting
{
    /// <summary>
    /// Summary description for ReportSpecConverterTest
    /// </summary>
    [TestClass]
    public class ReportSpecConverterTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestBlankDocument()
        {
            var blankDocument = new SrmDocument(SrmSettingsList.GetDefault());
            CheckReportCompatibility.CheckAll(blankDocument);
        }
        [TestMethod]
        public void TestDocumentWithOneLabel()
        {
            var assembly = typeof(ReportSpecConverterTest).Assembly;
            XmlSerializer ser = new XmlSerializer(typeof(SrmDocument));
            // ReSharper disable once AssignNullToNotNullAttribute
            var docWithLabel = (SrmDocument)ser.Deserialize(
                assembly.GetManifestResourceStream(typeof(ReportSpecConverterTest), "HeavyLabeledLeucine.sky"));
            CheckReportCompatibility.CheckAll(docWithLabel);
        }

        [TestMethod]
        public void TestPivotIsotopeLabel()
        {
            var assembly = typeof(ReportSpecConverterTest).Assembly;
            XmlSerializer documentSerializer = new XmlSerializer(typeof(SrmDocument));
            // ReSharper disable once AssignNullToNotNullAttribute
            var docWithLabel = (SrmDocument)documentSerializer.Deserialize(
                assembly.GetManifestResourceStream(typeof(ReportSpecConverterTest), "HeavyLabeledLeucine.sky"));
            XmlSerializer reportSerializer = new XmlSerializer(typeof(ReportSpecList));
            // ReSharper disable once AssignNullToNotNullAttribute
            var reports = (ReportSpecList)
                reportSerializer.Deserialize(assembly.GetManifestResourceStream(typeof (ReportSpecConverterTest),
                    "PivotIsotopeLabel.skyr"));
            Assert.AreNotEqual(0, reports.Count);
            using (var checker = new CheckReportCompatibility(docWithLabel))
            {
                foreach (var report in reports)
                {
                    checker.CheckReport(report);
                }
            }
        }
    }
}
