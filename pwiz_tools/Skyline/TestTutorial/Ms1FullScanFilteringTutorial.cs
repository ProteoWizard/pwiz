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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;
using pwiz.Skyline.Util;

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
            // Set true to look at tutorial screenshots.
            //IsPauseForScreenShots = true;

            // Lest we get "To export a scheduled method, you must first choose a retention time predictor in Peptide Settings / Prediction, or import results for all peptides in the document."
            TestSmallMolecules = false;

            LinkPdf = "https://skyline.gs.washington.edu/labkey/_webdav/home/software/Skyline/%40files/tutorials/MS1Filtering-2_5.pdf";

            TestFilesZipPaths = new[]
                {
                    PreferWiff
                        ? @"https://skyline.gs.washington.edu/tutorials/MS1Filtering_2.zip" // Not L10N
                        : @"https://skyline.gs.washington.edu/tutorials/MS1FilteringMzml_2.zip", // Not L10N
                    @"TestTutorial\Ms1FullScanFilteringViews.zip"
                };
            RunFunctionalTest();
        }

        /// <summary>
        /// Change to true to write annotation value arrays to console
        /// </summary>
        private bool IsRecordMode { get { return false; }}

        private readonly string[] EXPECTED_ANNOTATIONS =
            {
                "35.7;-34.5 ppm|36.6;-2.8 ppm|32.6;-8.2 ppm|33.2;-1.8 ppm|34.1;+17.2 ppm|37.5;+22.3 ppm|38.5;+5.7 ppm|39.1;-5.5 ppm", // Not L10N
                "39.0;-33.2 ppm;(idotp 0.90)|34.1;-25 ppm|34.6;-21.5 ppm|35.7;-48.1 ppm|36.1;-42.9 ppm;(idotp 0.97)|37.8;-9.2 ppm;(idotp 0.96)|37.8;-27.3 ppm|36.5;-52.7 ppm|40.9;+59.7 ppm", // Not L10N
                "37.0;-10.8 ppm|32.4;-12.2 ppm|35.2;+8.3 ppm|36.6;-10.4 ppm|37.5;-6.2 ppm|39.1;+7.1 ppm|40.5;-38.3 ppm|40.7;-38.2 ppm|42.0;+11.2 ppm|33.1;+39.9 ppm", // Not L10N
                "37.0;-10.8 ppm|32.4;-12.2 ppm|35.2;+8.3 ppm|36.6;-10.4 ppm|37.5;-6.2 ppm|39.1;+7.1 ppm|40.5;-38.3 ppm|40.7;-38.2 ppm|42.0;+11.2 ppm|33.1;+39.9 ppm", // Not L10N
                "37.0;-9.4 ppm|32.4;-12.2 ppm|35.2;+8.3 ppm|39.1;+7.1 ppm|40.5;-38.3 ppm|40.7;-38.2 ppm|42.0;+11.2 ppm|33.1;+39.9 ppm", // Not L10N
                "37.0;-10.8 ppm|36.6;-10.4 ppm|37.5;-6.2 ppm|40.5;-38.3 ppm|40.7;-38.2 ppm|42.0;+11.2 ppm|33.1;+39.9 ppm|32.2;+17.4 ppm|34.6;-41.3 ppm|39.7;+3 ppm", // Not L10N
                "37.4;+2.6 ppm|40.8;-20.9 ppm|33.2;+48 ppm|34.9;-41.1 ppm", // Not L10N
                "37.5;-33.7 ppm|33.6;-6.2 ppm|35.5;-20.6 ppm|36.0;+27.5 ppm|36.9;+9.3 ppm|38.8;+3.5 ppm|39.6;+59.8 ppm|42.0;-4.6 ppm|42.5;-2.1 ppm", // Not L10N
                "34.1;-9.9 ppm|36.1;+20.7 ppm|42.6;+11.7 ppm|38.2;+22.1 ppm", // Not L10N
                "37.7;-9.7 ppm|34.1;-9.9 ppm|36.1;+20.7 ppm|39.0;-0.9 ppm|42.6;+11.7 ppm", // Not L10N
                "34.5;+11.1 ppm|35.3;+2.9 ppm|35.3;+7.3 ppm|37.5;-9.4 ppm|38.9;-36.6 ppm;(idotp 0.80)|36.5;+6.5 ppm|36.6;+2.2 ppm;(idotp 0.89)|39.4;-61 ppm|40.9;-22.7 ppm", // Not L10N
                "35.7;+19.8 ppm|39.3;-17.9 ppm", // Not L10N
                "35.3;+2.9 ppm|35.3;+7.3 ppm;(idotp 0.78)|38.9;-37.4 ppm;(idotp 0.65)|34.5;+24.3 ppm|36.8;+13.8 ppm|36.8;+8.1 ppm|37.3;+5 ppm;(idotp 0.71)|39.4;-23.5 ppm|41.1;-7.5 ppm", // Not L10N
                "35.7;+19.8 ppm;(idotp 0.67)|38.4;+10.2 ppm;(idotp 0.54)", // Not L10N
                "36.1;-6.3 ppm|36.0;-34.1 ppm|37.3;-29.4 ppm|38.2;-19.2 ppm|38.5;-19.2 ppm|39.1;-1.8 ppm|39.9;-3.6 ppm|32.2;-11.1 ppm|34.3;-9.3 ppm", // Not L10N
                "41.9;+13.1 ppm|37.5;-12.8 ppm|34.7;+21.7 ppm|32.5;+0.1 ppm|42.4;-6.7 ppm", // Not L10N
                "35.9;-19.4 ppm|33.0;-55.9 ppm|39.5;-54.1 ppm|34.3;-50.2 ppm|37.3;-40.4 ppm", // Not L10N
            };

        private bool PreferWiff
        {
            get
            {
                // Prefer Wiff over mzML unless we're in the debugger where that's crazy slow, or we've been asked not to.
                // note the mzML files contain only the first 50 minutes of data to keep size down somewhat.
                // formerly they were apparently filtered on intensity but this didn't give the same results.
//                return (ExtensionTestContext.CanImportAbWiff && !System.Diagnostics.Debugger.IsAttached);
                return ExtensionTestContext.CanImportAbWiff;
            }
        }

        private string PreferedExtAbWiff
        {
            get { return PreferWiff ? ExtensionTestContext.ExtAbWiff : ExtensionTestContext.ExtMzml; }
        }
            
        private string GetTestPath(string path)
        {
            var folderMs1Filtering = PreferWiff ? "Ms1Filtering" : "Ms1FilteringMzml"; // Not L10N
            return TestFilesDirs[0].GetTestPath(folderMs1Filtering + '\\' + path);
        }

        protected override void DoTest()
        {
            TestSmallMolecules = false; // The presence of the extra test node without any results is incompatible with what's being tested here.

            // Clean-up before running the test
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings",
                            d => d.ChangeSettings(SrmSettingsList.GetDefault())));

            SrmDocument doc = SkylineWindow.Document;

            const string documentBaseName = "Ms1FilterTutorial";
            string documentFile = GetTestPath(documentBaseName + SrmDocument.EXT);
            RunUI(() => SkylineWindow.SaveDocument(documentFile));

            // show the empty Transition Setting dialog
            var transitionSettingsDlg = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() => transitionSettingsDlg.SelectedTab = TransitionSettingsUI.TABS.FullScan);
            PauseForScreenShot<TransitionSettingsUI.FullScanTab>("Transition Settings - Full-Scan tab nothing set", 2);
            OkDialog(transitionSettingsDlg, transitionSettingsDlg.OkDialog);

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);

            // We're on the "Build Spectral Library" page of the wizard.
            // Add the test xml file to the search files list and try to 
            // build the document library.
            string[] searchFiles =
                {
                    GetTestPath("100803_0001_MCF7_TiB_L.group.xml"),  // Not L10N
                    GetTestPath("100803_0005b_MCF7_TiTip3.group.xml")  // Not L10N
                };
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Import Peptide Search - Build Spectral Library empty page", 3);

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage ==
                            ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(searchFiles);
            });
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Import Peptide Search - Build Spectral Library populated page", 4);

            var ambiguousDlg = ShowDialog<MessageDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            OkDialog(ambiguousDlg, ambiguousDlg.OkDialog);

            // Verify document library was built
            string docLibPath = BiblioSpecLiteSpec.GetLibraryFileName(documentFile);
            string redundantDocLibPath = BiblioSpecLiteSpec.GetRedundantName(docLibPath);
            Assert.IsTrue(File.Exists(docLibPath) && File.Exists(redundantDocLibPath));
            var librarySettings = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
            Assert.IsTrue(librarySettings.HasDocumentLibrary);

            // We're on the "Extract Chromatograms" page of the wizard.
            // All the test results files are in the same directory as the 
            // document file, so all the files should be found, and we should
            // just be able to move to the next page.
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);
            PauseForScreenShot<ImportPeptideSearchDlg.ChromatogramsPage>("Import Peptide Search - Extract Chromatograms page", 5);

            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            PauseForScreenShot<ImportResultsNameDlg>("Import Results - Common prefix form", 6);

            OkDialog(importResultsNameDlg, importResultsNameDlg.YesDialog);

            // Wait for the "Add Modifications" page of the wizard.
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);

            List<string> modsToCheck = new List<string> { "Phospho (ST)", "Phospho (Y)", "Oxidation (M)" }; // Not L10N
            RunUI(() =>
            {
                importPeptideSearchDlg.MatchModificationsControl.CheckedModifications = modsToCheck;
            });
            PauseForScreenShot<ImportPeptideSearchDlg.MatchModsPage>("Import Peptide Search - Add Modifications page", 7);
            RunUIWithDocumentWait(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // We're on the "Configure MS1 Full-Scan Settings" page of the wizard.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new[] { 2, 3, 4 };
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorMassAnalyzer = FullScanMassAnalyzerType.tof;
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorRes = 10*1000;

                Assert.AreEqual(importPeptideSearchDlg.FullScanSettingsControl.PrecursorIsotopesCurrent, FullScanPrecursorIsotopes.Count);
                Assert.AreEqual(FullScanMassAnalyzerType.tof, importPeptideSearchDlg.FullScanSettingsControl.PrecursorMassAnalyzer);
                Assert.AreEqual(10*1000, importPeptideSearchDlg.FullScanSettingsControl.PrecursorRes);
                Assert.AreEqual(3, importPeptideSearchDlg.FullScanSettingsControl.Peaks);
                Assert.AreEqual(RetentionTimeFilterType.ms2_ids, importPeptideSearchDlg.FullScanSettingsControl.RetentionTimeFilterType);
                Assert.AreEqual(5, importPeptideSearchDlg.FullScanSettingsControl.TimeAroundMs2Ids);
            });
            PauseForScreenShot<ImportPeptideSearchDlg.Ms1FullScanPage>("Import Peptide Search - Configure MS1 Full-Scan Settings page", 8);

            RunUIWithDocumentWait(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // Last page of wizard - Import Fasta.
            string fastaPath = GetTestPath("12_proteins.062011.fasta");
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.AreEqual("Trypsin [KR | P]", importPeptideSearchDlg.ImportFastaControl.Enzyme.GetKey());
                importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages = 2;
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(fastaPath);
                Assert.IsFalse(importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled);
            });
            PauseForScreenShot<ImportPeptideSearchDlg.FastaPage>("Import Peptide Search - Import FASTA page", 10);

            var peptidesPerProteinDlg = ShowDialog<PeptidesPerProteinDlg>(() => importPeptideSearchDlg.ClickNextButton());
            WaitForCondition(() => peptidesPerProteinDlg.DocumentFinalCalculated);
            RunUI(() =>
            {
                int proteinCount, peptideCount, precursorCount, transitionCount;
                peptidesPerProteinDlg.NewTargetsAll(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                Assert.AreEqual(11, proteinCount);
                Assert.AreEqual(51, peptideCount);
                Assert.AreEqual(52, precursorCount);
                Assert.AreEqual(156, transitionCount);
                peptidesPerProteinDlg.NewTargetsFinal(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                Assert.AreEqual(11, proteinCount);
                Assert.AreEqual(51, peptideCount);
                Assert.AreEqual(52, precursorCount);
                Assert.AreEqual(156, transitionCount);
            });
            OkDialog(peptidesPerProteinDlg, peptidesPerProteinDlg.OkDialog);
            PauseForScreenShot<AllChromatogramsGraph>("Loading chromatograms window", 11);
            WaitForDocumentChangeLoaded(doc, 8 * 60 * 1000); // 10 minutes

            var libraryExplorer = ShowDialog<ViewLibraryDlg>(() => SkylineWindow.OpenLibraryExplorer(documentBaseName));
            var matchedPepModsDlg = WaitForOpenForm<AddModificationsDlg>();
            PauseForScreenShot<MultiButtonMsgDlg>("Add mods alert", 12);
            RunUI(() =>
                {
                    Assert.AreEqual(13, matchedPepModsDlg.NumMatched);
                    Assert.AreEqual(0, matchedPepModsDlg.NumUnmatched);
                    matchedPepModsDlg.CancelDialog();
                });
            RunUI(() =>
                {
                    libraryExplorer.GraphSettings.ShowBIons = true;
                    libraryExplorer.GraphSettings.ShowYIons = true;
                    libraryExplorer.GraphSettings.ShowCharge1 = true;
                    libraryExplorer.GraphSettings.ShowCharge2 = true;
                    libraryExplorer.GraphSettings.ShowPrecursorIon = true;
                });

            PauseForScreenShot<ViewLibraryDlg>("Spectral Library Explorer", 13);
            RunUI(() =>
                {
                    const string sourceFirst = "100803_0005b_MCF7_TiTip3.wiff";
                    const double timeFirst = 35.2128;
                    Assert.AreEqual(sourceFirst, libraryExplorer.SourceFile);
                    Assert.AreEqual(timeFirst, libraryExplorer.RetentionTime, 0.0001);
                    libraryExplorer.SelectedIndex++;
                    Assert.AreNotEqual(sourceFirst, libraryExplorer.SourceFile);
                    Assert.AreNotEqual(timeFirst, libraryExplorer.RetentionTime, 0.0001);
                });
            OkDialog(libraryExplorer, libraryExplorer.CancelDialog);

            const int TIB_L = 0; // index for Tib_L
            const int TIP3 = 1; // index for Tip3
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 11, 51, 52, 156);
            AssertResult.IsDocumentResultsState(SkylineWindow.Document, GetFileNameWithoutExtension(searchFiles[TIB_L]), 51, 52, 0, 156, 0);
            AssertResult.IsDocumentResultsState(SkylineWindow.Document, GetFileNameWithoutExtension(searchFiles[TIP3]), 51, 52, 0, 156, 0);
            string Tib_LFilename = searchFiles[TIB_L].Replace(".group.xml", PreferedExtAbWiff);
            string Tip3Filename = searchFiles[TIP3].Replace(".group.xml", PreferedExtAbWiff);

            // Select the first transition group.
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedPath =
                    SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                SkylineWindow.GraphSpectrumSettings.ShowAIons = true;
                SkylineWindow.GraphSpectrumSettings.ShowBIons = true;
                SkylineWindow.GraphSpectrumSettings.ShowYIons = true;
                SkylineWindow.GraphSpectrumSettings.ShowPrecursorIon = true;
                SkylineWindow.ExpandPrecursors();
                SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
            });
            RunDlg<SpectrumChartPropertyDlg>(SkylineWindow.ShowSpectrumProperties, dlg =>
            {
                dlg.FontSize = GraphFontSize.NORMAL;
                dlg.OkDialog();
            });
            RunDlg<ChromChartPropertyDlg>(SkylineWindow.ShowChromatogramProperties, dlg =>
            {
                dlg.FontSize = GraphFontSize.NORMAL;
                dlg.OkDialog();
            });
            RunUI(() =>
                {
                    // Make window screenshot size
                    if (IsPauseForScreenShots && SkylineWindow.WindowState != FormWindowState.Maximized)
                    {
                        SkylineWindow.Width = 1160;
                        SkylineWindow.Height = 792;
                    }
                });
            RestoreViewOnScreen(13);
            PauseForScreenShot("Main window with imported data", 14);

