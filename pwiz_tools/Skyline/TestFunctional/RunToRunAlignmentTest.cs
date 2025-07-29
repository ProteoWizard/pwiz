using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
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
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("RunToRunAlignmentTest.sky")));
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                Assert.AreEqual(AlignmentTargetSpec.Default, peptideSettingsUi.AlignmentTarget);
                peptideSettingsUi.AlignmentTarget = AlignmentTargetSpec.None;
                peptideSettingsUi.OkDialog();
            });
            WaitForDocumentLoaded();
            Assert.AreEqual(null, SkylineWindow.Document.Settings.DocumentRetentionTimes.AlignmentTarget);

            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.AlignmentTarget = AlignmentTargetSpec.Default;
                peptideSettingsUi.OkDialog();
            });
            WaitForDocumentLoaded();
            var alignmentTarget = SkylineWindow.Document.Settings.DocumentRetentionTimes.AlignmentTarget;
            Assert.IsNotNull(alignmentTarget);
            Assert.IsInstanceOfType(alignmentTarget, typeof(AlignmentTarget.LibraryTarget));

            RunDlg<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog, reintegrateDlg =>
            {
                reintegrateDlg.OverwriteManual = true;
                reintegrateDlg.OkDialog();
            });
            var originalDocument = SkylineWindow.Document;
            RunUI(() => { SkylineWindow.ShowRTRegressionGraphScoreToRun(); });
            WaitForGraphs();
            var scoreToRunGraphPane = GetScoreToRunGraphPane();
            Assert.IsNotNull(scoreToRunGraphPane);
            WaitForCondition(() => !scoreToRunGraphPane.IsCalculating);
            foreach (var rtOption in RtCalculatorOption.GetOptions(SkylineWindow.Document))
            {
                RunUI(()=>SkylineWindow.ChooseCalculator(rtOption));
                WaitForGraphs();
                WaitForCondition(() => !scoreToRunGraphPane.IsCalculating);
            }
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.AlignmentTarget = AlignmentTargetSpec.ChromatogramPeaks;
                peptideSettingsUi.OkDialog();
            });
            WaitForDocumentLoaded();
            var originalMedianRetentionTimes =
                RtCalculatorOption.MedianDocRetentionTimes.GetAlignmentTarget(SkylineWindow.Document.Settings);
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.AlignmentTarget = AlignmentTargetSpec.Default;
                peptideSettingsUi.OkDialog();
            });
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
            var reintegratedMissing = CountMissing(reintegratedDocument).ToList();
            RunDlg<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog, reintegrateDlg =>
            {
                reintegrateDlg.ReintegrateAll = false;
                reintegrateDlg.Cutoff = 0.01;
                reintegrateDlg.OkDialog();
            });
            WaitForDocumentLoaded();
            var reintegratedWithCutoff = SkylineWindow.Document;
            var reintegratedWithCutoffMissing = CountMissing(reintegratedWithCutoff).ToList();
            AssertLessThan(reintegratedMissing.Count, reintegratedWithCutoffMissing.Count);
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.ImputeMissingPeaks = true;
                peptideSettingsUi.OkDialog();
            });
            RunDlg<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog, reintegrateDlg =>
            {
                reintegrateDlg.ReintegrateAll = false;
                reintegrateDlg.Cutoff = 0.01;
                reintegrateDlg.OkDialog();
            });
            WaitForDocumentLoaded();
            var reintegratedWithCutoffImpute = SkylineWindow.Document;
            var reintegratedWithCutoffImputeMissingCount = CountMissing(reintegratedWithCutoffImpute);
            AssertLessThan(reintegratedWithCutoffImputeMissingCount.Count, reintegratedWithCutoffMissing.Count);
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
                foreach (var molecule in document.Molecules)
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

        private void AssertLessThan(double a, double b)
        {
            Assert.IsTrue(a < b, "{0} should be less than {1}", a, b);
        }
    }
}
