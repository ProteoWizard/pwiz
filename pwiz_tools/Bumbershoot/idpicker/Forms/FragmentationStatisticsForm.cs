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
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using NHibernate;
using NHibernate.Linq;
using NHibernate.Util;
using DigitalRune.Windows.Docking;
using IDPicker.DataModel;
using ZedGraph;

using proteome = pwiz.CLI.proteome;
using msdata = pwiz.CLI.msdata;
using pwiz.CLI.chemistry;
using pwiz.CLI.analysis;

namespace IDPicker.Forms
{
    using DataFilter = DataModel.DataFilter;

    public partial class FragmentationStatisticsForm : DockableForm
    {
        #region Wrapper class for encapsulating query results
        public class SpectrumRow
        {
            public string SourceName { get; private set; }
            public Spectrum Spectrum { get; private set; }
            public PeptideSpectrumMatch PeptideSpectrumMatch { get; private set; }
            public string ModifiedSequence { get; private set; }

            #region Constructor
            public SpectrumRow (object[] queryRow)
            {
                SourceName = (string) queryRow[0];
                Spectrum = queryRow[1] as Spectrum;
                PeptideSpectrumMatch = queryRow[2] as PeptideSpectrumMatch;

                var mods = new Dictionary<int, List<double>>();
                if (!String.IsNullOrEmpty((string) queryRow[3]))
                {
                    var offsetMassDeltaPairs = ((string) queryRow[3]).Split(Properties.Settings.Default.GroupConcatSeparator[0]);
                    foreach (var pair in offsetMassDeltaPairs)
                    {
                        var offsetAndMassDelta = pair.Split(':');
                        int offset = Convert.ToInt32(offsetAndMassDelta[0]);
                        if (!mods.ContainsKey(offset))
                            mods[offset] = new List<double>();
                        mods[offset].Add(Convert.ToDouble(offsetAndMassDelta[1]));
                    }
                }

                string format = "[{0}]";// String.Format("[{{0:f{0}}}]", 0);
                StringBuilder sb = new StringBuilder((string) queryRow[4]);
                foreach (var mod in (from m in mods orderby m.Key descending select m))
                    foreach (var massDelta in mod.Value)
                        if (mod.Key == int.MinValue)
                            sb.Insert(0, String.Format(format, massDelta));
                        else if (mod.Key == int.MaxValue || mod.Key >= sb.Length)
                            sb.AppendFormat(format, massDelta);
                        else
                            sb.Insert(mod.Key + 1, String.Format(format, massDelta));
                ModifiedSequence = sb.ToString();
            }
            #endregion
        }
        #endregion

        private enum IonSeries
        {
            a, b, c, //cMinus1,
            x, y, z, //zPlus1, zPlus2,
            Count
        }

        private string[] IonSeriesLabels =
        {
            "a", "b", "c", //"c-1",
            "x", "y", "z", //"z+1", "z+2"
        };

        public FragmentationStatisticsForm (IDPickerForm owner)
        {
            InitializeComponent();

            this.owner = owner;

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                e.Cancel = true;
                DockState = DockState.DockBottomAutoHide;
            };

            Text = TabText = "Fragmentation Statistics";
            Icon = Properties.Resources.BlankIcon;

            refreshButton.Image = new Icon(Properties.Resources.Refresh, refreshButton.Width / 2, refreshButton.Height / 2).ToBitmap();

            refreshDataLabel.LinkClicked += (sender, e) => refreshButton_Click(sender, e);

            percentTicGraphForm = new DockableForm { Text = "%TIC" };
            percentPeakCountGraphForm = new DockableForm { Text = "%PeakCount" };
            meanMzErrorGraphForm = new DockableForm { Text = "Mean m/z error" };

            percentTicGraphControl = new ZedGraphControl { Dock = DockStyle.Fill, Text = percentTicGraphForm.Text };
            percentPeakCountGraphControl = new ZedGraphControl { Dock = DockStyle.Fill, Text = percentPeakCountGraphForm.Text };
            meanMzErrorGraphControl = new ZedGraphControl { Dock = DockStyle.Fill, Text = meanMzErrorGraphForm.Text };

            percentTicGraphForm.Controls.Add(percentTicGraphControl);
            percentPeakCountGraphForm.Controls.Add(percentPeakCountGraphControl);
            meanMzErrorGraphForm.Controls.Add(meanMzErrorGraphControl);

