/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
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
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.BiblioSpec;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the tutorial for MS1 Full-Scan Filtering
    /// </summary>
    [TestClass]
    public class Ms1FullScanFilteringTutorial : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMs1Tutorial()
        {
            TestFilesZip = ExtensionTestContext.CanImportAbWiff
                ? @"https://skyline.gs.washington.edu/tutorials/MS1Filtering.zip" // Not L10N
                : @"https://skyline.gs.washington.edu/tutorials/MS1FilteringMzml.zip"; // Not L10N
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var folderMs1Filtering = ExtensionTestContext.CanImportAbWiff ? "Ms1Filtering" : "Ms1FilteringMzml"; // Not L10N

            SrmDocument doc = SkylineWindow.Document;

            // Configure the peptide settings for your new document.
            var peptideSettingsUI = ShowPeptideSettings();
            const string carbamidomethylCysteineName = "Carbamidomethyl Cysteine"; // Not L10N
            const string phosphoStName = "Phospho (ST)"; // Not L10N
            const string phosphoYName = "Phospho (Y)"; // Not L10N
            const string oxidationMName = "Oxidation (M)"; // Not L10N
            AddStaticMod(phosphoStName, true, peptideSettingsUI);
            AddStaticMod(phosphoYName, true, peptideSettingsUI);
            AddStaticMod(oxidationMName, true, peptideSettingsUI);
            RunUI(() =>
            {
                peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Modifications;
                peptideSettingsUI.PickedStaticMods = new[] {carbamidomethylCysteineName, phosphoStName, phosphoYName, oxidationMName};
                peptideSettingsUI.MissedCleavages = 2;
            });
            PauseForScreenShot();   // p. 3

            // Build Spectral Library.
            const string libraryName = "Phospho_TiO2"; // Not L10N
            string libraryPath = TestFilesDir.GetTestPath(libraryName + ".blib"); // Not L10N
            string redundantLibraryPath = TestFilesDir.GetTestPath(libraryName + ".redundant.blib"); // Not L10N
            {
                var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettingsUI.ShowBuildLibraryDlg);
                RunUI(() =>
                {
                    buildLibraryDlg.LibraryName = libraryName;
                    buildLibraryDlg.LibraryPath = libraryPath;
                    buildLibraryDlg.LibraryKeepRedundant = true;
                    buildLibraryDlg.LibraryBuildAction = LibraryBuildAction.Create;
                    buildLibraryDlg.LibraryCutoff = 0.95;
                    buildLibraryDlg.LibraryAuthority = "buckinstitute.org"; // Not L10N
                });
                PauseForScreenShot();   // p. 4

                RunUI(() =>
                {
                    buildLibraryDlg.OkWizardPage();
                    IList<string> inputPaths = new List<string>
                     {
                         TestFilesDir.GetTestPath(folderMs1Filtering + @"\100803_0005b_MCF7_TiTip3.group.xml") // Not L10N
                     };
                    buildLibraryDlg.AddInputFiles(inputPaths);
                });
                PauseForScreenShot();   // p. 5

                OkDialog(buildLibraryDlg, buildLibraryDlg.OkWizardPage);
            }
            Assert.IsTrue(WaitForConditionUI(() =>
                peptideSettingsUI.AvailableLibraries.Contains(libraryName)));
            RunUI(() =>
            {
                peptideSettingsUI.PickedLibraries = new[] {libraryName};
            });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            WaitForDocumentChange(doc);

            // Check library existence and loading.
            WaitForCondition(() => File.Exists(libraryPath) && File.Exists(redundantLibraryPath));
            WaitForCondition(() =>
            {
                var librarySettings = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
                return librarySettings.IsLoaded &&
                       librarySettings.Libraries.Count > 0;
            });

            // Verify library retention time information
            {
                var viewLibraryDlg = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
                var modMatchDlg = WaitForOpenForm<MultiButtonMsgDlg>();
                RunUI(() =>
                          {
                              modMatchDlg.BtnCancelClick();
                              Assert.AreEqual("100803_0005b_MCF7_TiTip3.wiff", viewLibraryDlg.SourceFile);
                              Assert.AreEqual(35.2128, viewLibraryDlg.RetentionTime, 0.00005);
                          });
                WaitForClosedForm(modMatchDlg);
                PauseForScreenShot();   // p. 6

                OkDialog(viewLibraryDlg, viewLibraryDlg.CancelDialog);
            }

            // Configuring appropriate transition settings and configuring full-scan settings for
            // MS1 chromatogram extraction.
            doc = SkylineWindow.Document;
            {
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() =>
                {
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Filter;
                    Assert.AreEqual(MassType.Monoisotopic, transitionSettingsUI.PrecursorMassType);
                    transitionSettingsUI.PrecursorCharges = "2, 3, 4"; // Not L10N
                    transitionSettingsUI.ProductCharges = "1, 2, 3"; // Not L10N
                    transitionSettingsUI.FragmentTypes = "p"; // Not L10N
                    transitionSettingsUI.SetAutoSelect = true;
                });
                PauseForScreenShot();   // p. 8

                RunUI(() =>
                {
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Library;
                    transitionSettingsUI.UseLibraryPick = false;
                });
                PauseForScreenShot();   // p. 9

                RunUI(() =>
                {
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                    transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                    transitionSettingsUI.Peaks = "3";
                    Assert.AreEqual(MassType.Monoisotopic, transitionSettingsUI.PrecursorMassType);
                });
                PauseForScreenShot();   // p. 11

                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            }
            WaitForDocumentChangeLoaded(doc);

            // Populating the Skyline peptide tree.
            doc = SkylineWindow.Document;
            string fastaPath = TestFilesDir.GetTestPath(folderMs1Filtering + @"\12_proteins.062011.fasta");
            RunUI(() => SkylineWindow.ImportFastaFile(fastaPath)); // Not L10N
            WaitForDocumentChange(doc);
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 11, 40, 40, 120);

            // Select the first transition group.
            var documentPath = TestFilesDir.GetTestPath(folderMs1Filtering + @"\Template_MS1 Filtering_1118_2011_3.sky"); // Not L10N
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedPath =
                    SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Peptides, 0);
                SkylineWindow.GraphSpectrumSettings.ShowAIons = true;
                SkylineWindow.GraphSpectrumSettings.ShowBIons = true;
                SkylineWindow.GraphSpectrumSettings.ShowYIons = true;
                SkylineWindow.GraphSpectrumSettings.ShowPrecursorIon = true;
                SkylineWindow.ExpandPrecursors();
                SkylineWindow.SaveDocument(documentPath);
            });
            WaitForCondition(() => File.Exists(documentPath));
            PauseForScreenShot();   // p. 12

            // MS1 filtering of raw data imported into Skyline.
            doc = SkylineWindow.Document;
            ImportResultsFile("100803_0005b_MCF7_TiTip3" + ExtensionTestContext.ExtAbWiff); // Not L10N
            WaitForDocumentChange(doc); 

            doc = SkylineWindow.Document;
            RunUI(() =>
            {
                SkylineWindow.IntegrateAll();
                SkylineWindow.ShowGraphPeakArea(true);
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.none);
                Settings.Default.ShowDotProductPeakArea = true;
                Settings.Default.ShowLibraryPeakArea = true;
                SkylineWindow.AutoZoomNone();
            });
            WaitForDocumentChange(doc);
            PauseForScreenShot();   // p. 14 & 15

            RunUI(SkylineWindow.AutoZoomNone);
            PauseForScreenShot();   // p. 16

            // Jump to another peptide.
            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findDlg =>
            {
                findDlg.FindOptions = new FindOptions().ChangeText("YGP"); // Not L10N
                findDlg.FindNext();
                findDlg.Close();
            });
            PauseForScreenShot();   // p. 17, figure 1

            RunUI(SkylineWindow.AutoZoomBestPeak);
            PauseForScreenShot();   // p. 17, figure 2; p. 18, figure 1 & 2

            // Limiting the chromatogram extraction time range.
            doc = SkylineWindow.Document;
            {
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() =>
                {
                    transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Instrument;
                    transitionSettingsUI.MinTime = "10"; // Not L10N
                    transitionSettingsUI.MaxTime = "100"; // Not L10N
                });
                PauseForScreenShot();   // p. 20

                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            }
            WaitForDocumentChange(doc);

            // Re-importing raw data.
            doc = SkylineWindow.Document;
            {
                var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
                RunUI(manageResultsDlg.ReimportResults);
                PauseForScreenShot();

                OkDialog(manageResultsDlg, manageResultsDlg.OkDialog);
            }
            SrmDocument docAfter = WaitForDocumentChangeLoaded(doc, 5*60*1000); // 5 minutes
            AssertEx.IsDocumentState(docAfter, null, 11, 40, 40, 120);

            RunUI(SkylineWindow.AutoZoomNone);

            // Minimizing a chromatogram cache file.
            RunUI(SkylineWindow.CollapsePeptides);
            for (int i = 0; i < 5; i++) // just do the first 5
            {
                int iPeptide = i;
                var path = docAfter.GetPathTo((int) SrmDocument.Level.Peptides, iPeptide);
                RunUI(() =>
                {
                    SkylineWindow.SelectedPath = path;
                });
                WaitForGraphs();
            }

            // Eliminate extraneous chromatogram data.
            doc = SkylineWindow.Document;
            var minimizedFile = TestFilesDir.GetTestPath(folderMs1Filtering + @"\Template_MS1Filtering_1118_2011_3-2min.sky"); // Not L10N
            var cacheFile = minimizedFile + "d"; // Not L10N
            {
                var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
                var minimizeResultsDlg = ShowDialog<MinimizeResultsDlg>(manageResultsDlg.MinimizeResults);
                RunUI(() =>
                {
                    minimizeResultsDlg.LimitNoiseTime = true;
                    minimizeResultsDlg.NoiseTimeRange = "2"; // Not L10N
                });
                PauseForScreenShot();   // p. 23

                OkDialog(minimizeResultsDlg, () => minimizeResultsDlg.MinimizeToFile(minimizedFile));
                WaitForCondition(() => File.Exists(cacheFile));
                WaitForClosedForm(manageResultsDlg);
            }
            WaitForDocumentChange(doc);

            // Inclusion list method export for MS1 filtering
            doc = SkylineWindow.Document;
            RunDlg<PeptideSettingsUI>(() => SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction), dlg =>
            {
                dlg.UseMeasuredRT(true);
                dlg.TimeWindow = 10;
                dlg.OkDialog();
            });
            WaitForDocumentChangeLoaded(doc);

            // Now deviating from the tutorial script for a moment to make sure we can choose a Scheduled export method.
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, dlg =>
            {
                dlg.MinPeptides = "1"; // Not L10N
                const double minPeakFoundRatio = 0.1;
                dlg.MinPeakFoundRatio = minPeakFoundRatio.ToString(CultureInfo.CurrentCulture);
                dlg.OkDialog();
            });

            // Ready to export, although we will just cancel out of the dialog.
            var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));
            RunUI(() =>
            {
                exportMethodDlg.InstrumentType = "AB SCIEX TOF"; // Not L10N
                exportMethodDlg.MethodType = ExportMethodType.Scheduled;
                exportMethodDlg.CancelButton.PerformClick();
            });
            WaitForClosedForm(exportMethodDlg);
        }
    }
}
