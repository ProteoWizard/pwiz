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

    public partial class PeakStatisticsForm : DockableForm
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

        LineItem precursorScatterPlot, chargeReducedScatterPlot;

        public PeakStatisticsForm (IDPickerForm owner)
        {
            InitializeComponent();

            this.owner = owner;

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                e.Cancel = true;
                DockState = DockState.DockBottomAutoHide;
            };

            Text = TabText = "Peak Statistics";
            Icon = Properties.Resources.BlankIcon;

            var csr = new ColorSymbolRotator();
            var colors = new List<Color>() { Color.Gray };
            for (int i = 0; i < (int) IonSeries.Count; ++i)
                colors.Add(csr.NextColor);

            var graphPane = zedGraphControl.GraphPane;
            graphPane.Title.Text = "Peak m/z - precursor m/z";
            graphPane.XAxis.Title.Text = "m/z";
            graphPane.YAxis.Title.Text = "Intensity";
            graphPane.YAxis.MajorTic.IsOpposite = false;
            graphPane.YAxis.MinorTic.IsOpposite = false;
            graphPane.Legend.IsVisible = false;
            graphPane.IsFontsScaled = false;

            precursorScatterPlot = graphPane.AddCurve("", new PointPairList(), Color.Gray, SymbolType.Circle);
            precursorScatterPlot.Line.IsVisible = false;
            precursorScatterPlot.Symbol.IsAntiAlias = false;
            precursorScatterPlot.Symbol.Border.IsVisible = false;
            precursorScatterPlot.Symbol.Fill = new Fill(colors.ToArray()) { Type = FillType.GradientByColorValue, RangeMin = 0, RangeMax = (double) IonSeries.Count, SecondaryValueGradientColor = Color.Gray };
            precursorScatterPlot.Symbol.Size = 5f;

            graphPane = new GraphPane();
            graphPane.Title.Text = "Peak m/z - charge reduced precursor m/z";
            graphPane.XAxis.Title.Text = "m/z";
            graphPane.YAxis.Title.Text = "Intensity";
            graphPane.YAxis.MajorTic.IsOpposite = false;
            graphPane.YAxis.MinorTic.IsOpposite = false;
            graphPane.Legend.IsVisible = false;
            graphPane.IsFontsScaled = false;
            zedGraphControl.MasterPane.PaneList.Add(graphPane);

            chargeReducedScatterPlot = graphPane.AddCurve("", new PointPairList(), Color.Gray, SymbolType.Circle);
            chargeReducedScatterPlot.Line.IsVisible = false;
            chargeReducedScatterPlot.Symbol.IsAntiAlias = false;
            chargeReducedScatterPlot.Symbol.Border.IsVisible = false;
            chargeReducedScatterPlot.Symbol.Fill = new Fill(colors.ToArray()) { Type = FillType.GradientByColorValue, RangeMin = 0, RangeMax = (double) IonSeries.Count, SecondaryValueGradientColor = Color.Gray };
            chargeReducedScatterPlot.Symbol.Size = 5f;

            zedGraphControl.MasterPane.SetLayout(zedGraphControl.CreateGraphics(), PaneLayout.SingleColumn);
            zedGraphControl.MasterPane.InnerPaneGap = 0;

            zedGraphControl.MasterPane.AxisChange();
            zedGraphControl.Refresh();
        }

        private IDPickerForm owner;
        private NHibernate.ISession session;

        private DataFilter viewFilter; // what the user has filtered on
        private DataFilter dataFilter; // how this view is filtered (i.e. never on its own rows)
        private DataFilter basicDataFilter; // the basic filter without the user filtering on rows

        private List<double> fragmentationStatistics, basicPeakStatistics;

        private double fragmentMass(pwiz.CLI.proteome.Fragmentation f, IonSeries series, int length, int charge)
        {
            switch (series)
            {
                case IonSeries.a: return f.a(length, charge);
                case IonSeries.b: return f.b(length, charge);
                case IonSeries.c: return f.c(length, charge);
                case IonSeries.cMinus1: return f.c(length, charge) - Proton.Mass / charge;
                case IonSeries.x: return f.x(length, charge);
                case IonSeries.y: return f.y(length, charge);
                case IonSeries.z: return f.z(length, charge);
                case IonSeries.zPlus1: return f.zRadical(length, charge);
                case IonSeries.zPlus2: return f.zRadical(length, charge) + Proton.Mass / charge;
                default: throw new ArgumentException();
            }
        }

        private List<double> getPeakStatistics ()
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

            precursorScatterPlot.Clear();
            chargeReducedScatterPlot.Clear();

            int spectraCount = 0;

            string spectrumListFilters = String.Empty;
            Invoke(new MethodInvoker(() =>
            {
                spectrumListFilters = spectrumFiltersTextBox.Text;
                zedGraphControl.MasterPane.AxisChange();
                zedGraphControl.Refresh();
            }));

            var points = new PointPairList();

            string currentSourceName = null;
            string currentSourcePath = null;
            msdata.MSData msd = null;

            lock(owner)
            foreach (var row in spectrumRows)
            {
                if (row.SourceName != currentSourceName)
                {
                    currentSourceName = row.SourceName;
                    currentSourcePath = IDPickerForm.LocateSpectrumSource(currentSourceName, session.Connection.GetDataSource());
                    msd = new pwiz.CLI.msdata.MSDataFile(currentSourcePath);

                    //var param = session.Query<AnalysisParameter>().Where(o => o.Name == "SpectrumListFilters").Min(o => o.Value);
                    //string spectrumListFilters = String.IsNullOrEmpty(param) ? String.Empty : param;
                    SpectrumListFactory.wrap(msd, spectrumListFilters.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries));
                }

                string label = String.Format("{0}/{1}\n{2}", row.SourceName, msdata.id.abbreviate(row.Spectrum.NativeID), row.ModifiedSequence);

                var spectrumList = msd.run.spectrumList;

                ++spectraCount;

                var pwizPeptide = new proteome.Peptide(row.ModifiedSequence, proteome.ModificationParsing.ModificationParsing_Auto,
                                                       proteome.ModificationDelimiter.ModificationDelimiter_Brackets);
                var fragmentation = pwizPeptide.fragmentation(true, true);

                var pwizSpectrum = spectrumList.spectrum(spectrumList.find(row.Spectrum.NativeID), true);
                var pointMap = new seems.PointMap(new ZedGraph.PointPairList(pwizSpectrum.getMZArray().data, pwizSpectrum.getIntensityArray().data));
                double tic = pointMap.Values.Sum();

                double precursorMz = row.Spectrum.PrecursorMZ;
                double chargeReducedPrecursorMz = precursorMz * row.PeptideSpectrumMatch.Charge;

                bool plotMatchedPeaks = true;
                bool removeMatchedPeaks = false;

                double tolerance = 0.03;
                seems.PointMap.Enumerator itr;
                IonSeries[] ionSeries = Enum.GetValues(typeof(IonSeries)).Cast<IonSeries>().Where(o => o != IonSeries.Count).ToArray();

                for (int z = 1; z <= 1; ++z)
                for (int length = 1, end = pwizPeptide.sequence.Length; length <= end; ++length)
                {
                    string NTermFragment = row.ModifiedSequence.Substring(0, length);
                    string CTermFragment = row.ModifiedSequence.Substring(row.ModifiedSequence.Length - length);

                    foreach (IonSeries series in ionSeries)
                    {
                        if ((series == IonSeries.c || series == IonSeries.cMinus1 || series == IonSeries.x) &&
                            length == pwizPeptide.sequence.Length)
                            continue;

                        itr = pointMap.FindNear(fragmentMass(fragmentation, series, length, z), tolerance);
                        if (itr != null && itr.IsValid)
                        {
                            if (plotMatchedPeaks)
                            {
                                precursorScatterPlot.AddPoint(new PointPair(itr.Current.Key - precursorMz, itr.Current.Value / tic, (int)series, String.Format("{0} {1}\n{2} {3} {4} {5}", label, precursorMz, NTermFragment, itr.Current.Key, IonSeriesLabels[(int)series], length)));
                                chargeReducedScatterPlot.AddPoint(new PointPair(itr.Current.Key - chargeReducedPrecursorMz, itr.Current.Value / tic, (int)series, String.Format("{0} {1}\n{2} {3} {4} {5}", label, chargeReducedPrecursorMz, NTermFragment, itr.Current.Key, IonSeriesLabels[(int)series], length)));
                            }

                            if (removeMatchedPeaks)
                                pointMap.Remove(itr);
                        }
                    }
                }

                foreach (var pair in pointMap)
                {
                    precursorScatterPlot.AddPoint(new PointPair(pair.Key - precursorMz, pair.Value/tic, 0, label));
                    chargeReducedScatterPlot.AddPoint(new PointPair(pair.Key - chargeReducedPrecursorMz, pair.Value/tic, 0, label));
                }

                if ((spectraCount % 100) == 0)
                {
                    Invoke(new MethodInvoker(() =>
                    {
                        zedGraphControl.MasterPane.AxisChange();
                        zedGraphControl.Refresh();
                    }));
                }
            }

            Invoke(new MethodInvoker(() =>
            {
                if (!lockZoomCheckBox.Checked)
                {
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

            foreach (var o in Controls.OfType<Control>()) o.Enabled = true;
        }

        public void ClearData ()
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => ClearData()));
                return;
            }

            Text = TabText = "Peak Statistics";

            Refresh();
        }

        public void ClearData (bool clearBasicFilter)
        {
            if (clearBasicFilter)
            {
                basicDataFilter = null;
                foreach (var o in Controls.OfType<Control>()) o.Enabled = false;
            }

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
                        basicPeakStatistics = getPeakStatistics();
                    }

                    fragmentationStatistics = basicPeakStatistics;
                }
                else
                    fragmentationStatistics = getPeakStatistics();
            }
            catch (Exception ex)
            {
                Invoke(new MethodInvoker(() => Program.HandleException(ex)));
            }
        }

        void renderData (object sender, RunWorkerCompletedEventArgs e)
        {
            Text = TabText = "Peak Statistics";

            foreach (var o in Controls.OfType<Control>()) o.Enabled = true;
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
            Text = TabText = "Loading peak statistics...";

            var workerThread = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            workerThread.DoWork += new DoWorkEventHandler(setData);
            workerThread.RunWorkerCompleted += new RunWorkerCompletedEventHandler(renderData);
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
    }
}
