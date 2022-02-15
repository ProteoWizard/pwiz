/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test for retention time alignment
    /// </summary>
    [TestClass]
    public class RetentionTimeAlignmentTest : AbstractFunctionalTest
    {
        private const double CHROMATOGRAM_WINDOW_LENGTH_MINUTES = 1.0;
        [TestMethod]
        public void TestRetentionTimeAlignment()
        {
            TestFilesZip = @"TestFunctional\RetentionTimeAlignmentTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var seqWithOneId = new Target("TFAEALR");
            var seqWithTwoIds = new Target("AADALLLK");
            SetPeptideSettings();
            SetTransitionSettings();
            InsertPeptides();
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("RetentionTimeAlignmentTest.sky")));
            ImportResultsFile("S_1.mzML");
            ImportResultsFile("S_10.mzML");
            var document = SkylineWindow.Document;
            var chromNames = document.Settings.MeasuredResults.Chromatograms.Select(c => c.Name).ToArray();
            CollectionAssert.AreEqual(
                new[]{"S_1", "S_10"}, 
                chromNames,
                "expected two chromatograms, named S_1 and S_10 - got " + string.Join(", ", chromNames));
            var peptideWithOneId =
                document.Peptides.First(
                    peptideDocNode => seqWithOneId.Equals(peptideDocNode.Peptide.Target));
            var precursorWithOneId = peptideWithOneId.TransitionGroups.First();
            var peptideWithTwoIds =
                document.Peptides.First(
                    peptideDocNode => seqWithTwoIds.Equals(peptideDocNode.Peptide.Target));
            var precursorWithTwoIds = peptideWithTwoIds.TransitionGroups.First();
            Assert.IsTrue(precursorWithOneId.Results[0][0].IsIdentified);
            Assert.IsFalse(precursorWithOneId.Results[1][0].Identified == PeakIdentification.TRUE);
            Assert.IsTrue(precursorWithOneId.Results[1][0].Identified == PeakIdentification.ALIGNED);
            Assert.IsTrue(precursorWithTwoIds.Results[0][0].IsIdentified);
            Assert.IsTrue(precursorWithTwoIds.Results[1][0].IsIdentified);

            var documentRetentionTimes = document.Settings.DocumentRetentionTimes;
            var alignedTo1 = documentRetentionTimes.FileAlignments.Find("S_1");
            var alignedTo10 = documentRetentionTimes.FileAlignments.Find("S_10");
            var af10To1 = alignedTo1.RetentionTimeAlignments.Find("S_10");
            var af1To10 = alignedTo10.RetentionTimeAlignments.Find("S_1");
		    // Verify that the slopes and intercepts are reciprocals of each other.
            // We can only verify this with very coarse precision
            Assert.AreEqual(af10To1.RegressionLine.Slope, 1/af1To10.RegressionLine.Slope, .03);
            Assert.AreEqual(af10To1.RegressionLine.Intercept, -af1To10.RegressionLine.Intercept * af10To1.RegressionLine.Slope, 1);

            var alignedRetentionTimes10To1 = AlignedRetentionTimes.AlignLibraryRetentionTimes(
                document.Settings.GetRetentionTimes("S_1").GetFirstRetentionTimes(),
                document.Settings.GetRetentionTimes("S_10").GetFirstRetentionTimes(),
                DocumentRetentionTimes.REFINEMENT_THRESHHOLD, 
                RegressionMethodRT.linear, CancellationToken.None);
            var alignedRetentionTimes1To10 = AlignedRetentionTimes.AlignLibraryRetentionTimes(
                document.Settings.GetRetentionTimes("S_10").GetFirstRetentionTimes(),
                document.Settings.GetRetentionTimes("S_1").GetFirstRetentionTimes(),
                DocumentRetentionTimes.REFINEMENT_THRESHHOLD, 
                RegressionMethodRT.linear, CancellationToken.None);
            var regressionLine10To1 = (RegressionLineElement) alignedRetentionTimes10To1.RegressionRefined.Conversion;
            Assert.AreEqual(af10To1.RegressionLine.Slope, regressionLine10To1.Slope);
            Assert.AreEqual(af10To1.RegressionLine.Intercept, regressionLine10To1.Intercept);
            var regressionLine1To10 = (RegressionLineElement) alignedRetentionTimes1To10.RegressionRefined.Conversion;
            Assert.AreEqual(af1To10.RegressionLine.Slope, regressionLine1To10.Slope);
            Assert.AreEqual(af1To10.RegressionLine.Intercept, regressionLine1To10.Intercept);


            // Verify that the generated chromatogram is of the expected length around the actual or aligned ID's
            var idTimes = document.Settings.GetRetentionTimes("S_1", seqWithOneId, peptideWithOneId.ExplicitMods);
            VerifyStartEndTime(document, peptideWithOneId, precursorWithOneId, 0, 
                idTimes.Min() - CHROMATOGRAM_WINDOW_LENGTH_MINUTES, 
                idTimes.Max() + CHROMATOGRAM_WINDOW_LENGTH_MINUTES);
            var alignedTimes = document.Settings.GetAllRetentionTimes("S_10", seqWithOneId, peptideWithOneId.ExplicitMods);
            Assert.AreEqual(0, document.Settings.GetRetentionTimes("S_10").GetRetentionTimes(seqWithOneId).Length);
            VerifyStartEndTime(document, peptideWithOneId, precursorWithOneId, 1, 
                alignedTimes.Min() - CHROMATOGRAM_WINDOW_LENGTH_MINUTES, 
                alignedTimes.Max() + CHROMATOGRAM_WINDOW_LENGTH_MINUTES);
            RunUI(()=>SkylineWindow.ComboResults.SelectedIndex = 1);
            var alignmentForm = ShowDialog<AlignmentForm>(() => SkylineWindow.ShowRetentionTimeAlignmentForm());
            RunUI(()=>
                      {
                          var alignAgainstOptions = alignmentForm.ComboAlignAgainst.Items.Cast<object>()
                              .Select(item => item.ToString())
                              .ToArray();
                          CollectionAssert.AreEqual(new[]{"S_1", "S_10"}, alignAgainstOptions);
                          Assert.AreEqual("S_10", alignmentForm.ComboAlignAgainst.SelectedItem.ToString());
                      });
            WaitForConditionUI(10000, () => alignmentForm.RegressionGraph.GraphPane.XAxis.Title.Text == string.Format(Resources.AlignmentForm_UpdateGraph_Time_from__0__,"S_1"),
                () => string.Format("Unexpected x-axis '{0}' found", alignmentForm.RegressionGraph.GraphPane.XAxis.Title.Text));
            RunUI(()=>
                      {
                          var curves = alignmentForm.RegressionGraph.GraphPane.CurveList;
                          var outlierCurve = curves.Find(curveItem => Resources.AlignmentForm_UpdateGraph_Outliers == curveItem.Label.Text);
                          var goodPointsCurve = curves.Find(curveItem => curveItem.Label.Text == Resources.AlignmentForm_UpdateGraph_Peptides_Refined);
                          Assert.AreEqual(alignedRetentionTimes1To10.OutlierIndexes.Count, outlierCurve.Points.Count);
                          Assert.AreEqual(alignedRetentionTimes1To10.RegressionPointCount, outlierCurve.Points.Count + goodPointsCurve.Points.Count);
                      });
            RunUI(alignmentForm.Close);
            RunUI(()=>SkylineWindow.ComboResults.SelectedIndex = 0);
            var alignmentForm2 = ShowDialog<AlignmentForm>(() => SkylineWindow.ShowRetentionTimeAlignmentForm());
            RunUI(()=>Assert.AreEqual("S_1", alignmentForm2.ComboAlignAgainst.SelectedItem.ToString()));
            RunUI(alignmentForm2.Close);
            RunUI(() => SkylineWindow.SaveDocument());
        }

        protected void VerifyStartEndTime(SrmDocument document, PeptideDocNode peptideDocNode, TransitionGroupDocNode transitionGroupDocNode,
                                          int fileIndex, double startTime, double endTime)
        {
            ChromatogramGroupInfo[] infoSet;
            document.Settings.MeasuredResults.TryLoadChromatogram(fileIndex, peptideDocNode, transitionGroupDocNode,
                                                                  (float) TransitionInstrument.DEFAULT_MZ_MATCH_TOLERANCE,
                                                                  out infoSet);
            Assert.AreNotEqual(0, infoSet.Length);
            foreach (var chromatogramGroupInfo in infoSet)
            {
                Assert.AreEqual(startTime, chromatogramGroupInfo.TimeIntensitiesGroup.MinTime, .1);
                Assert.AreEqual(endTime, chromatogramGroupInfo.TimeIntensitiesGroup.MaxTime, .1);
            }
        }

        private void SetPeptideSettings()
        {
            const string libName = "RetentionTimeAlignmentTest";
            var peptideSettingsUI = ShowPeptideSettings();
            Assert.IsFalse(peptideSettingsUI.AvailableLibraries.Contains(libName));
            var editListUI = ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUI.EditLibraryList);
            RunDlg<EditLibraryDlg>(editListUI.AddItem, addLibUI =>
            {
                var nameTextBox = (TextBox)addLibUI.Controls.Find("textName", true)[0];
                Assert.IsNotNull(nameTextBox);
                var pathTextBox = (TextBox)addLibUI.Controls.Find("textPath", true)[0];
                Assert.IsNotNull(pathTextBox);
                nameTextBox.Text = libName;
                pathTextBox.Text = TestFilesDir.GetTestPath("RetentionTimeAlignmentTest.blib");
                addLibUI.OkDialog();
            });
            RunUI(editListUI.OkDialog);
            WaitForClosedForm(editListUI);
            WaitForConditionUI(() => peptideSettingsUI.AvailableLibraries.Contains(libName));
            RunUI(() => peptideSettingsUI.PickedLibraries = new[] { libName });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            WaitForDocumentLoaded();
        }

        private void SetTransitionSettings()
        {
            // Switch to full-scan filtering of precursors in MS1
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI =>
            {
                transitionSettingsUI.FragmentTypes = "p";
                transitionSettingsUI.PrecursorCharges = "2, 3";
                transitionSettingsUI.UseLibraryPick = false;
                transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Percent;
                transitionSettingsUI.PrecursorMassAnalyzer = FullScanMassAnalyzerType.ft_icr;
                transitionSettingsUI.InstrumentMaxMz = 600;
                transitionSettingsUI.SetRetentionTimeFilter(RetentionTimeFilterType.ms2_ids, CHROMATOGRAM_WINDOW_LENGTH_MINUTES);
                transitionSettingsUI.OkDialog();
            });
        }

        private void InsertPeptides()
        {
            var peptides = new[] { "HLVDEPQNLIK", "AEFVEVTK", "AADALLLK", "TFAEALR" };
            var insertPeptidesDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg);

            var doc = SkylineWindow.Document;
            RunUI(()=>
                      {
                          SetClipboardText(string.Join("\n\n", peptides));
                          insertPeptidesDlg.PastePeptides();
                      });
            OkDialog(insertPeptidesDlg, insertPeptidesDlg.OkDialog);
            WaitForDocumentChange(doc);
        }
    }
}
