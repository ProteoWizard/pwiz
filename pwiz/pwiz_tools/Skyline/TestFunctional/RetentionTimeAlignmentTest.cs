using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
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
            const string seqWithOneId = "TFAEALR";
            const string seqWithTwoIds = "AADALLLK";
            SetPeptideSettings();
            SetTransitionSettings();
            InsertPeptides();
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("RetentionTimeAlignmentTest.sky")));
            ImportResultsFile("S_1.mzML");
            ImportResultsFile("S_10.mzML");
            WaitForDocumentLoaded();
            var document = SkylineWindow.Document;
            CollectionAssert.AreEqual(
                new[]{"S_1", "S_10"}, 
                document.Settings.MeasuredResults.Chromatograms.Select(c=>c.Name).ToArray());
            var peptideWithOneId =
                document.Peptides.First(
                    peptideDocNode => seqWithOneId.Equals(peptideDocNode.Peptide.Sequence));
            var precursorWithOneId = peptideWithOneId.TransitionGroups.First();
            var peptideWithTwoIds =
                document.Peptides.First(
                    peptideDocNode => seqWithTwoIds.Equals(peptideDocNode.Peptide.Sequence));
            var precursorWithTwoIds = peptideWithTwoIds.TransitionGroups.First();
            Assert.IsTrue(precursorWithOneId.Results[0][0].Identified);
            Assert.IsFalse(precursorWithOneId.Results[1][0].Identified);
            Assert.IsTrue(precursorWithTwoIds.Results[0][0].Identified);
            Assert.IsTrue(precursorWithTwoIds.Results[1][0].Identified);
            
            var loadedRetentionTimes = LoadedRetentionTimes.StartLoadFromAllLibraries(SkylineWindow.Document).Result;
            var alignedTo1 = loadedRetentionTimes.GetRetentionTimesAlignedToFile("S_1");
            var alignedTo10 = loadedRetentionTimes.GetRetentionTimesAlignedToFile("S_10");
            var af10To1 = alignedTo1.AlignedFiles.First();
            var af1To10 = alignedTo10.AlignedFiles.First();

            // Verify that the slopes and intercepts are reciprocals of each other.
            // We can only verify this to the precision of .01
            Assert.AreEqual(af10To1.RegressionLine.Slope, 1/af1To10.RegressionLine.Slope, .01);
            Assert.AreEqual(af10To1.RegressionLine.Intercept, -af1To10.RegressionLine.Intercept * af10To1.RegressionLine.Slope, .1);

            // Verify that the generated chromatogram is of the expected length around the actual or aligned ID's
            var idTimes = alignedTo1.TargetTimes.GetRetentionTimes(seqWithOneId);
            VerifyStartEndTime(document, precursorWithOneId, 0, 
                idTimes.Min() - CHROMATOGRAM_WINDOW_LENGTH_MINUTES, 
                idTimes.Max() + CHROMATOGRAM_WINDOW_LENGTH_MINUTES);
            var alignedTimes = alignedTo10.GetAlignedRetentionTimes(seqWithOneId);
            Assert.AreEqual(0, alignedTo10.TargetTimes.GetRetentionTimes(seqWithOneId).Length);
            VerifyStartEndTime(document, precursorWithOneId, 1, 
                alignedTimes.Min() - CHROMATOGRAM_WINDOW_LENGTH_MINUTES, 
                alignedTimes.Max() + CHROMATOGRAM_WINDOW_LENGTH_MINUTES);
            var alignmentForm = ShowDialog<AlignmentForm>(() => SkylineWindow.ShowRetentionTimeAlignmentForm());
            // TODO(nicksh): Test some things on the AlignmentForm.
            RunUI(alignmentForm.Close);
        }

        protected void VerifyStartEndTime(SrmDocument document, TransitionGroupDocNode transitionGroupDocNode, int fileIndex, double startTime, double endTime)
        {
            ChromatogramGroupInfo[] infoSet;
            document.Settings.MeasuredResults.TryLoadChromatogram(fileIndex, transitionGroupDocNode,
                                                                  (float) TransitionInstrument.DEFAULT_MZ_MATCH_TOLERANCE, true,
                                                                  out infoSet);
            Assert.AreNotEqual(0, infoSet.Length);
            foreach (var chromatogramGroupInfo in infoSet)
            {
                Assert.AreEqual(startTime, chromatogramGroupInfo.Times.First(), .1);
                Assert.AreEqual(endTime, chromatogramGroupInfo.Times.Last(), .1);
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
            RunUI(peptideSettingsUI.OkDialog);
            WaitForClosedForm(peptideSettingsUI);
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

            RunUI(()=>
                      {
                          SetClipboardText(string.Join("\n\n", peptides));
                          insertPeptidesDlg.PastePeptides();
                      });
            OkDialog(insertPeptidesDlg, insertPeptidesDlg.OkDialog);
        }
    }
}
