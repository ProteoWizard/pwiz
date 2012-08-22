//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NHibernate;
using NHibernate.Linq;
using DigitalRune.Windows.Docking;
using IDPicker.DataModel;
using ZedGraph;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Statistics;

namespace IDPicker.Forms
{
    public partial class DistributionStatisticsForm : DockableForm
    {
        public DistributionStatisticsForm (IDPickerForm owner)
        {
            InitializeComponent();

            this.owner = owner;

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                e.Cancel = true;
                DockState = DockState.DockBottomAutoHide;
            };

            Text = TabText = "Distribution Statistics";
            Icon = Properties.Resources.BlankIcon;

            refreshButton.Image = new Icon(Properties.Resources.Refresh, refreshButton.Width / 2, refreshButton.Height / 2).ToBitmap();

            refreshDataLabel.LinkClicked += (sender, e) => refreshButton_Click(sender, e);

            precursorMassErrorForm = new DockableGraphForm { Text = "Precursor Mass Error" };
            precursorMassErrorForm.ZedGraphControl.GraphPane.YAxis.Title.Text = "Density";
            precursorMassErrorForm.ZedGraphControl.GraphPane.XAxis.Title.Text = "Observed - Expected Mass";
            precursorMassErrorForm.ZedGraphControl.GraphPane.XAxis.MajorTic.IsInside = false;
            precursorMassErrorForm.ZedGraphControl.GraphPane.XAxis.MinorTic.IsInside = false;
            precursorMassErrorForm.ZedGraphControl.GraphPane.Legend.IsVisible = false;
            precursorMassErrorForm.ZedGraphControl.GraphPane.Border.IsVisible = false;
            precursorMassErrorForm.ZedGraphControl.GraphPane.IsFontsScaled = false;
            precursorMassErrorForm.ZedGraphControl.GraphPane.IsPenWidthScaled = false;
            precursorMassErrorForm.ZedGraphControl.BorderStyle = BorderStyle.None;

            scanTimeDistributionForm = new DockableGraphForm { Text = "Scan Time" };
            scanTimeDistributionForm.ZedGraphControl.GraphPane.YAxis.Title.Text = "Density";
            scanTimeDistributionForm.ZedGraphControl.GraphPane.XAxis.Title.Text = "Scan Time (minutes)";
            scanTimeDistributionForm.ZedGraphControl.GraphPane.XAxis.MajorTic.IsInside = false;
            scanTimeDistributionForm.ZedGraphControl.GraphPane.XAxis.MinorTic.IsInside = false;
            scanTimeDistributionForm.ZedGraphControl.GraphPane.Legend.IsVisible = false;
            scanTimeDistributionForm.ZedGraphControl.GraphPane.Border.IsVisible = false;
            scanTimeDistributionForm.ZedGraphControl.GraphPane.IsFontsScaled = false;
            scanTimeDistributionForm.ZedGraphControl.GraphPane.IsPenWidthScaled = false;
            scanTimeDistributionForm.ZedGraphControl.BorderStyle = BorderStyle.None;

            chargeStatesForm = new DockableGraphForm { Text = "Charge States" };
            chargeStatesForm.ZedGraphControl.GraphPane.YAxis.Title.Text = "Spectra";
            chargeStatesForm.ZedGraphControl.GraphPane.XAxis.Title.Text = "Charge State";
            chargeStatesForm.ZedGraphControl.GraphPane.XAxis.Scale.MajorStepAuto = false;
            chargeStatesForm.ZedGraphControl.GraphPane.XAxis.Scale.MajorStep = 1;
            chargeStatesForm.ZedGraphControl.GraphPane.XAxis.Scale.Format = "f0";
            chargeStatesForm.ZedGraphControl.GraphPane.XAxis.Scale.IsSkipFirstLabel = true;
            chargeStatesForm.ZedGraphControl.GraphPane.XAxis.Scale.IsSkipLastLabel = true;
            chargeStatesForm.ZedGraphControl.GraphPane.XAxis.MajorTic.IsAllTics = false;
            chargeStatesForm.ZedGraphControl.GraphPane.XAxis.MinorTic.IsAllTics = false;
            chargeStatesForm.ZedGraphControl.GraphPane.Legend.IsVisible = false;
            chargeStatesForm.ZedGraphControl.GraphPane.Border.IsVisible = false;
            chargeStatesForm.ZedGraphControl.GraphPane.IsFontsScaled = false;
            chargeStatesForm.ZedGraphControl.GraphPane.IsPenWidthScaled = false;
            chargeStatesForm.ZedGraphControl.IsEnableZoom = false;
            chargeStatesForm.ZedGraphControl.IsEnableHPan = false;
            chargeStatesForm.ZedGraphControl.IsEnableVPan = false;
            chargeStatesForm.ZedGraphControl.BorderStyle = BorderStyle.None;

