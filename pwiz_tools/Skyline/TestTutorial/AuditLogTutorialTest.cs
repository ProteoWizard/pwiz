/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
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
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.PanoramaClient;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.AuditLog;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.AuditLog.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
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
    public class AuditLogTutorialTest : AbstractFunctionalTestEx
    {
        public const string SERVER_URL = "https://panoramaweb.org/";
        public const string PANORAMA_FOLDER = "SkylineTest";
        public const string PANORAMA_USER_NAME = "skyline_tester@proteinms.net";
        public const string PANORAMA_PASSWORD = "lclcmsms";

        public string testFolderName = "AuditLogUpload";

        [TestMethod]
        public void TestAuditLogTutorial()
        {
            // Set true to look at tutorial screenshots.
//            IsPauseForScreenShots = true;
//            PauseStartingPage = 16;
//            IsCoverShotMode = true;
            CoverShotName = "AuditLog";

            ForceMzml = true;   // Mzml is ~8x faster for this test.
                                                    
            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/AuditLog-20_1_1.pdf";

            TestFilesZipPaths = new[]
            {
                UseRawFiles
                    ? @"https://skyline.ms/tutorials/AuditLog.zip"
                    : @"https://skyline.ms/tutorials/AuditLogMzml.zip",
                @"TestTutorial\AuditLogViews.zip"
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
            RunUI(() =>
            {
                SkylineWindow.ResetDefaultSettings();
                SkylineWindow.NewDocument();
            });
            ShowAndPositionAuditLog(false);
            PauseForScreenShot<AuditLogForm>("Empty Audit Log form.", 2);

            // Configuring Settings for Inserting a New Peptide, p. 3
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

            PeptideSettingsUI peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications);

            var modHeavyK = new StaticMod("Label:13C(6)15N(2) (C-term K)", "K", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15,
                                          RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyK, peptideSettingsUi);
            RunUI(() => peptideSettingsUi.PickedHeavyMods = new[] { modHeavyK.Name });

            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);

            PauseForScreenShot<AuditLogForm>("Audit Log form with settings modifications.", 4);

            RunUI(() =>
            {
                SkylineWindow.AuditLogForm.Close();
                SkylineWindow.Width = 1010;
            });

            PauseForScreenShot("Undo list expanded. (manual)", 4);

            RunUI(SkylineWindow.Undo);

            PauseForScreenShot("Redo list expanded. (manual)", 5);

            RunUI(SkylineWindow.Redo);

            // Inserting a peptide sequence p. 5
            using (new CheckDocumentState(1, 1, 2, 10))
            {
                WaitForProteinMetadataBackgroundLoaderCompletedUI();

                var pasteDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg);
                RunUI(() => SetClipboardText("IEAIPQIDK\tGST-tag"));
                RunUI(pasteDlg.PastePeptides);
                RunUI(() =>
                {
                    pasteDlg.Size = new Size(700, 210);
                    pasteDlg.Top = SkylineWindow.Bottom + 20;
                });
                PauseForScreenShot<PasteDlg.PeptideListTab>("Insert Peptide List", 6);

                using (new WaitDocumentChange())
                {
                    OkDialog(pasteDlg, pasteDlg.OkDialog);
                }

                WaitForConditionUI(() => SkylineWindow.SequenceTree.Nodes.Count > 0);
            }

            RunUI( () =>
            {
                SkylineWindow.ExpandPrecursors();
                SkylineWindow.Height = 390;
            });

            PauseForScreenShot("Main window with Targets view", 6);

            ShowAndPositionAuditLog(true);
            PauseForScreenShot<AuditLogForm>("Audit Log form with inserted peptide.", 7);

            ShowLastExtraInfo("Extra info form with inserted peptide info.", 7);

            string documentPath = GetTestPath("AuditLogTutorial" + SrmDocument.EXT);
            RunUI(() => SkylineWindow.SaveDocument(documentPath));
            WaitForCondition(() => File.Exists(documentPath));

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

            WaitForCondition(10 * 60 * 1000,    // ten minutes
                () => SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);

            PauseForScreenShot<AuditLogForm>("Audit Log form with imported data files.", 9);

            ShowLastExtraInfo("Extra info form for the import.", 9);
            
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

            PauseForScreenShot<AuditLogForm>("Audit Log form with quantification settings.", 10);

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
                QuantificationStrings.SampleType_STANDARD_Standard + "\t" + f));
            ClipboardEx.SetText(pasteString);

            using (new WaitDocumentChange())
            {
                RunUI(() =>
                {
                    var colSampleType = documentGridForm.FindColumn(PropertyPath.Root.Property("SampleType"));
                    documentGridForm.DataGridView.CurrentCell = documentGridForm.DataGridView.Rows[1].Cells[colSampleType.Index];
                    documentGridForm.DataGridView.SendPaste();
                });
            }

            // ReSharper restore AccessToModifiedClosure
            WaitForConditionUI(() => documentGridForm.IsComplete);
            RunUI(() =>
            {
                var gridFloatingWindow = documentGridForm.Parent.Parent;
                gridFloatingWindow.Size = new Size(370, 315);
                gridFloatingWindow.Top = SkylineWindow.Bottom + 20;
            });
            PauseForScreenShot<DocumentGridForm>("Document grid with concentrations filled in", 11);
            RunUI(documentGridForm.Close);

            ShowAndPositionAuditLog(true);
            PauseForScreenShot<AuditLogForm>("Audit Log form with grid changes", 12);

            ShowLastExtraInfo("Extra Info for the analyte data import.", 12);
            RunUI(SkylineWindow.AuditLogForm.Close);

            const string unknownReplicate = "FOXN1-GST";
            RestoreViewOnScreen(13);
            ActivateReplicate(unknownReplicate);
            SelectNode(SrmDocument.Level.TransitionGroups, 1);
            RunUI(() => SkylineWindow.Size = new Size(936, 527));
            WaitForGraphs();

            PauseForScreenShot("Heavy precursor chromatogram", 13);

            RunUI(()=>
            {
                var pathHeavy = SkylineWindow.DocumentUI.GetPathTo((int)SrmDocument.Level.TransitionGroups, 1);

                var graphChrom = SkylineWindow.GetGraphChrom(unknownReplicate);
                Assert.IsNotNull(graphChrom);
                Assert.AreEqual(unknownReplicate, graphChrom.NameSet);

                var firstGroupInfo = graphChrom.ChromGroupInfos.FirstOrDefault();
                Assert.IsNotNull(firstGroupInfo, "Missing group info");
                var firstChromItem = graphChrom.GraphItems.FirstOrDefault(gci => gci.TransitionChromInfo != null);
                Assert.IsNotNull(firstChromItem, "Missing graph item");

                var listChanges = new List<ChangedPeakBoundsEventArgs>
                {
                    new ChangedPeakBoundsEventArgs(pathHeavy,
                        null,
                        graphChrom.NameSet,
                        firstGroupInfo.FilePath,
                        firstChromItem.GetValidPeakBoundaryTime(20.65),
                        firstChromItem.GetValidPeakBoundaryTime(21.15),
                        PeakIdentification.FALSE,
                        PeakBoundsChangeType.both)
                };
                graphChrom.SimulateChangedPeakBounds(listChanges);
            });

            ShowAndPositionAuditLog(true, 50, 200);
            WaitForConditionUI(500, () => SkylineWindow.AuditLogForm.DataGridView.Rows.Count > 0);

            PauseForScreenShot<AuditLogForm>("Audit Log form with changed integration boundary.", 14);
            int reasonIndex = 2;
            using (new WaitDocumentChange())
            {
                RunUI(() =>
                {
                    var pathReason = PropertyPath.Root.Property("Reason");
                    var colReason = SkylineWindow.AuditLogForm.FindColumn(pathReason);
                    reasonIndex = colReason.Index;
                    SetCellValue(SkylineWindow.AuditLogForm.DataGridView, 0, reasonIndex, 
                        "Changed peak integration as instructed by the tutorial");
                });
            }
            RunUI(() => SkylineWindow.AuditLogForm.DataGridView.AutoResizeColumn(reasonIndex));
            SetGridFormToFullWidth(SkylineWindow.AuditLogForm);
            PauseForScreenShot<AuditLogForm>("Audit Log form with updated reason.", 14);

            // View the calibration curve p. 15
            RunUI(()=>SkylineWindow.ShowCalibrationForm());
            var calibrationForm = FindOpenForm<CalibrationForm>();
            var priorZoomState = ZoomCalibrationCurve(calibrationForm, 0.52);

            RunUI(() =>
            {
                Assert.AreEqual(CalibrationCurveFitter.AppendUnits(QuantificationStrings.Analyte_Concentration, quantUnits), calibrationForm.ZedGraphControl.GraphPane.XAxis.Title.Text);
                Assert.AreEqual(string.Format(QuantificationStrings.CalibrationCurveFitter_PeakAreaRatioText__0___1__Peak_Area_Ratio, IsotopeLabelType.light.Title, IsotopeLabelType.heavy.Title),
                    calibrationForm.ZedGraphControl.GraphPane.YAxis.Title.Text);

                VerifyCalibrationCurve(calibrationForm, 5.4065E-1, -2.9539E-1, 0.999);
            });

            PauseForScreenShot<CalibrationForm>("Calibration curve zoomed", 15);
            RunUI(() =>
            {
                priorZoomState?.ApplyState(calibrationForm.ZedGraphControl.GraphPane);

                var chromatograms = SkylineWindow.DocumentUI.Settings.MeasuredResults.Chromatograms;
                new []{5, 6, 7, 8}.ForEach((index) =>
                {
                    int replicateIdx = chromatograms.IndexOf((ch) => ch.Name.Equals("Standard_" + index));
                    Assert.IsTrue(replicateIdx >= 0);
                    using (var excludeStandardMenu = calibrationForm.MakeExcludeStandardMenuItem(replicateIdx))
                    {
                        excludeStandardMenu?.PerformClick();
                    }
                });
            });
            WaitForGraphs();
            RunUI(() => VerifyCalibrationCurve(calibrationForm, 5.52E-1, -6.3678E-1, 1));
            OkDialog(calibrationForm, calibrationForm.Close);

            PauseForScreenShot<AuditLogForm>("Audit Log with excluded standard records", 16);

            PauseForScreenShot<AuditLogForm>("Audit Log Reports menu (manual)", 16);

            // TODO(nicksh): Audit log reason field does not currently support fill down