//            RunUIWithDocumentWait(() =>
//            {
//                SkylineWindow.ToggleIntegrateAll(); // TODO: No longer necessary.  Change in tutorial
//            });
            RunUI(() =>
            {
                SkylineWindow.ShowGraphPeakArea(true);
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.none);
                Settings.Default.ShowDotProductPeakArea = true;
                Settings.Default.ShowLibraryPeakArea = true;
            });
            PauseForScreenShot<GraphSummary.AreaGraphView>("Peak Areas view (show context menu)", 16);

            RestoreViewOnScreen(15);
            RunUI(() =>
            {
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.ArrangeGraphsTiled();
                SkylineWindow.ShowChromatogramLegends(false);
            });
            PauseForScreenShot("Main window layout", 17);

            int atest = 0;

            CheckAnnotations(TIB_L, 0, atest++);
            int pepIndex = 3;
            RunUI(() => SkylineWindow.CollapsePeptides());
            RunUI(() => SkylineWindow.ShowAlignedPeptideIDTimes(true));
            ChangePeakBounds(TIB_L, pepIndex, 38.79, 39.385);
            PauseForScreenShot("Chromatogram graphs clipped from main window", 19);
            CheckAnnotations(TIB_L, pepIndex, atest++);

            var alignmentForm = ShowDialog<AlignmentForm>(() => SkylineWindow.ShowRetentionTimeAlignmentForm());
            RunUI(() =>
                {
                    alignmentForm.Width = 711;
                    alignmentForm.Height = 561;
                    alignmentForm.ComboAlignAgainst.SelectedIndex = 0; // to match what's in the tutorial doc
                });
            PauseForScreenShot<AlignmentForm>("Retention time alignment form", 20);

            OkDialog(alignmentForm, alignmentForm.Close);
            PauseForScreenShot("Status bar clipped from main window - 4/51 pep 4/52 prec 10/156 tran", 21);

            pepIndex = JumpToPeptide("SSKASLGSLEGEAEAEASSPK");
            RunUI(() => SkylineWindow.ShowChromatogramLegends(true));
            Assert.IsTrue(8 == pepIndex);
            PauseForScreenShot("Chromatogram graph metafiles for 9th peptide", 21);
            CheckAnnotations(TIB_L, pepIndex, atest++); 

            ZoomSingle(TIP3,32.6, 41.4); // simulate the wheel scroll described in tutorial
            PauseForScreenShot("Chromatogram graph metafile showing all peaks for 1_MCF_TiB_L", 22);
            CheckAnnotations(TIB_L, pepIndex, atest++); 

            // current TIB_L peak should have idotp .87 and ppm -6.9
            Assert.AreEqual(0.87, GetTransitionGroupChromInfo(TIB_L, pepIndex).IsotopeDotProduct ?? -1, .005);
            Assert.AreEqual(-10.8, GetTransitionChromInfo(TIB_L, pepIndex, 0).MassError ?? -1, .05);

            ChangePeakBounds(TIB_L,pepIndex,36.5,38.0);

            // now current TIB_L peak should have idotp .9 and ppm -6.5
            Assert.AreEqual(0.9, GetTransitionGroupChromInfo(TIB_L, pepIndex).IsotopeDotProduct ?? -1, .005);
            Assert.AreEqual(-9.4, GetTransitionChromInfo(TIB_L, pepIndex, 0).MassError ?? -1, .05);
            CheckAnnotations(TIB_L, pepIndex, atest++);

            var undoIndex = SkylineWindow.Document.RevisionIndex; // preserve for simulating ctrl-z

            PickPeakBoth(pepIndex, 40.471035, 40.8134); // select peak for both chromatograms at these respective retention times
            PauseForScreenShot<GraphSummary.AreaGraphView>("Peak Areas graph metafile", 23);

            int[] m1Thru4 = {1,2,3,4,5};
            PickTransitions(pepIndex, m1Thru4, "Transition pick list filtered", 24, "Transition pick list unfiltered", 24); // turn on chromatograms
            PickPeakBoth(pepIndex, 36.992836, 37.3896027); // select peak for both chromatograms at these respective retention times
            ZoomSingle(TIP3, 32.4, 39.6); // set the view for screenshot
            PauseForScreenShot("Chromatogram graph metafile comparing 33 and 37 minute peaks", 25);
            CheckAnnotations(TIB_L, pepIndex, atest++);
            CheckAnnotations(TIP3, pepIndex, atest++);

            RevertDoc(undoIndex); // undo changes
            pepIndex=JumpToPeptide("ASLGSLEGEAEAEASSPKGK"); // Not L10N
            Assert.IsTrue(10 == pepIndex);
            PauseForScreenShot("Chhromatogram graph meta files for peptide ASLGSLEGEAEAEASSPKGK", 26);
            CheckAnnotations(TIB_L, pepIndex, atest++);
            CheckAnnotations(TIP3, pepIndex, atest++);

            PickTransitions(pepIndex, m1Thru4); // turn on M+3 and M+4
            ChangePeakBounds(TIP3, pepIndex, 37.35, 38.08);
            ZoomSingle(TIP3, 36.65, 39.11); // simulate the wheel scroll described in tutorial
            PauseForScreenShot("upper - Chromatogram graph metafile for peptide ASLGSLEGEAEAEASSPKGK with adjusted integration", 27);
            CheckAnnotations(TIP3, pepIndex, atest++);

            RevertDoc(undoIndex); // undo changes
            pepIndex = JumpToPeptide("AEGEWEDQEALDYFSDKESGK"); // Not L10N
            PauseForScreenShot("lower - Chromatogram graph metafiles for peptide AEGEWEDQEALDYFSDKESGK", 27);
            CheckAnnotations(TIB_L, pepIndex, atest++);
            CheckAnnotations(TIP3, pepIndex, atest++);

            int[] m1Thru5 = { 1, 2, 3, 4, 5, 6 };
            PickTransitions(pepIndex, m1Thru5); // turn on M+3 M+4 and M+5
            PauseForScreenShot("Chromatogram graph metafiles with M+3, M+4 and M+5 added", 28);
            CheckAnnotations(TIB_L, pepIndex, atest++);
            CheckAnnotations(TIP3, pepIndex, atest++);

            JumpToPeptide("ALVEFESNPEETREPGSPPSVQR"); // Not L10N
            PauseForScreenShot("Chromatogram graph metafiles for peptide ALVEFESNPEETREPGSPPSVQR", 29); 

            pepIndex = JumpToPeptide("YGPADVEDTTGSGATDSKDDDDIDLFGSDDEEESEEAKR"); // Not L10N
            PauseForScreenShot("upper - Peak Areas graph metafile for peptide YGPADVEDTTGSGATDSKDDDDIDLFGSDDEEESEEAKR", 30);

            int[] m1Thru7 = { 1, 2, 3, 4, 5, 6, 7, 8 };
            PickTransitions(pepIndex, m1Thru7); // enable [M+3] [M+4] [M+5] [M+6] [M+7]
            PauseForScreenShot("lower - Peak Areas graph metafile with M+3 through M+7 added", 30);
            CheckAnnotations(TIB_L, pepIndex, atest++);
            CheckAnnotations(TIP3, pepIndex, atest++);

            // page 32 zooming setup
            RunUI(() => 
            {
                SkylineWindow.SynchronizeZooming(true);
                SkylineWindow.LockYChrom(false);
                SkylineWindow.AlignToFile = SkylineWindow.GraphChromatograms.ToArray()[TIP3].GetChromFileInfoId(); // align to Tip3
            });
            ZoomBoth(36.5, 39.5, 1600); // simulate the wheel scroll described in tutorial
            RunUI(() => SkylineWindow.ShowChromatogramLegends(false));
            PauseForScreenShot("Chromatogram graphs clipped from main window with synchronized zooming", 32);

            RestoreViewOnScreen(33); // float the  Library Match window TODO this causes a crash at next call to ChangePeakBounds, in pwiz.Skyline.Controls.Graphs.GraphChromatogram.ChromGroupInfos.get() Line 492 , why?
            RunUI(() => SkylineWindow.GraphSpectrum.SelectSpectrum(new SpectrumIdentifier(MsDataFileUri.Parse(Tip3Filename), 37.6076f))); // set the Library Match view
            PauseForScreenShot<GraphSpectrum>("Library Match graph metafile - 5b_MCF7_TiTip3 (37.61 Min)", 33);

            RunUI(() => SkylineWindow.GraphSpectrum.SelectSpectrum(new SpectrumIdentifier(MsDataFileUri.Parse(Tib_LFilename), 37.0335f))); // set the Library Match view
            PauseForScreenShot<GraphSpectrum>("Library Match graph metafile - 1_MCF_TiB_L (37.03 min)", 33);

            RestoreViewOnScreen(34); // back to normal view

            pepIndex = JumpToPeptide("GVVDSEDLPLNISR"); // Not L10N
            RunUI(() => SkylineWindow.ShowChromatogramLegends(true));
            PauseForScreenShot("upper - Chromatogram graph metafiles for peptide GVVDSEDLPLNISR", 34);

            ZoomBoth(35.05,36.9,480);
            PauseForScreenShot("lower - Chromatogram graph metafile - effect of zoom ", 34);  
            ChangePeakBounds(TIP3, pepIndex, 35.7, 36.5); // adjust integration per tutorial 
            CheckAnnotations(TIP3, pepIndex, atest++); // check the new idotp values

            /* pepIndex = */ JumpToPeptide("DQVANSAFVER"); // Not L10N
            PauseForScreenShot("Chromatogram graph metafiles for peptide DQVANSAFVER", 35);

