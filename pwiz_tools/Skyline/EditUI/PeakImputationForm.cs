using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MathNet.Numerics.Statistics;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
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
using pwiz.Skyline.Model.Results.Imputation;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using Peptide = pwiz.Skyline.Model.Peptide;

namespace pwiz.Skyline.EditUI
{
    public partial class PeakImputationForm : DataboundGridForm
    {
        private List<Row> _rows;
        private BindingList<Row> _rowsBindingList;
        private Receiver<PeakImputationData.Parameters, PeakImputationData> _receiver;
        private SkylineDataSchema _dataSchema;
        private PeakImputationData _data;

        public PeakImputationForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            SkylineWindow = skylineWindow;
            _rows = new List<Row>();
            _rowsBindingList = new BindingList<Row>(_rows);
            _dataSchema = new SkylineWindowDataSchema(skylineWindow);
            var rowSource = BindingListRowSource.Create(_rowsBindingList);
            var viewContext = new SkylineViewContext(ColumnDescriptor.RootColumn(_dataSchema, typeof(Row)), rowSource);
            BindingListSource.SetViewContext(viewContext);
            _receiver = PeakImputationData.PRODUCER.RegisterCustomer(this, ProductAvailableAction);
            _receiver.ProgressChange += ReceiverOnProgressChange;
        }

        private void ReceiverOnProgressChange()
        {
            ProductAvailableAction();
        }

        private void ProductAvailableAction()
        {
            if (_receiver.TryGetCurrentProduct(out _data))
            {
                _rows.Clear();
                _rows.AddRange(_data.MoleculePeaks.Select(peak=>new Row(_dataSchema, peak)));
                _rowsBindingList.ResetBindings();
                tbxAccepted.Text = _rows.SelectMany(row => row.Peaks.Values).Count(peak => peak.Accepted).ToString();
                tbxRejected.Text = _rows.SelectMany(row => row.Peaks.Values).Count(peak => !peak.Accepted).ToString();
                var rtShifts = _rows.SelectMany(row => row.Peaks.Values).Where(peak => !peak.Best).Select(peak=>Math.Abs(peak.ShiftFromBestPeak)).ToList();
                if (rtShifts.Count == 0)
                {
                    tbxAvgRtShift.Text = "";
                }
                else
                {
                    tbxAvgRtShift.Text = rtShifts.Mean().ToString(Formats.RETENTION_TIME);
                }

                progressBar1.Visible = false;
                updateProgressTimer.Stop();
                SkylineWindow.ConsensusAlignment = _data.ConsensusAlignment;
            }
            else
            {
                progressBar1.Visible = true;
                progressBar1.Value = (int) (_receiver.GetDeepProgressValue() * 100);
                updateProgressTimer.Start();
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

        protected override void OnHandleDestroyed(EventArgs e)
        {
            SkylineWindow.DocumentUIChangedEvent -= SkylineWindow_OnDocumentUIChangedEvent;
            base.OnHandleDestroyed(e);
        }
        private void SkylineWindow_OnDocumentUIChangedEvent(object sender, DocumentChangedEventArgs e)
        {
            OnDocumentChanged();
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
            var document = SkylineWindow.DocumentUI;
            ComboHelper.ReplaceItems(comboRetentionTimeAlignment, RtValueType.ForDocument(document).Prepend(null), 1);
            var scoringModel = GetScoringModelToUse(document);

            radioPValue.Enabled = !Equals(scoringModel, LegacyScoringModel.DEFAULT_MODEL);
            var scoreQValueMap = document.Settings.PeptideSettings.Integration.ScoreQValueMap;
            radioQValue.Enabled = scoreQValueMap != null && !Equals(scoreQValueMap, ScoreQValueMap.EMPTY);
            var parameters = new PeakImputationData.Parameters(document)
                .ChangeAlignmentType(comboRetentionTimeAlignment.SelectedItem as RtValueType)
                .ChangeOverwriteManualPeaks(cbxOverwriteManual.Checked)
                .ChangeScoringModel(scoringModel)
                .ChangeAllowableRtShift(GetDoubleValue(tbxRtDeviationCutoff));

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
                    }
                }
                    
                BestPeak = Peaks.Values.FirstOrDefault(peak=>peak.Best);
                CountAccepted = Peaks.Values.Count(peak => peak.Accepted);
                CountRejected = Peaks.Values.Count(peak => !peak.Accepted);
            }

