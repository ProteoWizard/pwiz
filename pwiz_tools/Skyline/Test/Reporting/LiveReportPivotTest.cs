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
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Reporting
{
    /// <summary>
    /// Summary description for LiveReportPivotTest
    /// </summary>
    [TestClass]
    public class LiveReportPivotTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestConverted()
        {
            var assembly = typeof(LiveReportPivotTest).Assembly;
            XmlSerializer documentSerializer = new XmlSerializer(typeof(SrmDocument));
            // ReSharper disable once AssignNullToNotNullAttribute
            var document = (SrmDocument)documentSerializer.Deserialize(
                assembly.GetManifestResourceStream(typeof(ReportSpecConverterTest), "silac_1_to_4.sky"));
            XmlSerializer reportSerializer = new XmlSerializer(typeof(ReportSpecList));
            // ReSharper disable once AssignNullToNotNullAttribute
            var reports = (ReportSpecList)
                    reportSerializer.Deserialize(assembly.GetManifestResourceStream(typeof(ReportSpecConverterTest),
                        "ResultSummaryPivot.skyr"));
            Assert.AreNotEqual(0, reports.Count);
            using (var checker = new CheckReportCompatibility(document))
            {
                foreach (var report in reports)
                {
                    checker.CheckReport(report);
                }
            }
        }

        /// <summary>
        /// Tests that if a view first pivots on Replicate (by having the SublistId not contain the Results collection),
        /// and then pivots on IsotopeLabelType (using the group/total mechanisms), columns get pivoted in the exact
        /// way they are supposed to.
        /// </summary>
        [TestMethod]
        public void TestPivotResultsThenIsotopeLabel()
        {
            var assembly = typeof (LiveReportPivotTest).Assembly;
            XmlSerializer documentSerializer = new XmlSerializer(typeof(SrmDocument));
            // ReSharper disable once AssignNullToNotNullAttribute
            var document = (SrmDocument)documentSerializer.Deserialize(
                assembly.GetManifestResourceStream(typeof(ReportSpecConverterTest), "silac_1_to_4.sky"));
            XmlSerializer reportSerializer = new XmlSerializer(typeof(ReportOrViewSpecList));
            // ReSharper disable once AssignNullToNotNullAttribute
            var views = (ReportOrViewSpecList) reportSerializer.Deserialize(
                assembly.GetManifestResourceStream(typeof(ReportSpecConverterTest), "LiveReportPivots.skyr"));
            var view = views.First(reportSpec => reportSpec.Name == "ResultSummaryPivotResultsThenLabelType").ViewSpec;
            var bindingListSource = new BindingListSource();
            var documentContainer = new MemoryDocumentContainer();
            Assert.IsTrue(documentContainer.SetDocument(document, null));
            var dataSchema = new SkylineDataSchema(documentContainer, DataSchemaLocalizer.INVARIANT);
            bindingListSource.SetViewContext(new DocumentGridViewContext(dataSchema), new ViewInfo(dataSchema, typeof(Precursor), view));
            var expectedColumnNames = new[] {
                    "PeptideSequence",
                    "Chromatograms Replicate",
                    "Chromatograms PeptideRetentionTime",
                    "light IsotopeLabelType",
                    "light MeanTotalArea",
                    "light Chromatograms TotalArea",
                    "heavy IsotopeLabelType",
                    "heavy MeanTotalArea",
                    "heavy Chromatograms TotalArea",
                };
            var actualColumnNames =
                bindingListSource.GetItemProperties(null)
                    .Cast<PropertyDescriptor>()
                    .Select(pd => pd.DisplayName)
                    .ToArray();
            CollectionAssert.AreEqual(expectedColumnNames, actualColumnNames);
        }
    }
}
