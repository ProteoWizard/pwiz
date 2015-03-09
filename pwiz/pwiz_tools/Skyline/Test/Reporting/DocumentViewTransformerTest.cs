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
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;
using Transition = pwiz.Skyline.Model.Databinding.Entities.Transition;

namespace pwiz.SkylineTest.Reporting
{
    [TestClass]
    public class DocumentViewTransformerTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestTransitions()
        {
            var transitionsReport = GetDefaultReport(Resources.ReportSpecList_GetDefaults_Transition_Results);
            ValidateReport(transitionsReport, typeof(Transition));
        }

        [TestMethod]
        public void TestPeptides()
        {
            ValidateReport(GetDefaultReport(Resources.ReportSpecList_GetDefaults_Peptide_RT_Results), typeof(Peptide));
            ValidateReport(GetDefaultReport(Resources.ReportSpecList_GetDefaults_Peptide_Ratio_Results), typeof(Peptide));
        }

        [TestMethod]
        public void TestDefaultViews()
        {
            SkylineDataSchema dataSchema = GetDataSchema();
            foreach (var type in new[]
                {typeof (Protein), typeof (Peptide), typeof (Precursor), typeof (Transition), typeof (Replicate)})
            {
                var viewInfo = SkylineViewContext.GetDefaultViewInfo(ColumnDescriptor.RootColumn(dataSchema, type));
                EnsureViewRoundTrips(viewInfo);
            }
        }

        private void ValidateReport(ReportSpec reportSpec, Type rowType)
        {
            var dataSchema = GetDataSchema();
            var converter = new ReportSpecConverter(dataSchema);
            var viewInfo = converter.Convert(reportSpec);
            Assert.AreEqual(rowType, viewInfo.ParentColumn.PropertyType);
            EnsureViewRoundTrips(viewInfo);
        }

        private void EnsureViewRoundTrips(ViewInfo viewInfo)
        {
            IEnumerable<PropertyPath> emptyPropertyPaths = new PropertyPath[0];
            ValidateViewInfo(viewInfo);
            var transformer = new DocumentViewTransformer();
            var viewInfoDocument = transformer.MakeIntoDocumentView(viewInfo, ref emptyPropertyPaths);
            Assert.AreEqual(typeof(SkylineDocument), viewInfoDocument.ParentColumn.PropertyType);
            ValidateViewInfo(viewInfoDocument);
            var viewInfoRoundTrip = transformer.ConvertFromDocumentView(viewInfoDocument, ref emptyPropertyPaths);
            Assert.AreEqual(viewInfo.ParentColumn.PropertyType, viewInfoRoundTrip.ParentColumn.PropertyType);
            Assert.AreEqual(viewInfo.GetViewSpec(), viewInfoRoundTrip.GetViewSpec());
        }
        
        private void ValidateViewInfo(ViewInfo viewInfo)
        {
            foreach (var column in viewInfo.DisplayColumns)
            {
                Assert.AreNotEqual(typeof(object), column.PropertyType, column.PropertyPath.ToString());
            }
        }

        private static ReportSpec GetDefaultReport(string name)
        {
            var reportSpecList = new ReportSpecList();
            reportSpecList.AddDefaults();
            return reportSpecList.First(reportSpec => name == reportSpec.Name);
        }

        private SkylineDataSchema GetDataSchema()
        {
            var document = new SrmDocument(SrmSettingsList.GetDefault());
            var container = new MemoryDocumentContainer();
            Assert.IsTrue(container.SetDocument(document, container.Document));
            return new SkylineDataSchema(container, DataSchemaLocalizer.INVARIANT);
        }
    }
}
