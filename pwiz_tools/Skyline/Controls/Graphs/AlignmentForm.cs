/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results;
using ZedGraph;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class AlignmentForm : FormEx
    {
        private readonly BindingList<DataRow> _dataRows = new BindingList<DataRow>
                                                              {
                                                                  AllowEdit = false,
                                                                  AllowNew = false,
                                                                  AllowRemove = false,
                                                              };
        private readonly QueueWorker<Action> _rowUpdateQueue = new QueueWorker<Action>(null, (a, i) => a());
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public AlignmentForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            SkylineWindow = skylineWindow;
            Icon = Resources.Skyline;
            bindingSource.DataSource = _dataRows;
            colIntercept.CellTemplate.Style.Format = @"0.0000";
            colSlope.CellTemplate.Style.Format = @"0.0000";
            colCorrelationCoefficient.CellTemplate.Style.Format = @"0.0000";
            colUnrefinedSlope.CellTemplate.Style.Format = @"0.0000";
            colUnrefinedIntercept.CellTemplate.Style.Format = @"0.0000";
            colUnrefinedCorrelationCoefficient.CellTemplate.Style.Format = @"0.0000";

            zedGraphControl.GraphPane.IsFontsScaled = false;
            zedGraphControl.GraphPane.YAxisList[0].MajorTic.IsOpposite = false;
            zedGraphControl.GraphPane.YAxisList[0].MinorTic.IsOpposite = false;
            zedGraphControl.GraphPane.XAxis.MajorTic.IsOpposite = false;
            zedGraphControl.GraphPane.XAxis.MinorTic.IsOpposite = false;
            zedGraphControl.GraphPane.Chart.Border.IsVisible = false;

            _rowUpdateQueue.RunAsync(ParallelEx.GetThreadCount(), @"Alignment Rows");
        }

        private PlotTypeRT _plotType;

        public SkylineWindow SkylineWindow { get; private set; }
        public SrmDocument Document
        {
            get { return SkylineWindow.DocumentUI; }
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (SkylineWindow != null)
            {
                SkylineWindow.DocumentUIChangedEvent += SkylineWindowOnDocumentUIChangedEvent;
            }
            UpdateAll();
        }
        protected override void OnClosing(CancelEventArgs e)
        {
            _cancellationTokenSource.Cancel();

            base.OnClosing(e);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (SkylineWindow != null)
            {
                SkylineWindow.DocumentUIChangedEvent -= SkylineWindowOnDocumentUIChangedEvent;
            }
            _rowUpdateQueue.Dispose();
            base.OnHandleDestroyed(e);
        }

        private void SkylineWindowOnDocumentUIChangedEvent(object sender, DocumentChangedEventArgs documentChangedEventArgs)
        {
            UpdateAll();
        }

        public void UpdateAll()
        {
            UpdateCombo();
        }

        public void UpdateGraph()
        {
            zedGraphControl.IsEnableVPan = zedGraphControl.IsEnableVZoom = PlotType == PlotTypeRT.residuals;
            zedGraphControl.GraphPane.CurveList.Clear();
            zedGraphControl.GraphPane.GraphObjList.Clear();
            zedGraphControl.IsZoomOnMouseCenter = true;
            if (!(bindingSource.Current is DataRow))
            {
                return;
            }
            var currentRow = (DataRow) bindingSource.Current;
            var alignedFile = currentRow.AlignedRetentionTimes;
            if (alignedFile == null)
            {
                zedGraphControl.GraphPane.Title.Text = Resources.AlignmentForm_UpdateGraph_Waiting_for_retention_time_alignment;
                return;
            }
            var points = new PointPairList();
            var outliers = new PointPairList();
            var peptideTimes = alignedFile.Regression.PeptideTimes;
            for (int i = 0; i < peptideTimes.Count; i++)
            {
                var peptideTime = peptideTimes[i];
                var xTime = alignedFile.OriginalTimes[peptideTime.PeptideSequence];
                var yTime = peptideTime.RetentionTime;
                if (PlotType == PlotTypeRT.residuals)
                    yTime = (double) (alignedFile.Regression.GetRetentionTime(xTime, true) - yTime);
                var point = new PointPair(xTime, yTime, peptideTime.PeptideSequence.Sequence);
                if (alignedFile.OutlierIndexes.Contains(i))
                {
                    outliers.Add(point);
                }
                else
                {
                    points.Add(point);
                }
            }

            var goodPointsLineItem = new LineItem(@"Peptides", points, Color.Black, SymbolType.Diamond) // CONSIDER: localize?
                {
                    Symbol = {Size = 8f},
                    Line = {IsVisible = false}
                };
            goodPointsLineItem.Symbol.Border.IsVisible = false;
            goodPointsLineItem.Symbol.Fill = new Fill(RTLinearRegressionGraphPane.COLOR_REFINED);
            
            if (outliers.Count > 0)
            {
                var outlierLineItem = zedGraphControl.GraphPane.AddCurve(Resources.AlignmentForm_UpdateGraph_Outliers, outliers, Color.Black,
                                                                        SymbolType.Diamond);
                outlierLineItem.Symbol.Size = 8f;
                outlierLineItem.Line.IsVisible = false;
                outlierLineItem.Symbol.Border.IsVisible = false;
                outlierLineItem.Symbol.Fill = new Fill(RTLinearRegressionGraphPane.COLOR_OUTLIERS);
                goodPointsLineItem.Label.Text = Resources.AlignmentForm_UpdateGraph_Peptides_Refined;
            }
            zedGraphControl.GraphPane.CurveList.Add(goodPointsLineItem);
            if (points.Count > 0 && PlotType == PlotTypeRT.correlation)
            {
                double xMin = points.Select(p => p.X).Min();
                double xMax = points.Select(p => p.X).Max();
                var regression = alignedFile.RegressionRefined ?? alignedFile.Regression;
                var regressionLine = zedGraphControl.GraphPane
                        .AddCurve(Resources.AlignmentForm_UpdateGraph_Regression_line, new[] { xMin, xMax },
                        new[] { regression.Conversion.GetY(xMin), regression.Conversion.GetY(xMax) },
                        Color.Black);
                regressionLine.Symbol.IsVisible = false;
            }
            zedGraphControl.GraphPane.Title.Text = string.Format(Resources.AlignmentForm_UpdateGraph_Alignment_of__0__to__1_,
                currentRow.DataFile, currentRow.Target.Name);
            zedGraphControl.GraphPane.XAxis.Title.Text = string.Format(Resources.AlignmentForm_UpdateGraph_Time_from__0__, 
                currentRow.DataFile);
            zedGraphControl.GraphPane.YAxis.Title.Text = PlotType == PlotTypeRT.correlation
                ? Resources.AlignmentForm_UpdateGraph_Aligned_Time
                : Resources.AlignmentForm_UpdateGraph_Time_from_Regression;
            zedGraphControl.GraphPane.AxisChange();
            zedGraphControl.Invalidate();
        }

        private void AlignDataRow(int index, CancellationToken cancellationToken)
        {
            var dataRow = _dataRows[index];
            if (dataRow.TargetTimes == null || dataRow.SourceTimes == null)
            {
                return;
            }

            _rowUpdateQueue.Add(() => AlignDataRowAsync(dataRow, index, cancellationToken));
        }

        private void AlignDataRowAsync(DataRow dataRow, int index, CancellationToken cancellationToken)
        {
            try
            {
                var alignedTimes = AlignedRetentionTimes.AlignLibraryRetentionTimes(
                    dataRow.TargetTimes, dataRow.SourceTimes,
                    DocumentRetentionTimes.REFINEMENT_THRESHHOLD,
                    RegressionMethodRT.linear,
                    cancellationToken);

                if (!cancellationToken.IsCancellationRequested)
                {
                    RunUI(() => UpdateDataRow(index, alignedTimes, cancellationToken));
                }
            }
            catch (OperationCanceledException operationCanceledException)
            {
                throw new OperationCanceledException(operationCanceledException.Message, operationCanceledException, cancellationToken);
            }
        }

        private void RunUI(Action action)
        {
            Invoke(action);
        }

        private void UpdateDataRow(int iRow, AlignedRetentionTimes alignedTimes, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            var dataRow = _dataRows[iRow];
            dataRow.AlignedRetentionTimes = alignedTimes;
            _dataRows[iRow] = dataRow;
        }

        public void UpdateRows()
        {
            var newRows = GetRows();
            if (newRows.SequenceEqual(_dataRows))
            {
                return;
            }
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            _dataRows.RaiseListChangedEvents = false;
            _dataRows.Clear();
            foreach (var row in newRows)
            {
                _dataRows.Add(row);
            }
            bool allSameLibrary = true;
            if (newRows.Count > 0)
            {
                var firstLibrary = newRows[0].Library;
                allSameLibrary = newRows.Skip(1).All(row => Equals(row.Library, firstLibrary));
            }
            colLibrary.Visible = !allSameLibrary;
            _dataRows.RaiseListChangedEvents = true;
            _dataRows.ResetBindings();
            for (int i = 0; i < _dataRows.Count; i++ )
            {
                AlignDataRow(i, _cancellationTokenSource.Token);
            }
            UpdateGraph();
        }
        private void UpdateCombo()
        {
            var documentRetentionTimes = Document.Settings.DocumentRetentionTimes;
            var newItems = documentRetentionTimes.RetentionTimeSources.Values.Select(retentionTimeSource=>new DataFileKey(retentionTimeSource)).ToArray();
            if (newItems.SequenceEqual(comboAlignAgainst.Items.Cast<DataFileKey>()))
            {
                return;
            }
            var selectedIndex = comboAlignAgainst.SelectedIndex;
            comboAlignAgainst.Items.Clear();
            comboAlignAgainst.Items.AddRange(newItems.Cast<object>().ToArray());
            ComboHelper.AutoSizeDropDown(comboAlignAgainst);
            bool updateRows = true;
            if (comboAlignAgainst.Items.Count > 0)
            {
                if (selectedIndex < 0)
                {
                    if (SkylineWindow.SelectedResultsIndex >= 0 && Document.Settings.HasResults)
                    {
                        var chromatogramSet =
                            Document.Settings.MeasuredResults.Chromatograms[SkylineWindow.SelectedResultsIndex];
                        foreach (var msDataFileInfo in chromatogramSet.MSDataFileInfos)
                        {
                            var retentionTimeSource = documentRetentionTimes.RetentionTimeSources.Find(msDataFileInfo);
                            if (retentionTimeSource == null)
                            {
                                continue;
                            }
                            selectedIndex =
                                newItems.IndexOf(
                                    dataFileKey => Equals(retentionTimeSource, dataFileKey.RetentionTimeSource));
                            break;
                        }
                    }
                }

                selectedIndex = Math.Min(comboAlignAgainst.Items.Count - 1,
                    Math.Max(0, selectedIndex));
                if (comboAlignAgainst.SelectedIndex != selectedIndex)
                {
                    comboAlignAgainst.SelectedIndex = selectedIndex;
                    updateRows = false; // because the selection change will cause an update
                }
            }
            if (updateRows)
                UpdateRows();
        }

        private IList<DataRow> GetRows()
        {
            var targetKey = comboAlignAgainst.SelectedItem as DataFileKey?;
            if (!targetKey.HasValue)
            {
                return new DataRow[0];
            }
            var documentRetentionTimes = Document.Settings.DocumentRetentionTimes;
            var dataRows = new List<DataRow>();
            foreach (var retentionTimeSource in documentRetentionTimes.RetentionTimeSources.Values)
            {
                if (targetKey.Value.RetentionTimeSource.Name == retentionTimeSource.Name)
                {
                    continue;
                }
                dataRows.Add(new DataRow(Document.Settings, targetKey.Value.RetentionTimeSource, retentionTimeSource));
            }
            return dataRows;
        }

        internal struct DataRow
        {
            public DataRow(SrmSettings settings, RetentionTimeSource target, RetentionTimeSource timesToAlign) : this()
            {
                DocumentRetentionTimes = settings.DocumentRetentionTimes;
                Target = target;
                Source = timesToAlign;
                Assume.IsNotNull(target, @"target");
                Assume.IsNotNull(DocumentRetentionTimes.FileAlignments, @"DocumentRetentionTimes.FileAlignments");
                var fileAlignment = DocumentRetentionTimes.FileAlignments.Find(target.Name);
                if (fileAlignment != null)
                {
                    Assume.IsNotNull(fileAlignment.RetentionTimeAlignments, @"fileAlignment.RetentionTimeAlignments");
                    Assume.IsNotNull(Source, @"Source");
                    Alignment = fileAlignment.RetentionTimeAlignments.Find(Source.Name);
                }
                TargetTimes = GetFirstRetentionTimes(settings, target);
                SourceTimes = GetFirstRetentionTimes(settings, timesToAlign);
            }

            internal DocumentRetentionTimes DocumentRetentionTimes { get; private set; }
            internal RetentionTimeSource Target { get; private set; }
            internal RetentionTimeSource Source { get; private set; }
            internal RetentionTimeAlignment Alignment { get; private set; }
            internal IDictionary<Target, double> TargetTimes { get; private set; }
            internal IDictionary<Target, double> SourceTimes { get; private set; }
            public AlignedRetentionTimes AlignedRetentionTimes { get; set; }

            public String DataFile { get { return Source.Name; } }
            public string Library { get { return Source.Library; } }
            public RegressionLine RegressionLine
            {
                get 
                { 
                    if (AlignedRetentionTimes != null)
                    {
                        var regression = AlignedRetentionTimes.RegressionRefined ?? AlignedRetentionTimes.Regression;
                        if (regression != null)
                        {
                            var regressionLine = regression.Conversion as RegressionLineElement;
                            if(regressionLine != null)
                                return new RegressionLine(regressionLine.Slope, regressionLine.Intercept);
                        }
                    }
                    if (Alignment != null)
                    {
                        return Alignment.RegressionLine;
                    }
                    return null;
                }
            }
            public double? Slope
            {
                get
                {
                    var regressionLine = RegressionLine;
                    if (regressionLine != null)
                    {
                        return regressionLine.Slope;
                    }
                    return null;
                }
            }
            public double? Intercept
            {
                get
                {
                    var regressionLine = RegressionLine;
                    if (regressionLine != null)
                    {
                        return regressionLine.Intercept;
                    }
                    return null;
                }
            }
            public double? CorrelationCoefficient
            {
                get
                {
                    if (AlignedRetentionTimes == null)
                    {
                        return null;
                    }
                    return AlignedRetentionTimes.RegressionRefinedStatistics.R;
                }
            }
            public int? OutlierCount
            {
                get
                {
                    if (AlignedRetentionTimes == null)
                    {
                        return null;
                    }
                    return AlignedRetentionTimes.OutlierIndexes.Count;
                }
            }
            public double? UnrefinedSlope
            {
                get
                {
                    if (AlignedRetentionTimes == null)
                    {
                        return null;
                    }
                    var regressionLine = AlignedRetentionTimes.Regression.Conversion as RegressionLineElement;
                    return regressionLine != null ? regressionLine.Slope : null as double?;
                }
            }
            public double? UnrefinedIntercept
            {
                get
                {
                    if (AlignedRetentionTimes == null)
                    {
                        return null;
                    }
                    var regressionLine = AlignedRetentionTimes.Regression.Conversion as RegressionLineElement;
                    return regressionLine != null ? regressionLine.Intercept: null as double?;
                }
            }
            public double? UnrefinedCorrelationCoefficient
            {
                get
                {
                    if (AlignedRetentionTimes == null)
                    {
                        return null;
                    }
                    return AlignedRetentionTimes.RegressionStatistics.R;
                }
            }
            public int? PointCount
            {
                get
                {
                    if (AlignedRetentionTimes == null)
                    {
                        return null;
                    }
                    return AlignedRetentionTimes.RegressionStatistics.Peptides.Count;
                }
            }

            private static IDictionary<Target, double> GetFirstRetentionTimes(
                SrmSettings settings, RetentionTimeSource retentionTimeSource)
            {
                var libraryRetentionTimes = settings.PeptideSettings.Libraries.IsLoaded ? 
                    settings.GetRetentionTimes(MsDataFileUri.Parse(retentionTimeSource.Name)) : null;
                if (null == libraryRetentionTimes)
                {
                    return new Dictionary<Target, double>();
                }
                return libraryRetentionTimes.GetFirstRetentionTimes();
            }
        }

        internal struct DataFileKey
        {
            public DataFileKey(RetentionTimeSource retentionTimeSource) : this()
            {
                RetentionTimeSource = retentionTimeSource;
            }

            public RetentionTimeSource RetentionTimeSource { get; private set; }
            public override string ToString()
            {
                return RetentionTimeSource.Name;
            }
        }

        private void comboAlignAgainst_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateRows();
        }

        private void bindingSource_CurrentItemChanged(object sender, EventArgs e)
        {
            UpdateGraph();
        }

        private void zedGraphControl_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            ZedGraphHelper.BuildContextMenu(sender, menuStrip, true);

            int iInsert = 0;
            menuStrip.Items.Insert(iInsert++, timePlotContextMenuItem);
            if (timePlotContextMenuItem.DropDownItems.Count == 0)
            {
                timePlotContextMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                    {
                        timeCorrelationContextMenuItem,
                        timeResidualsContextMenuItem
                    });
            }
            timeCorrelationContextMenuItem.Checked = PlotType == PlotTypeRT.correlation;
            timeResidualsContextMenuItem.Checked = PlotType == PlotTypeRT.residuals;
            menuStrip.Items.Insert(iInsert, new ToolStripSeparator());
        }

        private void timeCorrelationContextMenuItem_Click(object sender, EventArgs e)
        {
            PlotType = PlotTypeRT.correlation;
        }

        private void timeResidualsContextMenuItem_Click(object sender, EventArgs e)
        {
            PlotType = PlotTypeRT.residuals;
        }

        public PlotTypeRT PlotType
        {
            get { return _plotType; }

            set
            {
                if (_plotType != value)
                {
                    _plotType = value;
                    zedGraphControl.ZoomOutAll(zedGraphControl.GraphPane);
                    UpdateGraph();                    
                }
            }
        }

        #region Functional test support

        public ComboBox ComboAlignAgainst { get { return comboAlignAgainst; } }
        public DataGridView DataGridView { get { return dataGridView1; } }
        public ZedGraphControl RegressionGraph { get { return zedGraphControl; } }
        public SplitContainer Splitter { get { return splitContainer1; } }

        #endregion
    }
}