            initializeGraphControl(percentTicGraphControl);
            initializeGraphControl(percentPeakCountGraphControl);
            initializeGraphControl(meanMzErrorGraphControl);

            lastActiveGraphForm = meanMzErrorGraphForm;
            graphControls = new List<ZedGraphControl>
            {
                percentTicGraphControl,
                percentPeakCountGraphControl,
                meanMzErrorGraphControl
            };

            percentTicGraphForm.FormClosing += (sender, e) => e.Cancel = true;
            percentPeakCountGraphForm.FormClosing += (sender, e) => e.Cancel = true;
            meanMzErrorGraphForm.FormClosing += (sender, e) => e.Cancel = true;

            fragmentTolerance = new MZTolerance(0.5, MZTolerance.Units.MZ);
            fragmentToleranceUnitsComboBox.Text = fragmentTolerance.value.ToString();
            fragmentToleranceUnitsComboBox.SelectedIndex = (int) fragmentTolerance.units;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            refreshDataLabel.Location = new Point((Width - refreshDataLabel.Width) / 2,
                                                  (Height - refreshDataLabel.Height) / 2);

            base.OnSizeChanged(e);
        }

        private IDPickerForm owner;
        private NHibernate.ISession session;

        private DataFilter viewFilter; // what the user has filtered on
        private DataFilter dataFilter; // how this view is filtered (i.e. never on its own rows)
        //private DataFilter basicDataFilter; // the basic filter without the user filtering on rows

        private DockableForm percentTicGraphForm, percentPeakCountGraphForm;
        private DockableForm meanMzErrorGraphForm;

        private ZedGraphControl percentTicGraphControl, percentPeakCountGraphControl;
        private ZedGraphControl meanMzErrorGraphControl;

        private double maxPercentTic, maxPercentPeakCount, maxMeanMzError;

        private DockableForm lastActiveGraphForm;
        private List<ZedGraphControl> graphControls;

        private MZTolerance fragmentTolerance;

        private ZedGraphControl ActiveGraphControl
        {
            get
            {
                if (dockPanel.ActiveContent == null && dockPanel.LastActiveContent == null)
                    return null;
                if (dockPanel.ActiveContent == null)
                    return (dockPanel.LastActiveContent as DockableForm).Controls[0] as ZedGraphControl;
                return (dockPanel.ActiveContent as DockableForm).Controls[0] as ZedGraphControl;
            }
        }

