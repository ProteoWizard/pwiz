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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.BiblioSpec;
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
                ? @"https://skyline.gs.washington.edu/tutorials/MS1Filtering.zip"
                : @"https://skyline.gs.washington.edu/tutorials/MS1FilteringMzml.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var folderMs1Filtering = ExtensionTestContext.CanImportAbWiff ? "Ms1Filtering" : "Ms1FilteringMzml";

            SrmDocument doc = SkylineWindow.Document;

            // Configure the peptide settings for your new document.
            var peptideSettingsUI = ShowPeptideSettings();
            const string carbamidomethylCysteineName = "Carbamidomethyl Cysteine";
            const string phosphoStName = "Phospho (ST)";
            const string phosphoYName = "Phospho (Y)";
            const string oxidationMName = "Oxidation (M)";
            AddStaticMod(phosphoStName, true, peptideSettingsUI);
            AddStaticMod(phosphoYName, true, peptideSettingsUI);
            AddStaticMod(oxidationMName, true, peptideSettingsUI);
            RunUI(() =>
            {
                peptideSettingsUI.PickedStaticMods = new[] {carbamidomethylCysteineName, phosphoStName, phosphoYName, oxidationMName};
                peptideSettingsUI.MissedCleavages = 2;
            });

            // Build Spectral Library.
            const string libraryName = "Phospho_TiO2";
            string libraryPath = TestFilesDir.GetTestPath(libraryName + ".blib");
            string redundantLibraryPath = TestFilesDir.GetTestPath(libraryName + ".redundant.blib");
            RunDlg<BuildLibraryDlg>(peptideSettingsUI.ShowBuildLibraryDlg, buildLibraryDlg =>
            {
                buildLibraryDlg.LibraryName = libraryName;
                buildLibraryDlg.LibraryPath = libraryPath;
                buildLibraryDlg.LibraryKeepRedundant = true;
                buildLibraryDlg.LibraryBuildAction = LibraryBuildAction.Create;
                buildLibraryDlg.LibraryCutoff = 0.95;
                buildLibraryDlg.LibraryAuthority = "buckinstitute.org";
                buildLibraryDlg.OkWizardPage();
                IList<string> inputPaths = new List<string>
                 {
                     TestFilesDir.GetTestPath(folderMs1Filtering + @"\100803_0005b_MCF7_TiTip3.group.xml")
                 };
                buildLibraryDlg.AddInputFiles(inputPaths);
                buildLibraryDlg.OkWizardPage();
            });
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

            // Configuring appropriate transition settings and configuring full-scan settings for
            // MS1 chromatogram extraction.
            doc = SkylineWindow.Document;
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI =>
            {
                Assert.AreEqual(MassType.Monoisotopic, transitionSettingsUI.PrecursorMassType);
                transitionSettingsUI.PrecursorCharges = "2,3,4";
                transitionSettingsUI.ProductCharges = "1,2,3";
                transitionSettingsUI.FragmentTypes = "p";
                transitionSettingsUI.SetAutoSelect = true;
                transitionSettingsUI.UseLibraryPick = false;
                transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                transitionSettingsUI.Peaks = "3";
                Assert.AreEqual(MassType.Monoisotopic, transitionSettingsUI.PrecursorMassType);
                transitionSettingsUI.OkDialog();
            });
            WaitForDocumentChangeLoaded(doc);

            // Populating the Skyline peptide tree.
            doc = SkylineWindow.Document;
            RunUI(
                () => SkylineWindow.ImportFastaFile(TestFilesDir.GetTestPath(folderMs1Filtering + @"\12_proteins.062011.fasta")));
            WaitForDocumentChange(doc);
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 11, 40, 40, 120);

            // Select the first transition group.
            var documentPath = TestFilesDir.GetTestPath(folderMs1Filtering + @"\Template_MS1 Filtering_1118_2011_3.sky");
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedPath =
                    SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.TransitionGroups, 0);
                SkylineWindow.GraphSpectrumSettings.ShowAIons = true;
                SkylineWindow.GraphSpectrumSettings.ShowBIons = true;
                SkylineWindow.GraphSpectrumSettings.ShowYIons = true;
                SkylineWindow.GraphSpectrumSettings.ShowPrecursorIon = true;
                SkylineWindow.ExpandPrecursors();
                SkylineWindow.SaveDocument(documentPath);
            });
            WaitForCondition(() => File.Exists(documentPath));

            // MS1 filtering of raw data imported into Skyline.
            doc = SkylineWindow.Document;
            ImportResultsFile("100803_0005b_MCF7_TiTip3" + ExtensionTestContext.ExtAbWiff);
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

            // Jump to another peptide.
            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findDlg =>
            {
                findDlg.FindOptions = new FindOptions().ChangeText("YGP");
                findDlg.FindNext();
                findDlg.Close();
            });

            // Limiting the chromatogram extraction time range.
            doc = SkylineWindow.Document;
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI =>
            {
                transitionSettingsUI.MinTime = "10";
                transitionSettingsUI.MaxTime = "100";
                transitionSettingsUI.OkDialog();
            });
            WaitForDocumentChange(doc);

            // Re-importing raw data.
            doc = SkylineWindow.Document;
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                dlg.ReimportResults();
                dlg.OkDialog();
            });
            SrmDocument docAfter = WaitForDocumentChangeLoaded(doc);
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
            var minimizedFile = TestFilesDir.GetTestPath(folderMs1Filtering + @"\Template_MS1Filtering_1118_2011_3-2min.sky");
            var cacheFile = minimizedFile + "d";
            var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            RunDlg<MinimizeResultsDlg>(manageResultsDlg.MinimizeResults, dlg =>
            {
                dlg.LimitNoiseTime = true;
                dlg.NoiseTimeRange = "2";
                dlg.MinimizeToFile(minimizedFile);
            });
            WaitForCondition(() => File.Exists(cacheFile));
            WaitForClosedForm(manageResultsDlg);
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
                dlg.MinPeptides = "1";
                dlg.MinPeakFoundRatio = "0.1";
                dlg.OkDialog();
            });

            // Ready to export, although we will just cancel out of the dialog.
            var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));
            RunUI(() =>
            {
                exportMethodDlg.InstrumentType = "AB SCIEX TOF";
                exportMethodDlg.MethodType = ExportMethodType.Scheduled;
                exportMethodDlg.CancelButton.PerformClick();
            });
            WaitForClosedForm(exportMethodDlg);
        }
    }
}
