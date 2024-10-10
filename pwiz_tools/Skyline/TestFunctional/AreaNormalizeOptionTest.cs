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

using System;
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
                // Set the normalization method to "Ratio to label" and make sure it gets used by both the Peak Area graph
                // and the Sequence Tree
                ValidateNormalizeOption(NormalizeOption.FromIsotopeLabelType(labelType),
                    labelType.Name, areaReplicateGraphPane);

                // Set the normalization method to "Equalize Medians" and make sure the Sequence Tree is still displaying
                // the original ratio to label
                ValidateNormalizeOption(NormalizeOption.FromNormalizationMethod(NormalizationMethod.EQUALIZE_MEDIANS),
                    labelType.Name, areaReplicateGraphPane);

                // Set the normalization method to "Global Standards" and make sure that gets used by both the
                // peak area graph and the Sequence Tree
                ValidateNormalizeOption(NormalizeOption.GLOBAL_STANDARDS,
                    NormalizationMethod.GLOBAL_STANDARDS.Name, areaReplicateGraphPane);
            }
        }

        private static void ValidateNormalizeOption(NormalizeOption labelNormalizeOption, string labelName,
            AreaReplicateGraphPane areaReplicateGraphPane)
        {
            RunUI(() => SkylineWindow.AreaNormalizeOption = labelNormalizeOption);
            
            WaitForGraphs();
            // Unfortunately, WaitForGraphs does not quite guarantee that everything
            // that will be tested is fully updated.
            TryWaitForConditionUI(() =>
                MatchExpected(GetYAxisValues(labelNormalizeOption)) &&
                MatchExpected(GetSequenceTreeValues(labelName)));

            RunUI(() =>
            {
                ValidateAreaGraphYAxis(labelNormalizeOption);
                ValidateSequenceTreeNormalization(labelName);
            });
        }

        private static bool MatchExpected(Tuple<string, string> values)
        {
            return Equals(values.Item1, values.Item2);
        }

        private static Tuple<string, string> GetYAxisValues(NormalizeOption labelNormalizeOption)
        {
            var areaReplicateGraphPane = FindGraphPane<AreaReplicateGraphPane>();
            Assert.IsNotNull(areaReplicateGraphPane);
            string expected = labelNormalizeOption.NormalizationMethod.GetAxisTitle(Resources
                .AreaReplicateGraphPane_UpdateGraph_Peak_Area);
            string actual = areaReplicateGraphPane.YAxis.Title.Text;
            return new Tuple<string, string>(expected, actual);
        }

        private static void ValidateAreaGraphYAxis(NormalizeOption labelNormalizeOption)
        {
            var values = GetYAxisValues(labelNormalizeOption);
            Assert.AreEqual(values.Item1, values.Item2, 
                string.Format("Unexpected y-axis text. SkylineWindow.AreaNormalizeOption=<{0}>.", SkylineWindow.AreaNormalizeOption));
        }

        private static Tuple<string, string> GetSequenceTreeValues(string labelName)
        {
            var ratioToLabel = SequenceTreeNormalizationMethod as NormalizationMethod.RatioToLabel;
            string actualName = ratioToLabel != null
                ? ratioToLabel.IsotopeLabelTypeName
                : NormalizeOption.GLOBAL_STANDARDS.NormalizationMethod.Name;
            return new Tuple<string, string>(labelName, actualName);
        }

        private static void ValidateSequenceTreeNormalization(string labelName)
        {
            var values = GetSequenceTreeValues(labelName);
            Assert.AreEqual(values.Item1, values.Item2,
                string.Format("Unexpected SequenceTree normalization method. SkylineWindow.AreaNormalizeOption=<{0}>.", SkylineWindow.AreaNormalizeOption));
        }

        private static NormalizationMethod SequenceTreeNormalizationMethod
        {
            get
            {
                return SkylineWindow.SequenceTree.NormalizeOption.NormalizationMethod;
            }
        }

        private static T FindGraphPane<T>() where T : class
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
