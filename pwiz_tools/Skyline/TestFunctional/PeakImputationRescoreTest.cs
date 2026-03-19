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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Model.RetentionTimes.PeakImputation;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PeakImputationRescoreTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPeakImputationRescore()
        {
            TestFilesZip = @"TestFunctional\RunToRunAlignmentTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("RunToRunAlignmentTest.sky")));
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                peptideSettingsUi.AlignmentTarget = AlignmentTargetSpec.None;
                peptideSettingsUi.OkDialog();
            });
            WaitForDocumentLoaded();
            RunLongDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                RunDlg<RescoreResultsDlg>(manageResultsDlg.Rescore, dlg => dlg.RescoreToFile(TestFilesDir.GetTestPath("RescoredDocument.sky")));
            }, manageResultsDlg => { });
            WaitForDocumentLoaded();
            var rescoredDocument = SkylineWindow.Document;
            var rescoredPeakBounds = GetAllPeakBounds(rescoredDocument);
            RunDlg<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog, reintegrateDlg =>
            {
                reintegrateDlg.OkDialog();
            });
            var reintegratedDocument = SkylineWindow.Document;
            var reintegratedPeaks = GetAllPeakBounds(reintegratedDocument);
            Assert.AreEqual(rescoredPeakBounds, reintegratedPeaks);
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                peptideSettingsUi.MaxRtShift = 1;
                peptideSettingsUi.OkDialog();
            });
            var imputedBoundsMaxShiftOne = GetAllPeakBounds(SkylineWindow.Document);
            Assert.AreNotEqual(rescoredPeakBounds, imputedBoundsMaxShiftOne);
            // Choose a MaxRtShift that is so large as to not affect anything and verify original peak boundaries result
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                peptideSettingsUi.MaxRtShift = 1000;
                peptideSettingsUi.OkDialog();
            });
            var imputedBoundsMaxShiftThousand = GetAllPeakBounds(SkylineWindow.Document);
            Assert.IsNotNull(imputedBoundsMaxShiftOne);
            Assert.AreEqual(rescoredPeakBounds, imputedBoundsMaxShiftThousand);
        }

        private ImmutableList<ImmutableList<ScoredPeakBounds>> GetAllPeakBounds(SrmDocument document)
        {
            var peakBoundaryImputer = new PeakBoundaryImputer(document);
            var allPeakBounds = new List<ImmutableList<ScoredPeakBounds>>();
            var measuredResults = document.MeasuredResults;
            var imputation = document.Settings.PeptideSettings.Imputation;
            Assert.IsNotNull(measuredResults);
            foreach (var moleculeList in document.MoleculeGroups)
            {
                foreach (var molecule in moleculeList.Molecules)
                {
                    var exemplaryPeak = peakBoundaryImputer.GetExemplaryPeak(molecule);
                    Assert.IsNotNull(exemplaryPeak);
                    var exemplaryPeakMidPoint = (exemplaryPeak.Peak.StartTime + exemplaryPeak.Peak.EndTime) / 2;
                    foreach (var transitionGroup in molecule.TransitionGroups)
                    {
                        Assert.IsNotNull(transitionGroup.Results);
                        Assert.AreEqual(measuredResults.Chromatograms.Count, transitionGroup.Results.Count);
                        var peakBoundsList = new List<ScoredPeakBounds>();
                        for (int replicateIndex = 0; replicateIndex < transitionGroup.Results.Count; replicateIndex++)
                        {
                            var message =
                                $"{molecule.Peptide}{Transition.GetChargeIndicator(transitionGroup.PrecursorAdduct)} {measuredResults.Chromatograms[replicateIndex].Name}";
                            var chromInfoList = transitionGroup.GetSafeChromInfo(replicateIndex);
                            Assert.AreEqual(1, chromInfoList.Count, message);
                            var transitionGroupChromInfo = chromInfoList[0];
                            Assert.IsNotNull(transitionGroupChromInfo.OriginalPeak, message);
                            Assert.AreEqual(UserSet.FALSE, transitionGroupChromInfo.UserSet, message);
                            if (transitionGroupChromInfo.RetentionTime.HasValue)
                            {
                                Assert.IsNotNull(transitionGroupChromInfo.StartRetentionTime, message);
                                Assert.IsNotNull(transitionGroupChromInfo.EndRetentionTime, message);
                                peakBoundsList.Add(new ScoredPeakBounds(transitionGroupChromInfo.RetentionTime.Value, transitionGroupChromInfo.StartRetentionTime.Value, transitionGroupChromInfo.EndRetentionTime.Value, 0));
                                var chosenPeakMidPoint = (transitionGroupChromInfo.StartRetentionTime.Value + transitionGroupChromInfo.EndRetentionTime.Value) / 2;
                                if (imputation.MaxRtShift.HasValue)
                                {
                                    // The chosen peak should be within MaxRtShift of the exemplary peak,
                                    // unless the exemplary peak is beyond the chromatogram extraction window in which case the peak should be truncated.
                                    if (Math.Abs(exemplaryPeakMidPoint - chosenPeakMidPoint) >
                                        imputation.MaxRtShift.Value)
                                    {
                                        Assert.AreNotEqual(0, transitionGroupChromInfo.Truncated.Value, message);
                                    }
                                }
                            }
                        }
                        allPeakBounds.Add(peakBoundsList.ToImmutable());
                    }
                }
            }
            return allPeakBounds.ToImmutable();
        }
    }
}
