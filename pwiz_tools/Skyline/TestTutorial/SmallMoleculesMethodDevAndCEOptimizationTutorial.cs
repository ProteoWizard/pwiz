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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.Controls.Startup;
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

namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class SmallMoleculesMethodDevAndCEOptimizationTutorialTest : AbstractFunctionalTest
    {
        protected override bool UseRawFiles
        {
            get { return !ForceMzml && ExtensionTestContext.CanImportWatersRaw; }
        }

        protected override bool ShowStartPage
        {
            get { return true; }  // So we can point out the UI mode control
        }


        [TestMethod]
        public void TestSmallMoleculesMethodDevAndCEOptimizationTutorial()
        {
            // Set true to look at tutorial screenshots.
            // IsPauseForScreenShots = true;

            ForceMzml = true; // Prefer mzML as being the more efficient download

            LinkPdf = "https://skyline.gs.washington.edu/labkey/_webdav/home/software/Skyline/%40files/tutorials/SmallMoleculesMethodDevAndCEOptimization.pdf";

            TestFilesZipPaths = new[]
            {
                (UseRawFiles
                   ? @"https://skyline.gs.washington.edu/tutorials/SmallMolMethodCE.zip"
                   : @"https://skyline.gs.washington.edu/tutorials/SmallMolMethodCE_mzML.zip"),
                @"TestTutorial\SmallMoleculesMethodDevAndCEOptimizationViews.zip"
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
            // Setting the UI mode, p 2  
            var startPage = WaitForOpenForm<StartPage>();
            RunUI(() => startPage.SetUIMode(SrmDocument.DOCUMENT_TYPE.proteomic));
            PauseForScreenShot<StartPage>("Start Window proteomic", 2);
            RunUI(() => startPage.SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules));
            PauseForScreenShot<StartPage>("Start Window small molecule", 3);
            RunUI(() => startPage.DoAction(skylineWindow => true));
            WaitForOpenForm<SkylineWindow>();

            // Inserting a Transition List, p. 2
            {
                var doc = SkylineWindow.Document;

                SetCsvFileClipboardText(GetTestPath("Energy_TransitionList.csv"));
                RunUI(SkylineWindow.Paste);

                PauseForScreenShot<SkylineWindow>("after paste from csv", 3);

                var docTargets = WaitForDocumentChange(doc);

                AssertEx.IsDocumentState(docTargets, null, 3, 18, 36, 36);
                Assert.IsFalse(docTargets.MoleculeTransitions.Any(t => t.Transition.IsPrecursor()));

                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() =>
                {
                    // Predicition Settings
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Prediction;
                    transitionSettingsUI.PrecursorMassType = MassType.Monoisotopic;
                    transitionSettingsUI.FragmentMassType = MassType.Monoisotopic;
                    transitionSettingsUI.RegressionCEName = "Waters Xevo"; // Collision Energy
                    transitionSettingsUI.RegressionDPName = Resources.SettingsList_ELEMENT_NONE_None; // Declustering Potential
                    transitionSettingsUI.OptimizationLibraryName = Resources.SettingsList_ELEMENT_NONE_None; // Optimization Library
                    transitionSettingsUI.RegressionCOVName = Resources.SettingsList_ELEMENT_NONE_None; // Compensation Voltage

                    transitionSettingsUI.UseOptimized = false;
                });
                PauseForScreenShot<TransitionSettingsUI.PredictionTab>("Transition Settings - Prediction tab", 4);


                RunUI(() =>
                {
                    // Filter Settings
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Filter;
                    transitionSettingsUI.SelectedPeptidesSmallMolsSubTab = 1;
                    transitionSettingsUI.SmallMoleculePrecursorAdducts = Adduct.M_MINUS.AdductFormula;
                    transitionSettingsUI.SmallMoleculeFragmentAdducts = Adduct.M_MINUS.AdductFormula;
                    transitionSettingsUI.SmallMoleculeFragmentTypes = TransitionFilter.SMALL_MOLECULE_FRAGMENT_CHAR;
                    transitionSettingsUI.FragmentMassType = MassType.Monoisotopic;
                    transitionSettingsUI.SetAutoSelect = true;
                });
                PauseForScreenShot<TransitionSettingsUI.PredictionTab>("Transition Settings -Filter tab", 4);


                RunUI(() =>
                {
                    // Instrument Settings
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Instrument;
                    transitionSettingsUI.MinMz = 50;
                    transitionSettingsUI.MaxMz = 1500;
                    transitionSettingsUI.MZMatchTolerance = .055;
                    transitionSettingsUI.MinTime = null;
                    transitionSettingsUI.MaxTime = null;
                });
                PauseForScreenShot<TransitionSettingsUI.PredictionTab>("Transition Settings -Instrument tab", 4);

                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
                WaitForDocumentChange(docTargets);

                RunUI(() => SkylineWindow.SaveDocument(GetTestPath("EnergyMet_demo.sky")));


                // Export method - 2 minutes
                var exportMethodDlg2 = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));
                RunUI(() =>
                {
                    exportMethodDlg2.InstrumentType = ExportInstrumentType.WATERS_XEVO_TQ;
                    exportMethodDlg2.MethodType = ExportMethodType.Standard;
                    exportMethodDlg2.RunLength = 2;
                    exportMethodDlg2.OptimizeType = ExportOptimize.NONE;
                    exportMethodDlg2.SetTemplateFile("VerifyETemplate.exp");
                });
                PauseForScreenShot<ExportMethodDlg>("Exporting 2 minute method", 5);

                OkDialog(exportMethodDlg2, exportMethodDlg2.CancelDialog);
                WaitForClosedForm(exportMethodDlg2);


                // Export transition list
                var exportTransitionList = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                RunUI(() =>
                {
                    exportTransitionList.InstrumentType = ExportInstrumentType.WATERS_XEVO_TQ;
                    exportTransitionList.MethodType = ExportMethodType.Standard;
                    exportTransitionList.RunLength = 2;
                    exportTransitionList.OptimizeType = ExportOptimize.NONE;
                });
                PauseForScreenShot<ExportMethodDlg>("Exporting transition list", 6);
                OkDialog(exportTransitionList, exportTransitionList.CancelDialog);
                WaitForClosedForm(exportTransitionList);

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
                    PauseForScreenShot<ImportResultsSamplesDlg>("Import Results Files form", 7);
                    OkDialog(openDataSourceDialog1, openDataSourceDialog1.Open);
 
                    var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg1.OkDialog);
                    PauseForScreenShot<ImportResultsNameDlg>("Import Results common name form", 7);
                    OkDialog(importResultsNameDlg, importResultsNameDlg.OkDialog);

                    OkDialog(importResultsDlg1,importResultsDlg1.OkDialog);
                }

                SelectNode(SrmDocument.Level.Molecules, 0);
                SelectNode(SrmDocument.Level.MoleculeGroups, 0);

                PauseForScreenShot<SkylineWindow>("Skyline window multi-target graph", 8);

                // Renaming replicates
                var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
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
                    var newName = i == 0 ? "unscheduled_2min" : "unscheduled_5min";
                    RunUI(() =>
                    {
                        renameDlg.ReplicateName = newName;
                    });
                    if (i == 0)
                        PauseForScreenShot<SkylineWindow>("Renaming replicate", 10);
                    RunUI(() =>
                    {
                        renameDlg.OkDialog();
                    });
                    WaitForClosedForm(renameDlg);
                }
                RunUI(manageResultsDlg.OkDialog);
                WaitForClosedForm(manageResultsDlg);

                PauseForScreenShot<SkylineWindow>("Renamed", 10);

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
                RestoreViewOnScreen(9);
                SelectNode(SrmDocument.Level.MoleculeGroups, 0);
                PauseForScreenShot<SkylineWindow>("Skyline window multi-replicate layout", 10);

                // Set zoom to show better peak seperation in 5 minute run
                for (var index = 0; index < 2; index++)
                {
                    var indexChrom = index;
                    WaitForGraphs();
                    RunUI(() => SkylineWindow.GraphChromatograms.ToArray()[indexChrom].ZoomTo(.8, 1.8));
                    WaitForGraphs();
                }
                PauseForScreenShot<SkylineWindow>("Skyline window showing relative peak seperation", 12);

                // Set time window
                var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                RunUI(() =>
                {
                    peptideSettingsDlg.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                    peptideSettingsDlg.UseMeasuredRT(true);
                    peptideSettingsDlg.TimeWindow = 1;
                });
                PauseForScreenShot<PeptideSettingsUI>("Setting scheduled transition list time window", 12);
                RunUI(()=>peptideSettingsDlg.OkDialog());

                // Export transition list
                var exportTransitionList2 = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                RunUI(() =>
                {
                    exportTransitionList2.InstrumentType = ExportInstrumentType.WATERS_XEVO_TQ;
                    exportTransitionList2.MethodType = ExportMethodType.Scheduled;
                    exportTransitionList2.OptimizeType = ExportOptimize.NONE;
                });
                PauseForScreenShot<ExportMethodDlg>("Exporting scheduled transition list", 12);

                var schedulingOptionsDlg = ShowDialog<SchedulingOptionsDlg>(() => exportTransitionList2.OkDialog(GetTestPath("scheduled_5min.csv")));
                RunUI(() =>
                {
                    schedulingOptionsDlg.Algorithm = ExportSchedulingAlgorithm.Single;
                    schedulingOptionsDlg.ReplicateNum = 1;
                });
                PauseForScreenShot<SchedulingOptionsDlg>("Exporting scheduled transition list - choose replicate", 12);
                OkDialog(schedulingOptionsDlg, schedulingOptionsDlg.OkDialog);
                WaitForClosedForm(exportTransitionList2);

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
                    PauseForScreenShot<ImportResultsNameDlg>("Import Results common name form, not changing names", 14);
                    OkDialog(importResultsNameDlg, importResultsNameDlg.OkDialog);
                }

                // Remove 2 minute gradient
                // Renaming replicates
                var manageResultsDlg2 = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
                PauseForScreenShot<SkylineWindow>("Ready to remove", 20);

                RunUI(() =>
                {
                    doc = SkylineWindow.DocumentUI;
                    var chromatograms = doc.Settings.MeasuredResults.Chromatograms;
                    var chrom = chromatograms[0];
                    manageResultsDlg2.SelectedChromatograms = new[] { chrom };
                    manageResultsDlg2.RemoveReplicates();
                });
                for (var i = 1; i < 5; i++)
                {
                    var iC = i;
                    RunUI(() =>
                    {
                        doc = SkylineWindow.DocumentUI;
                        var chromatograms = doc.Settings.MeasuredResults.Chromatograms;
                        var chrom = chromatograms[iC];
                        manageResultsDlg2.SelectedChromatograms = new[] { chrom };
                    });

                    var renameDlg = ShowDialog<RenameResultDlg>(manageResultsDlg2.RenameResult);
                    string newName;
                    switch (i)
                    {
                        case 1:
                            newName = "1:1_1";
                            break;
                        case 2:
                            newName = "1:1_2";
                            break;
                        case 3:
                            newName = "2:1_2";
                            break;
                        default:
                            newName = "1:2_2";
                            break;
                    }
                    RunUI(() =>
                    {
                        renameDlg.ReplicateName = newName;
                        renameDlg.OkDialog();
                    });
                    WaitForClosedForm(renameDlg);
                }

                PauseForScreenShot<SkylineWindow>("Renaming replicates", 15);
                RunUI(manageResultsDlg2.OkDialog);
                WaitForClosedForm(manageResultsDlg2);

                SelectNode(SrmDocument.Level.Molecules, 0);
                PauseForScreenShot<SkylineWindow>("Inspecting ratios", 16);

                // Linearity

                var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
                RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates));
                
                IDictionary<string, Tuple<SampleType, double?>> sampleTypes =
                    new Dictionary<string, Tuple<SampleType, double?>> {
                        {"1:1_1", new Tuple<SampleType, double?>(SampleType.STANDARD,1)},
                        {"1:1_2", new Tuple<SampleType, double?>(SampleType.STANDARD,1)},
                        {"2:1_2", new Tuple<SampleType, double?>(SampleType.STANDARD,2)},
                        {"1:2_2", new Tuple<SampleType, double?>(SampleType.STANDARD,.5)},
                    };
                SetDocumentGridSampleTypesAndConcentrations(sampleTypes);
                PauseForScreenShot<DocumentGridForm>("Document Grid - sample types and concentrations ", 17);
                OkDialog(documentGrid, documentGrid.Close); // Hide the document grid

                using (new WaitDocumentChange(1, true))
                {
                    // Quant settings
                    var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);

                    RunUI(() =>
                    {
                        peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Quantification;
                        peptideSettingsUI.QuantRegressionFit = RegressionFit.LINEAR;
                        peptideSettingsUI.QuantNormalizationMethod =
                            new NormalizationMethod.RatioToLabel(IsotopeLabelType.heavy);
                        peptideSettingsUI.QuantRegressionWeighting = RegressionWeighting.NONE;
                        peptideSettingsUI.QuantMsLevel = null; // All
                        peptideSettingsUI.QuantUnits = "ratio to heavy";
                    });

                    PauseForScreenShot<PeptideSettingsUI.QuantificationTab>("Peptide Settings - Quantitation", 17);
                    OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
                }

                var calibrationForm = ShowDialog<CalibrationForm>(() => SkylineWindow.ShowCalibrationForm());
                PauseForScreenShot<CalibrationForm>("Calibration Curve ", 18);
                OkDialog(calibrationForm, calibrationForm.Close); // Hide the calibration window

                var transitionSettingsUI2 = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() =>
                {
                    // Predicition Settings
                    transitionSettingsUI2.SelectedTab = TransitionSettingsUI.TABS.Prediction;
                    transitionSettingsUI2.PrecursorMassType = MassType.Monoisotopic;
                    transitionSettingsUI2.FragmentMassType = MassType.Monoisotopic;
                    transitionSettingsUI2.RegressionCEName = "Waters Xevo"; // Collision Energy
                    transitionSettingsUI2.RegressionDPName =
                        Resources.SettingsList_ELEMENT_NONE_None; // Declustering Potential
                    transitionSettingsUI2.OptimizationLibraryName =
                        Resources.SettingsList_ELEMENT_NONE_None; // Optimization Library
                    transitionSettingsUI2.RegressionCOVName =
                        Resources.SettingsList_ELEMENT_NONE_None; // Compensation Voltage
                });

                var editCurrentCE = ShowDialog<EditCEDlg>(transitionSettingsUI2.EditCECurrent); 
                RunUI(() =>
                {
                    editCurrentCE.StepSize = 2;
                    editCurrentCE.StepCount = 5;
                });
                PauseForScreenShot<EditCEDlg>("Edit Collision Energy Equation form", 18);

                RunUI(() =>
                {
                    editCurrentCE.OkDialog();
                    transitionSettingsUI2.UseOptimized = true;
                    transitionSettingsUI2.OptimizeType = OptimizedMethodType.Transition.GetLocalizedString();
                });
                PauseForScreenShot<TransitionSettingsUI.PredictionTab>("Transition Settings - Prediction tab", 18);
                RunUI(transitionSettingsUI2.OkDialog);

                // Export transition lists
                var exportTransitionList3 = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                RunUI(() =>
                {
                    exportTransitionList3.ExportStrategy = ExportStrategy.Buckets;
                    exportTransitionList3.IgnoreProteins = true;
                    exportTransitionList3.MaxTransitions = 100;
                    exportTransitionList3.InstrumentType = ExportInstrumentType.WATERS_XEVO_TQ;
                    exportTransitionList3.MethodType = ExportMethodType.Scheduled;
                    exportTransitionList3.OptimizeType = ExportOptimize.CE;
                });
                PauseForScreenShot<ExportMethodDlg>("Exporting scheduled transition list", 19);

                var schedDlg = ShowDialog<SchedulingOptionsDlg>(() => exportTransitionList3.OkDialog(GetTestPath("TL_CE_Opt")));
                PauseForScreenShot<SchedulingOptionsDlg>("Scheduling", 19);
                RunUI(schedDlg.OkDialog);
                WaitForClosedForm(exportTransitionList3);

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
                    var openDataSourceDialog1 = ShowDialog<OpenDataSourceDialog>(() => importResultsDlg1.OkDialog());
                    RunUI(() =>
                    {
                        openDataSourceDialog1.CurrentDirectory = new MsDataFilePath(GetTestPath("CE Optimization"));
                        openDataSourceDialog1.SelectAllFileType(ExtWatersRaw);
                    });
                    PauseForScreenShot<ImportResultsSamplesDlg>("Import Results Files form", 20);
                    OkDialog(openDataSourceDialog1, openDataSourceDialog1.Open);

                    RunUI(() =>
                    {
                        SkylineWindow.ShowSingleTransition();
                        SkylineWindow.ShowSplitChromatogramGraph(true);
                    });
                    PauseForScreenShot<SkylineWindow>("Split graph", 21);

                    RunUI(() =>
                    {
                        SkylineWindow.ShowPeakAreaLegend(false);
                    });
                    PauseForScreenShot<SkylineWindow>("No legend", 24);

                    // Show Pentose-P
                    SelectNode(SrmDocument.Level.Molecules, 6);
                    PauseForScreenShot<SkylineWindow>("Pentose-P", 24);

                    // Export final transition list
                    var exportTransitionList4 = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                    RunUI(() =>
                    {
                        exportTransitionList4.ExportStrategy = ExportStrategy.Single;
                        exportTransitionList4.IgnoreProteins = true;
                        exportTransitionList4.MaxTransitions = 100;
                        exportTransitionList4.InstrumentType = ExportInstrumentType.WATERS_XEVO_TQ;
                        exportTransitionList4.MethodType = ExportMethodType.Scheduled;
                        exportTransitionList4.OptimizeType = ExportOptimize.NONE;
                    });
                    PauseForScreenShot<ExportMethodDlg>("Exporting final optimized transition list", 25);

                    var schedDlg2 = ShowDialog<SchedulingOptionsDlg>(() => exportTransitionList4.OkDialog(GetTestPath("TL_CE_Final.csv")));
                    PauseForScreenShot<SchedulingOptionsDlg>("Final Scheduling", 26);
                    RunUI(schedDlg2.OkDialog);

                }

            }

        }
    }
}
