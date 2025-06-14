﻿/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.CommonMsData;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.Controls.Graphs;

namespace TestPerf // This would be in TestTutorials if it didn't involve a 2GB download
{
    [TestClass]
    public class HiResMetabolomicsTutorialTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestHiResMetabolomicsTutorial()
        {
            // Not yet translated
            if (IsTranslationRequired)
                return;

            // Set true to look at tutorial screenshots.
//            IsPauseForScreenShots = true;
//            IsCoverShotMode = true;
            CoverShotName = "HiResMetabolomics";

            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/HiResMetabolomics-23_1.pdf";
            ForceMzml = true; // Prefer mzML as being the more efficient download

            TestFilesPersistent = new[] { ExtWatersRaw };
            TestFilesZipPaths = new[]
            {
                (UseRawFiles
                   ? @"https://skyline.ms/tutorials/HiResMetabolomics2.zip"
                   : @"https://skyline.ms/tutorials/HiResMetabolomics2_mzML.zip"),
                @"TestPerf\HiResMetabolomicsViews.zip"
            };
            RunFunctionalTest();
        }

        private string GetDataFolder()
        {
            return UseRawFiles ? "HiResMetabolomics" : "HiResMetabolomics_mzML";
        }

        private string GetTestPath(string relativePath = null)
        {
            string folderSmallMolecule = GetDataFolder();
            string fullRelativePath = relativePath != null ? Path.Combine(folderSmallMolecule, relativePath) : folderSmallMolecule;
            return TestFilesDirs[0].GetTestPath(fullRelativePath);
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules));

            {
                var doc = SkylineWindow.Document;

                // Setting up the Transition Settings, p. 4
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() =>
                {
                    // Filter Settings
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Filter;
                    transitionSettingsUI.SelectedPeptidesSmallMolsSubTab = 1;
                    transitionSettingsUI.SmallMoleculePrecursorAdducts = Adduct.M_PLUS_H.AdductFormula;
                    transitionSettingsUI.SmallMoleculeFragmentAdducts = Adduct.M_PLUS.AdductFormula;
                    transitionSettingsUI.SmallMoleculeFragmentTypes =
                        TransitionFilter.PRECURSOR_ION_CHAR;
                    transitionSettingsUI.FragmentMassType = MassType.Monoisotopic;
                    transitionSettingsUI.SetAutoSelect = true;
                });
                PauseForScreenShot<TransitionSettingsUI.PredictionTab>("Transition Settings -Filter tab");
                RunUI(() =>
                {
                    // Full Scan Settings
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                    transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                    transitionSettingsUI.Peaks = 2;
                    transitionSettingsUI.PrecursorMassAnalyzer = FullScanMassAnalyzerType.orbitrap;
                    transitionSettingsUI.PrecursorRes = 70000;
                    transitionSettingsUI.PrecursorResMz = 200;
                    transitionSettingsUI.RetentionTimeFilterType = RetentionTimeFilterType.none;
                });
                PauseForScreenShot<TransitionSettingsUI.PredictionTab>("Transition Settings -Full Scan tab");

                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
                var docTargets = WaitForDocumentChange(doc);


                var importDialog = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
                RunUIForScreenShot(() => ResizeFormOnScreen(importDialog, 600, 300));
                PauseForScreenShot<InsertTransitionListDlg>("Insert Transition List ready to accept paste of transition list");

                var text = GetCsvFileText(GetTestPath("PUFA_TransitionList.csv"));
                var col4Dlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog.TransitionListText = text);

                RunUI(col4Dlg.radioMolecule.PerformClick);
                RunUIForScreenShot(() =>
                {
                    col4Dlg.SetColumnWidth(0, 120); // To show "Molecule List Name" fully
                    col4Dlg.SetColumnWidth(1, 125); // To show the precursor names fully
                    col4Dlg.SetColumnWidth(2, 120); // To show "Molecule Formula" fully
                    col4Dlg.SetColumnWidth(5, 135); // To reduce wrapping to just 2 rows
                });
                PauseForScreenShot<ImportTransitionListColumnSelectDlg>("Insert Transition List column picker");

                var errDlg = ShowDialog<ImportTransitionListErrorDlg>(col4Dlg.CheckForErrors);
                RunUI(() => errDlg.Size = new Size(680, 250));
                PauseForScreenShot<ImportTransitionListErrorDlg>("Check For Errors dialog showing charge problem");
                OkDialog(errDlg, errDlg.OkDialog);

                RunUI(() => col4Dlg.ComboBoxes[4].SelectedIndex = 0); // Set the Precursor charge column to "ignore"

                PauseForScreenShot<ImportTransitionListColumnSelectDlg>("Paste Dialog with validated contents");
                OkDialog(col4Dlg, col4Dlg.OkDialog);

                var autoSelectDlg = WaitForOpenForm<MultiButtonMsgDlg>();
                PauseForScreenShot<MultiButtonMsgDlg>("Auto-select query");
                OkDialog(autoSelectDlg, autoSelectDlg.OkDialog);

                docTargets = WaitForDocumentChange(docTargets);

                AssertEx.IsDocumentState(docTargets, null, 1, 4, 7, 14);
                Assert.IsFalse(docTargets.MoleculeTransitions.Any(t => !t.Transition.IsPrecursor()));

                const int SHORT_HEIGHT = 654;
                RunUI(() =>
                {
                    SkylineWindow.ChangeTextSize(TreeViewMS.DEFAULT_TEXT_FACTOR);
                    SkylineWindow.Size = new Size(957, SHORT_HEIGHT);
                    SkylineWindow.ExpandPrecursors();
                });
                RestoreViewOnScreen(5);

                PauseForScreenShot("Skyline with 14 transition");

                // Set the standard type of the surrogate standards to StandardType.SURROGATE_STANDARD
                SelectNode(SrmDocument.Level.Molecules, 3);

                if (IsPauseForScreenShots)
                {
                    RunUI(() => SkylineWindow.Height = 730);    // Taller for context menu

                    var sequenceTree = SkylineWindow.SequenceTree;
                    ToolStripDropDown menuStrip = null, subMenuStrip = null;

                    RunUI(() =>
                    {
                        var rectSelectedItem = sequenceTree.SelectedNode.Bounds;
                        SkylineWindow.ContextMenuTreeNode.Show(sequenceTree.PointToScreen(
                            new Point(rectSelectedItem.X + rectSelectedItem.Width / 2,
                                rectSelectedItem.Y + rectSelectedItem.Height / 2)));
                        var setStandardTypeMenu = SkylineWindow.ContextMenuTreeNode.Items.OfType<ToolStripMenuItem>()
                            .First(i => Equals(i.Name, @"setStandardTypeContextMenuItem"));
                        setStandardTypeMenu.ShowDropDown();
                        setStandardTypeMenu.DropDownItems.OfType<ToolStripMenuItem>()
                            .First(i => Equals(i.Name, @"surrogateStandardContextMenuItem")).Select();

                        menuStrip = SkylineWindow.ContextMenuTreeNode;
                        subMenuStrip = setStandardTypeMenu.DropDown;
                        menuStrip.Closing += DenyMenuClosing;
                        subMenuStrip.Closing += DenyMenuClosing;
                    });

                    // Should all land on the SkylineWindow, so just screenshot the whole window
                    PauseForScreenShot("Skyline with 4 molecules with menu and submenu showing for surrogate standard setting");

                    RunUI(() =>
                    {
                        menuStrip.Closing -= DenyMenuClosing;
                        subMenuStrip.Closing -= DenyMenuClosing;
                        menuStrip.Close();

                        SkylineWindow.Height = SHORT_HEIGHT;
                    });
                }

                RunUI(() => SkylineWindow.SetStandardType(StandardType.SURROGATE_STANDARD));
                RunUI(() => SkylineWindow.SaveDocument(GetTestPath("FattyAcids_demo.sky")));

                using (new WaitDocumentChange(1, true))
                {
                    var importResultsDlg1 = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
                    var openDataSourceDialog1 = ShowDialog<OpenDataSourceDialog>(() => importResultsDlg1.NamedPathSets =
                        importResultsDlg1.GetDataSourcePathsFile(null));
                    RunUI(() =>
                    {
                        openDataSourceDialog1.CurrentDirectory = new MsDataFilePath(Path.Combine(TestFilesDirs.First().PersistentFilesDir, GetDataFolder()));
                        openDataSourceDialog1.SelectAllFileType(ExtWatersRaw);
                    });
                    PauseForScreenShot<OpenDataSourceDialog>("Import Results Files form");
                    OkDialog(openDataSourceDialog1, openDataSourceDialog1.Open);
                    OkDialog(importResultsDlg1,importResultsDlg1.OkDialog);
                }

                SelectNode(SrmDocument.Level.Molecules, 0);
                SelectNode(SrmDocument.Level.MoleculeGroups, 0);
                RunUI(SkylineWindow.CollapsePrecursors);
                PauseForScreenShot("Skyline window multi-target graph");

                var docResults = SkylineWindow.Document;

                var expectedTransCount = new Dictionary<string, int[]>
                {
                    // peptides, transition groups, heavy transition groups, transitions, heavy transitions
                    {"default", new[] {4, 4, 3, 8, 6}}, // Most have these values
                    {"ID31609_01_E749_4745_091517", new[] {4, 4, 3, 7, 6}},

                };
                var msg = "";
                foreach (var chromatogramSet in docResults.Settings.MeasuredResults.Chromatograms)
                {
                    int[] transitions;
                    if (!expectedTransCount.TryGetValue(chromatogramSet.Name, out transitions))
                        transitions = expectedTransCount["default"];
                    try
                    {
                        AssertResult.IsDocumentResultsState(docResults, chromatogramSet.Name, transitions[0], transitions[1], transitions[2], transitions[3], transitions[4]);
                    }
                    catch(Exception x)
                    {
                        msg += TextUtil.LineSeparate(x.Message);
                    }
                }
                if (!string.IsNullOrEmpty(msg))
                    Assert.IsTrue(string.IsNullOrEmpty(msg), msg);
                RestoreViewOnScreen(9);
                var documentGrid = FindOpenForm<DocumentGridForm>();
                if (documentGrid == null)
                {
                    // When running offscreen, can't depend on RestoreViewOnScreen to open document grid
                    RunUI(() => SkylineWindow.ShowDocumentGrid(true));
                    documentGrid = FindOpenForm<DocumentGridForm>();
                }
                if (!IsCoverShotMode)
                    RunUI(() => documentGrid.ChooseView(Resources.PersistedViews_GetDefaults_Molecule_Quantification));
                else
                {
                    RunUI(() => documentGrid.DataboundGridControl.ChooseView(new ViewName(ViewGroup.BUILT_IN.Id,
                        Resources.SkylineViewContext_GetDocumentGridRowSources_Molecules)));
                }
                PauseForScreenShot("Skyline window multi-replicate layout");

                if (IsCoverShotMode)
                {
                    RunUI(() =>
                    {
                        Settings.Default.PeakAreaDotpDisplay = DotProductDisplayOption.label.ToString();
                        Settings.Default.ChromatogramFontSize = 14;
                        Settings.Default.AreaFontSize = 14;
                        SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                        SkylineWindow.AutoZoomBestPeak();
                        SkylineWindow.ShowPeakAreaLegend(false);
                        SkylineWindow.ShowRTLegend(false);
                    });

                    RestoreCoverViewOnScreen();

                    RunUI(SkylineWindow.FocusDocument);

                    ClickChromatogram("GW2_01", 1.148979, 209663764);

                    // TODO: This doesn't exactly reproduce the screen shot. The profile curve does not get adjusted.
                    RunUI(() => ZoomXAxis(SkylineWindow.GraphFullScan.ZedGraphControl, 332.25, 332.28));
                    RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.PrevNode);
                    WaitForGraphs();
                    RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.NextNode);
                    WaitForGraphs();
                    FocusDocument();

                    TakeCoverShot();
                    return;
                }

                using (new WaitDocumentChange(1, true))
                {
                    // Quant settings
                    var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);

                    RunUI(() =>
                    {
                        peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Quantification;
                        peptideSettingsUI.QuantRegressionFit = RegressionFit.LINEAR_THROUGH_ZERO;
                        peptideSettingsUI.QuantNormalizationMethod =
                            new NormalizationMethod.RatioToLabel(IsotopeLabelType.heavy);
                        peptideSettingsUI.QuantRegressionWeighting = RegressionWeighting.NONE;
                        peptideSettingsUI.QuantMsLevel = null; // All
                        peptideSettingsUI.QuantUnits = "uM";
                    });

                    PauseForScreenShot<PeptideSettingsUI.QuantificationTab>("Molecule Settings - Quantitation");
                    OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
                }

                var documentGrid2 = FindOpenForm<DocumentGridForm>();
                RunUI(() =>
                {
                    documentGrid2.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates);
                });
                WaitForConditionUI(() => (documentGrid2.RowCount == 16)); // Let it initialize

                RunUI(() =>
                {
                    var gridView = documentGrid2.DataGridView;
                    for (var index = 0; index < gridView.Rows.Count; index++)
                    {
                        var row = gridView.Rows[index];
                        if (row.Cells[0].Value.ToString().StartsWith("NIST"))
                        {
                            row.Cells[1].Value = SampleType.STANDARD;
                            row.Cells[2].Value = 1.0;
                        }
                        else if (row.Cells[0].Value.ToString().StartsWith("GW"))
                        {
                            row.Cells[1].Value = SampleType.QC;
                        }
                    }
                });
                // Make sure the edits have flowed to the document
                WaitForConditionUI(() => SkylineWindow.DocumentUI.Settings.MeasuredResults.Chromatograms.Where(c => c.Name.StartsWith("GW")).All(c => c.SampleType.Equals(SampleType.QC)));
                RestoreViewOnScreen(14);
                PauseForScreenShot<DocumentGridForm>("Document Grid - replicates");

                // Finish setting up quant
                var documentGrid3 = FindOpenForm<DocumentGridForm>();
                RunUI(() =>
                {
                    documentGrid3.ChooseView(Resources.PersistedViews_GetDefaults_Molecule_Quantification);
                });
                WaitForConditionUI(() => documentGrid3.IsComplete);

                RunUI(() =>
                {
                    var colNormal = documentGrid3.FindColumn(PropertyPath.Root.Property("NormalizationMethod"));
                    var colMultiplier = documentGrid3.FindColumn(PropertyPath.Root.Property("ConcentrationMultiplier"));
                    const int indexOfHeavyDha = 6;
                    var gridView = documentGrid3.DataGridView;
                    var methods = ((DataGridViewComboBoxCell) gridView.Rows[0].Cells[colNormal.Index]).Items;
                    var ratioToSurrogateHeavyDHA = ((Tuple<String, NormalizationMethod>)methods[indexOfHeavyDha]).Item2;
                    gridView.Rows[0].Cells[colMultiplier.Index].Value = 2838.0;
                    gridView.Rows[1].Cells[colMultiplier.Index].Value = 54.0;
                    gridView.Rows[1].Cells[colNormal.Index].Value = ratioToSurrogateHeavyDHA;
                    gridView.Rows[2].Cells[colMultiplier.Index].Value = 984.0;
                    gridView.Rows[3].Cells[colMultiplier.Index].Value = 118.0;
                });

                RestoreViewOnScreen(15);
                PauseForScreenShot<DocumentGridForm>("Document Grid - molecule quant again");

                RunUI(() => SkylineWindow.ShowCalibrationForm());
                SelectNode(SrmDocument.Level.Molecules, 0);
                WaitForGraphs();
                PauseForScreenShot<CalibrationForm>("Calibration curve");
            }
        }
    }
}
