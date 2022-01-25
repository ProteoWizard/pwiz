/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
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
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class SmallMoleculesTutorialTest : AbstractFunctionalTest
    {
        private bool _inferredLabels;

        protected override bool UseRawFiles
        {
            get { return !ForceMzml && ExtensionTestContext.CanImportWatersRaw; }
        }

        protected override bool ShowStartPage
        {
            get { return !IsPauseForScreenShots; }  // So we can point out the UI mode control
        }

        [TestMethod]
        public void TestSmallMoleculesTutorial()
        {
            // Set true to look at tutorial screenshots.
//            IsPauseForScreenShots = true;
//            IsCoverShotMode = true;
            CoverShotName = "SmallMolecule";

            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/SmallMolecule-20_1.pdf";

            TestFilesZipPaths = new[]
            {
                (UseRawFiles
                   ? @"https://skyline.ms/tutorials/SmallMolecule_3_6.zip"
                   : @"https://skyline.ms/tutorials/SmallMoleculeMzml_3_6.zip"),
                @"TestTutorial\SmallMoleculeViews.zip"
            };
            RunFunctionalTest();
        }

        [TestMethod]
        public void TestSmallMoleculesTutorialInferredLabels()
        {
            // Verify ability to infer labels from transition list
            _inferredLabels = true;
            TestSmallMoleculesTutorial();
        }

        private string GetTestPath(string relativePath = null)
        {
            string folderSmallMolecule = UseRawFiles ? "SmallMolecule" : "SmallMoleculeMzml";
            string fullRelativePath = relativePath != null ? Path.Combine(folderSmallMolecule, relativePath) : folderSmallMolecule;
            return TestFilesDirs[0].GetTestPath(fullRelativePath);
        }

        protected override void DoTest()
        {
            if (IsPauseForScreenShots)
                RunUI(() => SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules));
            else // old way of doing things
            {
                // Setting the UI mode, p 2  
                var startPage = WaitForOpenForm<StartPage>();
                RunUI(() => startPage.SetUIMode(SrmDocument.DOCUMENT_TYPE.proteomic));
//                PauseForScreenShot<StartPage>("Start Window proteomic", 2);
                RunUI(() => startPage.SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules));
//                PauseForScreenShot<StartPage>("Start Window small molecule", 3);
                ShowSkyline(() => startPage.DoAction(skylineWindow => true));
            }

            // Inserting a Transition List, p. 3
            {
                var doc = SkylineWindow.Document;
                
                var importDialog3 = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
                RunUI(() => importDialog3.Size = new Size(600, 300));
                string impliedLabeled = GetCsvFileText(GetTestPath("SMTutorial_TransitionList.csv"));
                if (_inferredLabels)
                {
                    // Remove the explicit ",heavy" and ",label" from the text
                    var lines = impliedLabeled.Split('\n').Where(l => ! string.IsNullOrEmpty(l));
                    var altered = lines.Select(l => l.Substring(0,l.LastIndexOf(TextUtil.CsvSeparator))).ToArray();
                    impliedLabeled = TextUtil.LineSeparate(altered);
                }
                PauseForScreenShot<InsertTransitionListDlg>("ImportTransitionDlg ready for paste", 5);
                var col4Dlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog3.TransitionListText = impliedLabeled);
                RunUI(() => {
                    col4Dlg.radioMolecule.PerformClick();
                    if (!_inferredLabels)
                    {
                        var comboBoxes = col4Dlg.ComboBoxes;
                        comboBoxes[9].SelectedIndex = comboBoxes[1].FindStringExact(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type);
                    }
                });
                PauseForScreenShot<ImportTransitionListColumnSelectDlg>("Column Select Dlg with column headers selected", 6);

                OkDialog(col4Dlg, col4Dlg.OkDialog);




                var docTargets = WaitForDocumentChange(doc);

                AssertEx.IsDocumentState(docTargets, null, 6, 12, 19, 21);
                Assert.IsFalse(docTargets.MoleculeTransitions.Any(t => t.Transition.IsPrecursor()));

                RunUI(() =>
                {
                    SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                    SkylineWindow.Size = new Size(957, 654);
                });
                RestoreViewOnScreen(5);
                PauseForScreenShot<SkylineWindow>("Skyline with small molecule targets", 6);

                RunUI(() => SkylineWindow.SaveDocument(GetTestPath("Amino Acid Metabolism.sky")));

                using (new WaitDocumentChange(null, true))
                {
                    var importResultsDlg1 = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
                    var openDataSourceDialog1 = ShowDialog<OpenDataSourceDialog>(() => importResultsDlg1.NamedPathSets =
                        importResultsDlg1.GetDataSourcePathsFile(null));
                    RunUI(() =>
                    {
                        openDataSourceDialog1.CurrentDirectory = new MsDataFilePath(GetTestPath());
                        openDataSourceDialog1.SelectAllFileType(ExtWatersRaw);
                    });
                    PauseForScreenShot<ImportResultsDlg>("Import Results Files form", 7);
                    OkDialog(openDataSourceDialog1, openDataSourceDialog1.Open);

                    var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg1.OkDialog);
                    OkDialog(importResultsNameDlg, importResultsNameDlg.NoDialog);
                }

                SelectNode(SrmDocument.Level.MoleculeGroups, 0);

                PauseForScreenShot<SkylineWindow>("Skyline window multi-target graph", 9);

                var docResults = SkylineWindow.Document;

                var expectedTransCount = new Dictionary<string, int[]>
                {
                    // transition groups, heavy transition groups, tranistions, heavy transitions
                    {"ID15656_01_WAA263_3976_020415", new[] {12, 7, 13, 8}},
                    {"ID15658_01_WAA263_3976_020415", new[] {12, 6, 13, 7}},
                    {"ID15659_01_WAA263_3976_020415", new[] {12, 7, 13, 8}},
                    {"ID15661_01_WAA263_3976_020415", new[] {12, 7, 13, 8}},
                    {"ID15662_01_WAA263_3976_020415", new[] {12, 7, 13, 8}},
                    {"ID15663_01_WAA263_3976_020415", new[] {11, 7, 12, 8}},
                    {"ID15664_01_WAA263_3976_020415", new[] {11, 6, 12, 7}},
                    {"ID15739_01_WAA263_3976_020415", new[] {10, 6, 10, 7}},
                    {"ID15740_01_WAA263_3976_020415", new[] {12, 6, 12, 7}},
                    {"ID15740_02_WAA263_3976_020415", new[] {11, 5, 11, 6}},
                    {"ID15740_04_WAA263_3976_020415", new[] {12, 6, 12, 7}},
                    {"ID15741_01_WAA263_3976_020415", new[] {12, 7, 13, 8}},
                    {"ID15741_02_WAA263_3976_020415", new[] {12, 6, 13, 7}}
                };
                var msg = "";
                foreach (var chromatogramSet in docResults.Settings.MeasuredResults.Chromatograms)
                {
                    int[] transitions;
                    if (!expectedTransCount.TryGetValue(chromatogramSet.Name, out transitions))
                        transitions = new[] {12, 7, 13, 8}; // Most have this value
                    try
                    {
                        AssertResult.IsDocumentResultsState(docResults, chromatogramSet.Name, 12, transitions[0], transitions[1], transitions[2], transitions[3]);
                    }
                    catch(Exception x)
                    {
                        msg = TextUtil.LineSeparate(msg, x.Message);
                    }
                }
                if (!string.IsNullOrEmpty(msg))
                    Assert.IsTrue(string.IsNullOrEmpty(msg), msg);
                RestoreViewOnScreen(9);
                PauseForScreenShot<SkylineWindow>("Skyline window multi-replicate layout", 10);

                if (IsCoverShotMode)
                {
                    RunUI(() =>
                    {
                        Settings.Default.ChromatogramFontSize = 14;
                        Settings.Default.AreaFontSize = 14;
                        SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                        SkylineWindow.AutoZoomBestPeak();
                    });

                    RestoreCoverViewOnScreen();

                    var importDialog = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
                    string impliedLabeled2 = GetCsvFileText(GetTestPath("SMTutorial_TransitionList.csv"));
                    var colDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog.TransitionListText = impliedLabeled2);

                    TakeCoverShot();

                    OkDialog(colDlg, colDlg.CancelDialog);
                }
            }
        }
    }
}