        private void initializeGraphControl (ZedGraphControl zedGraphControl)
        {
            zedGraphControl.MasterPane.PaneList.Clear();
            zedGraphControl.MasterPane.SetLayout(zedGraphControl.CreateGraphics(), 1, (int) IonSeries.Count + 1);
            zedGraphControl.MasterPane.InnerPaneGap = 0;
            zedGraphControl.MasterPane.Border.IsVisible = true;
            zedGraphControl.IsEnableHPan = false;
            zedGraphControl.IsEnableHZoom = false;
            zedGraphControl.IsSynchronizeYAxes = true;
            zedGraphControl.IsZoomOnMouseCenter = true;

            var axisPane = new GraphPane();
            axisPane.Legend.IsVisible = false;
            axisPane.IsFontsScaled = false;
            axisPane.XAxis.IsVisible = false;
            axisPane.YAxis.Scale.Min = 0;
            axisPane.YAxis.Scale.Max = 100;
            axisPane.YAxis.Title.Text = zedGraphControl.Text;
            axisPane.YAxis.Title.Gap = 0.05f;
            axisPane.YAxis.MajorTic.IsOpposite = false;
            axisPane.YAxis.MinorTic.IsOpposite = false;
            axisPane.Chart.Border.IsVisible = false;
            axisPane.Border.IsVisible = false;
            axisPane.Margin.Left = 1;
            axisPane.Margin.Right = 0;
            axisPane.Title.Text = "Series:";
            zedGraphControl.MasterPane.Add(axisPane);

            var csr = new ColorSymbolRotator();
            for (int i = 0; i < (int) IonSeries.Count; ++i)
            {
                var graphPane = new GraphPane();
                graphPane.Title.Text = IonSeriesLabels[i];
                graphPane.Legend.IsVisible = false;
                graphPane.IsFontsScaled = false;
                graphPane.Chart.Border.IsVisible = false;
                graphPane.Border.IsVisible = false;
                graphPane.XAxis.Scale.Min = -1;
                graphPane.XAxis.Scale.Max = 1;
                graphPane.XAxis.IsVisible = false;
                graphPane.YAxis.Scale.Min = 0;
                graphPane.YAxis.Scale.Max = 100;
                graphPane.YAxis.IsVisible = false;
                zedGraphControl.MasterPane.Add(graphPane);

                graphPane.BarSettings.Type = BarType.Overlay;
                graphPane.BarSettings.ClusterScaleWidth = 1;

                var mean = graphPane.AddCurve(IonSeriesLabels[i],
                                              new PointPairList(),
                                              Color.Black,
                                              SymbolType.Circle);
                mean.Line.IsVisible = false;
                mean.Symbol.Border.IsVisible = false;
                mean.Symbol.Fill.Type = FillType.Solid;

                var errorBar = graphPane.AddErrorBar(IonSeriesLabels[i],
                                                     new PointPairList(),
                                                     Color.Black);
                errorBar.Bar.IsVisible = true;
                errorBar.Bar.PenWidth = .1f;
                errorBar.Bar.Symbol.IsVisible = true;
                errorBar.Bar.Symbol.Type = SymbolType.HDash;
                errorBar.Bar.Symbol.Border.Width = .1f;
                errorBar.Bar.Symbol.Size = 4;

                var hiLowBar = graphPane.AddHiLowBar(IonSeriesLabels[i],
                                                     new PointPairList(),
                                                     Color.Black);
                hiLowBar.Bar.Fill.Type = FillType.None;

                var scatter = graphPane.AddCurve(IonSeriesLabels[i],
                                                 new PointPairList(),
                                                 csr.NextColor,
                                                 SymbolType.Circle);
                scatter.Line.IsVisible = false;
                scatter.Symbol.IsAntiAlias = true;
                scatter.Symbol.Border.IsVisible = false;
                scatter.Symbol.Fill.Type = FillType.Solid;
                scatter.Symbol.Size = 3f;
            }

            zedGraphControl.MasterPane.AxisChange();
            zedGraphControl.Refresh();
        }

        private void addSixNumberSummary(GraphPane graphPane, IList<double> sortedNumbers)
        {
            double mean = sortedNumbers.Sum() / sortedNumbers.Count;
            double min = sortedNumbers.First();
            double q1 = sortedNumbers[sortedNumbers.Count / 4];
            double median = sortedNumbers[sortedNumbers.Count / 2];
            double q3 = sortedNumbers[sortedNumbers.Count * 3 / 4];
            double max = sortedNumbers.Last();

            string label = String.Format("Max: {0:G4}\n3rd Quartile: {1:G4}\nMedian: {2:G4}\n1st Quartile: {3:G4}\nMin: {4:G4}\nMean: {5:G4}", max, q3, median, q1, min, mean);
            graphPane.CurveList[0].AddPoint(new PointPair(0, mean, label));
            graphPane.CurveList[1].AddPoint(new PointPair(0, max, q3, label));
            graphPane.CurveList[1].AddPoint(new PointPair(0, q1, min, label));
            graphPane.CurveList[2].AddPoint(new PointPair(0, q3, median, label));
            graphPane.CurveList[2].AddPoint(new PointPair(0, median, q1, label));
        }

        private double mzError (double observed, double expected)
        {
            if (fragmentTolerance.units == MZTolerance.Units.PPM)
                return (observed - expected) / expected * 1e6;
            return observed - expected;
        }

        private double fragmentMass(pwiz.CLI.proteome.Fragmentation f, IonSeries series, int length, int charge)
        {
            switch (series)
            {
                case IonSeries.a: return f.a(length, charge);
                case IonSeries.b: return f.b(length, charge);
                case IonSeries.c: return f.c(length, charge);
                case IonSeries.x: return f.x(length, charge);
                case IonSeries.y: return f.y(length, charge);
                case IonSeries.z: return f.z(length, charge);
                default: throw new ArgumentException();
            }
        }