            precursorMassErrorForm.ZedGraphControl.ZoomEvent += (sender, before, after) =>
            {
                precursorMassErrorForm.ZedGraphControl.GraphPane.YAxis.Scale.Min = Math.Max(0, precursorMassErrorForm.ZedGraphControl.GraphPane.YAxis.Scale.Min);
            };

            scanTimeDistributionForm.ZedGraphControl.ZoomEvent += (sender, before, after) =>
            {
                precursorMassErrorForm.ZedGraphControl.GraphPane.YAxis.Scale.Min = Math.Max(0, precursorMassErrorForm.ZedGraphControl.GraphPane.YAxis.Scale.Min);
            };

            chargeStatesForm.ZedGraphControl.ZoomEvent += (sender, before, after) =>
            {
                chargeStatesForm.ZedGraphControl.GraphPane.XAxis.Scale.MajorStepAuto = false;
                chargeStatesForm.ZedGraphControl.GraphPane.XAxis.Scale.MajorStep = 1;
                chargeStatesForm.ZedGraphControl.GraphPane.XAxis.Scale.Format = "f0";
                chargeStatesForm.ZedGraphControl.GraphPane.XAxis.Scale.Min = Math.Max(0, chargeStatesForm.ZedGraphControl.GraphPane.XAxis.Scale.Min);
                chargeStatesForm.ZedGraphControl.GraphPane.YAxis.Scale.Min = Math.Max(0, chargeStatesForm.ZedGraphControl.GraphPane.YAxis.Scale.Min);
            };

            ppmMassErrorMenuItem = new ToolStripMenuItem("Show Mass Error in PPM", null,
                    (x, y) =>
                    {
                        if (ppmMassErrorMenuItem.Text.Contains("PPM"))
                            ppmMassErrorMenuItem.Text = "Show Mass Error in Daltons";
                        else
                            ppmMassErrorMenuItem.Text = "Show Mass Error in PPM";
                        refreshButton_Click(this, EventArgs.Empty);
                    });

            precursorMassErrorForm.ZedGraphControl.ContextMenuBuilder += (sender, menuStrip, mousePt, objState) =>
            {
                int index = menuStrip.Items.Cast<ToolStripMenuItem>().TakeWhile(o => o.Text != "Show Point Values").Count();
                menuStrip.Items.Insert(index, ppmMassErrorMenuItem);
            };

            precursorMassErrorForm.ShowDataTable += (sender, e) =>
            {
                e.DataTable = new DataTable();
                e.DataTable.Columns.Add("Mass Error", typeof(double));
                e.DataTable.BeginLoadData();
                foreach (var point in precursorMassErrorForm.ZedGraphControl.GraphPane.CurveList[1].Points as PointPairList)
                    e.DataTable.Rows.Add(point.X);
                e.DataTable.EndLoadData();
            };

            chargeStatesForm.ShowDataTable += (sender, e) =>
            {
                e.DataTable = new DataTable();
                e.DataTable.Columns.Add("Charge State", typeof(int));
                e.DataTable.Columns.Add("Spectra", typeof(int));
                e.DataTable.BeginLoadData();
                foreach (BarItem item in chargeStatesForm.ZedGraphControl.GraphPane.CurveList)
                    e.DataTable.Rows.Add((int) item.Points[0].X, (int) item.Points[0].Y);
                e.DataTable.EndLoadData();
            };

            lastActiveGraphForm = precursorMassErrorForm;
        }

        private IDPickerForm owner;
        private NHibernate.ISession session;

