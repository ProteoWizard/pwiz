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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate.Query;
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
            TestFilesZip = UseRawFiles
                ? @"http://skyline.gs.washington.edu/tutorials/TargetedMSMS.zip"
                : @"http://skyline.gs.washington.edu/tutorials/TargetedMSMSMzml.zip";
            RunFunctionalTest();
        }

        private bool UseRawFiles
        {
            get
            {
                // TODO: Figure out why using Agilent files causes frequent failures in this test
                return false; // ExtensionTestContext.CanImportThermoRaw && ExtensionTestContext.CanImportAgilentRaw;
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
            string folderMSMS = UseRawFiles ? "TargetedMSMS" : "TargetedMSMSMzml";
            return TestFilesDir.GetTestPath(Path.Combine(folderMSMS, relativePath));
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

            // p.4 Configure Document for Thermo raw files

            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionalSettingsUI =>
            {
                transitionalSettingsUI.SelectedTab =TransitionSettingsUI.TABS.FullScan;
                transitionalSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                transitionalSettingsUI.PrecursorMassAnalyzer = FullScanMassAnalyzerType.qit;
                transitionalSettingsUI.AcquisitionMethod = FullScanAcquisitionMethod.Targeted;
                // p.6
                transitionalSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Filter;
                transitionalSettingsUI.FragmentTypes += ", p";
                transitionalSettingsUI.OkDialog();
            });

            var docFullScan = WaitForDocumentChange(document);
            var tranSettingsFullScan = docFullScan.Settings.TransitionSettings;
            Assert.AreEqual(FullScanPrecursorIsotopes.Count, tranSettingsFullScan.FullScan.PrecursorIsotopes);
            Assert.AreEqual(FullScanMassAnalyzerType.qit, tranSettingsFullScan.FullScan.PrecursorMassAnalyzer);
            Assert.AreEqual(FullScanAcquisitionMethod.Targeted, tranSettingsFullScan.FullScan.AcquisitionMethod);
            Assert.IsTrue(ArrayUtil.EqualsDeep(new[] {IonType.y, IonType.b, IonType.precursor},
                                               tranSettingsFullScan.Filter.IonTypes));

            RunUI(() => SkylineWindow.ExpandPrecursors());

            // Check all the precursors on picklists
            foreach (PeptideGroupTreeNode node in SkylineWindow.SequenceTree.GetSequenceNodes())
            {
                foreach (TreeNode child in node.Nodes)
                {
                    foreach (SrmTreeNodeParent grandChild in child.Nodes)
                    {
                        // Because of RunUI must copy to local variable first.
                        SrmTreeNodeParent child1 = grandChild;
                        RunUI(() => SkylineWindow.SequenceTree.SelectedNode = child1);
                        RunDlg<PopupPickList>(() => SkylineWindow.SequenceTree.ShowPickList(false), picklist =>
                        {
                            picklist.SetItemChecked(0, true);
                            Assert.IsTrue(picklist.GetItemLabel(0).Contains("precursor"));
                            Assert.IsTrue(picklist.GetItemChecked(0));
                            picklist.OnOk();
                        });
                    }
                }
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
            // p. 10 Ok the error box.
            RunDlg<MessageDlg>(() => exportMethodDlg.OkDialog(GetTestPath(@"Low Res\TargetedMSMS_BSA_Protea.meth")),
                               dialog => dialog.OkDialog());

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

            RunDlg<PreviewReportDlg>(pivotReportDlg.ShowPreview, previewReportDlg =>
            {
                Assert.AreEqual(10, previewReportDlg.RowCount);
                Assert.AreEqual(4, previewReportDlg.ColumnCount);
                var precursors =
                    SkylineWindow.Document.TransitionGroups.ToArray();
                for (int i = 0; i < 10; i++)
                {
                    Assert.AreEqual(precursors[i].PrecursorMz,double.Parse(previewReportDlg.DataGridView.Rows[i].Cells[3].Value.ToString()),0.000001);
                }
                previewReportDlg.OkDialog();
            });

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

            //p. 12 Import Full-Scan Data
            ImportResultsDlg importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            const int prefixLen = 35;
            string lowRes20File = GetTestPath(@"Low Res\klc_20100329v_Protea_Peptide_Curve_20fmol_uL_tech1" + ExtThermoRaw);
            string shortLowRes20FileName = (Path.GetFileNameWithoutExtension(lowRes20File) ?? "").Substring(prefixLen);
            string lowRes80File = GetTestPath(@"Low Res\klc_20100329v_Protea_Peptide_Curve_80fmol_uL_tech1" + ExtThermoRaw);
            string shortLowRes80FileName = (Path.GetFileNameWithoutExtension(lowRes80File) ?? "").Substring(prefixLen);
            RunUI(() => importResultsDlg.NamedPathSets = importResultsDlg.GetDataSourcePathsFileReplicates(
                    new[] {lowRes20File, lowRes80File}));

            ImportResultsNameDlg importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg.OkDialog);
            RunUI(importResultsNameDlg.YesDialog);
            // Give the Raw files some time to be processed.
            WaitForCondition(15*60*1000, () => SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded); // 15 minutes

            AssertResult.IsDocumentResultsState(SkylineWindow.Document, shortLowRes20FileName, 9, 10, 0, 82, 0);
            AssertResult.IsDocumentResultsState(SkylineWindow.Document, shortLowRes80FileName, 9, 10, 0, 87, 0);

            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.GraphSpectrumVisible());
                SkylineWindow.ShowGraphSpectrum(false);
                Assert.IsFalse(SkylineWindow.GraphSpectrumVisible());
                SkylineWindow.ArrangeGraphsTiled();
                SkylineWindow.CollapsePrecursors();
            });

            // Select the first precursor. 
            FindNode("K.LVNELTEFAK.T [65, 74]");
            // Ensure Graphs look like p13. (checked)
