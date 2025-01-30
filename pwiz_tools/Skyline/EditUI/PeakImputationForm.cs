﻿/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Imputation;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.EditUI
{
    public partial class PeakImputationForm : DataboundGridForm
    {
        private List<Row> _rows;
        private BindingList<Row> _rowsBindingList;
        private Receiver<PeakImputationRows.Parameters, PeakImputationRows> _receiver;
        private SkylineDataSchema _dataSchema;
        private PeakImputationRows _data;
        private SequenceTree _sequenceTree;

        public PeakImputationForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            SkylineWindow = skylineWindow;
            _rows = new List<Row>();
            _rowsBindingList = new BindingList<Row>(_rows);
            _dataSchema = new SkylineWindowDataSchema(skylineWindow);
            var rowSource = BindingListRowSource.Create(_rowsBindingList);
            var rowSourceInfo = new RowSourceInfo(
                typeof(Row),
                rowSource, GetDefaultViewSpecs().Select(viewSpec=>new ViewInfo(_dataSchema, typeof(Row), viewSpec)));
            var viewContext = new SkylineViewContext(_dataSchema, new[] { rowSourceInfo });
            BindingListSource.SetViewContext(viewContext);
            _receiver = PeakImputationRows.PRODUCER.RegisterCustomer(this, ProductAvailableAction);
            _receiver.ProgressChange += ReceiverOnProgressChange;
        }

        private void ReceiverOnProgressChange()
        {
            ProductAvailableAction();
        }

        private void ProductAvailableAction()
        {
            if (_receiver.TryGetCurrentProduct(out var newData))
            {
                if (!Equals(_data, newData))
                {
                    _data = newData;
                    _rows.Clear();
                    _rows.AddRange(_data.MoleculePeaks.Select(peak => new Row(_dataSchema, peak)));
                    _rowsBindingList.ResetBindings();
                    tbxExemplary.Text = _rows.SelectMany(row => row.Peaks.Values)
                        .Count(peak => peak.Verdict == RatedPeak.Verdict.Exemplary).ToString();
                    tbxAccepted.Text = _rows.SelectMany(row => row.Peaks.Values)
                        .Count(peak => peak.Verdict == RatedPeak.Verdict.Accepted).ToString();
                    tbxRejected.Text = _rows.SelectMany(row => row.Peaks.Values)
                        .Count(peak => peak.Verdict == RatedPeak.Verdict.NeedsAdjustment).ToString();
                    tbxNeedsRemoval.Text = _rows.SelectMany(row => row.Peaks.Values)
                        .Count(peak => peak.Verdict == RatedPeak.Verdict.NeedsRemoval).ToString();
                }

                var alignments = _data.AlignmentData.Alignments;

                progressBar1.Visible = false;
                if (alignments != null && AlignAllGraphs)
                {
                    SkylineWindow.RetentionTimeTransformOp =
                        new AlignmentResultsRtTransformOp(comboRtCalculator.SelectedItem.ToString(), alignments);
                }
                else
                {
                    SkylineWindow.RetentionTimeTransformOp = null;
                }
            }
            else
            {
                progressBar1.Visible = true;
                DeepProgress deepProgress = _receiver.GetDeepProgressValue();
                string tooltip = null;
                int progressValue = 0;
                if (deepProgress != null)
                {
                    tooltip = TextUtil.LineSeparate(GetProgressLines(deepProgress, string.Empty));
                    progressValue = (int)Math.Max(0, Math.Min(10000, deepProgress.DeepProgressValue * 100));
                }

                progressBar1.Value = progressValue;
                if (tooltip != toolTip1.GetToolTip(progressBar1))
                {
                    toolTip1.SetToolTip(progressBar1, tooltip);
                }
            }

            var error = _receiver.GetError();
            if (error != null)
            {
                MessageDlg.ShowWithException(this, EditUIResources.PeakImputationForm_ProductAvailableAction_Peak_imputation_error, error);
            }
        }

        public SkylineWindow SkylineWindow { get; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SkylineWindow.DocumentUIChangedEvent += SkylineWindow_OnDocumentUIChangedEvent;
            OnDocumentChanged();
        }

        private void SequenceTreeOnAfterSelect(object sender, TreeViewEventArgs e)
        {
            OnDocumentChanged();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            SkylineWindow.DocumentUIChangedEvent -= SkylineWindow_OnDocumentUIChangedEvent;
            base.OnHandleDestroyed(e);
        }
        private void SkylineWindow_OnDocumentUIChangedEvent(object sender, DocumentChangedEventArgs e)
        {
            OnDocumentChanged();
        }

        private void SetSequenceTree(SequenceTree sequenceTree)
        {
            if (ReferenceEquals(_sequenceTree, sequenceTree))
            {
                return;
            }
            if (null != _sequenceTree)
            {
                _sequenceTree.AfterSelect -= SequenceTreeOnAfterSelect;
            }
            _sequenceTree = sequenceTree;
            if (null != _sequenceTree)
            {
                _sequenceTree.AfterSelect += SequenceTreeOnAfterSelect;
            }
        }


        private bool _inChange;
        private void OnDocumentChanged()
        {
            if (_inChange)
            {
                return;
            }

            try
            {
                _inChange = true;
                UpdateUi();
            }
            finally
            {
                _inChange = false;
            }

        }

        public void UpdateUi()
        {
            SetSequenceTree(SkylineWindow.SequenceTree);
            var document = SkylineWindow.DocumentUI;
            var imputationSettings = document.Settings.PeptideSettings.Imputation;
            var alignmentOptions = RtValueType.GetChoicesWithCalculators(document).ToList();
            ComboHelper.ReplaceItems(comboRtCalculator, alignmentOptions.Cast<object>().Prepend(string.Empty));
            comboRtCalculator.SelectedItem =
                alignmentOptions.FirstOrDefault(option => option.Name == imputationSettings.RtCalcName);
            tbxMaxPeakWidthVariation.Text = imputationSettings.MaxPeakWidthVariation?.ToString() ?? string.Empty;
            tbxRtDeviationCutoff.Text = imputationSettings.MaxRtShift?.ToString() ?? string.Empty;
            var parameters = new PeakImputationRows.Parameters(document)
                .ChangeOverwriteManualPeaks(cbxOverwriteManual.Checked);
            if (!DocumentWide)
            {
                var peptideIdentityPaths =
                    ImmutableList.ValueOf(SkylineWindow.SequenceTree.SelectedPaths.SelectMany(path =>
                        document.EnumeratePathsAtLevel(path, SrmDocument.Level.Molecules)));
                if (peptideIdentityPaths.Count != 0)
                {
                    parameters = parameters.ChangePeptideIdentityPaths(peptideIdentityPaths);
                }
            }
            _receiver?.TryGetProduct(parameters, out _);
            ProductAvailableAction();
        }

        private PeakScoringModelSpec GetScoringModelToUse(SrmDocument document)
        {
            var scoringModel = document.Settings.PeptideSettings.Integration.PeakScoringModel;
            if (true == scoringModel?.IsTrained)
            {
                return scoringModel;
            }
            return LegacyScoringModel.DEFAULT_MODEL;
        }

        public class Row
        {
            public Row(SkylineDataSchema dataSchema, MoleculePeaks moleculePeaks)
            {
                Peptide = new Model.Databinding.Entities.Peptide(dataSchema, moleculePeaks.PeptideIdentityPath);
                Peaks = new Dictionary<ResultKey, Peak>();
                foreach (var scoredPeak in moleculePeaks.Peaks)
                {
                    var replicate = dataSchema.ReplicateList.Values.ElementAtOrDefault(scoredPeak.ReplicateFileInfo.ReplicateIndex);
                    if (replicate != null)
                    {
                        var resultKey = new ResultKey(replicate,
                            replicate.ChromatogramSet.IndexOfId(scoredPeak.ReplicateFileInfo.ReplicateFileId.FileId));
                        var peak = new Peak(Peptide, scoredPeak);
                        Peaks[resultKey] = peak;
                        if (moleculePeaks.BestPeak != null &&
                            Equals(moleculePeaks.BestPeak.ReplicateFileInfo.ReplicateFileId,
                                scoredPeak.ReplicateFileInfo.ReplicateFileId))
                        {
                            BestPeak = peak;
                        }
                    }
                }

                ExemplaryPeakBounds = moleculePeaks.ExemplaryPeakBounds;
                CountExemplary = Peaks.Values.Count(peak => peak.Verdict == RatedPeak.Verdict.Exemplary);
                CountAccepted = Peaks.Values.Count(peak => peak.Verdict == RatedPeak.Verdict.Accepted);
                CountNeedAdjustment = Peaks.Values.Count(peak => peak.Verdict == RatedPeak.Verdict.NeedsAdjustment);
                AlignmentStandardTime = moleculePeaks.AlignmentStandardTime;
            }

            public Model.Databinding.Entities.Peptide Peptide { get; }
            public Dictionary<ResultKey, Peak> Peaks { get; }
            [InvariantDisplayName("BestPeak")]
            public Peak BestPeak { get; }
            [ChildDisplayName("Exemplary{0}")]
            public FormattablePeakBounds ExemplaryPeakBounds { get; }
            public int CountExemplary { get; }
            public int CountAccepted { get; }
            public int CountNeedAdjustment { get; }
            [Format(Formats.RETENTION_TIME)]
            public double? AlignmentStandardTime { get; private set; }
        }

        [InvariantDisplayName("Peak")]

        public class Peak : IFormattable, ILinkValue
        {
            private Model.Databinding.Entities.Peptide _peptide;
            private RatedPeak _ratedPeak;
            public Peak(Model.Databinding.Entities.Peptide peptide, RatedPeak peak)
            {
                _peptide = peptide;
                var dataSchema = peptide.DataSchema;
                if (dataSchema.ResultFileList.TryGetValue(new ResultFileKey(peak.ReplicateFileInfo.ReplicateIndex,
                        peak.ReplicateFileInfo.ReplicateFileId.FileId, 0), out var resultFile))
                {
                    ResultFile = resultFile;
                }

                _ratedPeak = peak;
            }

            public ResultFile ResultFile { get; }

            [ChildDisplayName("Raw{0}")]
            public FormattablePeakBounds RawBounds
            {
                get { return _ratedPeak.RawPeakBounds; }
            }

            [ChildDisplayName("Aligned{0}")]
            public FormattablePeakBounds AlignedBounds
            {
                get { return _ratedPeak.AlignedPeakBounds; }
            }

            public bool ManuallyIntegrated
            {
                get
                {
                    return _ratedPeak.ManuallyIntegrated;
                }
            }

            public RatedPeak.Verdict Verdict
            {
                get
                {
                    return _ratedPeak.PeakVerdict;
                }
            }

            public string Opinion
            {
                get { return _ratedPeak.Opinion; }
            }

            [Format(Formats.PEAK_SCORE)]
            public double? Score
            {
                get { return _ratedPeak.Score; }
            }

            public double? ShiftFromBestPeak
            {
                get
                {
                    return _ratedPeak.RtShift;
                }
            }

            public override string ToString()
            {
                return TextUtil.ColonSeparate(ResultFile?.Replicate.ToString(), _ratedPeak.AlignedPeakBounds?.ToString());
            }

            public string ToString(string format, IFormatProvider formatProvider)
            {
                return TextUtil.ColonSeparate(ResultFile.Replicate.ToString(),
                    _ratedPeak.AlignedPeakBounds?.ToString(format, formatProvider));
            }

            public RatedPeak GetRatedPeak()
            {
                return _ratedPeak;
            }

            EventHandler ILinkValue.ClickEventHandler
            {
                get
                {
                    return LinkValueOnClick;
                }
            }

            object ILinkValue.Value
            {
                get { return this; }
            }

            public void LinkValueOnClick(object sender, EventArgs args)
            {
                _peptide.LinkValueOnClick(sender, args);
                (ResultFile.Replicate as ILinkValue)?.ClickEventHandler(sender, args);
            }

            [DataGridViewColumnType(typeof(ImputeButtonColumn))]
            public string Action
            {
                get 
                {
                    switch (Verdict)
                    {
                        case RatedPeak.Verdict.NeedsAdjustment:
                            return EditUIResources.Peak_Action_Adjust_Peak;
                        case RatedPeak.Verdict.NeedsRemoval:
                            return EditUIResources.Peak_Action_Remove_Peak;
                        default:
                            return string.Empty;
                    }
                }
            }
        }

        private void SettingsControlChanged(object sender, EventArgs e)
        {
            if (_inChange)
            {
                return;
            }
            var newImputationSettings = GetImputationSettings();
            var document = SkylineWindow.DocumentUI;
            if (Equals(newImputationSettings, document.Settings.PeptideSettings.Imputation))
            {
                OnDocumentChanged();
            }
            else
            {
                SkylineWindow.ModifyDocument(EditUIResources.PeakImputationForm_SettingsControlChanged_Change_imputation_settings,
                    doc => doc.ChangeSettings(
                        doc.Settings.ChangePeptideSettings(
                            doc.Settings.PeptideSettings.ChangeImputation(newImputationSettings))),
                    AuditLogEntry.SettingsLogFunction);
            }
        }

        private ImputationSettings GetImputationSettings()
        {
            var imputationSettings = ImputationSettings.DEFAULT.ChangeRtCalcName((comboRtCalculator.SelectedItem as RtValueType)?.Name)
                .ChangeMaxRtShift(GetDoubleValue(tbxRtDeviationCutoff, 0, null))
                .ChangeMaxPeakWidthVariation(GetDoubleValue(tbxMaxPeakWidthVariation, 0, null) / 100);
            if (imputationSettings.RtCalcName != null)
            {
                imputationSettings = imputationSettings.ChangeRegressionMethodName(AlignmentType.KDE.Name);
            }

            return imputationSettings;
        }

        private double? GetDoubleValue(TextBox textBox, double? min, double? max)
        {
            var text = textBox.Text.Trim();
            double? value = null;
            if (!string.IsNullOrEmpty(text))
            {
                if (!double.TryParse(text, out var doubleValue) || double.IsNaN(doubleValue))
                {
                    SetError(textBox, EditUIResources.PeakImputationForm_GetDoubleValue_Error__Must_be_a_number);
                    return null;
                }

                if (min.HasValue && max.HasValue)
                {
                    if (min > doubleValue || max < doubleValue)
                    {
                        SetError(textBox, string.Format(EditUIResources.PeakImputationForm_GetDoubleValue_Error__must_be_between__0__and__1_, min, max));
                        return null;
                    }
                }

                if (min > doubleValue)
                {
                    SetError(textBox, string.Format(EditUIResources.PeakImputationForm_GetDoubleValue_Error__Must_not_be_below__0_, min));
                    return null;
                }

                if (max < doubleValue)
                {
                    SetError(textBox, string.Format(EditUIResources.PeakImputationForm_GetDoubleValue_Error__Must_not_be_above__0_, max));
                    return null;
                }

                value = doubleValue;
            }
            textBox.BackColor = Color.White;
            return value;
        }

        private Dictionary<Control, string> _originalTooltips = new Dictionary<Control, string>();

        private void SetError(TextBox textBox, string message)
        {
            if (!_originalTooltips.TryGetValue(textBox, out var originalTooltip))
            {
                originalTooltip = toolTip1.GetToolTip(textBox);
                _originalTooltips.Add(textBox, originalTooltip);
            }

            if (message == null)
            {
                textBox.BackColor = Color.White;
                toolTip1.SetToolTip(textBox, originalTooltip);
            }
            else
            {
                textBox.BackColor = Color.Red;
                var newTooltip = message;
                if (!string.IsNullOrEmpty(originalTooltip))
                {
                    newTooltip = TextUtil.LineSeparate(newTooltip, string.Empty, originalTooltip);
                }
                toolTip1.SetToolTip(textBox, newTooltip);
            }
        }

        private void btnImputeBoundaries_Click(object sender, EventArgs e)
        {
            ImputeBoundariesForAllRows();
        }

        public void ImputeBoundariesForAllRows()
        {
            var rows = BindingListSource.OfType<RowItem>().Select(rowItem => rowItem.Value).OfType<Row>().ToList();
            if (rows.Count == 0)
            {
                MessageDlg.Show(this, EditUIResources.PeakImputationForm_ImputeBoundariesForAllRows_There_are_no_rows);
                return;
            }
            ImputeBoundaries(rows);
        }

        public int ImputeBoundaries(IList<Row> rows)
        {
            return ImputeBoundaries(rows, null);
        }

        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        public int ImputeBoundaries(IList<Row> rows, ICollection<ReplicateFileId> replicateFileIds)
        {
            var alignmentData = _data.AlignmentData;
            lock (SkylineWindow.GetDocumentChangeLock())
            {
                var originalDocument = SkylineWindow.DocumentUI;
                using var longWaitDlg = new LongWaitDlg();
                int changeCount = 0;
                SrmDocument newDoc = originalDocument.BeginDeferSettingsChanges();
                longWaitDlg.PerformWork(this, 1000, broker =>
                {
                    newDoc =
                        ImputeBoundariesOnDocument(broker, newDoc, alignmentData, rows, replicateFileIds, out changeCount);
                });
                if (newDoc == null || longWaitDlg.IsCanceled || changeCount == 0)
                {
                    return 0;
                }

                newDoc = newDoc.EndDeferSettingsChanges(originalDocument, null);
                SkylineWindow.ModifyDocument(EditUIResources.PeakImputationForm_ImputeBoundaries_Impute_peak_boundaries, doc =>
                {
                    if (!ReferenceEquals(doc, originalDocument))
                    {
                        throw new InvalidOperationException(Resources.SkylineDataSchema_VerifyDocumentCurrent_The_document_was_modified_in_the_middle_of_the_operation_);
                    }

                    return newDoc;
                }, docPair => AuditLogEntry.CreateSimpleEntry(MessageType.applied_peak_all, docPair.NewDocumentType, MessageArgs.Create(changeCount)));
                return changeCount;
            }
        }

        private SrmDocument ImputeBoundariesOnDocument(ILongWaitBroker broker, SrmDocument newDoc, AlignmentData alignmentData, IList<Row> rows, ICollection<ReplicateFileId> replicateFileIds,
            out int changeCount)
        {
            var scoringModel = GetScoringModelToUse(newDoc);
            changeCount = 0;
            for (int iRow = 0; iRow < rows.Count; iRow++)
            {
                if (broker.IsCanceled)
                {
                    return null;
                }

                broker.ProgressValue = 100 * iRow / rows.Count;
                var row = rows[iRow];
                var exemplaryBounds = row.ExemplaryPeakBounds;
                if (exemplaryBounds == null)
                {
                    continue;
                }

                var rejectedPeaks = row.Peaks.Values
                    .Where(peak => peak.Verdict == RatedPeak.Verdict.NeedsAdjustment || peak.Verdict == RatedPeak.Verdict.NeedsRemoval)
                    .Select(peak => peak.GetRatedPeak()).ToList();
                foreach (var rejectedPeak in rejectedPeaks)
                {
                    if (false == replicateFileIds?.Contains(rejectedPeak.ReplicateFileInfo.ReplicateFileId))
                    {
                        continue;
                    }
                    var peakImputer = new PeakImputer(newDoc, alignmentData.ChromatogramTimeRanges, row.Peptide.IdentityPath, scoringModel,
                        rejectedPeak.ReplicateFileInfo);
                    var bestPeakBounds =
                        exemplaryBounds.ReverseAlignPreservingWidth(rejectedPeak.AlignmentFunction);
                    if (bestPeakBounds == null)
                    {
                        continue;
                    }

                    newDoc = peakImputer.ImputeBoundaries(newDoc, bestPeakBounds);
                    changeCount++;
                }
            }

            return newDoc;
        }

        public bool DocumentWide
        {
            get
            {
                return radioScopeDocument.Checked;
            }
            set
            {
                radioScopeDocument.Checked = value;
                radioScopeSelection.Checked = !value;
            }
        }

        private IEnumerable<ViewSpec> GetDefaultViewSpecs()
        {
            yield return new ViewSpec().SetRowType(typeof(Row)).SetColumns(new []
            {
                nameof(Row.Peptide),
                nameof(Row.BestPeak),
                nameof(Row.CountExemplary),
                nameof(Row.CountAccepted),
                nameof(Row.CountNeedAdjustment),
            }.Select(name=>new ColumnSpec(PropertyPath.Root.Property(name)))).SetName(EditUIResources.PeakImputationForm_GetDefaultViewSpecs_Default);

            var ppPeaks = PropertyPath.Root.Property(nameof(Row.Peaks)).DictionaryValues();
            var propertyPaths = new List<PropertyPath>
            {
                PropertyPath.Root.Property(nameof(Row.Peptide)),
                ppPeaks,
                ppPeaks.Property(nameof(Peak.Score))
            };

            propertyPaths.Add(ppPeaks.Property(nameof(Peak.Verdict)));
            propertyPaths.Add(ppPeaks.Property(nameof(Peak.Opinion)));
            propertyPaths.Add(ppPeaks.Property(nameof(Peak.Action)));
            yield return new ViewSpec().SetRowType(typeof(Row))
                .SetColumns(propertyPaths.Select(pp => new ColumnSpec(pp)))
                .SetSublistId(ppPeaks).SetName(EditUIResources.PeakImputationForm_GetDefaultViewSpecs_Details);
        }

        public class ImputeButtonCell : DataGridViewButtonCell
        {
            protected override void OnClick(DataGridViewCellEventArgs e)
            {
                var peakImputationForm = DataGridView?.FindForm() as PeakImputationForm;
                if (peakImputationForm == null)
                {
                    return;
                }

                var bindingListSource = DataGridView.DataSource as BindingListSource;
                if (bindingListSource == null)
                {
                    return;
                }

                var rowItem = bindingListSource[RowIndex] as RowItem;
                if (rowItem == null)
                {
                    return;
                }

                var row = rowItem.Value as Row;
                if (row == null)
                {
                    return;
                }

                var replicateFileId = GetReplicateFileId(rowItem);
                if (replicateFileId == null)
                {
                    return;
                }

                peakImputationForm.ImputeBoundaries(new[] { row }, new[] { replicateFileId });
            }

            private ReplicateFileId GetReplicateFileId(RowItem rowItem)
            {
                var columnPropertyDescriptor = GetColumnPropertyDescriptor();
                if (columnPropertyDescriptor == null)
                {
                    return null;
                }

                var parentColumnDescriptor = columnPropertyDescriptor.DisplayColumn.ColumnDescriptor?.Parent;
                if (parentColumnDescriptor == null)
                {
                    return null;
                }

                var value = parentColumnDescriptor.GetPropertyValue(rowItem, columnPropertyDescriptor.PivotKey);
                if (value == null)
                {
                    return null;
                }

                var peak = value as Peak;
                return peak?.GetRatedPeak().ReplicateFileInfo.ReplicateFileId;
            }

            private ColumnPropertyDescriptor GetColumnPropertyDescriptor()
            {
                var dataPropertyName = DataGridView.Columns[ColumnIndex].DataPropertyName;
                if (dataPropertyName == null)
                {
                    return null;
                }
                var bindingListSource = DataGridView.DataSource as BindingListSource;
                if (bindingListSource == null)
                {
                    return null;
                }
                return bindingListSource.ItemProperties.FindByName(dataPropertyName) as ColumnPropertyDescriptor;
            }
        }

        public sealed class ImputeButtonColumn : DataGridViewButtonColumn
        {
            public ImputeButtonColumn()
            {
                CellTemplate = new ImputeButtonCell();
            }
        }

        private IEnumerable<string> GetProgressLines(DeepProgress deepProgress, string indent)
        {
            string childIndent = indent + @"  ";
            IEnumerable<string> lines =
                deepProgress.InputProgress.SelectMany(child => GetProgressLines(child, childIndent));
            if (string.IsNullOrEmpty(deepProgress.Description))
            {
                return lines;
            }
            
            if (deepProgress.ProgressValue == 0)
            {
                return lines.Prepend(TextUtil.AppendColon(deepProgress.Description));
            }
            return lines.Prepend(TextUtil.ColonSeparate(deepProgress.Description,
                (deepProgress.ProgressValue / 100.0).ToString(Formats.Percent)));
        }

        private void linkLabelViewRegression_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            DisplayRetentionTimeRegression();
        }

        public bool AlignAllGraphs
        {
            get
            {
                return cbxAlignAllGraphs.Checked;
            }
            set
            {
                cbxAlignAllGraphs.Checked = value;
            }
        }

        public string RtCalculatorName
        {
            get
            {
                return (comboRtCalculator.SelectedItem as RtValueType)?.Name;
            }
            set
            {
                for (int i = 0; i < comboRtCalculator.Items.Count; i++)
                {
                    var rtValueType = comboRtCalculator.Items[i] as RtValueType;
                    if (value == rtValueType?.Name)
                    {
                        comboRtCalculator.SelectedIndex = i;
                        return;
                    }
                }
                throw new InvalidOperationException();
            }
        }

        public void DisplayRetentionTimeRegression()
        {
            var rtValueType = comboRtCalculator.SelectedItem as RtValueType;
            if (rtValueType == null)
            {
                MessageDlg.Show(this, EditUIResources.PeakImputationForm_DisplayRetentionTimeRegression_No_retention_time_calculator_selected);
                return;
            }

            SkylineWindow.ShowRTRegressionGraphScoreToRun();
            SkylineWindow.ShowRegressionMethod(RegressionMethodRT.kde);
            SkylineWindow.ChooseCalculator(rtValueType.Name);

        }

        private class AlignmentResultsRtTransformOp : GraphValues.IRetentionTimeTransformOp
        {
            private AlignmentResults _alignmentResults;
            public AlignmentResultsRtTransformOp(string name, AlignmentResults alignmentResults)
            {
                Name = name;
                _alignmentResults = alignmentResults;
            }

            public string GetAxisTitle(RTPeptideValue rtPeptideValue)
            {
                return string.Format(GraphsResources.RtAlignment_AxisTitleAlignedTo,
                    GraphValues.ToLocalizedString(rtPeptideValue), Name);
            }

            public string Name { get; }


            public bool TryGetRegressionFunction(ChromFileInfoId chromFileInfoId, out AlignmentFunction regressionFunction)
            {
                return _alignmentResults.TryGetRegressionFunction(chromFileInfoId, out regressionFunction);
            }
        }
    }
}
