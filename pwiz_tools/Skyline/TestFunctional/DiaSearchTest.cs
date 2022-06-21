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
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class DiaSearchTest : AbstractFunctionalTest
    {
        private class TestDetails
        {
            private SearchSettingsControl.SearchEngine _searchEngine;
            public string DocumentPath { get; set; }
            public IEnumerable<string> SearchFiles { get; set; }
            public string FastaPath { get; set; }

            public SearchSettingsControl.SearchEngine SearchEngine
            {
                get => _searchEngine;
                set
                {
                    _searchEngine = value;
                    HasMissingDependencies = !SearchSettingsControl.HasRequiredFilesDownloaded(value);
                }
            }

            public MzTolerance PrecursorMzTolerance { get; set; }
            public MzTolerance FragmentMzTolerance { get; set; }
            public List<KeyValuePair<string, string>> AdditionalSettings { get; set; }

            public bool HasMissingDependencies { get; private set; }

            public class DocumentCounts
            {
                public int ProteinCount;
                public int PeptideCount;
                public int PrecursorCount;
                public int TransitionCount;

                public override string ToString()
                {
                    return $@"{{ ProteinCount={ProteinCount}, PeptideCount={PeptideCount}, PrecursorCount={PrecursorCount}, TransitionCount={TransitionCount} }}";
                }
            }
            public DocumentCounts Initial { get; set; }
            public DocumentCounts Final { get; set; }

            public Action<ImportPeptideSearchDlg, EditIsolationSchemeDlg> EditIsolationSchemeAction { get; set; }
        }

        private void SetupDiaSearchVariableWindows()
        {
            _testDetails = new TestDetails
            {
                DocumentPath = "TestVariableWindowDiaUmpire.sky",
                SearchFiles = new[]
                {
                    "collinsb_I180316_001_SW-A-subset.mz5",
                    "collinsb_I180316_002_SW-B-subset.mz5"
                },
                FastaPath = "collinsb_I180316.fasta",
                PrecursorMzTolerance = new MzTolerance(50, MzTolerance.Units.ppm),
                FragmentMzTolerance = new MzTolerance(50, MzTolerance.Units.ppm),

                EditIsolationSchemeAction = (importPeptideSearchDlg, isolationScheme) =>
                {
                    RunDlg<OpenDataSourceDialog>(isolationScheme.ImportRanges, importRangesDlg =>
                    {
                        var diaSource = importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources[0];
                        importRangesDlg.CurrentDirectory = new MsDataFilePath(Path.GetDirectoryName(diaSource.GetFilePath()));
                        importRangesDlg.SelectFile(importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources.First().GetFileName());
                        importRangesDlg.Open();
                    });
                }
            };
        }

        [TestMethod]
        public void TestDiaSearchVariableWindows()
        {
            TestFilesZip = @"TestFunctional\DiaSearchTest.zip";

            SetupDiaSearchVariableWindows();
            _testDetails.Initial = new TestDetails.DocumentCounts { ProteinCount = 877, PeptideCount = 82, PrecursorCount = 89, TransitionCount = 801 };
            _testDetails.Final = new TestDetails.DocumentCounts { ProteinCount = 78, PeptideCount = 82, PrecursorCount = 89, TransitionCount = 801 };

            RunFunctionalTest();
        }

        [TestMethod]
        public void TestDiaSearchVariableWindowsMsgfPlus()
        {
            TestFilesZip = @"TestFunctional\DiaSearchTest.zip";

            SetupDiaSearchVariableWindows();
            _testDetails.SearchEngine = SearchSettingsControl.SearchEngine.MSGFPlus;
            _testDetails.Initial = new TestDetails.DocumentCounts { ProteinCount = 877, PeptideCount = 38, PrecursorCount = 38, TransitionCount = 342 };
            _testDetails.Final = new TestDetails.DocumentCounts { ProteinCount = 38, PeptideCount = 38, PrecursorCount = 38, TransitionCount = 342 };

            RunFunctionalTest();
        }

        [TestMethod]
        public void TestDiaSearchVariableWindowsMsFragger()
        {
            TestFilesZip = @"TestFunctional\DiaSearchTest.zip";

            SetupDiaSearchVariableWindows();
            _testDetails.SearchEngine = SearchSettingsControl.SearchEngine.MSFragger;
            _testDetails.PrecursorMzTolerance = new MzTolerance(100, MzTolerance.Units.ppm);
            _testDetails.FragmentMzTolerance = new MzTolerance(25, MzTolerance.Units.ppm);
            _testDetails.Initial = new TestDetails.DocumentCounts { ProteinCount = 877, PeptideCount = 78, PrecursorCount = 91, TransitionCount = 819 };
            _testDetails.Final = new TestDetails.DocumentCounts { ProteinCount = 77, PeptideCount = 78, PrecursorCount = 91, TransitionCount = 819 };
            _testDetails.AdditionalSettings = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("check_spectral_files", "0")
            };

            RunFunctionalTest();
        }

        [TestMethod]
        public void TestDiaSearchFixedWindows()
        {
            TestFilesZip = @"TestFunctional\DiaSearchTest.zip";

            string diaUmpireTestDataPath = TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.DiaUmpire);

            _testDetails = new TestDetails
            {
                DocumentPath = "TestFixedWindowDiaUmpire.sky",
                SearchFiles = new[]
                {
                    // CONSIDER: test automatic fixed window as well as manually calculated?
                    //Path.Combine(TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.ABI), "swath.api.wiff2")

                    "Hoofnagle_10xDil_SWATH_01-20130327_Hoofnagle_10xDil_SWATH_1_01.mzXML"
                },

                FastaPath = Path.Combine(diaUmpireTestDataPath, "Hoofnagle_10xDil_SWATH.fasta"),
                PrecursorMzTolerance = new MzTolerance(50, MzTolerance.Units.ppm),
                FragmentMzTolerance = new MzTolerance(50, MzTolerance.Units.ppm),

                Initial = new TestDetails.DocumentCounts { ProteinCount = 268, PeptideCount = 93, PrecursorCount = 94, TransitionCount = 846 },
                Final = new TestDetails.DocumentCounts { ProteinCount = 90, PeptideCount = 93, PrecursorCount = 94, TransitionCount = 846 },

                EditIsolationSchemeAction = (importPeptideSearchDlg, isolationScheme) =>
                {
                    /*RunDlg<OpenDataSourceDialog>(isolationScheme.ImportRanges, importRangesDlg =>
                    {
                        var diaSource = importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources[0];
                        importRangesDlg.CurrentDirectory = new MsDataFilePath(Path.GetDirectoryName(diaSource.GetFilePath()));
                        importRangesDlg.SelectFile(importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources.First().GetFileName());
                        importRangesDlg.Open();
                    });*/
                    RunDlg<CalculateIsolationSchemeDlg>(isolationScheme.Calculate, calculateIsolationSchemeDlg =>
                    {
                        calculateIsolationSchemeDlg.Start = 400;
                        calculateIsolationSchemeDlg.End = 1100;
                        calculateIsolationSchemeDlg.WindowWidth = 25;
                        calculateIsolationSchemeDlg.OkDialog();
                    });
                }
            };

            RunFunctionalTest();
        }

        private string GetTestPath(string path)
        {
            if (File.Exists(path))
                return path;
            return TestFilesDir.GetTestPath(path);
        }

        private TestDetails _testDetails;
        public bool IsRecordMode => false;

        protected override void DoTest()
        {
            TestDiaUmpireSearch(_testDetails);
        }

        private void ValidateTargets(TestDetails.DocumentCounts targetCounts, TestDetails.DocumentCounts actualCounts, string propName)
        {
            if (IsRecordMode)
                Console.WriteLine(@"{0} = new TestDetails.DocumentCounts {1},", propName, actualCounts);
            else
            {
                if (targetCounts.ToString() != actualCounts.ToString())
                    Console.Error.WriteLine($@"Expected target counts <{targetCounts}> do not match actual <{actualCounts}>.");
                Assert.IsTrue(Math.Abs(targetCounts.ProteinCount - actualCounts.ProteinCount) <= 10);
                Assert.IsTrue(Math.Abs(targetCounts.PeptideCount - actualCounts.PeptideCount) <= 10);
                Assert.IsTrue(Math.Abs(targetCounts.PrecursorCount - actualCounts.PrecursorCount) <= 10);
                Assert.IsTrue(Math.Abs(targetCounts.TransitionCount - actualCounts.TransitionCount) <= 100);
                //Assert.Fail($@"Expected target counts <{targetCounts}> do not match actual <{actualCounts}>.");}
            }
        }

        /// <summary>
        /// Quick test that DDA search works with MSAmanda.
        /// </summary>
        private void TestDiaUmpireSearch(TestDetails testDetails)
        {
            PrepareDocument(testDetails.DocumentPath);

            // copy files from core to test location (otherwise Skyline's DiaUmpire output will overwrite core test reference files)
            string diaUmpireTestDataPath = TestFilesDir.GetVendorTestData(TestFilesDir.VendorDir.DiaUmpire);
            foreach (var sourceName in testDetails.SearchFiles)
                if (File.Exists(Path.Combine(diaUmpireTestDataPath, sourceName)))
                    File.Copy(Path.Combine(diaUmpireTestDataPath, sourceName), Path.Combine(TestFilesDir.FullPath, sourceName), true);

            // delete -diaumpire files so they get regenerated instead of reused
            foreach (var file in Directory.GetFiles(TestFilesDir.FullPath, "*-diaumpire.*"))
                FileEx.SafeDelete(file);

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);

            // We're on the "Build Spectral Library" page of the wizard.
            // Add the test xml file to the search files list and try to 
            // build the document library.

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.InputFileType = ImportPeptideSearchDlg.InputFile.dia_raw;
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = testDetails.SearchFiles
                    .Select(o => (MsDataFileUri) new MsDataFilePath(GetTestPath(o))).Take(1).ToArray();
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dia;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // With only 1 source, no add/remove prefix/suffix dialog

            // We're on the "Match Modifications" page. Add M+16 mod.
            RunUI(() =>
            {
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
            OkDialog(editListUI, editListUI.OkDialog);

            // Test back/next buttons
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);

                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // We're on the transition settings page.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.transition_settings_page);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // We're on the MS1 full scan settings page.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
            });

            string isolationSchemeName = "DiaUmpire Test Scheme";
            RunUI(() => importPeptideSearchDlg.FullScanSettingsControl.ComboIsolationSchemeSetFocus());
            var isolationScheme = ShowDialog<EditIsolationSchemeDlg>(importPeptideSearchDlg.FullScanSettingsControl.AddIsolationScheme);

            RunUI(() =>
            {
                isolationScheme.IsolationSchemeName = isolationSchemeName;
                isolationScheme.UseResults = false;
            });

            testDetails.EditIsolationSchemeAction(importPeptideSearchDlg, isolationScheme);
            WaitForConditionUI(10000, () => isolationScheme.GetIsolationWindows().Any());

            var isolationGraph = ShowDialog<DiaIsolationWindowsGraphForm>(isolationScheme.OpenGraph);

            OkDialog(isolationGraph, isolationGraph.CloseButton);
            OkDialog(isolationScheme, isolationScheme.OkDialog);

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // We're on the "Import FASTA" page.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                // Assert.IsFalse(importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath(testDetails.FastaPath));
                importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages = 0;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // We're on the Converter settings page.
            TryWaitForOpenForm(typeof(ImportPeptideSearchDlg.ConverterSettingsPage));   // Stop to show this form during form testing
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.converter_settings_page);

                importPeptideSearchDlg.ConverterSettingsControl.InstrumentPreset = DiaUmpire.Config.InstrumentPreset.TripleTOF;
                importPeptideSearchDlg.ConverterSettingsControl.EstimateBackground = false;
                importPeptideSearchDlg.ConverterSettingsControl.UseMzMlSpillFile = true; // mz5 spill file leaks
                importPeptideSearchDlg.ConverterSettingsControl.AdditionalSettings =
                    new Dictionary<string, AbstractDdaSearchEngine.Setting>
                    {
                        {"MS1PPM", new AbstractDdaSearchEngine.Setting("MS1PPM", 50, 0, 1000)},
                        {"MS2PPM", new AbstractDdaSearchEngine.Setting("MS2PPM", 50, 0, 1000)},
                        //{"NoMissedScan", new AbstractDdaSearchEngine.Setting("NoMissedScan", 2, 0, 10)},
                        {"MaxCurveRTRange", new AbstractDdaSearchEngine.Setting("MaxCurveRTRange", 4, 0, 10)},
                        {"RTOverlap", new AbstractDdaSearchEngine.Setting("RTOverlap", 0.05, 0, 10)},
                        {"CorrThreshold", new AbstractDdaSearchEngine.Setting("CorrThreshold", 0.1, 0, 10)},
                        {"DeltaApex", new AbstractDdaSearchEngine.Setting("DeltaApex", 0.6, 0, 10)},
                    };
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // We're on the Adjust search settings page
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.dda_search_settings_page);
            });

            SkylineWindow.BeginInvoke(new Action(() => importPeptideSearchDlg.SearchSettingsControl.SelectedSearchEngine = testDetails.SearchEngine));

            if (testDetails.HasMissingDependencies)
            {
                if (testDetails.SearchEngine == SearchSettingsControl.SearchEngine.MSFragger)
                {
                    var msfraggerDownloaderDlg = TryWaitForOpenForm<MsFraggerDownloadDlg>(2000);
                    if (msfraggerDownloaderDlg != null)
                    {
                        RunUI(() => msfraggerDownloaderDlg.SetValues("Matt Chambers (testing download from Skyline)", "matt.chambers42@gmail.com", "UW"));
                        OkDialog(msfraggerDownloaderDlg, msfraggerDownloaderDlg.ClickAccept);
                    }
                }

                if (testDetails.SearchEngine != SearchSettingsControl.SearchEngine.MSAmanda)
                {
                    var downloaderDlg = TryWaitForOpenForm<MultiButtonMsgDlg>(2000);
                    if (downloaderDlg != null)
                    {
                        OkDialog(downloaderDlg, downloaderDlg.ClickYes);
                        var waitDlg = WaitForOpenForm<LongWaitDlg>();
                        WaitForClosedForm(waitDlg);
                    }
                }
            }

            RunUI(() =>
            {
                importPeptideSearchDlg.SearchSettingsControl.PrecursorTolerance = testDetails.PrecursorMzTolerance;
                importPeptideSearchDlg.SearchSettingsControl.FragmentTolerance = testDetails.FragmentMzTolerance;
                //importPeptideSearchDlg.SearchSettingsControl.FragmentIons = "b, y";
            });

            WaitForConditionUI(() => testDetails.FragmentMzTolerance.Unit == importPeptideSearchDlg.SearchSettingsControl.FragmentTolerance.Unit);

            bool? searchSucceeded = null;
            RunUI(() =>
            {
                // Run the search
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                importPeptideSearchDlg.SearchControl.OnSearchFinished += (success) => searchSucceeded = success;
                importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches = true;

                // Cancel search
                importPeptideSearchDlg.SearchControl.Cancel();
            });

            WaitForConditionUI(60000, () => searchSucceeded.HasValue, 
                () => importPeptideSearchDlg.SearchControl.LogText);
            RunUI(() => Assert.IsFalse(searchSucceeded.Value, importPeptideSearchDlg.SearchControl.LogText));
            searchSucceeded = null;

            // Go back and add another file
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // now on search settings
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // now on converter settings
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // now on import FASTA
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // now on full scan settings
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // now on transition settings
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // now on modifications
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // now on input files
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);

                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = testDetails.SearchFiles.Select(o => (MsDataFileUri) new MsDataFilePath(GetTestPath(o))).ToArray();
            });


            // With 2 sources, we get the remove prefix/suffix dialog; accept default behavior
            if (testDetails.SearchFiles.Count() > 1)
            {
                var removeSuffix = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton()); // now on remove prefix/suffix dialog
                OkDialog(removeSuffix, () => removeSuffix.YesDialog()); // now on modifications
                WaitForDocumentLoaded();
            }
            else
                RunUI(() => importPeptideSearchDlg.ClickNextButton());

            RunUI(() =>
            {
                // We're on the "Match Modifications" page again.
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage ==
                              ImportPeptideSearchDlg.Pages.match_modifications_page);
                importPeptideSearchDlg.MatchModificationsControl.ChangeItem(0, true); // uncheck C+57
                importPeptideSearchDlg.MatchModificationsControl.ChangeItem(1, true); // check M+16
                importPeptideSearchDlg.MatchModificationsControl.ChangeItem(2, true); // check U+C3P0

                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // now on transition settings
                importPeptideSearchDlg.TransitionSettingsControl.IonRangeFrom = TransitionFilter.StartFragmentFinder.ION_1.Label;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // now on full scan settings

                // re-select isolation scheme after FullScanSettingsControl reset
                importPeptideSearchDlg.FullScanSettingsControl.IsolationSchemeName = isolationSchemeName;
            });

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // now on import FASTA
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // now on converter settings
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // now on search settings
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // now on search progress
            });

            WaitForConditionUI(120000, () => searchSucceeded.HasValue, () => importPeptideSearchDlg.SearchControl.LogText);

            RunUI(() => Assert.IsTrue(searchSucceeded.Value, importPeptideSearchDlg.SearchControl.LogText));

            if(IsRecordMode)
                Console.WriteLine();

            RunDlg<PeptidesPerProteinDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck, emptyProteinsDlg =>
            {
                var aic = new TestDetails.DocumentCounts();

                emptyProteinsDlg.NewTargetsAll(out aic.ProteinCount, out aic.PeptideCount, out aic.PrecursorCount, out aic.TransitionCount);
                if (Environment.Is64BitProcess)
                    // TODO: reenable these checks for 32 bit once intermittent failures are debugged
                    ValidateTargets(testDetails.Initial, aic, "_testDetails.Initial");

                emptyProteinsDlg.NewTargetsFinalSync(out aic.ProteinCount, out aic.PeptideCount, out aic.PrecursorCount, out aic.TransitionCount);

                WaitForCondition(() => emptyProteinsDlg.DocumentFinalCalculated); // Dialog won't actually close on OkDialog until this is set

                if (Environment.Is64BitProcess)
                    ValidateTargets(testDetails.Final, aic, "_testDetails.Final");

                emptyProteinsDlg.OkDialog();
            });

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