//            WaitForGraphs();

            RunUI(() => SkylineWindow.AutoZoomBestPeak());
            // Ensure Graphs look like p14. (checked)
//            WaitForGraphs();

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
                          VerifyDotProducts(0.99, 0.99);
                      });

            // Check graph p15. (checked)
            RunUI(() => SkylineWindow.ShowGraphPeakArea(false));
            WaitForCondition(() => SkylineWindow.GraphPeakArea.IsHidden);


            // Select precursor
            FindNode("R.DRVYIHPF.- [34, 41] (missed 1)");

            WaitForGraphs();

            // Check graph p17. (checked)
            RunUI(() =>
            {
                var chromGraphs = SkylineWindow.GraphChromatograms.ToArray();
                Assert.AreEqual(40.1, chromGraphs[0].GraphItems.First(g => g.BestPeakTime > 0).BestPeakTime, 0.05);
                Assert.AreEqual(44.3, chromGraphs[1].GraphItems.First(g => g.BestPeakTime > 0).BestPeakTime, 0.05);

                SkylineWindow.LockYChrom(false);
                SkylineWindow.SynchronizeZooming(true);
                // Trouble getting the scroll wheel back and fourth to get graphs to zoom. Instead change the peak.
                var pathGroup =
                    SkylineWindow.SequenceTree.GetNodePath((TreeNodeMS) SkylineWindow.SelectedNode.Nodes[0]);
                var graphChrom = SkylineWindow.GraphChromatograms.ToList()[0];
                var listChanges = new List<ChangedPeakBoundsEventArgs>
                        {
                            new ChangedPeakBoundsEventArgs(pathGroup, null, graphChrom.NameSet, graphChrom.ChromGroupInfos[0].FilePath, 44.0, 45.0, false, PeakBoundsChangeType.both)
                        };
                graphChrom.SimulateChangedPeakBounds(listChanges);
                foreach (TransitionTreeNode node in SkylineWindow.SequenceTree.SelectedNode.Nodes[0].Nodes)
                {
                    Assert.IsTrue(((TransitionDocNode) node.Model).HasResults);
                }
                Assert.AreEqual(2, chromGraphs.Length);

                SkylineWindow.AutoZoomBestPeak();
            });

            WaitForGraphs();

            RunUI(() =>
            {
                var chromGraphs = SkylineWindow.GraphChromatograms.ToArray();
                Assert.AreEqual(44.3, chromGraphs[0].GraphItems.First(g => g.BestPeakTime > 0).BestPeakTime, 0.05);
                Assert.AreEqual(44.3, chromGraphs[1].GraphItems.First(g => g.BestPeakTime > 0).BestPeakTime, 0.05);
            });

            FindNode("R.IKNLQSLDPSH.- [80, 90]");
            RunUI(() => SkylineWindow.ShowPeakAreaReplicateComparison());
            WaitForCondition(() => !SkylineWindow.GraphPeakArea.IsHidden);
            WaitForGraphs();
            RunUI(() =>
            {
                // Graph p.19
                Assert.AreEqual(2, SkylineWindow.GraphPeakArea.Categories.Count());
                Assert.AreEqual(7, SkylineWindow.GraphPeakArea.CurveCount);
            });

            // PeakAreaGraph Normalize to total p.20.
            RunUI(() => SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.area_percent_view));

            // Ensure graph looks like p20.
            FindNode("R.IKNLQSLDPSH.- [80, 90]");
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.AreEqual(2, SkylineWindow.GraphPeakArea.Categories.Count());
                Assert.AreEqual(9, SkylineWindow.GraphPeakArea.CurveCount);
            });
            FindNode("K.HLVDEPQNLIK.Q [401, 411]");
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.AreEqual(3, SkylineWindow.GraphPeakArea.Categories.Count());
                Assert.AreEqual(7, SkylineWindow.GraphPeakArea.CurveCount);
            });

            // Show all transitions.
            RunUI(() => SkylineWindow.ShowAllTransitions());
            FindNode("K.YICDNQDTISSK.L [285, 296]");
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.AreEqual(2, SkylineWindow.GraphPeakArea.Categories.Count());
                Assert.AreEqual(9, SkylineWindow.GraphPeakArea.CurveCount);
            });
            // Ensure graphs look like p21.  
            RunUI(() =>
            {
                var chromGraphs = SkylineWindow.GraphChromatograms.ToArray();
                Assert.AreEqual(32.6, chromGraphs[0].GraphItems.First(g => g.BestPeakTime > 0).BestPeakTime, 0.05);
                Assert.AreEqual(32.6, chromGraphs[1].GraphItems.First(g => g.BestPeakTime > 0).BestPeakTime, 0.05);
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
                new[] { GetTestPath(@"TOF\1-BSA-50amol" + ExtAgilentRaw) }));
            RunDlg<MessageDlg>(importResultsDlg3.OkDialog, dlg =>
            {
                AssertEx.Contains(dlg.Message,
                                  "No SRM/MRM data found in 1-BSA-50amol.");
                dlg.OkDialog();
            });
            RunUI(() =>
            {
                SkylineWindow.Undo();
                SkylineWindow.Undo();
            });

            var document = SkylineWindow.Document;

            // Fill out Transition Settings Menu
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionalSettingsUI =>
            {
                transitionalSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                transitionalSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                transitionalSettingsUI.PrecursorMassAnalyzer = FullScanMassAnalyzerType.tof;
                transitionalSettingsUI.Peaks = "3";
                transitionalSettingsUI.AcquisitionMethod = FullScanAcquisitionMethod.Targeted;
                transitionalSettingsUI.ProductMassAnalyzer = FullScanMassAnalyzerType.tof;
                transitionalSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Filter;
                transitionalSettingsUI.FragmentTypes += ", p";
                transitionalSettingsUI.OkDialog();
            });

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

            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg2 =>
            {
                string[] filePaths = new[]
                    {
                        GetTestPath(@"TOF\1-BSA-50amol"  + ExtAgilentRaw),
                        GetTestPath(@"TOF\2-BSA-100amol" + ExtAgilentRaw),
                        GetTestPath(@"TOF\3-BSA-1fmol"   + ExtAgilentRaw),
                        GetTestPath(@"TOF\4-BSA-10fmol"  + ExtAgilentRaw),
                        GetTestPath(@"TOF\5-BSA-100fmol" + ExtAgilentRaw),
                        GetTestPath(@"TOF\6-BSA-500fmol" + ExtAgilentRaw)
                    };
                importResultsDlg2.NamedPathSets = importResultsDlg2.GetDataSourcePathsFileReplicates(filePaths);
                importResultsDlg2.OkDialog();
            });
            //Give the Raw files some time to be processed.
            WaitForCondition(15 * 60 * 1000, () => SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded); // 15 minutes                  
            RunUI(() => SkylineWindow.ShowGraphSpectrum(false));
            FindNode("K.LVNELTEFAK.T [65, 74]");
            RunUI(() =>
            {
                Assert.AreEqual("K.LVNELTEFAK.T [65, 74]", SkylineWindow.SelectedNode.Text);
                Assert.AreEqual(6, SkylineWindow.GraphChromatograms.Count(graphChrom => !graphChrom.IsHidden));
                var chromGraphs = SkylineWindow.GraphChromatograms.ToArray();
                Assert.AreEqual(6, chromGraphs.Length);
            });

            RunUI(() =>
            {
                Assert.IsFalse(SkylineWindow.GraphSpectrumVisible());
                SkylineWindow.ArrangeGraphsTiled();
                SkylineWindow.CollapsePrecursors();
                SkylineWindow.ShowProductTransitions();
                SkylineWindow.AutoZoomBestPeak();                          
            });

            WaitForGraphs();

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

                //Should look like graphs on page 27

                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.ShowPeptideLogScale(true);
                SkylineWindow.ShowReplicateOrder(SummaryReplicateOrder.time);
                SkylineWindow.ShowProductTransitions();
            });

            // p. 28
            WaitForDotProducts();
            RunUI(() =>
            {
                Assert.AreEqual(7, SkylineWindow.GraphPeakArea.Categories.Count());
                Assert.AreEqual(6, SkylineWindow.GraphPeakArea.CurveCount);

                VerifyDotProducts(0.98, 0.87, 0.99, 0.99, 0.99, 0.99);
            });

            RunUI(SkylineWindow.ShowPrecursorTransitions);

            // p. 30
            WaitForDotProducts();
            RunUI(() =>
            {
                Assert.AreEqual(7, SkylineWindow.GraphPeakArea.Categories.Count());
                Assert.AreEqual(3, SkylineWindow.GraphPeakArea.CurveCount);

                VerifyDotProducts(1.00, 0.73, 1.00, 1.00, 1.00, 1.00);
            });
        }

        private void WaitForDotProducts()
        {
            WaitForGraphs();
            WaitForConditionUI(() => AreaGraphDotProducts.Length > 0);
        }

        private void VerifyDotProducts(params double[] dotpExpects)
        {
            var dotpActuals = AreaGraphDotProducts;
            Assert.AreEqual(dotpExpects.Length, dotpActuals.Length);
            for (int i = 0; i < dotpExpects.Length; i++)
            {
                Assert.AreEqual(dotpExpects[i], dotpActuals[i], 0.05);
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

        private static void FindNode(string searchText)
        {
            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findPeptideDlg =>
            {
                findPeptideDlg.SearchString = searchText;
                findPeptideDlg.FindNext();
                findPeptideDlg.Close();
            });
        }
    }
}
