/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using pwiz.Topograph.Model;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class AlignmentForm : WorkspaceForm
    {
        private BindingList<DataRow> _dataRows = new BindingList<DataRow>();
        private KeyValuePair<int, string> _status;
        public AlignmentForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            bindingSource.DataSource = _dataRows;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateAll();
            comboTarget.SelectedIndexChanged += comboDataFile_SelectedIndexChanged;
            new Action(LoadRetentionTimesBackground).BeginInvoke(null, null);
        }

        void comboDataFile_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateRows();
        }

        public void UpdateAll()
        {
            UpdateTarget();
        }

        public void UpdateTarget()
        {
            var dataFiles = ListDataFiles();
            if (comboTarget.Items.Cast<MsDataFile>().SequenceEqual(dataFiles))
            {
                return;
            }
            var selectedIndexOld = comboTarget.SelectedIndex;
            comboTarget.Items.Clear();
            comboTarget.Items.AddRange(dataFiles.Cast<object>().ToArray());
            if (comboTarget.Items.Count > 0)
            {
                comboTarget.SelectedIndex = Math.Min(comboTarget.Items.Count - 1, Math.Max(0, selectedIndexOld));
            }
            UpdateRows();
        }

        private void LoadRetentionTimesBackground()
        {
            IList<MsDataFile> msDataFiles;
            using (Workspace.GetReadLock())
            {
                msDataFiles = Workspace.MsDataFiles.ListChildren();
            }
            for (int i = 0; i < msDataFiles.Count; i++)
            {
                if (IsDisposed)
                {
                    return;
                }
                try
                {
                    UpdateStatus(100 * i / msDataFiles.Count, "Loading MS2 IDs");
                    IList<Peptide> regressedPeptides;
                    msDataFiles[i].RegressTimes(msDataFiles[i], out regressedPeptides);
                }
                catch
                {
                    // ignore
                }
            }
            UpdateStatus(100, "Finished loading MS2 IDs");
        }

        private void UpdateStatus(int percentComplete, string message)
        {
            try
            {
                BeginInvoke(new Action(() =>
                                           {
                                               progressBarStatus.Value = percentComplete;
                                               lblStatus.Text = message;
                                               panelStatus.Visible = percentComplete < 100;
                                               _status = new KeyValuePair<int, string>(percentComplete, message);
                                               UpdateRows();
                                           }));
            }
            catch
            {
                // ignore
            }
        }

        public void UpdateRows()
        {
            if (_status.Key < 100)
            {
                return;
            }
            var rows = GetRows();
            if (rows.SequenceEqual(_dataRows))
            {
                return;
            }
            _dataRows.RaiseListChangedEvents = false;
            _dataRows.Clear();
            foreach (var row in rows)
            {
                _dataRows.Add(row);
            }
            _dataRows.RaiseListChangedEvents = true;
            _dataRows.ResetBindings();
            UpdateGraph();
        }

        public void UpdateGraph()
        {
            zedGraphControl.GraphPane.GraphObjList.Clear();
            zedGraphControl.GraphPane.CurveList.Clear();
            var currentRow = bindingSource.Current as DataRow;
            var target = comboTarget.SelectedItem;
            if (currentRow != null)
            {
                var refinedPoints = new PointPairList();
                var outliers = new PointPairList();
                var regression = currentRow.Refined ?? currentRow.Unrefined;
                if (regression != null)
                {
                    var outlierIndexes = regression.OutlierIndexes;
                    for (int i = 0; i < regression.TotalCount; i++)
                    {
                        var point = new PointPair(regression.OriginalTimes[i], regression.TargetTimes[i], currentRow.RegressedPeptides[i].FullSequence);
                        if (outlierIndexes.Contains(i))
                        {
                            outliers.Add(point);
                        }
                        else
                        {
                            refinedPoints.Add(point);
                        }
                    }
                    var refinedScatter = zedGraphControl.GraphPane.AddCurve("Refined Points", refinedPoints, Color.Black, SymbolType.Diamond);
                    refinedScatter.Symbol.Size = 8f;
                    refinedScatter.Line.IsVisible = false;
                    refinedScatter.Symbol.Border.IsVisible = false;
                    refinedScatter.Symbol.Fill = new Fill(Color.DarkBlue);
                    if (outliers.Count > 0)
                    {
                        var outlierScatter = zedGraphControl.GraphPane.AddCurve("Outliers", outliers, Color.Black,
                                                                                   SymbolType.Diamond);
                        outlierScatter.Symbol.Size = 8f;
                        outlierScatter.Line.IsVisible = false;
                        outlierScatter.Symbol.Border.IsVisible = false;
                        outlierScatter.Symbol.Fill = new Fill(Color.BlueViolet);
                    }
                    double xMin = regression.OriginalTimes.Min();
                    double xMax = regression.OriginalTimes.Max();
                    var regressionLine = zedGraphControl.GraphPane
                        .AddCurve("Regression line", new[] { xMin, xMax },
                            new[] { xMin * regression.Slope + regression.Intercept,xMax * regression.Slope + regression.Intercept},
                            Color.Black);
                    regressionLine.Symbol.IsVisible = false;
                    zedGraphControl.GraphPane.Title.Text = string.Format("Alignment of {0} to {1}",
                        currentRow.MsDataFile,
                        target);
                    zedGraphControl.GraphPane.XAxis.Title.Text
                        = string.Format("Time from {0}", currentRow.DataFile);
                    zedGraphControl.GraphPane.YAxis.Title.Text = "Aligned time";
                    zedGraphControl.GraphPane.AxisChange();
                    zedGraphControl.Invalidate();

                }
            }
        }

        private MsDataFile[] ListDataFiles()
        {
            var dataFiles = Workspace.MsDataFiles.ListChildren().ToArray();
            Array.Sort(dataFiles);
            return dataFiles;
        }

        private IList<DataRow> GetRows()
        {
            var target = comboTarget.SelectedItem as MsDataFile;
            if (target == null)
            {
                return new DataRow[0];
            }
            var list = new List<DataRow>();
            foreach (var msDataFile in ListDataFiles())
            {
                if (Equals(msDataFile, target))
                {
                    continue;
                }
                IList<Peptide> regressedPeptides;
                var rawRegression = msDataFile.RegressTimes(target, out regressedPeptides);
                var dataRow = new DataRow(msDataFile, rawRegression, regressedPeptides);
                list.Add(dataRow);
            }
            return list;
        }

        internal class DataRow
        {
            public DataRow(MsDataFile dataFile, RegressionWithOutliers unrefined, IList<Peptide> regressedPeptides)
            {
                MsDataFile = dataFile;
                Unrefined = unrefined;
                RegressedPeptides = regressedPeptides;
                if (Unrefined != null)
                {
                    Refined = Unrefined.Refine();
                }
            }

            public MsDataFile MsDataFile { get; private set; }
            public string DataFile { get { return MsDataFile.Label; } }
            public IList<Peptide> RegressedPeptides { get; private set; }
            public RegressionWithOutliers Unrefined { get; private set; }
            public RegressionWithOutliers Refined { get; private set; }
            public double? RefinedSlope { get { return Refined == null ? (double?) null : Refined.Slope; } }
            public double? RefinedIntercept { get { return Refined == null ? (double?) null : Refined.Intercept; } }
            public int RefinedPointCount { get { return Refined == null ? 0 : Refined.RefinedCount; } }
            public double RawSlope { get { return Unrefined.Slope; } }
            public double RawIntercept { get { return Unrefined.Intercept; } }
            public double RawR { get { return Unrefined.R; } }
            public int TotalPointCount { get { return Unrefined.TotalCount; } }
        }

        private void comboTarget_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateRows();
        }

        private void bindingSource_CurrentChanged(object sender, EventArgs e)
        {
            UpdateGraph();
        }
    }
}
