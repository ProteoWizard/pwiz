/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.BiblioSpec;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    public class BuildLibraryGridView : DataGridViewEx
    {
        private BindingList<File> FilesList => DataSource as BindingList<File>;

        private const string CELL_LOADING = @"...";

        private static Color ReadonlyCellBackColor => Color.FromArgb(245, 245, 245);
        private readonly Color _defaultCellBackColor;

        private readonly DataGridViewColumn _colFile = new DataGridViewTextBoxColumn
        {
            DataPropertyName = @"FilePath",
            HeaderText = PeptideSearchResources.BuildLibraryGridView__colFile_File,
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };

        private readonly DataGridViewColumn _colScoreType = new DataGridViewTextBoxColumn
        {
            DataPropertyName = @"ScoreType",
            HeaderText = PeptideSearchResources.BuildLibraryGridView__colScoreType_Score_Type,
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells
        };

        private readonly DataGridViewColumn _colThreshold = new DataGridViewTextBoxColumn
        {
            DataPropertyName = @"ScoreThreshold",
            HeaderText = PeptideSearchResources.BuildLibraryGridView__colThreshold_Score_Threshold,
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells
        };

        public BuildLibraryGridView()
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Runtime)
            {
                _defaultCellBackColor = DefaultCellStyle.BackColor;

                AutoGenerateColumns = false;
                AllowUserToAddRows = false;

                Columns.Clear();
                AddColumns(new[] { _colFile, _colScoreType, _colThreshold });

                DataBindingComplete += (sender, e) => Sort(_colFile, ListSortDirection.Ascending);
                UserDeletingRow += OnUserDeletingRow;
                RowPrePaint += OnRowPrePaint;
                CellPainting += OnCellPainting;
                CellFormatting += OnCellFormatting;
                CellValueChanged += OnCellValueChanged;

                DataSource = new SortableBindingList<File>();
            }
        }

        public event EventHandler FilesChanged;

        [DefaultValue(false)]
        public bool IsFileOnly
        {
            get => !_colScoreType.Visible;
            set => _colScoreType.Visible = _colThreshold.Visible = !value;
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IEnumerable<File> Files
        {
            get => FilesList;
            set
            {
                CellValueChanged -= OnCellValueChanged;
                FilesList.Clear();
                FilesList.AddRange(value ?? Array.Empty<File>());
                CellValueChanged += OnCellValueChanged;
                FilesChanged?.Invoke(this, null);
            }
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IEnumerable<string> FilePaths
        {
            get => Files.Select(f => f.FilePath).OrderBy(f => f).Distinct().ToArray();
            set
            {
                var valueSet = (value ?? Array.Empty<string>()).ToHashSet();
                var existingSet = FilePaths.ToHashSet();
                if (valueSet.SetEquals(existingSet))
                    return;

                CellValueChanged -= OnCellValueChanged;

                // Remove existing files not in new value
                for (var i = FilesList.Count - 1; i >= 0; i--)
                    if (!valueSet.Contains(FilesList[i].FilePath))
                        FilesList.RemoveAt(i);

                // Remove already existing files from value
                valueSet.ExceptWith(existingSet);
                FilesList.AddRange(valueSet.Select(f => new File(f, null, null)));

                CellValueChanged += OnCellValueChanged;

                FilesChanged?.Invoke(this, null);

                if (valueSet.Any() && !IsFileOnly)
                {
                    var bw = new BackgroundWorker();
                    bw.DoWork += (sender, e) =>
                    {
                        Dictionary<string, ScoreTypesResult> scoreTypes;
                        Exception getScoreTypesException = null;
                        try
                        {
                            scoreTypes = GetScoreTypes(valueSet);
                        }
                        catch (Exception x)
                        {
                            scoreTypes = null;
                            getScoreTypesException = x;
                        }

                        Invoke(new MethodInvoker(() => GridUpdateScoreInfo(scoreTypes, getScoreTypesException)));
                    };
                    bw.RunWorkerAsync();
                }
            }
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IEnumerable<MsDataFileUri> FileUris
        {
            get => FilePaths.Select(f => new MsDataFilePath(f));
            set => FilePaths = value.Select(uri => uri.GetFilePath() + (uri.GetSampleIndex() > 0 ? $@":{uri.GetSampleIndex()}" : string.Empty));
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IEnumerable<File> SelectedFiles => SelectedCells.Cast<DataGridViewCell>()
            .Where(cell => cell.ColumnIndex == _colFile.Index)
            .Select(cell => cell.OwningRow).Distinct()
            .Select(row => (File)row.DataBoundItem);

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ScoreTypesLoaded => Files.All(f => !f.HasScoreTypeError && f.ScoreType != null);

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsReady => FilesList.Count > 0 && (IsFileOnly || Files.All(f => !f.HasScoreTypeError && f.ScoreType != null && f.ScoreThreshold.HasValue));

        public bool Validate(IWin32Window parent, CancelEventArgs e, bool showWarnings, out Dictionary<string, double> thresholdsByFile)
        {
            if (!ScoreTypesLoaded)
                throw new Exception(PeptideSearchResources.BuildLibraryGridView_GetThresholds_Score_types_not_loaded_);

            var thresholdsByScoreType = new Dictionary<ScoreType, double>();
            thresholdsByFile = new Dictionary<string, double>();

            var errors = new List<string>();
            foreach (DataGridViewRow row in Rows)
            {
                var file = (File)row.DataBoundItem;
                var error = !string.IsNullOrEmpty(row.ErrorText) ? row.ErrorText : row.Cells[_colThreshold.Index].ErrorText;
                if (string.IsNullOrEmpty(error))
                {
                    var threshold = file.ScoreThreshold.GetValueOrDefault();
                    thresholdsByFile[file.FilePath] = threshold;
                    if (file.ScoreType != null)
                        thresholdsByScoreType[file.ScoreType] = threshold;
                }
                else
                {
                    errors.Add($@"{row.Cells[_colFile.Index].Value}: {error}");
                }
            }

            if (errors.Any())
            {
                MessageDlg.Show(parent, TextUtil.LineSeparate(errors));
                if (e != null)
                    e.Cancel = true;
                return false;
            }

            if (showWarnings)
            {
                var warnings = new List<string>();
                foreach (var pair in thresholdsByScoreType)
                {
                    var scoreType = pair.Key;
                    if (!scoreType.CanSet)
                        continue;

                    var probCorrect = scoreType.ProbabilityType == ScoreType.EnumProbabilityType.probability_correct;
                    var probIncorrect = scoreType.ProbabilityType == ScoreType.EnumProbabilityType.probability_incorrect;
                    var threshold = pair.Value;
                    var thresholdIsMin = threshold.Equals(scoreType.ValidRange.Min);
                    var thresholdIsMax = threshold.Equals(scoreType.ValidRange.Max);
                    string warning = null;
                    if ((probCorrect && thresholdIsMax) || (probIncorrect && thresholdIsMin))
                    {
                        warning = string.Format(
                            PeptideSearchResources.BuildLibraryGridView_GetThresholds_Score_threshold__0__for__1__will_only_include_identifications_with_perfect_scores_,
                            threshold, scoreType);
                    }
                    else if ((probCorrect && thresholdIsMin) || (probIncorrect && thresholdIsMax))
                    {
                        warning = string.Format(
                            PeptideSearchResources.BuildLibraryGridView_GetThresholds_Score_threshold__0__for__1__will_include_all_identifications_,
                            threshold, scoreType);
                    }
                    else if (threshold < scoreType.SuggestedRange.Min || threshold > scoreType.SuggestedRange.Max)
                    {
                        warning = string.Format(
                            PeptideSearchResources.BuildLibraryGridView_GetThresholds_Score_threshold__0__for__1__is_unusually_permissive_,
                            threshold, scoreType);
                    }

                    if (!string.IsNullOrEmpty(warning))
                    {
                        if (probCorrect)
                            warning = TextUtil.SpaceSeparate(warning, string.Format(
                                PeptideSearchResources.BuildLibraryGridView_GetThresholds__0__scores_indicate_the_probability_that_an_identification_is__1__,
                                scoreType, PeptideSearchResources.BuildLibraryGridView_GetThresholds_correct));
                        else if (probIncorrect)
                            warning = TextUtil.SpaceSeparate(warning, string.Format(
                                PeptideSearchResources.BuildLibraryGridView_GetThresholds__0__scores_indicate_the_probability_that_an_identification_is__1__,
                                scoreType, PeptideSearchResources.BuildLibraryGridView_GetThresholds_incorrect));
                        warnings.Add(warning);
                    }
                }

                if (warnings.Any())
                {
                    warnings.AddRange(new[] { string.Empty, PeptideSearchResources.BuildLibraryGridView_Validate_Are_you_sure_you_want_to_continue_ });
                    if (MultiButtonMsgDlg.Show(parent, TextUtil.LineSeparate(warnings), MessageBoxButtons.OKCancel) == DialogResult.Cancel)
                    {
                        if (e != null)
                            e.Cancel = true;
                        return false;
                    }
                }
            }

            // Update settings
            foreach (var threshold in thresholdsByScoreType)
                BiblioSpecLiteBuilder.SetDefaultScoreThreshold(threshold.Key.NameInvariant, threshold.Value);

            return true;
        }

        private Dictionary<string, ScoreTypesResult> GetScoreTypes(ICollection<string> files)
        {
            var blibBuild = new BlibBuild(null, files.ToArray());
            IProgressStatus status = new ProgressStatus();
            var results = blibBuild.GetScoreTypes(new SilentProgressMonitor(), ref status, out _);

            // Match input/output files
            var filesByLength = files.OrderByDescending(s => s.Length).ToArray();
            var resultsTmp = new Dictionary<string, ScoreTypesResult>();
            foreach (var pair in results)
                resultsTmp[filesByLength.First(f => f.EndsWith(pair.Key))] = pair.Value;
            results = resultsTmp;

            return results;
        }

        private void GridUpdateScoreInfo(IReadOnlyDictionary<string, ScoreTypesResult> scoreTypes, Exception exception)
        {
            // Gather existing score thresholds
            var existingThresholds = new Dictionary<string, double?>();
            foreach (var file in Files.Where(f => f.ScoreType != null && (scoreTypes == null || !scoreTypes.ContainsKey(f.FilePath))))
                existingThresholds[file.ScoreType.NameInvariant] = file.ScoreThreshold;

            var filesToProcess = Files.Where(f => f.ScoreType == null).ToArray();
            foreach (var file in filesToProcess)
            {
                ScoreTypesResult scoreTypesThis = null;
                if (exception != null)
                {
                    file.ScoreTypeError = exception.Message;
                }
                else if (scoreTypes == null || !scoreTypes.TryGetValue(file.FilePath, out scoreTypesThis))
                {
                    file.ScoreTypeError = PeptideSearchResources.BuildLibraryGridView_GridUpdateScoreInfo_Score_type_not_found_;
                }
                else if (scoreTypesThis.HasError)
                {
                    file.ScoreTypeError = TextUtil.LineSeparate(scoreTypesThis.Errors);
                }

                if (file.HasScoreTypeError || scoreTypesThis == null)
                {
                    InvalidateRow(FindRowIndex(file));
                    continue;
                }

                for (var i = 0; i < scoreTypesThis.NumScoreTypes; i++)
                {
                    var scoreType = scoreTypesThis.ScoreTypes[i];

                    double? threshold;
                    if (scoreType.CanSet)
                    {
                        if (!existingThresholds.TryGetValue(scoreType.NameInvariant, out threshold))
                            threshold = BiblioSpecLiteBuilder.GetDefaultScoreThreshold(scoreType.NameInvariant, scoreType.DefaultValue);
                    }
                    else
                    {
                        threshold = scoreType.DefaultValue;
                    }

                    if (i == 0)
                    {
                        file.ScoreType = scoreType;
                        file.ScoreThreshold = threshold;

                        var row = FindRowIndex(file);
                        InvalidateCell(_colScoreType.Index, row);
                        InvalidateCell(_colThreshold.Index, row);
                    }
                    else
                    {
                        FilesList.Add(new File(file.FilePath, scoreType, threshold));
                    }
                }
            }

            FilesChanged?.Invoke(this, null);
        }

        public void Remove(IEnumerable<File> files)
        {
            var set = files.Select(f => f.FilePath).ToHashSet();
            var anyRemoved = false;
            for (var i = FilesList.Count - 1; i >= 0; i--)
            {
                if (set.Contains(FilesList[i].FilePath))
                {
                    FilesList.RemoveAt(i);
                    anyRemoved = true;
                }
            }
            if (anyRemoved)
                FilesChanged?.Invoke(this, null);
        }

        private DataGridViewRow FindRow(File file)
        {
            var i = FindRowIndex(file);
            return i >= 0 ? Rows[i] : null;
        }

        private int FindRowIndex(File file)
        {
            for (var i = 0; i < RowCount; i++)
                if (ReferenceEquals(Rows[i].DataBoundItem, file))
                    return i;
            return -1;
        }

        #region Functional test support

        public void SetScoreThreshold(double threshold)
        {
            SetScoreThreshold(scoreType => threshold);
        }

        public void SetScoreThreshold(Func<ScoreType, double?> thresholdFunc)
        {
            foreach (DataGridViewRow row in Rows)
            {
                var file = (File)row.DataBoundItem;
                if (file.ScoreType == null || !file.ScoreType.CanSet)
                    continue;

                var threshold = thresholdFunc.Invoke(file.ScoreType);
                if (threshold != null)
                {
                    file.ScoreThreshold = threshold;
                    InvalidateCell(row.Cells[_colThreshold.Index]);
                }
            }
        }
        #endregion

        private void OnUserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            // Use own remove function in case multiple rows with same file
            Remove(Files.Where(f => Equals(((File)e.Row.DataBoundItem).FilePath, f.FilePath)));
            e.Cancel = true;
        }

        private void OnRowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            var row = Rows[e.RowIndex];
            var file = (File)row.DataBoundItem;

            row.ErrorText = !file.HasScoreTypeError
                ? null
                : TextUtil.LineSeparate(PeptideSearchResources.BuildLibraryGridView_OnRowPrepaint_Error_getting_score_type_for_this_file_,
                    string.Empty,
                    file.ScoreTypeError);
        }

        private void OnCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            var row = Rows[e.RowIndex];
            var cell = row.Cells[e.ColumnIndex];
            var file = (File)row.DataBoundItem;

            if (e.ColumnIndex == _colThreshold.Index)
            {
                var scoreType = file.ScoreType;
                var threshold = file.ScoreThreshold;

                cell.ReadOnly = !file.CanSetScore || file.HasScoreTypeError;
                cell.ErrorText = scoreType == null || (threshold.HasValue && scoreType.ValidRange.Min <= threshold && threshold <= scoreType.ValidRange.Max)
                    ? null
                    : string.Format(
                        PeptideSearchResources.BuildLibraryGridView_OnCellPainting_Score_threshold___0___is_invalid__must_be_a_decimal_value_between__1__and__2___,
                        threshold.ToString(), scoreType.ValidRange.Min, scoreType.ValidRange.Max);
                cell.ToolTipText = scoreType?.ThresholdDescription;
            }

            cell.Style.BackColor = cell.ReadOnly ? ReadonlyCellBackColor : _defaultCellBackColor;
        }

        private void OnCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var file = (File)Rows[e.RowIndex].DataBoundItem;
            if (e.ColumnIndex == _colFile.Index)
            {
                e.Value = PathEx.RemovePrefix(file.FilePath, PathEx.GetCommonRoot(FilePaths));
                e.FormattingApplied = true;
            }
            else if (e.ColumnIndex == _colScoreType.Index)
            {
                if (file.HasScoreTypeError)
                {
                    e.Value = string.Empty;
                    e.FormattingApplied = true;
                }
                else if (file.ScoreType == null)
                {
                    e.Value = CELL_LOADING;
                    e.FormattingApplied = true;
                }
            }
            else if (e.ColumnIndex == _colThreshold.Index)
            {
                if (file.HasScoreTypeError)
                {
                    e.Value = string.Empty;
                    e.FormattingApplied = true;
                }
                else if (!file.ScoreThreshold.HasValue)
                {
                    e.Value = CELL_LOADING;
                    e.FormattingApplied = true;
                }
            }
        }

        private void OnCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            var file = (File)Rows[e.RowIndex].DataBoundItem;
            var repaint = false;

            if (e.ColumnIndex == _colThreshold.Index)
            {

                var scoreType = file.ScoreType;
                if (scoreType == null)
                    return;

                // Copy new threshold to all other files with same score type
                foreach (var fileOther in Files.Where(f => Equals(scoreType, f.ScoreType)))
                {
                    fileOther.ScoreThreshold = file.ScoreThreshold;
                    repaint = true;
                }
            }

            if (repaint)
                Invalidate();
        }

        public class File
        {
            public string FilePath { get; set; }
            [Track]
            public string FileName => Path.GetFileName(FilePath);
            public ScoreType ScoreType { get; set; }
            [Track]
            public string ScoreTypeName => ScoreType?.ToString();
            public bool CanSetScore => ScoreType != null && ScoreType.CanSet;
            [Track]
            public double? ScoreThreshold { get; set; }
            public string ScoreTypeError { get; set; }
            public bool HasScoreTypeError => ScoreTypeError != null;

            public File(string filePath, ScoreType scoreType, double? scoreThreshold)
            {
                FilePath = filePath;
                ScoreType = scoreType;
                ScoreThreshold = scoreThreshold;
                ScoreTypeError = null;
            }

            public bool Equals(File obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return Equals(obj.FilePath, FilePath) &&
                       Equals(obj.ScoreType, ScoreType) &&
                       Equals(obj.ScoreThreshold, ScoreThreshold) &&
                       Equals(obj.ScoreTypeError, ScoreTypeError);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj.GetType() == typeof(File) && Equals((File)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var result = FilePath?.GetHashCode() ?? 0;
                    result = (result * 397) ^ (ScoreType?.GetHashCode() ?? 0);
                    result = (result * 397) ^ ScoreThreshold.GetHashCode();
                    result = (result * 397) ^ (ScoreTypeError ?? string.Empty).GetHashCode();
                    return result;
                }
            }
        }
    }
}
