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
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using NHibernate;
using NHibernate.Linq;
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
        public event EventHandler<SourceNotFoundEventArgs> SourceNotFound;

        //public DataGridView DataGridView { get { return dataGridView; } }

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
                    var offsetMassDeltaPairs = ((string) queryRow[3]).Split(',');
                    foreach (var pair in offsetMassDeltaPairs)
                    {
                        var offsetAndMassDelta = pair.Split(':');
                        int offset = Convert.ToInt32(offsetAndMassDelta[0]);
                        if (!mods.ContainsKey(offset))
                            mods[offset] = new List<double>();
                        mods[offset].Add(Convert.ToDouble(offsetAndMassDelta[1]));
                    }
                }

                string format = String.Format("[{{0:f{0}}}]", 0);
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
            a, b, c, cMinus1,
            x, y, z, zPlus1, zPlus2,
            Count
        }

        private string[] IonSeriesLabels =
        {
            "a", "b", "c", "c-1",
            "x", "y", "z", "z+1", "z+2"
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

            zedGraphControl.MasterPane.PaneList.Clear();
            zedGraphControl.MasterPane.SetLayout(zedGraphControl.CreateGraphics(), 2, (int) IonSeries.Count + 1);
            zedGraphControl.MasterPane.InnerPaneGap = 0;
            zedGraphControl.MasterPane.Border.IsVisible = true;

            var axisPane1 = new GraphPane();
            axisPane1.Legend.IsVisible = false;
            axisPane1.IsFontsScaled = false;
            axisPane1.XAxis.IsVisible = false;
            axisPane1.YAxis.Scale.Min = 0;
            axisPane1.YAxis.Scale.Max = 100;
            axisPane1.YAxis.Title.Text = "%TIC";
            axisPane1.YAxis.Title.Gap = 0.05f;
            axisPane1.YAxis.MajorTic.IsOpposite = false;
            axisPane1.YAxis.MinorTic.IsOpposite = false;
            axisPane1.Chart.Border.IsVisible = false;
            axisPane1.Border.IsVisible = false;
            axisPane1.Margin.Left = 1;
            axisPane1.Margin.Right = 0;
            axisPane1.Title.Text = "Series:";
            zedGraphControl.MasterPane.Add(axisPane1);

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

            var axisPane2 = new GraphPane();
            axisPane2.Legend.IsVisible = false;
            axisPane2.IsFontsScaled = false;
            axisPane2.XAxis.IsVisible = false;
            axisPane2.YAxis.Scale.Min = 0;
            axisPane2.YAxis.Scale.Max = 100;
            axisPane2.YAxis.Title.Text = "%Peaks";
            axisPane2.YAxis.Title.Gap = 0.05f;
            axisPane2.YAxis.MajorTic.IsOpposite = false;
            axisPane2.YAxis.MinorTic.IsOpposite = false;
            axisPane2.Chart.Border.IsVisible = false;
            axisPane2.Border.IsVisible = false;
            axisPane2.Margin.Left = 1;
            axisPane2.Margin.Right = 0;
            axisPane2.Title.Text = "";
            zedGraphControl.MasterPane.Add(axisPane2);

            csr = new ColorSymbolRotator();
            for (int i = 0; i < (int) IonSeries.Count; ++i)
            {
                var graphPane = new GraphPane();
                graphPane.Title.IsVisible = false;
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
                mean.Symbol.Fill.Color = Color.Black;

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

        private IDPickerForm owner;
        private NHibernate.ISession session;

        private DataFilter viewFilter; // what the user has filtered on
        private DataFilter dataFilter; // how this view is filtered (i.e. never on its own rows)
        private DataFilter basicDataFilter; // the basic filter without the user filtering on rows

        private List<double> fragmentationStatistics, basicFragmentationStatistics;

        private void addSixNumberSummary(GraphPane graphPane, IList<double> sortedNumbers)
        {
            double mean = sortedNumbers.Sum() / sortedNumbers.Count;
            double min = sortedNumbers.First();
            double q1 = sortedNumbers[sortedNumbers.Count / 4];
            double median = sortedNumbers[sortedNumbers.Count / 2];
            double q3 = sortedNumbers[sortedNumbers.Count * 3 / 4];
            double max = sortedNumbers.Last();

            string label = String.Format("Max: {0:G4}%\n3rd Quartile: {1:G4}%\nMedian: {2:G4}%\n1st Quartile: {3:G4}%\nMin: {4:G4}%\nMean: {5:G4}%", max, q3, median, q1, min, mean);
            graphPane.CurveList[0].AddPoint(new PointPair(0, mean, label));
            graphPane.CurveList[1].AddPoint(new PointPair(0, max, q3, label));
            graphPane.CurveList[1].AddPoint(new PointPair(0, q1, min, label));
            graphPane.CurveList[2].AddPoint(new PointPair(0, q3, median, label));
            graphPane.CurveList[2].AddPoint(new PointPair(0, median, q1, label));
        }

        private List<double> getFragmentationStatistics ()
        {
            IList<object[]> queryRows;
            lock (session)
                queryRows = session.CreateQuery("SELECT psm.Spectrum.Source.Name, psm.Spectrum, psm, DISTINCT_GROUP_CONCAT(pm.Offset || ':' || mod.MonoMassDelta), psm.Peptide.Sequence " +
                                                viewFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch, DataFilter.PeptideSpectrumMatchToModification) +
                                                " GROUP BY psm.Spectrum.id ")
                                   .List<object[]>();
            var spectrumRows = queryRows.Select(o => new SpectrumRow(o)).Shuffle().Take(1000).OrderBy(o => o.SourceName);

            var percentTicBySpectrumByFragmentType = new List<PointPairList>();
            var percentPeakCountBySpectrumByFragmentType = new List<PointPairList>();
            var percentTicListByFragmentType = new List<List<double>>();
            var percentPeakCountListByFragmentType = new List<List<double>>();

            foreach (var pane in zedGraphControl.MasterPane.PaneList)
                foreach (var curve in pane.CurveList)
                    curve.Clear();

            for (int i = 0; i < (int) IonSeries.Count; ++i)
            {
                percentTicBySpectrumByFragmentType.Add(zedGraphControl.MasterPane.PaneList[i + 1].CurveList[3].Points as PointPairList);
                percentPeakCountBySpectrumByFragmentType.Add(zedGraphControl.MasterPane.PaneList[(int) IonSeries.Count + i + 2].CurveList[3].Points as PointPairList);
                percentTicListByFragmentType.Add(new List<double>());
                percentPeakCountListByFragmentType.Add(new List<double>());
            }

            int spectraCount = 0;
            double maxPercentTic = 10;
            double maxPercentPeakCount = 10;

            string spectrumListFilters = String.Empty;
            Invoke(new MethodInvoker(() =>
            {
                spectrumListFilters = spectrumFiltersTextBox.Text;
                if (!lockZoomCheckBox.Checked)
                    for (int i = 0; i <= (int) IonSeries.Count; ++i)
                    {
                        zedGraphControl.MasterPane.PaneList[i].YAxis.Scale.Max = maxPercentTic;
                        zedGraphControl.MasterPane.PaneList[(int) IonSeries.Count + i + 1].YAxis.Scale.Max = maxPercentPeakCount;
                    }
                zedGraphControl.MasterPane.AxisChange();
                zedGraphControl.Refresh();
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
                    currentSourcePath = locateSpectrumSource(currentSourceName);
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

                for (int z = 1; z <= 1; ++z)
                for (int length = 1, end = pwizPeptide.sequence.Length; length <= end; ++length)
                {
                    seems.PointMap.Enumerator itr;

                    // a
                    itr = pointMap.FindNear(fragmentation.a(length, z), 0.5);
                    if (itr != null && itr.IsValid)
                    {
                        percentTicByFragmentType[0] += itr.Current.Value;
                        ++percentPeakCountByFragmentType[0];
                    }

                    // b
                    itr = pointMap.FindNear(fragmentation.b(length, z), 0.5);
                    if (itr != null && itr.IsValid)
                    {
                        percentTicByFragmentType[1] += itr.Current.Value;
                        ++percentPeakCountByFragmentType[1];
                    }

                    if (length != pwizPeptide.sequence.Length)
                    {
                        // c
                        itr = pointMap.FindNear(fragmentation.c(length, z), 0.5);
                        if (itr != null && itr.IsValid)
                        {
                            percentTicByFragmentType[2] += itr.Current.Value;
                            ++percentPeakCountByFragmentType[2];
                        }

                        // c-1
                        itr = pointMap.FindNear(fragmentation.c(length, z) - Proton.Mass / z, 0.5);
                        if (itr != null && itr.IsValid)
                        {
                            percentTicByFragmentType[3] += itr.Current.Value;
                            ++percentPeakCountByFragmentType[3];
                        }

                        // x
                        itr = pointMap.FindNear(fragmentation.x(length, z), 0.5);
                        if (itr != null && itr.IsValid)
                        {
                            percentTicByFragmentType[4] += itr.Current.Value;
                            ++percentPeakCountByFragmentType[4];
                        }
                    }

                    // y
                    itr = pointMap.FindNear(fragmentation.y(length, z), 0.5);
                    if (itr != null && itr.IsValid)
                    {
                        percentTicByFragmentType[5] += itr.Current.Value;
                        ++percentPeakCountByFragmentType[5];
                    }

                    // z
                    itr = pointMap.FindNear(fragmentation.z(length, z), 0.5);
                    if (itr != null && itr.IsValid)
                    {
                        percentTicByFragmentType[6] += itr.Current.Value;
                        ++percentPeakCountByFragmentType[6];
                    }

                    // z+1
                    itr = pointMap.FindNear(fragmentation.zRadical(length, z), 0.5);
                    if (itr != null && itr.IsValid)
                    {
                        percentTicByFragmentType[7] += itr.Current.Value;
                        ++percentPeakCountByFragmentType[7];
                    }

                    // z+2
                    itr = pointMap.FindNear(fragmentation.zRadical(length, z) + Proton.Mass / z, 0.5);
                    if (itr != null && itr.IsValid)
                    {
                        percentTicByFragmentType[8] += itr.Current.Value;
                        ++percentPeakCountByFragmentType[8];
                    }
                }

                var rng = new Random();

                for (int i = 0; i < percentTicBySpectrumByFragmentType.Count; ++i)
                {
                    // convert to percentages
                    percentTicByFragmentType[i] /= tic / 100;
                    percentPeakCountByFragmentType[i] /= pointMap.Count / 100.0;

                    maxPercentTic = Math.Max(maxPercentTic, percentTicByFragmentType[i]);
                    maxPercentPeakCount = Math.Max(maxPercentPeakCount, percentPeakCountByFragmentType[i]);

                    double jitter = (rng.NextDouble() - 0.5);
                    percentTicBySpectrumByFragmentType[i].Add(jitter, percentTicByFragmentType[i], String.Format("{0}: {1:G4}%", spectrumId, percentTicByFragmentType[i]));
                    percentPeakCountBySpectrumByFragmentType[i].Add(jitter, percentPeakCountByFragmentType[i], String.Format("{0}: {1:G4}%", spectrumId, percentPeakCountByFragmentType[i]));

                    percentTicListByFragmentType[i].Add(percentTicByFragmentType[i]);
                    percentPeakCountListByFragmentType[i].Add(percentPeakCountByFragmentType[i]);
                }

                if ((spectraCount % 100) == 0)
                {
                    Invoke(new MethodInvoker(() =>
                    {
                        if (!lockZoomCheckBox.Checked && !zedGraphControl.GraphPane.IsZoomed)
                            for (int i = 0; i <= (int) IonSeries.Count; ++i)
                            {
                                zedGraphControl.MasterPane.PaneList[i].YAxis.Scale.Max = Math.Min(100, maxPercentTic + 3);
                                zedGraphControl.MasterPane.PaneList[(int) IonSeries.Count + i + 1].YAxis.Scale.Max = Math.Min(100, maxPercentPeakCount + 3);
                            }
                        zedGraphControl.MasterPane.AxisChange();
                        zedGraphControl.Refresh();
                    }));
                }
            }

            Invoke(new MethodInvoker(() =>
            {
                for (int i = 0; i < percentTicBySpectrumByFragmentType.Count; ++i)
                {
                    if (percentTicListByFragmentType[i].Count < 5)
                        continue;

                    percentTicListByFragmentType[i].Sort();
                    percentPeakCountListByFragmentType[i].Sort();

                    addSixNumberSummary(zedGraphControl.MasterPane.PaneList[i + 1], percentTicListByFragmentType[i]);
                    addSixNumberSummary(zedGraphControl.MasterPane.PaneList[(int) IonSeries.Count + i + 2], percentPeakCountListByFragmentType[i]);
                }

                if (!lockZoomCheckBox.Checked)
                {
                    for (int i = 0; i <= (int) IonSeries.Count; ++i)
                    {
                        zedGraphControl.MasterPane.PaneList[i].YAxis.Scale.Max = Math.Min(100, maxPercentTic + 3);
                        zedGraphControl.MasterPane.PaneList[(int) IonSeries.Count + i + 1].YAxis.Scale.Max = Math.Min(100, maxPercentPeakCount + 3);
                    }
                    zedGraphControl.ZoomOutAll(zedGraphControl.GraphPane);
                }
                zedGraphControl.MasterPane.AxisChange();
                zedGraphControl.Refresh();
            }));
            return new List<double>(); //percentTicBySpectrumByFragmentType[1];
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
            Text = TabText = "Fragmentation Statistics";

            //dataGridView.Rows.Clear();
            //dataGridView.Refresh();
            Refresh();
        }

        public void ClearData (bool clearBasicFilter)
        {
            if (clearBasicFilter)
                basicDataFilter = null;
            ClearData();
        }

        void setData (object sender, DoWorkEventArgs e)
        {
            try
            {
                if (dataFilter.IsBasicFilter)
                {
                    // refresh basic data when basicDataFilter is unset or when the basic filter values have changed
                    if (basicDataFilter == null || (dataFilter.IsBasicFilter && dataFilter != basicDataFilter))
                    {
                        basicDataFilter = new DataFilter(dataFilter);
                        basicFragmentationStatistics = getFragmentationStatistics();
                    }

                    fragmentationStatistics = basicFragmentationStatistics;
                }
                else
                    fragmentationStatistics = getFragmentationStatistics();
            }
            catch (Exception ex)
            {
                Invoke(new MethodInvoker(() => Program.HandleException(ex)));
            }
        }

        void renderData (object sender, RunWorkerCompletedEventArgs e)
        {
            Text = TabText = "Fragmentation Statistics";           

            //dataGridView.Rows.Clear();
            //dataGridView.Rows.Add(fragmentationStatistics.Cast<object>().ToArray());
            //dataGridView.Refresh();
        }

        string locateSpectrumSource (string spectrumSourceName)
        {
            try
            {
                return Util.FindSourceInSearchPath(spectrumSourceName, ".");
            }
            catch
            {
                try
                {
                    return Util.FindSourceInSearchPath(spectrumSourceName, Properties.Settings.Default.LastSpectrumSourceDirectory);
                }
                catch
                {
                    if (SourceNotFound != null)
                    {
                        var eventArgs = new SourceNotFoundEventArgs() {SourcePath = spectrumSourceName};
                        SourceNotFound(this, eventArgs);
                        if (System.IO.File.Exists(eventArgs.SourcePath) || System.IO.Directory.Exists(eventArgs.SourcePath))
                        {
                            Properties.Settings.Default.LastSpectrumSourceDirectory = System.IO.Path.GetDirectoryName(eventArgs.SourcePath);
                            Properties.Settings.Default.Save();
                            return eventArgs.SourcePath;
                        }
                    }

                    throw;
                }
            }
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
                basicDataFilter = null; // force refresh
                SetData(session, viewFilter);
            }
        }

        private void refreshButton_Click (object sender, EventArgs e)
        {
            Text = TabText = "Loading fragmentation statistics...";

            var workerThread = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            workerThread.DoWork += new DoWorkEventHandler(setData);
            workerThread.RunWorkerCompleted += new RunWorkerCompletedEventHandler(renderData);
            workerThread.RunWorkerAsync();
        }
    }
}
