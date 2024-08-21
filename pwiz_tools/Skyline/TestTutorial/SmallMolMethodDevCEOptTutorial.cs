/*
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.Graphs.Calibration;
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
using ZedGraph;
using SampleType = pwiz.Skyline.Model.DocSettings.AbsoluteQuantification.SampleType;

namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class SmallMolMethodDevCEOptTutorialTest : AbstractFunctionalTest
    {
        protected override bool UseRawFiles
        {
            get { return !ForceMzml && ExtensionTestContext.CanImportWatersRaw; }
        }

        [TestMethod]
        public void TestSmallMolMethodDevCEOptTutorial()
        {
            // Set true to look at tutorial screenshots.
//            IsPauseForScreenShots = true;
//            IsCoverShotMode = true;
            CoverShotName = "SmallMoleculeMethodDevCEOpt";

            ForceMzml = true; // Prefer mzML as being the more efficient download
            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/SmallMoleculeMethodDevCEOpt-23_1.pdf";

            TestFilesZipPaths = new[]
            {
                UseRawFiles
                    ? @"https://skyline.ms/tutorials/SmallMolMethodCE.zip"
                    : @"https://skyline.ms/tutorials/SmallMolMethodCE_mzML.zip",
                @"TestTutorial\SmallMolMethodDevCEOptViews.zip"
            };
            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath = null)
        {
            string folderSmallMolecule = UseRawFiles ? "SmallMolMethodCE" : "SmallMolMethodCE_mzML";
            string fullRelativePath = relativePath != null ? Path.Combine(folderSmallMolecule, relativePath) : folderSmallMolecule;
            return TestFilesDirs[0].GetTestPath(fullRelativePath);
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules));

            // Inserting a Transition List, p. 5
            {
                var doc = SkylineWindow.Document;

                SetCsvFileClipboardText(GetTestPath("Energy_TransitionList.csv"));
                var confirmHeadersDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(SkylineWindow.Paste);
                PauseForScreenShot<ImportTransitionListColumnSelectDlg>("Confirming column headers", 5);
                OkDialog(confirmHeadersDlg, confirmHeadersDlg.OkDialog);

                RunUI(() =>
                {
                    AdjustSequenceTreePanelWidth();
                });

                PauseForScreenShot<SkylineWindow>("Main window after paste from csv", 5);

                var docTargets = WaitForDocumentChange(doc);

                AssertEx.IsDocumentState(docTargets, null, 3, 18, 36, 36);
                Assert.IsFalse(docTargets.MoleculeTransitions.Any(t => t.Transition.IsPrecursor()));

                {
                    var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                    RunUI(() =>
                    {
                        // Predicition Settings
                        transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Prediction;
                        transitionSettingsUI.RegressionCEName = "Waters Xevo"; // Collision Energy
                    });
                    PauseForScreenShot<TransitionSettingsUI.PredictionTab>("Transition Settings - Prediction tab", 6);


                    RunUI(() =>
                    {
                        // Filter Settings
                        transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Filter;
                        transitionSettingsUI.SmallMoleculePrecursorAdducts = Adduct.M_MINUS.AdductFormula;
                        transitionSettingsUI.SmallMoleculeFragmentAdducts = Adduct.M_MINUS.AdductFormula;
                        transitionSettingsUI.SmallMoleculeFragmentTypes = TransitionFilter.SMALL_MOLECULE_FRAGMENT_CHAR;
                    });
                    PauseForScreenShot<TransitionSettingsUI.PredictionTab>("Transition Settings -Filter tab", 7);

                    OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
                    WaitForDocumentChange(docTargets);
                }

                RunUI(() => SkylineWindow.SaveDocument(GetTestPath("EnergyMet.sky")));


                // Export method - 2 minutes
                {
                    var exportMethodDlg2 = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));
                    RunUI(() =>
                    {
                        exportMethodDlg2.InstrumentType = ExportInstrumentType.WATERS_XEVO_TQ;
                        exportMethodDlg2.MethodType = ExportMethodType.Standard;
                        exportMethodDlg2.RunLength = 2;
                        exportMethodDlg2.SetTemplateFile("VerifyETemplate.exp");
                    });
                    PauseForScreenShot<ExportMethodDlg>("Exporting 2 minute method", 9);
                    OkDialog(exportMethodDlg2, exportMethodDlg2.CancelDialog);
                }

                // Export transition list
                {
                    var exportTransitionList = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                    RunUI(() =>
                    {
                        exportTransitionList.InstrumentType = ExportInstrumentType.WATERS_XEVO_TQ;
                        exportTransitionList.MethodType = ExportMethodType.Standard;
                        exportTransitionList.RunLength = 2;
                    });
                    PauseForScreenShot<ExportMethodDlg>("Exporting transition list", 10);
                    OkDialog(exportTransitionList, exportTransitionList.CancelDialog);
                }

                using (new WaitDocumentChange(1, true))
                {
                    var importResultsDlg1 = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
                    var openDataSourceDialog1 = ShowDialog<OpenDataSourceDialog>(() => importResultsDlg1.NamedPathSets =
                        importResultsDlg1.GetDataSourcePathsFile(null));
                    RunUI(() =>
                    {
                        openDataSourceDialog1.CurrentDirectory = new MsDataFilePath(GetTestPath("Unscheduled"));
                        openDataSourceDialog1.SelectAllFileType(ExtWatersRaw);
                    });
                    PauseForScreenShot<OpenDataSourceDialog>("Import Results Files form", 11);
                    OkDialog(openDataSourceDialog1, openDataSourceDialog1.Open);
 
                    var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg1.OkDialog);
                    PauseForScreenShot<ImportResultsNameDlg>("Import Results common name form", 12);
                    OkDialog(importResultsNameDlg, importResultsNameDlg.OkDialog);
                    OkDialog(importResultsDlg1,importResultsDlg1.OkDialog);
                }

                SelectNode(SrmDocument.Level.Molecules, 0);
                SelectNode(SrmDocument.Level.MoleculeGroups, 0);

                PauseForScreenShot<SkylineWindow>("Skyline window multi-target graph", 13);

                // Renaming replicates
                {
                    var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
                    string[] repNames = new[] { "2 min", "5 min" };
                    RunUI(() => manageResultsDlg.Left = SkylineWindow.Right + 50);
                    for (var i = 0; i < 2; i++)
                    {
                        doc = SkylineWindow.Document;
                        var chromatograms = doc.Settings.MeasuredResults.Chromatograms;
                        var chrom = chromatograms[i];
                        RunUI(() =>
                        {
                            manageResultsDlg.SelectedChromatograms = new[] { chrom };
                        });

                        var renameDlg = ShowDialog<RenameResultDlg>(manageResultsDlg.RenameResult);
                        RunUI(() => renameDlg.ReplicateName = repNames[i]);
                        if (i == 0)
                            PauseForScreenShot<SkylineWindow>("Manage Results and Rename Replicate (PrtScn and select in Paint)", 14);
                        OkDialog(renameDlg, renameDlg.OkDialog);
                    }
                    OkDialog(manageResultsDlg, manageResultsDlg.OkDialog);
                }

                PauseForScreenShot<SkylineWindow>("Skyline window (renamed)", 15);

                var docResults = SkylineWindow.Document;

                var msg = "";
                foreach (var chromatogramSet in docResults.Settings.MeasuredResults.Chromatograms)
                {
                    try
                    {
                        AssertResult.IsDocumentResultsState(docResults, chromatogramSet.Name, 18, 18, 18, 18, 18);
                    }
                    catch(Exception x)
                    {
                        msg += TextUtil.LineSeparate(x.Message);
                    }
                }
                if (!string.IsNullOrEmpty(msg))
                    Assert.IsTrue(string.IsNullOrEmpty(msg), msg);
                RunUI(() =>
                {
                    SkylineWindow.ShowPeakAreaReplicateComparison();
                    SkylineWindow.ShowRTReplicateGraph();
                    SkylineWindow.ArrangeGraphsTiled();
                });
                RestoreViewOnScreen(16);
                SelectNode(SrmDocument.Level.MoleculeGroups, 0);
                WaitForGraphs();
                RunUI(() =>
                {
                    SkylineWindow.Size = new Size(1054, 587);
                    AdjustSequenceTreePanelWidth(true);
                });

                PauseForScreenShot<SkylineWindow>("Skyline window multi-replicate layout", 16);

                // Set zoom to show better peak separation in 5 minute run
                for (var i = 0; i < 2; i++)
                {
                    WaitForGraphs();
                    RunUI(() => SkylineWindow.GraphChromatograms.ToArray()[i].ZoomTo(.8, 1.8, 1.39e+8));
                    WaitForGraphs();
                }
                PauseForScreenShot<SkylineWindow>("Skyline window showing relative peak separation", 17);

                // Set time window
                {
                    var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                    RunUI(() =>
                    {
                        // ReSharper disable once RedundantCast
                        peptideSettingsDlg.SelectedTab = (PeptideSettingsUI.TABS)0; //regular enum does not work because of the hidden tabs in the Small Molecule mode.
                        peptideSettingsDlg.TimeWindow = 1;
                    });
                    PauseForScreenShot<PeptideSettingsUI>("Setting scheduled transition list time window", 18);
                    OkDialog(peptideSettingsDlg, peptideSettingsDlg.OkDialog);
                }

                // Export transition list
                {
                    var exportTransitionList = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                    RunUI(() =>
                    {
                        exportTransitionList.InstrumentType = ExportInstrumentType.WATERS_XEVO_TQ;
                        exportTransitionList.MethodType = ExportMethodType.Scheduled;
                        exportTransitionList.OptimizeType = ExportOptimize.NONE;
                    });
                    PauseForScreenShot<ExportMethodDlg>("Exporting scheduled transition list", 19);

                    var schedulingOptionsDlg = ShowDialog<SchedulingOptionsDlg>(() => exportTransitionList.OkDialog(GetTestPath("EnergyMet_5minutes_scheduled.csv")));
                    RunUI(() =>
                    {
                        schedulingOptionsDlg.Algorithm = ExportSchedulingAlgorithm.Single;
                        schedulingOptionsDlg.ReplicateNum = 1;  // 5 min
                    });
                    PauseForScreenShot<SchedulingOptionsDlg>("Exporting scheduled transition list - choose replicate", 19);
                    OkDialog(schedulingOptionsDlg, schedulingOptionsDlg.OkDialog);
                    WaitForClosedForm(exportTransitionList);
                }

                using (new WaitDocumentChange(1, true))
                {
                    var importResultsDlg1 = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
                    var openDataSourceDialog1 = ShowDialog<OpenDataSourceDialog>(() => importResultsDlg1.NamedPathSets =
                        importResultsDlg1.GetDataSourcePathsFile(null));
                    RunUI(() =>
                    {
                        openDataSourceDialog1.CurrentDirectory = new MsDataFilePath(GetTestPath("Scheduled"));
                        openDataSourceDialog1.SelectAllFileType(ExtWatersRaw);
                    });
                    OkDialog(openDataSourceDialog1, openDataSourceDialog1.Open);

                    var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg1.OkDialog);
                    RunUI(() => importResultsNameDlg.IsRemove = false);
                    PauseForScreenShot<ImportResultsNameDlg>("Import Results common name form, not changing names", 20);
                    OkDialog(importResultsNameDlg, importResultsNameDlg.OkDialog);
                }

                // Remove 2 minute gradient
                // Renaming replicates
                string[] newNames = { "1:1_1", "1:1_2", "2:1_2", "1:2_2" };
                {
                    var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
                    SelectReplicate(manageResultsDlg, 0);

                    PauseForScreenShot<SkylineWindow>("Manage Results removing 2 min", 21);

                    RunUI(manageResultsDlg.RemoveReplicates);

                    for (var i = 0; i < newNames.Length; i++)
                    {
                        SelectReplicate(manageResultsDlg, i + 1);
                        RunDlg<RenameResultDlg>(manageResultsDlg.RenameResult, renameDlg =>
                        {
                            renameDlg.ReplicateName = newNames[i];
                            renameDlg.OkDialog();
                        });
                    }

                    PauseForScreenShot<SkylineWindow>("Manage Results replicate renamed", 22);
                    OkDialog(manageResultsDlg, manageResultsDlg.OkDialog);
                }

                SelectNode(SrmDocument.Level.Molecules, 0);
                RunUI(() =>
                {
                    var molNode = SkylineWindow.SelectedNode;
                    molNode.Expand();
                    Assert.AreEqual(2, molNode.Nodes.Count);
                    molNode.Nodes[0].Expand();
                    molNode.Nodes[1].Expand();
                    SkylineWindow.ArrangeGraphsTabbed();
                });
                PauseForScreenShot<SkylineWindow>("Skyline window with calibration data", 23);

                // Linearity
                var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
                RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates));

                double[] concentrations = {1, 1, 2, 0.5};
                var namedConcentrations = newNames.Select((n, i) => new Tuple<string, double?>(n, concentrations[i]));
                SetDocumentGridSampleTypesAndConcentrations(namedConcentrations.ToDictionary(t => t.Item1,
                    t => new Tuple<SampleType, double?>(SampleType.STANDARD, t.Item2)));
                RunUI(() =>
                {
                    var gridFloatingWindow = documentGrid.Parent.Parent;
                    gridFloatingWindow.Size = new Size(370, 230);
                    gridFloatingWindow.Top = SkylineWindow.Top;
                    gridFloatingWindow.Left = SkylineWindow.Right + 20;
                });
                PauseForScreenShot<DocumentGridForm>("Document Grid - sample types and concentrations ", 23);
                RunUI(() => SkylineWindow.ShowDocumentGrid(false));

                using (new WaitDocumentChange(1, true))
                {
                    // Quant settings
                    var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);

                    RunUI(() =>
                    {
                        // ReSharper disable once RedundantCast
                        peptideSettingsUI.SelectedTab = (PeptideSettingsUI.TABS)3;
                        peptideSettingsUI.QuantRegressionFit = RegressionFit.LINEAR;
                        peptideSettingsUI.QuantNormalizationMethod =
                            new NormalizationMethod.RatioToLabel(IsotopeLabelType.heavy);
                        peptideSettingsUI.QuantUnits = "ratio to heavy";
                    });

                    PauseForScreenShot<PeptideSettingsUI.QuantificationTab>("Peptide Settings - Quantitation", 24);
                    OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
                }

                var calibrationForm = ShowDialog<CalibrationForm>(() => SkylineWindow.ShowCalibrationForm());
                RunUI(() =>
                {
                    var calibrationFloatingWindow = calibrationForm.Parent.Parent;
                    calibrationFloatingWindow.Width = 565;
                    calibrationFloatingWindow.Top = SkylineWindow.Top;
                    calibrationFloatingWindow.Left = SkylineWindow.Right + 20;
                });
                PauseForScreenShot<CalibrationForm>("Calibration Curve ", 25);
                OkDialog(calibrationForm, calibrationForm.Close); // Hide the calibration window

                {
                    var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                    RunUI(() => transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Prediction);
                    var editCurrentCE = ShowDialog<EditCEDlg>(transitionSettingsUI.EditCECurrent);
                    RunUI(() =>
                    {
                        editCurrentCE.StepSize = 2;
                        editCurrentCE.StepCount = 5;
                    });
                    PauseForScreenShot<EditCEDlg>("Edit Collision Energy Equation form", 26);

                    OkDialog(editCurrentCE, editCurrentCE.OkDialog);
                    RunUI(() =>
                    {
                        transitionSettingsUI.UseOptimized = true;
                        transitionSettingsUI.OptimizeType = OptimizedMethodType.Transition.GetLocalizedString();
                    });
                    PauseForScreenShot<TransitionSettingsUI.PredictionTab>("Transition Settings - Prediction tab", 27);
                    OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
                }

                // Export transition lists
                {
                    var exportTransitionList = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                    RunUI(() =>
                    {
                        exportTransitionList.ExportStrategy = ExportStrategy.Buckets;
                        exportTransitionList.IgnoreProteins = true;
                        exportTransitionList.MaxTransitions = 100;
                        exportTransitionList.InstrumentType = ExportInstrumentType.WATERS_XEVO_TQ;
                        exportTransitionList.MethodType = ExportMethodType.Scheduled;
                        exportTransitionList.OptimizeType = ExportOptimize.CE;
                    });
                    PauseForScreenShot<ExportMethodDlg>("Exporting scheduled transition list", 28);

                    var scheduleDlg = ShowDialog<SchedulingOptionsDlg>(() => exportTransitionList.OkDialog(GetTestPath("EnergyMet_5minutes_ceopt.csv")));
                    PauseForScreenShot<SchedulingOptionsDlg>("Scheduling", 29);
                    OkDialog(scheduleDlg, scheduleDlg.OkDialog);
                }

                // Import CE optimization runs
                using (new WaitDocumentChange(1, true))
                {
                    var importResultsDlg1 = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
                    RunUI(() =>
                    {
                        importResultsDlg1.RadioCreateMultipleChecked = true;
                        importResultsDlg1.OptimizationName = ExportOptimize.CE;
                        importResultsDlg1.ReplicateName = "CE Optimization";
                    });
                    PauseForScreenShot<ImportResultsDlg>("Setting new replicate name to CE Optimization", 30);
                    var openDataSourceDialog1 = ShowDialog<OpenDataSourceDialog>(() => importResultsDlg1.OkDialog());
                    RunUI(() =>
                    {
                        openDataSourceDialog1.CurrentDirectory = new MsDataFilePath(GetTestPath("CE Optimization"));
                        openDataSourceDialog1.SelectAllFileType(ExtWatersRaw);
                    });
                    PauseForScreenShot<OpenDataSourceDialog>("Import Results Files form", 31);
                    OkDialog(openDataSourceDialog1, openDataSourceDialog1.Open);
                }

                PauseForScreenShot<SkylineWindow>("Skyline shows new replicate \"CE Optimization\"", 32);
                RunUI(() =>
                {
                    SkylineWindow.Size = new Size(1600, 960);
                    SkylineWindow.ShowGraphRetentionTime(false, GraphTypeSummary.replicate);
                    SkylineWindow.AutoZoomBestPeak();
                    AdjustSequenceTreePanelWidth();
                    SkylineWindow.ShowSingleTransition();
                    SkylineWindow.ShowSplitChromatogramGraph(true);
                });
                PauseForScreenShot<SkylineWindow>("Split graph", 33);

                RunUI(() =>
                {
                    SkylineWindow.ShowPeakAreaLegend(false);
                });
                PauseForScreenShot<SkylineWindow>("No legend", 34);

                TestAsymmetricOptimization();

                // Show Pentose-P
                SelectNode(SrmDocument.Level.Molecules, 6);
                PauseForScreenShot<SkylineWindow>("Pentose-P", 35);

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
                    TakeCoverShot();
                    return;
                }

                // Export final transition list
                {
                    var exportTransitionList = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                    RunUI(() =>
                    {
                        exportTransitionList.ExportStrategy = ExportStrategy.Single;
                        exportTransitionList.IgnoreProteins = true;
                        exportTransitionList.MaxTransitions = 100;
                        exportTransitionList.InstrumentType = ExportInstrumentType.WATERS_XEVO_TQ;
                        exportTransitionList.MethodType = ExportMethodType.Scheduled;
                    });
                    PauseForScreenShot<ExportMethodDlg>("Exporting final optimized transition list", 36);

                    var scheduleDlg = ShowDialog<SchedulingOptionsDlg>(() => exportTransitionList.OkDialog(GetTestPath("EnergyMet_5minutes_optimal.csv")));
                    PauseForScreenShot<SchedulingOptionsDlg>("Final Scheduling", 37);
                    OkDialog(scheduleDlg, scheduleDlg.OkDialog);
                }
            }
        }

        private void TestAsymmetricOptimization()
        {
            SelectNode(SrmDocument.Level.TransitionGroups, 1);
            WaitForGraphs();
            TestSelectedAsymOpt(0.95, 0);
            SelectNode(SrmDocument.Level.TransitionGroups, 2);
            WaitForGraphs();
            TestSelectedAsymOpt(0.99, 2);
            SelectNode(SrmDocument.Level.Transitions, 2);
            WaitForGraphs();
            TestSelectedAsymOpt(0.99, 2);

            // Close the opened second molecule node.
            RunUI(() => SkylineWindow.SequenceTree.Nodes[0].Nodes[1].Collapse());
        }

        private void TestSelectedAsymOpt(double expectedMinCorr, int expMaxStep)
        {
            RunUI(() =>
            {
                var chromPoints = SkylineWindow.GraphChromatograms.Last().CurveList;
                Assert.AreEqual(11, chromPoints.Count);
                AreaReplicateGraphPane pane;
                SkylineWindow.GraphPeakArea.TryGetGraphPane(out pane);
                var areaPoints = pane.CurveList;
                Assert.AreEqual(chromPoints.Count, areaPoints.Count);
                var chromIntensities = new List<double>();
                var areaIntensities = new List<double>();
                for (int i = 0; i < chromPoints.Count; i++)
                {
                    var chrom = chromPoints[i];
                    var area = areaPoints[i];
                    Assert.AreEqual(chrom.Label.Text, area.Label.Text);
                    chromIntensities.Add(ToIntensities(chrom.Points).Max());
                    areaIntensities.Add(ToIntensities(area.Points).Last());
                }

                var statChrom = new Statistics(chromIntensities);
                var statArea = new Statistics(areaIntensities);
                double corr = statChrom.R(statArea);
                Assert.IsTrue(corr >= expectedMinCorr, "Correlation between chromatogram and area intensities {0} expected to be >= {1}",
                    corr, expectedMinCorr);
                Assert.AreEqual(expMaxStep, areaIntensities.IndexOf(statArea.Max()) - 5);
                Assert.AreEqual(expMaxStep, chromIntensities.IndexOf(statChrom.Max()) - 5);
            });
        }

        private IEnumerable<double> ToIntensities(IPointList points)
        {
            // Return at least a single zero point even if there are no points
            if (points.Count == 0)
                yield return 0;

            for (int i = 0; i < points.Count; i++)
            {
                if (points[i].IsMissing)
                    yield return 0;
                else
                    yield return points[i].Y;
            }
        }

        private static void SelectReplicate(ManageResultsDlg manageResultsDlg, int replicateIndex)
        {
            RunUI(() =>
            {
                manageResultsDlg.SelectedChromatograms = new[]
                {
                    SkylineWindow.DocumentUI.Settings.MeasuredResults.Chromatograms[replicateIndex]
                };
            });
        }
    }
}
