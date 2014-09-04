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
using System.Drawing.Drawing2D;
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
            precursorMassErrorForm.ZedGraphControl.GraphPane.Border.IsVisible = false;
            precursorMassErrorForm.ZedGraphControl.GraphPane.IsFontsScaled = false;
            precursorMassErrorForm.ZedGraphControl.GraphPane.IsPenWidthScaled = false;
            precursorMassErrorForm.ZedGraphControl.BorderStyle = BorderStyle.None;

            scanTimeDistributionForm = new DockableGraphForm { Text = "Scan Time" };
            scanTimeDistributionForm.ZedGraphControl.GraphPane.YAxis.Title.Text = "Density";
            scanTimeDistributionForm.ZedGraphControl.GraphPane.XAxis.Title.Text = "Scan Time (minutes)";
            scanTimeDistributionForm.ZedGraphControl.GraphPane.XAxis.MajorTic.IsInside = false;
            scanTimeDistributionForm.ZedGraphControl.GraphPane.XAxis.MinorTic.IsInside = false;
            scanTimeDistributionForm.ZedGraphControl.GraphPane.Border.IsVisible = false;
            scanTimeDistributionForm.ZedGraphControl.GraphPane.IsFontsScaled = false;
            scanTimeDistributionForm.ZedGraphControl.GraphPane.IsPenWidthScaled = false;
            scanTimeDistributionForm.ZedGraphControl.BorderStyle = BorderStyle.None;

            chargeStatesForm = new DockableGraphForm { Text = "Charge States" };
            chargeStatesForm.ZedGraphControl.GraphPane.YAxis.Title.Text = "Spectra";
            chargeStatesForm.ZedGraphControl.GraphPane.XAxis.Title.Text = "Charge State";
            chargeStatesForm.ZedGraphControl.GraphPane.XAxis.Type = AxisType.Text;
            chargeStatesForm.ZedGraphControl.GraphPane.XAxis.MajorTic.IsOpposite = false;
            chargeStatesForm.ZedGraphControl.GraphPane.XAxis.MajorTic.IsInside = false;
            chargeStatesForm.ZedGraphControl.GraphPane.XAxis.MinorTic.IsAllTics = false;
            chargeStatesForm.ZedGraphControl.GraphPane.Legend.IsVisible = false;
            chargeStatesForm.ZedGraphControl.GraphPane.Border.IsVisible = false;
            chargeStatesForm.ZedGraphControl.GraphPane.IsFontsScaled = false;
            chargeStatesForm.ZedGraphControl.GraphPane.IsPenWidthScaled = false;
            chargeStatesForm.ZedGraphControl.IsEnableZoom = false;
            chargeStatesForm.ZedGraphControl.IsEnableHPan = false;
            chargeStatesForm.ZedGraphControl.IsEnableVPan = false;
            chargeStatesForm.ZedGraphControl.BorderStyle = BorderStyle.None;

            precursorMassErrorForm.ZedGraphControl.ZoomEvent += (sender, before, after, mousePosition) =>
            {
                precursorMassErrorForm.ZedGraphControl.GraphPane.YAxis.Scale.Min = Math.Max(0, precursorMassErrorForm.ZedGraphControl.GraphPane.YAxis.Scale.Min);
            };

            scanTimeDistributionForm.ZedGraphControl.ZoomEvent += (sender, before, after, mousePosition) =>
            {
                precursorMassErrorForm.ZedGraphControl.GraphPane.YAxis.Scale.Min = Math.Max(0, precursorMassErrorForm.ZedGraphControl.GraphPane.YAxis.Scale.Min);
            };

            chargeStatesForm.ZedGraphControl.ZoomEvent += (sender, before, after, mousePosition) =>
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
                e.DataTable.Columns.Add("Charge", typeof(int));

                // the grass plots, with a symbol type, hold the real data; the tag is the charge state
                e.DataTable.BeginLoadData();
                foreach (var curve in precursorMassErrorForm.ZedGraphControl.GraphPane.CurveList.Where(o => (o as LineItem).Symbol.Type != SymbolType.None))
                {
                    int charge = (int) curve.Tag;
                    foreach (var point in curve.Points as PointPairList)
                        e.DataTable.Rows.Add(point.X, charge);
                }
                e.DataTable.EndLoadData();
            };

            chargeStatesForm.ShowDataTable += (sender, e) =>
            {
                e.DataTable = new DataTable();
                e.DataTable.Columns.Add("Charge State", typeof(int));
                e.DataTable.Columns.Add("Spectra", typeof(int));
                e.DataTable.BeginLoadData();
                var points = chargeStatesForm.ZedGraphControl.GraphPane.CurveList[0].Points;
                for (int i = 0; i < points.Count; ++i)
                    e.DataTable.Rows.Add((int)points[i].X, (int)points[i].Y);
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

            Controls.OfType<Control>().ForEach(o => o.Enabled = true);
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

        public void ClearData(bool clearBasicFilter)
        {
            if (clearBasicFilter)
            {
                viewFilter = dataFilter = null;
                Controls.OfType<Control>().ForEach(o => o.Enabled = false);
            }

            ClearData();
        }

        public void ClearSession ()
        {
            ClearData(true);
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
            public int Charge { get; set; }
        }

        Map<int, List<List<PSMRow>>> clusterMassErrors(Map<int, List<PSMRow>> massErrorsByCharge)
        {
            Map<int, List<List<PSMRow>>> result = new Map<int, List<List<PSMRow>>>();
            foreach (var kvp in massErrorsByCharge)
            {
                var massErrors = kvp.Value.OrderBy(o => o.MassError).ToList();
                List<PSMRow> currentCluster = null;
                foreach (var psm in massErrors)
                {
                    if (currentCluster.IsNullOrEmpty() || psm.MassError - currentCluster.Last().MassError > 0.9)
                    {
                        if (currentCluster != null)
                            result[kvp.Key].Add(currentCluster);
                        currentCluster = new List<PSMRow>();
                    }
                    currentCluster.Add(psm);
                }

                if (currentCluster != null)
                    result[kvp.Key].Add(currentCluster);
            }
            return result;
        }

        void setData (object sender, DoWorkEventArgs e)
        {
            try
            {
                Map<int, List<PSMRow>> precursorMassErrorsByCharge = new Map<int, List<PSMRow>>(); ;
                IDictionary<int, int> spectralCountByChargeState;
                lock (session)
                {
                    var randomIds = session.CreateQuery("SELECT psm.Id " + viewFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch))
                                           .List<long>()
                                           .Shuffle()
                                           .Take(50000)
                                           .OrderBy(o => o);
                    string randomIdSet = String.Join(",", randomIds.Select(o => o.ToString()).ToArray());
                    var query = session.CreateSQLQuery("SELECT psm.ObservedNeutralMass, psm.MonoisotopicMassError, psm.MolecularWeightError, s.ScanTimeInSeconds, psm.Charge " +
                                                       "FROM PeptideSpectrumMatch psm " +
                                                       "JOIN Spectrum s ON psm.Spectrum=s.Id " +
                                                       "WHERE psm.Id IN (" + randomIdSet + ") " +
                                                       "GROUP BY psm.Id");
                    query.List<object[]>().ForEach(o =>
                        precursorMassErrorsByCharge[Convert.ToInt32(o[4])].Add(new PSMRow
                        {
                            ObservedNeutralMass = Convert.ToDouble(o[0]),
                            MassError = PeptideSpectrumMatch.GetSmallerMassError(Convert.ToDouble(o[1]), Convert.ToDouble(o[2])),
                            ScanTime = Convert.ToDouble(o[3]) / 60, // convert to minutes
                            Charge = Convert.ToInt32(o[4])
                        }));

                    var query2 = session.CreateQuery("SELECT psm.Charge, COUNT(DISTINCT psm.Spectrum.id) " +
                                                     dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                                     "GROUP BY psm.Charge");
                    spectralCountByChargeState = query2.List<object[]>().ToDictionary(o => Convert.ToInt32(o[0]), o => Convert.ToInt32(o[1]));
                }

                Map<int, List<List<PSMRow>>> clusteredPrecursorMassErrorsByCharge = clusterMassErrors(precursorMassErrorsByCharge);

                // convert to PPM if the user requested it
                if (!ppmMassErrorMenuItem.Text.Contains("PPM"))
                    clusteredPrecursorMassErrorsByCharge.Values.ForEach(o3 => o3.ForEach(o2 => o2.ForEach(o => o.MassError = o.MassError / o.ObservedNeutralMass * 1e6)));

                {
                    var csr = new ColorSymbolRotator();
                    precursorMassErrorForm.ZedGraphControl.GraphPane.CurveList.Clear();
                    foreach (var kvp in clusteredPrecursorMassErrorsByCharge)
                    {
                        bool firstCluster = true;
                        var color = csr.NextColor;
                        foreach (var precursorMassErrorCluster in kvp.Value)
                        {
                            var precursorMassErrors = precursorMassErrorCluster;
                            var precursorMassErrorValues = precursorMassErrors.Select(o => o.MassError).ToArray();
                            var densityCurve = new LineItem(firstCluster ? kvp.Key.ToString() : "", getDensityCurve(precursorMassErrorValues, 0), color, SymbolType.None);
                            var grassPlot = new LineItem("", precursorMassErrorValues, Enumerable.Repeat(0.0, precursorMassErrorValues.Length).ToArray(), color, SymbolType.VDash) { Tag = kvp.Key };
                            densityCurve.Line.IsAntiAlias = true;
                            precursorMassErrorForm.ZedGraphControl.GraphPane.CurveList.Add(densityCurve);
                            precursorMassErrorForm.ZedGraphControl.GraphPane.CurveList.Add(grassPlot);
                            firstCluster = false;
                        }
                    }
                }

                {
                    var csr = new ColorSymbolRotator();
                    scanTimeDistributionForm.ZedGraphControl.GraphPane.CurveList.Clear();
                    foreach (var kvp in precursorMassErrorsByCharge)
                    {
                        var precursorMassErrors = kvp.Value;
                        var scanTimeValues = precursorMassErrors.Select(o => o.ScanTime).ToList();
                        var color = csr.NextColor;
                        if (scanTimeValues.Max() > 0)
                        {
                            var densityCurve = new LineItem(kvp.Key.ToString(), getDensityCurve(scanTimeValues, 0), color, SymbolType.None);
                            var grassPlot = new LineItem("", scanTimeValues.ToArray(), Enumerable.Repeat(0.0, scanTimeValues.Count).ToArray(), color, SymbolType.VDash);
                            densityCurve.Line.IsAntiAlias = true;
                            scanTimeDistributionForm.ZedGraphControl.GraphPane.CurveList.Add(densityCurve);
                            scanTimeDistributionForm.ZedGraphControl.GraphPane.CurveList.Add(grassPlot);
                        }
                    }

                    if (scanTimeDistributionForm.ZedGraphControl.GraphPane.CurveList.IsNullOrEmpty())
                        scanTimeDistributionForm.ZedGraphControl.GraphPane.GraphObjList.Add(
                            new TextObj("No scan time information present.\r\nUse File/Embed to import scan times from raw data.",
                                        0.5, 0.5, CoordType.ChartFraction, AlignH.Center, AlignV.Center)
                                        { FontSpec = new FontSpec("monospace", 18.0f, Color.Black, true, false, false) });
                }

                var colorRotator = new ColorSymbolRotator();
                var colors = new List<Color>();
                foreach (var kvp in spectralCountByChargeState)
                    colors.Add(colorRotator.NextColor);

                chargeStatesForm.ZedGraphControl.GraphPane.CurveList.Clear();
                chargeStatesForm.ZedGraphControl.GraphPane.BarSettings.MinClusterGap = 8;
                var barItem = chargeStatesForm.ZedGraphControl.GraphPane.AddBar("", spectralCountByChargeState.Keys.Select(o => (double)o).ToArray(), spectralCountByChargeState.Values.Select(o => (double)o).ToArray(), Color.White) as BarItem;
                for (int i = 0; i < barItem.Points.Count; ++i)
                    barItem.Points[i].ColorValue = (double) i+1;
                barItem.Bar.Fill = new Fill(colors.ToArray())
                {
                    Type = FillType.GradientByColorValue,
                    RangeMin = 1,
                    RangeMax = colors.Count
                };
                chargeStatesForm.ZedGraphControl.GraphPane.XAxis.Scale.TextLabels = spectralCountByChargeState.Keys.Select(o => o.ToString()).ToArray();
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        void renderData (object sender, RunWorkerCompletedEventArgs e)
        {
            Text = TabText = "Distribution Statistics";

            Controls.OfType<Control>().ForEach(o => o.Enabled = true);

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
