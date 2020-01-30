/*
 * Original author: Shannon Joyner <saj9191 .at. gmail.com>,
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

using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline;
using pwiz.Skyline.Controls.AuditLog;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;


namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class AuditLogTutorialTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAuditLogTutorial()
        {
            // Set true to look at tutorial screenshots.
            // IsPauseForScreenShots = true;

            ForceMzml = true;            //(Program.PauseSeconds == 0);   // Mzml is ~8x faster for this test.
                                                    
            LinkPdf = "https://skyline.gs.washington.edu/labkey/_webdav/home/software/Skyline/%40files/tutorials/AbsoluteQuant-1_4.pdf";

            TestFilesZipPaths = new[]
            {
                UseRawFiles
                    ? @"https://skyline.gs.washington.edu/tutorials/AbsoluteQuant.zip"
                    : @"https://skyline.gs.washington.edu/tutorials/AbsoluteQuantMzml.zip",
                @"TestTutorial\AbsoluteQuantViews.zip"
            };
            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            var dataFolder = UseRawFiles ? "AuditLog" : "AuditLogMzml"; // Not L10N
            return TestFilesDirs[0].GetTestPath(Path.Combine(dataFolder, relativePath));
        }
        protected override void DoTest()
        {
            var folderAuditLog = UseRawFiles ? "AuditLog" : "AuditLogMzml";

            RunUI(() =>
            {
                SkylineWindow.ResetDefaultSettings();
                SkylineWindow.NewDocument();
                SkylineWindow.ShowAuditLog();
                SkylineWindow.GraphSpectrum.Hide();
            });
            PauseForScreenShot<AuditLogForm>("New document with an empty Audit Log form.");

            // Generating a Transition List, p. 4
            {
                var doc = SkylineWindow.Document;
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);

                RunUI(() =>
                          {
                              // Filter Settings
                              transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Filter;
                              transitionSettingsUI.RangeFrom = Resources.TransitionFilter_FragmentStartFinders_ion_3;
                              transitionSettingsUI.RangeTo = Resources.TransitionFilter_FragmentEndFinders_last_ion_minus_1;
                          });
                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
                WaitForDocumentChange(doc);
            }

            // Configuring Peptide settings p. 3
            PeptideSettingsUI peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications);
            //PauseForScreenShot<PeptideSettingsUI.ModificationsTab>("Peptide Settings - Modification tab", 5);

            var modHeavyK = new StaticMod("Label:13C(6)15N(2) (C-term K)", "K", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15,
                                          RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyK, peptideSettingsUi);
            RunUI(() => peptideSettingsUi.PickedHeavyMods = new[] { modHeavyK.Name });

            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);

            PauseForScreenShot<AuditLogForm>("Audit Log form with settings modifications.");

            if(IsPauseForScreenShots)
                RunUI(SkylineWindow.UndoButton.ShowDropDown);
            PauseForScreenShot("Undo list expanded.");
            if (IsPauseForScreenShots)
                RunUI(SkylineWindow.UndoButton.HideDropDown);

            RunUI(SkylineWindow.Undo);

            if (IsPauseForScreenShots)
                RunUI(SkylineWindow.RedoButton.ShowDropDown);
            PauseForScreenShot("Redo list expanded.");
            if (IsPauseForScreenShots)
                RunUI(SkylineWindow.RedoButton.HideDropDown);

            RunUI(SkylineWindow.Redo);

            // Inserting a peptide sequence p. 5
            using (new CheckDocumentState(1, 1, 2, 10))
            {
                RunUI(() => SetClipboardText("IEAIPQIDK\tGST-tag"));
                var pasteDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg);
                RunUI(pasteDlg.PastePeptides);
                WaitForProteinMetadataBackgroundLoaderCompletedUI();
                RunUI(() => pasteDlg.Size = new Size(700, 210));
                PauseForScreenShot<PasteDlg.PeptideListTab>("Insert Peptide List", 6);

                OkDialog(pasteDlg, pasteDlg.OkDialog);
                WaitForDocumentChange(SkylineWindow.Document);
            }

            RunUI( () =>
            {
                SkylineWindow.ExpandPrecursors();
                SkylineWindow.Size = new Size(840, 410);
                SkylineWindow.AuditLogForm.Close();
            });

            PauseForScreenShot("Main window with Targets view", 6);

            RunUI(SkylineWindow.ShowAuditLog);
            PauseForScreenShot<AuditLogForm>("Audit Log form with inserted peptide.");

            ShowLastExtraInfo("Extra info form.");

            RunUI(() => SkylineWindow.SaveDocument(GetTestPath(folderAuditLog + @"test_file.sky")));
            WaitForCondition(() => File.Exists(GetTestPath(folderAuditLog + @"test_file.sky")));

            // Importing RAW files into Skyline p. 7
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);

            RunUI(() =>
            {
                var rawFiles = DataSourceUtil.GetDataSources(TestFilesDirs[0].FullPath).First().Value.Skip(1);
                var namedPathSets = from rawFile in rawFiles
                                    select new KeyValuePair<string, MsDataFileUri[]>(
                                        rawFile.GetFileNameWithoutExtension(), new[] { rawFile });
                importResultsDlg.NamedPathSets = namedPathSets.ToArray();
            });
            RunDlg<ImportResultsNameDlg>(importResultsDlg.OkDialog,
               importResultsNameDlg => importResultsNameDlg.NoDialog());

            WaitForGraphs();

            RunUI(() =>
                      {
                          SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                          Settings.Default.ArrangeGraphsOrder = GroupGraphsOrder.Document.ToString();
                          Settings.Default.ArrangeGraphsReversed = false;
                          SkylineWindow.ArrangeGraphsTiled();
                          SkylineWindow.AutoZoomBestPeak();
                      });
            WaitForCondition(() => Equals(8, SkylineWindow.GraphChromatograms.Count(graphChrom => !graphChrom.IsHidden)),
                "unexpected visible graphChromatogram count");

            RunUI( () => 
                        {   //resize the window and activate the first standard chromatogram pane.
                            RunUI(() => SkylineWindow.Size = new Size(1330, 720));
                            var chrom = SkylineWindow.GraphChromatograms.First();
                            chrom.Select();
                        });

            WaitForCondition(10 * 60 * 1000,    // ten minutes
                () => SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
            PauseForScreenShot<AuditLogForm>("Audit Log form with imported data records.", 9);

            ShowLastExtraInfo("Extra info form for the import.");
            
            // Peptide Quantitification Settings p. 9
            peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsUi.SelectedTab = (PeptideSettingsUI.TABS)5);
            const string quantUnits = "fmol/ul";
            RunUI(() =>
            {
                peptideSettingsUi.QuantRegressionFit = RegressionFit.LINEAR;
                peptideSettingsUi.QuantNormalizationMethod = new NormalizationMethod.RatioToLabel(IsotopeLabelType.heavy);
                peptideSettingsUi.QuantUnits = quantUnits;
            });
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);

            PauseForScreenShot<AuditLogForm>("Audit Log form with quantification settings.", 9);

            // Specify analyte concentrations of external standards
            RunUI(()=>
            {
                SkylineWindow.AuditLogForm.Close();
                SkylineWindow.ShowDocumentGrid(true);
            });
            var documentGridForm = FindOpenForm<DocumentGridForm>();
            RunUI(() =>
            {
                documentGridForm.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates);
            });
            WaitForConditionUI(() => documentGridForm.IsComplete);

            //TODO: have to do copy/paste instead.
            var concentrations = new[] {40, 12.5, 5, 2.5, 1, .5, .25, .1};
            for (int iRow = 0; iRow < concentrations.Length; iRow++)
            {
                // ReSharper disable AccessToModifiedClosure
                RunUI(() =>
                {
                    var colSampleType = documentGridForm.FindColumn(PropertyPath.Root.Property("SampleType"));
                    documentGridForm.DataGridView.Rows[iRow].Cells[colSampleType.Index].Value = SampleType.STANDARD;
                });
                WaitForConditionUI(() => documentGridForm.IsComplete);
                RunUI(() =>
                {
                    var colAnalyteConcentration =
                        documentGridForm.FindColumn(PropertyPath.Root.Property("AnalyteConcentration"));
                    var cell = documentGridForm.DataGridView.Rows[iRow].Cells[colAnalyteConcentration.Index];
                    documentGridForm.DataGridView.CurrentCell = cell;
                    cell.Value = concentrations[iRow];
                });
                // ReSharper restore AccessToModifiedClosure
                WaitForConditionUI(() => documentGridForm.IsComplete);
            }
            PauseForScreenShot<DocumentGridForm>("Document grid with concentrations filled in", 10);

            RunUI(SkylineWindow.ShowAuditLog);
            PauseForScreenShot<AuditLogForm>("Audit Log form with analyte data.", 9);
            ShowLastExtraInfo("Extra Info for the analyte data import.");

            GraphChromatogram gg = SkylineWindow.GraphChromatograms.First();

            // View the calibration curve p. 13
            RunUI(()=>SkylineWindow.ShowCalibrationForm());
            var calibrationForm = FindOpenForm<CalibrationForm>();
            PauseForScreenShot("View calibration curve", 14);

            Assert.AreEqual(CalibrationCurveFitter.AppendUnits(QuantificationStrings.Analyte_Concentration, quantUnits), calibrationForm.ZedGraphControl.GraphPane.XAxis.Title.Text);
            Assert.AreEqual(string.Format(QuantificationStrings.CalibrationCurveFitter_PeakAreaRatioText__0___1__Peak_Area_Ratio, IsotopeLabelType.light.Title, IsotopeLabelType.heavy.Title),
                calibrationForm.ZedGraphControl.GraphPane.YAxis.Title.Text);
        }

        private static void CheckGstGraphs(int rtCurveCount, int areaCurveCount)
        {
            var graphChrom = SkylineWindow.GetGraphChrom("FOXN1-GST");
            Assert.IsNotNull(graphChrom);
            Assert.IsTrue(graphChrom.BestPeakTime.HasValue);
            Assert.AreEqual(20.9, graphChrom.BestPeakTime.Value, 0.05);
            Assert.AreEqual(rtCurveCount, SkylineWindow.RTGraphController.GraphSummary.CurveCount);
            Assert.AreEqual(9, SkylineWindow.RTGraphController.GraphSummary.Categories.Count());
            Assert.AreEqual(areaCurveCount, SkylineWindow.GraphPeakArea.Controller.GraphSummary.CurveCount);
            Assert.AreEqual(9, SkylineWindow.GraphPeakArea.Controller.GraphSummary.Categories.Count());
        }

        /***
         * Shows AuditLogExtraInfoForm for the most recently performed operation if it is available.
         */
        private void ShowLastExtraInfo(string message)
        {
            RunUI(SkylineWindow.ShowAuditLog);
            if(SkylineWindow.AuditLogForm.DataGridView.Rows.Count > 0 && SkylineWindow.AuditLogForm.DataGridView.Rows[0].Cells.Count > 1)
                if (SkylineWindow.AuditLogForm.DataGridView.Rows[0].Cells[1] is TextImageCell importCell)
                {
                    if (importCell.Items.Length > 0)
                    {
                        var extraInfoDialog = ShowDialog<AuditLogExtraInfoForm>(() => importCell.ClickImage(0));
                        PauseForScreenShot<AuditLogExtraInfoForm>(message);
                        RunUI(extraInfoDialog.OkDialog);
                    }
                }


        }
    }
}
