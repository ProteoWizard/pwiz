using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MathNet.Numerics.Statistics;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Imputation;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.EditUI
{
    public partial class PeakImputationForm : DataboundGridForm
    {
        private List<Row> _rows;
        private BindingList<Row> _rowsBindingList;
        private Receiver<PeakImputationData.Parameters, PeakImputationData> _receiver;
        private SkylineDataSchema _dataSchema;
        private PeakImputationData _data;
        private ScoreConversionData _scoreConversionData = ScoreConversionData.EMPTY;
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
            _receiver = PeakImputationData.PRODUCER.RegisterCustomer(this, ProductAvailableAction);
            _receiver.ProgressChange += ReceiverOnProgressChange;
            var meanStandardDeviation = PeakImputationData.GetMeanRtStandardDeviation(SkylineWindow.Document, null);
            if (meanStandardDeviation.HasValue)
            {
                string format = meanStandardDeviation > 0.05 ? Formats.RETENTION_TIME : Formats.RoundTrip;
                tbxRtDeviationCutoff.Text = meanStandardDeviation.Value.ToString(format);
                tbxUnalignedDocRtStdDev.Text = meanStandardDeviation.Value.ToString(Formats.RETENTION_TIME);
            }
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
                    _scoreConversionData = _data.ScoreConversionData;
                    _rows.Clear();
                    _rows.AddRange(_data.MoleculePeaks.Select(peak => new Row(_dataSchema, peak)));
                    _rowsBindingList.ResetBindings();
                    tbxExemplary.Text = _rows.SelectMany(row => row.Peaks.Values)
                        .Count(peak => peak.Verdict == RatedPeak.Verdict.Exemplary).ToString();
                    tbxAccepted.Text = _rows.SelectMany(row => row.Peaks.Values).Count(peak => peak.Verdict == RatedPeak.Verdict.Accepted).ToString();
                    tbxRejected.Text = _rows.SelectMany(row => row.Peaks.Values).Count(peak => peak.Verdict == RatedPeak.Verdict.NeedsAdjustment).ToString();
                    var rtShifts = _rows.SelectMany(row => row.Peaks.Values)
                        .Where(peak => peak.ShiftFromBestPeak.HasValue)
                        .Select(peak => Math.Abs(peak.ShiftFromBestPeak.Value)).ToList();
                    if (rtShifts.Count == 0)
                    {
                        tbxMeanRtStdDev.Text = "";
                    }
                    else
                    {
                        tbxMeanRtStdDev.Text = rtShifts.Mean().ToString(Formats.RETENTION_TIME);
                    }
                }

                var document = _data.Params.Document;
                tbxUnalignedDocRtStdDev.Text =
                    PeakImputationData.GetMeanRtStandardDeviation(document, null)
                        ?.ToString(Formats.RETENTION_TIME) ?? string.Empty;
                tbxAlignedDocRtStdDev.Text =
                    PeakImputationData.GetMeanRtStandardDeviation(document, _data.Alignments)
                        ?.ToString(Formats.RETENTION_TIME) ?? string.Empty;
                tbxAvgPeakWidthCV.Text = PeakImputationData.GetAveragePeakWidthCV(document)?.ToString(Formats.CV) ??
                                         string.Empty;

                progressBar1.Visible = false;
                if (_data.Alignments != null && cbxAlignAllGraphs.Checked)
                {
                    SkylineWindow.RetentionTimeTransformOp =
                        _data.Alignments.ChangeName(comboRetentionTimeAlignment.SelectedItem.ToString());
                }
                else
                {
                    SkylineWindow.RetentionTimeTransformOp = null;
                }
            }
            else
            {
                progressBar1.Visible = true;
                progressBar1.Value = (int) (_receiver.GetDeepProgressValue() * 100);
            }

            var error = _receiver.GetError();
            if (error != null)
            {
                Trace.TraceWarning("Peak imputation error: {0}", error);
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
            ComboHelper.ReplaceItems(comboRetentionTimeAlignment, GetAlignmentOptions(document), 1);
            var scoringModel = GetScoringModelToUse(document);
            tbxScoringModel.Text = scoringModel.Name;

            radioPValue.Enabled = !Equals(scoringModel, LegacyScoringModel.DEFAULT_MODEL);
            radioQValue.Enabled = HasQValues(document);
            radioPercentile.Enabled = DocumentWide;
            var alignmentOption = comboRetentionTimeAlignment.SelectedItem as AlignmentOption;
            var parameters = new PeakImputationData.Parameters(document)
                .ChangeRtValueType(alignmentOption?.RtValueType)
                .ChangeAlignmentType(alignmentOption?.AlignmentType)
                .ChangeOverwriteManualPeaks(cbxOverwriteManual.Checked)
                .ChangeScoringModel(scoringModel)
                .ChangeAllowableRtShift(GetDoubleValue(tbxRtDeviationCutoff, 0, null))
                .ChangeMaxPeakWidthVariation(GetDoubleValue(tbxMaxPeakWidthVariation, 0, null) / 100);
            double? scoreCutoff;
            if (CutoffType == CutoffScoreType.RAW)
            {
                scoreCutoff = GetDoubleValue(tbxCoreScoreCutoff, null, null);
            }
            else
            {
                scoreCutoff = GetDoubleValue(tbxCoreScoreCutoff, 0, 100);
                if (CutoffType == CutoffScoreType.PERCENTILE)
                {
                    scoreCutoff /= 100;
                }
            }

            lblCutoffPercent.Visible = CutoffType == CutoffScoreType.PERCENTILE;

            parameters = parameters.ChangeCutoffScore(CutoffType, scoreCutoff);
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

        private bool HasQValues(SrmDocument document)
        {
            var libraries = document.Settings.PeptideSettings.Libraries;
            if (!libraries.LibrarySpecs.Any(spec => spec.UseExplicitPeakBounds))
            {
                return false;
            }

            if (!libraries.IsLoaded)
            {
                // If the libraries haven't been loaded yet, assume they have q-values
                return true;
            }

            return libraries.Libraries.Any(lib =>
                lib is { UseExplicitPeakBounds: true, HasExplicitBoundsQValues: true });
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
            public RatedPeak.PeakBounds ExemplaryPeakBounds { get; }
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
            public RatedPeak.PeakBounds RawBounds
            {
                get { return _ratedPeak.RawPeakBounds; }
            }

            [ChildDisplayName("Aligned{0}")]
            public RatedPeak.PeakBounds AlignedBounds
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

            [Format(Formats.Percent)]
            public double? Percentile
            {
                get
                {
                    return _ratedPeak.Percentile;
                }
            }

            [Format(Formats.PValue)]
            public double? PValue
            {
                get { return _ratedPeak.PValue; }
            }

            [Format(Formats.PValue)]
            public double? LibraryQValue
            {
                get { return _ratedPeak.QValue; }
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
                            return "Adjust Peak";
                        case RatedPeak.Verdict.NeedsRemoval:
                            return "Remove Peak";
                        default:
                            return string.Empty;
                    }
                }
            }
        }

        private void SettingsControlChanged(object sender, EventArgs e)
        {
            OnDocumentChanged();
        }
        private CutoffScoreType _oldCutoffType = CutoffScoreType.RAW;

        public CutoffScoreType CutoffType
        {
            get
            {
                if (radioQValue.Checked)
                {
                    return CutoffScoreType.QVALUE;
                }

                if (radioPValue.Checked)
                {
                    return CutoffScoreType.PVALUE;
                }

                if (radioPercentile.Checked)
                {
                    return CutoffScoreType.PERCENTILE;
                }
                return CutoffScoreType.RAW;
            }

            set
            {
                if (value == CutoffScoreType.QVALUE)
                {
                    radioQValue.Checked = true;
                }

                if (value == CutoffScoreType.PVALUE)
                {
                    radioPValue.Checked = true;
                }

                if (value == CutoffScoreType.PERCENTILE)
                {
                    radioPercentile.Checked = true;
                }

                if (value == CutoffScoreType.RAW)
                {
                    radioScore.Checked = true;
                }
            }
        }

        private void CutoffTypeChanged(object sender, EventArgs e)
        {
            UpdateCutoffValue();
        }

        private void UpdateCutoffValue()
        {
            var newCutoffType = CutoffType;
            if (newCutoffType == _oldCutoffType)
            {
                return;
            }
            if (_inChange)
            {
                return;
            }
            try
            {
                _inChange = true;
                var cutoffValue = GetDoubleValue(tbxCoreScoreCutoff, null, null);
                if (cutoffValue.HasValue)
                {
                    var score = _oldCutoffType.ToRawScore(_scoreConversionData, cutoffValue.Value);
                    if (score.HasValue && !double.IsNaN(score.Value))
                    {
                        var newCutoff = newCutoffType.FromRawScore(_scoreConversionData, score.Value);
                        if (newCutoff.HasValue && !double.IsNaN(newCutoff.Value))
                        {
                            if (newCutoffType == CutoffScoreType.PERCENTILE)
                            {
                                newCutoff *= 100;
                            }
                            tbxCoreScoreCutoff.Text = newCutoff.ToString();
                        }
                    }
                }
                _oldCutoffType = newCutoffType;
                UpdateUi();
            }
            finally
            {
                _inChange = false;
            }

        }

        private double? GetDoubleValue(TextBox textBox, double? min, double? max)
        {
            var text = textBox.Text.Trim();
            double? value = null;
            if (!string.IsNullOrEmpty(text))
            {
                if (!double.TryParse(text, out var doubleValue) || double.IsNaN(doubleValue))
                {
                    SetError(textBox, "Error: Must be a number");
                    return null;
                }

                if (min.HasValue && max.HasValue)
                {
                    if (min > doubleValue || max < doubleValue)
                    {
                        SetError(textBox, string.Format("Error: must be between {0} and {1}", min, max));
                        return null;
                    }
                }

                if (min > doubleValue)
                {
                    SetError(textBox, "Error: Must not be below " + min);
                    return null;
                }

                if (max < doubleValue)
                {
                    SetError(textBox, "Error: Must not be above " + max);
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
                MessageDlg.Show(this, "There are no rows");
                return;
            }
            ImputeBoundaries(rows);
        }

        public int ImputeBoundaries(IList<Row> rows)
        {
            return ImputeBoundaries(rows, null);
        }

        public int ImputeBoundaries(IList<Row> rows, ICollection<ReplicateFileId> replicateFileIds)
        {
            lock (SkylineWindow.GetDocumentChangeLock())
            {
                var originalDocument = SkylineWindow.DocumentUI;
                var newDoc = originalDocument.BeginDeferSettingsChanges();
                var scoringModel = GetScoringModelToUse(originalDocument);
                using var longWaitDlg = new LongWaitDlg();
                int changeCount = 0;
                longWaitDlg.PerformWork(this, 1000, () =>
                {
                    for (int iRow = 0; iRow < rows.Count; iRow++)
                    {
                        if (longWaitDlg.IsCanceled)
                        {
                            return;
                        }

                        longWaitDlg.ProgressValue = 100 * iRow / rows.Count;
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
                            var peakImputer = new PeakImputer(newDoc, row.Peptide.IdentityPath, scoringModel,
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
                });
                if (longWaitDlg.IsCanceled)
                {
                    return 0;
                }

                if (changeCount == 0)
                {
                    return 0;
                }

                newDoc = newDoc.EndDeferSettingsChanges(originalDocument, null);
                SkylineWindow.ModifyDocument("Impute peak boundaries", doc =>
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

        private bool IsPathSelected(IdentityPath identityPath)
        {
            foreach (var node in SkylineWindow.SelectedNodes.Prepend(SkylineWindow.SelectedNode).OfType<SrmTreeNode>())
            {
                if (Equals(node.Path, identityPath))
                {
                    return true;
                }

                if (node.Path.Depth > identityPath.Depth && node.Path.GetPathTo(identityPath.Depth).Equals(identityPath))
                {
                    return true;
                }
            }

            return false;
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
            }.Select(name=>new ColumnSpec(PropertyPath.Root.Property(name)))).SetName("Default");

            var ppPeaks = PropertyPath.Root.Property(nameof(Row.Peaks)).DictionaryValues();
            var propertyPaths = new List<PropertyPath>
            {
                PropertyPath.Root.Property(nameof(Row.Peptide)),
                ppPeaks,
                ppPeaks.Property(nameof(Peak.Score))
            };
            if (HasQValues(SkylineWindow.Document))
            {
                propertyPaths.Add(ppPeaks.Property(nameof(Peak.LibraryQValue)));
            }

            propertyPaths.Add(ppPeaks.Property(nameof(Peak.Verdict)));
            propertyPaths.Add(ppPeaks.Property(nameof(Peak.Opinion)));
            propertyPaths.Add(ppPeaks.Property(nameof(Peak.Action)));
            yield return new ViewSpec().SetRowType(typeof(Row))
                .SetColumns(propertyPaths.Select(pp => new ColumnSpec(pp)))
                .SetSublistId(ppPeaks).SetName("Details");
        }

        private class AlignmentOption
        {
            public AlignmentOption(string name, RtValueType rtValueType, AlignmentType alignmentType)
            {
                Name = name;
                RtValueType = rtValueType;
                AlignmentType = alignmentType;
            }

            public string Name { get; }
            public override string ToString()
            {
                return Name;
            }

            public RtValueType RtValueType { get; }
            public AlignmentType AlignmentType { get; }

            protected bool Equals(AlignmentOption other)
            {
                return Name == other.Name && Equals(RtValueType, other.RtValueType) && Equals(AlignmentType, other.AlignmentType);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((AlignmentOption)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (Name != null ? Name.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (RtValueType != null ? RtValueType.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (AlignmentType != null ? AlignmentType.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }

        private static IEnumerable<AlignmentOption> GetAlignmentOptions(SrmDocument document)
        {
            yield return new AlignmentOption(string.Empty, null, null);
            yield return new AlignmentOption("Peak Apexes", RtValueType.PEAK_APEXES, AlignmentType.CONSENSUS);
            // yield return new AlignmentOption("Good peak apex consensus", RtValueType.HIGH_SCORING_PEAK_APEXES,
            //     AlignmentType.CONSENSUS);
            // yield return new AlignmentOption("Peak Apex KDE", RtValueType.PEAK_APEXES, AlignmentType.KDE);
            if (RtValueType.PSM_TIMES.IsValidFor(document))
            {
                yield return new AlignmentOption("PSM Times", RtValueType.PSM_TIMES, AlignmentType.CONSENSUS);
//                yield return new AlignmentOption("PSM Times KDE", RtValueType.PSM_TIMES, AlignmentType.KDE);
            }
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
    }
}
