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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that when training a Default scoring model, features that have some invalid feature scores
    /// are still allowed to be used. Also verifies that the weights are always a linear scaling of
    /// <see cref="LegacyScoringModel.DEFAULT_WEIGHTS"/>.
    /// </summary>
    [TestClass]
    public class LegacyScoringModelTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLegacyScoringModel()
        {
            TestFilesZip = @"TestFunctional\LegacyScoringModelTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Human_plasma.sky")));
            WaitForDocumentLoaded();
            
            var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            var editPeakScoringDlg = ShowDialog<EditPeakScoringModelDlg>(reintegrateDlg.AddPeakScoringModel);
            RunUI(() =>
            {
                editPeakScoringDlg.SelectedModelItem = LegacyScoringModel.DEFAULT_NAME;
                editPeakScoringDlg.UsesDecoys = false;
                editPeakScoringDlg.UsesSecondBest = true;
                editPeakScoringDlg.SelectedGraphTab = 1;
            });
            for (int iRow = 0; iRow < editPeakScoringDlg.PeakCalculatorsGrid.RowCount; iRow++)
            {
                RunUI(()=>editPeakScoringDlg.PeakCalculatorsGrid.SelectRow(iRow));
                var calculatorName = editPeakScoringDlg.PeakCalculatorsGrid.Items[iRow].Name;
                var calculator = PeakFeatureCalculator.Calculators.FirstOrDefault(calc => calc.Name == calculatorName);
                Assert.IsNotNull(calculator);
                if (calculator is MQuestDefaultIntensityCorrelationCalc)
                {
                    Assert.IsTrue(editPeakScoringDlg.SelectedCalculatorHasUnknownScores, calculatorName);
                    RunUI(()=>editPeakScoringDlg.FindMissingValues(iRow));
                    var findResultsWindow = FindOpenForm<FindResultsForm>();
                    Assert.IsNotNull(findResultsWindow);
                    Assert.AreNotEqual(0, findResultsWindow.ItemCount, calculatorName);
                }
                else
                {
                    Assert.IsFalse(editPeakScoringDlg.SelectedCalculatorHasUnknownScores, calculatorName);
                }
            }

            const double episilon = 1e-7;

            int indexIdentifiedCount =
                LegacyScoringModel.DEFAULT_MODEL.PeakFeatureCalculators.IndexOf(calc =>
                    calc is LegacyIdentifiedCountCalc);
            RunUI(editPeakScoringDlg.TrainModelClick);
            var weightsAll = editPeakScoringDlg.PeakCalculatorsGrid.Items.Select(row => row.Weight).ToList();
            Assert.AreEqual(0, weightsAll.Count(w=>!w.HasValue));
            Assert.AreEqual(1.0, NormalizedDotProduct(LegacyScoringModel.DEFAULT_WEIGHTS, weightsAll), episilon);

            // All of the Identified Count values are the same, so that score's contribution should be zero
            Assert.AreEqual(0.0, editPeakScoringDlg.PeakCalculatorsGrid.Items[indexIdentifiedCount].PercentContribution);

            // Disabling the Identified calculator should have no effect on weights because it had no contribution
            RunUI(() =>
            {
                editPeakScoringDlg.PeakCalculatorsGrid.Items[indexIdentifiedCount].IsEnabled = false;
                editPeakScoringDlg.TrainModelClick();
            });
            var weightsWithoutIdentified = editPeakScoringDlg.PeakCalculatorsGrid.Items.Select(row => row.Weight).ToList();
            Assert.AreEqual(1, weightsWithoutIdentified.Count(w => !w.HasValue));
            Assert.AreEqual(1.0, NormalizedDotProduct(LegacyScoringModel.DEFAULT_WEIGHTS, weightsWithoutIdentified), episilon);
            for (int i = 0; i < weightsAll.Count; i++)
            {
                if (weightsWithoutIdentified[i].HasValue)
                {
                    Assert.AreEqual(weightsAll[i], weightsWithoutIdentified[i], editPeakScoringDlg.PeakCalculatorsGrid.Items[i].Name);
                }
            }

            // Disable the dot product score
            int indexLibraryDotProduct = LegacyScoringModel.DEFAULT_MODEL.PeakFeatureCalculators.IndexOf(calc =>
                calc is MQuestDefaultIntensityCorrelationCalc);
            RunUI(() =>
            {
                editPeakScoringDlg.PeakCalculatorsGrid.Items[indexLibraryDotProduct].IsEnabled = false;
                editPeakScoringDlg.TrainModelClick();
            });
            var weightsWithoutLibraryDotProduct =
                editPeakScoringDlg.PeakCalculatorsGrid.Items.Select(row => row.Weight).ToList();
            Assert.IsNull(weightsWithoutLibraryDotProduct[indexLibraryDotProduct]);
            Assert.AreEqual(1.0, NormalizedDotProduct(LegacyScoringModel.DEFAULT_WEIGHTS, weightsWithoutLibraryDotProduct));

            editPeakScoringDlg.PeakScoringModelName = "MyScoringModel";

            OkDialog(editPeakScoringDlg, editPeakScoringDlg.OkDialog);

            // Reintegrate using this scoring model
            OkDialog(reintegrateDlg, reintegrateDlg.OkDialog);
        }

        private double NormalizedDotProduct(IEnumerable<double> vector1, IEnumerable<double?> vector2)
        {
            var list1 = vector1.ToList();
            var list2 = vector2.ToList();
            var itemIndexes = Enumerable.Range(0, list2.Count).Where(i => list2[i].HasValue).ToList();
            var statistics1 = new Statistics(itemIndexes.Select(i => list1[i]));
            var statistics2 = new Statistics(list2.OfType<double>());
            return statistics1.Angle(statistics2);
        }
    }
}
