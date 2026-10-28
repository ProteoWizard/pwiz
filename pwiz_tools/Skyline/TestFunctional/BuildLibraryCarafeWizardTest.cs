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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Build Library's AlphaPeptDeep source on Osprey's Stellar subset: the pages after the source
    /// (what to predict for, the model, and for a fine-tune the FASTA, the modifications and the
    /// search settings of the tuning runs' search), a model fine-tuned on the three subset runs
    /// searched against the subset FASTA, the library predicted for the document's precursors
    /// with the tuned model, and that model saved beside the library. Then a second library,
    /// predicted with the saved model added to the model list, and a third for a protein FASTA's
    /// precursors rather than the document's. On the subset a single run searched alone keeps no
    /// precursor at 1% in the first pass, so the tuning is on all three runs.
    /// <para>
    /// Set <see cref="ENV_DEMO"/> and run with the UI shown to pause on every page.
    /// </para>
    /// </summary>
    [TestClass]
    public class BuildLibraryCarafeWizardTest : AbstractFunctionalTestEx
    {
        /// <summary>Names an environment variable that, when set, pauses the test on each page of the wizard for a look.</summary>
        public const string ENV_DEMO = @"SKYLINE_BUILD_LIBRARY_DEMO";

        private const string TUNED_LIBRARY_NAME = @"Stellar subset tuned";
        private const string SAVED_LIBRARY_NAME = @"Stellar subset saved model";
        private const string FASTA_LIBRARY_NAME = @"Stellar subset FASTA";
        private const string SAVED_MODEL_NAME = @"Stellar subset, tuned in Build Library";

        [TestMethod,
         NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestBuildLibraryCarafeWizard()
        {
            TestFilesZip = @"TestFunctional\OspreySearchTest.zip";
            TestFilesZipSuffix = @"-buildlib";
            RunFunctionalTest();
        }

        private static bool IsDemo => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ENV_DEMO));

        protected override void DoTest()
        {
            OspreyTestUtil.RegisterOsprey(OspreyTestUtil.EnsureOsprey(TestContext));
            // The additional settings start at Osprey's defaults, whatever an earlier wizard on this machine left
            Settings.Default.OspreyAdditionalSettings = null;
            OspreyProperties.ClearCache();
            RunUI(() =>
            {
                // The model list starts with the pretrained models alone
                Settings.Default.CarafeModelList.Clear();
                Settings.Default.CarafeModelList.AddDefaults();
            });

            string fastaPath = TestFilesDir.GetTestPath("stellar-subset-proteins.fasta");
            string[] diaFilePaths = Directory.GetFiles(TestFilesDir.FullPath, "*.mzML").OrderBy(p => p).ToArray();
            AssertEx.AreEqual(3, diaFilePaths.Length);

            // The document: the subset FASTA's peptides (7 to 35 residues, any N-terminal position)
            // at charges 2 and 3, so the library is predicted for the precursors the tuning runs
            // hold. Imported through the model, not the menu, which would ask about proteins the
            // filter empties.
            OspreySearchTest.PrepareDocument(TestFilesDir.GetTestPath("BuildLibraryCarafeWizardTest.sky"));
            RunUI(() =>
            {
                SkylineWindow.ModifyDocument("Set the subset's filters", doc =>
                {
                    var settings = doc.Settings.ChangeTransitionFilter(f =>
                        f.ChangePeptidePrecursorCharges(new[] { Adduct.DOUBLY_PROTONATED, Adduct.TRIPLY_PROTONATED }));
                    var peptideSettingsDoc = settings.PeptideSettings;
                    settings = settings.ChangePeptideSettings(peptideSettingsDoc.ChangeFilter(
                        peptideSettingsDoc.Filter.ChangeMinPeptideLength(7).ChangeMaxPeptideLength(35).ChangeExcludeNTermAAs(0)));
                    return doc.ChangeSettings(settings);
                });
                SkylineWindow.ModifyDocument("Import the subset FASTA", doc =>
                    ImportPeptideSearch.ImportFasta(doc, fastaPath, null, null, null, out _, out _, out _));
            });
            WaitForDocumentLoaded();
            AssertEx.IsTrue(SkylineWindow.Document.PeptideCount > 0, @"the FASTA added no peptides");

            string tunedModelPath = BuildTunedLibrary(diaFilePaths, fastaPath);
            BuildWithSavedModel(tunedModelPath);
            BuildForFasta(fastaPath);
        }

        /// <summary>
        /// The whole route: the source and its first-page options (the pretrained models, the
        /// document's precursors, with Finish there until the fine-tune box is checked), the runs
        /// (after the page's refusal of none), the FASTA (after its refusal of none), the
        /// modifications, the search settings, Previous back to the first page and Next forward
        /// with every choice kept, and Finish. The build tunes on the three runs and predicts
        /// the document's precursors with the tuned model; the model lands beside the library and
        /// records the runs. Returns the library's model.
        /// </summary>
        private string BuildTunedLibrary(string[] diaFilePaths, string fastaPath)
        {
            string libraryPath = TestFilesDir.GetTestPath("StellarSubsetTuned.blib");
            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = TUNED_LIBRARY_NAME;
                buildLibraryDlg.LibraryPath = libraryPath;
                buildLibraryDlg.Carafe = true;
                // Tune and predict on the CPU, whatever GPU this computer has
                buildLibraryDlg.TorchRuntimes = new TorchRuntimes { CudaDriver = null };
                AssertEx.AreEqual(BuildLibraryDlg.Pages.properties, buildLibraryDlg.CurrentPage);
            });
            // The first page's Carafe options: the pretrained models, the document's precursors
            // and no fine-tune, so Finish is on this page; the fine-tune box turns it into Next
            RunUI(() =>
            {
                AssertEx.IsTrue(buildLibraryDlg.PredictForDocument);
                AssertEx.IsTrue(buildLibraryDlg.UseExistingModel);
                AssertEx.IsTrue(buildLibraryDlg.CarafeModel.IsDefault);
                AssertEx.IsFalse(buildLibraryDlg.UseGpu);
                AssertEx.IsNull(buildLibraryDlg.ModificationWarning);
                AssertEx.AreEqual(Resources.BuildLibraryDlg_OkWizardPage_Finish, buildLibraryDlg.NextButtonText);
                buildLibraryDlg.UseExistingModel = false;
                AssertEx.AreEqual(Resources.BuildLibraryDlg_btnPrevious_Click__Next__, buildLibraryDlg.NextButtonText);
            });
            PauseForDemo("Build Library: the AlphaPeptDeep source, fine-tuning a new model");

            // The tuning runs: the page refuses none, then the three runs, browsed in
            NextPage(buildLibraryDlg, BuildLibraryDlg.Pages.library_model);
            ShowAndDismissMessage(buildLibraryDlg, PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_Choose_at_least_one_run_to_fine_tune_the_model_on_,
                BuildLibraryDlg.Pages.library_model);
            RunUI(() =>
            {
                buildLibraryDlg.AddTuningFiles(diaFilePaths);
                AssertEx.AreEqualDeep(diaFilePaths, buildLibraryDlg.TuningFiles);
            });
            PauseForDemo("Tuning runs: the three runs the new model is fine-tuned on");

            // Training FASTA: required; the document's enzyme and missed cleavages to start
            NextPage(buildLibraryDlg, BuildLibraryDlg.Pages.fasta);
            ShowAndDismissMessage(buildLibraryDlg, PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_A_FASTA_file_is_required_for_an_Osprey_search_,
                BuildLibraryDlg.Pages.fasta);
            RunUI(() =>
            {
                var peptideSettingsDoc = SkylineWindow.Document.Settings.PeptideSettings;
                AssertEx.AreEqual(peptideSettingsDoc.Enzyme.Name, buildLibraryDlg.FastaControl.Enzyme.Name);
                AssertEx.AreEqual(peptideSettingsDoc.DigestSettings.MaxMissedCleavages, buildLibraryDlg.FastaControl.MaxMissedCleavages);
                buildLibraryDlg.FastaControl.FastaFile = fastaPath;
            });
            PauseForDemo("Training FASTA: the FASTA the runs are searched against");

            // Modifications: the document's (Carbamidomethyl C fixed)
            NextPage(buildLibraryDlg, BuildLibraryDlg.Pages.modifications);
            RunUI(() =>
            {
                AssertEx.AreEqual(1, buildLibraryDlg.ModificationsControl.FixedMods.Count());
                AssertEx.AreEqual(0, buildLibraryDlg.ModificationsControl.VariableMods.Count());
            });
            PauseForDemo("Modifications: the document's");

            // Search Settings: the Stellar subset is unit resolution
            NextPage(buildLibraryDlg, BuildLibraryDlg.Pages.search_settings);
            RunUI(() =>
            {
                AssertEx.AreEqual(Resources.BuildLibraryDlg_OkWizardPage_Finish, buildLibraryDlg.NextButtonText);
                buildLibraryDlg.SearchSettingsControl.IsHighResolution = false;
                AssertEx.IsFalse(buildLibraryDlg.SearchSettingsControl.IsFragmentTolerancePpm);
                buildLibraryDlg.SearchSettingsControl.Threads = Environment.ProcessorCount;
            });
            PauseForDemo("Search Settings: unit resolution for Stellar");

            // Additional Settings from the page: a fine-tuned build's every scope, the tuning's
            // among them; a flag of the search is set, which the one command then carries
            var settings = buildLibraryDlg.SearchSettingsControl.AdditionalSettings;
            var grid = ShowDialog<OspreySettingsGridDlg>(buildLibraryDlg.SearchSettingsControl.ShowAdditionalSettings);
            RunUI(() =>
            {
                AssertEx.IsTrue(grid.Labels.Contains(settings.Properties.Find(@"seed").Label));
                grid.SetValue(settings.Properties.Find(@"verbose").Label, true.ToString());
            });
            PauseForDemo("Additional Settings: every setting of a fine-tuned build");
            OkDialog(grid, grid.OkDialog);

            // Previous back to the model, Next forward again: every choice is kept
            RunUI(() =>
            {
                buildLibraryDlg.PreviousPage();
                AssertEx.AreEqual(BuildLibraryDlg.Pages.modifications, buildLibraryDlg.CurrentPage);
                buildLibraryDlg.PreviousPage();
                AssertEx.AreEqual(BuildLibraryDlg.Pages.fasta, buildLibraryDlg.CurrentPage);
                buildLibraryDlg.PreviousPage();
                AssertEx.AreEqual(BuildLibraryDlg.Pages.library_model, buildLibraryDlg.CurrentPage);
                AssertEx.AreEqualDeep(diaFilePaths, buildLibraryDlg.TuningFiles);
                buildLibraryDlg.PreviousPage();
                AssertEx.AreEqual(BuildLibraryDlg.Pages.properties, buildLibraryDlg.CurrentPage);
                AssertEx.IsFalse(buildLibraryDlg.UseExistingModel);
                buildLibraryDlg.OkWizardPage();
                AssertEx.AreEqual(BuildLibraryDlg.Pages.library_model, buildLibraryDlg.CurrentPage);
                buildLibraryDlg.OkWizardPage();
                AssertEx.AreEqual(fastaPath, buildLibraryDlg.FastaControl.FastaFile);
                buildLibraryDlg.OkWizardPage();
                buildLibraryDlg.OkWizardPage();
                AssertEx.AreEqual(BuildLibraryDlg.Pages.search_settings, buildLibraryDlg.CurrentPage);
                AssertEx.IsFalse(buildLibraryDlg.SearchSettingsControl.IsHighResolution);
            });

            // Finish: the builder tunes on the runs searched against the FASTA, and the whole job
            // runs after the form closes
            RunUI(buildLibraryDlg.OkWizardPage);
            WaitForCondition(() => buildLibraryDlg.Builder != null);
            var builder = (CarafeLibraryBuilder)buildLibraryDlg.Builder;
            AssertEx.IsTrue(builder.IsTuning);
            AssertEx.IsNull(builder.CarafeModelPath);
            AssertEx.AreEqual(fastaPath, builder.TuningConfig.FastaPath);
            AssertEx.AreEqualDeep(diaFilePaths, builder.TuningConfig.TrainingInputs);
            AssertEx.AreEqual(OspreyHelpers.RESOLUTION_UNIT, builder.TuningConfig.Resolution);
            AssertEx.AreEqual(OspreyHelpers.GetLibraryModelPath(libraryPath), builder.TunedModelPath);
            AssertEx.Contains(OspreyHelpers.BuildCommandLine(builder.TuningConfig), OspreyHelpers.ARG_PREFIX + @"verbose");
            WaitForClosedForm<BuildLibraryDlg>();
            WaitForCondition(OspreySearchTest.SEARCH_TIMEOUT_MILLIS, () => peptideSettings.AvailableLibraries.Contains(TUNED_LIBRARY_NAME));
            AssertEx.FileExists(libraryPath);
            // The library's model, beside it, records the runs it was tuned on
            AssertEx.FileExists(builder.TunedModelPath, @"the fine-tuned model was not saved beside the library");
            var choice = CarafeModelChoice.Read(builder.TunedModelPath);
            Assert.IsNotNull(choice, @"the fine-tuned model cannot be read");
            AssertEx.AreEqual(diaFilePaths.Length, choice.TrainingRunCount);
            // Nothing joins the model list
            RunUI(() => AssertEx.AreEqual(1, Settings.Default.CarafeModelList.Count));

            RunUI(() => peptideSettings.PickedLibraries = new[] { TUNED_LIBRARY_NAME });
            OkDialog(peptideSettings, peptideSettings.OkDialog);
            WaitForDocumentLoaded();
            AssertLibrarySpectra(TUNED_LIBRARY_NAME);
            return builder.TunedModelPath;
        }

        /// <summary>
        /// The library's model joins the model list through the combo's Add..., and a second
        /// library is predicted with it as it is: no fine-tune, the model named to the builder.
        /// </summary>
        private void BuildWithSavedModel(string tunedModelPath)
        {
            string libraryPath = TestFilesDir.GetTestPath("StellarSubsetSaved.blib");
            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = SAVED_LIBRARY_NAME;
                buildLibraryDlg.LibraryPath = libraryPath;
                buildLibraryDlg.Carafe = true;
                buildLibraryDlg.TorchRuntimes = new TorchRuntimes { CudaDriver = null };
            });
            RunDlg<EditCarafeModelDlg>(buildLibraryDlg.AddCarafeModel, dlg =>
            {
                dlg.ModelName = SAVED_MODEL_NAME;
                dlg.ModelPath = tunedModelPath;
                dlg.OkDialog();
            });
            RunUI(() =>
            {
                AssertEx.AreEqual(2, Settings.Default.CarafeModelList.Count);
                AssertEx.AreEqual(SAVED_MODEL_NAME, buildLibraryDlg.CarafeModel.Name);
                AssertEx.AreEqual(tunedModelPath, buildLibraryDlg.CarafeModelPath);
                AssertEx.IsTrue(buildLibraryDlg.UseExistingModel);
                AssertEx.AreEqual(Resources.BuildLibraryDlg_OkWizardPage_Finish, buildLibraryDlg.NextButtonText);
            });
            PauseForDemo("Build Library: the library's model, saved to the list and used as it is");

            // The route with the model as it is reaches no settings page: the prediction runs with
            // Osprey's defaults, and the last-used additional settings do not apply to it
            RunUI(buildLibraryDlg.OkWizardPage);
            WaitForCondition(() => buildLibraryDlg.Builder != null);
            var builder = (CarafeLibraryBuilder)buildLibraryDlg.Builder;
            AssertEx.IsFalse(builder.IsTuning);
            AssertEx.AreEqual(tunedModelPath, builder.CarafeModelPath);
            WaitForClosedForm<BuildLibraryDlg>();
            WaitForCondition(OspreySearchTest.SEARCH_TIMEOUT_MILLIS, () => peptideSettings.AvailableLibraries.Contains(SAVED_LIBRARY_NAME));
            AssertEx.FileExists(libraryPath);
            AssertEx.IsFalse(File.Exists(OspreyHelpers.GetLibraryModelPath(libraryPath)), @"a build with a saved model wrote a model");
            RunUI(() => peptideSettings.PickedLibraries = new[] { SAVED_LIBRARY_NAME });
            OkDialog(peptideSettings, peptideSettings.OkDialog);
            WaitForDocumentLoaded();
            AssertLibrarySpectra(SAVED_LIBRARY_NAME);
        }

        /// <summary>
        /// A library predicted for a protein FASTA's precursors, digested with the document's
        /// settings, with the pretrained models: the same peptides as the document's, since the
        /// document was made from the same FASTA; plus CarafeSharp's entrapment peptides, shuffled
        /// from the FASTA's own, and a reverse-sequence decoy of every peptide, which the library
        /// marks in its DecoyPairs table. On the way, the page's refusals of an empty or missing
        /// FASTA, and of entrapment from a FASTA file not named.
        /// </summary>
        private void BuildForFasta(string fastaPath)
        {
            string libraryPath = TestFilesDir.GetTestPath("StellarSubsetFasta.blib");
            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = FASTA_LIBRARY_NAME;
                buildLibraryDlg.LibraryPath = libraryPath;
                buildLibraryDlg.Carafe = true;
                buildLibraryDlg.TorchRuntimes = new TorchRuntimes { CudaDriver = null };
            });
            // A FASTA's precursors: the first page's choice leads to the Predict For page
            RunUI(() =>
            {
                buildLibraryDlg.CarafeModel = CarafeModelSpec.DEFAULT;
                AssertEx.IsTrue(buildLibraryDlg.CarafeModel.IsDefault);
                AssertEx.AreEqual(Resources.BuildLibraryDlg_OkWizardPage_Finish, buildLibraryDlg.NextButtonText);
                buildLibraryDlg.PredictForDocument = false;
                AssertEx.AreEqual(Resources.BuildLibraryDlg_btnPrevious_Click__Next__, buildLibraryDlg.NextButtonText);
            });
            PauseForDemo("Build Library: the pretrained models, for a protein FASTA's precursors");
            NextPage(buildLibraryDlg, BuildLibraryDlg.Pages.predict_for);
            // The FASTA box starts empty and must name a file that exists
            RunUI(() => AssertEx.IsTrue(string.IsNullOrEmpty(buildLibraryDlg.PredictForFastaPath)));
            ShowAndDismissMessage(buildLibraryDlg, SettingsUIResources.BuildLibraryDlg_ValidatePage_Choose_the_protein_FASTA_file_to_predict_the_library_for_,
                BuildLibraryDlg.Pages.predict_for);
            string missingFasta = TestFilesDir.GetTestPath("missing.fasta");
            RunUI(() => buildLibraryDlg.PredictForFastaPath = missingFasta);
            ShowAndDismissMessage(buildLibraryDlg, string.Format(SettingsUIResources.BuildLibraryDlg_ValidatePage_The_FASTA_file__0__does_not_exist_, missingFasta),
                BuildLibraryDlg.Pages.predict_for);
            RunUI(() =>
            {
                buildLibraryDlg.PredictForFastaPath = fastaPath;
                buildLibraryDlg.AddDecoys = true;
                buildLibraryDlg.AddEntrapment = true;
                AssertEx.IsFalse(buildLibraryDlg.EntrapmentFromFasta);
                buildLibraryDlg.EntrapmentFromFasta = true;
            });
            // Entrapment from a FASTA file needs the file; shuffled from the predicted FASTA's own peptides needs nothing more
            ShowAndDismissMessage(buildLibraryDlg, SettingsUIResources.BuildLibraryDlg_ValidatePage_Choose_the_FASTA_file_of_the_entrapment_peptides_,
                BuildLibraryDlg.Pages.predict_for);
            RunUI(() =>
            {
                buildLibraryDlg.EntrapmentFromFasta = false;
                AssertEx.AreEqual(Resources.BuildLibraryDlg_OkWizardPage_Finish, buildLibraryDlg.NextButtonText);
            });
            PauseForDemo("Predict For: a protein FASTA's precursors, with decoys and shuffled entrapment peptides");
            RunUI(buildLibraryDlg.OkWizardPage);
            WaitForCondition(() => buildLibraryDlg.Builder != null);
            var builder = (CarafeLibraryBuilder)buildLibraryDlg.Builder;
            AssertEx.IsFalse(builder.IsTuning);
            // The FASTA's document, not the open one: the same peptides, digested the same way,
            // then the entrapment peptides, and a decoy for each
            AssertEx.IsFalse(ReferenceEquals(SkylineWindow.Document, builder.Document));
            int targetCount = builder.Document.Peptides.Count(p => !p.IsDecoy);
            int entrapmentCount = builder.EntrapmentTargets.Count;
            AssertEx.IsTrue(entrapmentCount > 0, @"the entrapment FASTA added no peptides");
            AssertEx.AreEqual(SkylineWindow.Document.PeptideCount + entrapmentCount, targetCount);
            AssertEx.AreEqual(targetCount, builder.Document.Peptides.Count(p => p.IsDecoy));
            AssertEx.AreEqual(BuildLibraryDlg.DECOY_METHOD_REVERSE, builder.DecoyMethod);
            WaitForClosedForm<BuildLibraryDlg>();
            WaitForCondition(OspreySearchTest.SEARCH_TIMEOUT_MILLIS, () => peptideSettings.AvailableLibraries.Contains(FASTA_LIBRARY_NAME));
            AssertEx.FileExists(libraryPath);
            // The library pairs its decoys with their targets and flags the entrapment peptides,
            // as Osprey's own libraries do
            AssertEx.IsTrue(OspreyHelpers.LibraryHasDecoyPairs(libraryPath), @"the library has no DecoyPairs table");
            LibraryDecoyPairs.Count(libraryPath, out int decoyRows, out int entrapmentRows);
            AssertEx.IsTrue(decoyRows > 0, @"no decoy rows in DecoyPairs");
            // One row per entrapment precursor (charges 2 and 3)
            var entrapmentSet = new HashSet<Target>(builder.EntrapmentTargets);
            int entrapmentPrecursors = builder.Document.Peptides.Where(p => !p.IsDecoy && entrapmentSet.Contains(p.Target)).Sum(p => p.TransitionGroupCount);
            AssertEx.AreEqual(entrapmentPrecursors, entrapmentRows);
            // The counts go to the test output as the record of what the library holds
            Console.WriteLine(@"DecoyPairs of {0}: {1} decoy rows, {2} entrapment rows for {3} entrapment peptides ({4} precursors); first entrapment peptides: {5}",
                Path.GetFileName(libraryPath), decoyRows, entrapmentRows, entrapmentCount, entrapmentPrecursors,
                string.Join(@", ", builder.EntrapmentTargets.Take(3).Select(t => t.Sequence)));
            RunUI(() => peptideSettings.PickedLibraries = new[] { FASTA_LIBRARY_NAME });
            OkDialog(peptideSettings, peptideSettings.OkDialog);
            WaitForDocumentLoaded();
            AssertLibrarySpectra(FASTA_LIBRARY_NAME);
        }

        /// <summary>The document's library of that name is loaded and holds spectra.</summary>
        private static void AssertLibrarySpectra(string libraryName)
        {
            var library = SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Single(lib => lib != null && lib.Name == libraryName);
            AssertEx.IsTrue(library.IsLoaded, libraryName + @" is not loaded");
            AssertEx.IsTrue(library.SpectrumCount > 0, libraryName + @" holds no spectra");
        }

        /// <summary>Next moves to <paramref name="page"/>.</summary>
        private static void NextPage(BuildLibraryDlg buildLibraryDlg, BuildLibraryDlg.Pages page)
        {
            RunUI(() =>
            {
                buildLibraryDlg.OkWizardPage();
                AssertEx.AreEqual(page, buildLibraryDlg.CurrentPage);
            });
        }

        /// <summary>Next refuses with <paramref name="message"/> and stays on <paramref name="page"/>.</summary>
        private static void ShowAndDismissMessage(BuildLibraryDlg buildLibraryDlg, string message, BuildLibraryDlg.Pages page)
        {
            var messageDlg = ShowDialog<MessageDlg>(buildLibraryDlg.OkWizardPage);
            AssertEx.AreEqual(message, messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            RunUI(() => AssertEx.AreEqual(page, buildLibraryDlg.CurrentPage));
        }

        /// <summary>Pauses on a page for a look, when the demo variable is set and the UI is shown.</summary>
        private void PauseForDemo(string description)
        {
            if (IsDemo)
                PauseForManualTutorialStep(description);
        }
    }
}
