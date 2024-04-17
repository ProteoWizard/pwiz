﻿/*
 * Original author: Daniel Broudy <daniel.broudy .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
 * A
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License atex
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
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;


namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the tutorial for Targeted MS/MS
    /// </summary>
    [TestClass]
    public class TargetedMsmsTutorialTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestTargetedMSMSTutorial()
        {
            DoTestTargetedMSMSTutorial(RefinementSettings.ConvertToSmallMoleculesMode.none);
        }

        [TestMethod]
        // N.B. it's not clear to me that this test makes perfect sense right now, but implementing it
        // did uncover some issues with the new small molecule work so it is still worthwhile
        public void TestTargetedMSMSTutorialAsSmallMolecules()
        {
            DoTestTargetedMSMSTutorial(RefinementSettings.ConvertToSmallMoleculesMode.formulas);
        }

        [TestMethod]
        // N.B. it's not clear to me that this test makes perfect sense right now, but implementing it
        // did uncover some issues with the new small molecule work so it is still worthwhile
        public void TestTargetedMSMSTutorialAsSmallMoleculeMasses()
        {
            DoTestTargetedMSMSTutorial(RefinementSettings.ConvertToSmallMoleculesMode.masses_only);
        }

        private RefinementSettings.ConvertToSmallMoleculesMode AsSmallMoleculesTestMode { get; set; }

        public void DoTestTargetedMSMSTutorial(RefinementSettings.ConvertToSmallMoleculesMode smallMoleculesTestMode)
        {
            // Set true to look at tutorial screenshots.
            //            IsPauseForScreenShots = true;
            //            IsCoverShotMode = true;
            CoverShotName = "TargetedMSMS";

            if (smallMoleculesTestMode != RefinementSettings.ConvertToSmallMoleculesMode.none &&
                SkipSmallMoleculeTestVersions())
            {
                return;
            }

            ForceMzml = true;   // 2-3x faster than raw files for this test - and audit log expects it.

            AsSmallMoleculesTestMode = smallMoleculesTestMode;

            if (smallMoleculesTestMode !=  RefinementSettings.ConvertToSmallMoleculesMode.none)
                TestDirectoryName = "AsSmMol_" + smallMoleculesTestMode;

            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/PRM-22_2.pdf";

            TestFilesZipPaths = new[]
                {
                    UseRawFiles
                        ? @"http://skyline.ms/tutorials/TargetedMSMS_2.zip"
                        : @"http://skyline.ms/tutorials/TargetedMSMSMzml_2.zip",
                    @"TestTutorial\TargetedMSMSViews.zip"
                };
            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            string folderMsMs = UseRawFiles ? "TargetedMSMS" : "TargetedMSMSMzml";
            return TestFilesDirs[0].GetTestPath(Path.Combine(folderMsMs, relativePath));
        }

        protected override void DoTest()
        {
            LowResTest();
            if (!IsCoverShotMode)
                TofTest();
        }

        private void LowResTestPartOne(RefinementSettings.ConvertToSmallMoleculesMode asSmallMoleculesTestMode, string documentFile)
        {
            WaitForCondition(() => File.Exists(documentFile));
            RunUI(() => SkylineWindow.OpenFile(documentFile));
            bool AsSmallMolecules = asSmallMoleculesTestMode != RefinementSettings.ConvertToSmallMoleculesMode.none;

            const int expectedMoleculeCount = 9;
            const int expectedTransitionGroupCount = 10;
            const int expectedTransitionCount = 78;
            var document = WaitForDocumentLoaded();
            if (AsSmallMolecules)
            {
                RunUI(() => SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.mixed)); // Necessary for import wizard to import peptide stuff before we convert
                CheckConsistentLibraryInfo();
                ConvertDocumentToSmallMolecules(AsSmallMoleculesTestMode);
                document = SkylineWindow.Document;
                Assert.AreEqual(document.PeptideTransitionGroupCount, 0);
                Assert.AreEqual(document.MoleculeTransitionGroupCount, expectedTransitionGroupCount);
                Assert.AreEqual(document.PeptideTransitionCount, 0);
                Assert.AreEqual(document.MoleculeTransitionCount, expectedTransitionCount);
            }
            AssertEx.IsDocumentState(document, null, 3, expectedMoleculeCount, expectedTransitionGroupCount,
                expectedTransitionCount);
            CheckConsistentLibraryInfo();

            // p. 3 Select first peptide
            RunUI(() => SkylineWindow.SelectedPath = document.GetPathTo((int) SrmDocument.Level.Molecules, 0));
            RunUI(() => SkylineWindow.Size = new Size(820, 554));
            RestoreViewOnScreen(3);
            PauseForScreenShot("Main window", 3);

            // p. 4 Configure Document for Thermo raw files
            {
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() => transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan);
                PauseForScreenShot<TransitionSettingsUI.FullScanTab>("Peptide Settings - Full-Scan tab blank", 5);

                RunUI(() =>
                {

                    transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                    transitionSettingsUI.PrecursorMassAnalyzer = FullScanMassAnalyzerType.qit;
                    transitionSettingsUI.AcquisitionMethod = FullScanAcquisitionMethod.PRM;
                });
                PauseForScreenShot<TransitionSettingsUI.FullScanTab>("Peptide Settings - Full-Scan tab low res", 6);

                RunUI(() =>
                {
                    transitionSettingsUI.SetRetentionTimeFilter(RetentionTimeFilterType.ms2_ids, 2);

                    // p.6 - library ion match tolerance same as extraction window
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Library;
                    transitionSettingsUI.IonMatchTolerance = 0.7;
                });
                PauseForScreenShot<TransitionSettingsUI.LibraryTab>(
                    "Transition Settings - Library tab match tolerance same as MS/MS resolution", 8);

                RunUI(() =>
                {
                    // p.6
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Filter;
                    Assert.IsTrue(transitionSettingsUI.FragmentTypes.Contains("p")); // Should be added automatically
                    Assert.IsTrue(transitionSettingsUI.SmallMoleculeFragmentTypes.Contains("p")); // Should be added automatically
                });
                PauseForScreenShot<TransitionSettingsUI.FilterTab>("Transition Settings - Filter tab", 9);

                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);

                var tranSettingsFullScan = SkylineWindow.Document.Settings.TransitionSettings;
                Assert.AreEqual(FullScanPrecursorIsotopes.Count, tranSettingsFullScan.FullScan.PrecursorIsotopes);
                Assert.AreEqual(FullScanMassAnalyzerType.qit, tranSettingsFullScan.FullScan.PrecursorMassAnalyzer);
                Assert.AreEqual(FullScanAcquisitionMethod.PRM, tranSettingsFullScan.FullScan.AcquisitionMethod);
                Assert.IsTrue(ArrayUtil.ContainsAll(new[] {IonType.y, IonType.b, IonType.precursor},
                    tranSettingsFullScan.Filter.PeptideIonTypes));
                Assert.IsTrue(ArrayUtil.ContainsAll(new[] { IonType.custom, IonType.precursor },
                        tranSettingsFullScan.Filter.SmallMoleculeIonTypes));
            }

            RunUI(() => SkylineWindow.ExpandPrecursors());

            // Check all the precursors on picklists
            bool pausedForScreenShot = false;
            foreach (PeptideGroupTreeNode node in SkylineWindow.SequenceTree.GetSequenceNodes())
            {
                foreach (TreeNode child in node.Nodes)
                {
                    foreach (SrmTreeNodeParent grandChild in child.Nodes)
                    {
                        // Because of RunUI must copy to local variable first.
                        SrmTreeNodeParent child1 = grandChild;
                        RunUI(() => SkylineWindow.SequenceTree.SelectedNode = child1);
                        var picklist = ShowDialog<PopupPickList>(() => SkylineWindow.SequenceTree.ShowPickList(false));
                        RunUI(() =>
                        {
                            picklist.SetItemChecked(0, true);
                            Assert.IsTrue(picklist.GetItemLabel(0).Contains(IonType.precursor.GetLocalizedString()));
                            Assert.IsTrue(picklist.GetItemChecked(0));
                        });
                        if (!pausedForScreenShot)
                        {
                            PauseForScreenShot<PopupPickList>("Transitions popup pick-list", 10);
                            pausedForScreenShot = true;
                        }
                        OkDialog(picklist, picklist.OnOk);
                    }
                }
            }
            WaitForDocumentLoaded();
            // All transition groups should now have a precursor transition
            foreach (var nodeGroup in SkylineWindow.Document.MoleculeTransitionGroups)
            {
                Assert.AreEqual(IonType.precursor, nodeGroup.Transitions.First().Transition.IonType);
            }

            // p.8
            ExportMethodDlg exportMethodDlg =
                ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));
            RunUI(() =>
            {
                exportMethodDlg.SetInstrument("Thermo LTQ");
                Assert.AreEqual("Thermo LTQ", exportMethodDlg.InstrumentType);
                exportMethodDlg.SetMethodType(ExportMethodType.Standard);
                exportMethodDlg.SetTemplateFile(GetTestPath(@"Low Res\TargetedMSMS_template.meth"), true);
            });
            PauseForScreenShot<ExportMethodDlg.MethodView>("Export Method form", 11);

            // p. 10 Ok the error box.
            {
                var messageDlg = ShowDialog<MessageDlg>(
                    () => exportMethodDlg.OkDialog(GetTestPath(@"Low Res\TargetedMSMS_BSA_Protea.meth")));
                RunUI(() => messageDlg.Height = 210);
                PauseForScreenShot<MessageDlg>("Error message (expected)", 12);

                OkDialog(messageDlg, messageDlg.OkDialog);
            }

            // Making a report by hand p.11
            ExportLiveReportDlg exportReportDlg =
                ShowDialog<ExportLiveReportDlg>(() => SkylineWindow.ShowExportReportDialog());
            var editReportListDlg = ShowDialog<ManageViewsForm>(exportReportDlg.EditList);
            var viewEditor = ShowDialog<ViewEditor>(editReportListDlg.AddView);
            RunUI(() =>
            {
                Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(PropertyPath.Parse("Proteins!*.Name")));
                viewEditor.ChooseColumnsTab.AddSelectedColumn();
                Assert.AreEqual(1, viewEditor.ChooseColumnsTab.ColumnCount);
                var columnsToAdd = new[]
                {
                    PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.ModifiedSequence"),
                    PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Charge"),
                    PropertyPath.Parse("Proteins!*.Peptides!*.Precursors!*.Mz"),
                };
                foreach (var id in columnsToAdd)
                {
                    Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(id), "Unable to select {0}", id);
                    viewEditor.ChooseColumnsTab.AddSelectedColumn();
                }
                Assert.AreEqual(4, viewEditor.ChooseColumnsTab.ColumnCount);
            });
            PauseForScreenShot<ViewEditor>("Edit Report form", 13);

            {
                var previewReportDlg = ShowDialog<DocumentGridForm>(viewEditor.ShowPreview);
                var expectedRows = 10;
                WaitForConditionUI(() => previewReportDlg.IsComplete && previewReportDlg.RowCount == expectedRows);
                RunUI(() =>
                {
                    Assert.AreEqual(4, previewReportDlg.ColumnCount);
                    var precursors =
                        SkylineWindow.Document.MoleculeTransitionGroups.ToArray();
                    const int precursorIndex = 3;
                    for (int i = 0; i < expectedRows; i++)
                    {
                        Assert.AreEqual(precursors[i].PrecursorMz,
                            double.Parse(previewReportDlg.DataGridView.Rows[i].Cells[precursorIndex].Value.ToString()),
                            0.000001);
                    }
                    var precursorMzCol = previewReportDlg.DataGridView.Columns[precursorIndex];
                    Assert.IsNotNull(precursorMzCol);
                    previewReportDlg.DataGridView.Sort(precursorMzCol, ListSortDirection.Ascending);
                    previewReportDlg.Size = new Size(460, 330);
                    previewReportDlg.Left = SkylineWindow.Right + 20;
                });
                PauseForScreenShot<DocumentGridForm>("Preview New Report window", 14);

                OkDialog(previewReportDlg, previewReportDlg.Close);
            }

            // Press the Esc key until all forms have been dismissed.
            RunUI(() =>
            {
                viewEditor.Close();
                editReportListDlg.Close();
                exportReportDlg.CancelClick();
            });
            WaitForClosedForm(viewEditor);
            WaitForClosedForm(editReportListDlg);
            WaitForClosedForm(exportReportDlg);
        }

        private void LowResTest()
        {
            string documentFile = GetTestPath(@"Low Res\BSA_Protea_label_free_meth3.sky");

            bool AsSmallMolecules = AsSmallMoleculesTestMode != RefinementSettings.ConvertToSmallMoleculesMode.none;
            bool AsSmallMoleculeMasses = AsSmallMoleculesTestMode ==
                                         RefinementSettings.ConvertToSmallMoleculesMode.masses_only;
            LowResTestPartOne(AsSmallMoleculesTestMode, documentFile);
            if (AsSmallMolecules && !AsSmallMoleculeMasses)
            {
                // Reload the original peptide data for the purposes of library building
                // (workflow being demonstrated is peptide based) CONSIDER small mol workflow eventually
                LowResTestPartOne(RefinementSettings.ConvertToSmallMoleculesMode.none, documentFile);
            }
            if (AsSmallMolecules)
            {
                RunUI(() => SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.mixed)); // So peptide import wizard still works
            }

            Settings.Default.PeakAreaDotpDisplay = DotProductDisplayOption.label.ToString();

            //p. 15 Import Full-Scan Data
            // Launch import peptide search wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);
            RunUI(() => importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.prm);

            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Import Peptide Search Build Spectral Library blank page", 15);

            const int prefixLen = 35;
            const string lowResDir = "Low Res";
            const string searchDir = "search";
            const string lowRes20Base = "klc_20100329v_Protea_Peptide_Curve_20fmol_uL_tech1";
            string lowRes20File = GetTestPath(Path.Combine(lowResDir, lowRes20Base + ExtThermoRaw));
            string lowRes20FileRaw = Path.ChangeExtension(lowRes20File, ExtThermoRaw);
            string lowRes20Search = GetTestPath(Path.Combine(lowResDir, Path.Combine(searchDir, lowRes20Base + BiblioSpecLiteBuilder.EXT_PERCOLATOR)));
            string shortLowRes20FileName = (Path.GetFileNameWithoutExtension(lowRes20File) ?? "").Substring(prefixLen, 6);
            const string lowRes80Base = "klc_20100329v_Protea_Peptide_Curve_80fmol_uL_tech1";
            string lowRes80File = GetTestPath(Path.Combine(lowResDir, lowRes80Base + ExtThermoRaw));
            string lowRes80Search = GetTestPath(Path.Combine(lowResDir, Path.Combine(searchDir, lowRes80Base + BiblioSpecLiteBuilder.EXT_PERCOLATOR)));
            string shortLowRes80FileName = (Path.GetFileNameWithoutExtension(lowRes80File) ?? "").Substring(prefixLen, 6);

            string[] searchFiles = { lowRes20Search, lowRes80Search };
            var doc = SkylineWindow.Document;
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(searchFiles);
            });
            WaitForConditionUI(() => importPeptideSearchDlg.BuildPepSearchLibControl.Grid.ScoreTypesLoaded);
            RunUI(() =>
            {
                importPeptideSearchDlg.BuildPepSearchLibControl.Grid.SetScoreThreshold(0.01);
                importPeptideSearchDlg.BuildPepSearchLibControl.FilterForDocumentPeptides = true;
            });
            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Import Peptide Search Build Spectral Library with files page", 16);
            
            RunDlg<MessageDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck, dlg => dlg.OkDialog());
            doc = WaitForDocumentChangeLoaded(doc, WAIT_TIME*2);    // double wait time, becuase of intermittent failures on nightly runs
            if (AsSmallMolecules)
            {
                // Convert the document and just-created libraries to small molecules
                ConvertDocumentToSmallMolecules(AsSmallMoleculesTestMode);
                var originalDoc = doc;
                RunUI(() => importPeptideSearchDlg.SetDocument(SkylineWindow.Document, originalDoc));
                doc = SkylineWindow.Document;
                documentFile = SkylineWindow.DocumentFilePath;
            }
            // Verify document library was built
            string docLibPath = BiblioSpecLiteSpec.GetLibraryFileName(documentFile);
            string redundantDocLibPath = BiblioSpecLiteSpec.GetRedundantName(docLibPath);
            Assert.IsTrue(File.Exists(docLibPath) && File.Exists(redundantDocLibPath));
            var librarySettings = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
            Assert.IsTrue(librarySettings.HasDocumentLibrary);

            PauseForScreenShot<ImportPeptideSearchDlg.ChromatogramsPage>("Import Peptide Search Extract Chromatograms page", 17);

            // We're on the "Extract Chromatograms" page of the wizard.
            // All the test results files are in the same directory as the 
            // document file, so all the files should be found, and we should
            // just be able to move to the next page.
            TryWaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);
            var correctPage = false;
            ImportPeptideSearchDlg.Pages currentPage = 0;
            RunUI(() => correctPage = ImportPeptideSearchDlg.Pages.chromatograms_page == (currentPage = importPeptideSearchDlg.CurrentPage));
            if (!correctPage)
            {
                WaitForConditionUI(1, // Immediate timeout - just want the richer error message that WaitForConditionUI provides
                    () => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page,
                    () => string.Format("Expected to be on chromatograms_page, on {0} instead", currentPage));
            }
            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            RunUI(() =>
            {
                Assert.AreEqual("klc_20100329v_Protea_Peptide_Curve_", importResultsNameDlg.Prefix);
                Assert.AreEqual("_uL_tech1", importResultsNameDlg.Suffix);
            });
            PauseForScreenShot<ImportResultsNameDlg>("Import Results form removing prefix and suffix", 18);
            OkDialog(importResultsNameDlg, importResultsNameDlg.YesDialog);

            // Skip Match Modifications page.
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
            RunUI(() => importPeptideSearchDlg.ClickNextButton());

            // PauseForScreenShot<ImportPeptideSearchDlg.TransitionSettingsPage>("Import Peptide Search Transition Settings page", 19);
            TryWaitForOpenForm(typeof(ImportPeptideSearchDlg.TransitionSettingsPage));  // So SkylineTester - Forms page still stops

            // We're on the "Configure Transition Settings" page of the wizard.
            // We've already set up these settings, so just click next.
            // Min/max ion m/z have been set to new defaults, since we haven't changed them, so set them back.
            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.transition_settings_page);
                importPeptideSearchDlg.TransitionSettingsControl.MinIonMz = 50;
                importPeptideSearchDlg.TransitionSettingsControl.MaxIonMz = 1500;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // PauseForScreenShot<ImportPeptideSearchDlg.Ms2FullScanPage>("Import Peptide Search Full-Scan Settings page", 19);
            TryWaitForOpenForm(typeof(ImportPeptideSearchDlg.Ms2FullScanPage));  // So SkylineTester - Forms page still stops

            // We're on the "Configure Full-Scan Settings" page of the wizard.
            // We've already set up these settings, so just click next.
            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });
            doc = WaitForDocumentChange(doc);

            // Add FASTA also skipped because filter for document peptides was chosen.

            WaitForClosedForm(importPeptideSearchDlg);
            var allChromGraph = WaitForOpenForm<AllChromatogramsGraph>();
            RunUI(() => allChromGraph.Left = SkylineWindow.Right + 20);
            PauseForScreenShot<AllChromatogramsGraph>("Loading chromatograms window", 19);
            WaitForDocumentChangeLoaded(doc, 15 * 60 * 1000); // 15 minutes
            WaitForClosedAllChromatogramsGraph();

            const int expectedMoleculeCount = 9;
            const int expectedTransitionGroupCount = 10; // Expect this many with results
            var expected20TransitionCount = AsSmallMoleculeMasses  ? 87 : 88; // Expect this many with results (note no library match possible for "as masses" version)
            var expected80TransitionCount = AsSmallMoleculeMasses ? 88 : 87;
            AssertResult.IsDocumentResultsState(SkylineWindow.Document, shortLowRes20FileName, expectedMoleculeCount, expectedTransitionGroupCount, 0, expected20TransitionCount, 0);
            AssertResult.IsDocumentResultsState(SkylineWindow.Document, shortLowRes80FileName, expectedMoleculeCount, expectedTransitionGroupCount, 0, expected80TransitionCount, 0);

            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.IsGraphSpectrumVisible);
                SkylineWindow.ArrangeGraphsTiled();
                SkylineWindow.CollapsePrecursors();
                SkylineWindow.Width = 1070;
            });

            // Select the first precursor. 
            if (!AsSmallMoleculeMasses)
                FindNode(AsSmallMolecules ? "LVNELTEFAK" : "K.LVNELTEFAK.T [66, 75]");
            else
                FindNode(SkylineWindow.Document.MoleculeTransitionGroups.First().CustomMolecule.DisplayName);
            // Ensure Graphs look like p20. (checked)
            WaitForGraphs();
            RunUI(() => SkylineWindow.Width = 1050);
            RestoreViewOnScreen(20);
            PauseForScreenShot("Main window with data imported", 20);
            if (AsSmallMoleculesTestMode != RefinementSettings.ConvertToSmallMoleculesMode.masses_only)
            {
                TestRedundantComboBox();
                TestPropertySheet();
            }

            ValidatePeakRanks(1, 176, true);

            if(AsSmallMoleculesTestMode != RefinementSettings.ConvertToSmallMoleculesMode.masses_only)  
                TestLibraryMatchPropertySheet();

            if (!AsSmallMoleculeMasses)
            {
                double minDotp = AsSmallMolecules ? 0.6 : 0.9; // Without retention time prediction we don't do as well
                foreach (var nodeGroup in SkylineWindow.Document.MoleculeTransitionGroups)
                {
                    double dotp = nodeGroup.Results[0][0].LibraryDotProduct ?? 0;
                    Assert.IsTrue(Math.Round(dotp, 2) >= minDotp, string.Format("Library dot-product {0} found below {1}", dotp, minDotp));
                }
            }
            RunUI(() => SkylineWindow.AutoZoomBestPeak());
            // Ensure Graphs look like p21. (checked)
            WaitForGraphs();
            PauseForScreenShot("Chromatogram graphs clipped from main window with zoomed peaks", 21);

            RestoreViewOnScreen(21);
            RunUI(() => SkylineWindow.GraphSpectrum.SelectSpectrum(new SpectrumIdentifier(lowRes20FileRaw, 77.7722)));
            PauseForScreenShot<GraphSpectrum>("Library Match view clipped from main window with noisy spectrum", 22);

            RunUI(() =>
            {
                SkylineWindow.ShowGraphSpectrum(false);
                Assert.IsFalse(SkylineWindow.IsGraphSpectrumVisible);
                SkylineWindow.ShowPeptideIDTimes(false);
            });

            RunUI(() =>
            {
                var chromGraphs = SkylineWindow.GraphChromatograms.ToArray();
                Assert.AreEqual(2, chromGraphs.Length);
                Assert.AreEqual(46.8, chromGraphs[0].GraphItems.First().BestPeakTime, 0.05);
                Assert.AreEqual(46.8, chromGraphs[1].GraphItems.First().BestPeakTime, 0.05);

                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.ShowProductTransitions();
            });
            WaitForCondition(() => !SkylineWindow.GraphPeakArea.IsHidden);
            if (!AsSmallMoleculeMasses)  // Library was generated from pepXML
                WaitForDotProducts();
            RunUI(() =>
            {
                Assert.AreEqual(AsSmallMoleculeMasses ? 2 : 3,
                    SkylineWindow.GraphPeakArea.Categories.Count()); // Library and two replicates
                Assert.AreEqual(6, SkylineWindow.GraphPeakArea.CurveCount);
            });
            // Graph p.23
            PauseForScreenShot<GraphSummary.AreaGraphView>("Peak Areas Replicate Comparison graph metafile", 23);
            if (!AsSmallMoleculeMasses)
                VerifyDotProducts(0.99, 0.98);

            // Check graph p23. (checked)
            RunUI(() =>
                {
                    SkylineWindow.ShowAllTransitions();
                    if (!IsCoverShotMode)
                        SkylineWindow.ShowSplitChromatogramGraph(true);                    
                });

            // p. 24 screenshot of full 5-point dilution curve

            // Select precursor
            if (!AsSmallMoleculeMasses)
                FindNode(AsSmallMolecules ? "DRVYIHPF" : "R.DRVYIHPF.- [35, 42]");  // May be localized " (missed 1)"
            else
                FindNode(Resources.CustomMolecule_DisplayName_Molecule + " [1045");
            WaitForGraphs();
            RestoreViewOnScreen(24);
            if (IsPauseForScreenShots)
            {
                WaitForGraphs();
                // Select the precursor to cause graphs to re-layout
                RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.Nodes[0]);
                PauseForScreenShot<GraphSummary.AreaGraphView>("Peak Areas Replicate Comparison graph metafile with split graphs", 25);
            }

            if (IsCoverShotMode)
            {
                RunUI(() =>
                {
                    Settings.Default.ChromatogramFontSize = 14;
                    Settings.Default.AreaFontSize = 14;
                    SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                    SkylineWindow.ShowChromatogramLegends(false);
                });

                RestoreCoverViewOnScreen();
                TreeNode selectedNode = null;
                RunUI(() => selectedNode = SkylineWindow.SequenceTree.SelectedNode);
                RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.PrevNode);
                WaitForGraphs();
                RunUI(() => SkylineWindow.SequenceTree.SelectedNode = selectedNode);
                RunUI(() => selectedNode.Nodes[0].Expand());
                TakeCoverShot();
                return;
            }

            RunUI(() =>
            {
                SkylineWindow.Size = new Size(970, 740);
                Settings.Default.ChromatogramFontSize = 14;
                SkylineWindow.ShowGraphPeakArea(false);
            });
            PauseForScreenShot("Chromatogram graphs clipped from main window with split graphs", 26);

            // PeakAreaGraph Normalize to total p.27.
            RunUI(() =>
                {
                    SkylineWindow.ShowPeakAreaReplicateComparison();
                    SkylineWindow.ShowProductTransitions();
                    SkylineWindow.NormalizeAreaGraphTo(NormalizeOption.TOTAL);
                });

            // Ensure graph looks like p27.
            if (!AsSmallMoleculeMasses)
            {
                FindNode(AsSmallMolecules ? "KNLQS" : "R.IKNLQSLDPSH.- [81, 91]");
                FindNode(AsSmallMolecules ? "KNLQS" : "R.IKNLQSLDPSH.- [81, 91]"); // Find again - takes you to Phosphorylated version
            }
            else
                FindNode(Resources.CustomMolecule_DisplayName_Molecule + " [1330");
            WaitForGraphs();
            PauseForScreenShot<GraphSummary.AreaGraphView>("figure 1a - Area Replicate graph metafile for IKNLQSLDPSH", 27);
            RunUI(() =>
            {
                Assert.AreEqual(AsSmallMoleculeMasses ? 2 : 3, SkylineWindow.GraphPeakArea.Categories.Count());
                Assert.AreEqual(9, SkylineWindow.GraphPeakArea.CurveCount);
            });

            if (!AsSmallMoleculeMasses)
                FindNode(AsSmallMolecules ? "HLVDEPQNLIK" : "K.HLVDEPQNLIK.Q [402, 412]");
            else
                FindNode(Resources.CustomMolecule_DisplayName_Molecule + " [1304");
            WaitForGraphs();
            PauseForScreenShot("figure 1b - Area replicate graph metafile for HLVDEPQNLIK", 27);
            RunUI(() =>
            {
                Assert.AreEqual(AsSmallMoleculeMasses ? 2 : 3, SkylineWindow.GraphPeakArea.Categories.Count());
                Assert.AreEqual(7, SkylineWindow.GraphPeakArea.CurveCount);
            });

            RunUI(() => SkylineWindow.ShowGraphPeakArea(false));
            WaitForCondition(() => SkylineWindow.GraphPeakArea.IsHidden);
            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void TofTest()
        {
            // Working wih High-Resolution Mass Spectra.

            // Import a new Document. High-Resolution Mass Spectra, working with TOF p27.
            string newDocumentFile = GetTestPath(@"TOF\BSA_Agilent.sky");
            WaitForCondition(() => File.Exists(newDocumentFile));
            RunUI(() => SkylineWindow.OpenFile(newDocumentFile));

            bool asSmallMolecules = AsSmallMoleculesTestMode != RefinementSettings.ConvertToSmallMoleculesMode.none;
            if (asSmallMolecules)
            {
                ConvertDocumentToSmallMolecules(AsSmallMoleculesTestMode);
            }
            var docCalibrate1 = WaitForDocumentLoaded();
            const int pepCount1 = 5, preCount1 = 5, tranCount1 = 30;
            AssertEx.IsDocumentState(docCalibrate1, null, 1, pepCount1, preCount1, tranCount1);


            // Try to import a file to show it fails.
            ImportResultsDlg importResultsDlg3 = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunUI(() => importResultsDlg3.NamedPathSets = importResultsDlg3.GetDataSourcePathsFileReplicates(
                new[] { MsDataFileUri.Parse(GetTestPath(@"TOF\6-BSA-500fmol" + ExtAgilentRaw)) }));
            var importProgress = ShowDialog<AllChromatogramsGraph>(importResultsDlg3.OkDialog);
            var docFullScanError = WaitForDocumentChangeLoaded(docCalibrate1);
//            WaitForConditionUI(() => importProgress.Files.Any());
            WaitForConditionUI(() => importProgress.Finished);
            string expectedErrorFormat = Resources.NoFullScanFilteringException_NoFullScanFilteringException_The_file__0__does_not_contain_SRM_MRM_chromatograms__To_extract_chromatograms_from_its_spectra__go_to_Settings___Transition_Settings___Full_Scan_and_choose_options_appropriate_to_the_acquisition_method_used_;
            if (!TryWaitForConditionUI(2000, () => importProgress.Files.Any(f => !string.IsNullOrEmpty(f.Error))))
            {
                RunUI(() =>
                {
                    var messageDlg = FindOpenForm<MessageDlg>();
                    if (messageDlg != null)
                    {
                        Assert.Fail(TextUtil.LineSeparate("Unexpected MessageDlg: ",
                            messageDlg.DetailedMessage,
                            TextUtil.LineSeparate(docFullScanError.NonLoadedStateDescriptionsFull)));
                    }

                    var importProgress2 = FindOpenForm<AllChromatogramsGraph>();
                    if (importProgress2 != null && !ReferenceEquals(importProgress, importProgress2))
                    {
                        Assert.IsTrue(importProgress2.HasErrors);
                        AssertEx.AreComparableStrings(expectedErrorFormat, importProgress2.Error, 1);
                        Assert.Fail("Error message appeared in new instance of progress UI");
                    }
                    Assert.IsFalse(importProgress.IsDisposed, "Import progress destroyed");
                    Assert.IsTrue(importProgress.IsHandleCreated, "Import progress not created");
                    Assert.IsTrue(importProgress.Visible, "Import progress hidden");

                    string message = "Missing expected error text: " + expectedErrorFormat;
                    if (!importProgress.Files.Any())
                        message = "No files found";
                    else
                    {
                        foreach (var importProgressFile in importProgress.Files)
                            AssertEx.AreComparableStrings(expectedErrorFormat, importProgressFile.Error);

                        if (importProgress.SelectedControl == null)
                            message = string.Format("No selected control. Selected index = {0}", importProgress.Selected);
                        else if (!string.IsNullOrEmpty(importProgress.SelectedControl.Error))
                            message = "Selected control error: " + importProgress.SelectedControl.Error + " not in text control";
                    }

                    Assert.Fail(TextUtil.LineSeparate(message, "(" + importProgress.DetailedMessage + ")"));
                });
            }
            RunUI(() =>
            {
                foreach (var importProgressFile in importProgress.Files)
                    AssertEx.AreComparableStrings(expectedErrorFormat, importProgressFile.Error);
                AssertEx.AreComparableStrings(expectedErrorFormat, importProgress.Error, 1);
            });
            PauseForScreenShot("Import with error", 28);
            RunUI(() =>
            {
                importProgress.ClickClose();
                SkylineWindow.Undo();
            });

            var document = SkylineWindow.Document;

            // Fill out Transition Settings Menu
            int tranCount2 =  (AsSmallMoleculesTestMode != RefinementSettings.ConvertToSmallMoleculesMode.masses_only) ? (tranCount1 + preCount1*3) : (tranCount1 + preCount1); // No iostopes for mass-only document
            using (new CheckDocumentState(1, pepCount1, preCount1, tranCount2))
            {
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() =>
                {
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                    transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                    transitionSettingsUI.PrecursorMassAnalyzer = FullScanMassAnalyzerType.centroided;
                    transitionSettingsUI.PrecursorRes = 20;
                    transitionSettingsUI.Peaks = 3;
                    transitionSettingsUI.AcquisitionMethod = FullScanAcquisitionMethod.PRM;
                    transitionSettingsUI.ProductMassAnalyzer = FullScanMassAnalyzerType.centroided;
                    transitionSettingsUI.ProductRes = 20;
                });
                PauseForScreenShot<TransitionSettingsUI.FullScanTab>("Transition Settings - Full-Scan tab for TOF", 29);

                RunUI(() =>
                {
                    transitionSettingsUI.RetentionTimeFilterType = RetentionTimeFilterType.none;

                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Filter;
                    Assert.IsTrue(transitionSettingsUI.FragmentTypes.Contains("p")); // Should be added automatically
                    Assert.IsTrue(transitionSettingsUI.SmallMoleculeFragmentTypes.Contains("p")); // Should be added automatically
                });
                // No longer doing anything we need a screen-shot for
                // PauseForScreenShot<TransitionSettingsUI.FilterTab>("Transition Settings - Filter tab", 30);

                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            }

            var docHighRes = WaitForDocumentChange(document);
            var tranSettingsHighRes = docHighRes.Settings.TransitionSettings;
            Assert.AreEqual(FullScanPrecursorIsotopes.Count, tranSettingsHighRes.FullScan.PrecursorIsotopes);
            Assert.AreEqual(FullScanMassAnalyzerType.centroided, tranSettingsHighRes.FullScan.PrecursorMassAnalyzer);
            Assert.AreEqual(20, tranSettingsHighRes.FullScan.PrecursorRes);
            Assert.AreEqual(FullScanAcquisitionMethod.PRM, tranSettingsHighRes.FullScan.AcquisitionMethod);
            Assert.AreEqual(20, tranSettingsHighRes.FullScan.ProductRes);
            Assert.IsTrue(ArrayUtil.ContainsAll(new[] { IonType.y, IonType.b, IonType.precursor },
                                               tranSettingsHighRes.Filter.PeptideIonTypes));
            Assert.IsTrue(ArrayUtil.ContainsAll(new[] { IonType.custom, IonType.precursor },
                                               tranSettingsHighRes.Filter.SmallMoleculeIonTypes));
            RunUI(() => SkylineWindow.ExpandPrecursors());

            // Assert each peptide contains 3 precursors transitions (unless this is a masses-only small molecule doc).
            foreach (var nodeGroup in SkylineWindow.Document.MoleculeTransitionGroups)
            {
                for (int i = 0; i < nodeGroup.Children.Count; i++)
                {
                    var nodeTran = (TransitionDocNode)nodeGroup.Children[i];
                    if (i < ((AsSmallMoleculesTestMode == RefinementSettings.ConvertToSmallMoleculesMode.masses_only) ? 1 : 3))
                        Assert.AreEqual(IonType.precursor, nodeTran.Transition.IonType);
                    else
                        Assert.AreNotEqual(IonType.precursor, nodeTran.Transition.IonType);
                }
            }
            RestoreViewOnScreen(30);
            PauseForScreenShot("Targets View tree clipped from main window", 30);

            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg2 =>
            {
                string[] filePaths =
                {
                    GetTestPath(@"TOF\1-BSA-50amol" + ExtAgilentRaw),
                    GetTestPath(@"TOF\2-BSA-100amol" + ExtAgilentRaw),
                    GetTestPath(@"TOF\3-BSA-1fmol" + ExtAgilentRaw),
                    GetTestPath(@"TOF\4-BSA-10fmol" + ExtAgilentRaw),
                    GetTestPath(@"TOF\5-BSA-100fmol" + ExtAgilentRaw),
                    GetTestPath(@"TOF\6-BSA-500fmol" + ExtAgilentRaw)
                };
                importResultsDlg2.NamedPathSets = importResultsDlg2.GetDataSourcePathsFileReplicates(filePaths.Select(MsDataFileUri.Parse));
                importResultsDlg2.OkDialog();
            });
            WaitForDocumentChangeLoaded(docHighRes, 15 * 60 * 1000); // 15 minutes
            WaitForClosedAllChromatogramsGraph();
            if (AsSmallMoleculesTestMode != RefinementSettings.ConvertToSmallMoleculesMode.masses_only)
                FindNode(asSmallMolecules ? "LVNELTEFAK" : "K.LVNELTEFAK.T [66, 75]");
            else
                FindNode(document.MoleculeTransitionGroups.First().CustomMolecule.DisplayName);
            RunUI(() =>
            {
                SkylineWindow.CollapsePrecursors();
                SkylineWindow.AutoZoomNone();
                SkylineWindow.ShowGraphPeakArea(false);
                SkylineWindow.ShowGraphSpectrum(false);
                SkylineWindow.ShowProductTransitions();
                SkylineWindow.ArrangeGraphs(DisplayGraphsType.Tiled);
            });

            var windowSize = new Size(1364, 930);
            RunUI(() =>
            {
                // Resize and center window on screen
                SkylineWindow.Size = windowSize;
                var screenRect = Screen.FromControl(SkylineWindow).WorkingArea;
                SkylineWindow.Location = new Point(screenRect.X + screenRect.Width/2 - windowSize.Width/2,
                    screenRect.Y + screenRect.Height/2 - windowSize.Height/2);
            });
            RestoreViewOnScreen(31);
            PauseForScreenShot("Main window", 31);

            // if (AsSmallMoleculesTestMode != RefinementSettings.ConvertToSmallMoleculesMode.masses_only)  // No formula means no isotopes, so this check is not applicable
            //     ValidatePeakRanks(18, 270, false);

            WaitForGraphs();
            TryWaitForConditionUI(() => SkylineWindow.GraphChromatograms.Count(graphChrom => !graphChrom.IsHidden) == 6);
            RunUI(() =>
            {
                Assert.IsFalse(SkylineWindow.IsGraphSpectrumVisible);
                var listGraphs = SkylineWindow.GraphChromatograms.Where(graphChrom => !graphChrom.IsHidden).ToList();
                if (listGraphs.Count != 6)
                {
                    string hiddenGraphs = string.Empty;
                    var listHidden = SkylineWindow.GraphChromatograms.Where(graphChrom => graphChrom.IsHidden).ToList();
                    if (listHidden.Count != 0)
                        hiddenGraphs = TextUtil.LineSeparate(string.Empty, "Hidden:", TextUtil.LineSeparate(listHidden.Select(g => g.TabText)));
                    Assert.Fail(TextUtil.LineSeparate(string.Format("Expecting 6 visible graphs but found {0}", listGraphs.Count),
                        TextUtil.LineSeparate(listGraphs.Select(g => g.TabText)), hiddenGraphs));
                }
                var chromGraphs = SkylineWindow.GraphChromatograms.ToArray();
                Assert.AreEqual(6, chromGraphs.Length);
            });

            RunUI(() =>
            {
                var graphChrom6 = SkylineWindow.GetGraphChrom("6-BSA-500fmol");
                var graphChrom5 = SkylineWindow.GetGraphChrom("5-BSA-100fmol");
                var graphChrom4 = SkylineWindow.GetGraphChrom("4-BSA-10fmol");
                var graphChrom3 = SkylineWindow.GetGraphChrom("3-BSA-1fmol");
                var graphChrom2 = SkylineWindow.GetGraphChrom("2-BSA-100amol");
                var graphChrom1 = SkylineWindow.GetGraphChrom("1-BSA-50amol");
                Assert.AreEqual(13.3, graphChrom6.BestPeakTime ?? 0, 0.05);
                Assert.AreEqual(13.5, graphChrom5.BestPeakTime ?? 0, 0.05);
                Assert.AreEqual(13.6, graphChrom4.BestPeakTime ?? 0, 0.05);
                Assert.AreEqual(13.6, graphChrom3.BestPeakTime ?? 0, 0.05);
                Assert.AreEqual(13.6, graphChrom2.BestPeakTime ?? 0, 0.05);
                Assert.AreEqual(13.6, graphChrom1.BestPeakTime ?? 0, 0.05);
            });

            RunUI(SkylineWindow.AutoZoomBestPeak);
            WaitForGraphs();

            PauseForScreenShot("Chromatogram graphs clipped from main window zoomed", 33);

            RunUI(() =>
            {
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.ShowPeptideLogScale(true);
            });            

            // p. 34
            PauseForScreenShot<GraphSummary.AreaGraphView>("Peak Areas Replicate Comparison graph metafile", 34);
            WaitForDotProducts();
            RunUI(() =>
            {
                Assert.AreEqual((AsSmallMoleculesTestMode != RefinementSettings.ConvertToSmallMoleculesMode.masses_only) ? 7 : 6, SkylineWindow.GraphPeakArea.Categories.Count());
                Assert.AreEqual(6, SkylineWindow.GraphPeakArea.CurveCount);
            });
            VerifyDotProducts(0.87, 0.67, 0.91, 0.90, 0.90, 0.90);

            RunUI(() =>
            {
                SkylineWindow.ShowAllTransitions();
                SkylineWindow.ShowSplitChromatogramGraph(false);
                SkylineWindow.ArrangeGraphsTabbed();
                SkylineWindow.ActivateReplicate("6-BSA-500fmol");
                SkylineWindow.Size = new Size(855, 545);
                SkylineWindow.ShowGraphPeakArea(false);
            });
            PauseForScreenShot("Chromatogram graph metafile for 500 fmol", 34);

            RunUI(() =>
            {
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.ShowPrecursorTransitions();
                SkylineWindow.ArrangeGraphsTiled();
                SkylineWindow.ActivateReplicate("4-BSA-10fmol");
            });

            WaitForDotProducts();
            RunUI(() =>
            {
                Assert.AreEqual(7, SkylineWindow.GraphPeakArea.Categories.Count());
                Assert.AreEqual((AsSmallMoleculesTestMode != RefinementSettings.ConvertToSmallMoleculesMode.masses_only) ? 3 : 1, SkylineWindow.GraphPeakArea.CurveCount);
            });
            VerifyDotProducts(0.99, 0.52, 0.98, 1.00, 1.00, 1.00);
            RunUI(() =>
            {
                SkylineWindow.ActivateReplicate("6-BSA-500fmol");
                SkylineWindow.ShowGraphPeakArea(false);
                SkylineWindow.Size = windowSize;
            });
            PauseForScreenShot("Main window", 35);
            RunUI(SkylineWindow.ShowPeakAreaReplicateComparison);
            PauseForScreenShot<GraphSummary.AreaGraphView>("Peak Areas Replicate Comparison graph metafile", 36);

            RunUI(() => SkylineWindow.SaveDocument());
            WaitForConditionUI(() => !SkylineWindow.Dirty);
        }

        private void ValidatePeakRanks(int precursorCount, int expectedRows, bool addColumns)
        {
            const string TRANSITIONS_ROOT = "Proteins!*.Peptides!*.Precursors!*.Transitions!*.";
            const string FRAGMENT_ION_PATH = "FragmentIon";
            const string RESULTS_PART = "Results!*.Value.";
            const string PEAK_RANK_PATH = RESULTS_PART + "PeakRank";
            const string PEAK_RANK_BY_LEVEL_PATH = RESULTS_PART + "PeakRankByLevel";

            var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            EnableDocumentGridColumns(documentGrid, Resources.ReportSpecList_GetDefaults_Transition_Results, expectedRows,
                addColumns ? new[] { TRANSITIONS_ROOT + PEAK_RANK_BY_LEVEL_PATH } : null);
            WaitForConditionUI(() => documentGrid.IsComplete && documentGrid.FindColumn(PEAK_RANK_BY_LEVEL_PATH) != null);
            RunUI(() =>
            {
                var colIon = documentGrid.FindColumn(FRAGMENT_ION_PATH);
                Assert.IsNotNull(colIon);
                var colRank = documentGrid.FindColumn(PEAK_RANK_PATH);
                Assert.IsNotNull(colRank);
                var colRankByLevel = documentGrid.FindColumn(PEAK_RANK_BY_LEVEL_PATH);
                Assert.IsNotNull(colRankByLevel);
                string precursorText = IonType.precursor.ToString();    // Invariant name

                foreach (DataGridViewRow row in documentGrid.DataGridView.Rows)
                {
                    int? peakRank = GetCellIntValue(row, colRank.DisplayIndex);
                    int? peakRankByLevel = GetCellIntValue(row, colRankByLevel.DisplayIndex);
                    if (row.Cells[colIon.DisplayIndex].Value.ToString().StartsWith(precursorText))
                    {
                        // Some overlap in peak area intensity of the M+2 ion
                        if (peakRank < 3)
                            Assert.AreEqual(peakRank, peakRankByLevel);
                        else
                            Assert.IsTrue(peakRankByLevel <= peakRank);
                    }
                    else
                    {
                        // One case where first 2 fragments are more intense than M+2
                        if (!peakRankByLevel.HasValue)
                            Assert.IsNull(peakRank);
                        else if (peakRankByLevel > 2)
                            Assert.AreEqual(peakRank - precursorCount, peakRankByLevel);
                        else
                            Assert.IsTrue(peakRankByLevel >= peakRank - precursorCount,
                                string.Format("Expected {0} >= {1}", peakRankByLevel, peakRank - precursorCount));
                    }
                }
            });
            OkDialog(documentGrid, documentGrid.Close);
        }

        private int? GetCellIntValue(DataGridViewRow row, int index)
        {
            var value = row.Cells[index].Value;
            if (value == null)
                return null;
            return int.Parse(value.ToString());
        }

        private void WaitForDotProducts()
        {
            WaitForGraphs();
            if (AsSmallMoleculesTestMode != RefinementSettings.ConvertToSmallMoleculesMode.masses_only) // No libraries for small molecules without IDs
                WaitForConditionUI(() => AreaGraphDotProducts.Any(p => p != 0));
        }

        private void VerifyDotProducts(params double[] dotpExpects)
        {
            if (AsSmallMoleculesTestMode != RefinementSettings.ConvertToSmallMoleculesMode.masses_only) // No libraries for small molecules without IDs
            {
                RunUI(() =>
                {
                    var dotpActuals = AreaGraphDotProducts;
                    Assert.AreEqual(dotpExpects.Length, dotpActuals.Length);
                    for (int i = 0; i < dotpExpects.Length; i++)
                    {
                        Assert.AreEqual(dotpExpects[i], dotpActuals[i], 0.05);
                    }
                });
            }
        }

        private static double[] AreaGraphDotProducts
        {
            get
            {
                AreaReplicateGraphPane pane;
                Assert.IsTrue(SkylineWindow.GraphPeakArea.TryGetGraphPane(out pane));
                var dotpActuals = pane.DotProducts.ToArray();
                return dotpActuals;
            }
        }

        /// <summary>
        /// Tests the redundant spectra dropdown menu in the <see cref="ViewLibraryDlg"/> which allows the user to view redundant spectra
        /// </summary>
        private void TestRedundantComboBox()
        {
            var dlg = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewMenu.ViewSpectralLibraries);
            WaitForConditionUI(() => dlg.IsUpdateComplete);
            if (AsSmallMoleculesTestMode == RefinementSettings.ConvertToSmallMoleculesMode.none)
            {
                Assert.AreEqual(690, dlg.PeptidesCount);

                // The dropdown is only visible if the peptide has redundant spectra. Index 0 does.
                VerifyRedundant(dlg, 0, true, 144);
                // Check that the peaks count of the graphed item matches the peaks of the selected spectra
                VerifyRedundant(dlg, 1, false, 346);
                RunUI(() => dlg.FilterString = "ik");
                VerifyRedundant(dlg, 4, true, 514);
                RunUI(() => Assert.AreEqual(1, dlg.RedundantComboBox.Items.Count));
                // This simulates the user clicking on or showing the drop down for the combobox, which populates the combobox
                RunUI(dlg.UpdateRedundantComboItems);
                // Checks that for this peptide, there are 11 different spectra available in the dropdown
                WaitForConditionUI(() => dlg.IsComboBoxUpdated);
                RunUI(() => Assert.AreEqual(11, dlg.RedundantComboBox.Items.Count));
                RunUI(() => dlg.RedundantComboBox.SelectedIndex = 1);
                // Checks the peaks count changes upon changing the selected redundant spectra in the dropdown
                RunUI(() => Assert.AreEqual(551, dlg.GraphItem.PeaksCount));
                RunUI(() => dlg.RedundantComboBox.SelectedIndex = 2);
                RunUI(() => Assert.AreEqual(513, dlg.GraphItem.PeaksCount));
                var fileSet = new HashSet<string>();
                var RTSet = new HashSet<string>();
                RunUI(() =>
                {
                    // Different languages have different parenthesis characters - split on either
                    var splitterChars = new[] { '(', '（' };
                    foreach (ViewLibraryDlg.ComboOption redundantOption in dlg.RedundantComboBox.Items)
                    {
                        var splitName = redundantOption.OptionName.Split(splitterChars);
                        fileSet.Add(splitName[0]);
                        RTSet.Add(splitName[1]);
                    }
                });
                // Checks the naming conventions are accurate, two different file names and 11 different retention times
                Assert.AreEqual(2, fileSet.Count);
                Assert.AreEqual(11, RTSet.Count);
                VerifyRedundant(dlg, 1, false, 725);
            }
            else
            {
                Assert.AreEqual(10, dlg.PeptidesCount);

                // For small molecules, all have redundancies. Check to make sure the dropdown is visible for all of them
                var peakCounts = new[] { 382, 368, 385, 435, 514, 627, 534, 425, 458, 554 };
                for (var i = 0; i < 10; i++)
                    VerifyRedundant(dlg, i, true, peakCounts[i]);
            }
            OkDialog(dlg, () => dlg.Close());
        }

        private static bool IsRecordMode { get { return false; } }  // Set to true to get peak counts

        private static void VerifyRedundant(ViewLibraryDlg dlg, int i, bool visible, int peakCount)
        {
            RunUI(() => dlg.SelectedIndex = i);
            // The peptide at index one does not have redundant spectra
            int waitMs = IsRecordMode ? 1000 : 10 * 1000;
            if (!TryWaitForConditionUI(waitMs, () => IsViewLibraryDlgState(dlg, i, visible, peakCount)) && !IsRecordMode)
            {
                RunUI(() =>
                {
                    string redundantMessage = visible
                        ? string.Format("Redundant list hidden with {0} selected",
                            dlg.SelectedIndex)
                        : string.Format("Redundant list visible with {0} selected, and {1} entries",
                            dlg.SelectedIndex, dlg.RedundantComboBox.Items.Count);
                    string peaksMessage = string.Format("(peaks {0}, expected {1})", dlg.GraphItem.PeaksCount, peakCount);
                    string message = TextUtil.SpaceSeparate(redundantMessage, peaksMessage);
                    Assert.Fail(message);
                });
            }
            if (IsRecordMode)
                Console.Write(dlg.GraphItem.PeaksCount + @", ");
        }

        private static bool IsViewLibraryDlgState(ViewLibraryDlg dlg, int i, bool visible, int peakCount)
        {
            return dlg.SelectedIndex == i && dlg.IsVisibleRedundantSpectraBox == visible &&
                   peakCount == dlg.GraphItem.PeaksCount;
        }

        /// <summary>
        /// Tests the property sheet on the ViewLibraryDlg to confirm it shows up and displays accurate information
        /// </summary>
        private void TestPropertySheet()
        {
            var isSmallMolecules = AsSmallMoleculesTestMode != RefinementSettings.ConvertToSmallMoleculesMode.none;
            var expectedPropertiesDict = isSmallMolecules
                    ? new Dictionary<string, object>
                    {
                        { "FileName", "klc_20100329v_Protea_Peptide_Curve_80fmol_uL_tech1.ms2" },
                        { "LibraryName", "BSA_Protea_label_free_meth3.converted_to_small_molecules" },
                        { "PrecursorMz", 523.7745.ToString( CultureInfo.CurrentCulture) },
                        { "Charge", 2 },
                        { "Label", IsotopeLabelType.LIGHT_NAME },
                        { "RetentionTime", 44.29.ToString( Formats.RETENTION_TIME, CultureInfo.CurrentCulture) },
                        { "Score", 0.0.ToString( @"N1", CultureInfo.CurrentCulture) },
                        { "ScoreType", BiblioSpec.Properties.Resources.BiblioSpecScoreType_DisplayName_Percolator_q_value },
                        { "SpectrumCount", 24.ToString( CultureInfo.CurrentCulture) }
                    }
                    : new Dictionary<string, object>
                    {
                        { "IdFileName", "klc_20100329v_Protea_Peptide_Curve_20fmol_uL_tech1.perc.xml" },
                        { "FileName", "klc_20100329v_Protea_Peptide_Curve_20fmol_uL_tech1.ms2" },
                        { "LibraryName", "BSA_Protea_label_free_meth3" },
                        { "PrecursorMz", 417.7271.ToString( Formats.Mz, CultureInfo.CurrentCulture) },
                        { "Charge", "2" },
                        { "Label", IsotopeLabelType.LIGHT_NAME },
                        { "RetentionTime", 17.ToString( Formats.RETENTION_TIME, CultureInfo.CurrentCulture) },
                        { "SpecIdInFile", 488 },
                        { "Score", 0.0.ToString( @"N1", CultureInfo.CurrentCulture) },
                        { "ScoreType", BiblioSpec.Properties.Resources.BiblioSpecScoreType_DisplayName_Percolator_q_value },
                        { "SpectrumCount", 4 }
                    }
                ;

            var expectedProperties = new SpectrumProperties();
            expectedProperties.Deserialize(expectedPropertiesDict);

            var dlg = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            WaitForConditionUI(() => dlg.IsUpdateComplete);
            var graphExtension = dlg.GraphExtensionControl;
            RunUI(() => Assert.IsFalse(graphExtension.PropertiesVisible));
            ToolStripButton propertiesButton = null;
            // Check the pressing the properties button on the toolstrip displays the property sheet
            RunUI(() =>
            {
                var toolStrip = (ToolStrip)dlg.Controls.Find("toolStrip1", true)[0];
                propertiesButton = (ToolStripButton)toolStrip.Items.Find("propertiesButton", true)[0];
                Assert.IsNotNull(propertiesButton);
                Assert.IsFalse(propertiesButton.Checked);
                propertiesButton.PerformClick();
            });
            WaitForConditionUI(() => graphExtension.PropertiesVisible);
            RunUI(() => Assert.IsTrue(propertiesButton.Checked));

            var propertyGrid = graphExtension.PropertiesSheet;
            Assert.IsNotNull(propertyGrid);
            SpectrumProperties currentProperties = null;
            RunUI(() => currentProperties = propertyGrid.SelectedObject as SpectrumProperties);
            Assert.IsNotNull(currentProperties);

            // To write new json string for the expected property values into the output stream uncomment the next line
            //Trace.Write(currentProperties.Serialize());
            Assert.IsTrue(expectedProperties.IsSameAs(currentProperties));

            // Checks the number of properties displayed is the expected number
            // Checks that the property sheet updates upon switching peptides
            RunUI(() =>
            {
                dlg.FilterString = isSmallMolecules ? @"pep_HLVD" : @"HLVD";
                // If the ViewLibraryDlg property grid is updated with new properties, these values likely need to change
                ValidatePropertyCount(expectedProperties.GetProperties().Count, propertyGrid);
                ValidateSpectrumCount(401, propertyGrid);

                Assert.IsTrue(graphExtension.PropertiesVisible);
                Assert.IsTrue(propertiesButton.Checked);
            });
            // Checks that the property sheet displays nothing when no peptide is selected
            RunUI(() =>
            {
                dlg.FilterString = "b";
                Assert.AreEqual(0, GetProperties(propertyGrid).Count);
                dlg.FilterString = "";
            });
            if (!isSmallMolecules)
            {
                // Checks the property sheet updates when switching in the redundant combobox to viewing a redundant peptide
                RunUI(() =>
                {
                    ValidateSpecId("488", propertyGrid);
                    RunUI(dlg.UpdateRedundantComboItems);
                    WaitForConditionUI(() => dlg.IsComboBoxUpdated);
                    dlg.RedundantComboBox.SelectedIndex = 1;
                    ValidateSpecId("19208", propertyGrid);
                });
                RunUI(() =>
                {
                    // Tests that the score displayed on the graph matches the score displayed in the property sheet
                    dlg.SelectedIndex = 4;
                    ValidateScoresMatch(0.001, propertyGrid, dlg);
                    RunUI(dlg.UpdateRedundantComboItems);
                    WaitForConditionUI(() => dlg.IsComboBoxUpdated);
                    dlg.RedundantComboBox.SelectedIndex = 1;
                    ValidateScoresMatch(0.004, propertyGrid, dlg);
                });
            }
            RunUI(() => propertiesButton.PerformClick());
            RunUI(() =>
            {
                Assert.IsFalse(graphExtension.PropertiesVisible);
                Assert.IsFalse(propertiesButton.Checked);
            });
            OkDialog(dlg, () => dlg.Close());
        }

        private void TestLibraryMatchPropertySheet()
        {
            var isSmallMolecules = AsSmallMoleculesTestMode != RefinementSettings.ConvertToSmallMoleculesMode.none;

            var expectedPropertiesDict = new Dictionary<string, object>
            {
                { "FileName", "klc_20100329v_Protea_Peptide_Curve_20fmol_uL_tech1.ms2" },
                { "PrecursorMz", 582.319 },
                { "Charge", 2 },
                { "Label", IsotopeLabelType.LIGHT_NAME },
                { "RetentionTime", 46.81 },
                { "Score", 0.0 },
                { "ScoreType", BiblioSpec.Properties.Resources.BiblioSpecScoreType_DisplayName_Percolator_q_value },
                { "SpectrumCount", 118 }
            };
            if (isSmallMolecules)
                expectedPropertiesDict.Add("LibraryName", "BSA_Protea_label_free_meth3.converted_to_small_molecules");
            else
            {
                expectedPropertiesDict.Add("LibraryName", "BSA_Protea_label_free_meth3");
                expectedPropertiesDict.Add("IdFileName", "klc_20100329v_Protea_Peptide_Curve_20fmol_uL_tech1.perc.xml");
                expectedPropertiesDict.Add("SpecIdInFile", 7901);
            }

            var expectedProperties = new SpectrumProperties();
            expectedProperties.Deserialize(expectedPropertiesDict);
            var msGraph = SkylineWindow.GraphSpectrum.MsGraphExtension;

            Assert.IsTrue(SkylineWindow.GraphSpectrum != null && SkylineWindow.GraphSpectrum.Visible);
            var propertiesButton = SkylineWindow.GraphSpectrum.PropertyButton;
            Assert.IsFalse(propertiesButton.Checked);
            RunUI(() =>
            {
                propertiesButton.PerformClick();

            }); 
            WaitForConditionUI(() => msGraph.PropertiesVisible);
            WaitForGraphs();
            SpectrumProperties currentProperties = null;
            RunUI(() =>
            {
                currentProperties = msGraph.PropertiesSheet.SelectedObject as SpectrumProperties;
            });
                
            Assert.IsNotNull(currentProperties);
            // To write new json string for the expected property values into the output stream uncomment the next line
            //Trace.Write(currentProperties.Serialize());
            Assert.IsTrue(expectedProperties.IsSameAs(currentProperties));
            Assert.IsTrue(propertiesButton.Checked);
            // make sure properties are updated when spectrum combo selection changes
            RunUI(() =>
            {
                SkylineWindow.GraphSpectrum.SpectrumCombo.SelectedIndex = 1;
            });
            WaitForGraphs();
            RunUI(() =>
            {
                currentProperties = msGraph.PropertiesSheet.SelectedObject as SpectrumProperties;
                Assert.IsNotNull(currentProperties);
                Assert.AreEqual(isSmallMolecules ? 118 : 4000, currentProperties.SpectrumCount);
            });
            var lastNodeIndex = SkylineWindow.SequenceTree.Nodes.Count;
            IdentityPath oldPath = null;
            RunUI(() => { oldPath = SkylineWindow.SequenceTree.SelectedPath; });
            FindNode((547.3174).ToString(CultureInfo.CurrentCulture));
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectPath(new IdentityPath(new[] { SequenceTree.NODE_INSERT_ID }));
            });
            WaitForGraphs();
            RunUI(() =>
            {
                currentProperties = msGraph.PropertiesSheet.SelectedObject as SpectrumProperties;
                Assert.IsNull(currentProperties);
            });

            // test the close button
            RunUI(() =>
            {
                Assert.IsTrue(msGraph.CloseButton.Visible);
                msGraph.CloseButton.PerformClick();
            });
            WaitForConditionUI(() => !msGraph.PropertiesVisible);
            RunUI(() => { SkylineWindow.SequenceTree.SelectPath(oldPath); });
            WaitForGraphs();
        }
        private static PropertyDescriptorCollection GetProperties(PropertyGrid propertyGrid)
        {
            return ((ICustomTypeDescriptor)propertyGrid.SelectedObject).GetProperties();
        }

        private static void ValidatePropertyCount(int expectedPropCount, PropertyGrid pg)
        {
            Assert.AreEqual(expectedPropCount, 
                (pg.SelectedObject as GlobalizedObject)?.GetPropertiesForComparison()?.Count);
        }

        private static void ValidateSpectrumCount(int expectedSpectrumCount, PropertyGrid pg)
        {
            var spectrumCount = GetProperties(pg).Find("SpectrumCount", true).GetValue(pg.SelectedObject);
            Assert.AreEqual(expectedSpectrumCount, spectrumCount);
        }

        private static void ValidateSpecId(string expectedId, PropertyGrid pg)
        {
            var specId = GetProperties(pg).Find("SpecIdInFile", true).GetValue(pg.SelectedObject);
            Assert.AreEqual(expectedId, specId);
        }

        private static void ValidateScoresMatch(double expectedScore, PropertyGrid pg, ViewLibraryDlg dlg)
        {
            var score = GetProperties(pg).Find("Score", true).GetValue(pg.SelectedObject);
            Assert.AreEqual(expectedScore.ToString(CultureInfo.CurrentCulture), score?.ToString());
            Assert.AreEqual(expectedScore, dlg.GraphItem.SpectrumInfo.Score);
        }
    }
}
