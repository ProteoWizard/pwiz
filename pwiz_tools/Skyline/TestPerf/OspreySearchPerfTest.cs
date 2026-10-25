/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestFunctional;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    /// <summary>
    /// The Osprey Search wizard's Library Model page at real-data scale: the three Stellar HeLa sDIA
    /// runs (1.5 GB of mzML each) searched against the filtered HeLa FASTA, with a model fine-tuned
    /// on the middle run chosen from the searched files; then the outer runs searched with the middle
    /// run browsed in and held out of the search, which must train the same model tensor for tensor;
    /// then the three runs searched with the saved model. OspreySearchTuningTest covers the page's
    /// behavior on Osprey's subset in a minute or two; this is the run as a user makes it, where a
    /// run searched alone keeps enough precursors to tune on.
    /// </summary>
    [TestClass]
    public class OspreySearchPerfTest : AbstractFunctionalTestEx
    {
        private const string ZIP_NAME = @"osprey-testfiles-mzML-v2";
        private const string STELLAR_DIR = @"stellar";
        private const string FASTA_NAME = @"hela-filtered.fasta";
        private const string RUN_STEM = @"Ste-2024-12-02_HeLa_4mz_sDIA_400-900_";
        private static readonly string[] RUN_NAMES = { RUN_STEM + @"20.mzML", RUN_STEM + @"21.mzML", RUN_STEM + @"22.mzML" };

        /// <summary>
        /// Osprey searched the three runs untuned in 3 min 42 s on the development machine. A tuning
        /// search adds a first search of the tuning run and the fine-tune itself, on the CPU when the
        /// torch runtimes folder has no CUDA runtime, so the wait is generous.
        /// </summary>
        private const int SEARCH_TIMEOUT_MILLIS = 60 * 60 * 1000;

        /// <summary>The runs are 1.5 GB each, so the chromatogram import also gets more than the default three minutes.</summary>
        private const int IMPORT_TIMEOUT_MILLIS = 60 * 60 * 1000;

        /// <summary>
        /// The untuned search of the three runs wrote 29,884 library spectra, and the search tuned on
        /// the middle run 35,473, so 20,000 is a clear lower bound for three runs. Two runs cannot
        /// yield fewer spectra than either one alone, and half the three-run bound keeps the margin.
        /// </summary>
        private const int MIN_SPECTRA_THREE_RUNS = 20000;
        private const int MIN_SPECTRA_TWO_RUNS = 10000;

        [TestMethod,
         NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestOspreySearchPerf()
        {
            // The zip holds a single top-level folder, so it is extracted in place beside the zip
            // (<Downloads>\Perftests\osprey-testfiles-mzML-v2), as DiannSearchPerfTest does, and
            // everything in it is persistent: 26 GB of data extracted once and reused in place.
            // The documents and Osprey's working folders go to the test's own folder in TestResults.
            TestFilesZipPaths = new[] { GetPerfTestDataURL(ZIP_NAME + @".zip") };
            TestFilesZipExtractHere = new[] { true };
            TestFilesPersistent = new[] { ZIP_NAME + @"\" };
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            OspreySearchTest.RegisterOsprey(OspreySearchTest.EnsureOsprey(TestContext));
            RunUI(() =>
            {
                // The model list starts with the pretrained models alone
                Settings.Default.CarafeModelList.Clear();
                Settings.Default.CarafeModelList.AddDefaults();
            });

            string fastaFilepath = GetDataPath(FASTA_NAME);
            string[] diaFilePaths = RUN_NAMES.Select(GetDataPath).ToArray();
            foreach (var path in diaFilePaths.Append(fastaFilepath))
                AssertEx.FileExists(path);
            string middleRun = diaFilePaths[1];
            string[] outerRuns = { diaFilePaths[0], diaFilePaths[2] };

            // Scenario 1: the three runs searched, and the model tuned on the middle run, chosen
            // from the searched files.
            OspreySearchTest.PrepareDocument(TestFilesDir.GetTestPath("OspreySearchPerfTest.sky"));
            var modelFromSearched = TuneOnSearchedRun(diaFilePaths, middleRun, fastaFilepath);

            // Scenario 2: the outer runs searched, and the model tuned on the middle run, browsed
            // in, so it is held out of the search. The same run trains the same tensors.
            OspreySearchTest.PrepareDocument(TestFilesDir.GetTestPath("OspreySearchPerfTest-heldout.sky"));
            var modelFromBrowsed = TuneOnHeldOutRun(outerRuns, middleRun, fastaFilepath);
            OspreySearchTuningTest.AssertSameModelTensors(modelFromSearched.FilePath, modelFromBrowsed.FilePath);

            // Scenario 3: the three runs searched with the saved model from scenario 1.
            OspreySearchTest.PrepareDocument(TestFilesDir.GetTestPath("OspreySearchPerfTest-saved.sky"));
            SearchWithSavedModel(diaFilePaths, fastaFilepath, modelFromSearched);
        }

        /// <summary>A file of the persistent data folder, extracted in place beside the zip.</summary>
        private string GetDataPath(string fileName)
        {
            return TestFilesDir.GetTestPath(Path.Combine(ZIP_NAME, STELLAR_DIR, fileName));
        }

        /// <summary>
        /// "Choose from the searched files" lists the searched runs unchecked; the one checked run
        /// is tuned on, and names the model by default.
        /// </summary>
        private CarafeModelSpec TuneOnSearchedRun(string[] diaFilePaths, string tuningRun, string fastaFilepath)
        {
            string docPath = SkylineWindow.DocumentFilePath;
            string modelName = OspreySearchTuningTest.GetDefaultModelName(docPath, tuningRun);
            var searchDlg = OspreySearchTuningTest.OpenWizardOnLibraryModelPage(diaFilePaths, fastaFilepath);
            RunUI(() =>
            {
                AssertEx.IsTrue(searchDlg.UseExistingModel);
                AssertEx.IsTrue(searchDlg.Model.IsDefault);
                searchDlg.UseExistingModel = false;
                searchDlg.ChooseFromSearchedFiles = true;
                AssertEx.AreEqualDeep(diaFilePaths, searchDlg.TuningFileItems);
                AssertEx.AreEqual(0, searchDlg.TuningFiles.Count);
                searchDlg.SetTuningFileChecked(tuningRun, true);
                AssertEx.AreEqualDeep(new[] { tuningRun }, searchDlg.TuningFiles);
                AssertEx.AreEqual(modelName, searchDlg.ModelName);
            });
            OspreySearchTest.AdvanceToSearchSettings(searchDlg);
            OspreySearchTuningTest.VerifyCommandLine(searchDlg,diaFilePaths, new[] { tuningRun }, null);
            var spec = OspreySearchTuningTest.RunTuningSearch(searchDlg, modelName,
                TestFilesDir.GetTestPath("OspreySearchControlLog-searched.txt"), SEARCH_TIMEOUT_MILLIS);
            FinishImport(searchDlg, diaFilePaths, docPath, MIN_SPECTRA_THREE_RUNS);
            return spec;
        }

        /// <summary>
        /// A browsed run arrives at the top of the tuning list, checked, and names the model; it is
        /// tuned on without being searched.
        /// </summary>
        private CarafeModelSpec TuneOnHeldOutRun(string[] searchedRuns, string tuningRun, string fastaFilepath)
        {
            string docPath = SkylineWindow.DocumentFilePath;
            string modelName = OspreySearchTuningTest.GetDefaultModelName(docPath, tuningRun);
            var searchDlg = OspreySearchTuningTest.OpenWizardOnLibraryModelPage(searchedRuns, fastaFilepath);
            RunUI(() =>
            {
                searchDlg.UseExistingModel = false;
                searchDlg.AddTuningFiles(new[] { tuningRun });
                AssertEx.AreEqualDeep(new[] { tuningRun }, searchDlg.TuningFileItems);
                AssertEx.AreEqualDeep(new[] { tuningRun }, searchDlg.TuningFiles);
                AssertEx.AreEqual(modelName, searchDlg.ModelName);
            });
            OspreySearchTest.AdvanceToSearchSettings(searchDlg);
            OspreySearchTuningTest.VerifyCommandLine(searchDlg,searchedRuns, new[] { tuningRun }, null);
            var spec = OspreySearchTuningTest.RunTuningSearch(searchDlg, modelName,
                TestFilesDir.GetTestPath("OspreySearchControlLog-heldout.txt"), SEARCH_TIMEOUT_MILLIS);
            FinishImport(searchDlg, searchedRuns, docPath, MIN_SPECTRA_TWO_RUNS);
            return spec;
        }

        /// <summary>The saved model is predicted with as it is: the command names it and no tuning runs, and no model is written.</summary>
        private void SearchWithSavedModel(string[] diaFilePaths, string fastaFilepath, CarafeModelSpec savedModel)
        {
            string docPath = SkylineWindow.DocumentFilePath;
            var searchDlg = OspreySearchTuningTest.OpenWizardOnLibraryModelPage(diaFilePaths, fastaFilepath);
            RunUI(() =>
            {
                // The pretrained default and the two models the tuning searches saved
                AssertEx.AreEqual(3, Settings.Default.CarafeModelList.Count);
                searchDlg.Model = savedModel;
                AssertEx.AreEqual(savedModel.FilePath, searchDlg.Model.FilePath);
                AssertEx.IsTrue(searchDlg.UseExistingModel);
            });
            OspreySearchTest.AdvanceToSearchSettings(searchDlg);
            OspreySearchTuningTest.VerifyCommandLine(searchDlg,diaFilePaths, null, savedModel.FilePath);
            string docBlibPath = BiblioSpecLiteSpec.GetLibraryFileName(docPath);
            string workDir = OspreyHelpers.GetWorkDirectory(docPath);
            OspreySearchTest.RunSearch(searchDlg, docBlibPath, workDir,
                TestFilesDir.GetTestPath("OspreySearchControlLog-saved.txt"), SEARCH_TIMEOUT_MILLIS);
            AssertEx.IsFalse(File.Exists(OspreyHelpers.GetTunedModelPath(docBlibPath, workDir)), @"a search with a saved model wrote a model");
            FinishImport(searchDlg, diaFilePaths, docPath, MIN_SPECTRA_THREE_RUNS);
        }

        /// <summary>
        /// Finish imports the searched runs; the document library then holds one file per run and
        /// at least <paramref name="minSpectra"/> spectra. The count is written to the test output
        /// for the record.
        /// </summary>
        private static void FinishImport(OspreySearchDlg searchDlg, string[] searchedRuns, string docPath, int minSpectra)
        {
            OspreySearchTest.FinishImport(searchDlg, searchedRuns, BiblioSpecLiteSpec.GetLibraryFileName(docPath), IMPORT_TIMEOUT_MILLIS);
            var docLib = OspreySearchTest.GetDocumentLibrary();
            Assert.IsNotNull(docLib, @"the document library is not loaded");
            AssertEx.AreEqual(searchedRuns.Length, docLib.LibraryFiles.Count);
            Console.WriteLine(@"{0}: {1} library spectra from {2} runs", Path.GetFileName(docPath), docLib.SpectrumCount, searchedRuns.Length);
            AssertEx.IsTrue(docLib.SpectrumCount >= minSpectra,
                string.Format(@"the document library of {0} holds {1} spectra, fewer than the {2} expected", Path.GetFileName(docPath), docLib.SpectrumCount, minSpectra));
        }
    }
}
