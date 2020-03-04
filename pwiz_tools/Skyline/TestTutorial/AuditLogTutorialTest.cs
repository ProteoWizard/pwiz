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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.AuditLog;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.AuditLog.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using ZedGraph;


namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class AuditLogTutorialTest : AbstractFunctionalTest
    {
        public const string SERVER_URL = "https://panoramaweb.org/";
        public const string PANORAMA_FOLDER = "SkylineTest";
        public const string PANORAMA_USER_NAME = "ritach@uw.edu";
        public const string PANORAMA_PASSWORD = "lclcmsms";

        public string testFolderName = "AuditLogUpload";

        [TestMethod]
        public void TestAuditLogTutorial()
        {
            // Set true to look at tutorial screenshots.
            // IsPauseForScreenShots = true;

            ForceMzml = (Program.PauseSeconds == 0);   // Mzml is ~8x faster for this test.
                                                    
            LinkPdf = "https://skyline.gs.washington.edu/labkey/_webdav/home/software/Skyline/%40files/tutorials/AbsoluteQuant-1_4.pdf";

            TestFilesZipPaths = new[]
            {
                UseRawFiles
                    ? @"https://skyline.gs.washington.edu/tutorials/AuditLog.zip"
                    : @"https://skyline.gs.washington.edu/tutorials/AuditLogMzml.zip",
                @"TestTutorial\AbsoluteQuantViews.zip"
            };

            if(IsPauseForScreenShots)
                PanoramaSetup();

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
            PauseForScreenShot<AuditLogForm>("New document with an empty Audit Log form.", 2);

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

            var modHeavyK = new StaticMod("Label:13C(6)15N(2) (C-term K)", "K", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15,
                                          RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyK, peptideSettingsUi);
            RunUI(() => peptideSettingsUi.PickedHeavyMods = new[] { modHeavyK.Name });

            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);

            PauseForScreenShot<AuditLogForm>("Audit Log form with settings modifications.", 3);

            if(IsPauseForScreenShots)
                RunUI(SkylineWindow.UndoButton.ShowDropDown);
            PauseForScreenShot("Undo list expanded.", 4);
            if (IsPauseForScreenShots)
                RunUI(SkylineWindow.UndoButton.HideDropDown);

            RunUI(SkylineWindow.Undo);

            if (IsPauseForScreenShots)
                RunUI(SkylineWindow.RedoButton.ShowDropDown);
            PauseForScreenShot("Redo list expanded.", 4);
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
                PauseForScreenShot<PasteDlg.PeptideListTab>("Insert Peptide List", 5);

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
            PauseForScreenShot<AuditLogForm>("Audit Log form with inserted peptide.", 6);

            ShowLastExtraInfo("Extra info form with inserted peptide info.", 7);

            RunUI(() => SkylineWindow.SaveDocument(GetTestPath(folderAuditLog + @"test_file.sky")));
            WaitForCondition(() => File.Exists(GetTestPath(folderAuditLog + @"test_file.sky")));

            // Importing RAW files into Skyline p. 7
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);

            RunUI(() =>
            {
                var rawFiles = DataSourceUtil.GetDataSources(TestFilesDirs[0].FullPath).First().Value;
                var namedPathSets = from rawFile in rawFiles
                                    select new KeyValuePair<string, MsDataFileUri[]>(
                                        rawFile.GetFileNameWithoutExtension(), new[] { rawFile });
                importResultsDlg.NamedPathSets = namedPathSets.ToArray();
            });
            OkDialog(importResultsDlg, importResultsDlg.OkDialog);

            WaitForGraphs();

            RunUI(() =>
                      {
                          SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                          Settings.Default.ArrangeGraphsOrder = GroupGraphsOrder.Document.ToString();
                          Settings.Default.ArrangeGraphsReversed = false;
                          SkylineWindow.ArrangeGraphsTabbed();
                          SkylineWindow.AutoZoomBestPeak();
                      });
            WaitForCondition(() => Equals(9, SkylineWindow.GraphChromatograms.Count(graphChrom => !graphChrom.IsHidden)),
                "unexpected visible graphChromatogram count");

            RunUI( () => 
                    {   //resize the window and activate the first standard chromatogram pane.
                        SkylineWindow.Size = new Size(1330, 720);
            });

            WaitForCondition(10 * 60 * 1000,    // ten minutes
                () => SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
            RunUI(SkylineWindow.GetGraphChrom("FOXN1-GST").Show);

            PauseForScreenShot<AuditLogForm>("Audit Log form with imported data files.", 8);

            ShowLastExtraInfo("Extra info form for the import.", 8);
            
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

            var concentrations = new[] {40, 12.5, 5, 2.5, 1, .5, .25, .1};

            string pasteString = TextUtil.LineSeparate(concentrations.Select((f, i) =>
                QuantificationStrings.SampleType_STANDARD_Standard + "\t" + f
            ));
            ClipboardEx.SetText(pasteString);

            RunUI(() =>
                {
                    var colSampleType = documentGridForm.FindColumn(PropertyPath.Root.Property("SampleType"));
                    documentGridForm.DataGridView.CurrentCell = documentGridForm.DataGridView.Rows[1].Cells[colSampleType.Index];
                    documentGridForm.DataGridView.SendPaste();
                });

            // ReSharper restore AccessToModifiedClosure
            WaitForConditionUI(() => documentGridForm.IsComplete);
            PauseForScreenShot<DocumentGridForm>("Document grid with concentrations filled in", 10);

            RunUI(SkylineWindow.ShowAuditLog);
            PauseForScreenShot<AuditLogForm>("Audit Log form with analyte data.", 11);
            ShowLastExtraInfo("Extra Info for the analyte data import.", 11);

            var listChanges = new List<ChangedPeakBoundsEventArgs>();
            RunUI(()=>
            {
                documentGridForm.Close();
                SkylineWindow.AuditLogForm.Close();
                var graphChrom = SkylineWindow.GetGraphChrom("FOXN1-GST");

                var pathPep = SkylineWindow.DocumentUI.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                var nodeGroup = SkylineWindow.DocumentUI.Peptides.ElementAt(0).TransitionGroups.Last();
                var firstPeak = graphChrom.GraphItems.First();
                var scaledStartTime = firstPeak.ScaleRetentionTime(firstPeak.TransitionChromInfo.StartRetentionTime);
                listChanges = new List<ChangedPeakBoundsEventArgs>
                {
                    new ChangedPeakBoundsEventArgs(new IdentityPath(pathPep, nodeGroup.TransitionGroup),
                        null,
                        graphChrom.NameSet,
                        graphChrom.ChromGroupInfos[0].FilePath,
                        scaledStartTime,
                        graphChrom.GraphItems.First().GetValidPeakBoundaryTime(21.1),
                        PeakIdentification.ALIGNED,
                        PeakBoundsChangeType.end)
                };
                SkylineWindow.SelectedPath = SkylineWindow.DocumentUI.GetPathTo((int)SrmDocument.Level.TransitionGroups, 1);

            });

            PauseForScreenShot("Heavy precursor chromatogram", 12);

            RunUI(() =>
            {
                var graphChrom = SkylineWindow.GetGraphChrom("FOXN1-GST");
                graphChrom.SimulateChangedPeakBounds(listChanges);
            });

            RunUI(SkylineWindow.ShowAuditLog);
            WaitForOpenForm<AuditLogForm>();
            WaitForConditionUI(500, () => SkylineWindow.AuditLogForm.DataGridView.Rows.Count > 0);

            PauseForScreenShot<AuditLogForm>("Audit Log form with changed integration boundary.", 13);
            RunUI(() =>
            {
                var colReason = SkylineWindow.AuditLogForm.FindColumn(PropertyPath.Root.Property("Reason"));
                var cell = SkylineWindow.AuditLogForm.DataGridView.Rows[0].Cells[colReason.Index];
                SkylineWindow.AuditLogForm.DataGridView.CurrentCell = cell;
                cell.Value = "Changed end boundary to better fit the peak.";

                foreach (DataGridViewColumn col in SkylineWindow.AuditLogForm.DataGridView.Columns)
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;

            });
            SetGridFormToFullWidth((SkylineWindow.AuditLogForm));
            PauseForScreenShot<AuditLogForm>("Audit Log form with updated reason.", 13);

            // View the calibration curve p. 13
            RunUI(()=>SkylineWindow.ShowCalibrationForm());
            var calibrationForm = FindOpenForm<CalibrationForm>();
            RunUI(() =>
            {
                PointF centerPoint =  calibrationForm.ZedGraphControl.GraphPane.GeneralTransform(new PointF(0, 0), CoordType.AxisXYScale);
                calibrationForm.ZedGraphControl.ZoomPane(calibrationForm.ZedGraphControl.GraphPane, 0.52, centerPoint, true);
            });

            Assert.AreEqual(CalibrationCurveFitter.AppendUnits(QuantificationStrings.Analyte_Concentration, quantUnits), calibrationForm.ZedGraphControl.GraphPane.XAxis.Title.Text);
            Assert.AreEqual(string.Format(QuantificationStrings.CalibrationCurveFitter_PeakAreaRatioText__0___1__Peak_Area_Ratio, IsotopeLabelType.light.Title, IsotopeLabelType.heavy.Title),
                calibrationForm.ZedGraphControl.GraphPane.YAxis.Title.Text);

            PauseForScreenShot<CalibrationForm>("View calibration curve", 14);
            RunUI(() =>
            {
                var chroms = SkylineWindow.DocumentUI.Settings.MeasuredResults.Chromatograms;
                new []{5, 6, 7, 8}.ForEach((index) =>
                {
                    int replicateIdx = chroms.IndexOf((ch) => ch.Name.Equals("Standard_" + index));
                    if (replicateIdx >= 0)
                    {
                        ToolStripMenuItem excludeStandardMenu = calibrationForm.MakeExcludeStandardMenuItem(replicateIdx);
                        excludeStandardMenu?.PerformClick();
                        excludeStandardMenu?.Dispose();
                    }
                });
                calibrationForm.Close();
                SkylineWindow.ShowAuditLog();
            });

            PauseForScreenShot<AuditLogForm>("Show Audit Log with excluded standard records.", 14);
            RunUI(() =>
            {
                var colReason = SkylineWindow.AuditLogForm.FindColumn(PropertyPath.Root.Property("Reason"));
                var cell = SkylineWindow.AuditLogForm.DataGridView.Rows[0].Cells[colReason.Index];
                SkylineWindow.AuditLogForm.DataGridView.CurrentCell = cell;
                cell.Value = "Excluded standard since it was below LOD.";
            });

            if(IsPauseForScreenShots)
                RunUI(SkylineWindow.AuditLogForm.NavBar.ReportsButton.ShowDropDown);

            PauseForScreenShot<AuditLogForm>("Audit Log Reports menu.", 15);

            RunUI(() =>
            {
                SkylineWindow.AuditLogForm.ChooseView(AuditLogStrings.AuditLogForm_MakeAuditLogForm_Undo_Redo);
            });
            SetGridFormToFullWidth((SkylineWindow.AuditLogForm));
            PauseForScreenShot<AuditLogForm>("Audit Log with UndoRedo view.", 15);

            var customizeDialog = ShowDialog<ViewEditor>(SkylineWindow.AuditLogForm.NavBar.CustomizeView);

            RunUI(() =>
            {
                customizeDialog.ViewName = "Custom Columns";
                var columnsToAdd = new[]
                                       {
                                           PropertyPath.Parse("SkylineVersion"),
                                           PropertyPath.Parse("User"),
                                       };
                foreach (var id in columnsToAdd)
                {
                    Assert.IsTrue(customizeDialog.ChooseColumnsTab.TrySelect(id), "Unable to select {0}", id);
                    customizeDialog.ChooseColumnsTab.AddSelectedColumn();
                }
            });
            PauseForScreenShot<ViewEditor.ChooseColumnsView>("Custom columns selection.", 16);
            OkDialog(customizeDialog, customizeDialog.OkDialog);
            SetGridFormToFullWidth((SkylineWindow.AuditLogForm));
            PauseForScreenShot<AuditLogForm>("Audit Log with custom view.", 16);

            if (IsPauseForScreenShots)
            {
                var registrationDialog = ShowDialog<MultiButtonMsgDlg>(() => { 
                    SkylineWindow.ShowPublishDlg(null);
                });
                PauseForScreenShot<MultiButtonMsgDlg>("Upload confirmation dialog.", 16);
                RunUI(registrationDialog.ClickNo);

                var loginDialog = WaitForOpenForm<EditServerDlg>();
                PauseForScreenShot<EditServerDlg>("Login dialog.");

                RunUI(() =>
                {
                    loginDialog.URL = SERVER_URL;
                    loginDialog.Username = PANORAMA_USER_NAME;
                    loginDialog.Password = PANORAMA_PASSWORD;
                });
                RunUI(loginDialog.OkDialog);

                var publishDialog = WaitForOpenForm<PublishDocumentDlg>();
                WaitForCondition(() => publishDialog.IsLoaded);
                RunUI(() =>
                {
                    publishDialog.SelectItem(testFolderName);
                });
                PauseForScreenShot<PublishDocumentDlg>("Folder selection dialog.");
                RunUI(publishDialog.OkDialog);

                var browserConfirmationDialog = WaitForOpenForm<MultiButtonMsgDlg>();
                RunUI(browserConfirmationDialog.ClickYes);

                PauseForScreenShot("Uploaded document in Panorama (in browser).");

                Regex reDocId = new Regex(@"\?id=([0-9]+)");
                Assert.IsTrue(reDocId.IsMatch(publishDialog.PanoramaPublishClient.UploadedDocumentUri.ToString()));

                string docId = reDocId.Match(publishDialog.PanoramaPublishClient.UploadedDocumentUri.ToString()).Groups[1].Captures[0].Value;
                Uri serverUri = new Uri(SERVER_URL);
                Uri requestUri = PanoramaUtil.Call(serverUri, "targetedms", String.Format("{0}/{1}", PANORAMA_FOLDER, testFolderName),
                    "showSkylineAuditLog", "id=" + docId);
                Process.Start(requestUri.ToString());
                PauseForScreenShot("Uploaded document audit log in Panorama (in browser).");
            }
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
        private void ShowLastExtraInfo(string message, int? pageNum = null)
        {
            RunUI(SkylineWindow.ShowAuditLog);
            if(SkylineWindow.AuditLogForm.DataGridView.Rows.Count > 0 && SkylineWindow.AuditLogForm.DataGridView.Rows[0].Cells.Count > 1)
                if (SkylineWindow.AuditLogForm.DataGridView.Rows[0].Cells[1] is TextImageCell importCell)
                {
                    if (importCell.Items.Length > 0)
                    {
                        var extraInfoDialog = ShowDialog<AuditLogExtraInfoForm>(() => importCell.ClickImage(0));
                        PauseForScreenShot<AuditLogExtraInfoForm>(message, pageNum);
                        RunUI(extraInfoDialog.OkDialog);
                    }
                }
        }

        private void SetGridFormToFullWidth(DataboundGridForm form)
        {
            RunUI(() =>
            {
                int totalWidth = form.DataGridView.RowHeadersWidth + 10;
                foreach (DataGridViewColumn col in form.DataGridView.Columns)
                    totalWidth += col.Width;
                form.FloatingPane.Parent.Width = totalWidth;
            });

        }

        private void PanoramaSetup()
        {
            Uri serverUri = new Uri(SERVER_URL);

            IPanoramaClient panoramaClient = PanoramaUtil.CreatePanoramaClient(serverUri);

            var deleteResult = panoramaClient.DeleteFolder($@"{PANORAMA_FOLDER}/{testFolderName}", PANORAMA_USER_NAME,
                PANORAMA_PASSWORD);
            if(deleteResult != FolderOperationStatus.OK && deleteResult != FolderOperationStatus.notfound)
                Assert.Fail($@"Cannot delete existing test folder. Returns {deleteResult}");
            Assert.AreEqual(FolderOperationStatus.OK, panoramaClient.CreateFolder(PANORAMA_FOLDER, testFolderName,
                PANORAMA_USER_NAME, PANORAMA_PASSWORD), "Error when creating panorama test folder.");
        }
    }
}