        private DataFilter viewFilter; // what the user has filtered on
        private DataFilter dataFilter; // how this view is filtered (i.e. never on its own rows)

        private ToolStripMenuItem ppmMassErrorMenuItem;
        private DockableGraphForm precursorMassErrorForm;
        private DockableGraphForm scanTimeDistributionForm;
        private DockableGraphForm chargeStatesForm;

        private DockableForm lastActiveGraphForm;

        protected override void OnSizeChanged(EventArgs e)
        {
            refreshDataLabel.Location = new Point((Width - refreshDataLabel.Width) / 2,
                                                  (Height - refreshDataLabel.Height) / 2);

            base.OnSizeChanged(e);
        }

        public void SetData (NHibernate.ISession session, DataFilter dataFilter)
        {
            this.session = session;
            viewFilter = dataFilter;
            this.dataFilter = new DataFilter(dataFilter);

            ClearData();
        }

        public void ClearData ()
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => ClearData()));
                return;
            }

            precursorMassErrorForm.ZedGraphControl.GraphPane.CurveList.Clear();
            scanTimeDistributionForm.ZedGraphControl.GraphPane.CurveList.Clear();
            chargeStatesForm.ZedGraphControl.GraphPane.CurveList.Clear();

            lastActiveGraphForm = dockPanel.LastActiveContent as DockableForm ?? precursorMassErrorForm;
            precursorMassErrorForm.Hide();
            scanTimeDistributionForm.Hide();
            chargeStatesForm.Hide();

            refreshDataLabel.Visible = true;

            Text = TabText = "Distribution Statistics";
            Refresh();
        }

        public void ClearSession ()
        {
            ClearData();
            if (session != null && session.IsOpen)
            {
                session.Close();
                session.Dispose();
                session = null;
            }
        }

        public PointPairList getDensityCurve (IList<double> data, double bandwidth)
        {
            int intervals = 1000;
            var result = new PointPairList { Capacity = intervals };

            //generate a base line
            var statistics = new DescriptiveStatistics(data);
            double minValue = data.Min() - (2 * statistics.StandardDeviation);
            double maxValue = data.Max() + (2 * statistics.StandardDeviation);
            double interval = (maxValue - minValue) / intervals * 1.0;

            for (int i = 0; i < intervals; i++)
                result.Add(minValue + i * interval, 0);

            if (bandwidth == 0)
                bandwidth = 1.06 * statistics.StandardDeviation * Math.Pow(data.Count, -1.0 / 5);

            var orderedData = data.OrderBy(o => o);
            foreach (var value in orderedData)
            {
                Normal nD = new Normal(0, 1);
                for (int q = 0; q < intervals; q++)
                    result[q].Y += (1 / (data.Count * bandwidth)) * nD.Density((value - result[q].X) / bandwidth);
            }
            return result;
        }

        private class PSMRow
        {
            public double ObservedNeutralMass { get; set; }
            public double MassError { get; set; }
            public double ScanTime { get; set; }
        }

        void setData (object sender, DoWorkEventArgs e)
        {
            try
            {
                IList<PSMRow> precursorMassErrors;
                IDictionary<int, int> spectralCountByChargeState;
                lock (session)
                {
                    var query = session.CreateQuery("SELECT psm.ObservedNeutralMass, psm.MonoisotopicMassError, psm.MolecularWeightError, s.ScanTimeInSeconds" + dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch, DataFilter.PeptideSpectrumMatchToSpectrum));
                    precursorMassErrors = query.List<object[]>().Select(o => new PSMRow
                    {
                        ObservedNeutralMass = (double)o[0],
                        MassError = PeptideSpectrumMatch.GetSmallerMassError((double)o[1], (double)o[2]),
                        ScanTime = (double)o[3] / 60 // convert to minutes
                    }).ToList();

                    query = session.CreateQuery("SELECT psm.Charge, COUNT(DISTINCT psm.Spectrum.id) " +
                                                dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                                "GROUP BY psm.Charge");
                    spectralCountByChargeState = query.List<object[]>().ToDictionary(o => Convert.ToInt32(o[0]), o => Convert.ToInt32(o[1]));
                }

                // convert to PPM if the user requested it
                if (!ppmMassErrorMenuItem.Text.Contains("PPM"))
                    precursorMassErrors.ForEach(o => o.MassError = o.MassError / o.ObservedNeutralMass * 1e6);

                {
                    precursorMassErrorForm.ZedGraphControl.GraphPane.CurveList.Clear();
                    var precursorMassErrorValues = precursorMassErrors.Select(o => o.MassError).ToList();
                    var densityCurve = new LineItem("", getDensityCurve(precursorMassErrorValues, 0), Color.Red, SymbolType.None);
                    var grassPlot = new LineItem("", precursorMassErrorValues.ToArray(), Enumerable.Repeat(0.0, precursorMassErrors.Count).ToArray(), Color.Gray, SymbolType.VDash);
                    densityCurve.Line.IsAntiAlias = true;
                    precursorMassErrorForm.ZedGraphControl.GraphPane.CurveList.Add(densityCurve);
                    precursorMassErrorForm.ZedGraphControl.GraphPane.CurveList.Add(grassPlot);
                }

                {
                    scanTimeDistributionForm.ZedGraphControl.GraphPane.CurveList.Clear();
                    var scanTimeValues = precursorMassErrors.Select(o => o.ScanTime).ToList();
                    if (scanTimeValues.Max() > 0)
                    {
                        var densityCurve = new LineItem("", getDensityCurve(scanTimeValues, 0), Color.Red, SymbolType.None);
                        var grassPlot = new LineItem("", scanTimeValues.ToArray(), Enumerable.Repeat(0.0, scanTimeValues.Count).ToArray(), Color.Gray, SymbolType.VDash);
                        densityCurve.Line.IsAntiAlias = true;
                        scanTimeDistributionForm.ZedGraphControl.GraphPane.CurveList.Add(densityCurve);
                        scanTimeDistributionForm.ZedGraphControl.GraphPane.CurveList.Add(grassPlot);
                    }
                }

                var colorRotator = new ColorSymbolRotator();
                var colors = new Dictionary<int, Color[]>();
                foreach (var kvp in spectralCountByChargeState)
                {
                    var color = colorRotator.NextColor;
                    colors[kvp.Key] = new Color[3] { color, Color.White, color };
                }

                chargeStatesForm.ZedGraphControl.GraphPane.CurveList.Clear();
                foreach (var kvp in spectralCountByChargeState)
                {
                    var barItem = chargeStatesForm.ZedGraphControl.GraphPane.AddBar("", new double[] { kvp.Key }, new double[] { kvp.Value }, Color.White) as BarItem;
                    barItem.Bar.Fill = new Fill(colors[kvp.Key]);
                }
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        void renderData (object sender, RunWorkerCompletedEventArgs e)
        {
            Text = TabText = "Distribution Statistics";

            if (e.Result is Exception)
            {
                ClearData();
                Program.HandleException(e.Result as Exception);
            }

            precursorMassErrorForm.Show(dockPanel, DockState.Document);
            scanTimeDistributionForm.Show(dockPanel, DockState.Document);
            chargeStatesForm.Show(dockPanel, DockState.Document);
            lastActiveGraphForm.Activate();

            precursorMassErrorForm.ZedGraphControl.GraphPane.AxisChange();
            scanTimeDistributionForm.ZedGraphControl.GraphPane.AxisChange();
            chargeStatesForm.ZedGraphControl.GraphPane.AxisChange();

            precursorMassErrorForm.ZedGraphControl.RestoreScale(precursorMassErrorForm.ZedGraphControl.GraphPane);
            scanTimeDistributionForm.ZedGraphControl.RestoreScale(precursorMassErrorForm.ZedGraphControl.GraphPane);
            chargeStatesForm.ZedGraphControl.RestoreScale(precursorMassErrorForm.ZedGraphControl.GraphPane);
            Refresh();
        }

        void refreshButton_Click (object sender, EventArgs e)
        {
            Text = TabText = "Loading distribution statistics...";

            refreshDataLabel.Visible = false;

            var workerThread = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            workerThread.DoWork += setData;
            workerThread.RunWorkerCompleted += renderData;
            workerThread.RunWorkerAsync();
        }
    }
}
