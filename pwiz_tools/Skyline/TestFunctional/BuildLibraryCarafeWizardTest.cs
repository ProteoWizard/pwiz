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
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.SystemUtil;
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
    /// </summary>
    [TestClass]
    public class BuildLibraryCarafeWizardTest : AbstractFunctionalTestEx
    {
        private const string TUNED_LIBRARY_NAME = @"Stellar subset tuned";
        private const string SAVED_LIBRARY_NAME = @"Stellar subset saved model";
        private const string FASTA_LIBRARY_NAME = @"Stellar subset FASTA";
        private const string SAVED_MODEL_NAME = @"Stellar subset, tuned in Build Library";
        private const string COPY_MODEL_NAME = @"Stellar subset, a copy";

        /// <summary>
        /// Peptides Osprey detected at 1% in the middle subset run, one from about each 0.8 minutes
        /// of its 7 to 13 minute slice, with the retention times it measured there.
        /// </summary>
        private static readonly (string Sequence, double RetentionTime)[] PEPTIDES =
        {
            (@"GGAVVDEGPTGVK", 7.315),
            (@"IQETQAELPR", 8.112),
            (@"IAEPNLDTADK", 8.877),
            (@"ANAEELANNLK", 9.702),
            (@"ATPEQYQILK", 10.435),
            (@"VTLGTQPTVLR", 11.229),
            (@"LDLTENLTGSK", 12.266),
            (@"ANYAVDAYFR", 12.755),
        };

        /// <summary>
        /// How far a predicted retention time may be from the measured one, in minutes. The fine-tuned
        /// RT model's median error on held-out peptides is under 0.1 minutes; iRT values would be tens
        /// of units away.
        /// </summary>
        private const double RT_TOLERANCE = 1.0;

        [TestMethod,
         NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestBuildLibraryCarafeWizard()
        {
            TestFilesZip = @"TestFunctional\OspreySearchTest.zip";
            TestFilesZipSuffix = @"-buildlib";
            RunFunctionalTest();
        }

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
            BuildForFasta(fastaPath);
            BuildWithSavedModel(tunedModelPath);
        }

        /// <summary>
        /// The library's Proteins table, which a library built from the document's or a FASTA's
        /// peptides carries as Osprey's own libraries do: every target spectrum of a document
        /// protein links to that protein's name, with no entrapment suffix and no decoy prefix on a
        /// target. Returns the accessions by sequence for further checks.
        /// </summary>
        private static Dictionary<string, HashSet<string>> AssertLibraryProteins(string libraryPath, SrmDocument document)
        {
            var proteins = LibraryDecoyPairs.ReadProteins(libraryPath);
            AssertEx.IsTrue(proteins.Count > 0, @"the library has no proteins");
            int checkedTargets = 0;
            foreach (var nodeGroup in document.PeptideGroups.Where(g => g.IsProtein))
            {
                foreach (var nodePep in nodeGroup.Peptides)
                {
                    if (!proteins.TryGetValue(nodePep.Target.Sequence, out var accessions))
                        continue;   // A peptide the prediction left out
                    // A FASTA record naming several proteins (sp|A|A;sp|B|B) links to each
                    AssertEx.IsTrue(nodeGroup.Name.Split(';').All(accessions.Contains),
                        string.Format(@"{0} links to {1}, not to its protein {2}", nodePep.Target.Sequence, string.Join(@";", accessions), nodeGroup.Name));
                    AssertEx.IsFalse(accessions.Any(a => a.Contains(EntrapmentFastaSettings.DEFAULT_ENTRAPMENT_SUFFIX) || a.StartsWith(EntrapmentFastaSettings.DEFAULT_DECOY_PREFIX)),
                        string.Format(@"the target {0} links to {1}", nodePep.Target.Sequence, string.Join(@";", accessions)));
                    checkedTargets++;
                }
            }
            AssertEx.IsTrue(checkedTargets > 0, @"no target spectrum links to a protein");
            return proteins;
        }

        /// <summary>
        /// The whole route: the source and its first-page options (the pretrained models, the
        /// document's precursors, with Finish there until the fine-tune box is checked), the runs
        /// (after the page's refusal of none), the FASTA (after its refusal of none), the
        /// modifications, the search settings, Previous back to the first page and Next forward
        /// with every choice kept, and Finish. The build tunes on the given runs and predicts
        /// the document's precursors with the tuned model; the model lands beside the library and
        /// records the runs. Returns the library's model.
        /// </summary>
        private string BuildTunedLibrary(string[] tuningRuns, string fastaPath)
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

            // The tuning runs: the page refuses none, then the three runs, browsed in
            NextPage(buildLibraryDlg, BuildLibraryDlg.Pages.library_model);
            ShowAndDismissMessage(buildLibraryDlg, PeptideSearchResources.OspreySearchDlg_ValidateCurrentPage_Choose_at_least_one_run_to_fine_tune_the_model_on_,
                BuildLibraryDlg.Pages.library_model);
            RunUI(() =>
            {
                buildLibraryDlg.AddTuningFiles(tuningRuns);
                AssertEx.AreEqualDeep(tuningRuns, buildLibraryDlg.TuningFiles);
            });

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

            // Modifications: the document's (Carbamidomethyl C fixed)
            NextPage(buildLibraryDlg, BuildLibraryDlg.Pages.modifications);
            RunUI(() =>
            {
                AssertEx.AreEqual(1, buildLibraryDlg.ModificationsControl.FixedMods.Count());
                AssertEx.AreEqual(0, buildLibraryDlg.ModificationsControl.VariableMods.Count());
            });

            // Search Settings: the Stellar subset is unit resolution
            NextPage(buildLibraryDlg, BuildLibraryDlg.Pages.search_settings);
            RunUI(() =>
            {
                AssertEx.AreEqual(Resources.BuildLibraryDlg_OkWizardPage_Finish, buildLibraryDlg.NextButtonText);
                buildLibraryDlg.SearchSettingsControl.IsHighResolution = false;
                AssertEx.IsFalse(buildLibraryDlg.SearchSettingsControl.IsFragmentTolerancePpm);
                buildLibraryDlg.SearchSettingsControl.Threads = Environment.ProcessorCount;
            });

            // Additional Settings from the page: a fine-tuned build's every scope, the tuning's
            // among them; a flag of the search is set, which the one command then carries
            var settings = buildLibraryDlg.SearchSettingsControl.AdditionalSettings;
            var grid = ShowDialog<OspreySettingsGridDlg>(buildLibraryDlg.SearchSettingsControl.ShowAdditionalSettings);
            RunUI(() =>
            {
                AssertEx.IsTrue(grid.Labels.Contains(settings.Properties.Find(@"tf").Label));
                grid.SetValue(settings.Properties.Find(@"verbose").Label, true.ToString());
            });
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
                AssertEx.AreEqualDeep(tuningRuns, buildLibraryDlg.TuningFiles);
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
            AssertEx.AreEqualDeep(tuningRuns, builder.TuningConfig.TrainingInputs);
            AssertEx.AreEqual(OspreyHelpers.RESOLUTION_UNIT, builder.TuningConfig.Resolution);
            AssertEx.AreEqual(OspreyHelpers.GetLibraryModelPath(libraryPath), builder.TunedModelPath);
            AssertEx.Contains(OspreyHelpers.BuildCommandLine(builder.TuningConfig), OspreyHelpers.ARG_PREFIX + @"verbose");
            WaitForClosedForm<BuildLibraryDlg>();
            WaitForCondition(OspreySearchTest.SEARCH_TIMEOUT_MILLIS, () => peptideSettings.AvailableLibraries.Contains(TUNED_LIBRARY_NAME));
            AssertEx.FileExists(libraryPath);
            // The document's proteins reach the library, as a FASTA's do on Osprey's own route
            AssertLibraryProteins(libraryPath, SkylineWindow.Document);
            // The library's model, beside it, records the runs it was tuned on
            AssertEx.FileExists(builder.TunedModelPath, @"the fine-tuned model was not saved beside the library");
            var choice = CarafeModelChoice.Read(builder.TunedModelPath);
            Assert.IsNotNull(choice, @"the fine-tuned model cannot be read");
            AssertEx.AreEqual(tuningRuns.Length, choice.TrainingRunCount);
            // Nothing joins the model list
            RunUI(() => AssertEx.AreEqual(1, Settings.Default.CarafeModelList.Count));

            RunUI(() => peptideSettings.PickedLibraries = new[] { TUNED_LIBRARY_NAME });
            OkDialog(peptideSettings, peptideSettings.OkDialog);
            WaitForDocumentLoaded();
            AssertLibrarySpectra(TUNED_LIBRARY_NAME);
            return builder.TunedModelPath;
        }

        /// <summary>
        /// The library's model joins the model list through Edit list..., which shows what it was
        /// trained on and refuses its name twice; a copy added through the combo's Add... is removed
        /// again; and a second library is predicted with the listed model as it is: no fine-tune, the
        /// model named to the builder, retention times in the tuning runs' minutes.
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
            // What the model was trained on: the three tuning runs, their instrument, NCE and gradient,
            // and a fine-tuned Chronologer RT model that fits them
            var choice = CarafeModelChoice.Read(tunedModelPath);
            Assert.IsNotNull(choice, @"the fine-tuned model cannot be read");
            AssertEx.AreEqual(RtModelType.chronologer, CarafeModelFile.Open(tunedModelPath).RtModel);
            AssertEx.AreEqual(3, choice.TrainingRunCount);
            AssertEx.AreEqual(30, choice.Nce);
            AssertEx.IsTrue(choice.GradientMinutes > PEPTIDES.Max(p => p.RetentionTime));
            // The RT model's R2 on the held-out rows, 4 or 5 peptides of this subset's ~140 forms,
            // so it moves with which peptides the split holds out (the pretrained model scores 0.92
            // to 0.98 on them, the fine-tuned 0.96 to 0.997); the bar catches a fit that failed
            // (0 when the training export had one run's rows), not a quality grade
            AssertEx.IsTrue(choice.RtR2 > 0.9, string.Format(@"RT R2 {0}", choice.RtR2));

            // Edit list... shows no models added; its Add... takes a name and a file, shows what the
            // file was trained on, and refuses a name already in the list
            var listDlg = ShowDialog<CarafeModelListDlg>(buildLibraryDlg.EditCarafeModelList);
            RunUI(() => AssertEx.AreEqual(0, listDlg.Models.Count));
            var editDlg = ShowDialog<EditCarafeModelDlg>(listDlg.AddItem);
            RunUI(() =>
            {
                editDlg.ModelName = SAVED_MODEL_NAME;
                editDlg.ModelPath = tunedModelPath;
                AssertEx.Contains(editDlg.Details, choice.Instrument);
            });
            OkDialog(editDlg, editDlg.OkDialog);
            var editAgainDlg = ShowDialog<EditCarafeModelDlg>(listDlg.AddItem);
            RunUI(() =>
            {
                editAgainDlg.ModelName = SAVED_MODEL_NAME;
                editAgainDlg.ModelPath = tunedModelPath;
            });
            RunDlg<MessageDlg>(editAgainDlg.OkDialog, dlg =>
            {
                AssertEx.AreComparableStrings(SettingsUIResources.EditCarafeModelDlg_OkDialog_The_model__0__already_exists_, dlg.Message);
                dlg.OkDialog();
            });
            OkDialog(editAgainDlg, editAgainDlg.CancelDialog);
            OkDialog(listDlg, listDlg.OkDialog);

            // The combo's own Add... adds a copy and selects it; Edit list... removes it, and the
            // choice falls back to the pretrained models
            RunDlg<EditCarafeModelDlg>(buildLibraryDlg.AddCarafeModel, dlg =>
            {
                dlg.ModelName = COPY_MODEL_NAME;
                dlg.ModelPath = tunedModelPath;
                dlg.OkDialog();
            });
            RunUI(() =>
            {
                AssertEx.AreEqual(COPY_MODEL_NAME, buildLibraryDlg.CarafeModel.Name);
                AssertEx.AreEqual(3, Settings.Default.CarafeModelList.Count);
            });
            RunDlg<CarafeModelListDlg>(buildLibraryDlg.EditCarafeModelList, dlg =>
            {
                AssertEx.AreEqual(2, dlg.Models.Count);
                dlg.SelectItem(COPY_MODEL_NAME);
                dlg.RemoveItem();
                AssertEx.AreEqual(SAVED_MODEL_NAME, dlg.Models.Single().Name);
                dlg.OkDialog();
            });
            RunUI(() =>
            {
                var models = Settings.Default.CarafeModelList;
                AssertEx.AreEqual(2, models.Count);
                AssertEx.IsTrue(buildLibraryDlg.CarafeModel.IsDefault);
                // The list's model is offered by name, with what it was trained on
                AssertEx.Contains(models.GetDisplayName(models[1]), SAVED_MODEL_NAME, choice.Instrument,
                    string.Format(SettingsUIResources.CarafeModelChoice_GetDetails__0__min_gradient,
                        choice.GradientMinutes.Value.ToString(@"0.#", LocalizationHelper.CurrentCulture)));
                buildLibraryDlg.CarafeModel = models[1];
                AssertEx.AreEqual(SAVED_MODEL_NAME, buildLibraryDlg.CarafeModel.Name);
                AssertEx.AreEqual(tunedModelPath, buildLibraryDlg.CarafeModelPath);
                AssertEx.IsTrue(buildLibraryDlg.UseExistingModel);
                AssertEx.AreEqual(Resources.BuildLibraryDlg_OkWizardPage_Finish, buildLibraryDlg.NextButtonText);
            });

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

            // A fine-tuned RT model predicts on its training runs' gradient: minutes, near what the
            // middle run measured, not iRT
            var library = SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Single(lib => lib != null && lib.Name == SAVED_LIBRARY_NAME);
            foreach (var peptide in PEPTIDES)
            {
                var spectrum = library.GetSpectra(new LibKey(peptide.Sequence, 2), null, LibraryRedundancy.best).SingleOrDefault();
                Assert.IsNotNull(spectrum, peptide.Sequence + @" is not in the library");
                Assert.IsNotNull(spectrum.RetentionTime, peptide.Sequence);
                double predicted = spectrum.RetentionTime.Value;
                AssertEx.IsTrue(Math.Abs(predicted - peptide.RetentionTime) <= RT_TOLERANCE,
                    string.Format(@"{0}: predicted {1:F2} min, measured {2:F2} min", peptide.Sequence, predicted, peptide.RetentionTime));
            }
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
            NextPage(buildLibraryDlg, BuildLibraryDlg.Pages.predict_for);
            // The FASTA box starts empty and must name a file that exists
            RunUI(() => AssertEx.IsTrue(string.IsNullOrEmpty(buildLibraryDlg.PredictForFastaPath)));
            ShowAndDismissMessage(buildLibraryDlg, SettingsUIResources.BuildLibraryDlg_ValidatePage_Choose_the_protein_FASTA_file_to_predict_the_library_for_,
                BuildLibraryDlg.Pages.predict_for);
            string missingFasta = TestFilesDir.GetTestPath("missing.fasta");
            RunUI(() => buildLibraryDlg.PredictForFastaPath = missingFasta);
            ShowAndDismissMessage(buildLibraryDlg, string.Format(SettingsUIResources.BuildLibraryDlg_ValidatePage_The_FASTA_file__0__does_not_exist_, missingFasta),
                BuildLibraryDlg.Pages.predict_for);
            const double entrapmentPercent = 10;
            RunUI(() =>
            {
                buildLibraryDlg.PredictForFastaPath = fastaPath;
                buildLibraryDlg.AddDecoys = true;
                buildLibraryDlg.AddEntrapment = true;
                AssertEx.IsFalse(buildLibraryDlg.EntrapmentFromFasta);
                // The percentages start at the user's last values, 10% and 10% to begin with
                AssertEx.AreEqual(entrapmentPercent, buildLibraryDlg.EntrapmentPercent);
                AssertEx.AreEqual(entrapmentPercent, buildLibraryDlg.EntrapmentInTargetsPercent);
                buildLibraryDlg.EntrapmentPercent = EntrapmentPercentControls.MIN_TOTAL_PERCENT / 2;
                buildLibraryDlg.EntrapmentInTargetsPercent = 0;
            });
            // Together the percentages must reach what CarafeSharp builds
            ShowAndDismissMessage(buildLibraryDlg, string.Format(SettingsUIResources.EntrapmentPercentControls_Validate_The_entrapment_percentages_together_must_be_at_least__0___and_at_most__1___,
                EntrapmentPercentControls.MIN_TOTAL_PERCENT, EntrapmentPercentControls.MAX_PERCENT), BuildLibraryDlg.Pages.predict_for);
            RunUI(() =>
            {
                buildLibraryDlg.EntrapmentPercent = buildLibraryDlg.EntrapmentInTargetsPercent = entrapmentPercent;
                buildLibraryDlg.EntrapmentFromFasta = true;
            });
            // Entrapment from a FASTA file needs the file; shuffled from the predicted FASTA's own peptides needs nothing more
            ShowAndDismissMessage(buildLibraryDlg, SettingsUIResources.BuildLibraryDlg_ValidatePage_Choose_the_FASTA_file_of_the_entrapment_peptides_,
                BuildLibraryDlg.Pages.predict_for);
            RunUI(() =>
            {
                buildLibraryDlg.EntrapmentFromFasta = false;
                // What CarafeSharp builds with: a tenth of the targets on entrapment proteins of
                // their own and a tenth placed in target proteins, no decoys of its own
                var entrapmentSettings = buildLibraryDlg.GetEntrapmentSettings(fastaPath);
                AssertEx.AreEqual(fastaPath, entrapmentSettings.InputFasta);
                AssertEx.AreEqual(entrapmentPercent / 100, entrapmentSettings.EntrapmentRatio);
                AssertEx.AreEqual(entrapmentPercent / 100, entrapmentSettings.EntrapmentInTargetsRatio);
                AssertEx.IsTrue(entrapmentSettings.AddEntrapment);
                AssertEx.IsFalse(entrapmentSettings.AddDecoys);
                AssertEx.AreEqual(Resources.BuildLibraryDlg_OkWizardPage_Finish, buildLibraryDlg.NextButtonText);
            });
            RunUI(buildLibraryDlg.OkWizardPage);
            WaitForCondition(() => buildLibraryDlg.Builder != null);
            var builder = (CarafeLibraryBuilder)buildLibraryDlg.Builder;
            AssertEx.IsFalse(builder.IsTuning);
            // The FASTA's document, not the open one: the same peptides, digested the same way,
            // then the entrapment peptides, a fifth of the targets or so rather than one per target,
            // and a decoy for each
            AssertEx.IsFalse(ReferenceEquals(SkylineWindow.Document, builder.Document));
            int targetCount = builder.Document.Peptides.Count(p => !p.IsDecoy);
            int entrapmentCount = builder.EntrapmentTargets.Count;
            AssertEx.IsTrue(entrapmentCount > 0, @"the entrapment FASTA added no peptides");
            AssertEx.IsTrue(entrapmentCount < SkylineWindow.Document.PeptideCount / 2,
                string.Format(@"{0} entrapment peptides for {1} targets is not a fifth of them", entrapmentCount, SkylineWindow.Document.PeptideCount));
            AssertEx.AreEqual(SkylineWindow.Document.PeptideCount + entrapmentCount, targetCount);
            AssertEx.AreEqual(targetCount, builder.Document.Peptides.Count(p => p.IsDecoy));
            AssertEx.AreEqual(BuildLibraryDlg.DECOY_METHOD_REVERSE, builder.DecoyMethod);
            // Each entrapment peptide carries CarafeSharp's label: an entrapment protein of its own,
            // or, for the tenth placed in target proteins, a target protein's clean name
            var entrapmentProteinIds = builder.EntrapmentProteinIds;
            AssertEx.AreEqual(entrapmentCount, entrapmentProteinIds.Count);
            var proteinNames = new HashSet<string>(builder.Document.PeptideGroups.Where(g => g.IsProtein).Select(g => g.Name));
            var ownProtein = entrapmentProteinIds.Where(p => p.Value.Contains(EntrapmentFastaSettings.DEFAULT_ENTRAPMENT_SUFFIX)).ToList();
            var inTargets = entrapmentProteinIds.Where(p => !p.Value.Contains(EntrapmentFastaSettings.DEFAULT_ENTRAPMENT_SUFFIX)).ToList();
            AssertEx.IsTrue(ownProtein.Count > 0 && inTargets.Count > 0,
                string.Format(@"{0} entrapment peptides on their own proteins, {1} placed in target proteins", ownProtein.Count, inTargets.Count));
            // Compared by accession: CarafeSharp spells a label db|accession|entry from the FASTA
            // header, and this FASTA's shared-peptide records (sp|A|A;sp|B|B) parse differently there
            var proteinAccessions = new HashSet<string>(proteinNames.SelectMany(n => n.Split(';')).Select(Accession));
            AssertEx.IsTrue(inTargets.All(p => proteinAccessions.Contains(Accession(p.Value))),
                @"an entrapment peptide placed in a target protein carries a label that is not a target protein's: " +
                string.Join(@", ", inTargets.Select(p => p.Value).Where(v => !proteinAccessions.Contains(Accession(v))).Take(3)));
            WaitForClosedForm<BuildLibraryDlg>();
            WaitForCondition(OspreySearchTest.SEARCH_TIMEOUT_MILLIS, () => peptideSettings.AvailableLibraries.Contains(FASTA_LIBRARY_NAME));
            AssertEx.FileExists(libraryPath);
            // The FASTA's proteins reach the library; an entrapment peptide links to its label's
            // proteins, flagged as entrapment either way, and a decoy to its target's with the decoy prefix
            var libraryProteins = AssertLibraryProteins(libraryPath, builder.Document);
            var entrapmentSequences = LibraryDecoyPairs.ReadEntrapmentSequences(libraryPath);
            int entrapmentChecked = 0;
            foreach (var pair in entrapmentProteinIds)
            {
                if (!libraryProteins.TryGetValue(pair.Key, out var accessions))
                    continue;   // A peptide the prediction left out
                AssertEx.IsTrue(accessions.SetEquals(pair.Value.Split(';')),
                    string.Format(@"the entrapment peptide {0} links to {1}, not {2}", pair.Key, string.Join(@";", accessions), pair.Value));
                AssertEx.IsTrue(entrapmentSequences.Contains(pair.Key), @"not flagged as entrapment: " + pair.Key);
                entrapmentChecked++;
            }
            AssertEx.IsTrue(entrapmentChecked > inTargets.Count / 2, string.Format(@"only {0} entrapment peptides in the library", entrapmentChecked));
            var decoyAccessions = builder.Document.Peptides.Where(p => p.IsDecoy)
                .Select(p => libraryProteins.TryGetValue(p.Target.Sequence, out var accessions) ? accessions : null)
                .Where(a => a != null).ToList();
            AssertEx.IsTrue(decoyAccessions.Count > 0, @"no decoy links to a protein");
            AssertEx.IsTrue(decoyAccessions.All(a => a.All(accession => accession.StartsWith(EntrapmentFastaSettings.DEFAULT_DECOY_PREFIX))),
                @"a decoy links to a protein without the decoy prefix");
            // The library pairs its decoys with their targets and flags the entrapment peptides,
            // as Osprey's own libraries do
            AssertEx.IsTrue(OspreyHelpers.LibraryHasDecoyPairs(libraryPath), @"the library has no DecoyPairs table");
            LibraryDecoyPairs.Count(libraryPath, out int decoyRows, out int entrapmentRows);
            AssertEx.IsTrue(decoyRows > 0, @"no decoy rows in DecoyPairs");
            var entrapmentSet = new HashSet<Target>(builder.EntrapmentTargets);
            int entrapmentPrecursors = builder.Document.Peptides.Where(p => !p.IsDecoy && entrapmentSet.Contains(p.Target)).Sum(p => p.TransitionGroupCount);
            // The counts go to the test output as the record of what the library holds
            Console.WriteLine(@"DecoyPairs of {0}: {1} decoy rows, {2} entrapment rows for {3} entrapment peptides ({4} precursors); first entrapment peptides: {5}",
                Path.GetFileName(libraryPath), decoyRows, entrapmentRows, entrapmentCount, entrapmentPrecursors,
                string.Join(@", ", builder.EntrapmentTargets.Take(3).Select(t => t.Sequence)));
            RunUI(() => peptideSettings.PickedLibraries = new[] { FASTA_LIBRARY_NAME });
            OkDialog(peptideSettings, peptideSettings.OkDialog);
            WaitForDocumentLoaded();
            AssertLibrarySpectra(FASTA_LIBRARY_NAME);
            // A row per paired entrapment precursor (charges 2 and 3) the library holds. Nearly every
            // one is paired: CarafeSharp leaves out a pair whose target or decoy predicts too few
            // fragments, and writes a precursor unpaired when its partner side has no precursor of
            // its charge and modifications to predict (2 of 5,334 on a larger Stellar subset).
            var library = SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Single(lib => lib != null && lib.Name == FASTA_LIBRARY_NAME);
            int heldEntrapmentPrecursors = builder.Document.Peptides.Where(p => !p.IsDecoy && entrapmentSet.Contains(p.Target))
                .Sum(p => p.TransitionGroups.Count(tg => library.Contains(tg.GetLibKey(builder.Document.Settings, p))));
            string entrapmentCounts = string.Format(@"{0} entrapment rows, {1} entrapment precursors in the library, {2} in the document",
                entrapmentRows, heldEntrapmentPrecursors, entrapmentPrecursors);
            AssertEx.IsTrue(entrapmentRows <= heldEntrapmentPrecursors && heldEntrapmentPrecursors <= entrapmentPrecursors, entrapmentCounts);
            AssertEx.IsTrue(entrapmentRows > entrapmentPrecursors * 0.99, entrapmentCounts);
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

        /// <summary>The accession of a db|accession|entry label, or the label itself when it is not one.</summary>
        private static string Accession(string label)
        {
            var parts = label.Split('|');
            return parts.Length > 1 ? parts[1] : label;
        }

        /// <summary>Next refuses with <paramref name="message"/> and stays on <paramref name="page"/>.</summary>
        private static void ShowAndDismissMessage(BuildLibraryDlg buildLibraryDlg, string message, BuildLibraryDlg.Pages page)
        {
            var messageDlg = ShowDialog<MessageDlg>(buildLibraryDlg.OkWizardPage);
            AssertEx.AreEqual(message, messageDlg.Message);
            OkDialog(messageDlg, messageDlg.OkDialog);
            RunUI(() => AssertEx.AreEqual(page, buildLibraryDlg.CurrentPage));
        }
    }
}