//            int[] m1 = {2};
//            PickTransitions(pepIndex, m1); // enable [M+1] only
//            // Measured times in TIB_L are different from displayed times, because of alignment
//            ChangePeakBounds(TIB_L, pepIndex, 23.99, 25.29); 
//            ChangePeakBounds(TIP3, pepIndex, 23.81, 25.21);
//            // First transition selected for screenshot
//            RunUI(() =>
//            {
//                var pathPep = SkylineWindow.SelectedPath;
//                var nodePep = ((PeptideTreeNode)SkylineWindow.SelectedNode).DocNode;
//                var nodeGroup = nodePep.TransitionGroups.First();
//                var nodeTran = nodeGroup.Transitions.First();
//                SkylineWindow.SelectedPath = new IdentityPath(
//                    new IdentityPath(pathPep, nodeGroup.TransitionGroup), nodeTran.Transition);
//            });
//            PauseForScreenShot("page 36 - M+1 only, with adjusted integration");
//            CheckAnnotations(TIB_L, pepIndex, atest++);
//            CheckAnnotations(TIP3, pepIndex, EXPECTED_ANNOTATIONS[atest]);

            var docAfter = WaitForProteinMetadataBackgroundLoaderCompletedUI();

            // Minimizing a chromatogram cache file.
            RunUI(SkylineWindow.CollapsePeptides);
            for (int i = 0; i < 5; i++) // just do the first 5
            {
                int iPeptide = i;
                var path = docAfter.GetPathTo((int) SrmDocument.Level.Molecules, iPeptide);
                RunUI(() =>
                {
                    SkylineWindow.SelectedPath = path;
                });
                WaitForGraphs();
            }

            // Eliminate extraneous chromatogram data.
            doc = WaitForProteinMetadataBackgroundLoaderCompletedUI();
            var minimizedFile = GetTestPath("Ms1FilteringTutorial-2min.sky"); // Not L10N
            var cacheFile = Path.ChangeExtension(minimizedFile, ChromatogramCache.EXT);
            {
                // TODO: Figure out why the minimize fails to unlock the .skyd file, if not minimized to current file
                RunUI(() => SkylineWindow.SaveDocument(minimizedFile));

                var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
                var minimizeResultsDlg = ShowDialog<MinimizeResultsDlg>(manageResultsDlg.MinimizeResults);
                RunUI(() =>
                {
                    minimizeResultsDlg.LimitNoiseTime = true;
                    minimizeResultsDlg.NoiseTimeRange = 2; // Not L10N
                });
                PauseForScreenShot<MinimizeResultsDlg>("Minimize Results form (percentages vary slightly)", 36);   // old p. 23

                OkDialog(minimizeResultsDlg, () => minimizeResultsDlg.MinimizeToFile(minimizedFile));
                WaitForCondition(() => File.Exists(cacheFile));
                WaitForClosedForm(manageResultsDlg);
            }
            WaitForDocumentChange(doc);

            // Inclusion list method export for MS1 filtering
            doc = SkylineWindow.Document;
            RunDlg<PeptideSettingsUI>(() => SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction), dlg =>
            {
                dlg.IsUseMeasuredRT = true;
                dlg.TimeWindow = 10;
                dlg.OkDialog();
            });
            WaitForDocumentChangeLoaded(doc);

            // Now deviating from the tutorial script for a moment to make sure we can choose a Scheduled export method.
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, dlg =>
            {
                dlg.MinPeptides = 1; // Not L10N
                const double minPeakFoundRatio = 0.1;
                dlg.MinPeakFoundRatio = minPeakFoundRatio;
                dlg.OkDialog();
            });

            // Ready to export, although we will just cancel out of the dialog.
            var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));
            RunUI(() =>
            {
                exportMethodDlg.InstrumentType = ExportInstrumentType.ABI_TOF; // Not L10N
                exportMethodDlg.MethodType = ExportMethodType.Scheduled;
                exportMethodDlg.CancelButton.PerformClick();
            });
            WaitForClosedForm(exportMethodDlg);

            RunUI(() => SkylineWindow.SaveDocument());
            RunUI(SkylineWindow.NewDocument);
        }

        private void ZoomSingle(int index, double startTime, double endTime, double? y = null)
        {
            RunUI(() => SkylineWindow.GraphChromatograms.ToArray()[index].ZoomTo(startTime, endTime, y));
            WaitForGraphs();            
        }

        private void ZoomBoth(double startTime,double endTime, double y)
        {
            ZoomSingle(0, startTime, endTime, y); // simulate the wheel scroll described in tutorial
            ZoomSingle(1, startTime, endTime, y); // simulate the wheel scroll described in tutorial
        }

        /// <summary>
        /// Selects peptide by sequence substring, returns its index
        /// </summary>
        /// <param name="pep">Sequence or sequence substring</param>
        /// <returns>The index of the peptide in the list of peptides for the current document</returns>
        private int JumpToPeptide(string pep)
        {
            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findDlg =>
            {
                findDlg.FindOptions = new FindOptions().ChangeText(pep); 
                findDlg.FindNext();
                findDlg.Close();
            });

            var peptides = SkylineWindow.Document.Peptides.ToArray();
            return peptides.IndexOf(nodePep => nodePep.Peptide.ToString().Contains(pep));
        }

        private IList<string> GetPointAnnotationStrings(int chromIndex, int pepIndex)
        {
            IList<string> result = null;
            RunUI(() => 
            {
                var pathPep = SkylineWindow.DocumentUI.GetPathTo((int) SrmDocument.Level.Molecules, pepIndex);
                SkylineWindow.SelectedPath = pathPep;
                var graphChrom = SkylineWindow.GraphChromatograms.ToArray()[chromIndex];
                // ToArray in RunUI() to avoid trying to enumerate off the UI thread
                result = graphChrom.GetAnnotationLabelStrings().ToArray();
            });
            return result;
        }

        private void PickPeakBoth(int pepIndex, double rt0, double rt1)
        {
            RunUIWithDocumentWait(() =>
            {
                var peptides = SkylineWindow.DocumentUI.Peptides.ToArray();
                var nodeGroup = peptides[pepIndex].TransitionGroups.First();
                var nodeTran = nodeGroup.Transitions.First();
                for (int i = 0; i < 2; i++)
                {
                    var graph = SkylineWindow.GraphChromatograms.ToArray()[i];
                    var approxRT = ((i == 1) ? rt1 : rt0);
                    TransitionGroupDocNode nodeGroupGraph;
                    TransitionDocNode nodeTranGraph;
                    var scaledRT = graph.FindAnnotatedPeakRetentionTime(approxRT, out nodeGroupGraph, out nodeTranGraph);
                    graph.FirePickedPeak(nodeGroup, nodeTran, scaledRT);
                }
            });
            WaitForGraphs();
        }

        private TransitionGroupChromInfo GetTransitionGroupChromInfo(int chromIndex, int pepIndex)
        {
            TransitionGroupChromInfo result = null;
            RunUI(() => 
            {
                var nodePep = SkylineWindow.DocumentUI.Peptides.ElementAt(pepIndex);
                var nodeGroup = nodePep.TransitionGroups.First();
                result = nodeGroup.ChromInfos.ToArray()[chromIndex];
            });
            return result;
        }

        private TransitionChromInfo GetTransitionChromInfo(int chromIndex, int pepIndex, int transIndex)
        {
            TransitionChromInfo result = null;
            RunUI(() => 
            {
                var nodePep = SkylineWindow.DocumentUI.Peptides.ElementAt(pepIndex);
                var nodeGroup = nodePep.TransitionGroups.First();
                var transition = nodeGroup.Transitions.ElementAt(transIndex);
                result = transition.ChromInfos.ToArray()[chromIndex];
            });
            return result;
        }


        private void ChangePeakBounds(int chromIndex,
                                       int pepIndex,
                                       double startDisplayTime,
                                       double endDisplayTime)
        {
            RunUIWithDocumentWait(() => // adjust integration
            {
                var pathPep = SkylineWindow.DocumentUI.GetPathTo((int) SrmDocument.Level.Molecules, pepIndex);
                SkylineWindow.SelectedPath = pathPep;

                var nodeGroup = SkylineWindow.DocumentUI.Peptides.ElementAt(pepIndex).TransitionGroups.First();
                var graphChrom = SkylineWindow.GraphChromatograms.ToArray()[chromIndex];

                var listChanges = new List<ChangedPeakBoundsEventArgs>
                {
                    new ChangedPeakBoundsEventArgs(new IdentityPath(pathPep, nodeGroup.TransitionGroup),
                        null,
                        graphChrom.NameSet,
                        graphChrom.ChromGroupInfos[0].FilePath,
                        graphChrom.GraphItems.First().GetNearestDisplayTime(startDisplayTime),
                        graphChrom.GraphItems.First().GetNearestDisplayTime(endDisplayTime),
                        PeakIdentification.ALIGNED,
                        PeakBoundsChangeType.both)
                };
                graphChrom.SimulateChangedPeakBounds(listChanges);
            });
            WaitForGraphs();
        }

        private void CheckAnnotations(int chromIndex, int pepIndex, int annotIndex)
        {
            RunUI(() =>
            {
                // Make window large enough that ID and Best Value can paint
                if (SkylineWindow.WindowState != FormWindowState.Maximized)
                {
                    SkylineWindow.Width = Math.Max(SkylineWindow.Width,600);
                    SkylineWindow.Height = Math.Max(SkylineWindow.Height,500);
                }
            });

            WaitForGraphs();
            string annotations = string.Join(@"|", GetPointAnnotationStrings(chromIndex, pepIndex)).Replace("\n",";");
            // Normalize decimal separator
            annotations = annotations.Replace(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator, 
                                              CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator);
            if (IsRecordMode)
                Console.WriteLine(@"""{0}"", // Not L10N", annotations);  // Not L10N
            else
                AssertEx.AreEqualLines(EXPECTED_ANNOTATIONS[annotIndex], annotations);
        }

        private void PickTransitions(int pepIndex, int[] transIndexes,
            string screenshotPromptA = null, int? pageA = null, string screenshotPromptB = null, int? pageB = null)
        {
            var doc = SkylineWindow.Document;
            var pepPath = doc.GetPathTo((int)SrmDocument.Level.Molecules, pepIndex);
            var nodeGroup = doc.Peptides.ElementAt(pepIndex).TransitionGroups.First();
            var groupPath = new IdentityPath(pepPath, nodeGroup.TransitionGroup);
            RunUI(() => SkylineWindow.SequenceTree.SelectedPath = groupPath);
            var popupPickList = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            if (screenshotPromptA != null)
                PauseForScreenShot<PopupPickList>(screenshotPromptA, pageA);
            RunUI(() => popupPickList.ApplyFilter(false));  // clear the filter
            if (screenshotPromptB != null)
                PauseForScreenShot<PopupPickList>(screenshotPromptB, pageB);
            RunUI(() =>
            {
                for (int i = 0; i < popupPickList.ItemNames.Count(); i++)
                {
                    popupPickList.SetItemChecked(i, transIndexes.Contains(i));
                }
            });
            OkDialog(popupPickList, popupPickList.OnOk);
            WaitForDocumentChange(doc);
            WaitForGraphs();
        }

        private void RevertDoc(int undoIndex)
        {
            while (SkylineWindow.Document.RevisionIndex > undoIndex)
            {
                RunUIWithDocumentWait(SkylineWindow.Undo);
            }
            WaitForGraphs();
        }
        
        private void RunUIWithDocumentWait(Action act)
        {
            var doc = SkylineWindow.Document;
            RunUI(act);
            WaitForDocumentChange(doc); // make sure the action changes the document
        }

        private string GetFileNameWithoutExtension(string searchFile)
        {
            searchFile = Path.GetFileName(searchFile) ?? "";
            // Remove the shared prefix and everything after the first period
            const int prefixLen = 10;
            return searchFile.Substring(prefixLen, searchFile.IndexOf('.') - prefixLen);
        }
    }
}
