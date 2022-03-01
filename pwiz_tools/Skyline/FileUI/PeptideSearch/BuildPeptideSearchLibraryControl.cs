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
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.BiblioSpec;
using pwiz.Common.Database;
using pwiz.Skyline.Model.Results;
using BiblioSpecLiteLibrary = pwiz.Skyline.Model.Lib.BiblioSpecLiteLibrary;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public partial class BuildPeptideSearchLibraryControl : UserControl
    {
        private readonly SettingsListComboDriver<IrtStandard> _driverStandards;
        private MsDataFileUri[] _ddaSearchDataSources = Array.Empty<MsDataFileUri>();

        private readonly Color _defaultCellBackColor;
        private static Color ReadonlyCellBackColor => Color.FromArgb(245, 245, 245);
        private const string CELL_LOADING = @"...";

        public BuildPeptideSearchLibraryControl(IModifyDocumentContainer documentContainer, ImportPeptideSearch importPeptideSearch, LibraryManager libraryManager)
        {
            DocumentContainer = documentContainer;
            ImportPeptideSearch = importPeptideSearch;
            LibraryManager = libraryManager;

            InitializeComponent();

            CutOffScore = ImportPeptideSearch.CutoffScore;

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

            _defaultCellBackColor = gridSearchFiles.DefaultCellStyle.BackColor;
            columnThreshold.CellTemplate = new ThresholdCell();
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
            private set { SetSearchFiles(value); }
        }

        private void SetSearchFiles(string[] searchFiles)
        {
            // Set new value
            ImportPeptideSearch.SearchFilenames = searchFiles;

            // Always show sorted list of files
            Array.Sort(ImportPeptideSearch.SearchFilenames);

            var toAdd = searchFiles.ToHashSet();

            // Populate the input files list
            gridSearchFiles.CellValueChanged -= gridSearchFiles_CellValueChanged;

            var existingFiles = new HashSet<string>();
            for (var i = gridSearchFiles.RowCount - 1; i >= 0; i--)
            {
                var existingFile = FileCellValue.Get(gridSearchFiles.Rows[i], columnFile).File;
                if (!toAdd.Contains(existingFile))
                    gridSearchFiles.Rows.RemoveAt(i);
                else
                    existingFiles.Add(existingFile);
            }
            toAdd.ExceptWith(existingFiles);

            foreach (var file in toAdd)
                AddRow(null, file, null);

            if (gridSearchFiles.SortedColumn == null || gridSearchFiles.SortOrder == SortOrder.None)
                gridSearchFiles.Sort(columnFile, ListSortDirection.Ascending);
            else
                gridSearchFiles.Sort(gridSearchFiles.SortedColumn, gridSearchFiles.SortOrder == SortOrder.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending);

            gridSearchFiles.CellValueChanged += gridSearchFiles_CellValueChanged;

            if (!toAdd.Any())
                return;

            FireInputFilesChanged();
            var bw = new BackgroundWorker();
            bw.DoWork += (sender, e) =>
            {
                var success = GetScoreTypes(toAdd, out var scoreTypes);
                Invoke(new MethodInvoker(() => GridUpdateScoreInfo(success, scoreTypes)));
            };
            bw.RunWorkerAsync();
        }

        private bool GetScoreTypes(ICollection<string> files, out Dictionary<string, BiblioSpecScoreType[]> scoreTypes)
        {
            var blibBuild = new BlibBuild(null, files.ToArray());
            IProgressStatus status = new ProgressStatus();
            var success = blibBuild.GetScoreTypes(new SilentProgressMonitor(), ref status, out _, out _, out scoreTypes);

            // Match input/output files
            var filesByLength = files.OrderByDescending(s => s.Length).ToArray();
            var scoreTypesTmp = new Dictionary<string, BiblioSpecScoreType[]>();
            foreach (var pair in scoreTypes)
                scoreTypesTmp[filesByLength.First(f => f.EndsWith(pair.Key))] = pair.Value;
            scoreTypes = scoreTypesTmp;

            return success;
        }

        private void GridUpdateScoreInfo(bool success, IReadOnlyDictionary<string, BiblioSpecScoreType[]> scoreTypes)
        {
            gridSearchFiles.CellValueChanged -= gridSearchFiles_CellValueChanged;

            // Gather existing score thresholds
            var existingThresholds = new Dictionary<string, double?>();
            foreach (var row in gridSearchFiles.Rows.Cast<DataGridViewRow>().Where(row => !scoreTypes.ContainsKey(FileCellValue.Get(row, columnFile).File)))
            {
                var scoreType = ScoreTypeCellValue.Get(row, columnScoreType).NameInvariant;
                if (scoreType != null)
                    existingThresholds[scoreType] = ThresholdCell.Get(row, columnThreshold).Threshold;
            }

            for (var i = 0; i < gridSearchFiles.RowCount; i++)
            {
                var row = gridSearchFiles.Rows[i];
                var file = FileCellValue.Get(row, columnFile).File;

                if (!success || !scoreTypes.TryGetValue(FileCellValue.Get(row, columnFile).File, out var scoreTypesThis))
                {
                    row.ErrorText = Resources.BuildPeptideSearchLibraryControl_GridUpdateScoreInfo_Error_getting_score_type_for_this_file_;
                    row.Cells[columnScoreType.Index].Value = null;
                    ThresholdCell.Get(row, columnThreshold).Value = null;
                    continue;
                }

                for (var j = 0; j < scoreTypesThis.Length; j++)
                {
                    var scoreType = scoreTypesThis[j];
                    if (j == 0)
                    {
                        row.Cells[columnScoreType.Index].Value = new ScoreTypeCellValue(scoreType);
                    }
                    else
                    {
                        row = AddRow(++i, file, scoreType);
                    }
                    var thresholdCell = ThresholdCell.Get(row, columnThreshold);
                    if (scoreType != null)
                    {
                        if (scoreType.CanSet)
                        {
                            thresholdCell.Value = existingThresholds.TryGetValue(scoreType.NameInvariant, out var threshold)
                                ? threshold
                                : BiblioSpecLiteBuilder.GetDefaultScoreThreshold(scoreType.NameInvariant, scoreType.DefaultValue);
                            thresholdCell.ToolTipText = scoreType.ProbabilityType == BiblioSpecScoreType.EnumProbabilityType.probability_correct
                                ? Resources.BuildPeptideSearchLibraryControl_AddSearchFiles_Score_threshold_minimum__score_is_probability_that_identification_is_correct__
                                : Resources.BuildPeptideSearchLibraryControl_AddSearchFiles_Score_threshold_maximum__score_is_probability_that_identification_is_incorrect__;
                            thresholdCell.ReadOnly = false;
                        }
                        else
                        {
                            thresholdCell.Value = scoreType.DefaultValue;
                        }
                    }
                    else
                    {
                        thresholdCell.Value = null;
                    }
                }
            }

            gridSearchFiles.CellValueChanged += gridSearchFiles_CellValueChanged;

            FireInputFilesChanged();
        }

        private DataGridViewRow AddRow(int? insertPos, string file, BiblioSpecScoreType scoreType)
        {
            var i = insertPos ?? gridSearchFiles.RowCount;
            gridSearchFiles.Rows.Insert(i);
            gridSearchFiles[columnFile.Index, i].Value = new FileCellValue(gridSearchFiles, columnFile, file);
            gridSearchFiles[columnScoreType.Index, i].Value = new ScoreTypeCellValue(scoreType);
            var thresholdCell = new ThresholdCell();
            gridSearchFiles[columnThreshold.Index, i] = thresholdCell;
            thresholdCell.ReadOnly = true;
            return gridSearchFiles.Rows[i];
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

                // Populate the input files list
                gridSearchFiles.Rows.Clear();
                foreach (var uri in _ddaSearchDataSources)
                {
                    var fileAndSampleLocator = uri.GetFilePath();
                    if (uri.GetSampleIndex() > 0)
                        fileAndSampleLocator += $@":{uri.GetSampleIndex()}";
                    var i = gridSearchFiles.Rows.Add();
                    gridSearchFiles[columnFile.Index, i].Value = new FileCellValue(gridSearchFiles, columnFile, fileAndSampleLocator);
                }

                FireInputFilesChanged();
            }
        }

        private IEnumerable<string> SelectedFiles => gridSearchFiles.SelectedCells.Cast<DataGridViewCell>()
            .Where(cell => cell.ColumnIndex == columnFile.Index)
            .Select(cell => cell.OwningRow).Distinct()
            .Select(row => FileCellValue.Get(row, columnFile))
            .Where(value => value != null)
            .Select(value => value.File);      

        private void btnRemFile_Click(object sender, EventArgs e)
        {
            RemoveFiles();
        }

        public void RemoveFiles()
        {
            var selectedFiles = SelectedFiles.ToHashSet();
            if (PerformDDASearch)
                DdaSearchDataSources = DdaSearchDataSources.Where(source => !selectedFiles.Contains(source.GetFilePath())).ToArray();
            else
                SearchFilenames = SearchFilenames.Where(file => !selectedFiles.Contains(file)).ToArray();
            FireInputFilesChanged();
        }

        private void gridSearchFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnRemFile.Enabled = SelectedFiles.Any();
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
            SetSearchFiles(BuildLibraryDlg.AddInputFiles(WizardForm, SearchFilenames, fileNames, PerformDDASearch));
        }

        public bool ScoreTypesLoaded => ScoreTypes.All(scoreType => scoreType != null);

        public BiblioSpecScoreType[] ScoreTypes
        {
            get
            {
                var scoreTypes = new BiblioSpecScoreType[gridSearchFiles.RowCount];
                for (var i = 0; i < gridSearchFiles.RowCount; i++)
                    scoreTypes[i] = ScoreTypeCellValue.Get(gridSearchFiles.Rows[i], columnScoreType).ScoreType;
                return scoreTypes;
            }

            set
            {
                if (value.Length != gridSearchFiles.RowCount)
                    throw new Exception(Resources.BuildPeptideSearchLibraryControl_ScoreThresholds_Number_of_score_thresholds_does_not_match_number_of_rows_in_grid_);
                for (var i = 0; i < gridSearchFiles.RowCount; i++)
                    ScoreTypeCellValue.Get(gridSearchFiles.Rows[i], columnScoreType).ScoreType = value[i];
            }
        }

        public double?[] ScoreThresholds
        {
            get
            {
                var thresholds = new double?[gridSearchFiles.RowCount];
                for (var i = 0; i < gridSearchFiles.RowCount; i++)
                    thresholds[i] = ThresholdCell.Get(gridSearchFiles.Rows[i], columnThreshold).Threshold;
                return thresholds;
            }

            set
            {
                if (value.Length != gridSearchFiles.RowCount)
                    throw new Exception(Resources.BuildPeptideSearchLibraryControl_ScoreThresholds_Number_of_score_thresholds_does_not_match_number_of_rows_in_grid_);
                for (var i = 0; i < gridSearchFiles.RowCount; i++)
                    ThresholdCell.Get(gridSearchFiles.Rows[i], columnThreshold).Threshold = value[i];
            }
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

        public bool BuildOrUsePeptideSearchLibrary(CancelEventArgs e, bool showWarnings)
        {
            return UseExistingLibrary ? AddExistingLibrary(e) : BuildPeptideSearchLibrary(e, showWarnings);
        }

        public string LastBuildCommandArgs { get; private set; }
        public string LastBuildOutput { get; private set; }

        private bool BuildPeptideSearchLibrary(CancelEventArgs e, bool showWarnings)
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

            var thresholdsByScoreType = new Dictionary<BiblioSpecScoreType, double>();
            var thresholdsByFile = new Dictionary<string, double>();
            var errors = new List<string>();
            var warnings = new List<string>();
            foreach (DataGridViewRow row in gridSearchFiles.Rows)
            {
                var file = FileCellValue.Get(row, columnFile);
                var scoreType = ScoreTypeCellValue.Get(row, columnScoreType);
                var thresholdCell = ThresholdCell.Get(row, columnThreshold);
                var error = !string.IsNullOrEmpty(row.ErrorText) ? row.ErrorText : thresholdCell.ErrorText;
                if (string.IsNullOrEmpty(error))
                {
                    var threshold = thresholdCell.Threshold.GetValueOrDefault();
                    thresholdsByFile[file.File] = threshold;
                    if (scoreType.ScoreType != null)
                        thresholdsByScoreType[scoreType.ScoreType] = threshold;
                }
                else
                {
                    errors.Add(string.Format(@"{0}: {1}", file.File, error));
                }
            }
            if (errors.Any())
            {
                MessageDlg.Show(WizardForm, TextUtil.LineSeparate(errors));
                e.Cancel = true;
                return false;
            }

            if (showWarnings)
            {
                foreach (var pair in thresholdsByScoreType)
                {
                    var scoreType = pair.Key;
                    if (!scoreType.CanSet)
                        continue;

                    var probCorrect = scoreType.ProbabilityType == BiblioSpecScoreType.EnumProbabilityType.probability_correct;
                    var probIncorrect = scoreType.ProbabilityType == BiblioSpecScoreType.EnumProbabilityType.probability_incorrect;
                    var threshold = pair.Value;
                    var thresholdIsMin = threshold.Equals(scoreType.ValidRange.Min);
                    var thresholdIsMax = threshold.Equals(scoreType.ValidRange.Max);
                    string warning = null;
                    if ((probCorrect && thresholdIsMax) || (probIncorrect && thresholdIsMin))
                    {
                        warning = string.Format(
                            Resources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Score_threshold__0__for__1__will_only_include_identifications_with_perfect_scores_,
                            threshold, scoreType.DisplayName);
                    }
                    else if ((probCorrect && thresholdIsMin) || (probIncorrect && thresholdIsMax))
                    {
                        warning = string.Format(
                            Resources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Score_threshold__0__for__1__will_include_all_identifications_,
                            threshold, scoreType.DisplayName);
                    }
                    else if (threshold < scoreType.SuggestedRange.Min || threshold > scoreType.SuggestedRange.Max)
                    {
                        warning = string.Format(
                            Resources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Score_threshold__0__for__1__is_unusually_permissive_,
                            threshold, scoreType.DisplayName);
                    }

                    if (!string.IsNullOrEmpty(warning))
                    {
                        if (probCorrect)
                            warning = TextUtil.SpaceSeparate(warning, string.Format(
                                Resources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary__0__scores_indicate_the_probability_that_an_identification_is__1__,
                                scoreType.DisplayName, Resources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_correct));
                        else if (probIncorrect)
                            warning = TextUtil.SpaceSeparate(warning, string.Format(
                                Resources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary__0__scores_indicate_the_probability_that_an_identification_is__1__,
                                scoreType.DisplayName, Resources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_incorrect));
                        warnings.Add(warning);
                    }
                }

                if (warnings.Any())
                {
                    warnings.AddRange(new[] { string.Empty, Resources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Are_you_sure_you_want_to_continue_ });
                    if (MultiButtonMsgDlg.Show(WizardForm, TextUtil.LineSeparate(warnings), MessageBoxButtons.OKCancel) == DialogResult.Cancel)
                    {
                        e.Cancel = true;
                        return false;
                    }
                }
            }
            foreach (var threshold in thresholdsByScoreType)
                BiblioSpecLiteBuilder.SetDefaultScoreThreshold(threshold.Key.NameInvariant, threshold.Value);

            BiblioSpecLiteBuilder builder;
            try
            {
                builder = ImportPeptideSearch.GetLibBuilder(DocumentContainer.Document, DocumentContainer.DocumentFilePath, cbIncludeAmbiguousMatches.Checked);
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

            var addedIrts = LibraryBuildNotificationHandler.AddIrts(IrtRegressionType.DEFAULT,
                ImportPeptideSearch.DocLib, docLibSpec, _driverStandards.SelectedItem, WizardForm, false, out var outStandard);

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
            ImportPeptideSearch.IrtStandard = outStandard;
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

            var blib = ImportPeptideSearch.DocLib as BiblioSpecLiteLibrary;
            if (blib?.ReadStream is ConnectionId<SQLiteConnection> connection && SqliteOperations.TableExists(connection.Connection, @"IrtLibrary"))
            {
                using (var dlg = new MultiButtonMsgDlg(
                    Resources.BuildPeptideSearchLibraryControl_AddExistingLibrary_This_library_contains_iRT_values__Do_you_want_to_create_a_retention_time_predictor_with_these_values_,
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

            string extraHelp = Resources.VendorIssueHelper_ShowLibraryMissingExternalSpectraError_ButtonDescriptions;

            string messageFormat = spectrumFilenames.Count > 1
                ? Resources.VendorIssueHelper_ShowLibraryMissingExternalSpectrumFilesError
                : Resources.VendorIssueHelper_ShowLibraryMissingExternalSpectrumFileError;

            // TODO: parse supported file extensions from BiblioSpec or ProteoWizard
            var dialogResult = MultiButtonMsgDlg.Show(parentWindow,
                string.Format(messageFormat,
                    resultsFilepath, string.Join(Environment.NewLine, spectrumFilenames),
                    string.Join(Environment.NewLine, directoriesSearched),
                    BiblioSpecLiteBuilder.BiblioSpecSupportedFileExtensions) + extraHelp,
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

        public bool IsReady
        {
            get
            {
                return UseExistingLibrary
                    ? !string.IsNullOrEmpty(tbxLibraryPath.Text)
                    : gridSearchFiles.RowCount > 0 &&
                      (PerformDDASearch || gridSearchFiles.Rows.Cast<DataGridViewRow>().All(row =>
                      {
                          var scoreType = ScoreTypeCellValue.Get(row, columnScoreType)?.ToString();
                          var threshold = ThresholdCell.Get(row, columnThreshold)?.ToString();
                          return scoreType != null && !Equals(scoreType, CELL_LOADING) && threshold != null && !Equals(threshold, CELL_LOADING);
                      }));
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
            {
                DdaSearchDataSources = DdaSearchDataSources;
                panelSearchThreshold.Visible = true;
                columnScoreType.Visible = false;
                columnThreshold.Visible = false;
            }
            else
            {
                SearchFilenames = SearchFilenames;
                panelSearchThreshold.Visible = false;
                columnScoreType.Visible = true;
                columnThreshold.Visible = true;
            }
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

        private void gridSearchFiles_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            foreach (var cell in gridSearchFiles.Rows[e.RowIndex].Cells.Cast<DataGridViewCell>())
                cell.Style.BackColor = cell.ReadOnly ? ReadonlyCellBackColor : _defaultCellBackColor;
        }

        private void gridSearchFiles_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != columnThreshold.Index)
                return;

            var scoreType = ScoreTypeCellValue.Get(e.RowIndex, columnScoreType)?.ScoreType;
            if (scoreType == null)
                return;

            // Copy new threshold to all other files with same score type
            var thresholdCell = ThresholdCell.Get(gridSearchFiles.Rows[e.RowIndex], columnThreshold);
            var threshold = thresholdCell.Threshold;
            var errorText = threshold.HasValue && scoreType.ValidRange.Min <= threshold && threshold <= scoreType.ValidRange.Max
                ? null
                : string.Format(
                    Resources.BuildPeptideSearchLibraryControl_BuildPeptideSearchLibrary_Score_threshold___0___is_invalid__must_be_a_decimal_value_between__1__and__2___,
                    thresholdCell.Value, scoreType.ValidRange.Min, scoreType.ValidRange.Max);

            foreach (var row in gridSearchFiles.Rows.Cast<DataGridViewRow>().Where(row => Equals(ScoreTypeCellValue.Get(row, columnScoreType)?.ScoreType, scoreType)))
            {
                var thisCell = ThresholdCell.Get(row, columnThreshold);
                thisCell.Value = thresholdCell.Value;
                thisCell.ErrorText = errorText;
            }
        }

        private class FileCellValue
        {
            private DataGridView Grid { get; }
            private DataGridViewColumn FileColumn { get; }
            public string File { get; }

            public FileCellValue(DataGridView grid, DataGridViewColumn fileColumn, string file)
            {
                Grid = grid;
                FileColumn = fileColumn;
                File = file;
            }

            public override string ToString()
            {
                return PathEx.RemovePrefix(File, PathEx.GetCommonRoot(Grid.Rows.Cast<DataGridViewRow>().Select(row => Get(row, FileColumn).File)));
            }

            public static FileCellValue Get(DataGridViewRow row, DataGridViewColumn col)
            {
                return row.Cells[col.Index].Value as FileCellValue;
            }
        }

        private class ScoreTypeCellValue
        {
            public BiblioSpecScoreType ScoreType { get; set; }
            public string NameInvariant => ScoreType?.NameInvariant;

            public ScoreTypeCellValue(BiblioSpecScoreType scoreType = null)
            {
                ScoreType = scoreType;
            }

            public override string ToString()
            {
                return ScoreType != null ? ScoreType.DisplayName : CELL_LOADING;
            }

            public static ScoreTypeCellValue Get(DataGridViewRow row, DataGridViewColumn col)
            {
                return row.Cells[col.Index].Value as ScoreTypeCellValue;
            }

            public static ScoreTypeCellValue Get(int rowIndex, DataGridViewColumn col)
            {
                return Get(col.DataGridView.Rows[rowIndex], col);
            }
        }

        private class ThresholdCell : DataGridViewTextBoxCell
        {
            public double? Threshold
            {
                get
                {
                    switch (Value)
                    {
                        case null:
                            return null;
                        case double d:
                            return d;
                        default:
                            return double.TryParse(Value.ToString(), out var threshold) ? (double?)threshold : null;
                    }
                }

                set => Value = value;
            }

            public ThresholdCell() : this(null)
            {
                // Need parameterless constructor to use as CellTemplate
            }

            private ThresholdCell(double? threshold)
            {
                if (threshold.HasValue)
                    Value = threshold.Value;
                else
                    Value = CELL_LOADING;
            }

            public static ThresholdCell Get(DataGridViewRow row, DataGridViewColumn col)
            {
                return row.Cells[col.Index] as ThresholdCell;
            }
        }
    }
}
