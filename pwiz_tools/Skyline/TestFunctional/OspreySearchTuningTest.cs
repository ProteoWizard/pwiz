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
using pwiz.CarafeSharp.Core;
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
    /// held out of the search, and a search that predicts with the saved model. Both tuning searches
    /// tune on the middle run alone, as a user does, and two trainings on the same run produce the
    /// same model tensors, which is how the held-out tuning is shown to be the same tuning.
    /// </summary>
    [TestClass]
    public class OspreySearchTuningTest : AbstractFunctionalTestEx
    {
        /// <summary>The LibMetadata key CarafeSharp writes the command that made a predicted library under.</summary>
        private const string KEY_CARAFE_COMMAND = @"carafe.command";
        private const string PREDICTED_LIBRARY_SUFFIX = @".predicted";
        /// <summary>The name a test gives the document's fine-tuned model when it adds it to the model list.</summary>
        private const string SAVED_MODEL_NAME = @"Stellar subset, tuned";

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
            OspreyTestUtil.RegisterOsprey(OspreyTestUtil.EnsureOsprey(TestContext));
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
            string[] middleRun = { diaFilePaths[1] };

            // Scenario 1: the three runs searched, and the model tuned on the middle one, chosen from
            // the searched files. The model is the document's, beside it.
            OspreySearchTest.PrepareDocument(TestFilesDir.GetTestPath("OspreySearchTuningTest.sky"));
            string tunedModelPath = TuneOnSearchedRuns(diaFilePaths, middleRun, fastaFilepath);

            // Scenario 2: the outer runs searched, and the model tuned on the middle run, browsed in,
            // so the run tuned on is held out of the search. Scenario 1's model joins the model list
            // through Add..., and a model already beside the document is overwritten only with consent.
            OspreySearchTest.PrepareDocument(TestFilesDir.GetTestPath("OspreySearchTuningTest-heldout.sky"));
            string heldOutModelPath = TuneOnBrowsedRuns(outerRuns, middleRun, fastaFilepath, tunedModelPath);
            AssertSameModelTensors(tunedModelPath, heldOutModelPath);

            // Scenario 3: the three runs searched with the listed model from scenario 1.
            OspreySearchTest.PrepareDocument(TestFilesDir.GetTestPath("OspreySearchTuningTest-saved.sky"));
            SearchWithSavedModel(diaFilePaths, fastaFilepath, tunedModelPath);
        }

        /// <summary>
        /// The tuning list starts empty; "Choose from the searched files" lists the searched runs,
        /// unchecked, and the checked ones are tuned on. On the way, the page's refusals: no run
        /// checked, a browsed run that does not exist, and a browsed run whose file name a searched
        /// run shares. Browsed runs go to the top, checked; an unwanted one is unchecked, not
        /// removed. Returns the document's fine-tuned model.
        /// </summary>
        private string TuneOnSearchedRuns(string[] diaFilePaths, string[] tuningRuns, string fastaFilepath)
        {
            string docPath = SkylineWindow.DocumentFilePath;
            var searchDlg = OpenWizardOnLibraryModelPage(diaFilePaths, fastaFilepath);
            RunUI(() =>
            {
                // The defaults: the pretrained models, used as they are, no tuning runs, and the CPU
                // (a test's Tools folder has no CUDA runtime)
                AssertEx.IsTrue(searchDlg.UseExistingModel);
                AssertEx.IsTrue(searchDlg.Model.IsDefault);
                AssertEx.AreEqual(0, searchDlg.TuningFileItems.Count);
                AssertEx.IsFalse(searchDlg.UseGpu);
            });
            VerifyUseGpu(searchDlg);
            RunUI(() => searchDlg.UseExistingModel = false);
            ShowAndDismissMessage(searchDlg, PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_Choose_at_least_one_run_to_fine_tune_the_model_on_);

            RunUI(() =>
            {
                searchDlg.ChooseFromSearchedFiles = true;
                AssertEx.AreEqualDeep(diaFilePaths, searchDlg.TuningFileItems);
                AssertEx.AreEqual(0, searchDlg.TuningFiles.Count);
                foreach (var path in tuningRuns)
                    searchDlg.SetTuningFileChecked(path, true);
                AssertEx.AreEqualDeep(tuningRuns, searchDlg.TuningFiles);
            });

            // A browsed run that does not exist
            string missingRun = TestFilesDir.GetTestPath("missing.mzML");
            RunUI(() => searchDlg.AddTuningFiles(new[] { missingRun }));
            ShowAndDismissMessage(searchDlg, string.Format(
                PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_The_tuning_run__0__does_not_exist_, missingRun));
            RunUI(() =>
            {
                AssertEx.AreEqual(missingRun, searchDlg.TuningFileItems[0]);
                AssertEx.IsTrue(searchDlg.TuningFiles.Contains(missingRun));
                searchDlg.SetTuningFileChecked(missingRun, false);
                AssertEx.AreEqualDeep(tuningRuns, searchDlg.TuningFiles);
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
            RunUI(() =>
            {
                searchDlg.SetTuningFileChecked(copiedRun, false);
                AssertEx.AreEqual(diaFilePaths.Length + 2, searchDlg.TuningFileItems.Count);
                AssertEx.AreEqualDeep(tuningRuns, searchDlg.TuningFiles);
            });

            OspreySearchTest.AdvanceToSearchSettings(searchDlg);
            VerifyCommandLine(searchDlg, diaFilePaths, tuningRuns, null);
            string modelPath = RunTuningSearch(searchDlg, TestFilesDir.GetTestPath("OspreySearchControlLog-searched.txt"));
            // The model is the document's, like its library: nothing joins the model list
            RunUI(() => AssertEx.AreEqual(1, Settings.Default.CarafeModelList.Count));
            OspreySearchTest.FinishImport(searchDlg, diaFilePaths, BiblioSpecLiteSpec.GetLibraryFileName(docPath));
            AssertEx.AreEqual(diaFilePaths.Length, OspreySearchTest.GetDocumentLibrary().LibraryFiles.Count);
            return modelPath;
        }

        /// <summary>
        /// Browsed runs are listed at the top, checked, in the order chosen, and the searched-files
        /// checkbox neither lists a browsed run twice nor takes it away. The run not searched is
        /// tuned on and absent from the output. On the way: a model file joins the list through the
        /// combo's Add..., fine-tuning starts from the pretrained models whatever the combo held, and
        /// a model already beside the document is overwritten only after the user agrees. Returns
        /// the document's fine-tuned model.
        /// </summary>
        private string TuneOnBrowsedRuns(string[] searchedRuns, string[] tuningRuns, string fastaFilepath, string existingModelPath)
        {
            string docPath = SkylineWindow.DocumentFilePath;
            var searchDlg = OpenWizardOnLibraryModelPage(searchedRuns, fastaFilepath);
            // Scenario 1's model, added to the list by name as a user adds a model worth keeping;
            // Add... selects it
            RunDlg<EditCarafeModelDlg>(searchDlg.AddModel, dlg =>
            {
                dlg.ModelName = SAVED_MODEL_NAME;
                dlg.ModelPath = existingModelPath;
                dlg.OkDialog();
            });
            RunUI(() =>
            {
                AssertEx.AreEqual(2, Settings.Default.CarafeModelList.Count);
                var savedModel = searchDlg.Model;
                AssertEx.AreEqual(SAVED_MODEL_NAME, savedModel.Name);
                AssertEx.AreEqual(existingModelPath, savedModel.FilePath);
                // Fine-tuning starts from the pretrained models whatever the combo held, which
                // comes back with "Use this model"
                searchDlg.UseExistingModel = false;
                AssertEx.IsTrue(searchDlg.Model.IsDefault);
                searchDlg.UseExistingModel = true;
                AssertEx.AreEqual(savedModel, searchDlg.Model);
                searchDlg.UseExistingModel = false;
                AssertEx.IsTrue(searchDlg.Model.IsDefault);
                // A searched run browsed in with the tuning runs, and unchecked as unwanted
                var browsedRuns = tuningRuns.Append(searchedRuns[0]).ToArray();
                searchDlg.AddTuningFiles(browsedRuns);
                AssertEx.AreEqualDeep(browsedRuns, searchDlg.TuningFileItems);
                searchDlg.SetTuningFileChecked(searchedRuns[0], false);
                AssertEx.AreEqualDeep(tuningRuns, searchDlg.TuningFiles);
                // The searched runs join below the browsed ones, the one already browsed not twice
                searchDlg.ChooseFromSearchedFiles = true;
                AssertEx.AreEqualDeep(browsedRuns.Concat(searchedRuns.Except(browsedRuns)).ToArray(), searchDlg.TuningFileItems);
                AssertEx.AreEqualDeep(tuningRuns, searchDlg.TuningFiles);
                searchDlg.ChooseFromSearchedFiles = false;
                AssertEx.AreEqualDeep(browsedRuns, searchDlg.TuningFileItems);
                AssertEx.AreEqualDeep(tuningRuns, searchDlg.TuningFiles);
            });

            // A fine-tuned model already beside this document: Next asks, and Cancel stays on the page
            string documentModelPath = OspreyHelpers.GetDocumentModelPath(docPath);
            File.Copy(existingModelPath, documentModelPath);
            var overwritePrompt = ShowDialog<MultiButtonMsgDlg>(searchDlg.NextPage);
            AssertEx.AreEqual(string.Format(
                PeptideSearchResources.OspreySearchDlg_ValidateLibraryModelPage_The_document_s_fine_tuned_model__0__already_exists__Continuing_will_overwrite_it_with_the_model_this_search_fine_tunes_,
                documentModelPath), overwritePrompt.Message);
            OkDialog(overwritePrompt, overwritePrompt.BtnCancelClick);
            RunUI(() => AssertEx.AreEqual(OspreySearchDlg.Pages.library_model_page, searchDlg.CurrentPage));
            overwritePrompt = ShowDialog<MultiButtonMsgDlg>(searchDlg.NextPage);
            OkDialog(overwritePrompt, overwritePrompt.ClickOk);
            RunUI(() => AssertEx.AreEqual(OspreySearchDlg.Pages.modifications_page, searchDlg.CurrentPage));

            OspreySearchTest.AdvanceToSearchSettings(searchDlg);
            VerifyCommandLine(searchDlg, searchedRuns, tuningRuns, null);
            string modelPath = RunTuningSearch(searchDlg, TestFilesDir.GetTestPath("OspreySearchControlLog-heldout.txt"));
            // Overwritten: the new training's manifest carries its own save time
            AssertEx.IsFalse(File.ReadAllBytes(modelPath).SequenceEqual(File.ReadAllBytes(existingModelPath)),
                @"the document's fine-tuned model was not overwritten");
            OspreySearchTest.FinishImport(searchDlg, searchedRuns, BiblioSpecLiteSpec.GetLibraryFileName(docPath));
            AssertEx.AreEqual(searchedRuns.Length, OspreySearchTest.GetDocumentLibrary().LibraryFiles.Count);
            return modelPath;
        }

        /// <summary>
        /// The listed model is offered in the combo and predicted with as it is: the command names it
        /// and no tuning runs, no model is written, and the library Osprey predicts records the
        /// command that made it.
        /// </summary>
        private void SearchWithSavedModel(string[] diaFilePaths, string fastaFilepath, string savedModelPath)
        {
            string docPath = SkylineWindow.DocumentFilePath;
            var searchDlg = OpenWizardOnLibraryModelPage(diaFilePaths, fastaFilepath);
            RunUI(() =>
            {
                var models = Settings.Default.CarafeModelList;
                AssertEx.AreEqual(2, models.Count);
                searchDlg.Model = models.First(m => Equals(m.Name, SAVED_MODEL_NAME));
                AssertEx.AreEqual(savedModelPath, searchDlg.Model.FilePath);
                AssertEx.IsTrue(searchDlg.UseExistingModel);
            });
            OspreySearchTest.AdvanceToSearchSettings(searchDlg);
            VerifyCommandLine(searchDlg, diaFilePaths, null, savedModelPath);
            string docBlibPath = BiblioSpecLiteSpec.GetLibraryFileName(docPath);
            string workDir = OspreyHelpers.GetWorkDirectory(docPath);
            OspreySearchTest.RunSearch(searchDlg, docBlibPath, workDir, TestFilesDir.GetTestPath("OspreySearchControlLog-saved.txt"));
            AssertEx.IsFalse(File.Exists(OspreyHelpers.GetTunedModelPath(docBlibPath, workDir)), @"a search with a saved model wrote a model");
            AssertEx.IsFalse(File.Exists(OspreyHelpers.GetDocumentModelPath(docPath)), @"a search with a saved model copied a model beside the document");

            string predictedLibraryPath = Path.Combine(workDir,
                Path.GetFileNameWithoutExtension(docPath) + PREDICTED_LIBRARY_SUFFIX + BiblioSpecLiteSpec.EXT);
            AssertEx.FileExists(predictedLibraryPath);
            using (var reader = new BlibReader(predictedLibraryPath))
            {
                var metadata = reader.ReadMetadata();
                Assert.IsNotNull(metadata, @"the predicted library has no LibMetadata");
                AssertEx.IsTrue(metadata.TryGetValue(KEY_CARAFE_COMMAND, out var command), @"the predicted library does not record its command");
                AssertEx.Contains(command, savedModelPath);
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
        /// working folder and the wizard copies it beside the document as the document's fine-tuned
        /// model, which records the runs it was tuned on. The wizard's log is saved to
        /// <paramref name="logPath"/>. Returns the document's model.
        /// </summary>
        public static string RunTuningSearch(OspreySearchDlg searchDlg, string logPath,
            int searchTimeoutMillis = OspreySearchTest.SEARCH_TIMEOUT_MILLIS)
        {
            string docPath = SkylineWindow.DocumentFilePath;
            string docBlibPath = BiblioSpecLiteSpec.GetLibraryFileName(docPath);
            string workDir = OspreyHelpers.GetWorkDirectory(docPath);
            int tuningRunCount = 0;
            string tunedModelPath = null;
            string documentModelPath = null;
            RunUI(() =>
            {
                tuningRunCount = searchDlg.TuningFiles.Count;
                tunedModelPath = searchDlg.TunedModelPath;
                documentModelPath = searchDlg.DocumentModelPath;
            });
            AssertEx.AreEqual(OspreyHelpers.GetTunedModelPath(docBlibPath, workDir), tunedModelPath);
            AssertEx.AreEqual(OspreyHelpers.GetDocumentModelPath(docPath), documentModelPath);

            OspreySearchTest.RunSearch(searchDlg, docBlibPath, workDir, logPath, searchTimeoutMillis);

            // Osprey's model in the working folder, and the document's copy beside the document
            AssertEx.FileExists(tunedModelPath, @"Osprey did not write the fine-tuned model");
            AssertEx.FileExists(documentModelPath, @"the fine-tuned model was not copied beside the document");
            var choice = CarafeModelChoice.Read(documentModelPath);
            Assert.IsNotNull(choice, @"the fine-tuned model cannot be read");
            AssertEx.AreEqual(tuningRunCount, choice.TrainingRunCount);
            return documentModelPath;
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
