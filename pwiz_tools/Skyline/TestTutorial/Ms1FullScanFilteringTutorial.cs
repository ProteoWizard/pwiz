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
using System.Text;
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
                "35.7;-23 ppm|36.6;0 ppm|32.4;-6 ppm|33.2;0 ppm|34.1;+6.8 ppm|37.5;-1.7 ppm|38.5;-1.6 ppm|39.1;+3.4 ppm", // Not L10N
                "39.0;-13.1 ppm;(idotp 0.90)|34.1;-0.5 ppm|34.6;-19.7 ppm|35.7;-47.9 ppm|36.1;-36.4 ppm;(idotp 0.97)|37.8;-11.6 ppm|37.8;0 ppm|36.5;-41.4 ppm|40.9;0 ppm", // Not L10N
                "37.0;-6.9 ppm|32.4;0 ppm|35.2;0 ppm|35.3;0 ppm|36.6;-10.6 ppm|37.5;-3.9 ppm|40.5;-37.8 ppm|40.7;-28 ppm|42.0;+3.8 ppm|33.1;+5.4 ppm", // Not L10N
                "37.0;-6.9 ppm|32.4;0 ppm|35.2;0 ppm|35.3;0 ppm|36.6;-10.6 ppm|37.5;-3.9 ppm|40.5;-37.8 ppm|40.7;-28 ppm|42.0;+3.8 ppm|33.1;+5.4 ppm", // Not L10N
                "37.0;-6.5 ppm|32.4;0 ppm|35.2;0 ppm|35.3;0 ppm|40.5;-37.8 ppm|40.7;-28 ppm|42.0;+3.8 ppm|33.1;+5.4 ppm", // Not L10N
                "37.0;-6.9 ppm|35.3;0 ppm|36.6;-10.6 ppm|37.5;-3.9 ppm|40.5;-37.8 ppm|40.7;-28 ppm|42.0;+3.8 ppm|33.1;+5.4 ppm|32.2;0 ppm|34.6;-46.3 ppm", // Not L10N
                "37.4;-1.5 ppm|40.8;-20.7 ppm|33.2;+25.8 ppm|34.9;-41 ppm", // Not L10N
                "37.5;0 ppm|32.8;-13.6 ppm|34.1;-8.5 ppm|35.5;0 ppm|36.0;+9.6 ppm|36.9;-2.6 ppm|38.8;0 ppm|39.6;0 ppm|42.0;-5.4 ppm", // Not L10N
                "34.1;-3.9 ppm|36.1;+11.2 ppm|40.6;-7.9 ppm;(idotp 0.91)|38.2;+19.9 ppm;(idotp 0.83)", // Not L10N
                "37.7;-4.1 ppm|34.1;-3.9 ppm|36.1;+11.2 ppm|40.8;+4.5 ppm", // Not L10N
                "34.5;+2.9 ppm|35.3;0 ppm|35.3;+5.6 ppm|37.5;0 ppm|38.9;-28.1 ppm;(idotp 0.80)|36.5;0 ppm|36.6;0 ppm;(idotp 0.89)|39.4;0 ppm|40.9;-2.4 ppm", // Not L10N
                "35.7;+2.4 ppm|39.3;-14.3 ppm", // Not L10N
                "35.3;0 ppm|35.3;+5.6 ppm;(idotp 0.78)|38.9;-31.2 ppm;(idotp 0.65)|34.5;0 ppm|36.8;0 ppm|36.8;0 ppm|37.3;0 ppm;(idotp 0.71)|39.4;0 ppm|41.1;-12.7 ppm", // Not L10N
                "35.7;+2.4 ppm;(idotp 0.67)|38.4;+10.6 ppm;(idotp 0.54)", // Not L10N
                "36.1;-5.2 ppm|37.3;-20.8 ppm|38.2;0 ppm|38.5;0 ppm|39.1;0 ppm|39.9;-0.5 ppm|32.2;-0.5 ppm|34.3;0 ppm|34.8;0 ppm", // Not L10N
                "41.9;0 ppm|37.5;-11.9 ppm|34.7;0 ppm|32.5;0 ppm|42.4;-16.1 ppm", // Not L10N
                "35.9;-16.3 ppm|33.0;-47.7 ppm|39.5;-53.6 ppm", // Not L10N
                "","","","","","","","","","","" // room to grow Not L10N
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
            PauseForScreenShot("page 3 - empty Transition Settings");
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
            PauseForScreenShot("page 4 - empty library page");

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage ==
                            ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(searchFiles);
            });
            PauseForScreenShot("page 5 - populated library page");

            RunUIWithDocumentWait(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

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
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page));
            PauseForScreenShot("page 6 - results page");

            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            PauseForScreenShot("page 7 - remove prefix form");

            OkDialog(importResultsNameDlg, importResultsNameDlg.YesDialog);

            // Wait for the "Add Modifications" page of the wizard.
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);

            List<string> modsToCheck = new List<string> { "Phospho (ST)", "Phospho (Y)", "Oxidation (M)" }; // Not L10N
            RunUI(() =>
            {
                importPeptideSearchDlg.MatchModificationsControl.CheckedModifications = modsToCheck;
            });
            PauseForScreenShot("page 8 - add modifications");
            RunUIWithDocumentWait(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // We're on the "Configure MS1 Full-Scan Settings" page of the wizard.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.ms1_full_scan_settings_page);
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new[] { 2, 3, 4 };
                Assert.AreEqual(importPeptideSearchDlg.FullScanSettingsControl.PrecursorIsotopesCurrent, FullScanPrecursorIsotopes.Count);
                Assert.AreEqual(3, importPeptideSearchDlg.FullScanSettingsControl.Peaks);
                Assert.AreEqual(RetentionTimeFilterType.ms2_ids, importPeptideSearchDlg.FullScanSettingsControl.RetentionTimeFilterType);
                Assert.AreEqual(5, importPeptideSearchDlg.FullScanSettingsControl.TimeAroundMs2Ids);
            });
            PauseForScreenShot("page 9 - full-scan settings");

            RunUIWithDocumentWait(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // Last page of wizard - Import Fasta.
            string fastaPath = GetTestPath("12_proteins.062011.fasta");
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.AreEqual("Trypsin [KR | P]", importPeptideSearchDlg.ImportFastaControl.Enzyme.GetKey());
                importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages = 2;
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(fastaPath);
            });
            PauseForScreenShot("page 11 - import fasta page");

            OkDialog(importPeptideSearchDlg, () => importPeptideSearchDlg.ClickNextButton());
            WaitForDocumentChangeLoaded(doc, 8 * 60 * 1000); // 10 minutes

            var libraryExplorer = ShowDialog<ViewLibraryDlg>(() => SkylineWindow.OpenLibraryExplorer(documentBaseName));
            var matchedPepModsDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            PauseForScreenShot("page 13 - add mods alert");
            RunUI(() =>
                {
                    Assert.IsTrue(matchedPepModsDlg.Message.StartsWith(Resources.ViewLibraryDlg_MatchModifications_This_library_appears_to_contain_the_following_modifications));
                    Assert.IsTrue(matchedPepModsDlg.Message.Split('\n').Length > 16);
                    matchedPepModsDlg.BtnCancelClick();
                });
            RunUI(() =>
                {
                    libraryExplorer.GraphSettings.ShowBIons = true;
                    libraryExplorer.GraphSettings.ShowYIons = true;
                    libraryExplorer.GraphSettings.ShowCharge1 = true;
                    libraryExplorer.GraphSettings.ShowCharge2 = true;
                    libraryExplorer.GraphSettings.ShowPrecursorIon = true;
                });

            PauseForScreenShot("page 14 - spectral library explorer");
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
            AssertResult.IsDocumentResultsState(SkylineWindow.Document, GetFileNameWithoutExtension(searchFiles[TIB_L]), 47, 48, 0, 142, 0);
            AssertResult.IsDocumentResultsState(SkylineWindow.Document, GetFileNameWithoutExtension(searchFiles[TIP3]), 49, 50, 0, 143, 0);
            string Tib_LFilename = searchFiles[TIB_L].Replace(".group.xml", PreferedExtAbWiff);
            string Tip3Filename = searchFiles[TIP3].Replace(".group.xml", PreferedExtAbWiff);

            // Select the first transition group.
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedPath =
                    SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Peptides, 0);
                SkylineWindow.GraphSpectrumSettings.ShowAIons = true;
                SkylineWindow.GraphSpectrumSettings.ShowBIons = true;
                SkylineWindow.GraphSpectrumSettings.ShowYIons = true;
                SkylineWindow.GraphSpectrumSettings.ShowPrecursorIon = true;
                SkylineWindow.ExpandPrecursors();
                SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
            });
            RunDlg<SpectrumChartPropertyDlg>(SkylineWindow.ShowSpectrumProperties, dlg =>
            {
                dlg.FontSize = 12;
                dlg.OkDialog();
            });
            RunDlg<ChromChartPropertyDlg>(SkylineWindow.ShowChromatogramProperties, dlg =>
            {
                dlg.FontSize = 12;
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
            RestoreViewOnScreen(TestFilesDirs[1].GetTestPath(@"p13.view"));
            PauseForScreenShot("page 15 - imported data - TIB_L annotations don't agree with tutorial");   // p. 12
 
           RunUIWithDocumentWait(() =>
           {
                SkylineWindow.IntegrateAll();

                SkylineWindow.ShowGraphPeakArea(true);
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.none);
                Settings.Default.ShowDotProductPeakArea = true;
                Settings.Default.ShowLibraryPeakArea = true;
            });
            PauseForScreenShot("page 17 - peak area view (show context menu)");

            RestoreViewOnScreen(TestFilesDirs[1].GetTestPath(@"p15.view"));
            RunUI(() =>
            {
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.ArrangeGraphsTiled();
                SkylineWindow.ShowChromatogramLegends(false);
            });
            PauseForScreenShot("page 18 - main window layout"); // Not L10N

            int atest = 0;

            CheckAnnotations(TIB_L, 0, atest++);
            int pepIndex = 3;
            RunUI(() => SkylineWindow.CollapsePeptides());
            RunUI(() => SkylineWindow.ShowAlignedPeptideIDTimes(true));
            ChangePeakBounds(TIB_L, pepIndex, 38.79, 39.385);
            PauseForScreenShot("page 20 - chromatogram graphs"); // Not L10N
            CheckAnnotations(TIB_L, pepIndex, atest++);

            var alignmentForm = ShowDialog<AlignmentForm>(() => SkylineWindow.ShowRetentionTimeAlignmentForm());
            RunUI(() =>
                {
                    alignmentForm.Width = 711;
                    alignmentForm.Height = 561;
                    alignmentForm.ComboAlignAgainst.SelectedIndex = 0; // to match what's in the tutorial doc
                });
            PauseForScreenShot("page 21 - retention time alignment form");

            OkDialog(alignmentForm, alignmentForm.Close);
            PauseForScreenShot("page 22 - 4/51 pep 4/52 prec 10/156 tran");

            pepIndex = JumpToPeptide("SSKASLGSLEGEAEAEASSPK");
            RunUI(() => SkylineWindow.ShowChromatogramLegends(true));
            Assert.IsTrue(8 == pepIndex);
            PauseForScreenShot("page 22 - chromatograms for 9th peptide");
            CheckAnnotations(TIB_L, pepIndex, atest++); 

            ZoomSingle(TIP3,32.6, 41.4); // simulate the wheel scroll described in tutorial
            PauseForScreenShot("page 23 - showing all peaks for 1_MCF_TiB_L");
            CheckAnnotations(TIB_L, pepIndex, atest++); 

            // current TIB_L peak should have idotp .87 and ppm -6.9
            Assert.AreEqual(0.87, GetTransitionGroupChromInfo(TIB_L, pepIndex).IsotopeDotProduct ?? -1, .005);
            Assert.AreEqual(-6.9, GetTransitionChromInfo(TIB_L, pepIndex, 0).MassError ?? -1, .05);

            ChangePeakBounds(TIB_L,pepIndex,36.5,38.0);

            // now current TIB_L peak should have idotp .9 and ppm -6.5
            Assert.AreEqual(0.9, GetTransitionGroupChromInfo(TIB_L, pepIndex).IsotopeDotProduct ?? -1, .005);
            Assert.AreEqual(-6.5, GetTransitionChromInfo(TIB_L, pepIndex, 0).MassError ?? -1, .05);
            CheckAnnotations(TIB_L, pepIndex, atest++);

            var undoIndex = SkylineWindow.Document.RevisionIndex; // preserve for simulating ctrl-z

            PickPeakBoth(pepIndex, 40.471035, 40.8134); // select peak for both chromatograms at these respective retention times
            PauseForScreenShot("page 24 - Peak areas graphs");

            int[] m1Thru4 = {1,2,3,4,5};
            PickTransitions(pepIndex, m1Thru4, "page 25 - selecting chromatograms", "page 25 - selecting chromatograms continued"); // turn on chromatograms
            PickPeakBoth(pepIndex, 36.992836, 37.3896027); // select peak for both chromatograms at these respective retention times
            ZoomSingle(TIP3, 32.4, 39.6); // set the view for screenshot
            PauseForScreenShot("page 26 - comparing 33 and 37 minute peaks");
            CheckAnnotations(TIB_L, pepIndex, atest++);
            CheckAnnotations(TIP3, pepIndex, atest++);

            RevertDoc(undoIndex); // undo changes
            pepIndex=JumpToPeptide("ASLGSLEGEAEAEASSPKGK"); // Not L10N
            Assert.IsTrue(10 == pepIndex);
            PauseForScreenShot("page 27 - chromatograms for peptide ASLGSLEGEAEAEASSPKGK");
            CheckAnnotations(TIB_L, pepIndex, atest++);
            CheckAnnotations(TIP3, pepIndex, atest++);

            PickTransitions(pepIndex, m1Thru4); // turn on M+3 and M+4
            ChangePeakBounds(TIP3, pepIndex, 37.35, 38.08);
            ZoomSingle(TIP3, 36.65, 39.11); // simulate the wheel scroll described in tutorial
            PauseForScreenShot("page 28, upper - chromatograms for peptide ASLGSLEGEAEAEASSPKGK with adjusted integration");
            CheckAnnotations(TIP3, pepIndex, atest++);

            RevertDoc(undoIndex); // undo changes
            pepIndex = JumpToPeptide("AEGEWEDQEALDYFSDKESGK"); // Not L10N
            PauseForScreenShot("page 28, lower - chromatograms for peptide AEGEWEDQEALDYFSDKESGK");
            CheckAnnotations(TIB_L, pepIndex, atest++);
            CheckAnnotations(TIP3, pepIndex, atest++);

            int[] m1Thru5 = { 1, 2, 3, 4, 5, 6 };
            PickTransitions(pepIndex, m1Thru5); // turn on M+3 M+4 and M+5
            PauseForScreenShot("page 29 - chromatogram graphs");
            CheckAnnotations(TIB_L, pepIndex, atest++);
            CheckAnnotations(TIP3, pepIndex, atest++);

            JumpToPeptide("ALVEFESNPEETREPGSPPSVQR"); // Not L10N
            PauseForScreenShot("page 30 - chromatograms for peptide ALVEFESNPEETREPGSPPSVQR "); 

            pepIndex = JumpToPeptide("YGPADVEDTTGSGATDSKDDDDIDLFGSDDEEESEEAKR"); // Not L10N
            PauseForScreenShot("page 31 upper - peak area plot for peptide YGPADVEDTTGSGATDSKDDDDIDLFGSDDEEESEEAKR");

            int[] m1Thru7 = { 1, 2, 3, 4, 5, 6, 7, 8 };
            PickTransitions(pepIndex, m1Thru7); // enable [M+3] [M+4] [M+5] [M+6] [M+7]
            PauseForScreenShot("page 31 lower - peak area plot");
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
            PauseForScreenShot("page 32 - effects of zoom settings");

            RestoreViewOnScreen(TestFilesDirs[1].GetTestPath(@"p33.view")); // float the  Library Match window TODO this causes a crash at next call to ChangePeakBounds, in pwiz.Skyline.Controls.Graphs.GraphChromatogram.ChromGroupInfos.get() Line 492 , why?
            RunUI(() => SkylineWindow.GraphSpectrum.SelectSpectrum(new SpectrumIdentifier(Tip3Filename, 37.6076f))); // set the Library Match view
            PauseForScreenShot("page 33 - 5b_MCF7_TiTip3 (37.61 Min)");

            RunUI(() => SkylineWindow.GraphSpectrum.SelectSpectrum(new SpectrumIdentifier(Tib_LFilename, 37.0335f))); // set the Library Match view
            PauseForScreenShot("page 33 - 1_MCF_TiB_L (37.03 min)");

            RestoreViewOnScreen(TestFilesDirs[1].GetTestPath(@"p34.view")); // back to normal view

            pepIndex = JumpToPeptide("GVVDSEDLPLNISR"); // Not L10N
            RunUI(() => SkylineWindow.ShowChromatogramLegends(true));
            PauseForScreenShot("page 34 upper - chromatograms for peptide GVVDSEDLPLNISR");

            ZoomBoth(35.05,36.9,480);
            PauseForScreenShot("page 34 lower - effect of zoom ");  
            ChangePeakBounds(TIP3, pepIndex, 35.7, 36.5); // adjust integration per tutorial 
            CheckAnnotations(TIP3, pepIndex, atest++); // check the new idotp values

            /* pepIndex = */ JumpToPeptide("DQVANSAFVER"); // Not L10N
            PauseForScreenShot("page 35 upper - chromatograms for peptide DQVANSAFVER");

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

            var docAfter = SkylineWindow.Document;

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
                PauseForScreenShot("page 37 - minimize results (percentages vary slightly)");   // old p. 23

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
                var pathPep = SkylineWindow.DocumentUI.GetPathTo((int) SrmDocument.Level.Peptides, pepIndex);
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
                var pathPep = SkylineWindow.DocumentUI.GetPathTo((int) SrmDocument.Level.Peptides, pepIndex);
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

        private void PickTransitions(int pepIndex, int[] transIndexes, string screenshotPromptA = null, string screenshotPromptB = null)
        {
            var doc = SkylineWindow.Document;
            var pepPath = doc.GetPathTo((int)SrmDocument.Level.Peptides, pepIndex);
            var nodeGroup = doc.Peptides.ElementAt(pepIndex).TransitionGroups.First();
            var groupPath = new IdentityPath(pepPath, nodeGroup.TransitionGroup);
            RunUI(() => SkylineWindow.SequenceTree.SelectedPath = groupPath);
            var popupPickList = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
            if (screenshotPromptA != null)
                PauseForScreenShot(screenshotPromptA);
            RunUI(() => popupPickList.ApplyFilter(false));  // clear the filter
            if (screenshotPromptB != null)
                PauseForScreenShot(screenshotPromptB);
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
