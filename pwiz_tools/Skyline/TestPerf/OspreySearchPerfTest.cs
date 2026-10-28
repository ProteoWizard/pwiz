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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestFunctional;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    /// <summary>
    /// The Osprey Search wizard's Library Model page at real-data scale, as the lab runs it: the three
    /// Stellar HeLa sDIA runs (1.5 GB of mzML each) searched against the filtered HeLa FASTA with a
    /// model fine-tuned on the middle run, chosen from the searched files; then the three runs
    /// searched again with that model, added to the model list by name. OspreySearchTuningTest covers
    /// the page's behavior on Osprey's subset in a minute, where a run searched alone keeps nothing
    /// to tune on; this is the run as a user makes it, with one fine-tune, on the GPU when
    /// <see cref="ENV_TORCH_RUNTIMES"/> names a CUDA runtime. Run on-screen (-ShowUI), it pauses on
    /// each page of the first search for a look at the pages; the pauses are no-ops offscreen.
    /// </summary>
    [TestClass]
    public class OspreySearchPerfTest : AbstractFunctionalTestEx
    {
        /// <summary>
        /// Names a runtimes folder holding the installed CUDA runtime, the folder with the
        /// <c>libtorch-cuda-12.8-win-x64.2.10.0</c> subfolder in it (Osprey's own is
        /// <c>%LOCALAPPDATA%\Osprey\torch</c>), so the test predicts on the GPU; unset, the test's Tools
        /// folder has none and Osprey predicts on the CPU, where the fine-tune takes about ten minutes.
        /// </summary>
        public const string ENV_TORCH_RUNTIMES = @"SKYLINE_TEST_OSPREY_TORCH_RUNTIMES";

        /// <summary>
        /// Names a local zip searched instead of the regression data: the same layout, a single
        /// top-level folder holding <c>stellar</c> with the three runs and one FASTA, as
        /// ai/scripts/Osprey/SubsetData cuts a mid-size Stellar subset. Unset, the test downloads
        /// and searches the full regression data.
        /// </summary>
        public const string ENV_DATA_ZIP = @"SKYLINE_TEST_OSPREY_PERF_ZIP";

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
        /// the middle run 35,473 (regression.ps1's digest) or 32,519 (the wizard's default digest), so
        /// 20,000 is a clear lower bound for three runs.
        /// </summary>
        private const int MIN_SPECTRA_THREE_RUNS = 20000;

        /// <summary>
        /// Eight windows over the whole gradient, with the proteins of the peptides they detect,
        /// wrote 2,671 to 2,707 library spectra searched tuned or with the saved model.
        /// </summary>
        private const int MIN_SPECTRA_THREE_RUNS_SUBSET = 1500;

        /// <summary>The name the test gives the document's fine-tuned model when it adds it to the model list.</summary>
        private const string SAVED_MODEL_NAME = @"Stellar, tuned on the middle run";

        [TestMethod,
         NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestOspreySearchPerf()
        {
            // The zip holds a single top-level folder, so it is extracted in place beside the zip
            // (<Downloads>\Perftests\osprey-testfiles-mzML-v2), as DiannSearchPerfTest does, and
            // everything in it is persistent: 26 GB of data extracted once and reused in place.
            // The documents and Osprey's working folders go to the test's own folder in TestResults.
            TestFilesZipPaths = new[] { DataZipPath ?? GetPerfTestDataURL(ZIP_NAME + @".zip") };
            TestFilesZipExtractHere = new[] { true };
            TestFilesPersistent = new[] { DataFolderName + @"\" };
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            OspreyTestUtil.RegisterOsprey(OspreyTestUtil.EnsureOsprey(TestContext));
            RunUI(() =>
            {
                // The model list starts with the pretrained models alone
                Settings.Default.CarafeModelList.Clear();
                Settings.Default.CarafeModelList.AddDefaults();
            });

            // A subset carries the proteins of its own peptides, under whatever name it gave them
            string fastaFilepath = DataZipPath != null
                ? Directory.GetFiles(GetDataPath(string.Empty), @"*.fasta").Single()
                : GetDataPath(FASTA_NAME);
            string[] diaFilePaths = RUN_NAMES.Select(GetDataPath).ToArray();
            foreach (var path in diaFilePaths.Append(fastaFilepath))
                AssertEx.FileExists(path);
            string middleRun = diaFilePaths[1];

            // The three runs searched, and the model tuned on the middle run, chosen from the searched
            // files: the lab's way. (The subset test covers the other list, browsed runs held out of
            // the search; a second fine-tune here would double the run.)
            OspreySearchTest.PrepareDocument(TestFilesDir.GetTestPath("OspreySearchPerfTest.sky"));
            string tunedModelPath = TuneOnSearchedRun(diaFilePaths, middleRun, fastaFilepath);

            // The three runs searched with that model, added to the model list by name: a model tuned
            // on one run serving a search of all three, with no training.
            OspreySearchTest.PrepareDocument(TestFilesDir.GetTestPath("OspreySearchPerfTest-saved.sky"));
            SearchWithSavedModel(diaFilePaths, fastaFilepath, tunedModelPath);
        }

        /// <summary>A file of the persistent data folder, extracted in place beside the zip.</summary>
        private string GetDataPath(string fileName)
        {
            return TestFilesDir.GetTestPath(Path.Combine(DataFolderName, STELLAR_DIR, fileName));
        }

        /// <summary>
        /// Page by page, pausing on each on-screen: the three runs on the data files page, the FASTA,
        /// and on the Library Model page "Choose from the searched files" listing the runs unchecked,
        /// then the middle run checked as the one tuning run. Returns the document's fine-tuned model.
        /// </summary>
        private string TuneOnSearchedRun(string[] diaFilePaths, string tuningRun, string fastaFilepath)
        {
            string docPath = SkylineWindow.DocumentFilePath;
            var searchDlg = ShowDialog<OspreySearchDlg>(SkylineWindow.ShowOspreySearchDlg);
            RunUI(() =>
            {
                AssertEx.AreEqual(OspreySearchDlg.Pages.data_files_page, searchDlg.CurrentPage);
                searchDlg.DataFileResults.FoundResultsFiles = diaFilePaths
                    .Select(p => new ImportPeptideSearch.FoundResultsFile(Path.GetFileNameWithoutExtension(p), p)).ToArray();
            });
            RunUI(() =>
            {
                searchDlg.NextPage();
                AssertEx.AreEqual(OspreySearchDlg.Pages.fasta_page, searchDlg.CurrentPage);
                searchDlg.ImportFastaControl.SetFastaContent(fastaFilepath);
            });
            RunUI(() =>
            {
                searchDlg.NextPage();
                AssertEx.AreEqual(OspreySearchDlg.Pages.library_model_page, searchDlg.CurrentPage);
                AssertEx.IsTrue(searchDlg.UseExistingModel);
                AssertEx.IsTrue(searchDlg.Model.IsDefault);
            });
            RunUI(() =>
            {
                searchDlg.UseExistingModel = false;
                searchDlg.ChooseFromSearchedFiles = true;
                AssertEx.AreEqualDeep(diaFilePaths, searchDlg.TuningFileItems);
                AssertEx.AreEqual(0, searchDlg.TuningFiles.Count);
            });
            RunUI(() =>
            {
                searchDlg.SetTuningFileChecked(tuningRun, true);
                AssertEx.AreEqualDeep(new[] { tuningRun }, searchDlg.TuningFiles);
            });
            ChooseDevice(searchDlg);
            RunUI(() =>
            {
                searchDlg.NextPage();
                AssertEx.AreEqual(OspreySearchDlg.Pages.modifications_page, searchDlg.CurrentPage);
            });
            OspreySearchTest.AdvanceToSearchSettings(searchDlg);
            OspreySearchTuningTest.VerifyCommandLine(searchDlg, diaFilePaths, new[] { tuningRun }, null);
            VerifyDevice(searchDlg);
            string modelPath = OspreySearchTuningTest.RunTuningSearch(searchDlg,
                TestFilesDir.GetTestPath("OspreySearchControlLog-searched.txt"), SEARCH_TIMEOUT_MILLIS);
            FinishImport(searchDlg, diaFilePaths, docPath, MinSpectra);
            return modelPath;
        }

        /// <summary>
        /// The document's model from the tuning search is added to the model list by name through
        /// the combo's Add..., and predicted with as it is: the command names it and no tuning runs,
        /// and no model is written.
        /// </summary>
        private void SearchWithSavedModel(string[] diaFilePaths, string fastaFilepath, string savedModelPath)
        {
            string docPath = SkylineWindow.DocumentFilePath;
            var searchDlg = OspreySearchTuningTest.OpenWizardOnLibraryModelPage(diaFilePaths, fastaFilepath);
            RunDlg<EditCarafeModelDlg>(searchDlg.AddModel, dlg =>
            {
                dlg.ModelName = SAVED_MODEL_NAME;
                dlg.ModelPath = savedModelPath;
                dlg.OkDialog();
            });
            RunUI(() =>
            {
                // The pretrained default and the model just added, selected
                AssertEx.AreEqual(2, Settings.Default.CarafeModelList.Count);
                AssertEx.AreEqual(SAVED_MODEL_NAME, searchDlg.Model.Name);
                AssertEx.AreEqual(savedModelPath, searchDlg.Model.FilePath);
                AssertEx.IsTrue(searchDlg.UseExistingModel);
            });
            ChooseDevice(searchDlg);
            OspreySearchTest.AdvanceToSearchSettings(searchDlg);
            OspreySearchTuningTest.VerifyCommandLine(searchDlg, diaFilePaths, null, savedModelPath);
            VerifyDevice(searchDlg);
            string docBlibPath = BiblioSpecLiteSpec.GetLibraryFileName(docPath);
            string workDir = OspreyHelpers.GetWorkDirectory(docPath);
            OspreySearchTest.RunSearch(searchDlg, docBlibPath, workDir,
                TestFilesDir.GetTestPath("OspreySearchControlLog-saved.txt"), SEARCH_TIMEOUT_MILLIS);
            AssertEx.IsFalse(File.Exists(OspreyHelpers.GetTunedModelPath(docBlibPath, workDir)), @"a search with a saved model wrote a model");
            FinishImport(searchDlg, diaFilePaths, docPath, MinSpectra);
        }

        /// <summary>The zip <see cref="ENV_DATA_ZIP"/> names, or null for the regression data.</summary>
        private static string DataZipPath
        {
            get
            {
                string path = Environment.GetEnvironmentVariable(ENV_DATA_ZIP);
                return string.IsNullOrEmpty(path) ? null : path;
            }
        }

        /// <summary>The zip's single top-level folder, named for the zip.</summary>
        private static string DataFolderName
        {
            get { return DataZipPath != null ? Path.GetFileNameWithoutExtension(DataZipPath) : ZIP_NAME; }
        }

        /// <summary>The fewest library spectra three runs of the searched data may report.</summary>
        private static int MinSpectra
        {
            get { return DataZipPath != null ? MIN_SPECTRA_THREE_RUNS_SUBSET : MIN_SPECTRA_THREE_RUNS; }
        }

        /// <summary>The folder <see cref="ENV_TORCH_RUNTIMES"/> names, or null for the test's own Tools folder and the CPU.</summary>
        private static string TorchRuntimesDirectory
        {
            get
            {
                string directory = Environment.GetEnvironmentVariable(ENV_TORCH_RUNTIMES);
                return string.IsNullOrEmpty(directory) ? null : directory;
            }
        }

        /// <summary>
        /// On the Library Model page: with <see cref="ENV_TORCH_RUNTIMES"/> set, points the wizard at
        /// that runtimes folder and checks Use GPU, which must then stay checked (the folder holds the
        /// runtime and this computer's GPU can use it); otherwise leaves the CPU default alone.
        /// </summary>
        private static void ChooseDevice(OspreySearchDlg searchDlg)
        {
            string directory = TorchRuntimesDirectory;
            if (directory == null)
                return;
            RunUI(() =>
            {
                var runtimes = new TorchRuntimes { RuntimesDirectory = directory };
                AssertEx.IsTrue(runtimes.CanUseCuda, ENV_TORCH_RUNTIMES + @" is set but this computer's GPU cannot predict: " + runtimes.GetCudaUnavailableReason());
                AssertEx.IsTrue(runtimes.IsCudaInstalled, ENV_TORCH_RUNTIMES + @" names a folder without the CUDA runtime: " + directory);
                searchDlg.TorchRuntimes = runtimes;
                searchDlg.UseGpu = true;
                AssertEx.IsTrue(searchDlg.UseGpu);
            });
        }

        /// <summary>The command names the device chosen and, on the GPU, the runtimes folder the variable named.</summary>
        private static void VerifyDevice(OspreySearchDlg searchDlg)
        {
            OspreyConfig config = null;
            RunUI(() => config = searchDlg.GetSearchConfig());
            string commandLine = OspreyHelpers.BuildCommandLine(config);
            string directory = TorchRuntimesDirectory;
            AssertEx.Contains(commandLine, OspreyHelpers.ARG_DEVICE + @" " + (directory != null ? OspreyHelpers.DEVICE_GPU : OspreyHelpers.DEVICE_CPU) + @" ");
            if (directory != null)
                AssertEx.Contains(commandLine, OspreyHelpers.ARG_TORCH_RUNTIMES + @" " + directory.Quote() + @" ");
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
