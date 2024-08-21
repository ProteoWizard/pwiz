/*
 * Original author: Tahmina Jahan <tabaker .at. u.washington.edu>,
 *                  UWPR, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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

using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Database;
using pwiz.Skyline.Model.Results;
using BiblioSpecLiteLibrary = pwiz.Skyline.Model.Lib.BiblioSpecLiteLibrary;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class BuildPeptideSearchLibraryControl : UserControl
    {
        public BuildLibraryGridView Grid { get; }
        private readonly SettingsListComboDriver<IrtStandard> _driverStandards;
        private MsDataFileUri[] _ddaSearchDataSources = Array.Empty<MsDataFileUri>();
        private bool _isFeatureDetectionWorkflow;
        private bool _isRunPeptideSearch;
        private ImportPeptideSearchDlg.InputFile _inputFileType;

        public BuildPeptideSearchLibraryControl(IModifyDocumentContainer documentContainer, ImportPeptideSearch importPeptideSearch, LibraryManager libraryManager, bool isRunPeptideSearch)
        {
            DocumentContainer = documentContainer;
            ImportPeptideSearch = importPeptideSearch;
            LibraryManager = libraryManager;
            _isRunPeptideSearch = isRunPeptideSearch;

            InitializeComponent();

            Grid = gridSearchFiles;
            Grid.FilesChanged += OnGridChange;

            if (DocumentContainer.Document.PeptideCount == 0)
                cbFilterForDocumentPeptides.Hide();


            _driverStandards = new SettingsListComboDriver<IrtStandard>(comboStandards, Settings.Default.IrtStandardList);
            _driverStandards.LoadList(IrtStandard.EMPTY.GetKey());

            if (_isRunPeptideSearch)
            {
                panel1.Hide();
                radioDIA.Text = SkylineResources.BuildPeptideSearchLibraryControl_RunPeptideSearchRadioDIAText_DIA_with_DIA_Umpire;
                helpTip.SetToolTip(radioDIA, SkylineResources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibraryControl_Library_from_DIA_deconvoluted_to_single_precursor_MS_MS_spectra_and_chromatograms_from_raw_DIA_spectra_of_same_runs);
                InputFileType = ImportPeptideSearchDlg.InputFile.dda_raw;
            }
            else
            {
                InputFileType = ImportPeptideSearchDlg.InputFile.search_result;
            }
        }

        private BuildPeptideSearchLibrarySettings _buildPeptideSearchLibrarySettings;
        public BuildPeptideSearchLibrarySettings BuildLibrarySettings
        {
            get
            {
                return _buildPeptideSearchLibrarySettings ??= new BuildPeptideSearchLibrarySettings(this);
            }
        }

        public class BuildPeptideSearchLibrarySettings : AuditLogOperationSettings<BuildPeptideSearchLibrarySettings>
        {
            private SrmDocument.DOCUMENT_TYPE _docType;

            public static BuildPeptideSearchLibrarySettings DEFAULT = new BuildPeptideSearchLibrarySettings(null, null, false,
                false, ImportPeptideSearchDlg.Workflow.dda, ImportPeptideSearchDlg.InputFile.search_result, SrmDocument.DOCUMENT_TYPE.proteomic);

            public override MessageInfo MessageInfo
            {
                get
                {
                    return new MessageInfo(MessageType.added_spectral_library, _docType,
                       string.IsNullOrEmpty(LibraryName) ? Settings.Default.SpectralLibraryList.First().Name : LibraryName);
                }
            }

            public BuildPeptideSearchLibrarySettings(BuildPeptideSearchLibraryControl control) : this(
                control.Grid.Files, control.IrtStandards, control.IncludeAmbiguousMatches,
                control.FilterForDocumentPeptides, control.WorkflowType, control.InputFileType, control.ModeUI)
            {
            }

            public BuildPeptideSearchLibrarySettings(IEnumerable<BuildLibraryGridView.File> files, IrtStandard standard,
                bool includeAmbiguousMatches, bool filterForDocumentPeptides,
                ImportPeptideSearchDlg.Workflow workFlow, ImportPeptideSearchDlg.InputFile inputFileType, SrmDocument.DOCUMENT_TYPE docType)
            {
                SearchFileNames = files?.ToArray() ?? Array.Empty<BuildLibraryGridView.File>();
                Standard = standard;
                IncludeAmbiguousMatches = includeAmbiguousMatches;
                FilterForDocumentPeptides = filterForDocumentPeptides;
                WorkFlow = workFlow;
                InputFileType = inputFileType;
                _docType = docType;
            }

            [Track]
            public BuildLibraryGridView.File[] SearchFileNames { get; private set; }
            [Track]
            public IrtStandard Standard { get; private set; }
            [Track]
            public bool IncludeAmbiguousMatches { get; private set; }
            [Track]
            public bool FilterForDocumentPeptides { get; private set; }
            [Track(ignoreDefaultParent: true)]
            public ImportPeptideSearchDlg.Workflow WorkFlow { get; private set; }
            [Track(ignoreDefaultParent: true)]
            public ImportPeptideSearchDlg.InputFile InputFileType { get; private set; }

            public string LibraryName; // Name of the library being built

            public object GetDefaultObject(ObjectInfo<object> info)
            {
                return DEFAULT;
            }
        }

        public event EventHandler InputFilesChanged;

        private void FireInputFilesChanged()
        {
            InputFilesChanged?.Invoke(this, EventArgs.Empty);
            gridSearchFiles_SelectedIndexChanged(this, EventArgs.Empty);
        }

        private IModifyDocumentContainer DocumentContainer { get; set; }
        private LibraryManager LibraryManager { get; set; }
        public ImportPeptideSearch ImportPeptideSearch { get; set; }

        private SrmDocument.DOCUMENT_TYPE ModeUI => (WizardForm is FormEx parent) ? parent.ModeUI : SrmDocument.DOCUMENT_TYPE.none;

        private Form WizardForm => FormEx.GetParentForm(this);

        public IrtStandard IrtStandards
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
        public bool DebugMode { get; set; }

        public ImportPeptideSearchDlg.Workflow WorkflowType
        {
            get
            {
                if (_isFeatureDetectionWorkflow)
                    return ImportPeptideSearchDlg.Workflow.feature_detection;
                if (radioPRM.Checked)
                    return ImportPeptideSearchDlg.Workflow.prm;
                if (radioDIA.Checked)
                    return ImportPeptideSearchDlg.Workflow.dia;
                return ImportPeptideSearchDlg.Workflow.dda;
            }
            set
            {
                switch (value)
                {
                    case ImportPeptideSearchDlg.Workflow.prm:
                        radioPRM.Checked = true;
                        break;
                    case ImportPeptideSearchDlg.Workflow.dia:
                        radioDIA.Checked = true;
                        break;
                    case ImportPeptideSearchDlg.Workflow.feature_detection:
                        _isFeatureDetectionWorkflow = true;
                        radioDDA.Checked = true;
                        break;
                    default:
                        radioDDA.Checked = true;
                        break;
                }
            }
        }

        public ImportPeptideSearchDlg.InputFile InputFileType
        {
            get => _inputFileType;
            set
            {
                _inputFileType = value;
                UpdatePerformDDASearch();
            }
        }

        public bool FilterForDocumentPeptides
        {
            get { return cbFilterForDocumentPeptides.Checked; }
            set { cbFilterForDocumentPeptides.Checked = value; }
        }

        public string[] SearchFilenames
        {
            get => ImportPeptideSearch.SearchFilenames;
            set => Grid.FilePaths = value;
        }

        public MsDataFileUri[] DdaSearchDataSources
        {
            get => _ddaSearchDataSources;
            set => Grid.FileUris = value;
        }

        private void btnRemFile_Click(object sender, EventArgs e)
        {
            Grid.Remove(Grid.SelectedFiles);
        }

        private void gridSearchFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnRemFile.Enabled = Grid.SelectedFiles.Any();
        }

        private void btnAddFile_Click(object sender, EventArgs e)
        {
            if (PerformDDASearch)
            {
                MsDataFileUri[] dataSources;
                using (var dlg = new OpenDataSourceDialog(Settings.Default.RemoteAccountList))
                {
                    dlg.Text = _isFeatureDetectionWorkflow ?
                        PeptideSearchResources.BuildPeptideSearchLibraryControl_btnAddFile_Click_Select_Files_to_Search :
                        PeptideSearchResources.ImportResultsControl_browseToResultsFileButton_Click_Import_Peptide_Search;
                    dlg.InitialDirectory = new MsDataFilePath(Path.GetDirectoryName(DocumentContainer.DocumentFilePath));
                    // Use saved source type, if there is one.
                    //string sourceType = Settings.Default.SrmResultsSourceType;
                    //if (!string.IsNullOrEmpty(sourceType))
                    //    dlg.SourceTypeName = sourceType;

                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    dataSources = dlg.DataSources;
                }

                if (dataSources == null || dataSources.Length == 0)
                {
                    MessageDlg.Show(this, Resources.ImportResultsDlg_GetDataSourcePathsFile_No_results_files_chosen);
                    return;
                }

                DdaSearchDataSources = DdaSearchDataSources.Union(dataSources).ToArray();
            }
            else
            {
                string[] addFiles = BuildLibraryDlg.ShowAddFile(WizardForm, Path.GetDirectoryName(DocumentContainer.DocumentFilePath));
                if (addFiles != null)
                {
                    AddSearchFiles(addFiles);
                }
            }
        }

        public void AddSearchFiles(IEnumerable<string> fileNames)
        {
            SearchFilenames = BuildLibraryDlg.AddInputFiles(WizardForm, SearchFilenames, fileNames, PerformDDASearch);
        }

        public bool NeedsCutoffScore => _isRunPeptideSearch; // Only needed if Skyline is conducting the search

        public bool IncludeAmbiguousMatches
        {
            get { return cbIncludeAmbiguousMatches.Checked; }
            set { cbIncludeAmbiguousMatches.Checked = value; }
        }

        public bool BuildOrUsePeptideSearchLibrary(CancelEventArgs e, bool showWarnings, bool isFeatureDetection)
        {
            return UseExistingLibrary ? AddExistingLibrary(e) : BuildPeptideSearchLibrary(e, showWarnings, isFeatureDetection);
        }

        public string LastBuildCommandArgs { get; private set; }
        public string LastBuildOutput { get; private set; }

        private bool BuildPeptideSearchLibrary(CancelEventArgs e, bool showWarnings, bool isFeatureDetection)
        {
            // Nothing to build, if no search files were specified
            if (!SearchFilenames.Any())
            {
                var libraries = DocumentContainer.Document.Settings.PeptideSettings.Libraries;
                if (!libraries.HasLibraries)
                    return false;
                var libSpec = libraries.LibrarySpecs.FirstOrDefault(s => s.IsDocumentLibrary);
                return libSpec != null && LoadPeptideSearchLibrary(libSpec);
            }

            if (!Grid.Validate(WizardForm, e, showWarnings, out var thresholdsByFile))
                return false;

            BiblioSpecLiteBuilder builder;
            try
            {
                builder = ImportPeptideSearch.GetLibBuilder(DocumentContainer.Document, DocumentContainer.DocumentFilePath, cbIncludeAmbiguousMatches.Checked, isFeatureDetection);
                builder.ScoreThresholdsByFile = thresholdsByFile;
                builder.PreferEmbeddedSpectra = PreferEmbeddedSpectra;
                builder.DebugMode = DebugMode;
            }
            catch (FileEx.DeleteException de)
            {
                MessageDlg.ShowException(this, de);
                return false;
            }

            bool retry = false;
            do
            {
                using (var longWaitDlg = new LongWaitDlg())
                {
                    longWaitDlg.Text = isFeatureDetection ?
                        PeptideSearchResources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Building_Detected_Features_Library :
                        PeptideSearchResources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Building_Peptide_Search_Library;
                    longWaitDlg.Message = isFeatureDetection ?
                        PeptideSearchResources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Building_Detected_Features_Library :
                        PeptideSearchResources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Building_document_library_for_peptide_search_;
                    // Disable the wizard, because the LongWaitDlg does not
                    try
                    {
                        ImportPeptideSearch.ClosePeptideSearchLibraryStreams(DocumentContainer.Document);
                        var buildState = new LibraryManager.BuildState(null, null);
                        var status = longWaitDlg.PerformWork(WizardForm, 800,
                            monitor => LibraryManager.BuildLibraryBackground(DocumentContainer, builder, monitor, buildState));
                        LastBuildCommandArgs = buildState.BuildCommandArgs;
                        LastBuildOutput = buildState.BuildOutput;
                        if (status.IsError)
                        {
                            // E.g. could not find external raw data for MaxQuant msms.txt; ask user if they want to retry with "prefer embedded spectra" option
                            if (BiblioSpecLiteBuilder.IsLibraryMissingExternalSpectraError(status.ErrorException))
                            {
                                var response = ShowLibraryMissingExternalSpectraError(WizardForm, status.ErrorException);
                                if (response == UpdateProgressResponse.cancel)
                                    return false;
                                else if (response == UpdateProgressResponse.normal)
                                    builder.PreferEmbeddedSpectra = true;

                                retry = true;
                            }
                            else
                            {
                                MessageDlg.ShowException(WizardForm, status.ErrorException);
                                return false;
                            }
                        }
                    }
                    catch (Exception x)
                    {
                        MessageDlg.ShowWithException(WizardForm, TextUtil.LineSeparate(string.Format(PeptideSearchResources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Failed_to_build_the_library__0__,
                            Path.GetFileName(BiblioSpecLiteSpec.GetLibraryFileName(DocumentContainer.DocumentFilePath))), x.Message), x);
                        return false;
                    }
                }
            } while (retry) ;

            var docLibSpec = builder.LibrarySpec;
            BuildLibrarySettings.LibraryName = docLibSpec.Name;
            if (isFeatureDetection)
            {
                Settings.Default.SpectralLibraryList.Add(docLibSpec); // Won't displace the current document library
            }
            else
            {
                docLibSpec = builder.LibrarySpec.ChangeDocumentLibrary(true);
                Settings.Default.SpectralLibraryList.Insert(0, docLibSpec);
            }

            // Go ahead and load the library - we'll need it for 
            // the modifications and chromatograms page.
            if (!LoadPeptideSearchLibrary(docLibSpec))
                return false;

            IrtStandard outStandard = null;
            var addedIrts = !isFeatureDetection && LibraryBuildNotificationHandler.AddIrts(IrtRegressionType.DEFAULT,
                ImportPeptideSearch.DocLib, docLibSpec, _driverStandards.SelectedItem, WizardForm, false, out outStandard);

            var docNew = ImportPeptideSearch.AddDocumentSpectralLibrary(DocumentContainer.Document, docLibSpec);
            if (docNew == null)
                return false;

            if (addedIrts)
                docNew = ImportPeptideSearch.AddRetentionTimePredictor(docNew, docLibSpec);

            DocumentContainer.ModifyDocumentNoUndo(doc => docNew);

            if (!string.IsNullOrEmpty(builder.AmbiguousMatchesMessage))
            {
                MessageDlg.Show(WizardForm, builder.AmbiguousMatchesMessage);
            }
            if (!isFeatureDetection)
            {
                ImportPeptideSearch.IrtStandard = outStandard;
            }
            return true;
        }

        private bool AddExistingLibrary(CancelEventArgs e)
        {
            string libraryPath = ValidateLibraryPath();
            if (libraryPath == null)
            {
                e.Cancel = true;
                return false;
            }

            var peptideLibraries = DocumentContainer.Document.Settings.PeptideSettings.Libraries;
            var docLibSpec = peptideLibraries.LibrarySpecs.FirstOrDefault(spec => spec.FilePath == libraryPath);
            if (docLibSpec == null)
            {
                docLibSpec =
                    Settings.Default.SpectralLibraryList.FirstOrDefault(spec => spec.FilePath == libraryPath);
                if (docLibSpec == null)
                {
                    var existingNames = new HashSet<string>();
                    existingNames.UnionWith(Settings.Default.SpectralLibraryList.Select(spec => spec.Name));
                    existingNames.UnionWith(peptideLibraries.LibrarySpecs.Select(spec => spec.Name));
                    string libraryName =
                        Helpers.GetUniqueName(Path.GetFileNameWithoutExtension(libraryPath), existingNames);
                    docLibSpec = LibrarySpec.CreateFromPath(libraryName, libraryPath);
                    if (docLibSpec == null)
                    {
                        MessageDlg.Show(WizardForm, string.Format(Resources.EditLibraryDlg_OkDialog_The_file__0__is_not_a_supported_spectral_library_file_format, libraryPath));
                        return false;
                    }
                    Settings.Default.SpectralLibraryList.SetValue(docLibSpec);
                }
            }
            if (!LoadPeptideSearchLibrary(docLibSpec))
            {
                return false;
            }

            var docNew = ImportPeptideSearch.AddDocumentSpectralLibrary(DocumentContainer.Document, docLibSpec);
            if (docNew == null)
                return false;
            if (docNew.Settings.PeptideSettings.Libraries.LibrarySpecs.Count ==
                DocumentContainer.Document.Settings.PeptideSettings.Libraries.LibrarySpecs.Count)
                return false;

            var blib = ImportPeptideSearch.DocLib as BiblioSpecLiteLibrary;
            if (blib?.ReadStream is ConnectionId<SQLiteConnection> connection && SqliteOperations.TableExists(connection.Connection, @"IrtLibrary"))
            {
                using (var dlg = new MultiButtonMsgDlg(
                           PeptideSearchResources.BuildPeptideSearchLibraryControl_AddExistingLibrary_This_library_contains_iRT_values__Do_you_want_to_create_a_retention_time_predictor_with_these_values_,
                           MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false))
                {
                    if (dlg.ShowDialog(WizardForm) == DialogResult.Yes)
                    {
                        docNew = ImportPeptideSearch.AddRetentionTimePredictor(docNew, docLibSpec);
                    }
                }
            }

            DocumentContainer.ModifyDocumentNoUndo(doc => docNew);
            return true;
        }


        /// <summary>
        /// Shows a dialog prompting user to decide whether to use embedded spectra when external spectra are preferred but cannot be found.
        /// Returns 'normal' if the user wants Embedded spectra, 'option1' to retry finding the external spectra, or to 'cancel' to abort the library build.
        /// </summary>
        public static UpdateProgressResponse ShowLibraryMissingExternalSpectraError(Control parentWindow, Exception errorException)
        {
            // E.g. could not find external raw data for MaxQuant msms.txt; ask user if they want to retry with "prefer embedded spectra" option
            if (!BiblioSpecLiteBuilder.IsLibraryMissingExternalSpectraError(errorException, out IList<string> spectrumFilenames, out IList<string> directoriesSearched, out string resultsFilepath))
                throw new InvalidOperationException(@"IsLibraryMissingExternalSpectraError returned false");

            string extraHelp = PeptideSearchResources.VendorIssueHelper_ShowLibraryMissingExternalSpectraError_ButtonDescriptions;

            string messageFormat = spectrumFilenames.Count > 1
                ? Resources.VendorIssueHelper_ShowLibraryMissingExternalSpectrumFilesError
                : Resources.VendorIssueHelper_ShowLibraryMissingExternalSpectrumFileError;

            // TODO: parse supported file extensions from BiblioSpec or ProteoWizard
            var dialogResult = MultiButtonMsgDlg.Show(parentWindow,
                string.Format(messageFormat,
                    resultsFilepath, string.Join(Environment.NewLine, spectrumFilenames),
                    string.Join(Environment.NewLine, directoriesSearched),
                    BiblioSpecLiteBuilder.BiblioSpecSupportedFileExtensions) + extraHelp,
                PeptideSearchResources.BiblioSpecLiteBuilder_Embedded,
                PeptideSearchResources.AlertDlg_GetDefaultButtonText__Retry, true);

            switch (dialogResult)
            {
                case DialogResult.Cancel: return UpdateProgressResponse.cancel;
                case DialogResult.Yes: return UpdateProgressResponse.normal;
                case DialogResult.No: return UpdateProgressResponse.option1;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool LoadPeptideSearchLibrary(LibrarySpec docLibSpec)
        {
            if (docLibSpec == null)
                return false;

            using (var longWait = new LongWaitDlg())
            {
                longWait.Text = PeptideSearchResources.BuildPeptideSearchLibraryControl_LoadPeptideSearchLibrary_Loading_Library;
                try
                {
                    var status = longWait.PerformWork(WizardForm, 800, monitor => ImportPeptideSearch.LoadPeptideSearchLibrary(LibraryManager, docLibSpec, monitor));
                    if (status.IsError)
                    {
                        MessageDlg.ShowException(WizardForm, status.ErrorException);
                    }
                }
                catch (Exception x)
                {
                    MessageDlg.ShowWithException(WizardForm,
                        TextUtil.LineSeparate(string.Format(PeptideSearchResources.BuildPeptideSearchLibraryControl_LoadPeptideSearchLibrary_An_error_occurred_attempting_to_import_the__0__library_,
                            docLibSpec.Name), x.Message), x);
                }
            }
            return ImportPeptideSearch.HasDocLib;
        }

        public void ForceWorkflow(ImportPeptideSearchDlg.Workflow workflowType)
        {
            WorkflowType = workflowType;
            grpWorkflow.Hide();
            if (WorkflowType == ImportPeptideSearchDlg.Workflow.feature_detection)
            {
                // Initialize then hide various controls that Hardklor doesn't need
                panel1.Hide();
                radioButtonNewLibrary.Checked = true;
                radioButtonNewLibrary.Hide();
                radioExistingLibrary.Hide();
                InputFileType = ImportPeptideSearchDlg.InputFile.dda_raw;
                lblStandardPeptides.Hide();
                comboStandards.Hide();
                cbIncludeAmbiguousMatches.Hide();
                cbFilterForDocumentPeptides.Hide();

                panelPeptideSearch.Top = panel1.Top;
                panelChooseFile.Top = 12;
                panelPeptideSearch.Height = gridSearchFiles.Bottom;
                Height = panelPeptideSearch.Height;
            }
        }

        private void radioButtonLibrary_CheckedChanged(object sender, EventArgs e)
        {
            UpdateUseExistingLibrary();
        }

        public void UpdateUseExistingLibrary()
        {
            panelChooseFile.Visible = UseExistingLibrary;
            panelPeptideSearch.Visible = !UseExistingLibrary;
            FireInputFilesChanged();
        }

        public bool UseExistingLibrary
        {
            get { return radioExistingLibrary.Checked; }
            set
            {
                radioExistingLibrary.Checked = value;
                radioButtonNewLibrary.Checked = !value;
            }
        }

        public bool IsReady => UseExistingLibrary ? !string.IsNullOrEmpty(tbxLibraryPath.Text) : Grid.IsReady;

        public string ExistingLibraryPath
        {
            get { return tbxLibraryPath.Text; }
            set { tbxLibraryPath.Text = value; }
        }

        public string ValidateLibraryPath()
        {
            if (!EditLibraryDlg.ValidateLibraryPath(this, ExistingLibraryPath))
            {
                tbxLibraryPath.Focus();
                return null;
            }

            return ExistingLibraryPath;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            string newPath = EditLibraryDlg.GetLibraryPath(this, ExistingLibraryPath);
            if (newPath != null)
            {
                tbxLibraryPath.Text = newPath;
            }
        }

        private void tbxLibraryPath_TextChanged(object sender, EventArgs e)
        {
            FireInputFilesChanged();
        }

        public void UpdatePerformDDASearch()
        {
            //panelChooseFile.Visible = !PerformDDASearch;
            lblFileCaption.Text = PerformDDASearch
                ? PeptideSearchResources.BuildPeptideSearchLibraryControl_Files_to_search_
                : PeptideSearchResources.BuildPeptideSearchLibraryControl_Result_files_;
            //peptideSearchSplitContainer.Visible = PerformDDASearch;

            Grid.FilesChanged -= OnGridChange;
            if (PerformDDASearch)
            {
                Grid.IsFileOnly = true;
                Grid.FileUris = _ddaSearchDataSources;
            }
            else
            {
                Grid.IsFileOnly = false;
                Grid.FilePaths = ImportPeptideSearch.SearchFilenames;
            }
            Grid.FilesChanged += OnGridChange;
        }

        public bool PerformDDASearch
        {
            get { return InputFileType != ImportPeptideSearchDlg.InputFile.search_result; }
        }

        public bool DIAConversionNeeded => InputFileType == ImportPeptideSearchDlg.InputFile.dia_raw;

        private void OnGridChange(object sender, EventArgs e)
        {
            if (!PerformDDASearch)
                ImportPeptideSearch.SearchFilenames = Grid.FilePaths.ToArray();
            else
                _ddaSearchDataSources = Grid.FileUris.ToArray();
            FireInputFilesChanged();
        }

        private void comboStandards_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverStandards.SelectedIndexChangedEvent(sender, e);
        }

        private void radioDDA_CheckedChanged(object sender, EventArgs e)
        {
            if (radioDDA.Checked && _isRunPeptideSearch)
            {
                InputFileType = ImportPeptideSearchDlg.InputFile.dda_raw;
            }
        }

        private void radioDIA_CheckedChanged(object sender, EventArgs e)
        {
            if (radioDIA.Checked && _isRunPeptideSearch)
            {
                InputFileType = ImportPeptideSearchDlg.InputFile.dia_raw;
            }
        }

        private void radioPRM_CheckedChanged(object sender, EventArgs e)
        {
            if (radioPRM.Checked && _isRunPeptideSearch)
            {
                InputFileType = ImportPeptideSearchDlg.InputFile.dda_raw;
            }
        }
    }
}
