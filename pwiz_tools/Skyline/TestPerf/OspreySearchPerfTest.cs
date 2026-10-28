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
    /// One Osprey search with a fine-tune at scale, as the lab runs it: three Stellar HeLa sDIA runs
    /// searched against the proteins of their peptides with a model fine-tuned on the middle run,
    /// chosen from the searched files, and the results imported. It is the time of that whole job
    /// that this test is for; the wizard's behavior (both tuning lists, the saved model added by
    /// name and searched with, the overwrite prompt) is OspreySearchTuningTest's, on Osprey's subset
    /// in a couple of minutes. On the GPU when <see cref="ENV_TORCH_RUNTIMES"/> names a CUDA runtime.
    /// Run on-screen (-ShowUI), it pauses on each page for a look at the pages; the pauses are
    /// no-ops offscreen.
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
        /// Eight of the 125 isolation windows of the regression data's Stellar runs (580.5-612.5 m/z)
        /// over the whole 24 minute gradient, 71 MB of mzML per run, and the 1,780 HeLa proteins of
        /// the peptides detected in them: cut by ai/scripts/Osprey/SubsetData/build_subset.py
        /// (--windows 8 --rt-min 0 --rt-max 24 --undetected-max 0 --real-proteins --perf-zip),
        /// 87.3 MB, SHA-256 f9fd2d9f6e2b9dc6ee8786de8c47477ee2ff567cf456c66893d425302a267f9f.
        /// </summary>
        private const string ZIP_NAME = @"osprey-stellar-subset-v1";
        private const string STELLAR_DIR = @"stellar";
        private const string FASTA_NAME = @"stellar-subset-proteins.fasta";
        private const string RUN_STEM = @"Ste-2024-12-02_HeLa_4mz_sDIA_400-900_";
        private static readonly string[] RUN_NAMES = { RUN_STEM + @"20.mzML", RUN_STEM + @"21.mzML", RUN_STEM + @"22.mzML" };

        /// <summary>
        /// The tuning search took 3.5 minutes on a 12-thread CPU; the wait is generous for a slower
        /// agent.
        /// </summary>
        private const int SEARCH_TIMEOUT_MILLIS = 30 * 60 * 1000;

        /// <summary>The chromatogram import gets more than the default three minutes on a slow agent.</summary>
        private const int IMPORT_TIMEOUT_MILLIS = 15 * 60 * 1000;

        /// <summary>The search tuned on the middle run wrote 2,523 library spectra in the wizard.</summary>
        private const int MIN_SPECTRA_THREE_RUNS = 1500;

        [TestMethod,
         NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestOspreySearchPerf()
        {
            // The zip holds a single top-level folder, so it is extracted in place beside the zip
            // (<Downloads>\Perftests\osprey-stellar-subset-v1), as DiannSearchPerfTest does, and
            // everything in it is persistent: extracted once and reused in place. The documents and
            // Osprey's working folders go to the test's own folder in TestResults.
            TestFilesZipPaths = new[] { GetPerfTestDataURL(ZIP_NAME + @".zip") };
            TestFilesZipExtractHere = new[] { true };
            TestFilesPersistent = new[] { ZIP_NAME + @"\" };
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

            string fastaFilepath = GetDataPath(FASTA_NAME);
            string[] diaFilePaths = RUN_NAMES.Select(GetDataPath).ToArray();
            foreach (var path in diaFilePaths.Append(fastaFilepath))
                AssertEx.FileExists(path);
            string middleRun = diaFilePaths[1];

            // The three runs searched, and the model tuned on the middle run, chosen from the searched
            // files: the lab's way
            OspreySearchTest.PrepareDocument(TestFilesDir.GetTestPath("OspreySearchPerfTest.sky"));
            string tunedModelPath = TuneOnSearchedRun(diaFilePaths, middleRun, fastaFilepath);
            AssertEx.FileExists(tunedModelPath);
        }

        /// <summary>A file of the persistent data folder, extracted in place beside the zip.</summary>
        private string GetDataPath(string fileName)
        {
            return TestFilesDir.GetTestPath(Path.Combine(ZIP_NAME, STELLAR_DIR, fileName));
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
            FinishImport(searchDlg, diaFilePaths, docPath, MIN_SPECTRA_THREE_RUNS);
            return modelPath;
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