            public Model.Databinding.Entities.Peptide Peptide { get; }
            public Dictionary<ResultKey, Peak> Peaks { get; }
            public Peak BestPeak { get; }
            public int CountAccepted { get; }
            public int CountRejected { get; }
        }

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
            public double? QValue
            {
                get { return _ratedPeak.QValue; }
            }

            public bool Best
            {
                get { return _ratedPeak.Best; }
            }
            public bool Accepted
            {
                get { return _ratedPeak.Accepted; }
            }

            public double ShiftFromBestPeak
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

            public EventHandler ClickEventHandler
            {
                get
                {
                    return LinkValueOnClick;
                }
            }

            public object Value
            {
                get { return this; }
            }

            public void LinkValueOnClick(object sender, EventArgs args)
            {
                _peptide.LinkValueOnClick(sender, args);
                (ResultFile.Replicate as ILinkValue)?.ClickEventHandler(sender, args);
            }
        }

        private void SettingsControlChanged(object sender, EventArgs e)
        {
            OnDocumentChanged();
        }
        private CutoffScoreType _cutoffType;

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
            var newCutoffType = CutoffType;
            if (newCutoffType == _cutoffType)
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
                var cutoffValue = GetDoubleValue(tbxCoreScoreCutoff);
                if (cutoffValue.HasValue)
                {
                    var score = _cutoffType.ToRawScore(_data, cutoffValue.Value);
                    if (score.HasValue && !double.IsNaN(score.Value))
                    {
                        var newCutoff = newCutoffType.FromRawScore(_data, score.Value);
                        if (newCutoff.HasValue && !double.IsNaN(newCutoff.Value))
                        {
                            tbxCoreScoreCutoff.Text = newCutoff.ToString();
                        }
                    }
                }
                _cutoffType = newCutoffType;
                OnDocumentChanged();
            }
            finally
            {
                _inChange = false;
            }

        }

        private double? GetDoubleValue(TextBox textBox)
        {
            var text = textBox.Text.Trim();
            double? value = null;
            if (!string.IsNullOrEmpty(text))
            {
                if (!double.TryParse(text, out var doubleValue))
                {
                    textBox.BackColor = Color.Red;
                    return null;
                }

                value = doubleValue;
            }
            textBox.BackColor = Color.White;
            return value;
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
                        var bestPeak = row.BestPeak?.GetRatedPeak();
                        if (bestPeak == null)
                        {
                            continue;
                        }

                        var rejectedPeaks = row.Peaks.Values.Where(peak => !peak.Accepted)
                            .Select(peak => peak.GetRatedPeak()).ToList();
                        foreach (var rejectedPeak in rejectedPeaks)
                        {
                            var peakImputer = new PeakImputer(newDoc, row.Peptide.IdentityPath, scoringModel,
                                rejectedPeak.ReplicateFileInfo);
                            var bestPeakBounds =
                                bestPeak.AlignedPeakBounds.ReverseAlign(rejectedPeak.AlignmentFunction);
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

        private void btnImputeForCurrentRow_Click(object sender, EventArgs e)
        {
            var row = BindingListSource.OfType<RowItem>().ElementAtOrDefault(BindingListSource.Position)?.Value as Row;
            if (row == null)
            {
                MessageDlg.Show(this, "There is no current row");
                return;
            }

            int changeCount = ImputeBoundaries(new[] { row });
            if (changeCount > 0 && !IsPathSelected(row.Peptide.IdentityPath))
            {
                SkylineWindow.SelectPath(row.Peptide.IdentityPath);
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

        private void updateProgressTimer_Tick(object sender, EventArgs e)
        {
            ReceiverOnProgressChange();
        }
    }
}
