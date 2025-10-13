/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class LookupGroupComparisonTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLookupGroupComparison()
        {
            TestFilesZip = @"TestFunctional\LookupGroupComparisonTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string groupComparisonName = "Peptides by Condition";
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("RatsWithSamples.sky"));
            });

            RunLongDlg<EditGroupComparisonDlg>(SkylineWindow.AddGroupComparison, editGroupComparisonDlg =>
            {
                RunUI(()=>
                {
                    editGroupComparisonDlg.TextBoxName.Text = groupComparisonName;
                    SelectComboItem(editGroupComparisonDlg.ComboControlAnnotation, " Condition");
                    SelectComboItem(editGroupComparisonDlg.ComboIdentityAnnotation, " Name");
                });
                WaitForConditionUI(() => editGroupComparisonDlg.ComboControlValue.Items.Count > 1);
                RunUI(() =>
                {
                    SelectComboItem(editGroupComparisonDlg.ComboControlValue, "Healthy");
                });
            }, editGroupComparisonDlg=>editGroupComparisonDlg.OkDialog());
            RunUI(()=>SkylineWindow.ShowGroupComparisonWindow(groupComparisonName));
            var groupComparisonGrid = FindOpenForm<FoldChangeGrid>();
            WaitForConditionUI(() => groupComparisonGrid.IsComplete);
            RunUI(() =>
            {
                Assert.AreEqual(125, groupComparisonGrid.DataboundGridControl.RowCount);
            });

            // Test sorting and grouping area graph
            RunUI(()=>
            {
                SkylineWindow.ShowPeakAreaReplicateComparison();
            });
            // Should start out in document order
            var chromatograms = SkylineWindow.Document.MeasuredResults.Chromatograms;
            CollectionAssert.AreEqual(chromatograms.Select(c=>c.Name).ToList(), GetAreaAxisLabels());
            
            // Order by acquired time
            RunUI(()=>SkylineWindow.ShowReplicateOrder(SummaryReplicateOrder.time));
            WaitForGraphs();
            var orderedByRunStartTime =
                chromatograms.OrderBy(c => c.MSDataFileInfos.First().RunStartTime).ToList();
            CollectionAssert.AreNotEqual(chromatograms.ToList(), orderedByRunStartTime);
            CollectionAssert.AreEqual(orderedByRunStartTime.Select(c=>c.Name).ToList(), GetAreaAxisLabels());
            
            // Group by Name
            var annotationSubjectId =
                SkylineWindow.Document.Settings.DataSettings.AnnotationDefs.First(annotation =>
                    annotation.Name == "SubjectID");
            var replicateValueSubjectId = new ReplicateValue.Annotation(annotationSubjectId);
            var listDef = SkylineWindow.Document.Settings.DataSettings.Lists.First().ListDef;
            var replicateValueName = new ReplicateValue.Lookup(replicateValueSubjectId,
                listDef.Properties.First(def => def.Name == "Name"));
            RunUI(()=>SkylineWindow.GroupByReplicateValue(replicateValueName));
            WaitForGraphs();
            var ratNamesByRunStartTime = orderedByRunStartTime.Select(GetRatName).Distinct().ToList();
            CollectionAssert.AreEqual(ratNamesByRunStartTime, GetAreaAxisLabels());
            
            // Order by rat name
            RunUI(()=>SkylineWindow.OrderByReplicateAnnotation(replicateValueName));
            WaitForGraphs();
            var alphabeticalRatNames =
                ratNamesByRunStartTime.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToList();
            CollectionAssert.AreEqual(alphabeticalRatNames, GetAreaAxisLabels());
        }

        private List<string> GetAreaAxisLabels()
        {
            return CallUI(() =>
            {
                var graphSummary = FindGraphSummaryByGraphType<AreaReplicateGraphPane>();
                Assert.IsNotNull(graphSummary);
                Assert.IsTrue(graphSummary.TryGetGraphPane(out AreaReplicateGraphPane areaReplicateGraphPane));
                return areaReplicateGraphPane.XAxis.Scale.TextLabels.ToList();
            });
        }

        private string GetRatName(ChromatogramSet chromatogramSet)
        {
            var subjectId = chromatogramSet.Annotations.GetAnnotation("SubjectID");
            var listData = SkylineWindow.Document.Settings.DataSettings.Lists.Single();
            var rowIndex = listData.RowIndexOfPrimaryKey(subjectId);
            Assert.AreNotEqual(-1, rowIndex, "No row {0}", subjectId);
            int indexName = listData.FindColumnIndex("Name");
            Assert.AreNotEqual(-1, indexName, "No column Name");
            return listData.Columns[indexName].GetValue(rowIndex)?.ToString();
        }
    }
}
