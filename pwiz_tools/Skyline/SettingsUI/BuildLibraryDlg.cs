/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.BiblioSpec;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Koina;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using File = System.IO.File;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// Build Library: the library's name and output, its data source, and the pages the source
    /// leads to. The Files source goes on to its input files; the Carafe and Koina sources
    /// to what the library is predicted for (the document's precursors, or a protein FASTA's); and
    /// Carafe then to the model Osprey predicts with, which may be fine-tuned first on DIA
    /// runs Osprey searches against a protein FASTA - the Osprey Search wizard's Library Model,
    /// Modifications and Search Settings pages, hosted here. Everything is chosen before Finish;
    /// the tuning, the prediction and the import then run as one job.
    /// </summary>
    public partial class BuildLibraryDlg : FormEx, IMultipleViewProvider
    {
        public BuildLibraryGridView Grid { get; }
        public static string[] RESULTS_EXTS =>
            Program.ModeUI == SrmDocument.DOCUMENT_TYPE.small_molecules ? RESULTS_EXTS_SMALL_MOL : RESULTS_EXTS_PEPTIDES;

        public static readonly string[] RESULTS_EXTS_PEPTIDES =
        {
            BiblioSpecLiteBuilder.EXT_DAT,
            BiblioSpecLiteBuilder.EXT_PEP_XML,
            BiblioSpecLiteBuilder.EXT_PEP_XML_ONE_DOT,
            BiblioSpecLiteBuilder.EXT_MZID,
            BiblioSpecLiteBuilder.EXT_MZID_GZ,
            BiblioSpecLiteBuilder.EXT_XTAN_XML,
            BiblioSpecLiteBuilder.EXT_PROTEOME_DISC,
            BiblioSpecLiteBuilder.EXT_PROTEOME_DISC_FILTERED,
            BiblioSpecLiteBuilder.EXT_PILOT,
            BiblioSpecLiteBuilder.EXT_PILOT_XML,
            BiblioSpecLiteBuilder.EXT_PRIDE_XML,
            BiblioSpecLiteBuilder.EXT_IDP_XML,
            BiblioSpecLiteBuilder.EXT_SQT,
            BiblioSpecLiteBuilder.EXT_SSL,
            BiblioSpecLiteBuilder.EXT_PERCOLATOR,
            BiblioSpecLiteBuilder.EXT_PERCOLATOR_XML,
            BiblioSpecLiteBuilder.EXT_MAX_QUANT,
            BiblioSpecLiteBuilder.EXT_WATERS_MSE,
            BiblioSpecLiteBuilder.EXT_PROXL_XML,
            BiblioSpecLiteBuilder.EXT_TSV,
            BiblioSpecLiteBuilder.EXT_MZTAB,
            BiblioSpecLiteBuilder.EXT_MZTAB_TXT,
            BiblioSpecLiteBuilder.EXT_OPEN_SWATH,
            BiblioSpecLiteBuilder.EXT_SPECLIB,
            BiblioSpecLiteBuilder.EXT_PARQUET,
        };

        public static readonly string[] RESULTS_EXTS_SMALL_MOL =
        {
            BiblioSpecLiteBuilder.EXT_SSL,
        };

        /// <summary>The pages, in the order of the hidden tabs; which ones a build visits depends on its source.</summary>
        public enum Pages { properties, files, predict_for, library_model, fasta, modifications, search_settings }

        public class PropertiesPage : IFormView { }
        public class FilesPage : IFormView { }
        public class PredictForPage : IFormView { }
        public class LibraryModelPage : IFormView { }
        public class FastaPage : IFormView { }
        public class ModificationsPage : IFormView { }
        public class SearchSettingsPage : IFormView { }

        private static readonly IFormView[] TAB_PAGES =
        {
            new PropertiesPage(), new FilesPage(), new PredictForPage(), new LibraryModelPage(), new FastaPage(),
            new ModificationsPage(), new SearchSettingsPage()
        };

        public enum DataSourcePages { files, alpha, koina }

        private readonly MessageBoxHelper _helper;
        private readonly IDocumentUIContainer _documentUiContainer;
        private readonly SkylineWindow _skylineWindow;

        private readonly SettingsListComboDriver<IrtStandard> _driverStandards;

        // Values used to keep from overwriting user changes to synchronized fields
        private string _lastUpdatedFileName;
        private string _lastUpdatedLibName;

        // The pages visited before the current one, for Previous
        private readonly Stack<Pages> _pageHistory = new Stack<Pages>();
        private readonly CarafeModelCombo _carafeModelCombo;
        private UseGpuDriver _useGpuDriver;
        private bool _useGpuUpdated;

        public BuildLibraryDlg(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            // On .NET a control anchored to the bottom of a TabPage comes out short at high DPI
            // (issue #4599): size the grid below its label and left of the buttons by hand.
            DpiUtil.FillTabPage(tabFiles, gridInputFiles, tabFiles.ClientSize.Width - gridInputFiles.Right);

            Icon = Resources.Skyline;

            _skylineWindow = skylineWindow;
            _documentUiContainer = skylineWindow;

            textName.Focus();
            textPath.Text = Settings.Default.LibraryDirectory;
            comboAction.SelectedItem = LibraryBuildAction.Create.GetLocalizedString();

            if (_documentUiContainer.Document.PeptideCount == 0)
                cbFilter.Hide();
            else
                cbFilter.Checked = Settings.Default.LibraryFilterDocumentPeptides;

            cbKeepRedundant.Checked = Settings.Default.LibraryKeepRedundant;

            _carafeModelCombo = new CarafeModelCombo(comboCarafeModel);
            // Use GPU is as wide as its caption in the current language, so it is placed from the
            // group box's right edge, and the model combo ends before it, whatever that width is
            cbUseGpu.Left = groupAlphaOptions.ClientSize.Width - cbUseGpu.Width - labelCarafeModel.Left;
            comboCarafeModel.Width = cbUseGpu.Left - comboCarafeModel.Left - labelCarafeModel.Left;
            PredictForDocument = true;
            EntrapmentFromFasta = false;
            helpTip.SetToolTip(cbAddDecoys, SettingsUIResources.BuildLibraryDlg_BuildLibraryDlg_A_reverse_sequence_decoy_of_every_peptide__paired_with_its_target_in_the_library);
            helpTip.SetToolTip(cbAddEntrapment, SettingsUIResources.BuildLibraryDlg_BuildLibraryDlg_Peptides_not_expected_in_the_sample__flagged_as_entrapment_in_the_library_to_measure_the_false_discovery_rate);

            ceCombo.Items.AddRange(
                Enumerable.Range(KoinaConstants.MIN_NCE, KoinaConstants.MAX_NCE - KoinaConstants.MIN_NCE + 1).Select(c => (object)c)
                    .ToArray());
            ceCombo.SelectedItem = Settings.Default.KoinaNCE;

            _helper = new MessageBoxHelper(this);

            _driverStandards = new SettingsListComboDriver<IrtStandard>(comboStandards, Settings.Default.IrtStandardList);
            _driverStandards.LoadList(IrtStandard.EMPTY.GetKey());

            Grid = gridInputFiles;
            Grid.FilesChanged += (sender, e) =>
            {
                if (CurrentPage == Pages.files)
                {
                    btnNext.Enabled = Grid.IsReady;
                }
            };

            // If we're not using dataSourceGroupBox (because we're in small molecule mode) shift other controls over where it was
            if (modeUIHandler.ComponentsDisabledForModeUI(dataSourceGroupBox))
                tabControlDataSource.Left = dataSourceGroupBox.Left;

            // The Osprey Search wizard's pages, for the fine-tune: the runs, the FASTA they are
            // searched against, the modifications and the search settings. The model and Use GPU
            // are on the first page, so the runs page shows the runs alone.
            var document = _documentUiContainer.DocumentUI;
            LibraryModelControl = new OspreyLibraryModelControl { DocumentFilePath = skylineWindow.DocumentFilePath };
            LibraryModelControl.HideSearchedFilesChoice();
            LibraryModelControl.HideModelChoice();
            WizardPages.AddPageControl(LibraryModelControl, tabLibraryModel, 10, 50, 4);
            FastaControl = new OspreyFastaControl { DocumentFilePath = skylineWindow.DocumentFilePath };
            FastaControl.Initialize(document.Settings.PeptideSettings);
            WizardPages.AddPageControl(FastaControl, tabFasta, 10, 50, 4);
            ModificationsControl = new OspreyModificationsControl();
            ModificationsControl.SetModifications(document.Settings.PeptideSettings.Modifications.StaticModifications);
            WizardPages.AddPageControl(ModificationsControl, tabModifications, 10, 50, 4);
            SearchSettingsControl = new OspreySearchSettingsControl();
            WizardPages.AddPageControl(SearchSettingsControl, tabSearchSettings, 10, 50, 4);
            UpdatePredictForControls();
        }

        public OspreyLibraryModelControl LibraryModelControl { get; }
        public OspreyFastaControl FastaControl { get; }
        public OspreyModificationsControl ModificationsControl { get; }
        public OspreySearchSettingsControl SearchSettingsControl { get; }

        private void BuildLibraryDlg_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!Settings.Default.IrtStandardList.Contains(IrtStandard.AUTO))
            {
                Settings.Default.IrtStandardList.Insert(0, IrtStandard.AUTO);
            }
        }

        public ILibraryBuilder Builder { get; private set; }

        public IEnumerable<string> InputFileNames
        {
            get => Grid.FilePaths;
            set => Grid.FilePaths = value;
        }

        public string AddLibraryFile { get; private set; }

        public bool ValidateBuilder(bool validateInputFiles)
        {
            string name;
            if (!_helper.ValidateNameTextBox(textName, out name))
                return false;

            string outputPath = textPath.Text;

            if (string.IsNullOrEmpty(outputPath))
            {
                _helper.ShowTextBoxError(textPath, SettingsUIResources.BuildLibraryDlg_ValidateBuilder_You_must_specify_an_output_file_path, outputPath);
                return false;
            }
            if (Directory.Exists(outputPath))
            {
                _helper.ShowTextBoxError(textPath, SettingsUIResources.BuildLibraryDlg_ValidateBuilder_The_output_path__0__is_a_directory_You_must_specify_a_file_path, outputPath);
                return false;
            }
            string outputDir = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(outputDir))
            {
                _helper.ShowTextBoxError(textPath, SettingsUIResources.BuildLibraryDlg_ValidateBuilder_You_must_specify_an_output_file_path, outputPath);
                return false;
            }
            if (!Directory.Exists(outputDir))
            {
                _helper.ShowTextBoxError(textPath, SettingsUIResources.BuildLibraryDlg_ValidateBuilder_The_directory__0__does_not_exist, outputDir);
                return false;
            }
            if (!outputPath.EndsWith(BiblioSpecLiteSpec.EXT))
                outputPath += BiblioSpecLiteSpec.EXT;
            try
            {
                using (var sfLib = new FileSaver(outputPath))
                {
                    if (!sfLib.CanSave(this))
                    {
                        textPath.Focus();
                        textPath.SelectAll();
                        return false;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                _helper.ShowTextBoxError(textPath, TextUtil.LineSeparate(SettingsUIResources.BuildLibraryDlg_ValidateBuilder_Access_violation_attempting_to_write_to__0__,
                    SettingsUIResources.BuildLibraryDlg_ValidateBuilder_Please_check_that_you_have_write_access_to_this_folder_), outputDir);
                return false;
            }
            catch (IOException)
            {
                _helper.ShowTextBoxError(textPath, TextUtil.LineSeparate(SettingsUIResources.BuildLibraryDlg_ValidateBuilder_Failure_attempting_to_create_a_file_in__0__,
                    SettingsUIResources.BuildLibraryDlg_ValidateBuilder_Please_check_that_you_have_write_access_to_this_folder_), outputDir);
                return false;
            }

            if (validateInputFiles)
            {
                if (radioKoinaSource.Checked)
                {
                    if (!CreateKoinaBuilder(name, outputPath, NCE))
                        return false;
                }
                else if (radioAlphaSource.Checked)
                {
                    if (!CreateCarafeBuilder(name, outputPath))
                        return false;
                }
                else
                {
                    if (!Grid.Validate(this, null, true, out var thresholdsByFile))
                        return false;

                    List<Target> targetPeptidesChosen = null;
                    if (cbFilter.Checked)
                    {
                        targetPeptidesChosen = new List<Target>();
                        var doc = _documentUiContainer.Document;
                        foreach (PeptideDocNode nodePep in doc.Peptides)
                        {
                            // Add light modified sequences
                            targetPeptidesChosen.Add(nodePep.ModifiedTarget);
                            // Add heavy modified sequences
                            foreach (var nodeGroup in nodePep.TransitionGroups)
                            {
                                if (nodeGroup.TransitionGroup.LabelType.IsLight)
                                    continue;
                                targetPeptidesChosen.Add(doc.Settings.GetModifiedSequence(nodePep.Peptide.Target,
                                    nodeGroup.TransitionGroup.LabelType,
                                    nodePep.ExplicitMods,
                                    SequenceModFormatType.lib_precision));
                            }
                        }
                    }

                    Builder = new BiblioSpecLiteBuilder(name, outputPath, InputFileNames.ToArray(), targetPeptidesChosen)
                    {
                        Action = LibraryBuildAction,
                        IncludeAmbiguousMatches = cbIncludeAmbiguousMatches.Checked,
                        KeepRedundant = LibraryKeepRedundant,
                        ScoreThresholdsByFile = thresholdsByFile,
                        Id = Helpers.MakeId(textName.Text),
                        IrtStandard = _driverStandards.SelectedItem,
                        PreferEmbeddedSpectra = PreferEmbeddedSpectra
                    };
                }
            }
            return true;
        }

        private bool CreateCarafeBuilder(string name, string outputPath)
        {
            if (PredictForDocument && !_documentUiContainer.DocumentUI.HasPeptides)
            {
                MessageDlg.Show(this, SettingsUIResources.BuildLibraryDlg_CreateCarafeBuilder_Add_peptide_precursors_to_the_document_to_build_a_library_from_Carafe_predictions_);
                return false;
            }
            // A model added to the list some time ago may have been moved or damaged since
            var carafeModel = CarafeModel;
            if (!carafeModel.IsDefault && !EditCarafeModelDlg.ValidateModelPath(this, carafeModel.FilePath))
                return false;
            // Osprey predicts the library; offered for download when the source was chosen, and
            // again here in case that was declined
            if (!OspreyInstaller.EnsureInstalled(this))
                return false;
            var doc = GetDocumentToPredict();
            if (doc == null)
                return false;

            var builder = new CarafeLibraryBuilder(name, outputPath, doc, IrtStandard)
            {
                CarafeModelPath = carafeModel.FilePath,
                CarafeModelName = carafeModel.Name,
                TorchRuntimes = TorchRuntimes,
                UseGpu = UseGpu,
                TuningConfig = IsTuning ? GetTuningConfig() : null,
                DecoyMethod = DecoyMethod,
                EntrapmentTargets = _entrapmentTargets
            };
            // Asked here, while this form is still up, so that Cancel returns to it rather than to
            // Peptide Settings with everything chosen here gone
            string warning = builder.GetWarning();
            if (!string.IsNullOrEmpty(warning) &&
                MultiButtonMsgDlg.Show(this, warning, MessageBoxButtons.OKCancel) == DialogResult.Cancel)
            {
                return false;
            }
            Builder = builder;
            UseGpuDriver.SaveSetting();

            return true;
        }

        /// <summary>
        /// The fine-tune's search, as Osprey's one command takes it: the FASTA and its digest from
        /// the FASTA page, the modifications from theirs, the search settings from theirs, and the
        /// peptide length and precursor charge filters and the variable modification limit from the
        /// document, since the training library should cover what the document's library does.
        /// The precursor table, the output and the device are the builder's to fill in.
        /// </summary>
        private OspreyConfig GetTuningConfig()
        {
            var settings = _documentUiContainer.DocumentUI.Settings;
            var charges = settings.TransitionSettings.Filter.PeptidePrecursorCharges.Select(a => a.AdductCharge).ToList();
            var config = new OspreyConfig
            {
                FastaPath = FastaControl.FastaFile,
                TrainingInputs = LibraryModelControl.TuningFiles,
                EnzymeIndex = OspreyHelpers.GetCarafeEnzymeIndex(FastaControl.Enzyme),
                MaxMissedCleavages = FastaControl.MaxMissedCleavages,
                MinPeptideLength = settings.PeptideSettings.Filter.MinPeptideLength,
                MaxPeptideLength = settings.PeptideSettings.Filter.MaxPeptideLength,
                MinCharge = charges.Count > 0 ? charges.Min() : 2,
                MaxCharge = charges.Count > 0 ? charges.Max() : 3,
                MaxVariableMods = settings.PeptideSettings.Modifications.MaxVariableMods
            };
            SearchSettingsControl.ApplyTo(config);
            ModificationsControl.ApplyTo(config);
            return config;
        }

        /// <summary>
        /// The precursors the library is predicted for: the document's, or a protein FASTA's,
        /// digested with the document's settings into a document of its own, as Import FASTA would
        /// add them to the document - except that every peptide the filter allows is kept, with its
        /// precursors, whatever the library pick, rank and auto-select say; then the entrapment
        /// peptides, if asked for, and a
        /// reverse-sequence decoy of every peptide, if asked for, which the library marks in its DecoyPairs
        /// table (<see cref="LibraryDecoyPairs"/>). The open document itself is never changed. Null
        /// when a digest is cancelled, fails or gives no peptides, after reporting it.
        /// </summary>
        private SrmDocument GetDocumentToPredict()
        {
            var doc = _documentUiContainer.DocumentUI;
            _entrapmentTargets = null;
            // A digest for prediction keeps every peptide the filter allows, with every precursor the
            // transition filter allows: the library-match pick would keep only peptides with spectra
            // already, and auto-select off would digest the proteins into no peptides, or the peptides
            // into no precursors, which are the opposite of the point. The rank goes before the pick,
            // since a pick by filter refuses a rank
            var settingsDigest = doc.Settings.ChangePeptideLibraries(lib => lib.ChangeRankId(null).ChangePick(PeptidePick.filter))
                .ChangePeptideFilter(filter => filter.ChangeAutoSelect(true))
                .ChangeTransitionFilter(filter => filter.ChangeAutoSelect(true));
            if (!PredictForDocument)
            {
                doc = ImportFastaToPredict(new SrmDocument(settingsDigest), PredictForFastaPath, out _);
                if (doc == null)
                    return null;
            }
            if (PredictEntrapment)
            {
                // Digested on their own and added as they are, so the document's own proteins are untouched
                List<PeptideGroupDocNode> entrapmentGroups;
                bool peptideList = !EntrapmentFromFasta;
                if (peptideList)
                {
                    // Carafe's entrapment peptides, shuffled from the predicted FASTA's own peptides, as a peptide list
                    var docEntrapment = ImportEntrapmentPeptides(new SrmDocument(settingsDigest), PredictForFastaPath, out entrapmentGroups);
                    if (docEntrapment == null)
                        return null;
                }
                else if (ImportFastaToPredict(new SrmDocument(settingsDigest), EntrapmentFastaPath, out entrapmentGroups) == null)
                {
                    return null;
                }
                _entrapmentTargets = entrapmentGroups.SelectMany(g => g.Peptides).Select(p => p.Target).ToList();
                doc = doc.AddPeptideGroups(entrapmentGroups, peptideList, null, out _, out _);
            }
            if (PredictDecoys)
            {
                var refine = new RefinementSettings { NumberOfDecoys = doc.PeptideCount, DecoysMethod = DecoyGeneration.REVERSE_SEQUENCE };
                doc = refine.GenerateDecoys(doc);
            }
            return doc;
        }

        /// <summary>
        /// Adds <paramref name="fastaPath"/>'s proteins to <paramref name="doc"/>, digested with its
        /// settings, under a cancellable wait; <paramref name="groupsNew"/> are the proteins added.
        /// Null, after reporting it, when the digest is cancelled, fails or gives no peptides.
        /// </summary>
        private SrmDocument ImportFastaToPredict(SrmDocument doc, string fastaPath, out List<PeptideGroupDocNode> groupsNew)
        {
            SrmDocument docFasta = null;
            List<PeptideGroupDocNode> groupsAdded = null;
            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.Text = SettingsUIResources.BuildLibraryDlg_GetDocumentToPredict_Digesting_FASTA;
                longWaitDlg.Message = string.Format(SettingsUIResources.BuildLibraryDlg_GetDocumentToPredict_Digesting__0__with_the_document_settings, fastaPath);
                var status = longWaitDlg.PerformWork(this, 800, monitor =>
                {
                    docFasta = ImportPeptideSearch.ImportFasta(doc, fastaPath, null, monitor, null, out _, out _, out groupsAdded);
                });
                // The proteins as the document holds them: their peptides are made when they join it
                var idsAdded = new HashSet<Identity>((groupsAdded ?? new List<PeptideGroupDocNode>()).Select(g => g.Id));
                groupsNew = docFasta?.PeptideGroups.Where(g => idsAdded.Contains(g.Id)).ToList() ?? new List<PeptideGroupDocNode>();
                if (status.IsCanceled)
                    return null;
                if (status.IsError)
                {
                    MessageDlg.ShowWithException(this, string.Format(
                        SettingsUIResources.BuildLibraryDlg_GetDocumentToPredict_Failed_digesting_the_FASTA_file__0__, fastaPath), status.ErrorException);
                    return null;
                }
            }
            if (docFasta == null || !groupsNew.Any(g => g.PeptideCount > 0))
            {
                MessageDlg.Show(this, string.Format(
                    SettingsUIResources.BuildLibraryDlg_GetDocumentToPredict_The_FASTA_file__0__gave_no_peptides_with_the_document_settings_, fastaPath));
                return null;
            }
            return docFasta;
        }

        /// <summary>
        /// Entrapment peptides generated in process by CarafeSharp from <paramref name="fastaPath"/>'s
        /// proteins (<see cref="EntrapmentFastaBuilder"/>: the FASTA's peptides shuffled, one per
        /// target, the entrapment Osprey's own FASTA route adds), under a cancellable wait, then added
        /// to <paramref name="doc"/> as a peptide list, since they are peptides, not proteins to digest.
        /// Null, after reporting it, when cancelled, failed or empty.
        /// </summary>
        private SrmDocument ImportEntrapmentPeptides(SrmDocument doc, string fastaPath, out List<PeptideGroupDocNode> groupsNew)
        {
            var peptides = new List<string>();
            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.Text = SettingsUIResources.BuildLibraryDlg_GetDocumentToPredict_Digesting_FASTA;
                longWaitDlg.Message = string.Format(SettingsUIResources.BuildLibraryDlg_GetDocumentToPredict_Digesting__0__with_the_document_settings, fastaPath);
                var status = longWaitDlg.PerformWork(this, 800, (IProgressMonitor monitor) =>
                {
                    var settings = new EntrapmentFastaSettings { InputFasta = fastaPath, AddEntrapment = true, AddDecoys = false };
                    var quartets = new EntrapmentFastaBuilder(settings).Build(new EntrapmentFastaResult(), out _);
                    peptides.AddRange(quartets.Where(q => q.EntrapmentSelected && !string.IsNullOrEmpty(q.PTarget)).Select(q => q.PTarget).Distinct());
                });
                groupsNew = new List<PeptideGroupDocNode>();
                if (status.IsCanceled)
                    return null;
                if (status.IsError)
                {
                    MessageDlg.ShowWithException(this, string.Format(
                        SettingsUIResources.BuildLibraryDlg_GetDocumentToPredict_Failed_digesting_the_FASTA_file__0__, fastaPath), status.ErrorException);
                    return null;
                }
            }
            SrmDocument docList = null;
            if (peptides.Count > 0)
            {
                using var reader = new StringReader(TextUtil.LineSeparate(new[] { @">" + ENTRAPMENT_LIST_NAME }.Concat(peptides)));
                docList = doc.ImportFasta(reader, true, null, out _);
                groupsNew = docList.PeptideGroups.ToList();
            }
            if (docList == null || !groupsNew.Any(g => g.PeptideCount > 0))
            {
                MessageDlg.Show(this, string.Format(
                    SettingsUIResources.BuildLibraryDlg_GetDocumentToPredict_The_FASTA_file__0__gave_no_peptides_with_the_document_settings_, fastaPath));
                return null;
            }
            return docList;
        }

        /// <summary>The name of the peptide list the generated entrapment peptides form.</summary>
        private const string ENTRAPMENT_LIST_NAME = @"Entrapment";

        /// <summary>The entrapment FASTA's peptides in the document last made to predict for, for the library's DecoyPairs table.</summary>
        private List<Target> _entrapmentTargets;

        /// <summary>
        /// Decoys and entrapment peptides are the FASTA route's choices: the Predict For page's
        /// boxes count only when the route goes through that page, not when it was visited and
        /// then left for the document's precursors.
        /// </summary>
        private bool PredictDecoys => !PredictForDocument && AddDecoys;

        private bool PredictEntrapment => !PredictForDocument && AddEntrapment;

        /// <summary>The DecoyPairs Method the library records for Skyline's reverse-sequence decoys, or null when none are added.</summary>
        private string DecoyMethod => PredictDecoys ? DECOY_METHOD_REVERSE : null;

        /// <summary>The DecoyPairs Method recorded for reverse-sequence decoys, beside Carafe's "shuffle".</summary>
        public const string DECOY_METHOD_REVERSE = @"reverse";

        #region Predict For page

        /// <summary>
        /// True predicts the library for the document's precursors, and Finish is on the first
        /// page; false for <see cref="PredictForFastaPath"/>'s, chosen on the Predict For page.
        /// The Carafe and Koina tabs each show the choice, kept the same on both.
        /// </summary>
        public bool PredictForDocument
        {
            get => comboCarafePredictFor.SelectedIndex == 0;
            set => comboCarafePredictFor.SelectedIndex = comboKoinaPredictFor.SelectedIndex = value ? 0 : 1;
        }

        private void comboPredictFor_SelectedIndexChanged(object sender, EventArgs e)
        {
            // The other tab's combo follows, so the choice is one whichever tab shows it
            var combo = (ComboBox)sender;
            var other = ReferenceEquals(combo, comboCarafePredictFor) ? comboKoinaPredictFor : comboCarafePredictFor;
            if (other.SelectedIndex != combo.SelectedIndex)
                other.SelectedIndex = combo.SelectedIndex;
            UpdateNextButton();
        }

        /// <summary>The protein FASTA whose peptides and precursors, digested with the document settings, the library is predicted for.</summary>
        public string PredictForFastaPath
        {
            get => textFastaPath.Text;
            set => textFastaPath.Text = value;
        }

        /// <summary>Whether a shuffled-sequence decoy of every peptide is predicted too, paired with its target in the library.</summary>
        public bool AddDecoys
        {
            get => cbAddDecoys.Checked;
            set => cbAddDecoys.Checked = value;
        }

        /// <summary>Whether the peptides of <see cref="EntrapmentFastaPath"/> are predicted too, flagged as entrapment in the library.</summary>
        public bool AddEntrapment
        {
            get => cbAddEntrapment.Checked;
            set => cbAddEntrapment.Checked = value;
        }

        /// <summary>The FASTA of the entrapment proteins, digested with the document settings like the others.</summary>
        public string EntrapmentFastaPath
        {
            get => textEntrapmentFasta.Text;
            set => textEntrapmentFasta.Text = value;
        }

        /// <summary>
        /// True takes the entrapment peptides from <see cref="EntrapmentFastaPath"/>, digested
        /// with the document settings; false shuffles them from the predicted FASTA's own peptides
        /// (CarafeSharp's <see cref="EntrapmentFastaBuilder"/>), the default.
        /// </summary>
        public bool EntrapmentFromFasta
        {
            get => comboEntrapmentMethod.SelectedIndex == 1;
            set => comboEntrapmentMethod.SelectedIndex = value ? 1 : 0;
        }

        private void cbAddEntrapment_CheckedChanged(object sender, EventArgs e)
        {
            UpdatePredictForControls();
        }

        private void comboEntrapmentMethod_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdatePredictForControls();
        }

        /// <summary>The entrapment method shows once entrapment is asked for, and its FASTA box once a FASTA is the method.</summary>
        private void UpdatePredictForControls()
        {
            labelEntrapmentMethod.Visible = comboEntrapmentMethod.Visible = AddEntrapment;
            textEntrapmentFasta.Visible = btnBrowseEntrapment.Visible = AddEntrapment && EntrapmentFromFasta;
        }

        private void btnBrowseEntrapment_Click(object sender, EventArgs e)
        {
            string path = OspreyFastaControl.BrowseFasta(this, _skylineWindow.DocumentFilePath);
            if (path != null)
                EntrapmentFastaPath = path;
        }

        private void btnBrowseFasta_Click(object sender, EventArgs e)
        {
            string path = OspreyFastaControl.BrowseFasta(this, _skylineWindow.DocumentFilePath);
            if (path != null)
                PredictForFastaPath = path;
        }

        /// <summary>
        /// Next or Finish from the Predict For page: the FASTA must exist, and the entrapment
        /// FASTA too when it is the entrapment method.
        /// </summary>
        private bool ValidatePredictForPage()
        {
            if (!ValidateFastaPath(textFastaPath, SettingsUIResources.BuildLibraryDlg_ValidatePage_Choose_the_protein_FASTA_file_to_predict_the_library_for_))
                return false;
            if (AddEntrapment && EntrapmentFromFasta)
                return ValidateFastaPath(textEntrapmentFasta, SettingsUIResources.BuildLibraryDlg_ValidatePage_Choose_the_FASTA_file_of_the_entrapment_peptides_);
            return true;
        }

        /// <summary>A FASTA box must name a file that exists; <paramref name="emptyMessage"/> is the refusal of an empty box.</summary>
        private bool ValidateFastaPath(TextBox textFasta, string emptyMessage)
        {
            string fastaPath = textFasta.Text;
            if (string.IsNullOrWhiteSpace(fastaPath))
            {
                MessageDlg.Show(this, emptyMessage);
                textFasta.Focus();
                return false;
            }
            if (!File.Exists(fastaPath))
            {
                MessageDlg.Show(this, string.Format(SettingsUIResources.BuildLibraryDlg_ValidatePage_The_FASTA_file__0__does_not_exist_, fastaPath));
                textFasta.Focus();
                return false;
            }
            return true;
        }

        #endregion

        #region Carafe options

        /// <summary>The Use GPU checkbox's driver, which asks its questions from this form.</summary>
        private UseGpuDriver UseGpuDriver => _useGpuDriver ??= new UseGpuDriver(this, cbUseGpu);

        /// <summary>
        /// The libtorch runtimes the prediction runs with, which decide whether Use GPU can be checked.
        /// </summary>
        public TorchRuntimes TorchRuntimes
        {
            get { return UseGpuDriver.TorchRuntimes; }
            set { UseGpuDriver.TorchRuntimes = value; }
        }

        /// <summary>
        /// Whether the prediction runs on the GPU. Checking it says why when the GPU cannot be used,
        /// and otherwise downloads the NVIDIA CUDA runtime first, if it is not installed.
        /// </summary>
        public bool UseGpu
        {
            get { return UseGpuDriver.UseGpu; }
            set { UseGpuDriver.UseGpu = value; }
        }

        private void cbUseGpu_CheckedChanged(object sender, EventArgs e)
        {
            UseGpuDriver.CheckedChanged();
        }

        /// <summary>
        /// Checks Use GPU as the user last left it, if the GPU can be used and the CUDA runtime is
        /// installed. Called when the Carafe source is chosen, so the NVIDIA driver is not
        /// loaded for the other sources, and only once, so the user's choice is kept after that.
        /// </summary>
        private void UpdateUseGpu()
        {
            if (_useGpuUpdated)
                return;
            UseGpuDriver.Update();
            _useGpuUpdated = true;
        }

        /// <summary>
        /// The model to predict with: the default, the pretrained AlphaPeptDeep models, or a
        /// fine-tuned model from the list. Setting it selects that model in the combo.
        /// </summary>
        public CarafeModelSpec CarafeModel
        {
            get { return _carafeModelCombo.Model; }
            set { _carafeModelCombo.Model = value; }
        }

        /// <summary>The chosen model's file, or null for the pretrained models.</summary>
        public string CarafeModelPath
        {
            get { return CarafeModel.FilePath; }
        }

        private void comboCarafeModel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_carafeModelCombo.SelectedIndexChanged(sender, e))
                UpdateModificationWarning();
        }

        /// <summary>
        /// True predicts with the chosen model as it is, and Finish is on the first page; false
        /// fine-tunes a new model on <see cref="TuningFiles"/> first, which the pages the Fine-tune
        /// box leads to configure.
        /// </summary>
        public bool UseExistingModel
        {
            get { return !cbCarafeFineTune.Checked; }
            set { cbCarafeFineTune.Checked = !value; }
        }

        private void cbCarafeFineTune_CheckedChanged(object sender, EventArgs e)
        {
            // The tuning starts from the pretrained models, and the runs page enables its list
            _carafeModelCombo.SetTuning(cbCarafeFineTune.Checked);
            LibraryModelControl.UseExistingModel = UseExistingModel;
            UpdateNextButton();
        }

        /// <summary>A fine-tuned build: Osprey searches the tuning runs against the FASTA, tunes the models and predicts with them.</summary>
        public bool IsTuning => radioAlphaSource.Checked && !UseExistingModel;

        /// <summary>The runs the model is fine-tuned on: the checked runs of the Library Model page's list.</summary>
        public IList<string> TuningFiles => LibraryModelControl.TuningFiles;

        /// <summary>What Browse... does with the chosen runs: they go to the top of the list, checked.</summary>
        public void AddTuningFiles(IEnumerable<string> paths)
        {
            LibraryModelControl.AddTuningFiles(paths);
        }

        /// <summary>
        /// Says, as soon as the model is chosen, which of the document's modifications the pretrained
        /// models do not know or support only in part, so the user does not learn it from the
        /// question asked when the build starts.
        /// </summary>
        private void UpdateModificationWarning()
        {
            // What the chosen model supports: the pretrained models' modifications, and a fine-tuned
            // model's training modifications with them
            var warningMods = AbstractDeepLibraryBuilder.GetWarningMods(_documentUiContainer.DocumentUI,
                CarafeLibraryBuilder.GetModificationSupport(CarafeModel.FilePath));
            labelModWarning.Visible = warningMods.Count > 0;
            if (warningMods.Count > 0)
            {
                string mods = warningMods.Keys.OrderBy(name => name).CommaSeparateListItems();
                labelModWarning.Text = string.Format(SettingsUIResources.BuildLibraryDlg_UpdateModificationWarning_Limited_or_no_support_for__0_, mods);
                helpTip.SetToolTip(labelModWarning, AbstractDeepLibraryBuilder.GetWarning(warningMods, false));
            }
        }

        /// <summary>The modifications the pretrained models do not know or support only in part, as shown; for tests.</summary>
        public string ModificationWarning
        {
            get { return labelModWarning.Visible ? labelModWarning.Text : null; }
        }

        /// <summary>The combo's Add..., for tests: opens the model editor and adds its model to the list.</summary>
        public void AddCarafeModel()
        {
            _carafeModelCombo.AddItem();
        }

        /// <summary>The combo's Edit list..., for tests: opens the grid of models.</summary>
        public void EditCarafeModelList()
        {
            _carafeModelCombo.EditList();
        }

        #endregion

        private bool CreateKoinaBuilder(string name, string outputPath, int nce = 27)
        {
            // TODO: Need to figure out a better way to do this, use KoinaPeptidePrecursorPair?
            var doc = GetDocumentToPredict();
            if (doc == null)
                return false;
            // The document's own decoys stay out, as always; the decoys asked for here go in
            var peptides = doc.Peptides.Where(pep => PredictDecoys || !pep.IsDecoy).ToArray();
            var precursorCount = peptides.Sum(pep=>pep.TransitionGroupCount);
            var peptidesPerPrecursor = new PeptideDocNode[precursorCount];
            var precursors = new TransitionGroupDocNode[precursorCount];
            int index = 0;

            for (var i = 0; i < peptides.Length; ++i)
            {
                var groups = peptides[i].TransitionGroups.ToArray();
                Array.Copy(Enumerable.Repeat(peptides[i], groups.Length).ToArray(), 0, peptidesPerPrecursor, index,
                    groups.Length);
                Array.Copy(groups, 0, precursors, index, groups.Length);
                index += groups.Length;
            }

            if (index == 0)
            {
                MessageDlg.Show(this, Resources.BuildLibraryDlg_ValidateBuilder_Add_peptide_precursors_to_the_document_to_build_a_library_from_Koina_predictions_);
                return false;
            }

            try
            {
                KoinaUIHelpers.CheckKoinaSettings(this, _skylineWindow);

                // Still construct the library builder, otherwise a user might configure Koina
                // incorrectly, causing the build to silently fail
                Builder = new KoinaLibraryBuilder(doc, name, outputPath, () => true, IrtStandard,
                    peptidesPerPrecursor, precursors, nce)
                {
                    DecoyMethod = DecoyMethod,
                    EntrapmentTargets = _entrapmentTargets
                };
            }
            catch (Exception ex)
            {
                MessageDlg.ShowWithException(this, ex.Message, ex);
                return false;
            }

            return true;
        }

        private void textName_TextChanged(object sender, EventArgs e)
        {
            string name = textName.Text;
            string outputPath = textPath.Text;
            if (outputPath.Length > 0 && !Directory.Exists(outputPath))
            {
                try
                {
                    // ReSharper disable once ConstantNullCoalescingCondition
                    outputPath = Path.GetDirectoryName(outputPath) ?? string.Empty;
                }
                catch (Exception)
                {
                    outputPath = string.Empty;
                }
            }
            string id = (name.Length == 0 ? string.Empty : Helpers.MakeId(textName.Text));

            if (_lastUpdatedFileName.IsNullOrEmpty() || _lastUpdatedFileName == _lastUpdatedLibName)
            {
                textPath.Text = id.Length == 0 ? outputPath
                    : Path.Combine(outputPath, id + BiblioSpecLiteSpec.EXT);
                _lastUpdatedFileName = id;
                _lastUpdatedLibName = id;

            }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            string fileName;
            try
            {
                fileName = Path.GetFileName(textPath.Text);
            }
            catch (Exception)
            {
                fileName = string.Empty;
            }

            using (var dlg = new SaveFileDialog())
            {
                dlg.InitialDirectory = Settings.Default.LibraryDirectory;
                dlg.FileName = fileName;
                dlg.OverwritePrompt = true;
                dlg.DefaultExt = BiblioSpecLiteSpec.EXT;
                dlg.Filter = TextUtil.FileDialogFiltersAll(BiblioSpecLiteSpec.FILTER_BLIB);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.LibraryDirectory = Path.GetDirectoryName(dlg.FileName);
                    textPath.Text = dlg.FileName;
                    _lastUpdatedFileName = Path.GetFileNameWithoutExtension(dlg.FileName);
                }
            }
        }

        #region Page navigation

        public Pages CurrentPage => (Pages)tabControlMain.SelectedIndex;

        /// <summary>The Next button's text, Next or Finish, for tests.</summary>
        public string NextButtonText => btnNext.Text;

        /// <summary>
        /// The page after <paramref name="page"/> on the route the first page's choices set, or
        /// null when the next step is Finish: the Files source goes to its files; a FASTA's
        /// precursors to the Predict For page; a fine-tune to its runs, then the FASTA, the
        /// modifications and the search settings of the tuning runs' search. The defaults (the
        /// chosen model, the document's precursors) finish from the first page.
        /// </summary>
        private Pages? GetNextPage(Pages page)
        {
            switch (page)
            {
                case Pages.properties:
                    if (radioFilesSource.Checked)
                        return Pages.files;
                    if (!PredictForDocument)
                        return Pages.predict_for;
                    return IsTuning ? Pages.library_model : null;
                case Pages.predict_for:
                    return IsTuning ? Pages.library_model : null;
                case Pages.library_model:
                    return Pages.fasta;
                case Pages.fasta:
                    return Pages.modifications;
                case Pages.modifications:
                    return Pages.search_settings;
                default:
                    return null;
            }
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            OkWizardPage();
        }

        /// <summary>
        /// Next, or Finish from the last page of the route: the current page is checked, and
        /// Finish makes the builder and closes the form.
        /// </summary>
        public void OkWizardPage()
        {
            var page = CurrentPage;
            var next = GetNextPage(page);
            // Finish from the first page checks it as the builder's own validation, in full, below
            if ((next != null || page != Pages.properties) && !ValidatePage(page))
                return;
            if (next == null)
            {
                if (ValidateBuilder(true))
                {
                    Settings.Default.LibraryFilterDocumentPeptides = LibraryFilterPeptides;
                    Settings.Default.LibraryKeepRedundant = LibraryKeepRedundant;
                    DialogResult = DialogResult.OK;
                }
                return;
            }
            if (page == Pages.properties)
                Settings.Default.LibraryDirectory = Path.GetDirectoryName(LibraryPath);
            _pageHistory.Push(page);
            ShowPage(next.Value);
        }

        private bool ValidatePage(Pages page)
        {
            switch (page)
            {
                case Pages.properties:
                    return ValidateBuilder(false);
                case Pages.predict_for:
                    return ValidatePredictForPage();
                case Pages.library_model:
                    return LibraryModelControl.ValidatePage();
                case Pages.fasta:
                    return FastaControl.ValidatePage();
                case Pages.modifications:
                    return ModificationsControl.ValidatePage();
                case Pages.search_settings:
                    return SearchSettingsControl.ValidatePage();
                default:
                    return true;
            }
        }

        private void ShowPage(Pages page)
        {
            tabControlMain.SelectedIndex = (int)page;
            btnPrevious.Enabled = _pageHistory.Count > 0;
            switch (page)
            {
                case Pages.files:
                    btnNext.Enabled = Grid.IsReady;
                    break;
                default:
                    btnNext.Enabled = true;
                    break;
            }
            UpdateNextButton();
        }

        /// <summary>Next reads Finish on the last page of the route, which the page's own choices can change.</summary>
        private void UpdateNextButton()
        {
            string nextText = GetNextPage(CurrentPage) == null
                ? Resources.BuildLibraryDlg_OkWizardPage_Finish
                : Resources.BuildLibraryDlg_btnPrevious_Click__Next__;
            if (!Equals(btnNext.Text, nextText))
                btnNext.Text = nextText;
            AcceptButton = btnNext;
        }

        private void btnPrevious_Click(object sender, EventArgs e)
        {
            PreviousPage();
        }

        /// <summary>Previous: back to the page before, with its choices as they were.</summary>
        public void PreviousPage()
        {
            if (_pageHistory.Count == 0)
                return;
            ShowPage(_pageHistory.Pop());
        }

        #endregion

        private void btnAddFile_Click(object sender, EventArgs e)
        {
            ClickAddFile();
        }

        /// <summary>
        /// Shows the "Add Input Files" Open dialog and adds whatever files are selected -- the "Add File..."
        /// button's action, exposed so a test can drive the real native dialog. OrbiPrmTutorialTest uses it to
        /// select multiple files through the native multiselect Open dialog, exercising the connector's
        /// native-dialog automation end to end.
        /// </summary>
        public void ClickAddFile()
        {
            string[] addFiles = ShowAddFile(this, Settings.Default.LibraryResultsDirectory);
            if (addFiles != null)
            {
                AddInputFiles(addFiles);
            }
        }

        public static string[] ShowAddFile(Form parent, String initialDirectory)
        {
            var wildExts = new string[RESULTS_EXTS.Length];
            for (int i = 0; i < wildExts.Length; i++)
                wildExts[i] = @"*" + RESULTS_EXTS[i];

            // Adjust the button text for small molecule UI
            var buttonText = parent is FormEx formEx ?
                formEx.GetModeUIHelper().Translate(SettingsUIResources.BuildLibraryDlg_btnAddFile_Click_Matched_Peptides) :
                SettingsUIResources.BuildLibraryDlg_btnAddFile_Click_Matched_Peptides;
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = SettingsUIResources.BuildLibraryDlg_btnAddFile_Click_Add_Input_Files;
                dlg.InitialDirectory = initialDirectory;
                dlg.CheckPathExists = true;
                dlg.SupportMultiDottedExtensions = true;
                dlg.Multiselect = true;
                dlg.DefaultExt = BiblioSpecLibSpec.EXT;
                dlg.Filter = TextUtil.FileDialogFiltersAll(
                    buttonText + string.Join(@",", wildExts) + @")|" +
                    string.Join(@";", wildExts),
                    BiblioSpecLiteSpec.FILTER_BLIB);
                if (dlg.ShowDialog(parent) == DialogResult.OK)
                {
                    Settings.Default.LibraryResultsDirectory = Path.GetDirectoryName(dlg.FileName);

                    return dlg.FileNames;
                }
                return null;
            }
        }

        private void btnAddDirectory_Click(object sender, EventArgs e)
        {
            ClickAddDirectory();
        }

        /// <summary>
        /// Shows the "Add Input Directory" folder browser and adds the library input files found under the chosen
        /// folder -- the "Add Directory..." button's action, exposed so a test can drive the real native
        /// folder-browser dialog (see LibraryBuildTest).
        /// </summary>
        public void ClickAddDirectory()
        {
            // TODO: classic Browse-For-Folder, for parity with .NET Framework; revisit to adopt the newer picker
            using (var dlg = FormUtil.CreateFolderBrowserDialog())
            {
                dlg.Description = SettingsUIResources.BuildLibraryDlg_btnAddDirectory_Click_Add_Input_Directory;
                dlg.ShowNewFolderButton = false;
                dlg.SelectedPath = Settings.Default.LibraryResultsDirectory;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.LibraryResultsDirectory = dlg.SelectedPath;

                    AddDirectory(dlg.SelectedPath);
                }
            }
        }

        public void AddDirectory(string dirPath)
        {
            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.Text = SettingsUIResources.BuildLibraryDlg_AddDirectory_Find_Input_Files;
                try
                {
                    var inputFiles = new List<string>();
                    longWaitDlg.PerformWork(this, 800, broker => FindInputFiles(dirPath, inputFiles, broker));
                    AddInputFiles(inputFiles);
                }
                catch (Exception x)
                {
                    var message = TextUtil.LineSeparate(string.Format(SettingsUIResources.BuildLibraryDlg_AddDirectory_An_error_occurred_reading_files_in_the_directory__0__,
                                                                      dirPath),
                                                        x.Message);
                    MessageDlg.ShowWithException(this, message, x);
                }
            }
        }

        private void btnAddPaths_Click(object sender, EventArgs e)
        {
            ShowAddPathsDlg();
        }

        public void ShowAddPathsDlg()
        {
            CheckDisposed();

            using (var dlg = new AddPathsDlg())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    AddInputFiles(dlg.FileNames);
                }
            }
        }

        private static void FindInputFiles(string dir, ICollection<string> inputFiles, ILongWaitBroker broker)
        {
            broker.ProgressValue = 0;
            FindInputFiles(dir, inputFiles, broker, 0, 100);
        }

        private static void FindInputFiles(string dir, ICollection<string> inputFiles,
            ILongWaitBroker broker, double start, double stop)
        {
            broker.Message = TextUtil.LineSeparate(SettingsUIResources.BuildLibraryDlg_FindInputFiles_Finding_library_input_files_in,
                                                   PathEx.ShortenPathForDisplay(dir));

            string[] fileNames = Directory.GetFiles(dir);
            Array.Sort(fileNames);
            string[] dirs = Directory.GetDirectories(dir);
            Array.Sort(dirs);

            double startSub = start;
            double increment = (stop - start) / (dirs.Length + 1);

            const string extPep = BiblioSpecLiteBuilder.EXT_PEP_XML_ONE_DOT;
            const string extIdp = BiblioSpecLiteBuilder.EXT_IDP_XML;
            bool hasIdp = fileNames.Contains(f => PathEx.HasExtension(f, extIdp));

            foreach (string fileName in fileNames)
            {
                if (IsValidInputFile(fileName))
                {
                    // If the directory has any .idpXML files, then do not add the
                    // supporting .pepXML files.
                    if (!hasIdp || !PathEx.HasExtension(fileName, extPep))
                        inputFiles.Add(fileName);
                }
                if (broker.IsCanceled)
                    return;
            }

            startSub += increment;
            broker.ProgressValue = (int) startSub;

            foreach (string dirSub in dirs)
            {
                FindInputFiles(dirSub, inputFiles, broker, startSub, startSub + increment);
                if (broker.IsCanceled)
                    return;
                startSub += increment;
                broker.ProgressValue = (int)startSub;
            }
        }

        public void AddInputFiles(IEnumerable<string> fileNames)
        {
            InputFileNames = AddInputFiles(this, InputFileNames, fileNames);
        }

        public static void CheckInputFiles(IEnumerable<string> inputFileNames, IEnumerable<string> fileNames, bool performDDASearch, out List<string> filesNew, out List<string> filesError)
        {
            filesNew = new List<string>(inputFileNames);
            filesError = new List<string>();
            foreach (var fileName in fileNames)
            {
                if (IsValidInputFile(fileName, performDDASearch))
                {
                    if (!filesNew.Contains(fileName))
                        filesNew.Add(fileName);
                }
                else
                {
                    if (!filesError.Contains(fileName))
                        filesError.Add(fileName);
                }
            }
        }

        private string[] AddInputFiles(Form parent, IEnumerable<string> inputFileNames, IEnumerable<string> fileNames)
        {
            CheckInputFiles(inputFileNames, fileNames, false, out var filesNew, out var filesError);

            if (filesError.Count > 0)
            {
                var filesLib = filesError.Where(IsLibraryFile).ToArray();
                if (filesError.Count == filesLib.Length)
                {
                    // All files are library files (e.g. msp, sptxt, etc)
                    if (filesLib.Length == 1)
                    {
                        using (var dlg = new MultiButtonMsgDlg(
                            string.Format(SettingsUIResources.BuildLibraryDlg_AddInputFiles_The_file__0__is_a_library_file_and_does_not_need_to_be_built__Would_you_like_to_add_this_library_to_the_document_,
                                filesLib[0]), MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false))
                        {
                            if (dlg.ShowDialog(parent) == DialogResult.Yes)
                            {
                                AddLibraryFile = filesLib[0];
                                DialogResult = DialogResult.OK;
                            }
                        }
                    }
                    else
                    {
                        MessageDlg.Show(parent, SettingsUIResources.BuildLibraryDlg_AddInputFiles_These_files_are_library_files_and_do_not_need_to_be_built__Edit_the_list_of_libraries_to_add_them_directly_);
                    }
                }
                else if (filesError.Count == 1)
                {
                    MessageDlg.Show(parent, string.Format(SettingsUIResources.BuildLibraryDlg_AddInputFiles_The_file__0__is_not_a_valid_library_input_file, filesError[0]));
                }
                else
                {
                    var message = TextUtil.SpaceSeparate(SettingsUIResources.BuildLibraryDlg_AddInputFiles_The_following_files_are_not_valid_library_input_files,
                                  string.Empty,
                                  // ReSharper disable LocalizableElement
                                  "\t" + string.Join("\n\t", filesError.ToArray()));
                                  // ReSharper restore LocalizableElement
                    MessageDlg.Show(parent, message);
                }
            }

            return filesNew.ToArray();
        }

        public static string[] AddInputFiles(Form parent, IEnumerable<string> inputFileNames, IEnumerable<string> fileNames, bool performDDASearch = false)
        {
            CheckInputFiles(inputFileNames, fileNames, performDDASearch, out var filesNew, out var filesError);

            if (filesError.Count == 1)
            {
                MessageDlg.Show(parent, string.Format(SettingsUIResources.BuildLibraryDlg_AddInputFiles_The_file__0__is_not_a_valid_library_input_file, filesError[0]));
            }
            else if (filesError.Count > 1)
            {
                var message = TextUtil.SpaceSeparate(SettingsUIResources.BuildLibraryDlg_AddInputFiles_The_following_files_are_not_valid_library_input_files,
                              string.Empty,
                              // ReSharper disable LocalizableElement
                              "\t" + string.Join("\n\t", filesError.ToArray()));
                              // ReSharper restore LocalizableElement
                MessageDlg.Show(parent, message);
            }

            return filesNew.ToArray();
        }

        private static bool IsValidInputFile(string fileName, bool performDDASearch = false)
        {
            if (performDDASearch)
                return true; // these are validated in OpenFileDialog
            else
            {
                foreach (string extResult in RESULTS_EXTS)
                {
                    if (PathEx.HasExtension(fileName, extResult))
                        return true;
                }
            }
            return fileName.EndsWith(BiblioSpecLiteSpec.EXT);
        }

        private static bool IsLibraryFile(string fileName)
        {
            return LibrarySpec.CreateFromPath(@"__internal__", fileName) != null;
        }

        private void textPath_TextChanged(object sender, EventArgs e)
        {
            bool existsRedundant = false;
            string path = textPath.Text;
            if (!string.IsNullOrEmpty(path) && path.EndsWith(BiblioSpecLiteSpec.EXT))
            {
                try
                {
                    string baseName = Path.GetFileNameWithoutExtension(textPath.Text);
                    string redundantName = baseName + BiblioSpecLiteSpec.EXT_REDUNDANT;
                    existsRedundant = File.Exists(Path.Combine(Path.GetDirectoryName(textPath.Text) ?? string.Empty, redundantName));
                }
                catch (IOException)
                {
                    // May happen if path is too long.
                }
            }

            if (existsRedundant)
            {
                if (!comboAction.Enabled)
                    comboAction.Enabled = true;
            }
            else
            {
                if (comboAction.Enabled)
                {
                    comboAction.SelectedItem = LibraryBuildAction.Create.GetLocalizedString();
                    comboAction.Enabled = false;
                }
            }
        }

        public string LibraryName
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        public string LibraryPath
        {
            get { return textPath.Text; }
            set { textPath.Text = value; }
        }

        public void LibraryPathSelectEnd()
        {
            textPath.Select(textPath.Text.Length, 0);
        }

        public bool Koina
        {
            get { return radioKoinaSource.Checked; }
            set { radioKoinaSource.Checked = value; }
        }

        public bool Carafe
        {
            get { return radioAlphaSource.Checked; }
            set { radioAlphaSource.Checked = value; }
        }

        public int NCE
        {
            get { return (int)ceCombo.SelectedItem; }
            set { ceCombo.SelectedItem = value; }
        }

        public bool LibraryKeepRedundant
        {
            get { return cbKeepRedundant.Checked; }
            set { cbKeepRedundant.Checked = value; }
        }

        public bool IncludeAmbiguousMatches
        {
            get { return cbIncludeAmbiguousMatches.Checked; }
            set { cbIncludeAmbiguousMatches.Checked = value; }
        }

        public bool LibraryFilterPeptides
        {
            get { return cbFilter.Checked; }
            set { cbFilter.Checked = value; }
        }

        public LibraryBuildAction LibraryBuildAction
        {
            get
            {
                return (comboAction.SelectedIndex == 0
                            ? LibraryBuildAction.Create
                            : LibraryBuildAction.Append);
            }

            set
            {
                comboAction.SelectedIndex = (value == LibraryBuildAction.Create ? 0 : 1);
            }
        }

        public IrtStandard IrtStandard
        {
            get { return _driverStandards.SelectedItem; }
            set
            {
                var index = 0;
                if (value != null)
                {
                    for (var i = 0; i < comboStandards.Items.Count; i++)
                    {
                        if (comboStandards.Items[i].ToString().Equals(value.GetKey()))
                        {
                            index = i;
                            break;
                        }
                    }
                }
                comboStandards.SelectedIndex = index;
                _driverStandards.SelectedIndexChangedEvent(null, null);
            }
        }

        public bool? PreferEmbeddedSpectra { get; set; }

        private void comboStandards_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverStandards.SelectedIndexChangedEvent(sender, e);
        }

        private void dataSourceRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            // Only respond to the checking event, or this will happen
            // twice for every change.
            var radioSender = (RadioButton)sender;
            if (!radioSender.Checked)
                return;

            var selectedStandard = _driverStandards.SelectedItem;
            if (radioFilesSource.Checked)
            {
                tabControlDataSource.SelectedIndex = (int)DataSourcePages.files;
                if (!Settings.Default.IrtStandardList.Contains(IrtStandard.AUTO))
                {
                    Settings.Default.IrtStandardList.Insert(1, IrtStandard.AUTO);
                }
            }
            else
            {
                Settings.Default.IrtStandardList.Remove(IrtStandard.AUTO);

                if (radioAlphaSource.Checked)
                {
                    tabControlDataSource.SelectedIndex = (int)DataSourcePages.alpha;
                    // Only now, so Osprey is not downloaded for the other library sources. Its
                    // package lists the CUDA runtime the Use GPU box checks for; the box, the
                    // model list (each saved model's name reads its file) and the modification
                    // warning follow, for the same reason.
                    OspreyInstaller.EnsureInstalled(this);
                    _carafeModelCombo.Load();
                    UpdateUseGpu();
                    UpdateModificationWarning();
                }
                else // must be Koina
                {
                    tabControlDataSource.SelectedIndex = (int)DataSourcePages.koina;
                    KoinaUIHelpers.CheckKoinaSettings(this, _skylineWindow);
                }
            }
            _driverStandards.LoadList(selectedStandard.GetKey());
            UpdateNextButton();
        }

        private void koinaInfoSettingsBtn_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            _skylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Koina);
        }

        public IFormView ShowingFormView
        {
            get
            {
                return TAB_PAGES[tabControlMain.SelectedIndex];
            }
        }
    }
}
