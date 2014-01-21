/*
 * Original author: Daniel Broudy <daniel.broudy .at. gmail.com>,
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
using System.ComponentModel;
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
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;


namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the tutorial for Targeted MS/MS
    /// </summary>
    [TestClass]
    public class TargetedMsmsTutorialTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestTargetedMSMSTutorial()
        {
            // Set true to look at tutorial screenshots.
            //IsPauseForScreenShots = true;

            TestFilesZipPaths = new[]
                {
                    UseRawFiles
                        ? @"http://skyline.gs.washington.edu/tutorials/TargetedMSMS_2.zip"
                        : @"http://skyline.gs.washington.edu/tutorials/TargetedMSMSMzml_2.zip",
                    @"TestTutorial\TargetedMSMSViews.zip"
                };
            RunFunctionalTest();
        }

        private bool UseRawFiles
        {
            get
            {
                return ExtensionTestContext.CanImportThermoRaw && ExtensionTestContext.CanImportAgilentRaw;
            }
        }

        private string ExtThermoRaw
        {
            get { return UseRawFiles ? ExtensionTestContext.ExtThermoRaw : ExtensionTestContext.ExtMzml; }
        }

        private string ExtAgilentRaw
        {
            get { return UseRawFiles ? ExtensionTestContext.ExtAgilentRaw : ExtensionTestContext.ExtMzml; }
        }

        private string GetTestPath(string relativePath)
        {
            string folderMsMs = UseRawFiles ? "TargetedMSMS" : "TargetedMSMSMzml";
            return TestFilesDirs[0].GetTestPath(Path.Combine(folderMsMs, relativePath));
        }

        protected override void DoTest()
        {
            LowResTest();
            TofTest();
        }

        private void LowResTest()
        {
            string documentFile = GetTestPath(@"Low Res\BSA_Protea_label_free_meth3.sky");
            WaitForCondition(() => File.Exists(documentFile));
            RunUI(() => SkylineWindow.OpenFile(documentFile));

            var document = SkylineWindow.Document;
            AssertEx.IsDocumentState(document, null, 3, 9, 10, 78);

            // p. 3 Select first peptide
            RunUI(() => SkylineWindow.SelectedPath = document.GetPathTo((int) SrmDocument.Level.Peptides, 0));
            PauseForScreenShot("p. 3");

            // p. 4 Configure Document for Thermo raw files
            {
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() => transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan);
                PauseForScreenShot("p. 5 - Blank full-scan settings");

                RunUI(() =>
                {
                    
                    transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                    transitionSettingsUI.PrecursorMassAnalyzer = FullScanMassAnalyzerType.qit;
                    transitionSettingsUI.AcquisitionMethod = FullScanAcquisitionMethod.Targeted;
                });
                PauseForScreenShot("p. 6 - Low res full-scan settings");

                RunUI(() =>
                {
                    transitionSettingsUI.SetRetentionTimeFilter(RetentionTimeFilterType.ms2_ids, 2);

                    // p.6 - library ion match tolerance same as extraction window
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Library;
                    transitionSettingsUI.IonMatchTolerance = 0.7;
                });
                PauseForScreenShot("p. 7 - Library match tolerance same as MS/MS resolution");

                RunUI(() =>
                {
                    // p.6
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Filter;
                    transitionSettingsUI.FragmentTypes += ", p";
                });
                PauseForScreenShot("p. 9 - Filter for precursors");

                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);

                var docFullScan = WaitForDocumentChange(document);
                var tranSettingsFullScan = docFullScan.Settings.TransitionSettings;
                Assert.AreEqual(FullScanPrecursorIsotopes.Count, tranSettingsFullScan.FullScan.PrecursorIsotopes);
                Assert.AreEqual(FullScanMassAnalyzerType.qit, tranSettingsFullScan.FullScan.PrecursorMassAnalyzer);
                Assert.AreEqual(FullScanAcquisitionMethod.Targeted, tranSettingsFullScan.FullScan.AcquisitionMethod);
                Assert.IsTrue(ArrayUtil.EqualsDeep(new[] { IonType.y, IonType.b, IonType.precursor },
                                                   tranSettingsFullScan.Filter.IonTypes));
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
                            PauseForScreenShot("p. 10 - Manual precursor pick");
                            pausedForScreenShot = true;
                        }
                        OkDialog(picklist, picklist.OnOk);
                    }
                }
            }

            WaitForDocumentLoaded();
            foreach (var nodeGroup in SkylineWindow.Document.TransitionGroups)
            {
                Assert.IsFalse(nodeGroup.HasLibInfo && nodeGroup.Transitions.All(nodeTran => !nodeTran.HasLibInfo));
            }

            // All transition groups should now have a precursor transition
            foreach (var nodeGroup in SkylineWindow.Document.TransitionGroups)
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
                exportMethodDlg.SetTemplateFile(GetTestPath(@"Low Res\TargetedMSMS_template.meth"));
            });
            PauseForScreenShot("p. 11 - Export method");

            // p. 10 Ok the error box.
            {
                var messageDlg = ShowDialog<MessageDlg>(() => exportMethodDlg.OkDialog(GetTestPath(@"Low Res\TargetedMSMS_BSA_Protea.meth")));
                PauseForScreenShot("p. 12 - Error message");

                OkDialog(messageDlg, messageDlg.OkDialog);
            }

            if (IsEnableLiveReports)
            {
                // Making a report by hand p.11
                ExportLiveReportDlg exportReportDlg = ShowDialog<ExportLiveReportDlg>(() => SkylineWindow.ShowExportReportDialog());
                var editReportListDlg = ShowDialog<EditListDlg<SettingsListBase<ReportOrViewSpec>, ReportOrViewSpec>>(exportReportDlg.EditList);
                var viewEditor = ShowDialog<ViewEditor>(editReportListDlg.AddItem);
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
                PauseForScreenShot("p. 13 - Custom report template");

                {
                    var previewReportDlg = ShowDialog<DocumentGridForm>(viewEditor.ShowPreview);
                    RunUI(() =>
                    {
                        Assert.AreEqual(10, previewReportDlg.RowCount);
                        Assert.AreEqual(4, previewReportDlg.ColumnCount);
                        var precursors =
                            SkylineWindow.Document.TransitionGroups.ToArray();
                        const int precursorIndex = 3;
                        for (int i = 0; i < 10; i++)
                        {
                            Assert.AreEqual(precursors[i].PrecursorMz, double.Parse(previewReportDlg.DataGridView.Rows[i].Cells[precursorIndex].Value.ToString()), 0.000001);
                        }
                        var precursorMzCol = previewReportDlg.DataGridView.Columns[precursorIndex];
                        Assert.IsNotNull(precursorMzCol);
                        previewReportDlg.DataGridView.Sort(precursorMzCol, ListSortDirection.Ascending);
                    });
                    PauseForScreenShot("p. 14 - Report preview");

                    OkDialog(previewReportDlg, previewReportDlg.Close);
                }

                // Press the Esc key until all forms have been dismissed.
                RunUI(() =>
                {
                    viewEditor.Close();
                    editReportListDlg.CancelDialog();
                    exportReportDlg.CancelClick();
                });
                WaitForClosedForm(viewEditor);
                WaitForClosedForm(editReportListDlg);
                WaitForClosedForm(exportReportDlg);

            }
            else
            {
                // Making a report by hand p.11
                ExportReportDlg exportReportDlg = ShowDialog<ExportReportDlg>(() => SkylineWindow.ShowExportReportDialog());
                var editReportListDlg = ShowDialog<EditListDlg<SettingsListBase<ReportSpec>, ReportSpec>>(exportReportDlg.EditList);
                var pivotReportDlg = ShowDialog<PivotReportDlg>(editReportListDlg.AddItem);
                RunUI(() =>
                {
                    Assert.IsTrue(pivotReportDlg.TrySelect(new Identifier("ProteinName")));
                    pivotReportDlg.AddSelectedColumn();
                    Assert.AreEqual(1, pivotReportDlg.ColumnCount);
                    var columnsToAdd = new[]
                                {
                                    new Identifier("Peptides", "Precursors", "ModifiedSequence"),
                                    new Identifier("Peptides", "Precursors", "Charge"),
                                    new Identifier("Peptides", "Precursors", "Mz")
                                };
                    foreach (Identifier id in columnsToAdd)
                    {
                        Assert.IsTrue(pivotReportDlg.TrySelect(id));
                        pivotReportDlg.AddSelectedColumn();
                    }
                    Assert.AreEqual(4, pivotReportDlg.ColumnCount);
                });
                PauseForScreenShot("p. 13 - Custom report template");

                {
                    var previewReportDlg = ShowDialog<PreviewReportDlg>(pivotReportDlg.ShowPreview);
                    RunUI(() =>
                    {
                        Assert.AreEqual(10, previewReportDlg.RowCount);
                        Assert.AreEqual(4, previewReportDlg.ColumnCount);
                        var precursors =
                            SkylineWindow.Document.TransitionGroups.ToArray();
                        const int precursorIndex = 3;
                        for (int i = 0; i < 10; i++)
                        {
                            Assert.AreEqual(precursors[i].PrecursorMz, double.Parse(previewReportDlg.DataGridView.Rows[i].Cells[precursorIndex].Value.ToString()), 0.000001);
                        }
                        var precursorMzCol = previewReportDlg.DataGridView.Columns[precursorIndex];
                        Assert.IsNotNull(precursorMzCol);
                        previewReportDlg.DataGridView.Sort(precursorMzCol, ListSortDirection.Ascending);
                    });
                    PauseForScreenShot("p. 14 - Report preview");

                    OkDialog(previewReportDlg, previewReportDlg.OkDialog);
                }

                // Press the Esc key until all forms have been dismissed.
                RunUI(() =>
                {
                    pivotReportDlg.CancelDialog();
                    editReportListDlg.CancelDialog();
                    exportReportDlg.CancelClick();
                });
                WaitForClosedForm(pivotReportDlg);
                WaitForClosedForm(editReportListDlg);
                WaitForClosedForm(exportReportDlg);

            }

            //p. 12 Import Full-Scan Data
            // Launch import peptide search wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);

            PauseForScreenShot("p. 15 - Import peptide search start page");

            const int prefixLen = 35;
            const string lowResDir = "Low Res";
            const string searchDir = "search";
            const string lowRes20Base = "klc_20100329v_Protea_Peptide_Curve_20fmol_uL_tech1";
            string lowRes20File = GetTestPath(Path.Combine(lowResDir, lowRes20Base + ExtThermoRaw));
            string lowRes20FileRaw = Path.ChangeExtension(lowRes20File, ExtensionTestContext.ExtThermoRaw);
            string lowRes20Search = GetTestPath(Path.Combine(lowResDir, Path.Combine(searchDir, lowRes20Base + BiblioSpecLiteBuilder.EXT_PERCOLATOR)));
            string shortLowRes20FileName = (Path.GetFileNameWithoutExtension(lowRes20File) ?? "").Substring(prefixLen);
            const string lowRes80Base = "klc_20100329v_Protea_Peptide_Curve_80fmol_uL_tech1";
            string lowRes80File = GetTestPath(Path.Combine(lowResDir, lowRes80Base + ExtThermoRaw));
            string lowRes80Search = GetTestPath(Path.Combine(lowResDir, Path.Combine(searchDir, lowRes80Base + BiblioSpecLiteBuilder.EXT_PERCOLATOR)));
            string shortLowRes80FileName = (Path.GetFileNameWithoutExtension(lowRes80File) ?? "").Substring(prefixLen);

            string[] searchFiles = { lowRes20Search, lowRes80Search };
            var doc = SkylineWindow.Document;
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage ==
                            ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(searchFiles);
                importPeptideSearchDlg.BuildPepSearchLibControl.CutOffScore = 0.99;
                importPeptideSearchDlg.BuildPepSearchLibControl.FilterForDocumentPeptides = true;
            });
            PauseForScreenShot("p. 16 - Build document library");
            
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
            doc = WaitForDocumentChange(doc);

            // Verify document library was built
            string docLibPath = BiblioSpecLiteSpec.GetLibraryFileName(documentFile);
            string redundantDocLibPath = BiblioSpecLiteSpec.GetRedundantName(docLibPath);
            Assert.IsTrue(File.Exists(docLibPath) && File.Exists(redundantDocLibPath));
            var librarySettings = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
            Assert.IsTrue(librarySettings.HasDocumentLibrary);

            PauseForScreenShot("p. 17 - Add results files");

            // We're on the "Extract Chromatograms" page of the wizard.
            // All the test results files are in the same directory as the 
            // document file, so all the files should be found, and we should
            // just be able to move to the next page.
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page));
            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());

            RunUI(importResultsNameDlg.YesDialog);

            // Modificatios and full-scan settings are already set up, so those
            // pages should get skipped.

            // Add FASTA also skipped because filter for document peptides was chosen.

            WaitForClosedForm(importPeptideSearchDlg);
            WaitForDocumentChangeLoaded(doc, 15 * 60 * 1000); // 15 minutes
            
            AssertResult.IsDocumentResultsState(SkylineWindow.Document, shortLowRes20FileName, 9, 10, 0, 87, 0);
            AssertResult.IsDocumentResultsState(SkylineWindow.Document, shortLowRes80FileName, 9, 10, 0, 87, 0);

            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.IsGraphSpectrumVisible);
                SkylineWindow.ArrangeGraphsTiled();
                SkylineWindow.CollapsePrecursors();
            });

            // Select the first precursor. 
            FindNode("K.LVNELTEFAK.T [65, 74]");
            // Ensure Graphs look like p17. (checked)
            WaitForGraphs();
            RestoreViewOnScreen(TestFilesDirs[1].GetTestPath(@"p18.view"));
            PauseForScreenShot("p. 19 - Imported data");

            const double minDotp = 0.9;
            foreach (var nodeGroup in SkylineWindow.Document.TransitionGroups)
            {
                double dotp = nodeGroup.Results[0][0].LibraryDotProduct ?? 0;
                Assert.IsTrue(Math.Round(dotp, 2) >= minDotp, string.Format("Library dot-product {0} found below {1}", dotp, minDotp));
            }

            RunUI(() => SkylineWindow.AutoZoomBestPeak());
            // Ensure Graphs look like p18. (checked)
            WaitForGraphs();
            PauseForScreenShot("p. 20 - Zoomed peaks");

            RunUI(() => SkylineWindow.GraphSpectrum.SelectSpectrum(new SpectrumIdentifier(lowRes20FileRaw, 77.7722)));
            PauseForScreenShot("p. 21 - Noisy spectrum");

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
            WaitForDotProducts();
            RunUI(() =>
                      {
                          // Graph p.15
                          Assert.AreEqual(3, SkylineWindow.GraphPeakArea.Categories.Count());
                          Assert.AreEqual(6, SkylineWindow.GraphPeakArea.CurveCount);
                      });
            PauseForScreenShot("p. 22 - Area replicate comparison");
            VerifyDotProducts(0.99, 0.98);

            // Check graph p15. (checked)
            RunUI(() =>
                {
                    SkylineWindow.ShowAllTransitions();
                    SkylineWindow.ShowSplitChromatogramGraph(true);                    
                });

            // p. 16 screenshot of full 5-point dilution curve

            // Select precursor
            FindNode("R.DRVYIHPF.- [34, 41]");  // May be localized " (missed 1)"
            WaitForGraphs();
            PauseForScreenShot("p. 24 - split area graphs, 25 - split chromatogram graphs");

            // PeakAreaGraph Normalize to total p.20.
            RunUI(() =>
                {
                    SkylineWindow.ShowProductTransitions();
                    SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.area_percent_view);
                });

            // Ensure graph looks like p20.
            FindNode("R.IKNLQSLDPSH.- [80, 90]");
            FindNode("R.IKNLQSLDPSH.- [80, 90]");   // Phosphorylated
            WaitForGraphs();
            PauseForScreenShot("p. 26, figure 1a - Area replicate graph");
            RunUI(() =>
            {
                Assert.AreEqual(3, SkylineWindow.GraphPeakArea.Categories.Count());
                Assert.AreEqual(9, SkylineWindow.GraphPeakArea.CurveCount);
            });

            FindNode("K.HLVDEPQNLIK.Q [401, 411]");
            WaitForGraphs();
            PauseForScreenShot("p. 26, figure 1b - Area replicate graph");
            RunUI(() =>
            {
                Assert.AreEqual(3, SkylineWindow.GraphPeakArea.Categories.Count());
                Assert.AreEqual(7, SkylineWindow.GraphPeakArea.CurveCount);
            });

            RunUI(() => SkylineWindow.ShowGraphPeakArea(false));
            WaitForCondition(() => SkylineWindow.GraphPeakArea.IsHidden);
            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void TofTest()
        {
            // Working wih High-Resolution Mass Spectra.

            // Import a new Document. High-Resolution Mass Spectra, working with TOF p22.
            string newDocumentFile = GetTestPath(@"TOF\BSA_Agilent.sky");
            WaitForCondition(() => File.Exists(newDocumentFile));
            RunUI(() => SkylineWindow.OpenFile(newDocumentFile));
            var docCalibrate1 = SkylineWindow.Document;
            const int pepCount1 = 5, preCount1 = 5, tranCount1 = 30;
            AssertEx.IsDocumentState(docCalibrate1, null, 1, pepCount1, preCount1, tranCount1);


            // Try to import a file to show it fails.
            ImportResultsDlg importResultsDlg3 = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunUI(() => importResultsDlg3.NamedPathSets = importResultsDlg3.GetDataSourcePathsFileReplicates(
                new[] { GetTestPath(@"TOF\6-BSA-500fmol" + ExtAgilentRaw) }));
            var messageDlg = ShowDialog<MessageDlg>(importResultsDlg3.OkDialog);
            RunUI(() => AssertEx.AreComparableStrings(Resources.NoFullScanFilteringException_NoFullScanFilteringException_To_extract_chromatograms_from__0__full_scan_settings_must_be_enabled_,
                                                      messageDlg.Message, 1));
            PauseForScreenShot("p. 27 - Error message");
            
            OkDialog(messageDlg, messageDlg.OkDialog);

            RunUI(() =>
            {
                SkylineWindow.Undo();
                SkylineWindow.Undo();
            });

            var document = SkylineWindow.Document;

            // Fill out Transition Settings Menu
            const int tranCount2 = tranCount1 + preCount1*3;
            using (new CheckDocumentState(1, pepCount1, preCount1, tranCount2))
            {
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() =>
                {
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                    transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                    transitionSettingsUI.PrecursorMassAnalyzer = FullScanMassAnalyzerType.tof;
                    transitionSettingsUI.Peaks = 3;
                    transitionSettingsUI.AcquisitionMethod = FullScanAcquisitionMethod.Targeted;
                    transitionSettingsUI.ProductMassAnalyzer = FullScanMassAnalyzerType.tof;
                });
                PauseForScreenShot("p. 28 - Full-scan settings");

                RunUI(() =>
                {
                    transitionSettingsUI.RetentionTimeFilterType = RetentionTimeFilterType.none;

                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Filter;
                    transitionSettingsUI.FragmentTypes += ", p";
                });
                PauseForScreenShot("p. 29 - Transition filter settings");

                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            }

            var docHighRes = WaitForDocumentChange(document);
            var tranSettingsHighRes = docHighRes.Settings.TransitionSettings;
            Assert.AreEqual(FullScanPrecursorIsotopes.Count, tranSettingsHighRes.FullScan.PrecursorIsotopes);
            Assert.AreEqual(FullScanMassAnalyzerType.tof, tranSettingsHighRes.FullScan.PrecursorMassAnalyzer);
            Assert.AreEqual(FullScanAcquisitionMethod.Targeted, tranSettingsHighRes.FullScan.AcquisitionMethod);
            Assert.IsTrue(ArrayUtil.EqualsDeep(new[] { IonType.y, IonType.b, IonType.precursor },
                                               tranSettingsHighRes.Filter.IonTypes));
            RunUI(() => SkylineWindow.ExpandPrecursors());

            // Assert each peptide contains 3 precursors transitions.
            foreach (var nodeGroup in SkylineWindow.Document.TransitionGroups)
            {
                for (int i = 0; i < nodeGroup.Children.Count; i++)
                {
                    var nodeTran = (TransitionDocNode)nodeGroup.Children[i];
                    if (i < 3)
                        Assert.AreEqual(IonType.precursor, nodeTran.Transition.IonType);
                    else
                        Assert.AreNotEqual(IonType.precursor, nodeTran.Transition.IonType);
                }
            }
            PauseForScreenShot("p. 30 - First peptide transition nodes");

            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg2 =>
            {
                string[] filePaths =
                    {
                        GetTestPath(@"TOF\6-BSA-500fmol" + ExtAgilentRaw)
                    };
                importResultsDlg2.NamedPathSets = importResultsDlg2.GetDataSourcePathsFileReplicates(filePaths);
                importResultsDlg2.OkDialog();
            });

            FindNode("K.LVNELTEFAK.T [65, 74]");
            RunUI(() =>
            {
                SkylineWindow.CollapsePrecursors();
                SkylineWindow.AutoZoomNone();
                SkylineWindow.ShowGraphPeakArea(false);
                SkylineWindow.ShowProductTransitions();
            });

            PauseForScreenShot("p 31 - full gradient import of high concentration");

            {
                var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                RunUI(() =>
                {
                    peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                    Assert.IsTrue(peptideSettingsUI.IsUseMeasuredRT);
                    Assert.AreEqual(2.0, peptideSettingsUI.TimeWindow);
                });

                PauseForScreenShot("p 32 - scheduling settings");

                RunUI(peptideSettingsUI.CancelDialog);
            }

            using (new CheckDocumentState(1, pepCount1, preCount1, tranCount2))
            {
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() =>
                {
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                    transitionSettingsUI.SetRetentionTimeFilter(RetentionTimeFilterType.scheduling_windows, 1);
                });

                PauseForScreenShot("p 33 - scheduled extraction");

                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            }

            var chooseSchedulingReplicateDlg = ShowDialog<ChooseSchedulingReplicatesDlg>(SkylineWindow.ImportResults);
            RunUI(()=>chooseSchedulingReplicateDlg.SelectOrDeselectAll(true));
            RunDlg<ImportResultsDlg>(chooseSchedulingReplicateDlg.OkDialog, importResultsDlg2 =>
            {
                string[] filePaths =
                    {
                        GetTestPath(@"TOF\1-BSA-50amol"  + ExtAgilentRaw),
                        GetTestPath(@"TOF\2-BSA-100amol" + ExtAgilentRaw),
                        GetTestPath(@"TOF\3-BSA-1fmol"   + ExtAgilentRaw),
                        GetTestPath(@"TOF\4-BSA-10fmol"  + ExtAgilentRaw),
                        GetTestPath(@"TOF\5-BSA-100fmol" + ExtAgilentRaw),
                    };
                importResultsDlg2.NamedPathSets = importResultsDlg2.GetDataSourcePathsFileReplicates(filePaths);
                importResultsDlg2.OkDialog();
            });
            //Give the Raw files some time to be processed.
            WaitForCondition(15 * 60 * 1000, () => SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded); // 15 minutes                  
            RunUI(() => SkylineWindow.ShowGraphSpectrum(false));
            RunUI(() =>
            {
                Assert.IsFalse(SkylineWindow.IsGraphSpectrumVisible);
                Assert.AreEqual(6, SkylineWindow.GraphChromatograms.Count(graphChrom => !graphChrom.IsHidden));
                var chromGraphs = SkylineWindow.GraphChromatograms.ToArray();
                Assert.AreEqual(6, chromGraphs.Length);
            });

            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                for (int i = 0; i < 5; i++)
                    dlg.MoveDown();
                dlg.OkDialog();
            });
            RunDlg<ArrangeGraphsGroupedDlg>(SkylineWindow.ArrangeGraphsGrouped, dlg =>
            {
                dlg.Groups = 6;
                dlg.GroupOrder = GroupGraphsOrder.Document;
                dlg.OkDialog();
            });
            WaitForGraphs();

            PauseForScreenShot("p. 34 - chromatogram graphs");

            RunUI(() =>
            {
                // Should look like graphs on page 27
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

            PauseForScreenShot("p. 35 - chromatogram graphs zoomed");

            RunUI(() =>
            {
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.ShowPeptideLogScale(true);
            });            

            // p. 28
            PauseForScreenShot("p. 36 - Area replicate graph");
            WaitForDotProducts();
            RunUI(() =>
            {
                Assert.AreEqual(7, SkylineWindow.GraphPeakArea.Categories.Count());
                Assert.AreEqual(6, SkylineWindow.GraphPeakArea.CurveCount);
            });
            VerifyDotProducts(0.87, 0.67, 0.91, 0.90, 0.90, 0.90);

            RunUI(() =>
            {
                SkylineWindow.ShowAllTransitions();
                SkylineWindow.ShowSplitChromatogramGraph(false);
                SkylineWindow.ArrangeGraphsTabbed();
                SkylineWindow.ActivateReplicate("6-BSA-500fmol");
            });
            PauseForScreenShot("p. 37 - Chromatograms for 500 fmol");

            RunUI(() =>
            {
                SkylineWindow.ShowPrecursorTransitions();
                SkylineWindow.ArrangeGraphsTiled();
                SkylineWindow.ActivateReplicate("4-BSA-10fmol");
            });

            // p. 30
            WaitForDotProducts();
            RunUI(() =>
            {
                Assert.AreEqual(7, SkylineWindow.GraphPeakArea.Categories.Count());
                Assert.AreEqual(3, SkylineWindow.GraphPeakArea.CurveCount);
            });
            VerifyDotProducts(0.99, 0.52, 0.98, 1.00, 1.00, 1.00);
            PauseForScreenShot("p. 38 and 39 - Chromatograms and area replicate graph");

            RunUI(() => SkylineWindow.SaveDocument());
            WaitForConditionUI(() => !SkylineWindow.Dirty);
        }

        private void WaitForDotProducts()
        {
            WaitForGraphs();
            WaitForConditionUI(() => AreaGraphDotProducts.Any(p => p != 0));
        }

        private void VerifyDotProducts(params double[] dotpExpects)
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
    }
}
