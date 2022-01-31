/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests synchronization between the "Ratios To" context menu item on the Sequence Tree with the "Normalize To"
    /// choices on the peak area graph.
    /// </summary>
    [TestClass]
    public class AreaNormalizeOptionTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAreaOptionNormalize()
        {
            TestFilesZip = @"TestFunctional\AreaNormalizeOptionTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            AreaReplicateGraphPane areaReplicateGraphPane = null;
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("AreaNormalizeOptionTest.sky"));
                SkylineWindow.ShowPeakAreaReplicateComparison();
                areaReplicateGraphPane = FindGraphPane<AreaReplicateGraphPane>();
                Assert.IsNotNull(areaReplicateGraphPane);
            });
            foreach (var labelType in SkylineWindow.Document.Settings.PeptideSettings.Modifications
                .InternalStandardTypes)
            {
                var labelNormalizeOption = NormalizeOption.FromIsotopeLabelType(labelType);

                // Set the normalization method to "Ratio to label" and make sure it gets used by both the Peak Area graph
                // and the Sequence Tree
                RunUI(()=>SkylineWindow.AreaNormalizeOption = labelNormalizeOption);
                WaitForGraphs();
                Assert.AreEqual(labelNormalizeOption.NormalizationMethod.GetAxisTitle(Resources
                    .AreaReplicateGraphPane_UpdateGraph_Peak_Area), areaReplicateGraphPane.YAxis.Title.Text);
                Assert.AreEqual(labelType.Name,
                    (SequenceTreeNormalizationMethod as NormalizationMethod.RatioToLabel)?.IsotopeLabelTypeName);

                // Set the normalization method to "Equalize Medians" and make sure the Sequence Tree is still displaying
                // the original ratio to label
                RunUI(() => SkylineWindow.AreaNormalizeOption =
                    NormalizeOption.FromNormalizationMethod(NormalizationMethod.EQUALIZE_MEDIANS));
                WaitForGraphs();
                Assert.AreEqual(labelType.Name,
                    (SequenceTreeNormalizationMethod as NormalizationMethod.RatioToLabel)?.IsotopeLabelTypeName);
                Assert.AreEqual(NormalizationMethod.EQUALIZE_MEDIANS.GetAxisTitle(Resources
                    .AreaReplicateGraphPane_UpdateGraph_Peak_Area), areaReplicateGraphPane.YAxis.Title.Text);

                // Set the normalization method to "Global Standards" and make sure that gets used by both the
                // peak area graph and the Sequence Tree
                RunUI(()=>SkylineWindow.AreaNormalizeOption = NormalizeOption.GLOBAL_STANDARDS);
                WaitForGraphs();
                Assert.AreEqual(NormalizationMethod.GLOBAL_STANDARDS.GetAxisTitle(Resources
                    .AreaReplicateGraphPane_UpdateGraph_Peak_Area), areaReplicateGraphPane.YAxis.Title.Text);
                Assert.AreEqual(NormalizationMethod.GLOBAL_STANDARDS, SequenceTreeNormalizationMethod);
            }
        }

        private static NormalizationMethod SequenceTreeNormalizationMethod
        {
            get
            {
                return SkylineWindow.SequenceTree.NormalizeOption.NormalizationMethod;
            }
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
