/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class MedianNormalizationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMedianNormalization()
        {
            TestFilesZip = @"TestFunctional\MedianNormalizationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            AreaReplicateGraphPane areaReplicateGraphPane = null;
            PeptideGroupDocNode secondProtein = null;
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MedianNormalizationTest.sky"));
                SkylineWindow.SetDisplayTypeChrom(DisplayTypeChrom.products);
                SkylineWindow.SetNormalizationMethod(NormalizationMethod.EQUALIZE_MEDIANS);
                Settings.Default.PeakAreaDotpDisplay = DotProductDisplayOption.none.ToString();
                Settings.Default.ShowLibraryPeakArea = false;
                secondProtein = (PeptideGroupDocNode) SkylineWindow.Document.Children[1];
                Assert.AreEqual(1, secondProtein.Children.Count);
                SkylineWindow.SelectedPath = new IdentityPath(secondProtein.Id, secondProtein.Children[0].Id);
                SkylineWindow.ShowPeakAreaReplicateComparison();
                areaReplicateGraphPane = FindGraphPane<AreaReplicateGraphPane>();
                SkylineWindow.ShowGroupComparisonWindow("Group Comparison");
                areaReplicateGraphPane.ExpectedVisible = AreaExpectedValue.none;
            });
            var foldChangeGrid = FindOpenForm<FoldChangeGrid>();
            WaitForConditionUI(() => foldChangeGrid.DataboundGridControl.RowCount > 0);
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.AreEqual(4, foldChangeGrid.DataboundGridControl.RowCount);
                var foldChangeRow = (FoldChangeBindingSource.FoldChangeRow) ((RowItem) foldChangeGrid.DataboundGridControl.BindingListSource[3]).Value;
                Assert.AreSame(secondProtein.Id, foldChangeRow.Protein.IdentityPath.Child);
                Assert.AreEqual(2, foldChangeRow.MsLevel);
                Assert.AreEqual(1, secondProtein.Children.Count);
                var peptideDocNode = secondProtein.Molecules.First();
                Assert.AreEqual(1, peptideDocNode.Children.Count);
                var precursorDocNode = peptideDocNode.TransitionGroups.First();
                var productTransitions = precursorDocNode.Transitions.Where(t => !t.IsMs1).ToList();
                Assert.AreEqual(productTransitions.Count, areaReplicateGraphPane.CurveList.Count);
                var replicates = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms;
                Assert.AreEqual(2, replicates.Count);
                Assert.AreEqual("AD", replicates[0].Annotations.GetAnnotation("Diagnosis"));
                Assert.AreEqual("PD", replicates[1].Annotations.GetAnnotation("Diagnosis"));
                double adArea = 0;
                double pdArea = 0;
                foreach (var curve in areaReplicateGraphPane.CurveList)
                {
                    Assert.AreEqual(replicates.Count, curve.Points.Count);
                    adArea += curve.Points[0].Y;
                    pdArea += curve.Points[1].Y;
                }

                var foldChange = foldChangeRow.FoldChangeResult.FoldChange;
                var ratio = pdArea / adArea;
                AssertEx.AreEqual(foldChange, ratio, 1e-6);
            });
        }

        private T FindGraphPane<T>() where T : class
        {
            foreach (var graphSummary in FormUtil.OpenForms.OfType<GraphSummary>())
            {
                if (graphSummary.TryGetGraphPane(out T pane))
                {
                    return pane;
                }
            }
            return null;
        }

    }
}
