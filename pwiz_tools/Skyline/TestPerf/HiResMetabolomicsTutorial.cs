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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace TestPerf // This would be in TestTutorials if it didn't involve a 2GB download
{
    [TestClass]
    public class HiResMetabolomicsTutorialTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestHiResMetabolomicsTutorial()
        {
            // Set true to look at tutorial screenshots.
           // IsPauseForScreenShots = true;

            LinkPdf = "https://skyline.gs.washington.edu/labkey/_webdav/home/software/Skyline/%40files/tutorials/HiResMetabolomics.pdf";
            ForceMzml = true; // Prefer mzML as being the more efficient download

            TestFilesPersistent = new[] { ExtWatersRaw };
            TestFilesZipPaths = new[]
            {
                (UseRawFiles
                   ? @"https://skyline.gs.washington.edu/tutorials/HiResMetabolomics.zip"
                   : @"https://skyline.gs.washington.edu/tutorials/HiResMetabolomics_mzML.zip"),
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
            // Inserting a Transition List, p. 2
            {
                var doc = SkylineWindow.Document;

                for (var retry = 0; retry < 2; retry++)
                {
                    var pasteDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteTransitionListDlg);
                    RunUI(() =>
                    {
                        pasteDlg.IsMolecule = false;  // Default peptide view
                        pasteDlg.Size = new Size(800, 275);
                    });
                    if (retry == 0)
                        PauseForScreenShot<PasteDlg>("Paste Dialog in peptide mode", 2);

                    RunUI(() =>
                    {
                        pasteDlg.IsMolecule = true;
                        pasteDlg.SetSmallMoleculeColumns(null);  // Default columns
                    });
                    if (retry == 0)
                        PauseForScreenShot<PasteDlg>("Paste Dialog in small molecule mode, default columns - show Columns checklist", 3);


                    var columnsOrdered = new[]
                    {
                        // Prepare transition list insert window to match tutorial
                        // Molecule List Name,Precursor Name,Precursor Formula,Precursor Adduct,Label Type,Precursor m/z,Precursor Charge,Explicit Retention Time
                        SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                        SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                        SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                        SmallMoleculeTransitionListColumnHeaders.adductPrecursor,
                        SmallMoleculeTransitionListColumnHeaders.labelType,
                        SmallMoleculeTransitionListColumnHeaders.mzPrecursor,
                        SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                        SmallMoleculeTransitionListColumnHeaders.rtPrecursor,
                    }.ToList();
                    RunUI(() => { pasteDlg.SetSmallMoleculeColumns(columnsOrdered); });
                    WaitForConditionUI(() => pasteDlg.GetUsableColumnCount() == columnsOrdered.Count);
                    if (retry == 0)
                        PauseForScreenShot<PasteDlg>("Paste Dialog with selected and ordered columns", 4);

                    var text = GetCsvFileText(GetTestPath("PUFA_TransitionList.csv"), true);
                    if (retry > 0)
                    {
                        // Fix bad charge declaration
                        var z = string.Format("{0}1{0}", TextUtil.CsvSeparator);
                        var zneg = string.Format("{0}-1{0}", TextUtil.CsvSeparator);
                        text = text.Replace(z, zneg);
                    }
                    SetClipboardText(text);
                    RunUI(pasteDlg.PasteTransitions);
                    RunUI(pasteDlg.ValidateCells);
                    if (retry == 0)
                    {
                        PauseForScreenShot<PasteDlg>("Paste Dialog with validated contents showing charge problem", 5);
                        OkDialog(pasteDlg, pasteDlg.CancelDialog);
                    }
                    else
                    {
                        PauseForScreenShot<PasteDlg>("Paste Dialog with validated contents", 5);
                        OkDialog(pasteDlg, pasteDlg.OkDialog);
                    }
                }
                var docTargets = WaitForDocumentChange(doc);

                AssertEx.IsDocumentState(docTargets, null, 1, 4, 7, 7);
                Assert.IsFalse(docTargets.MoleculeTransitions.Any(t => !t.Transition.IsPrecursor()));

                RunUI(() =>
                {
                    SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                    SkylineWindow.Size = new Size(957, 654);
                    SkylineWindow.ExpandPeptides();
                });
                RestoreViewOnScreen(5);
                PauseForScreenShot<SkylineWindow>("Skyline with small molecule targets - show the right-click menu for setting DHA to be a surrogate standard", 5);

                // Set the standard type of the surrogate standards to StandardType.SURROGATE_STANDARD
                RunUI(() =>
                {
                    List<IdentityPath> pathsToSelect = SkylineWindow.SequenceTree.Nodes.OfType<PeptideGroupTreeNode>()
                        .SelectMany(peptideGroup => peptideGroup.Nodes.OfType<PeptideTreeNode>())
                        .Where(peptideTreeNode => peptideTreeNode.DocNode.RawTextId.Contains("(DHA)"))
                        .Select(treeNode => treeNode.Path)
                        .ToList();
                    SkylineWindow.SequenceTree.SelectedPaths = pathsToSelect;
                    SkylineWindow.SetStandardType(StandardType.SURROGATE_STANDARD);
                });


                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);

                RunUI(() =>
                {
                    // Filter Settings
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Filter;
                    transitionSettingsUI.SelectedPeptidesSmallMolsSubTab = 1;
                    transitionSettingsUI.SmallMoleculePrecursorAdducts = Adduct.M_PLUS_H.AdductFormula;
                    transitionSettingsUI.SmallMoleculeFragmentAdducts = Adduct.M_PLUS.AdductFormula;
                    transitionSettingsUI.SmallMoleculeFragmentTypes =
                        TransitionFilter.SMALL_MOLECULE_FRAGMENT_CHAR + "," + TransitionFilter.PRECURSOR_ION_CHAR;
                    transitionSettingsUI.FragmentMassType = MassType.Monoisotopic;
                    transitionSettingsUI.SetAutoSelect = true;
                });
                PauseForScreenShot<TransitionSettingsUI.PredictionTab>("Transition Settings -Filter tab", 4);


                RunUI(() =>
                {
                    // Full Scan Settings
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                    transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                    transitionSettingsUI.PrecursorMassAnalyzer = FullScanMassAnalyzerType.orbitrap;
                    transitionSettingsUI.PrecursorRes = 70000;
                    transitionSettingsUI.PrecursorResMz = 200;
                    transitionSettingsUI.RetentionTimeFilterType = RetentionTimeFilterType.none;
                });
                PauseForScreenShot<TransitionSettingsUI.PredictionTab>("Transition Settings -Full Scan tab", 4);

                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
                WaitForDocumentChange(docTargets);

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
                    PauseForScreenShot<ImportResultsSamplesDlg>("Import Results Files form", 6);
                    OkDialog(openDataSourceDialog1, openDataSourceDialog1.Open);

                    OkDialog(importResultsDlg1,importResultsDlg1.OkDialog);
                }

                SelectNode(SrmDocument.Level.Molecules, 0);
                SelectNode(SrmDocument.Level.MoleculeGroups, 0);

                PauseForScreenShot<SkylineWindow>("Skyline window multi-target graph", 8);

                var docResults = SkylineWindow.Document;

                var expectedTransCount = new Dictionary<string, int[]>
                {
                    // transition groups, heavy transition groups, tranistions, heavy transitions
                    {"ID31609_01_E749_4745_091517", new[] {3, 3, 3, 10, 9}},
                    {"ID31627_01_E749_4745_091517", new[] {4, 4, 3, 12, 9}},
                    {"ID31624_01_E749_4745_091517", new[] {4, 4, 3, 12, 9}},
                    {"ID31653_01_E749_4745_091517", new[] {4, 4, 3, 12, 9}},

                };
                var msg = "";
                foreach (var chromatogramSet in docResults.Settings.MeasuredResults.Chromatograms)
                {
                    int[] transitions;
                    if (!expectedTransCount.TryGetValue(chromatogramSet.Name, out transitions))
                        transitions = new[] {  4, 4, 3, 11, 9 }; // Most have this value
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
                RunUI(() => documentGrid.ChooseView(Resources.Resources_ReportSpecList_GetDefaults_Peptide_Quantification));
                PauseForScreenShot<SkylineWindow>("Skyline window multi-replicate layout", 9);

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

                    PauseForScreenShot<PeptideSettingsUI.QuantificationTab>("Peptide Settings - Quantitation", 10);
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
                            row.Cells[2].Value = 2838.0;
                        }
                        else if (row.Cells[0].Value.ToString().StartsWith("GW"))
                        {
                            row.Cells[1].Value = SampleType.QC;
                        }
                    }
                });
                // Make sure the edits have flowed to the document
                WaitForConditionUI(() => SkylineWindow.DocumentUI.Settings.MeasuredResults.Chromatograms.Where(c => c.Name.StartsWith("GW")).All(c => c.SampleType.Equals(SampleType.QC)));
                PauseForScreenShot<DocumentGridForm>("Document Grid - replicates", 11);

                // Finish setting up quant
                var documentGrid3 = FindOpenForm<DocumentGridForm>();
                RunUI(() =>
                {
                    documentGrid3.ChooseView(Resources.Resources_ReportSpecList_GetDefaults_Peptide_Quantification);
                });
                WaitForConditionUI(() => (documentGrid3.RowCount > 0 &&
                                          documentGrid3.ColumnCount > 6)); // Let it initialize

                RunUI(() =>
                {
                    var gridView = documentGrid3.DataGridView;
                    var methods = ((DataGridViewComboBoxCell) gridView.Rows[0].Cells[6]).Items;
                    var ratioToHeavy = ((Tuple<String, NormalizationMethod>)methods[3]).Item2;
                    var ratioToSurrogateHeavyDHA = ((Tuple<String, NormalizationMethod>)methods[6]).Item2;
                    gridView.Rows[0].Cells[5].Value = 1.0;
                    gridView.Rows[0].Cells[6].Value = ratioToHeavy; 
                    gridView.Rows[1].Cells[5].Value = .0192;
                    gridView.Rows[1].Cells[6].Value = ratioToSurrogateHeavyDHA;
                    gridView.Rows[2].Cells[5].Value = .3467;
                    gridView.Rows[2].Cells[6].Value = ratioToHeavy;
                    gridView.Rows[3].Cells[5].Value = .0416;
                    gridView.Rows[3].Cells[6].Value = ratioToHeavy;
                });

                PauseForScreenShot<DocumentGridForm>("Document Grid - peptide quant again", 11);

                RunUI(() => SkylineWindow.ShowCalibrationForm());
                SelectNode(SrmDocument.Level.Molecules, 0);
                PauseForScreenShot<DocumentGridForm>("Calibration curve", 12);

            }

        }
    }
}
