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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests the run-to-run alignment dropdown on the Peptide Settings Prediction
    /// </summary>
    [TestClass]
    public class RunToRunAlignmentTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRunToRunAlignment()
        {
            TestFilesZip = @"TestFunctional\RunToRunAlignmentTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("RunToRunAlignmentTest.sky")));
            var startDocument = WaitForDocumentLoaded();
            RunLongDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                RunDlg<RescoreResultsDlg>(manageResultsDlg.Rescore, dlg =>
                {
                    dlg.Rescore(false);
                });
            }, x => { });
            WaitForDocumentChangeLoaded(startDocument);
            int originalPeakCount = CountOriginalPeaks(SkylineWindow.Document);
            Assert.AreNotEqual(0, originalPeakCount);
            Assert.AreEqual(0, CountReintegratedPeaks(SkylineWindow.Document));
            Assert.AreEqual(0, CountQValues(SkylineWindow.Document));
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                Assert.AreEqual(AlignmentTargetSpec.Default, peptideSettingsUi.AlignmentTarget);
                peptideSettingsUi.AlignmentTarget = AlignmentTargetSpec.None;
                peptideSettingsUi.OkDialog();
            });
            WaitForDocumentLoaded();
            Assert.IsTrue(SkylineWindow.Document.Settings.TryGetAlignmentTarget(out var alignmentTarget));
            Assert.IsNull(alignmentTarget);

            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.AlignmentTarget = AlignmentTargetSpec.Default;
                peptideSettingsUi.OkDialog();
            });
            WaitForDocumentLoaded();
            Assert.IsTrue(SkylineWindow.Document.Settings.TryGetAlignmentTarget(out alignmentTarget));
            Assert.IsInstanceOfType(alignmentTarget, typeof(AlignmentTarget.LibraryTarget));

            RunDlg<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog, reintegrateDlg =>
            {
                reintegrateDlg.OverwriteManual = true;
                reintegrateDlg.OkDialog();
            });
            Assert.AreEqual(originalPeakCount, CountOriginalPeaks(SkylineWindow.Document));
            Assert.AreEqual(0, CountReintegratedPeaks(SkylineWindow.Document));
            var originalDocument = SkylineWindow.Document;
            RunUI(() => { SkylineWindow.ShowRTRegressionGraphScoreToRun(); });
            WaitForGraphs();
            var scoreToRunGraphPane = GetScoreToRunGraphPane();
            Assert.IsNotNull(scoreToRunGraphPane);
            WaitForCondition(() => scoreToRunGraphPane.IsComplete);
            foreach (var rtOption in RtCalculatorOption.GetOptions(SkylineWindow.Document))
            {
                RunUI(() => SkylineWindow.ChooseCalculator(rtOption));
                WaitForGraphs();
                WaitForCondition(() => scoreToRunGraphPane.IsComplete);
            }

            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.AlignmentTarget = AlignmentTargetSpec.ChromatogramPeaks;
                peptideSettingsUi.OkDialog();
            });
            WaitForDocumentLoaded();
            var originalMedianRetentionTimes =
                RtCalculatorOption.MedianDocRetentionTimes.GetAlignmentTarget(SkylineWindow.Document.Settings);
            Assert.AreEqual(originalMedianRetentionTimes,
                new AlignmentTarget.MedianDocumentRetentionTimes(SkylineWindow.Document));
            Assert.AreEqual(originalMedianRetentionTimes,
                new AlignmentTarget.MedianDocumentRetentionTimes(originalDocument));

            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.AlignmentTarget = AlignmentTargetSpec.Default;
                peptideSettingsUi.OkDialog();
            });

            // Train a peak scoring model
            RunLongDlg<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog, reintegrateDlg =>
            {
                RunDlg<EditPeakScoringModelDlg>(reintegrateDlg.AddPeakScoringModel, editPeakScoringModelDlg =>
                {
                    editPeakScoringModelDlg.PeakScoringModelName = "MyScoringModel";
                    editPeakScoringModelDlg.TrainModel();
                    editPeakScoringModelDlg.OkDialog();
                });
            }, reintegrateDlg => reintegrateDlg.OkDialog());
            WaitForDocumentLoaded();
            var reintegratedDocument = SkylineWindow.Document;
            Assert.AreEqual(originalPeakCount, CountOriginalPeaks(reintegratedDocument));
            int reintegratedPeakCount = CountReintegratedPeaks(reintegratedDocument);
            Assert.AreNotEqual(0, reintegratedPeakCount);
            int reintegratedQValueCount = CountQValues(reintegratedDocument);
            Assert.AreNotEqual(0, reintegratedQValueCount);
            var reintegratedMissingCounts = CountMissing(reintegratedDocument).ToList();

            // Reintegrate with a 0.01 q-value cutoff
            RunDlg<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog, reintegrateDlg =>
            {
                reintegrateDlg.ReintegrateAll = false;
                reintegrateDlg.Cutoff = 0.01;
                reintegrateDlg.OkDialog();
            });
            WaitForDocumentLoaded();
            var reintegratedWithCutoffDoc = SkylineWindow.Document;
            var reintegratedWithCutoffMissingCounts = CountMissing(reintegratedWithCutoffDoc);
            Assert.AreEqual(originalPeakCount, CountOriginalPeaks(reintegratedWithCutoffDoc));
            AssertLessThan(reintegratedMissingCounts.Count, reintegratedWithCutoffMissingCounts.Count);
            int reintegratedWithCutoffReintegratedPeakCount = CountReintegratedPeaks(reintegratedWithCutoffDoc);
            AssertLessThan(reintegratedWithCutoffReintegratedPeakCount, reintegratedPeakCount);
            int reintegratedWithCutoffQValueCount = CountQValues(reintegratedWithCutoffDoc);
            Assert.AreEqual(reintegratedQValueCount, reintegratedWithCutoffQValueCount);

            // Set ImputeMissingPeaks = true and reintegrate with 0.01 q-value cutoff
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.ImputeMissingPeaks = true;
                peptideSettingsUi.OkDialog();
            });
            Assert.AreEqual(reintegratedWithCutoffReintegratedPeakCount, CountReintegratedPeaks(SkylineWindow.Document));
            RunDlg<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog, reintegrateDlg =>
            {
                reintegrateDlg.ReintegrateAll = false;
                reintegrateDlg.Cutoff = 0.01;
                reintegrateDlg.OkDialog();
            });
            WaitForDocumentLoaded();
            var reintegratedWithCutoffImputeDoc = SkylineWindow.Document;

            // Should be fewer missing peaks because impute missing
            var reintegratedWithCutoffImputeMissingCounts = CountMissing(reintegratedWithCutoffImputeDoc);
            AssertLessThan(reintegratedWithCutoffImputeMissingCounts.Values.Sum(),
                reintegratedWithCutoffMissingCounts.Values.Sum());
            CollectionAssert.IsSubsetOf(reintegratedWithCutoffImputeMissingCounts.Keys,
                reintegratedWithCutoffMissingCounts.Keys);

            var reintegratedMedianRetentionTimes =
                new AlignmentTarget.MedianDocumentRetentionTimes(reintegratedDocument);
            Assert.AreNotEqual(originalMedianRetentionTimes, reintegratedMedianRetentionTimes);

            // Q-value cutoff changes median retention times
            var reintegratedWithCutoffMedianRetentionTimes =
                new AlignmentTarget.MedianDocumentRetentionTimes(reintegratedWithCutoffDoc);
            Assert.AreNotEqual(reintegratedMedianRetentionTimes, reintegratedWithCutoffMedianRetentionTimes);

            // Impute missing does not change median retention times because median retention times are based on pre-imputation peaks
            var reintegratedWithCutoffImputeRetentionTimes =
                new AlignmentTarget.MedianDocumentRetentionTimes(reintegratedWithCutoffImputeDoc);
            Assert.AreEqual(reintegratedWithCutoffImputeRetentionTimes, reintegratedWithCutoffImputeRetentionTimes);
            
            RunUI(()=>SkylineWindow.SetIntegrateAll(!SkylineWindow.DocumentUI.Settings.TransitionSettings.Integration.IsIntegrateAll));
            WaitForDocumentLoaded();
            Assert.AreEqual(originalPeakCount, CountOriginalPeaks(SkylineWindow.Document));
            Assert.AreEqual(reintegratedWithCutoffReintegratedPeakCount, CountReintegratedPeaks(SkylineWindow.Document));
            Assert.AreEqual(reintegratedWithCutoffQValueCount, CountQValues(SkylineWindow.Document));
        }

        public static RTLinearRegressionGraphPane GetScoreToRunGraphPane()
        {
            return FormUtil.OpenForms.OfType<GraphSummary>().Select(graphSummary => graphSummary.GraphControl.GraphPane)
                .OfType<RTLinearRegressionGraphPane>().FirstOrDefault(graphPane => !graphPane.RunToRun);
        }

        private Dictionary<IdentityPath, int> CountMissing(SrmDocument document)
        {
            var dictionary = new Dictionary<IdentityPath, int>();
            int replicateCount = document.Settings.MeasuredResults?.Chromatograms.Count ?? 0;

            foreach (var moleculeGroup in document.MoleculeGroups)
            {
                foreach (var molecule in moleculeGroup.Molecules)
                {
                    int countMissing = Enumerable.Range(0, replicateCount).Count(replicateIndex =>
                        molecule.TransitionGroups.Any(tg =>
                            tg.GetSafeChromInfo(replicateIndex).Any(transitionGroupChromInfo =>
                                transitionGroupChromInfo.RetentionTime == null)));
                    if (countMissing > 0)
                    {
                        dictionary.Add(new IdentityPath(moleculeGroup.PeptideGroup, molecule.Peptide), countMissing);
                    }
                }
            }

            return dictionary;
        }

        private int CountOriginalPeaks(SrmDocument document)
        {
            return document.MoleculeTransitionGroups.Sum(tg => tg.GetChromInfos(null)
                .Count(chromInfo => chromInfo.OriginalPeak != null));
        }

        private int CountReintegratedPeaks(SrmDocument document)
        {
            return document.MoleculeTransitionGroups.Sum(tg => tg.GetChromInfos(null)
                .Count(chromInfo => chromInfo.ReintegratedPeak != null));
        }

        private int CountQValues(SrmDocument document)
        {
            return document.MoleculeTransitionGroups.Sum(tg =>
                tg.GetChromInfos(null).Count(chromInfo => chromInfo.QValue.HasValue));
        }
        
        private void AssertLessThan(double a, double b)
        {
            Assert.IsTrue(a < b, "{0} should be less than {1}", a, b);
        }
    }
}