        private void getFragmentationStatistics ()
        {
            IList<object[]> queryRows;
            lock (session)
            {
                var randomIds = session.CreateQuery("SELECT psm.Id " + viewFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch))
                                       .List<long>()
                                       .Shuffle()
                                       .Take(1000)
                                       .OrderBy(o => o);
                string randomIdSet = String.Join(",", randomIds.Select(o => o.ToString()).ToArray());
                queryRows = session.CreateQuery("SELECT psm.Spectrum.Source.Name, psm.Spectrum, psm, DISTINCT_GROUP_CONCAT(pm.Offset || ':' || mod.MonoMassDelta), psm.Peptide.Sequence " +
                                                "FROM PeptideSpectrumMatch psm " +
                                                "LEFT JOIN psm.Modifications pm " + 
                                                "LEFT JOIN pm.Modification mod " +
                                                "WHERE psm.Id IN (" + randomIdSet + ") " +
                                                "GROUP BY psm.Spectrum.id ")
                                   .List<object[]>();
            }
            var spectrumRows = queryRows.Select(o => new SpectrumRow(o)).OrderBy(o => o.SourceName);

            var percentTicBySpectrumByFragmentType = new List<PointPairList>();
            var percentPeakCountBySpectrumByFragmentType = new List<PointPairList>();
            var meanMzErrorBySpectrumByFragmentType = new List<PointPairList>();
            var percentTicListByFragmentType = new List<List<double>>();
            var percentPeakCountListByFragmentType = new List<List<double>>();
            var meanMzErrorListByFragmentType = new List<List<double>>();

            foreach(var graphControl in graphControls)
                graphControl.MasterPane.PaneList.ForEach(o => o.CurveList.ForEach(c => c.Clear()));

            for (int i = 0; i < (int) IonSeries.Count; ++i)
            {
                percentTicBySpectrumByFragmentType.Add(percentTicGraphControl.MasterPane.PaneList[i + 1].CurveList[3].Points as PointPairList);
                percentPeakCountBySpectrumByFragmentType.Add(percentPeakCountGraphControl.MasterPane.PaneList[i + 1].CurveList[3].Points as PointPairList);
                meanMzErrorBySpectrumByFragmentType.Add(meanMzErrorGraphControl.MasterPane.PaneList[i + 1].CurveList[3].Points as PointPairList);
                percentTicListByFragmentType.Add(new List<double>());
                percentPeakCountListByFragmentType.Add(new List<double>());
                meanMzErrorListByFragmentType.Add(new List<double>());
            }

            int spectraCount = 0;
            maxPercentTic = 10;
            maxPercentPeakCount = 10;
            maxMeanMzError = 0.1;
            var tolerance = fragmentTolerance;

            string spectrumListFilters = String.Empty;
            Invoke(new MethodInvoker(() =>
            {
                tolerance.value = Convert.ToDouble(fragmentToleranceTextBox.Text);
                tolerance.units = (MZTolerance.Units) fragmentToleranceUnitsComboBox.SelectedIndex;
                meanMzErrorGraphControl.GraphPane.YAxis.Title.Text = "Mean m/z error (" + tolerance.units.ToString() + ")";

                spectrumListFilters = spectrumFiltersTextBox.Text;
                setAutomaticScales();
            }));

            var points = new PointPairList();

            string currentSourceName = null;
            string currentSourcePath = null;
            msdata.MSData msd = null;

            lock (owner)
            foreach (var row in spectrumRows)
            {
                if (row.SourceName != currentSourceName)
                {
                    currentSourceName = row.SourceName;
                    currentSourcePath = IDPickerForm.LocateSpectrumSource(currentSourceName, session.Connection.GetDataSource());
                    if (String.IsNullOrEmpty(currentSourcePath))
                        throw new FileNotFoundException("source file not found");
                    msd = new pwiz.CLI.msdata.MSDataFile(currentSourcePath);

                    //var param = session.Query<AnalysisParameter>().Where(o => o.Name == "SpectrumListFilters").Min(o => o.Value);
                    //string spectrumListFilters = String.IsNullOrEmpty(param) ? String.Empty : param;
                    SpectrumListFactory.wrap(msd, spectrumListFilters.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries));
                }

                string spectrumId = String.Format("{0}/{1}", row.SourceName, msdata.id.abbreviate(row.Spectrum.NativeID));

                var spectrumList = msd.run.spectrumList;

                ++spectraCount;