//            RunUI(() =>
//            {
//                SetCellValue(SkylineWindow.AuditLogForm.DataGridView, 0, reasonIndex, "Excluded standard below LOD");
//                for (int i = 0; i < 4; i++)
//                    SkylineWindow.AuditLogForm.DataGridView.Rows[i].Cells[reasonIndex].Selected = true;
//            });
//            RunUI(() => SkylineWindow.AuditLogForm.DataboundGridControl.FillDown());
            RunUI(() =>
            {
                for (int i = 0; i < 4; i++)
                    SetCellValue(SkylineWindow.AuditLogForm.DataGridView, i, reasonIndex, "Excluded standard below LOD");
            });
            RunUI(() =>
            {
                SkylineWindow.AuditLogForm.ChooseView(AuditLogStrings.AuditLogForm_MakeAuditLogForm_Undo_Redo);
            });
            SetGridFormToFullWidth(SkylineWindow.AuditLogForm);
            RunUI(() =>
            {
                var floatingWindow = SkylineWindow.AuditLogForm.Parent.Parent;
                floatingWindow.Height = 334;
                floatingWindow.Width -= 15;
            });
            PauseForScreenShot<AuditLogForm>("Audit Log with UndoRedo view.", 17);
            if (IsCoverShotMode)
            {
                RunUI(() =>
                {
                    SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                });

                RestoreCoverViewOnScreen();

                var calibrationCoverForm = WaitForOpenForm<CalibrationForm>();
                ZoomCalibrationCurve(calibrationCoverForm, 0.53);
                var floatingLogWindow = SkylineWindow.AuditLogForm.Parent.Parent;
                var floatingCalWindow = calibrationCoverForm.Parent.Parent;
                RunUI(() =>
                {
                    floatingLogWindow.Top = SkylineWindow.Bottom - floatingLogWindow.Height - 8;
                    floatingLogWindow.Left =
                        (SkylineWindow.Left + SkylineWindow.Right) / 2 - floatingLogWindow.Width / 2;
                    floatingCalWindow.Top = SkylineWindow.Top + 8;
                    floatingCalWindow.Left = SkylineWindow.Right - floatingCalWindow.Width - 8;
                    SkylineWindow.AuditLogForm.DataGridView.AutoResizeColumn(reasonIndex);
                    SkylineWindow.AuditLogForm.DataGridView.AutoResizeColumn(reasonIndex - 1);
                });
                TakeCoverShot();
            }

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

                customizeDialog.Height = 370;
            });
            PauseForScreenShot<ViewEditor.ChooseColumnsView>("Custom Columns report template", 17);
            OkDialog(customizeDialog, customizeDialog.OkDialog);
            SetGridFormToFullWidth(SkylineWindow.AuditLogForm);
            RunUI(() => SkylineWindow.AuditLogForm.Parent.Parent.Height += 10); // Extra for 2-line headers
            PauseForScreenShot<AuditLogForm>("Audit Log with custom view.", 18);

            var registrationDialog = ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.ShowPublishDlg(null));
            PauseForScreenShot<MultiButtonMsgDlg>("Upload confirmation dialog.", 19);

            var loginDialog = ShowDialog<EditServerDlg>(registrationDialog.ClickNo);
            PauseForScreenShot<EditServerDlg>("Login dialog.", 20);

            RunUI(() =>
            {
                loginDialog.URL = SERVER_URL;
                loginDialog.Username = PANORAMA_USER_NAME;
            });

            if (!IsPauseForScreenShots)
                OkDialog(loginDialog, loginDialog.CancelButton.PerformClick);
            else
            {
                PanoramaSetup();

                PauseForManualTutorialStep("MANUAL STEP (no screenshot). Enter password in the Edit Server dialog but DO NOT click OK. Close this window instead to proceed.");

                var publishDialog = ShowDialog<PublishDocumentDlg>(loginDialog.OkDialog);
                WaitForCondition(() => publishDialog.IsLoaded);
                RunUI(() =>
                {
                    publishDialog.SelectItem(testFolderName);
                });
                PauseForScreenShot<PublishDocumentDlg>("Folder selection dialog.", 21);
                var shareTypeDlg = ShowDialog<ShareTypeDlg>(publishDialog.OkDialog);
                var browserConfirmationDialog = ShowDialog<MultiButtonMsgDlg>(shareTypeDlg.OkDialog);
                OkDialog(browserConfirmationDialog, browserConfirmationDialog.ClickYes);

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

        private static ZoomState ZoomCalibrationCurve(CalibrationForm calibrationForm, double zoomFraction)
        {
            ZoomState priorZoomState = null;
            RunUI(() =>
            {
                PointF centerPoint =
                    calibrationForm.ZedGraphControl.GraphPane.GeneralTransform(new PointF(0, 0), CoordType.AxisXYScale);
                priorZoomState = new ZoomState(calibrationForm.ZedGraphControl.GraphPane, ZoomState.StateType.Zoom);
                calibrationForm.ZedGraphControl.ZoomPane(calibrationForm.ZedGraphControl.GraphPane, zoomFraction, centerPoint, true);
            });
            return priorZoomState;
        }

        private void VerifyCalibrationCurve(CalibrationForm calibrationForm, double slope, double intercept, double rSquared)
        {
            var labels = calibrationForm.ZedGraphControl.GraphPane.GraphObjList.FirstOrDefault(o => o is TextObj) as TextObj;
            Assert.IsNotNull(labels);
            var lines = labels.Text.Split(new[] {Environment.NewLine}, StringSplitOptions.None);
            Assert.IsTrue(lines.Length >= 2);
            Assert.AreEqual(CalibrationCurveMetrics.Format(slope, intercept), lines[0]);
            Assert.AreEqual(CalibrationCurveMetrics.RSquaredDisplayText(rSquared), lines[1]);
        }

        private void DelayedRunUI(Action act)
        {
            var delayThread = new Thread(() =>
            {
                Thread.Sleep(1000);
                SkylineWindow.Activate();
                act();
            });
            delayThread.Start();
        }

        private static void ShowAndPositionAuditLog(bool verticalScrollbar, int messageExtra = 0, int? height = null)
        {
            // ShowDialog causes problems debugging
            RunUI(SkylineWindow.ShowAuditLog);
            var auditLogForm = WaitForOpenForm<AuditLogForm>();
            WaitForCondition(() => auditLogForm.IsComplete);
            if (Program.SkylineOffscreen)
                return;

            const int spacing = 20;
            int formWidth = 772 + messageExtra;
            if (verticalScrollbar)
                formWidth += spacing;
            RunUI(() =>
            {
                var floatingWindow = auditLogForm.Parent.Parent;
                floatingWindow.Size = new Size(formWidth, height ?? 354);
                var screen = Screen.FromControl(SkylineWindow);
                if (screen.Bounds.Right > SkylineWindow.Right + spacing + floatingWindow.Width)
                {
                    floatingWindow.Top = SkylineWindow.Top;
                    floatingWindow.Left = SkylineWindow.Right + spacing;
                }
                else
                {
                    floatingWindow.Top = SkylineWindow.Bottom + spacing;
                    floatingWindow.Left = (screen.Bounds.Left + screen.Bounds.Right) / 2 - floatingWindow.Width / 2;
                }
                if (messageExtra > 0)
                {
                    var pathMessage = PropertyPath.Parse("Details!*.AllInfoMessage");
                    var colMessage = auditLogForm.FindColumn(pathMessage);
                    Assert.IsNotNull(colMessage);
                    colMessage.Width += messageExtra;
                }
            });
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
            WaitForConditionUI(() =>
            {
                var logRows = SkylineWindow.AuditLogForm.DataGridView.Rows;
                if (logRows.Count == 0 || logRows[0].Cells.Count < 2)
                    return false;
                var importCell = logRows[0].Cells[1] as TextImageCell;
                if (importCell == null)
                    return false;
                return importCell.Items.Length > 0;
            });

            var extraInfoDialog = ShowDialog<AuditLogExtraInfoForm>(() =>
                ((TextImageCell)SkylineWindow.AuditLogForm.DataGridView.Rows[0].Cells[1]).ClickImage(0));
            RunUI(() =>
            {
                var logFloatingWindow = SkylineWindow.AuditLogForm.Parent.Parent;
                extraInfoDialog.Left = logFloatingWindow.Left;
                extraInfoDialog.Top = logFloatingWindow.Bottom + 20;
            });
            PauseForScreenShot<AuditLogExtraInfoForm>(message, pageNum);
            OkDialog(extraInfoDialog, extraInfoDialog.OkDialog);
        }

        private void SetGridFormToFullWidth(DataboundGridForm form)
        {
            RunUI(() =>
            {
                int totalWidth = form.DataGridView.RowHeadersWidth + 35;    // Avoid horizontal scrollbar
                foreach (DataGridViewColumn col in form.DataGridView.Columns)
                    totalWidth += col.Width;
                form.FloatingPane.Parent.Width = totalWidth;
            });

        }

        private void PanoramaSetup()
        {
            // NOTE: This method is called when IsPauseForScreenShots is set to true. 
            // Before running the test change the permissions on the Panorama project folder at 
            // https://panoramaweb.org/SkylineTest/project-begin.view 
            // Make the test user (PANORAMA_USER_NAME) a folder administrator so that the
            // user is able to create and delete folders in the "SkylineTest" project.
            var panoramaClient =  new WebPanoramaClient(new Uri(SERVER_URL), PANORAMA_USER_NAME, PANORAMA_PASSWORD);

            try
            {
                panoramaClient.DeleteFolderIfExists($@"{PANORAMA_FOLDER}/{testFolderName}");
            }
            catch (Exception e)
            {
                AssertEx.Fail("Cannot delete existing Panorama test folder. {0}", e.Message);
            }
            
            try
            {
                panoramaClient.CreateTargetedMsFolder(PANORAMA_FOLDER, testFolderName);
            }
            catch (Exception e)
            {
                AssertEx.Fail("Error creating Panorama test folder. {0}", e.Message);
            }
        }
    }
}
