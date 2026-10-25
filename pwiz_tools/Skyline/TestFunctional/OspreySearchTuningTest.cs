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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.BiblioSpec.IO;
using pwiz.CarafeSharp.Models;
using pwiz.CarafeSharp.Proteome;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// The Osprey Search wizard's Library Model page on Osprey's Stellar subset: a model fine-tuned
    /// on the searched runs (chosen from the searched files), a model fine-tuned on browsed runs
    /// with one held out of the search, and a search that predicts with the saved model. On the
    /// subset a single run searched alone keeps no precursor at 1% in the first pass, so both
    /// tuning searches tune on all three runs, and two trainings on the same runs produce the same
    /// model tensors, which is how the held-out tuning is shown to be the same tuning.
    /// </summary>
    [TestClass]
    public class OspreySearchTuningTest : AbstractFunctionalTestEx
    {
        /// <summary>The LibMetadata key CarafeSharp writes the command that made a predicted library under.</summary>
        private const string KEY_CARAFE_COMMAND = @"carafe.command";
        private const string PREDICTED_LIBRARY_SUFFIX = @".predicted";
        private const string MODEL_NAME_HELD_OUT = @"Held-out middle run";

        [TestMethod,
         NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestOspreySearchTuning()
        {
            TestFilesZip = @"TestFunctional\OspreySearchTest.zip";
            TestFilesZipSuffix = @"-tuning";
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

            string fastaFilepath = TestFilesDir.GetTestPath("stellar-subset-proteins.fasta");
            string[] diaFilePaths = Directory.GetFiles(TestFilesDir.FullPath, "*.mzML").OrderBy(p => p).ToArray();
            AssertEx.AreEqual(3, diaFilePaths.Length);
            string[] outerRuns = { diaFilePaths[0], diaFilePaths[2] };

            // Scenario 1: the three runs searched, and the model tuned on all of them, chosen from
            // the searched files.
            OspreySearchTest.PrepareDocument(TestFilesDir.GetTestPath("OspreySearchTuningTest.sky"));
            var modelFromSearched = TuneOnSearchedRuns(diaFilePaths, fastaFilepath);

            // Scenario 2: the outer runs searched, and the model tuned on all three, browsed in, so
            // the middle run is held out of the search.
            OspreySearchTest.PrepareDocument(TestFilesDir.GetTestPath("OspreySearchTuningTest-heldout.sky"));
            var modelFromBrowsed = TuneOnBrowsedRuns(outerRuns, diaFilePaths, fastaFilepath, modelFromSearched.Name);
            AssertSameModelTensors(modelFromSearched.FilePath, modelFromBrowsed.FilePath);

            // Scenario 3: the three runs searched with the saved model from scenario 1.
            OspreySearchTest.PrepareDocument(TestFilesDir.GetTestPath("OspreySearchTuningTest-saved.sky"));
            SearchWithSavedModel(diaFilePaths, fastaFilepath, modelFromSearched);
        }

        /// <summary>
        /// The tuning list starts empty; "Choose from the searched files" lists the searched runs,
        /// unchecked, and the checked ones are tuned on. On the way, the page's refusals: no run
        /// checked, a browsed run that does not exist, a browsed run whose file name a searched run
        /// shares, and no name. Browsed runs go to the top, checked, and name the model; an unwanted
        /// one is unchecked, not removed.
        /// </summary>
        private CarafeModelSpec TuneOnSearchedRuns(string[] diaFilePaths, string fastaFilepath)
        {
            string docPath = SkylineWindow.DocumentFilePath;
            // Page by page, with a pause on each when the test is run on-screen (-ShowUI) for a review
            // of the layout; the pauses are no-ops offscreen
            var searchDlg = ShowDialog<OspreySearchDlg>(SkylineWindow.ShowOspreySearchDlg);
            RunUI(() =>
            {
                AssertEx.AreEqual(OspreySearchDlg.Pages.data_files_page, searchDlg.CurrentPage);
                searchDlg.DataFileResults.FoundResultsFiles = diaFilePaths
                    .Select(p => new ImportPeptideSearch.FoundResultsFile(Path.GetFileNameWithoutExtension(p), p)).ToArray();
            });
            PauseTest("Data files page");
            RunUI(() =>
            {
                searchDlg.NextPage();
                AssertEx.AreEqual(OspreySearchDlg.Pages.fasta_page, searchDlg.CurrentPage);
                searchDlg.ImportFastaControl.SetFastaContent(fastaFilepath);
            });
            PauseTest("FASTA page");
            RunUI(() =>
            {
                searchDlg.NextPage();
                AssertEx.AreEqual(OspreySearchDlg.Pages.library_model_page, searchDlg.CurrentPage);
                // The defaults: the pretrained models, used as they are, no tuning runs, and the CPU
                // (a test's Tools folder has no CUDA runtime)
                AssertEx.IsTrue(searchDlg.UseExistingModel);
                AssertEx.IsTrue(searchDlg.Model.IsDefault);
                AssertEx.AreEqual(0, searchDlg.TuningFileItems.Count);
                AssertEx.IsFalse(searchDlg.UseGpu);
            });
            PauseTest("Library Model page: the defaults");
            VerifyUseGpu(searchDlg);
            RunUI(() => searchDlg.UseExistingModel = false);
            ShowAndDismissMessage(searchDlg, PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_Choose_at_least_one_run_to_fine_tune_the_model_on_);

            RunUI(() =>
            {
                searchDlg.ChooseFromSearchedFiles = true;
                AssertEx.AreEqualDeep(diaFilePaths, searchDlg.TuningFileItems);
                AssertEx.AreEqual(0, searchDlg.TuningFiles.Count);
                AssertEx.AreEqual(string.Empty, searchDlg.ModelName);
                foreach (var path in diaFilePaths)
                    searchDlg.SetTuningFileChecked(path, true);
                AssertEx.AreEqualDeep(diaFilePaths, searchDlg.TuningFiles);
                AssertEx.AreEqual(GetDefaultModelName(docPath, diaFilePaths[0]), searchDlg.ModelName);
            });
            PauseTest("Library Model page: fine-tune on the three searched files");

            // A browsed run that does not exist
            string missingRun = TestFilesDir.GetTestPath("missing.mzML");
            RunUI(() => searchDlg.AddTuningFiles(new[] { missingRun }));
            ShowAndDismissMessage(searchDlg, string.Format(
                PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_The_tuning_run__0__does_not_exist_, missingRun));
            RunUI(() =>
            {
                AssertEx.AreEqual(missingRun, searchDlg.TuningFileItems[0]);
                AssertEx.AreEqual(GetDefaultModelName(docPath, missingRun), searchDlg.ModelName);
                searchDlg.SetTuningFileChecked(missingRun, false);
                AssertEx.AreEqual(GetDefaultModelName(docPath, diaFilePaths[0]), searchDlg.ModelName);
            });

            // A copy of a searched run in another folder: Osprey keeps one spectra cache per file name
            string copyDir = TestFilesDir.GetTestPath("copy");
            Directory.CreateDirectory(copyDir);
            string copiedRun = Path.Combine(copyDir, Path.GetFileName(diaFilePaths[0]));
            File.Copy(diaFilePaths[0], copiedRun);
            RunUI(() => searchDlg.AddTuningFiles(new[] { copiedRun }));
            ShowAndDismissMessage(searchDlg, string.Format(
                PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_The_runs__0__and__1__have_the_same_file_name__Osprey_keeps_one_spectra_cache_per_name__so_rename_one_of_them_,
                diaFilePaths[0], copiedRun));
            RunUI(() => searchDlg.SetTuningFileChecked(copiedRun, false));

            // No name
            RunUI(() => searchDlg.ModelName = string.Empty);
            var emptyName = ShowDialog<MessageDlg>(searchDlg.NextPage);
            OkDialog(emptyName, emptyName.OkDialog);
            string modelName = GetDefaultModelName(docPath, diaFilePaths[0]);
            RunUI(() =>
            {
                AssertEx.AreEqual(OspreySearchDlg.Pages.library_model_page, searchDlg.CurrentPage);
                searchDlg.ModelName = modelName;
                AssertEx.AreEqual(diaFilePaths.Length + 2, searchDlg.TuningFileItems.Count);
                AssertEx.AreEqualDeep(diaFilePaths, searchDlg.TuningFiles);
            });
            PauseTest("Library Model page: two browsed runs on top, unchecked");

            RunUI(() =>
            {
                searchDlg.NextPage();
                AssertEx.AreEqual(OspreySearchDlg.Pages.modifications_page, searchDlg.CurrentPage);
            });
            PauseTest("Modifications page");
            RunUI(() =>
            {
                searchDlg.NextPage();
                AssertEx.AreEqual(OspreySearchDlg.Pages.search_settings_page, searchDlg.CurrentPage);
                searchDlg.IsHighResolution = false;
                searchDlg.Threads = Environment.ProcessorCount;
            });
            PauseTest("Search settings page");
            VerifyCommandLine(searchDlg, diaFilePaths, diaFilePaths, null);
            var spec = RunTuningSearch(searchDlg, modelName, TestFilesDir.GetTestPath("OspreySearchControlLog-searched.txt"));
            PauseTest("Run page: the search finished");
            RunUI(() =>
            {
                // The combo offers the saved model
                searchDlg.Model = spec;
                AssertEx.AreEqual(spec, searchDlg.Model);
            });
            OspreySearchTest.FinishImport(searchDlg, diaFilePaths, BiblioSpecLiteSpec.GetLibraryFileName(docPath));
            AssertEx.AreEqual(diaFilePaths.Length, OspreySearchTest.GetDocumentLibrary().LibraryFiles.Count);
            return spec;
        }

        /// <summary>
        /// Browsed runs are listed at the top, checked, in the order chosen, and the searched-files
        /// checkbox neither lists a browsed run twice nor takes it away. A name already in the list
        /// is replaced only with the user's consent. The run not searched is tuned on and absent
        /// from the output.
        /// </summary>
        private CarafeModelSpec TuneOnBrowsedRuns(string[] searchedRuns, string[] tuningRuns, string fastaFilepath, string existingModelName)
        {
            string docPath = SkylineWindow.DocumentFilePath;
            var searchDlg = OpenWizardOnLibraryModelPage(searchedRuns, fastaFilepath);
            RunUI(() =>
            {
                searchDlg.UseExistingModel = false;
                searchDlg.AddTuningFiles(tuningRuns);
                AssertEx.AreEqualDeep(tuningRuns, searchDlg.TuningFileItems);
                AssertEx.AreEqualDeep(tuningRuns, searchDlg.TuningFiles);
                searchDlg.ChooseFromSearchedFiles = true;
                AssertEx.AreEqualDeep(tuningRuns, searchDlg.TuningFileItems);
                searchDlg.ChooseFromSearchedFiles = false;
                AssertEx.AreEqualDeep(tuningRuns, searchDlg.TuningFileItems);
                AssertEx.AreEqualDeep(tuningRuns, searchDlg.TuningFiles);
                searchDlg.ModelName = existingModelName;
            });
            var replacePrompt = ShowDialog<MultiButtonMsgDlg>(searchDlg.NextPage);
            AssertEx.AreEqual(string.Format(
                PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_A_model_named__0__is_already_in_the_list__Replace_it_with_the_model_this_search_fine_tunes_,
                existingModelName), replacePrompt.Message);
            OkDialog(replacePrompt, replacePrompt.BtnCancelClick); // The No button
            RunUI(() =>
            {
                AssertEx.AreEqual(OspreySearchDlg.Pages.library_model_page, searchDlg.CurrentPage);
                searchDlg.ModelName = MODEL_NAME_HELD_OUT;
            });

            OspreySearchTest.AdvanceToSearchSettings(searchDlg);
            VerifyCommandLine(searchDlg, searchedRuns, tuningRuns, null);
            var spec = RunTuningSearch(searchDlg, MODEL_NAME_HELD_OUT, TestFilesDir.GetTestPath("OspreySearchControlLog-heldout.txt"));
            OspreySearchTest.FinishImport(searchDlg, searchedRuns, BiblioSpecLiteSpec.GetLibraryFileName(docPath));
            AssertEx.AreEqual(searchedRuns.Length, OspreySearchTest.GetDocumentLibrary().LibraryFiles.Count);
            return spec;
        }

        /// <summary>
        /// The saved model is offered in the combo and predicted with as it is: the command names it
        /// and no tuning runs, and the library Osprey predicts records the command that made it.
        /// </summary>
        private void SearchWithSavedModel(string[] diaFilePaths, string fastaFilepath, CarafeModelSpec savedModel)
        {
            string docPath = SkylineWindow.DocumentFilePath;
            var searchDlg = OpenWizardOnLibraryModelPage(diaFilePaths, fastaFilepath);
            RunUI(() =>
            {
                AssertEx.AreEqual(3, Settings.Default.CarafeModelList.Count);
                searchDlg.Model = savedModel;
                AssertEx.AreEqual(savedModel.FilePath, searchDlg.Model.FilePath);
                AssertEx.IsTrue(searchDlg.UseExistingModel);
            });
            OspreySearchTest.AdvanceToSearchSettings(searchDlg);
            VerifyCommandLine(searchDlg, diaFilePaths, null, savedModel.FilePath);
            string docBlibPath = BiblioSpecLiteSpec.GetLibraryFileName(docPath);
            string workDir = OspreyHelpers.GetWorkDirectory(docPath);
            OspreySearchTest.RunSearch(searchDlg, docBlibPath, workDir, TestFilesDir.GetTestPath("OspreySearchControlLog-saved.txt"));
            AssertEx.IsFalse(File.Exists(OspreyHelpers.GetTunedModelPath(docBlibPath, workDir)), @"a search with a saved model wrote a model");

            string predictedLibraryPath = Path.Combine(workDir,
                Path.GetFileNameWithoutExtension(docPath) + PREDICTED_LIBRARY_SUFFIX + BiblioSpecLiteSpec.EXT);
            AssertEx.FileExists(predictedLibraryPath);
            using (var reader = new BlibReader(predictedLibraryPath))
            {
                var metadata = reader.ReadMetadata();
                Assert.IsNotNull(metadata, @"the predicted library has no LibMetadata");
                AssertEx.IsTrue(metadata.TryGetValue(KEY_CARAFE_COMMAND, out var command), @"the predicted library does not record its command");
                AssertEx.Contains(command, savedModel.FilePath);
            }
            OspreySearchTest.FinishImport(searchDlg, diaFilePaths, docBlibPath);
        }

        /// <summary>
        /// Opens the wizard on the data files, sets the FASTA and moves to the Library Model page.
        /// </summary>
        public static OspreySearchDlg OpenWizardOnLibraryModelPage(string[] diaFilePaths, string fastaFilepath)
        {
            var searchDlg = OspreySearchTest.OpenWizardOnFastaPage(diaFilePaths);
            RunUI(() =>
            {
                searchDlg.ImportFastaControl.SetFastaContent(fastaFilepath);
                searchDlg.NextPage();
                AssertEx.AreEqual(OspreySearchDlg.Pages.library_model_page, searchDlg.CurrentPage);
            });
            return searchDlg;
        }

        /// <summary>
        /// Use GPU applies to both choices on the page: without an NVIDIA GPU, checking it says so and
        /// it stays unchecked; with one but no CUDA runtime in the runtimes folder, it offers the
        /// download first and stays unchecked when that is declined. Ends with the wizard's own
        /// runtimes back in place.
        /// </summary>
        private void VerifyUseGpu(OspreySearchDlg searchDlg)
        {
            RunUI(() => searchDlg.TorchRuntimes = new TorchRuntimes { CudaDriver = null });
            var noGpuDlg = ShowDialog<MessageDlg>(() => searchDlg.UseGpu = true);
            AssertEx.AreEqual(ModelResources.TorchRuntimes_GetCudaUnavailableReason_No_NVIDIA_GPU_was_found__Predicting_on_the_GPU_needs_an_NVIDIA_GPU_and_its_driver_,
                noGpuDlg.Message);
            OkDialog(noGpuDlg, noGpuDlg.OkDialog);
            RunUI(() =>
            {
                AssertEx.IsFalse(searchDlg.UseGpu);
                searchDlg.TorchRuntimes = new TorchRuntimes
                {
                    CudaDriver = new CudaDriver(12080, 89, @"Test GPU"),
                    RuntimesDirectory = TestFilesDir.GetTestPath(@"libtorch")
                };
            });
            var downloadDlg = ShowDialog<MultiButtonMsgDlg>(() => searchDlg.UseGpu = true);
            OkDialog(downloadDlg, downloadDlg.BtnCancelClick);
            RunUI(() =>
            {
                AssertEx.IsFalse(searchDlg.UseGpu);
                searchDlg.TorchRuntimes = new TorchRuntimes();
            });
        }

        /// <summary>Next refuses with <paramref name="message"/> and stays on the Library Model page.</summary>
        private static void ShowAndDismissMessage(OspreySearchDlg searchDlg, string message)
        {
            var messageDlg = ShowDialog<MessageDlg>(searchDlg.NextPage);
            AssertEx.AreEqual(message, messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            RunUI(() => AssertEx.AreEqual(OspreySearchDlg.Pages.library_model_page, searchDlg.CurrentPage));
        }

        /// <summary>The wizard's default name for the tuned model: "&lt;document&gt; tuned on &lt;first checked run&gt;".</summary>
        public static string GetDefaultModelName(string docPath, string firstRun)
        {
            return string.Format(PeptideSearchResources.OspreySearchDlg_GetDefaultModelName__0__tuned_on__1_,
                Path.GetFileNameWithoutExtension(docPath), Path.GetFileNameWithoutExtension(firstRun));
        }

        /// <summary>
        /// The command line: the searched runs as the inputs, the tuning runs after --training-input
        /// (or no --training-input), the saved model after --model (or no --model), and never an
        /// existing library.
        /// </summary>
        public static void VerifyCommandLine(OspreySearchDlg searchDlg, string[] searchedRuns, string[] tuningRuns, string modelPath)
        {
            OspreyConfig config = null;
            RunUI(() => config = searchDlg.GetSearchConfig());
            string commandLine = OspreyHelpers.BuildCommandLine(config);
            AssertEx.Contains(commandLine, OspreyHelpers.ARG_INPUT + @" " + QuoteAll(searchedRuns) + @" " + OspreyHelpers.ARG_FASTA + @" ");
            if (tuningRuns != null)
                AssertEx.Contains(commandLine, OspreyHelpers.ARG_TRAINING_INPUT + @" " + QuoteAll(tuningRuns) + @" ");
            else
                AssertEx.IsFalse(commandLine.Contains(OspreyHelpers.ARG_TRAINING_INPUT + @" "));
            if (modelPath != null)
                AssertEx.Contains(commandLine, OspreyHelpers.ARG_MODEL + @" " + modelPath.Quote() + @" ");
            else
                AssertEx.IsFalse(commandLine.Contains(OspreyHelpers.ARG_MODEL + @" "));
            AssertEx.IsFalse(commandLine.Contains(OspreyHelpers.ARG_LIBRARY + @" "));
        }

        private static string QuoteAll(IEnumerable<string> paths)
        {
            return string.Join(@" ", paths.Select(p => p.Quote()));
        }

        /// <summary>
        /// Runs a tuning search from the search settings page: Osprey writes the model in the
        /// working folder and the wizard lists a copy of it under <paramref name="modelName"/>,
        /// recording the runs it was tuned on. The wizard's log is saved to <paramref name="logPath"/>.
        /// </summary>
        public static CarafeModelSpec RunTuningSearch(OspreySearchDlg searchDlg, string modelName, string logPath,
            int searchTimeoutMillis = OspreySearchTest.SEARCH_TIMEOUT_MILLIS)
        {
            string docPath = SkylineWindow.DocumentFilePath;
            string docBlibPath = BiblioSpecLiteSpec.GetLibraryFileName(docPath);
            string workDir = OspreyHelpers.GetWorkDirectory(docPath);
            int tuningRunCount = 0;
            string modelPath = null;
            string registeredPath = null;
            RunUI(() =>
            {
                tuningRunCount = searchDlg.TuningFiles.Count;
                modelPath = searchDlg.TunedModelPath;
                registeredPath = searchDlg.RegisteredModelPath;
            });
            AssertEx.AreEqual(OspreyHelpers.GetTunedModelPath(docBlibPath, workDir), modelPath);
            AssertEx.AreEqual(OspreyHelpers.GetRegisteredModelPath(workDir, modelName), registeredPath);

            OspreySearchTest.RunSearch(searchDlg, docBlibPath, workDir, logPath, searchTimeoutMillis);

            // Osprey's model, and the copy named after the model that the list points to, so the
            // document's next tuning cannot overwrite it
            AssertEx.FileExists(modelPath, @"Osprey did not write the fine-tuned model");
            AssertEx.FileExists(registeredPath, @"the fine-tuned model was not saved under its name");
            CarafeModelSpec spec = null;
            RunUI(() => spec = Settings.Default.CarafeModelList.FirstOrDefault(m => Equals(m.Name, modelName)));
            Assert.IsNotNull(spec, @"the fine-tuned model is not in the model list");
            AssertEx.AreEqual(registeredPath, spec.FilePath);
            var choice = CarafeModelChoice.Read(registeredPath);
            Assert.IsNotNull(choice, @"the fine-tuned model cannot be read");
            AssertEx.AreEqual(tuningRunCount, choice.TrainingRunCount);
            return spec;
        }

        /// <summary>
        /// Two trainings on the same runs save the same tensors: the manifest's SHA-256 of each
        /// safetensors entry matches. The files themselves differ, since the manifest carries the
        /// save time.
        /// </summary>
        public static void AssertSameModelTensors(string modelPath1, string modelPath2)
        {
            var entries1 = CarafeModelFile.ReadDescription(modelPath1).Entries;
            var entries2 = CarafeModelFile.ReadDescription(modelPath2).Entries;
            var tensors1 = entries1.Keys.Where(IsTensorEntry).OrderBy(k => k, StringComparer.Ordinal).ToList();
            var tensors2 = entries2.Keys.Where(IsTensorEntry).OrderBy(k => k, StringComparer.Ordinal).ToList();
            AssertEx.IsTrue(tensors1.Count > 0, @"the model holds no tensors");
            AssertEx.AreEqualDeep(tensors1, tensors2);
            foreach (var entry in tensors1)
            {
                // The hashes go to the test output as the record of the two trainings
                Console.WriteLine(@"{0}: {1} / {2}", entry, entries1[entry], entries2[entry]);
                AssertEx.AreEqual(entries1[entry], entries2[entry], @"the tensors of " + entry + @" differ between the two trainings");
            }
        }

        private static bool IsTensorEntry(string entryName)
        {
            return entryName.EndsWith(@".safetensors", StringComparison.OrdinalIgnoreCase);
        }
    }
}