                var pwizPeptide = new proteome.Peptide(row.ModifiedSequence, proteome.ModificationParsing.ModificationParsing_Auto,
                                                       proteome.ModificationDelimiter.ModificationDelimiter_Brackets);
                var fragmentation = pwizPeptide.fragmentation(true, true);

                var pwizSpectrum = spectrumList.spectrum(spectrumList.find(row.Spectrum.NativeID), true);
                var pointMap = new seems.PointMap(new ZedGraph.PointPairList(pwizSpectrum.getMZArray().data, pwizSpectrum.getIntensityArray().data));
                double tic = pointMap.Values.Sum();

                var percentTicByFragmentType = new List<double>(Enumerable.Repeat(0.0, (int) IonSeries.Count));
                var percentPeakCountByFragmentType = new List<double>(Enumerable.Repeat(0.0, (int) IonSeries.Count));
                var matchCountByFragmentType = new List<int>(Enumerable.Repeat(0, (int) IonSeries.Count));
                var meanMzErrorByFragmentType = new List<double>(Enumerable.Repeat(Double.NaN, (int) IonSeries.Count));

                seems.PointMap.Enumerator itr;
                double expected;
                IonSeries[] ionSeries = Enum.GetValues(typeof(IonSeries)).Cast<IonSeries>().Where(o => o != IonSeries.Count).ToArray();

                for (int z = 1; z <= 1; ++z)
                for (int length = 1, end = pwizPeptide.sequence.Length; length <= end; ++length)
                foreach (IonSeries series in ionSeries)
                {
                    if ((series == IonSeries.c || series == IonSeries.x) &&
                        length == pwizPeptide.sequence.Length)
                        continue;

                    expected = fragmentMass(fragmentation, series, length, z);
                    itr = pointMap.FindNear(expected, expected - (expected - tolerance));
                    if (itr != null && itr.IsValid)
                    {
                        percentTicByFragmentType[(int)series] += itr.Current.Value;
                        ++percentPeakCountByFragmentType[(int)series];
                        ++matchCountByFragmentType[(int)series];
                        if (Double.IsNaN(meanMzErrorByFragmentType[(int)series])) meanMzErrorByFragmentType[(int)series] = 0;
                        meanMzErrorByFragmentType[(int)series] += mzError(itr.Current.Key, expected);
                    }
                }

                var rng = new Random();

                for (int i = 0; i < percentTicBySpectrumByFragmentType.Count; ++i)
                {
                    // convert sum to mean
                    if (percentPeakCountByFragmentType[i] > 0)
                        meanMzErrorByFragmentType[i] /= matchCountByFragmentType[i];

                    // convert to percentages
                    percentTicByFragmentType[i] /= tic / 100;
                    percentPeakCountByFragmentType[i] /= pointMap.Count / 100.0;

                    maxPercentTic = Math.Max(maxPercentTic, percentTicByFragmentType[i]);
                    maxPercentPeakCount = Math.Max(maxPercentPeakCount, percentPeakCountByFragmentType[i]);

                    double jitter = (rng.NextDouble() - 0.5);
                    percentTicBySpectrumByFragmentType[i].Add(jitter, percentTicByFragmentType[i], String.Format("{0}: {1:G4}% ({2} matches)", spectrumId, percentTicByFragmentType[i], matchCountByFragmentType[i]));
                    percentPeakCountBySpectrumByFragmentType[i].Add(jitter, percentPeakCountByFragmentType[i], String.Format("{0}: {1:G4}% ({2} matches)", spectrumId, percentPeakCountByFragmentType[i], matchCountByFragmentType[i]));

                    percentTicListByFragmentType[i].Add(percentTicByFragmentType[i]);
                    percentPeakCountListByFragmentType[i].Add(percentPeakCountByFragmentType[i]);

                    if (!Double.IsNaN(meanMzErrorByFragmentType[i]))
                    {
                        maxMeanMzError = Math.Max(maxMeanMzError, Math.Abs(meanMzErrorByFragmentType[i]));
                        meanMzErrorBySpectrumByFragmentType[i].Add(jitter, meanMzErrorByFragmentType[i], String.Format("{0}: {1:G4}%", spectrumId, meanMzErrorByFragmentType[i]));
                        meanMzErrorListByFragmentType[i].Add(meanMzErrorByFragmentType[i]);
                    }
                }

