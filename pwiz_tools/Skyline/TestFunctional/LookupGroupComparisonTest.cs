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
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
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
            const string peptidesByCondition = "Peptides by Condition";
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("RatsWithSamples.sky"));
            });

            RunLongDlg<EditGroupComparisonDlg>(SkylineWindow.AddGroupComparison, editGroupComparisonDlg =>
            {
                RunUI(()=>
                {
                    editGroupComparisonDlg.TextBoxName.Text = peptidesByCondition;
                    editGroupComparisonDlg.ControlAnnotation = " Condition";
                    editGroupComparisonDlg.IdentityAnnotation = " Name";
                });
                WaitForConditionUI(() => editGroupComparisonDlg.ControlValueOptions.Any());
                RunUI(() =>
                {
                    editGroupComparisonDlg.ControlValue = "Healthy";
                });
            }, editGroupComparisonDlg=>editGroupComparisonDlg.OkDialog());
            RunUI(()=>SkylineWindow.ShowGroupComparisonWindow(peptidesByCondition));
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
            
            // Set SampleType and Concentration on Replicates and use those to define a group comparison
            RunUI(()=>SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            Assert.IsNotNull(documentGrid);
            RunUI(()=>documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates));
            WaitForConditionUI(() => documentGrid.IsComplete);
            RunUI(() =>
            {
                var sampleTypes = Enumerable.Repeat(SampleType.BLANK.ToString(), 21)
                    .Concat(Enumerable.Repeat(SampleType.DOUBLE_BLANK.ToString(), 21));
                var colSampleType =
                    documentGrid.DataboundGridControl.FindColumn(
                        PropertyPath.Root.Property(nameof(Replicate.SampleType)));
                Assert.IsNotNull(colSampleType);
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[0].Cells[colSampleType.Index];
                SetClipboardText(TextUtil.LineSeparate(sampleTypes));
                documentGrid.DataGridView.SendPaste();
                var concentrations = Enumerable.Range(0, 7).SelectMany(i =>
                    Enumerable.Repeat((i * i / 10.0).ToString(CultureInfo.CurrentCulture), 3));
                var colConcentration =
                    documentGrid.DataboundGridControl.FindColumn(
                        PropertyPath.Root.Property(nameof(Replicate.AnalyteConcentration)));
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[0].Cells[colConcentration.Index];
                SetClipboardText(TextUtil.LineSeparate(concentrations));
                documentGrid.DataGridView.SendPaste();
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[0].Cells[0];
            });

            const string proteinsBySampleType = "Proteins by Sample Type";
            RunLongDlg<EditGroupComparisonDlg>(SkylineWindow.AddGroupComparison, editGroupComparisonDlg =>
            {
                RunUI(() =>
                {
                    editGroupComparisonDlg.TextBoxName.Text = proteinsBySampleType;
                    editGroupComparisonDlg.ControlAnnotation = ColumnCaptions.SampleType;
                    editGroupComparisonDlg.IdentityAnnotation = ColumnCaptions.AnalyteConcentration;
                    editGroupComparisonDlg.RadioScopePerProtein.Checked = true;
                });
                WaitForConditionUI(() => editGroupComparisonDlg.ControlValueOptions.Any());
                RunUI(() =>
                {
                    editGroupComparisonDlg.ControlValue = SampleType.BLANK.ToString();
                });
            }, editGroupComparisonDlg => editGroupComparisonDlg.OkDialog());
            RunUI(() => SkylineWindow.ShowGroupComparisonWindow(proteinsBySampleType));

            var proteinsBySampleTypeGrid = FoldChangeForm.FindForm<FoldChangeGrid>(SkylineWindow, proteinsBySampleType);
            WaitForConditionUI(() => proteinsBySampleTypeGrid.IsComplete);
            Assert.AreEqual(48, proteinsBySampleTypeGrid.DataboundGridControl.RowCount);
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
