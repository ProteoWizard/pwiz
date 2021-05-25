/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using pwiz.Common.Chemistry;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class DdaSearchTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDdaSearch()
        {
            TestFilesZip = @"TestFunctional\DdaSearchTest.zip";

            // Test that correct error is issued when MSAmanda tries to parse a missing file (enzymes.xml)
            // Automating this test turned out to be more difficult than I thought and not worth the effort.
            //var skylineDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            //File.Move(System.IO.Path.Combine(skylineDir, "enzymes.xml"), System.IO.Path.Combine(skylineDir, "not-the-enzymes-you-are-looking-for.xml"));

            RunFunctionalTest();
        }

        private string GetTestPath(string path)
        {
            return TestFilesDir.GetTestPath(path);
        }

        private IEnumerable<string> SearchFiles
        {
            get
            {
                return new[]
                {
                    GetTestPath("Rpal_Std_2d_FullMS_Orbi30k_MSMS_Orbi7k_Centroid_Run1_102006_02.mzML"),
                    GetTestPath("Rpal_Std_2d_FullMS_Orbi30k_MSMS_Orbi7k_Centroid_Run1_102006_03.mzML")
                };
            }
        }

        private string[] SearchFilesSameName
        {
            get
            {
                return new[]
                {
                    GetTestPath("Rpal_Std_2d_FullMS_Orbi30k_MSMS_Orbi7k_Centroid_Run1_102006_02.mzML"),
                    GetTestPath(Path.Combine("subdir", "Rpal_Std_2d_FullMS_Orbi30k_MSMS_Orbi7k_Centroid_Run1_102006_02.mzML"))
                };
            }
        }

        protected override void DoTest()
        {
            TestAmandaSearch();
        }

        /// <summary>
        /// Quick test that DDA search works with MSAmanda.
        /// </summary>
        private void TestAmandaSearch()
        {
            PrepareDocument("TestDdaSearch.sky");

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);

            // We're on the "Build Spectral Library" page of the wizard.
            // Add the test xml file to the search files list and try to 
            // build the document library.

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.PerformDDASearch = true;
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => (MsDataFileUri) new MsDataFilePath(o)).Take(1).ToArray();
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dia; // will go back and switch to DDA
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // With only 1 source, no add/remove prefix/suffix dialog

            // We're on the "Match Modifications" page. Add M+16 mod.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
                // Test going back and switching workflow to DDA. This used to cause an exception.
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dda;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
            });

            // In PerformDDASearch mode, ClickAddStructuralModification launches edit list dialog
            var editListUI =
                ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(importPeptideSearchDlg.MatchModificationsControl.ClickAddStructuralModification);
            RunDlg<EditStaticModDlg>(editListUI.AddItem, editModDlg =>
            {
                editModDlg.SetModification("Oxidation (M)", true); // Not L10N
                editModDlg.OkDialog();
            });

            // Test a non-Unimod mod that won't affect the search
            RunDlg<EditStaticModDlg>(editListUI.AddItem, editModDlg =>
            {
                editModDlg.Modification = new StaticMod("NotUniModMod (U)", "U", null, "C3P1O1", LabelAtoms.None, null, null);
                editModDlg.OkDialog();
            });

            // Test a terminal mod with no AA
            RunDlg<EditStaticModDlg>(editListUI.AddItem, editModDlg =>
            {
                editModDlg.Modification = new StaticMod("NotUniModMod (N-term)", null, ModTerminus.N, "H1", LabelAtoms.None, null, null);
                editModDlg.Modification = editModDlg.Modification.ChangeVariable(true);
                editModDlg.OkDialog();
            });
            OkDialog(editListUI, editListUI.OkDialog);

            // Test back/next buttons
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);

                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // We're on the MS1 full scan settings page.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // We're on the "Import FASTA" page.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.IsFalse(importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("rpal-subset.fasta"));
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // We're on the "Adjust Search Settings" page
            bool? searchSucceeded = null;
            TryWaitForOpenForm(typeof(ImportPeptideSearchDlg.DDASearchSettingsPage));   // Stop to show this form during form testing
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.dda_search_settings_page);
                importPeptideSearchDlg.SearchSettingsControl.PrecursorTolerance = new MzTolerance(15, MzTolerance.Units.ppm);
                importPeptideSearchDlg.SearchSettingsControl.FragmentTolerance = new MzTolerance(25, MzTolerance.Units.ppm);
                importPeptideSearchDlg.SearchSettingsControl.FragmentIons = "b, y";

                // Run the search
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });
            TryWaitForOpenForm(typeof(ImportPeptideSearchDlg.DDASearchPage));   // Stop to show this form during form testing
            RunUI(() =>
            {
                importPeptideSearchDlg.SearchControl.OnSearchFinished += (success) => searchSucceeded = success;
                importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches = true;

                // Cancel search
                importPeptideSearchDlg.SearchControl.Cancel();
            });

            WaitForConditionUI(60000, () => searchSucceeded.HasValue);
            Assert.IsFalse(searchSucceeded.Value);
            searchSucceeded = null;

            // Go back and test 2 input files with the same name
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());

                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFilesSameName.Select(o => (MsDataFileUri) new MsDataFilePath(o)).ToArray();
            });

            var removeSuffix = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            OkDialog(removeSuffix, removeSuffix.CancelDialog);

            // Test with 2 files (different name)
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => (MsDataFileUri)new MsDataFilePath(o)).ToArray();
            });

            // With 2 sources, we get the remove prefix/suffix dialog; accept default behavior
            var removeSuffix2 = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            OkDialog(removeSuffix, () => removeSuffix2.YesDialog());
            WaitForDocumentLoaded();

            RunUI(() =>
            {
                // We're on the "Match Modifications" page again.
                importPeptideSearchDlg.MatchModificationsControl.ChangeItem(0, false); // uncheck C+57
                importPeptideSearchDlg.MatchModificationsControl.ChangeItem(1, true); // check M+16
                importPeptideSearchDlg.MatchModificationsControl.ChangeItem(2, true); // check U+C3P0
                importPeptideSearchDlg.MatchModificationsControl.ChangeItem(3, true); // check H+1
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            WaitForConditionUI(60000, () => searchSucceeded.HasValue);
            RunUI(() => Assert.IsTrue(searchSucceeded.Value, importPeptideSearchDlg.SearchControl.LogText));

            var emptyProteinsDlg = ShowDialog<PeptidesPerProteinDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            RunUI(()=>
            {
                int proteinCount, peptideCount, precursorCount, transitionCount;
                emptyProteinsDlg.NewTargetsAll(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                if (Environment.Is64BitProcess)
                {
                    // TODO: reenable these checks for 32 bit once intermittent failures are debugged
                    if (RecordAuditLogs)
                    {
                        Console.WriteLine();
                        Console.WriteLine($@"Assert.AreEqual({proteinCount}, proteinCount);");
                        Console.WriteLine($@"Assert.AreEqual({peptideCount}, peptideCount);");
                        Console.WriteLine($@"Assert.AreEqual({precursorCount}, precursorCount);");
                        Console.WriteLine($@"Assert.AreEqual({transitionCount}, transitionCount);");
                    }
                    else
                    {
                        Assert.AreEqual(1131, proteinCount);
                        Assert.AreEqual(111, peptideCount);
                        Assert.AreEqual(111, precursorCount);
                        Assert.AreEqual(333, transitionCount);
                    }
                }
                emptyProteinsDlg.NewTargetsFinalSync(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                if (Environment.Is64BitProcess)
                {
                    if (RecordAuditLogs)
                    {
                        Console.WriteLine($@"Assert.AreEqual({proteinCount}, proteinCount);");
                        Console.WriteLine($@"Assert.AreEqual({peptideCount}, peptideCount);");
                        Console.WriteLine($@"Assert.AreEqual({precursorCount}, precursorCount);");
                        Console.WriteLine($@"Assert.AreEqual({transitionCount}, transitionCount);");
                    }
                    else
                    {
                        Assert.AreEqual(97, proteinCount);
                        Assert.AreEqual(111, peptideCount);
                        Assert.AreEqual(111, precursorCount);
                        Assert.AreEqual(333, transitionCount);
                    }
                }
            });
            WaitForConditionUI(() => emptyProteinsDlg.DocumentFinalCalculated);
            OkDialog(emptyProteinsDlg, emptyProteinsDlg.OkDialog);

            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void PrepareDocument(string documentFile)
        {
            RunUI(SkylineWindow.NewDocument);
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings", 
                doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath(documentFile)));
        }
    }
}
