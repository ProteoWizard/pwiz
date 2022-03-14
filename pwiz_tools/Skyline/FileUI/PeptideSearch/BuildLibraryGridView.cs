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
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.BiblioSpec;
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
        private const string CELL_LOADING = @"...";

        private static Color ReadonlyCellBackColor => Color.FromArgb(245, 245, 245);
        private readonly Color _defaultCellBackColor;

        private readonly DataGridViewColumn _colFile = new DataGridViewTextBoxColumn
        {
            HeaderText = Resources.BuildLibraryGridView__colFile_File,
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };

        private readonly DataGridViewColumn _colScoreType = new DataGridViewTextBoxColumn
        {
            HeaderText = Resources.BuildLibraryGridView__colScoreType_Score_Type,
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells
        };

        private readonly DataGridViewColumn _colThreshold = new DataGridViewTextBoxColumn
        {
            HeaderText = Resources.BuildLibraryGridView__colThreshold_Score_Threshold,
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells,
            CellTemplate = new ThresholdCell()
        };

        public BuildLibraryGridView()
        {
            _defaultCellBackColor = DefaultCellStyle.BackColor;

            if (LicenseManager.UsageMode == LicenseUsageMode.Runtime)
            {
                AllowUserToAddRows = false;
                AllowUserToDeleteRows = true;
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

                Columns.Clear();
                AddColumns(new[] { _colFile, _colScoreType, _colThreshold });

                RowPrePaint += OnRowPrePaint;
                CellValueChanged += OnCellValueChanged;
                UserDeletedRow += (sender, e) =>
                {
                    var files = Files.ToList();
                    files.Remove(FileCellValue.Get(e.Row, _colFile).File);
                    Files = files.ToArray();
                };

                IsFileOnly = false;
                _files = Array.Empty<string>();
            }
        }

        public event EventHandler FilesChanged;

        private bool _isFileOnly;
        public bool IsFileOnly
        {
            get => _isFileOnly;
            set
            {
                _colScoreType.Visible = _colThreshold.Visible = !value;
                _isFileOnly = value;
            }
        }

        private string[] _files;
        public string[] Files
        {
            get => _files ?? Array.Empty<string>();
            set
            {
                var toAdd = (value ?? Array.Empty<string>()).ToHashSet();
                if (Files.ToHashSet().SetEquals(toAdd))
                    return;

                _files = value;

                CellValueChanged -= OnCellValueChanged;

                var existingFiles = new HashSet<string>();
                for (var i = RowCount - 1; i >= 0; i--)
                {
                    var existingFile = FileCellValue.Get(Rows[i], _colFile).File;
                    if (!toAdd.Contains(existingFile))
                        Rows.RemoveAt(i);
                    else
                        existingFiles.Add(existingFile);
                }
                toAdd.ExceptWith(existingFiles);

                foreach (var file in toAdd)
                    AddFile(null, file, null);

                if (SortedColumn == null || SortOrder == SortOrder.None)
                    Sort(_colFile, ListSortDirection.Ascending);
                else
                    Sort(SortedColumn, SortOrder == SortOrder.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending);

                CellValueChanged += OnCellValueChanged;

                FilesChanged?.Invoke(this, null);

                if (!toAdd.Any())
                    return;

                if (!IsFileOnly)
                {
                    var bw = new BackgroundWorker();
                    bw.DoWork += (sender, e) =>
                    {
                        bool success;
                        Dictionary<string, BiblioSpecScoreType[]> scoreTypes;
                        Exception getScoreTypesException = null;
                        try
                        {
                            success = GetScoreTypes(toAdd, out scoreTypes);
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

        public MsDataFileUri[] FilesUris
        {
            get => Files.Select(f => new MsDataFilePath(f)).Cast<MsDataFileUri>().ToArray();
            set => Files = value.Select(uri => uri.GetFilePath() + (uri.GetSampleIndex() > 0 ? $@":{uri.GetSampleIndex()}" : string.Empty)).ToArray();
        }

        public IEnumerable<string> SelectedFiles => SelectedCells.Cast<DataGridViewCell>()
            .Where(cell => cell.ColumnIndex == _colFile.Index)
            .Select(cell => cell.OwningRow).Distinct()
            .Select(row => FileCellValue.Get(row, _colFile))
            .Where(value => value != null)
            .Select(value => value.File);

        public BiblioSpecScoreType[] ScoreTypes
        {
            get
            {
                if (IsFileOnly)
                    return Array.Empty<BiblioSpecScoreType>();
                var scoreTypes = new BiblioSpecScoreType[RowCount];
                for (var i = 0; i < RowCount; i++)
                    scoreTypes[i] = ScoreTypeCellValue.Get(Rows[i], _colScoreType)?.ScoreType;
                return scoreTypes;
            }

            set
            {
                if (value.Length != RowCount)
                    throw new Exception(Resources.BuildLibraryGridView_ScoreTypes_Number_of_score_types_is_not_equal_to_number_of_rows_in_grid_);
                for (var i = 0; i < RowCount; i++)
                    ScoreTypeCellValue.Get(Rows[i], _colScoreType).ScoreType = value[i];
            }
        }

        public double?[] ScoreThresholds
        {
            get
            {
                if (IsFileOnly)
                    return Array.Empty<double?>();
                var thresholds = new double?[RowCount];
                for (var i = 0; i < RowCount; i++)
                    thresholds[i] = ThresholdCell.Get(Rows[i], _colThreshold).Threshold;
                return thresholds;
            }

            set
            {
                if (value.Length != RowCount)
                    throw new Exception(Resources.BuildLibraryGridView_ScoreThresholds_Number_of_score_thresholds_is_not_equal_to_number_of_rows_in_grid_);
                for (var i = 0; i < RowCount; i++)
                    ThresholdCell.Get(Rows[i], _colThreshold).Threshold = value[i];
            }
        }

        public bool ScoreTypesLoaded => ScoreTypes.All(scoreType => scoreType != null);

        public bool IsReady => RowCount > 0 &&
                               (IsFileOnly || Rows.Cast<DataGridViewRow>().All(row =>
                               {
                                   var scoreType = ScoreTypeCellValue.Get(row, _colScoreType)?.ToString();
                                   var threshold = ThresholdCell.Get(row, _colThreshold)?.ToString();
                                   return scoreType != null && !Equals(scoreType, CELL_LOADING) &&
                                          threshold != null && !Equals(threshold, CELL_LOADING);
                               }));

        public bool Validate(IWin32Window parent, CancelEventArgs e, bool showWarnings, out Dictionary<string, double> thresholdsByFile)
        {
            if (!ScoreTypesLoaded)
                throw new Exception(Resources.BuildLibraryGridView_GetThresholds_Score_types_not_loaded_);

            var thresholdsByScoreType = new Dictionary<BiblioSpecScoreType, double>();
            thresholdsByFile = new Dictionary<string, double>();

            var errors = new List<string>();
            foreach (DataGridViewRow row in Rows)
            {
                var file = FileCellValue.Get(row, _colFile);
                var scoreType = ScoreTypeCellValue.Get(row, _colScoreType);
                var thresholdCell = ThresholdCell.Get(row, _colThreshold);
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
                    errors.Add($@"{file.File}: {error}");
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
                            threshold, scoreType.DisplayName);
                    }
                    else if ((probCorrect && thresholdIsMin) || (probIncorrect && thresholdIsMax))
                    {
                        warning = string.Format(
                            Resources.BuildLibraryGridView_GetThresholds_Score_threshold__0__for__1__will_include_all_identifications_,
                            threshold, scoreType.DisplayName);
                    }
                    else if (threshold < scoreType.SuggestedRange.Min || threshold > scoreType.SuggestedRange.Max)
                    {
                        warning = string.Format(
                            Resources.BuildLibraryGridView_GetThresholds_Score_threshold__0__for__1__is_unusually_permissive_,
                            threshold, scoreType.DisplayName);
                    }

                    if (!string.IsNullOrEmpty(warning))
                    {
                        if (probCorrect)
                            warning = TextUtil.SpaceSeparate(warning, string.Format(
                                Resources.BuildLibraryGridView_GetThresholds__0__scores_indicate_the_probability_that_an_identification_is__1__,
                                scoreType.DisplayName, Resources.BuildLibraryGridView_GetThresholds_correct));
                        else if (probIncorrect)
                            warning = TextUtil.SpaceSeparate(warning, string.Format(
                                Resources.BuildLibraryGridView_GetThresholds__0__scores_indicate_the_probability_that_an_identification_is__1__,
                                scoreType.DisplayName, Resources.BuildLibraryGridView_GetThresholds_incorrect));
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

        private DataGridViewRow AddFile(int? insertPos, string file, BiblioSpecScoreType scoreType)
        {
            var i = insertPos ?? RowCount;
            Rows.Insert(i);
            this[_colFile.Index, i].Value = new FileCellValue(this, _colFile, file);
            this[_colScoreType.Index, i].Value = new ScoreTypeCellValue(scoreType);
            var thresholdCell = new ThresholdCell();
            this[_colThreshold.Index, i] = thresholdCell;
            thresholdCell.ReadOnly = true;
            return Rows[i];
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
            foreach (DataGridViewRow row in Rows)
            {
                if (scoreTypes == null || !scoreTypes.ContainsKey(FileCellValue.Get(row, _colFile).File))
                {
                    var scoreType = ScoreTypeCellValue.Get(row, _colScoreType).NameInvariant;
                    if (scoreType != null)
                        existingThresholds[scoreType] = ThresholdCell.Get(row, _colThreshold).Threshold;
                }
            }

            for (var i = 0; i < RowCount; i++)
            {
                var row = Rows[i];

                if (!success || exception != null || scoreTypes == null)
                {
                    var errorSb = new StringBuilder(Resources.BuildLibraryGridView_GridUpdateScoreInfo_Error_getting_score_type_for_this_file_);
                    if (exception != null)
                    {
                        errorSb.AppendLine();
                        errorSb.Append(exception.Message);
                    }
                    row.ErrorText = errorSb.ToString();
                    row.Cells[_colScoreType.Index].Value = null;
                    ThresholdCell.Get(row, _colThreshold).Value = null;
                    continue;
                }

                var file = FileCellValue.Get(row, _colFile).File;

                if (!scoreTypes.TryGetValue(file, out var scoreTypesThis))
                    continue;

                for (var j = 0; j < scoreTypesThis.Length; j++)
                {
                    var scoreType = scoreTypesThis[j];
                    if (j == 0)
                    {
                        row.Cells[_colScoreType.Index].Value = new ScoreTypeCellValue(scoreType);
                    }
                    else
                    {
                        row = AddFile(++i, file, scoreType);
                    }
                    var thresholdCell = ThresholdCell.Get(row, _colThreshold);
                    if (scoreType != null)
                    {
                        if (scoreType.CanSet)
                        {
                            thresholdCell.Value = existingThresholds.TryGetValue(scoreType.NameInvariant, out var threshold)
                                ? threshold
                                : BiblioSpecLiteBuilder.GetDefaultScoreThreshold(scoreType.NameInvariant, scoreType.DefaultValue);
                            thresholdCell.ToolTipText = scoreType.ProbabilityType == BiblioSpecScoreType.EnumProbabilityType.probability_correct
                                ? Resources.BuildLibraryGridView_GridUpdateScoreInfo_Score_threshold_minimum__score_is_probability_that_identification_is_correct__
                                : Resources.BuildLibraryGridView_GridUpdateScoreInfo_Score_threshold_maximum__score_is_probability_that_identification_is_incorrect__;
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

            CellValueChanged += OnCellValueChanged;

            FilesChanged?.Invoke(this, null);
        }

        private void OnRowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            foreach (var cell in Rows[e.RowIndex].Cells.Cast<DataGridViewCell>())
                cell.Style.BackColor = cell.ReadOnly ? ReadonlyCellBackColor : _defaultCellBackColor;
        }

        private void OnCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != _colThreshold.Index)
                return;

            var scoreType = ScoreTypeCellValue.Get(e.RowIndex, _colScoreType)?.ScoreType;
            if (scoreType == null)
                return;

            // Copy new threshold to all other files with same score type
            var thresholdCell = ThresholdCell.Get(Rows[e.RowIndex], _colThreshold);
            var threshold = thresholdCell.Threshold;
            var errorText = threshold.HasValue && scoreType.ValidRange.Min <= threshold && threshold <= scoreType.ValidRange.Max
                ? null
                : string.Format(
                    Resources.BuildLibraryGridView_OnCellValueChanged_Score_threshold___0___is_invalid__must_be_a_decimal_value_between__1__and__2___,
                    thresholdCell.Value, scoreType.ValidRange.Min, scoreType.ValidRange.Max);

            foreach (var row in Rows.Cast<DataGridViewRow>().Where(row => Equals(ScoreTypeCellValue.Get(row, _colScoreType)?.ScoreType, scoreType)))
            {
                var thisCell = ThresholdCell.Get(row, _colThreshold);
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
