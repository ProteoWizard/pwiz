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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class BuildPeptideSearchLibraryControl : UserControl
    {
        private readonly SettingsListComboDriver<IrtStandard> _driverStandards;
        private MsDataFileUri[] _ddaSearchDataSources = new MsDataFileUri[0];

        public BuildPeptideSearchLibraryControl(IModifyDocumentContainer documentContainer, ImportPeptideSearch importPeptideSearch, LibraryManager libraryManager)
        {
            DocumentContainer = documentContainer;
            ImportPeptideSearch = importPeptideSearch;
            LibraryManager = libraryManager;

            InitializeComponent();

            textCutoff.Text = ImportPeptideSearch.CutoffScore.ToString(LocalizationHelper.CurrentCulture);

            if (DocumentContainer.Document.PeptideCount == 0)
                cbFilterForDocumentPeptides.Hide();

            comboInputFileType.Items.AddRange(new[]
            {
                EnumNames.InputFile_search_result,
                EnumNames.InputFile_dda_raw,
                EnumNames.InputFile_dia_raw
            });

            _driverStandards = new SettingsListComboDriver<IrtStandard>(comboStandards, Settings.Default.IrtStandardList);
            _driverStandards.LoadList(IrtStandard.EMPTY.GetKey());

            comboInputFileType.SelectedIndex = 0;
        }

        public BuildPeptideSearchLibrarySettings BuildLibrarySettings
        {
            get { return new BuildPeptideSearchLibrarySettings(this); }
        }

        public class BuildPeptideSearchLibrarySettings : AuditLogOperationSettings<BuildPeptideSearchLibrarySettings>
        {
            private SrmDocument.DOCUMENT_TYPE _docType;

            public static BuildPeptideSearchLibrarySettings DEFAULT = new BuildPeptideSearchLibrarySettings(0.0, new List<string>(), null, false,
                false, ImportPeptideSearchDlg.Workflow.dda, ImportPeptideSearchDlg.InputFile.search_result, SrmDocument.DOCUMENT_TYPE.proteomic);

            public override MessageInfo MessageInfo
            {
                get
                {
                    return new MessageInfo(MessageType.added_spectral_library, _docType,
                        Settings.Default.SpectralLibraryList.First().Name);
                }
            }


            public BuildPeptideSearchLibrarySettings(BuildPeptideSearchLibraryControl control) : this(control.CutOffScore,
                control.SearchFilenames, control.IrtStandards, control.IncludeAmbiguousMatches,
                control.FilterForDocumentPeptides, control.WorkflowType, control.InputFileType, control.ModeUI)
            {
            }

            public BuildPeptideSearchLibrarySettings(double cutoffScore, IList<string> searchFileNames, IrtStandard standard, bool includeAmbiguousMatches, bool filterForDocumentPeptides,
                ImportPeptideSearchDlg.Workflow workFlow, ImportPeptideSearchDlg.InputFile inputFileType, SrmDocument.DOCUMENT_TYPE docType)
            {
                CutoffScore = cutoffScore;
                SearchFileNames = searchFileNames == null
                    ? new List<AuditLogPath>()
                    : searchFileNames.Select(AuditLogPath.Create).ToList();
                Standard = standard;
                IncludeAmbiguousMatches = includeAmbiguousMatches;
                FilterForDocumentPeptides = filterForDocumentPeptides;
                WorkFlow = workFlow;
                InputFileType = inputFileType;
                _docType = docType;
            }

            [Track(ignoreDefaultParent: true)]
            public double CutoffScore { get; private set; }
            [Track]
            public List<AuditLogPath> SearchFileNames { get; private set; }
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

            public object GetDefaultObject(ObjectInfo<object> info)
            {
                return DEFAULT;
            }
        }

        public event EventHandler InputFilesChanged;

        private void FireInputFilesChanged()
        {
            InputFilesChanged?.Invoke(this, new EventArgs());
        }

        private IModifyDocumentContainer DocumentContainer { get; set; }
        private LibraryManager LibraryManager { get; set; }
        public ImportPeptideSearch ImportPeptideSearch { get; set; }

        private SrmDocument.DOCUMENT_TYPE ModeUI => (WizardForm is FormEx parent) ? parent.ModeUI : SrmDocument.DOCUMENT_TYPE.none;

        private Form WizardForm
        {
            get { return FormEx.GetParentForm(this); }
        }

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
                    default:
                        radioDDA.Checked = true;
                        break;
                }
            }
        }

        public ImportPeptideSearchDlg.InputFile InputFileType
        {
            get { return (ImportPeptideSearchDlg.InputFile) comboInputFileType.SelectedIndex; }
            set { comboInputFileType.SelectedIndex = (int) value; }
        }

        public bool FilterForDocumentPeptides
        {
            get { return cbFilterForDocumentPeptides.Checked; }
            set { cbFilterForDocumentPeptides.Checked = value; }
        }

        public string[] SearchFilenames
        {
            get { return ImportPeptideSearch.SearchFilenames; }

            private set
            {
                // Set new value
                ImportPeptideSearch.SearchFilenames = value;

                // Always show sorted list of files
                Array.Sort(ImportPeptideSearch.SearchFilenames);

                // Calculate the common root directory
                string dirInputRoot = PathEx.GetCommonRoot(ImportPeptideSearch.SearchFilenames);

                // Populate the input files list
                listSearchFiles.BeginUpdate();
                listSearchFiles.Items.Clear();
                foreach (string fileName in ImportPeptideSearch.SearchFilenames)
                {
                    listSearchFiles.Items.Add(PathEx.RemovePrefix(fileName, dirInputRoot));
                }
                listSearchFiles.EndUpdate();

                FireInputFilesChanged();
            }
        }

        public MsDataFileUri[] DdaSearchDataSources
        {
            get => _ddaSearchDataSources;
            set
            {
                // Set new value
                _ddaSearchDataSources = value;

                // Always show sorted list of files
                Array.Sort(_ddaSearchDataSources);

                // Calculate the common root directory
                string dirInputRoot = PathEx.GetCommonRoot(_ddaSearchDataSources.Select(o => o.GetFilePath()));

                // Populate the input files list
                listSearchFiles.BeginUpdate();
                listSearchFiles.Items.Clear();
                foreach (var uri in _ddaSearchDataSources)
                {
                    string fileAndSampleLocator = uri.GetFilePath();
                    if (uri.GetSampleIndex() > 0)
                        fileAndSampleLocator += $@":{uri.GetSampleIndex()}";
                    listSearchFiles.Items.Add(PathEx.RemovePrefix(fileAndSampleLocator, dirInputRoot));
                }

                listSearchFiles.EndUpdate();

                FireInputFilesChanged();
            }
        }

        private void btnRemFile_Click(object sender, EventArgs e)
        {
            RemoveFiles();
        }

        public void RemoveFiles()
        {
            IList listSearchFilenames = PerformDDASearch ? (IList) _ddaSearchDataSources.ToList() : SearchFilenames.ToList();
            var selectedIndices = listSearchFiles.SelectedIndices;
            for (int i = selectedIndices.Count - 1; i >= 0; i--)
            {
                listSearchFilenames.RemoveAt(selectedIndices[i]);
                listSearchFiles.Items.RemoveAt(selectedIndices[i]); // this changes SelectedIndices
            }

            if (PerformDDASearch)
                _ddaSearchDataSources = listSearchFilenames.Cast<MsDataFileUri>().ToArray();
            else
                SearchFilenames = listSearchFilenames.Cast<string>().ToArray();

            FireInputFilesChanged();
        }

        private void listSearchFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnRemFile.Enabled = listSearchFiles.SelectedItems.Count > 0;
        }

        private void btnAddFile_Click(object sender, EventArgs e)
        {
            if (PerformDDASearch)
            {
                MsDataFileUri[] dataSources;
                using (var dlg = new OpenDataSourceDialog(Settings.Default.RemoteAccountList)
                {
                    Text = Resources.ImportResultsControl_browseToResultsFileButton_Click_Import_Peptide_Search,
                    InitialDirectory = new MsDataFilePath(Path.GetDirectoryName(DocumentContainer.DocumentFilePath)),
                })
                {
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

        public double CutOffScore
        {
            get { return double.Parse(textCutoff.Text); }
            set { textCutoff.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public bool IncludeAmbiguousMatches
        {
            get { return cbIncludeAmbiguousMatches.Checked; }
            set { cbIncludeAmbiguousMatches.Checked = value; }
        }

        public bool BuildOrUsePeptideSearchLibrary(CancelEventArgs e)
        {
            if (UseExistingLibrary)
            {
                return AddExistingLibrary(e);
            }
            else 
            {
                return BuildPeptideSearchLibrary(e);
            }
        }

        public string LastBuildCommandArgs { get; private set; }
        public string LastBuildOutput { get; private set; }

        private bool BuildPeptideSearchLibrary(CancelEventArgs e)
        {
            // Nothing to build, if now search files were specified
            if (!SearchFilenames.Any())
            {
                var libraries = DocumentContainer.Document.Settings.PeptideSettings.Libraries;
                if (!libraries.HasLibraries)
                    return false;
                var libSpec = libraries.LibrarySpecs.FirstOrDefault(s => s.IsDocumentLibrary);
                return libSpec != null && LoadPeptideSearchLibrary(libSpec);
            }

            double cutOffScore;
            MessageBoxHelper helper = new MessageBoxHelper(WizardForm);
            if (!helper.ValidateDecimalTextBox(textCutoff, 0, 1.0, out cutOffScore))
            {
                e.Cancel = true;
                return false;
            }
            ImportPeptideSearch.CutoffScore = cutOffScore;

            BiblioSpecLiteBuilder builder;
            try
            {
                builder = ImportPeptideSearch.GetLibBuilder(DocumentContainer.Document, DocumentContainer.DocumentFilePath, cbIncludeAmbiguousMatches.Checked);
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
                using (var longWaitDlg = new LongWaitDlg
                {
                    Text = Resources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Building_Peptide_Search_Library,
                    Message = Resources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Building_document_library_for_peptide_search_,
                })
                {
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
                        MessageDlg.ShowWithException(WizardForm, TextUtil.LineSeparate(string.Format(Resources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Failed_to_build_the_library__0__,
                            Path.GetFileName(BiblioSpecLiteSpec.GetLibraryFileName(DocumentContainer.DocumentFilePath))), x.Message), x);
                        return false;
                    }
                }
            } while (retry) ;

            var docLibSpec = builder.LibrarySpec.ChangeDocumentLibrary(true);
            Settings.Default.SpectralLibraryList.Insert(0, docLibSpec);

            // Go ahead and load the library - we'll need it for 
            // the modifications and chromatograms page.
            if (!LoadPeptideSearchLibrary(docLibSpec))
                return false;

            var addedIrts = LibraryBuildNotificationHandler.AddIrts(IrtRegressionType.DEFAULT, ImportPeptideSearch.DocLib, docLibSpec, _driverStandards.SelectedItem, WizardForm, false);

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
            ImportPeptideSearch.IrtStandard = _driverStandards.SelectedItem;
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
            DocumentContainer.ModifyDocumentNoUndo(doc => ImportPeptideSearch.AddDocumentSpectralLibrary(doc, docLibSpec));
            return true;
        }


        /// <summary>
        /// Shows a dialog prompting user to decide whether to use embedded spectra when external spectra are preferred but cannot be found.
        /// Returns 'normal' if the user wants Embedded spectra, 'option1' to retry finding the external spectra, or to 'cancel' to abort the library build.
        /// </summary>
        public static UpdateProgressResponse ShowLibraryMissingExternalSpectraError(Control parentWindow, Exception errorException)
        {
            // E.g. could not find external raw data for MaxQuant msms.txt; ask user if they want to retry with "prefer embedded spectra" option
            if (!BiblioSpecLiteBuilder.IsLibraryMissingExternalSpectraError(errorException, out string spectrumFilename, out string resultsFilepath))
                throw new InvalidOperationException(@"IsLibraryMissingExternalSpectraError returned false");

            // TODO: parse supported file extensions from BiblioSpec or ProteoWizard
            var dialogResult = MultiButtonMsgDlg.Show(parentWindow,
                string.Format(Resources.VendorIssueHelper_ShowLibraryMissingExternalSpectraError_Could_not_find_an_external_spectrum_file_matching__0__in_the_same_directory_as_the_MaxQuant_input_file__1__,
                    spectrumFilename, resultsFilepath) +
                string.Format(Resources.VendorIssueHelper_ShowLibraryMissingExternalSpectraError_ButtonDescriptionsSupportsExtensions__0__, BiblioSpecLiteBuilder.BiblioSpecSupportedFileExtensions),
                Resources.BiblioSpecLiteBuilder_Embedded,
                Resources.AlertDlg_GetDefaultButtonText__Retry, true);

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

            using (var longWait = new LongWaitDlg {Text = Resources.BuildPeptideSearchLibraryControl_LoadPeptideSearchLibrary_Loading_Library})
            {
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
                        TextUtil.LineSeparate(string.Format(Resources.BuildPeptideSearchLibraryControl_LoadPeptideSearchLibrary_An_error_occurred_attempting_to_import_the__0__library_,
                            docLibSpec.Name), x.Message), x);
                }
            }
            return ImportPeptideSearch.HasDocLib;
        }

        public void ForceWorkflow(ImportPeptideSearchDlg.Workflow workflowType)
        {
            WorkflowType = workflowType;
            grpWorkflow.Hide();
        }

        private void radioButtonLibrary_CheckedChanged(object sender, EventArgs e)
        {
            UpdateUseExistingLibrary();
        }

        public void UpdateUseExistingLibrary()
        {
            panelChooseFile.Visible = UseExistingLibrary;
            peptideSearchSplitContainer.Visible = !UseExistingLibrary;
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

        public bool AnyInputFiles
        {
            get
            {
                if (UseExistingLibrary)
                {
                    return !string.IsNullOrEmpty(tbxLibraryPath.Text);
                }

                return 0 != listSearchFiles.Items.Count;
            }
        }

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
                ? Resources.BuildPeptideSearchLibraryControl_Files_to_search_
                : Resources.BuildPeptideSearchLibraryControl_Result_files_;
            //peptideSearchSplitContainer.Visible = PerformDDASearch;

            if (PerformDDASearch)
                DdaSearchDataSources = DdaSearchDataSources;
            else
                SearchFilenames = SearchFilenames;
        }

        public bool PerformDDASearch
        {
            get { return InputFileType != ImportPeptideSearchDlg.InputFile.search_result; }
            set { InputFileType = value ? ImportPeptideSearchDlg.InputFile.dda_raw : ImportPeptideSearchDlg.InputFile.search_result; }
        }

        public bool DIAConversionNeeded => InputFileType == ImportPeptideSearchDlg.InputFile.dia_raw;


        private void comboStandards_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverStandards.SelectedIndexChangedEvent(sender, e);
        }

        private void comboInputFileType_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdatePerformDDASearch();
        }
    }
}
