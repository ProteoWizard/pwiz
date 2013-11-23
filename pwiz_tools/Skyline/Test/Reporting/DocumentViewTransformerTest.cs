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
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;
using Transition = pwiz.Skyline.Model.Databinding.Entities.Transition;

namespace pwiz.SkylineTest.Reporting
{
    [TestClass]
    public class DocumentViewTransformerTest
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

        private void ValidateReport(ReportSpec reportSpec, Type rowType)
        {
            var dataSchema = GetDataSchema();
            var converter = new ReportSpecConverter(dataSchema);
            var viewInfo = converter.Convert(reportSpec);
            IEnumerable<PropertyPath> emptyPropertyPaths = new PropertyPath[0];
            Assert.AreEqual(rowType, viewInfo.ParentColumn.PropertyType);
            ValidateViewInfo(viewInfo);
            var transformer = new DocumentViewTransformer();
            var viewInfoProteins = transformer.MakeIntoProteinsView(viewInfo, ref emptyPropertyPaths);
            Assert.AreEqual(typeof(Protein), viewInfoProteins.ParentColumn.PropertyType);
            ValidateViewInfo(viewInfoProteins);
            var viewInfoRoundTrip = transformer.ConvertFromProteinsView(viewInfoProteins, ref emptyPropertyPaths);
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
            return new SkylineDataSchema(container);
        }
    }
}
