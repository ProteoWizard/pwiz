/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford University
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class DiaTutorialTest : AbstractFunctionalTest
    {
        private readonly string[] _importFiles =
            {
                "20130311_DIA_Pit01",
                "20130311_DIA_Pit02"
            };

        private const string DIA_START_CHECKPOINT = "DIABlank.sky";
        private const string DIA_SETUP_CHECKPOINT = "DIASetup.sky";
        private const string DIA_IMPORTED_CHECKPOINT = "DIAImported.sky";

        [TestMethod]
        public void TestDiaTutorial()
        {
            // Set true to look at tutorial screenshots.
            // IsPauseForScreenShots = true;

            LinkPdf = "https://skyline.gs.washington.edu/labkey/_webdav/home/software/Skyline/%40files/tutorials/Dia_2-6.pdf";

            TestFilesZipPaths = new[]
                {
                    // There is a small and a large version of the tutorial data.
                    // Large version contains 1 DDA and 2 DIA runs for 6 GB.
                    // Small version runs by default, skips some steps.
                    IsFullImportMode ? @"https://skyline.gs.washington.edu/tutorials/DIA.zip" :
                                       @"https://skyline.gs.washington.edu/tutorials/DIASmall.zip",
                    @"TestTutorial\DiaViews.zip"
                };
            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            string folderTutorial = IsFullImportMode ? "Dia" : "DiaSmall" ; // Not L10N
            return TestFilesDirs[0].GetTestPath(Path.Combine(folderTutorial, relativePath));
        }

        /// <summary>
        /// Change to true to run full import of DIA data.  Also used
        /// to regenerate checkpoint files for non-full-import mode,
        /// when something changes in the test.
        /// </summary>
        private bool IsFullImportMode { get { return false; } }

        protected override void DoTest()
        {
            // Clear all the settings lists that will be defined in this tutorial
            ClearSettingsLists();

            // Open the file
            RunUI(() => SkylineWindow.OpenFile(GetTestPath(DIA_START_CHECKPOINT))); 
            WaitForDocumentLoaded();

            // Build spectral library
            var peptideSettings = ShowDialog<PeptideSettingsUI>(() => SkylineWindow.ShowPeptideSettingsUI());
            RunUI(() => peptideSettings.SelectedTab = PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            PauseForScreenShot<BuildLibraryDlg>("Build Library Dialog", 1);
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = "Dia_Tutorial_Library"; // Not L10N
                buildLibraryDlg.LibraryAuthority = "proteome.gs.washington.edu"; // Not L10N
                buildLibraryDlg.LibraryPath = GetTestPath("Dia_Tutorial_Library.blib");
                buildLibraryDlg.OkWizardPage();
            });
            RunUI(() =>
            {
                var ddaFiles = new[]
                {
                    "interact-20130311_DDA_Pit01.pep.xml", // Not L10N
                };
                var ddaFilesFull = ddaFiles.Select(GetTestPath);
                buildLibraryDlg.AddInputFiles(ddaFilesFull);
            });
            PauseForScreenShot<BuildLibraryDlg>("Add files dialog", 2);
            if (IsFullImportMode)
            {
                RunUI(buildLibraryDlg.OkWizardPage);

                WaitForConditionUI(() => peptideSettings.AvailableLibraries.Length > 0);
                RunUI(() =>
                {
                    peptideSettings.PickedLibraries = peptideSettings.AvailableLibraries;
                    Assert.AreEqual(peptideSettings.PickedLibraries.Length, 1);
                    Assert.AreEqual(peptideSettings.PickedLibraries[0], "Dia_Tutorial_Library"); // Not L10N
                });
                PauseForScreenShot<PeptideSettingsUI.LibraryTab>("Peptide Settings after Building Library", 3);
                OkDialog(peptideSettings, peptideSettings.OkDialog);
                WaitForConditionUI(() => SkylineWindow.Document.Settings.PeptideSettings.Libraries.IsLoaded);
                RunUI(() =>
                {
                    SkylineWindow.SaveDocument(GetTestPath(DIA_SETUP_CHECKPOINT));
                    SkylineWindow.SaveDocument(GetTestPath(DIA_START_CHECKPOINT));
                });
            }
            else
            {
                OkDialog(buildLibraryDlg, buildLibraryDlg.CancelDialog);
                OkDialog(peptideSettings, peptideSettings.OkDialog);
                RunUI(() => SkylineWindow.OpenFile(GetTestPath(DIA_SETUP_CHECKPOINT)));
            }
            WaitForDocumentLoaded();

            // Specify DIA acquisition scheme and machine settings
            var transitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettings.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                transitionSettings.AcquisitionMethod = FullScanAcquisitionMethod.DIA;
                transitionSettings.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                transitionSettings.PrecursorMassAnalyzer = FullScanMassAnalyzerType.orbitrap;
                transitionSettings.PrecursorRes = 35000;
                transitionSettings.PrecursorResMz = 200;
                transitionSettings.ProductMassAnalyzer = FullScanMassAnalyzerType.orbitrap;
                transitionSettings.ProductRes = 17500;
                transitionSettings.ProductResMz = 200;
            });
            PauseForScreenShot<TransitionSettingsUI.FullScanTab>("Full Scan Settings for DIA", 4);

            // Set up isolation scheme
            var isolationSchemeDlg = ShowDialog<EditIsolationSchemeDlg>(transitionSettings.AddIsolationScheme);
            RunUI(() =>
            {
                isolationSchemeDlg.UseResults = false;
            });
            PauseForScreenShot<EditIsolationSchemeDlg>("Edit Isolation Scheme Dialog", 5);
            
            var calculateIsolationDlg = ShowDialog<CalculateIsolationSchemeDlg>(isolationSchemeDlg.Calculate);
            RunUI(() =>
            {
                calculateIsolationDlg.WindowWidth = 20;
                calculateIsolationDlg.Start = 500;
                calculateIsolationDlg.End = 900;
            });
            PauseForScreenShot<CalculateIsolationSchemeDlg>("Calculate Isolation Scheme Dialog", 6);
            OkDialog(calculateIsolationDlg, calculateIsolationDlg.OkDialog);
            PauseForScreenShot<EditIsolationSchemeDlg>("Edit Isolation Scheme Dialog Filled", 7);

            var isolationSchemeGraphDlg = ShowDialog<DiaIsolationWindowsGraphForm>(isolationSchemeDlg.OpenGraph);
            PauseForScreenShot<DiaIsolationWindowsGraphForm>("Graph of Isolation Scheme", 8);
            OkDialog(isolationSchemeGraphDlg, isolationSchemeGraphDlg.CloseButton);
            RunUI(() => isolationSchemeDlg.IsolationSchemeName = "DIA tutorial isolation"); // Not L10N
            OkDialog(isolationSchemeDlg, isolationSchemeDlg.OkDialog);
            OkDialog(transitionSettings, transitionSettings.OkDialog);
            
            // Export isolation scheme
            var exportIsolationDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList));
            PauseForScreenShot<ExportMethodDlg>("Export Isolation Scheme", 9);
            OkDialog(exportIsolationDlg, () => exportIsolationDlg.OkDialog(GetTestPath("DIA_tutorial_isolation_list.csv")));
            
            // Set up chromatogram retention time restriction
            var newTransitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() => newTransitionSettings.SetRetentionTimeFilter(RetentionTimeFilterType.ms2_ids, 5.0));
            PauseForScreenShot<TransitionSettingsUI.FullScanTab>("Retention time filtering options", 10);
            OkDialog(newTransitionSettings, newTransitionSettings.OkDialog);

            // Adjust modifications and filter
            var newPeptideSettings = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                newPeptideSettings.AutoSelectMatchingPeptides = true;
                newPeptideSettings.SelectedTab = PeptideSettingsUI.TABS.Modifications;
            });
            var editModificationsDlg = ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(newPeptideSettings.EditStaticMods);
            var addModificationDlg = ShowDialog<EditStaticModDlg>(editModificationsDlg.AddItem);
            const string carbamidoMod = StaticModList.DEFAULT_NAME;
            RunUI(() => addModificationDlg.SetModification(carbamidoMod)); // Not L10N
            PauseForScreenShot<EditStaticModDlg>("Add fixed modification", 12);
            OkDialog(addModificationDlg, addModificationDlg.OkDialog);
            OkDialog(editModificationsDlg, editModificationsDlg.OkDialog);
            RunUI(() =>
            {
                newPeptideSettings.PickedStaticMods = new[] {carbamidoMod};
            });
            PauseForScreenShot<PeptideSettingsUI.ModificationsTab>("Add Carbamidomethyl modification", 13);
            OkDialog(newPeptideSettings, newPeptideSettings.OkDialog);

            var filterTransitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                filterTransitionSettings.SelectedTab = TransitionSettingsUI.TABS.Filter;
                filterTransitionSettings.PrecursorCharges = "1, 2, 3, 4"; // Not L10N
                filterTransitionSettings.ProductCharges = "1, 2"; // Not L10N
                filterTransitionSettings.FragmentTypes = "b, y, p"; // Not L10N
                filterTransitionSettings.SetAutoSelect = true;
                filterTransitionSettings.SetDIAExclusionWindow = true;
            });
            PauseForScreenShot<TransitionSettingsUI.FilterTab>("Transition filter settings", 14);
            OkDialog(filterTransitionSettings, filterTransitionSettings.OkDialog);

            // Import .fasta file with 30 peptides
            RunUI(() => SkylineWindow.ImportFastaFile(GetTestPath("pituitary_database.fasta")));
            PauseForScreenShot("Peptides and transitions for imported FASTA file", 15);

            // Adjust library transition ranking
            var libraryTransitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                libraryTransitionSettings.SelectedTab = TransitionSettingsUI.TABS.Library;
                libraryTransitionSettings.UseLibraryPick = true;
                libraryTransitionSettings.IonCount = 5;
                libraryTransitionSettings.Filtered = true;
            });
            PauseForScreenShot<TransitionSettingsUI.LibraryTab>("Picking best transitions from library spectra", 16);
            OkDialog(libraryTransitionSettings, libraryTransitionSettings.OkDialog);
            PauseForScreenShot("Best 5 transitions only", 17);

            // Generate decoys
            var decoysDlg = ShowDialog<GenerateDecoysDlg>(SkylineWindow.ShowGenerateDecoysDlg);
            PauseForScreenShot<GenerateDecoysDlg>("Generate decoys", 18);
            RunUI(() =>
            {
                decoysDlg.NumDecoys = 26;
                Assert.AreEqual(decoysDlg.DecoysMethod, DecoyGeneration.SHUFFLE_SEQUENCE);
            });
            OkDialog(decoysDlg, decoysDlg.OkDialog);
            PauseForScreenShot("Decoys added to document", 19);

            // Import mass spec data
            if (IsFullImportMode)
            {
                // Import the raw data
                var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
                RunUI(() =>
                {
                    importResultsDlg.RadioAddNewChecked = true;
                    var path = new KeyValuePair<string, MsDataFileUri[]>[2];
                    for (int i = 0; i < 2; ++i)
                    {
                        path[i] = new KeyValuePair<string, MsDataFileUri[]>(_importFiles[i],
                                                new[] { MsDataFileUri.Parse(GetTestPath(_importFiles[i] + ExtensionTestContext.ExtThermoRaw)) });
                    }

                    importResultsDlg.NamedPathSets = path;
                });
                var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg.OkDialog);
                PauseForScreenShot<ImportResultsNameDlg>("Import results common prefix", 20);
                RunUI(() =>
                {
                    string prefix = importResultsNameDlg.Prefix;
                    importResultsNameDlg.Prefix = prefix.Substring(0, prefix.Length - 1);
                    importResultsNameDlg.YesDialog();
                });
                WaitForClosedForm(importResultsNameDlg);
                WaitForClosedForm(importResultsDlg);
                WaitForCondition(5 * 60 * 1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);    // 5 minutes
                RunUI(() =>
                {
                    SkylineWindow.SaveDocument(GetTestPath(DIA_IMPORTED_CHECKPOINT));
                    SkylineWindow.SaveDocument(GetTestPath(DIA_START_CHECKPOINT));
                });
            }
            else
            {
                RunUI(() => SkylineWindow.OpenFile(GetTestPath(DIA_IMPORTED_CHECKPOINT)));
            }
            WaitForDocumentLoaded();
            PauseForScreenShot("testing");

            // Explore the results

            // Fit a custom peak scoring model

            // Export results

            // Clear all the settings lists that were defined in this tutorial
            ClearSettingsLists();
        }

        /// <summary>
        /// Clears all the relevant settings lists
        /// </summary>
        private static void ClearSettingsLists()
        {
            Settings.Default.PeakScoringModelList.Clear();
            Settings.Default.IsolationSchemeList.Clear();
            Settings.Default.HeavyModList.Clear();
            Settings.Default.StaticModList.Clear();
            Settings.Default.SpectralLibraryList.Clear();
        }
    }
}
