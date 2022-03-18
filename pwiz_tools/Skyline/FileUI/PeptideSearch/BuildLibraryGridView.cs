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
using System.Text;
using System.Windows.Forms;
using pwiz.BiblioSpec;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
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
            HeaderText = Resources.BuildLibraryGridView__colFile_File,
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };

        private readonly DataGridViewColumn _colScoreType = new DataGridViewTextBoxColumn
        {
            DataPropertyName = @"ScoreType",
            HeaderText = Resources.BuildLibraryGridView__colScoreType_Score_Type,
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells
        };

        private readonly DataGridViewColumn _colThreshold = new DataGridViewTextBoxColumn
        {
            DataPropertyName = @"ScoreThreshold",
            HeaderText = Resources.BuildLibraryGridView__colThreshold_Score_Threshold,
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
                        bool success;
                        Dictionary<string, BiblioSpecScoreType[]> scoreTypes;
                        Exception getScoreTypesException = null;
                        try
                        {
                            success = GetScoreTypes(valueSet, out scoreTypes);
                        }
                        catch (Exception x)
                        {
                            success = false;
                            scoreTypes = null;
                            getScoreTypesException = x;
                        }

                        Invoke(new MethodInvoker(() => GridUpdateScoreInfo(success, scoreTypes, getScoreTypesException)));
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
        public bool ScoreTypesLoaded => Files.All(f => !f.ScoreTypeError && f.ScoreType != null);

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsReady => FilesList.Count > 0 && (IsFileOnly || Files.All(f => !f.ScoreTypeError && f.ScoreType != null && f.ScoreThreshold.HasValue));

        public bool Validate(IWin32Window parent, CancelEventArgs e, bool showWarnings, out Dictionary<string, double> thresholdsByFile)
        {
            if (!ScoreTypesLoaded)
                throw new Exception(Resources.BuildLibraryGridView_GetThresholds_Score_types_not_loaded_);

            var thresholdsByScoreType = new Dictionary<BiblioSpecScoreType, double>();
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

                    var probCorrect = scoreType.ProbabilityType == BiblioSpecScoreType.EnumProbabilityType.probability_correct;
                    var probIncorrect = scoreType.ProbabilityType == BiblioSpecScoreType.EnumProbabilityType.probability_incorrect;
                    var threshold = pair.Value;
                    var thresholdIsMin = threshold.Equals(scoreType.ValidRange.Min);
                    var thresholdIsMax = threshold.Equals(scoreType.ValidRange.Max);
                    string warning = null;
                    if ((probCorrect && thresholdIsMax) || (probIncorrect && thresholdIsMin))
                    {
                        warning = string.Format(
                            Resources.BuildLibraryGridView_GetThresholds_Score_threshold__0__for__1__will_only_include_identifications_with_perfect_scores_,
                            threshold, scoreType);
                    }
                    else if ((probCorrect && thresholdIsMin) || (probIncorrect && thresholdIsMax))
                    {
                        warning = string.Format(
                            Resources.BuildLibraryGridView_GetThresholds_Score_threshold__0__for__1__will_include_all_identifications_,
                            threshold, scoreType);
                    }
                    else if (threshold < scoreType.SuggestedRange.Min || threshold > scoreType.SuggestedRange.Max)
                    {
                        warning = string.Format(
                            Resources.BuildLibraryGridView_GetThresholds_Score_threshold__0__for__1__is_unusually_permissive_,
                            threshold, scoreType);
                    }

                    if (!string.IsNullOrEmpty(warning))
                    {
                        if (probCorrect)
                            warning = TextUtil.SpaceSeparate(warning, string.Format(
                                Resources.BuildLibraryGridView_GetThresholds__0__scores_indicate_the_probability_that_an_identification_is__1__,
                                scoreType, Resources.BuildLibraryGridView_GetThresholds_correct));
                        else if (probIncorrect)
                            warning = TextUtil.SpaceSeparate(warning, string.Format(
                                Resources.BuildLibraryGridView_GetThresholds__0__scores_indicate_the_probability_that_an_identification_is__1__,
                                scoreType, Resources.BuildLibraryGridView_GetThresholds_incorrect));
                        warnings.Add(warning);
                    }
                }

                if (warnings.Any())
                {
                    warnings.AddRange(new[] { string.Empty, Resources.BuildLibraryGridView_Validate_Are_you_sure_you_want_to_continue_ });
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

        private void GridUpdateScoreInfo(bool success, IReadOnlyDictionary<string, BiblioSpecScoreType[]> scoreTypes, Exception exception)
        {
            CellValueChanged -= OnCellValueChanged;

            // Gather existing score thresholds
            var existingThresholds = new Dictionary<string, double?>();
            foreach (var file in Files.Where(f => f.ScoreType != null && (scoreTypes == null || !scoreTypes.ContainsKey(f.FilePath))))
                existingThresholds[file.ScoreType.NameInvariant] = file.ScoreThreshold;

            var filesToProcess = Files.Where(f => f.ScoreType == null).ToArray();
            foreach (var file in filesToProcess)
            {
                if (!success || exception != null || scoreTypes == null || !scoreTypes.TryGetValue(file.FilePath, out var scoreTypesThis))
                {
                    var errorSb = new StringBuilder(Resources.BuildLibraryGridView_GridUpdateScoreInfo_Error_getting_score_type_for_this_file_);
                    if (exception != null)
                    {
                        errorSb.AppendLine();
                        errorSb.Append(exception.Message);
                    }
                    var row = FindRow(file);
                    row.ErrorText = errorSb.ToString();
                    file.ScoreTypeError = true;
                    InvalidateCell(row.Cells[_colScoreType.Index]);
                    InvalidateCell(row.Cells[_colThreshold.Index]);
                    continue;
                }

                for (var i = 0; i < scoreTypesThis.Length; i++)
                {
                    var scoreType = scoreTypesThis[i];

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

                    DataGridViewRow row;
                    if (i == 0)
                    {
                        file.ScoreType = scoreType;
                        file.ScoreThreshold = threshold;
                        row = FindRow(file);
                    }
                    else
                    {
                        var newFile = new File(file.FilePath, scoreType, threshold);
                        FilesList.Add(newFile);
                        row = FindRow(newFile);
                    }

                    if (scoreType.CanSet)
                    {
                        var thresholdCell = row.Cells[_colThreshold.Index];
                        thresholdCell.ToolTipText = scoreType.ProbabilityType == BiblioSpecScoreType.EnumProbabilityType.probability_correct
                            ? Resources.BuildLibraryGridView_GridUpdateScoreInfo_Score_threshold_minimum__score_is_probability_that_identification_is_correct__
                            : Resources.BuildLibraryGridView_GridUpdateScoreInfo_Score_threshold_maximum__score_is_probability_that_identification_is_incorrect__;
                        thresholdCell.ReadOnly = false;
                    }
                    InvalidateCell(row.Cells[_colScoreType.Index]);
                    InvalidateCell(row.Cells[_colThreshold.Index]);
                }
            }

            CellValueChanged += OnCellValueChanged;

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
            return Rows.Cast<DataGridViewRow>().FirstOrDefault(row => Equals(file, row.DataBoundItem));
        }

        #region Functional test support

        public void SetScoreThreshold(double threshold)
        {
            SetScoreThreshold(scoreType => threshold);
        }

        public void SetScoreThreshold(Func<BiblioSpecScoreType, double?> thresholdFunc)
        {
            foreach (var file in Files.Where(f => f.ScoreType != null && f.ScoreType.CanSet))
            {
                var threshold = thresholdFunc.Invoke(file.ScoreType);
                if (threshold != null)
                {
                    file.ScoreThreshold = threshold;
                    InvalidateCell(FindRow(file).Cells[_colThreshold.Index]);
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
            foreach (var cell in Rows[e.RowIndex].Cells.Cast<DataGridViewCell>())
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
                if (file.ScoreTypeError)
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
                if (file.ScoreTypeError)
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
            if (e.RowIndex < 0 || e.ColumnIndex != _colThreshold.Index)
                return;

            var file = (File)Rows[e.RowIndex].DataBoundItem;
            var scoreType = file.ScoreType;
            if (scoreType == null)
                return;

            // Copy new threshold to all other files with same score type
            var threshold = file.ScoreThreshold;
            var errorText = threshold.HasValue && scoreType.ValidRange.Min <= threshold && threshold <= scoreType.ValidRange.Max
                ? null
                : string.Format(
                    Resources.BuildLibraryGridView_OnCellValueChanged_Score_threshold___0___is_invalid__must_be_a_decimal_value_between__1__and__2___,
                    threshold.ToString(), scoreType.ValidRange.Min, scoreType.ValidRange.Max);

            foreach (var fileThis in Files.Where(f => Equals(scoreType, f.ScoreType)))
            {
                var cellThis = FindRow(fileThis).Cells[_colThreshold.Index];
                fileThis.ScoreThreshold = threshold;
                cellThis.ErrorText = errorText;
                InvalidateCell(cellThis);
            }
        }

        public class File
        {
            public string FilePath { get; set; }
            [Track]
            public string FileName => Path.GetFileName(FilePath);
            public BiblioSpecScoreType ScoreType { get; set; }
            [Track]
            public string ScoreTypeName => ScoreType?.ToString();
            [Track]
            public double? ScoreThreshold { get; set; }
            public bool ScoreTypeError { get; set; }

            public File(string filePath, BiblioSpecScoreType scoreType, double? scoreThreshold)
            {
                FilePath = filePath;
                ScoreType = scoreType;
                ScoreThreshold = scoreThreshold;
                ScoreTypeError = false;
            }

            public bool Equals(File obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return Equals(obj.FilePath, FilePath) && Equals(obj.ScoreType, ScoreType) && Equals(obj.ScoreThreshold, ScoreThreshold);
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
                    return result;
                }
            }
        }
    }
}