                if ((spectraCount % 100) == 0)
                    setAutomaticScales();
            } // for each spectrum row

            Invoke(new MethodInvoker(() =>
            {
                for (int i = 0; i < percentTicBySpectrumByFragmentType.Count; ++i)
                {
                    if (percentTicListByFragmentType[i].Count < 5)
                        continue;
                    percentTicListByFragmentType[i].Sort();
                    percentPeakCountListByFragmentType[i].Sort();
                    addSixNumberSummary(percentTicGraphControl.MasterPane.PaneList[i + 1], percentTicListByFragmentType[i]);
                    addSixNumberSummary(percentPeakCountGraphControl.MasterPane.PaneList[i + 1], percentPeakCountListByFragmentType[i]);

                    if (meanMzErrorListByFragmentType[i].Count < 5)
                        continue;
                    meanMzErrorListByFragmentType[i].Sort();
                    addSixNumberSummary(meanMzErrorGraphControl.MasterPane.PaneList[i + 1], meanMzErrorListByFragmentType[i]);
                }
            }));
        }

        private void setAutomaticScales()
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => setAutomaticScales()));
                return;
            }

            if (meanMzErrorGraphForm.IsHidden)
            {
                meanMzErrorGraphForm.Show(dockPanel, DockState.Document);
                percentTicGraphForm.Show(dockPanel, DockState.Document);
                percentPeakCountGraphForm.Show(dockPanel, DockState.Document);
                lastActiveGraphForm.Activate();
            }
            
            foreach (var graphControl in graphControls)
                graphControl.MasterPane.AxisChange();

            if (!lockZoomCheckBox.Checked)
                for (int i = 0; i <= (int) IonSeries.Count; ++i)
                {
                    percentTicGraphControl.MasterPane.PaneList[i].YAxis.Scale.Max = Math.Min(100, maxPercentTic + 3);
                    percentTicGraphControl.MasterPane.PaneList[i].YAxis.Scale.Min = 0;
                    percentPeakCountGraphControl.MasterPane.PaneList[i].YAxis.Scale.Max = Math.Min(100, maxPercentPeakCount + 3);
                    percentPeakCountGraphControl.MasterPane.PaneList[i].YAxis.Scale.Min = 0;
                    meanMzErrorGraphControl.MasterPane.PaneList[i].YAxis.Scale.Max = maxMeanMzError + maxMeanMzError*0.05;
                    meanMzErrorGraphControl.MasterPane.PaneList[i].YAxis.Scale.Min = -meanMzErrorGraphControl.MasterPane.PaneList[i].YAxis.Scale.Max;
                }

            foreach (var graphControl in graphControls)
                graphControl.Refresh();
        }

        public void SetData (NHibernate.ISession session, DataFilter dataFilter)
        {
            this.session = session;
            viewFilter = dataFilter;
            this.dataFilter = new DataFilter(dataFilter);

            ClearData();

            foreach (var o in Controls.OfType<Control>()) o.Enabled = true;
        }

        public void ClearData ()
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => ClearData()));
                return;
            }

            refreshDataLabel.Visible = true;

            percentTicGraphControl.GraphPane.CurveList.Clear();
            percentPeakCountGraphControl.GraphPane.CurveList.Clear();
            meanMzErrorGraphControl.GraphPane.CurveList.Clear();

            lastActiveGraphForm = dockPanel.LastActiveContent as DockableForm ?? meanMzErrorGraphForm;
            percentTicGraphForm.Hide();
            percentPeakCountGraphForm.Hide();
            meanMzErrorGraphForm.Hide();

            Text = TabText = "Fragmentation Statistics";
            Refresh();
        }

        public void ClearData (bool clearBasicFilter)
        {
            if (clearBasicFilter)
            {
                //basicDataFilter = null;
                foreach (var o in Controls.OfType<Control>()) o.Enabled = false;
            }

            ClearData();
        }

        void setData (object sender, DoWorkEventArgs e)
        {
            try
            {
                getFragmentationStatistics();
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        void renderData (object sender, RunWorkerCompletedEventArgs e)
        {
            Text = TabText = "Fragmentation Statistics";

            foreach (var o in Controls.OfType<Control>()) o.Enabled = true;

            if (e.Result is Exception)
            {
                ClearData(true);

                // no error if the user canceled when locating the source
                if (e.Result is FileNotFoundException)
                    return;

                Program.HandleException(e.Result as Exception);
            }

            setAutomaticScales();
        }

        #region Export stuff
        private void clipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //var table = getFormTable();

            //TableExporter.CopyToClipboard(table);
        }

        private void fileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //var table = getFormTable();

            //TableExporter.ExportToFile(table);
        }

        private void exportButton_Click(object sender, EventArgs e)
        {
            /*if (dataGridView.SelectedCells.Count > 1)
            {
                exportMenu.Items[0].Text = "Copy Selected to Clipboard";
                exportMenu.Items[1].Text = "Export Selected to File";
                exportMenu.Items[2].Text = "Show Selected in Excel";
            }
            else
            {
                exportMenu.Items[0].Text = "Copy to Clipboard";
                exportMenu.Items[1].Text = "Export to File";
                exportMenu.Items[2].Text = "Show in Excel";
            }*/

            exportMenu.Show(Cursor.Position);
        }

        /*private List<List<string>> getFormTable()
        {
            var table = new List<List<string>>();
            var row = new List<string>();

            if (dataGridView.SelectedCells.Count > 1)
            {
                var rowList = new List<int>();
                var columnList = new List<int>();

                foreach (DataGridViewCell cell in dataGridView.SelectedCells)
                {
                    if (!rowList.Contains(cell.RowIndex))
                        rowList.Add(cell.RowIndex);
                    if (!columnList.Contains(cell.ColumnIndex))
                        columnList.Add(cell.ColumnIndex);
                }
                rowList.Sort();
                columnList.Sort();

                //get column names
                for (int x = 0; x < columnList.Count; x++)
                    row.Add(dataGridView.Columns[columnList[x]].HeaderText);

                table.Add(row);
                row = new List<string>();

                //Retrieve all items
                for (int tableRow = 0; tableRow < rowList.Count; tableRow++)
                {
                    //row.Add(dataGridView.Rows[tableRow].HeaderCell.Value.ToString());
                    for (int x = 0; x < columnList.Count; x++)
                        row.Add(dataGridView[columnList[x], rowList[tableRow]].Value.ToString());

                    table.Add(row);
                    row = new List<string>();
                }
            }
            else
            {
                //get column names
                for (int x = 0; x < dataGridView.Columns.Count; x++)
                    row.Add(dataGridView.Columns[x].HeaderText);

                table.Add(row);
                row = new List<string>();

                //Retrieve all items
                for (int tableRow = 0; tableRow < dataGridView.Rows.Count; tableRow++)
                {
                    //row.Add(dataGridView.Rows[tableRow].HeaderCell.Value.ToString());
                    for (int x = 0; x < dataGridView.Columns.Count; x++)
                        row.Add(dataGridView[x, tableRow].Value.ToString());

                    table.Add(row);
                    row = new List<string>();
                }
            }

            return table;
        }*/

        private void showInExcelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //var table = getFormTable();

            //TableExporter.ShowInExcel(table);
        }
        #endregion

        bool spectrumFiltersDirty = false;
        private void spectrumFiltersTextBox_TextChanged (object sender, EventArgs e)
        {
            spectrumFiltersDirty = true;
        }

        private void spectrumFiltersTextBox_Leave (object sender, EventArgs e)
        {
            if (spectrumFiltersDirty)
            {
                spectrumFiltersDirty = false;
                //basicDataFilter = null; // force refresh
                SetData(session, viewFilter);
            }
        }

        private void refreshButton_Click (object sender, EventArgs e)
        {
            Text = TabText = "Loading fragmentation statistics...";

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

        public void ClearSession()
        {
            ClearData(true);
            if (session != null && session.IsOpen)
            {
                session.Close();
                session.Dispose();
                session = null;
            }
        }

        private void fragmentToleranceTextBox_KeyDown (object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Decimal || e.KeyCode == Keys.OemPeriod)
            {
                if ((sender as Control).Text.Length == 0 || (sender as Control).Text.Contains("."))
                    e.SuppressKeyPress = true;
            }
            else if (!(e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9 ||
                    e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9 ||
                    e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back ||
                    e.KeyCode == Keys.Left || e.KeyCode == Keys.Right))
                e.SuppressKeyPress = true;
        }
    }
}
