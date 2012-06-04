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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZedGraph;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class AlignmentForm : Form
    {
        private LoadedRetentionTimes _loadedRetentionTimes;
        private BindingList<DataRow> _dataRows = new BindingList<DataRow>();
        public AlignmentForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            SkylineWindow = skylineWindow;
            Icon = Resources.Skyline;
            bindingSource.DataSource = _dataRows;
            colIntercept.CellTemplate.Style.Format = "0.0000";
            colSlope.CellTemplate.Style.Format = "0.0000";
        }

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
        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (SkylineWindow != null)
            {
                SkylineWindow.DocumentUIChangedEvent -= SkylineWindowOnDocumentUIChangedEvent;
            }
            base.OnHandleDestroyed(e);
        }

        private void SkylineWindowOnDocumentUIChangedEvent(object sender, DocumentChangedEventArgs documentChangedEventArgs)
        {
            if (_loadedRetentionTimes != null && !_loadedRetentionTimes.IsValidFor(Document))
            {
                _loadedRetentionTimes = null;
            }
            UpdateAll();
        }

        public void UpdateAll()
        {
            UpdateRetentionTimes(null);
            UpdateCombo();
        }
        public void UpdateGraph()
        {
            zedGraphControl.GraphPane.CurveList.Clear();
            zedGraphControl.GraphPane.GraphObjList.Clear();
            var currentRow = bindingSource.Current as DataRow;
            if (currentRow != null)
            {
                var alignedFile = currentRow.AlignedFile;
                var points = new PointPairList();
                var outliers = new PointPairList();
                HashSet<int> outlierIndexes;
                string pointsLabel;
                var peptideTimes = alignedFile.Regression.PeptideTimes;
                if (alignedFile.RegressionRefined != null)
                {
                    outlierIndexes = alignedFile.OutlierIndexes;
                    pointsLabel = "Peptides Refined";
                }
                else
                {
                    outlierIndexes = new HashSet<int>();
                    pointsLabel = "Peptides";
                }
                for (int i = 0; i < peptideTimes.Count; i++)
                {
                    var peptideTime = peptideTimes[i];
                    var point =
                        new PointPair(alignedFile.OriginalTimes.GetRetentionTime(peptideTime.PeptideSequence).Value,
                                      peptideTime.RetentionTime, peptideTime.PeptideSequence);
                    if (outlierIndexes.Contains(i))
                    {
                        outliers.Add(point);
                    }
                    else
                    {
                        points.Add(point);
                    }
                }

                if (outliers.Count > 0)
                {
                    var curveOut = zedGraphControl.GraphPane.AddCurve("Outliers", outliers, Color.Black,
                                                                          SymbolType.Diamond);
                    curveOut.Symbol.Size = 8f;
                    curveOut.Line.IsVisible = false;
                    curveOut.Symbol.Border.IsVisible = false;
                    curveOut.Symbol.Fill = new Fill(RTLinearRegressionGraphPane.COLOR_OUTLIERS);

                }
                var scatterCurve = zedGraphControl.GraphPane.AddCurve(pointsLabel, points,
                                                                      Color.Black, SymbolType.Diamond);
                scatterCurve.Symbol.Size = 8f;
                scatterCurve.Line.IsVisible = false;
                scatterCurve.Symbol.Border.IsVisible = false;
                scatterCurve.Symbol.Fill = new Fill(RTLinearRegressionGraphPane.COLOR_REFINED);
                if (points.Count > 0)
                {
                    double xMin = points.Select(p => p.X).Min();
                    double xMax = points.Select(p => p.X).Max();
                    var regressionLine = zedGraphControl.GraphPane
                        .AddCurve("Regression line", new[] { xMin, xMax },
                            new[] { alignedFile.RegressionLine.GetY(xMin), alignedFile.RegressionLine.GetY(xMax) },
                            Color.Black);
                    regressionLine.Symbol.IsVisible = false;
                }
                zedGraphControl.GraphPane.Title.Text = string.Format("Alignment of {0} to {1}",
                    Path.GetFileNameWithoutExtension(currentRow.DataFile),
                    Path.GetFileNameWithoutExtension(((DataFileKey)comboAlignAgainst.SelectedItem).DataFilePath));
                zedGraphControl.GraphPane.XAxis.Title.Text 
                    = string.Format("Time from {0}", Path.GetFileNameWithoutExtension(currentRow.DataFile));
                zedGraphControl.GraphPane.YAxis.Title.Text = "Aligned time";
                zedGraphControl.GraphPane.AxisChange();
                zedGraphControl.Invalidate();
            }
        }

        public void UpdateRows()
        {
            var newRows = GetRows();
            if (newRows.SequenceEqual(_dataRows))
            {
                return;
            }
            _dataRows.RaiseListChangedEvents = false;
            _dataRows.Clear();
            foreach (var row in newRows)
            {
                _dataRows.Add(row);
            }
            _dataRows.RaiseListChangedEvents = true;
            _dataRows.ResetBindings();
            UpdateGraph();
        }
        private void UpdateCombo()
        {
            var items = new List<DataFileKey>();
            if (_loadedRetentionTimes != null)
            {
                foreach (var libEntry in _loadedRetentionTimes.LibraryRetentionTimesByPath)
                {
                    var filePaths = libEntry.Value.Keys.ToArray();
                    Array.Sort(filePaths);
                    items.AddRange(filePaths.Select(filePath=>new DataFileKey(libEntry.Key, filePath)));
                }
            }
            if (items.SequenceEqual(comboAlignAgainst.Items.Cast<DataFileKey>()))
            {
                return;
            }
            var selectedIndex = comboAlignAgainst.SelectedIndex;
            comboAlignAgainst.Items.Clear();
            comboAlignAgainst.Items.AddRange(items.Cast<object>().ToArray());
            ComboHelper.AutoSizeDropDown(comboAlignAgainst);
            if (comboAlignAgainst.Items.Count > 0)
            {
                if (selectedIndex < 0)
                {
                    if (SkylineWindow.SelectedResultsIndex >= 0)
                    {
                        var chromatograms =
                            Document.Settings.MeasuredResults.Chromatograms[SkylineWindow.SelectedResultsIndex];
                        selectedIndex =
                            items.IndexOf(
                                dataFileKey => MeasuredResults.IsBaseNameMatch
                                (
                                    Path.GetFileNameWithoutExtension(chromatograms.Name),
                                    Path.GetFileNameWithoutExtension(dataFileKey.DataFilePath)
                                )
                            );
                    }
                }
                comboAlignAgainst.SelectedIndex = Math.Min(comboAlignAgainst.Items.Count - 1, 
                    Math.Max(0, selectedIndex));
            }
            UpdateRows();
        }

        private IList<DataRow> GetRows()
        {
            LibraryRetentionTimes targetTimes = null;
            if (_loadedRetentionTimes != null)
            {
                var targetKey = comboAlignAgainst.SelectedItem as DataFileKey?;
                if (targetKey.HasValue)
                {
                    var libEntry = _loadedRetentionTimes.LibraryRetentionTimesByPath.FirstOrDefault(
                            kvp => Equals(kvp.Key, targetKey.Value.Library));
                    if (libEntry.Value != null)
                    {
                        libEntry.Value.TryGetValue(targetKey.Value.DataFilePath, out targetTimes);
                    }
                }
                
            }
            if  (targetTimes == null)
            {
                return new DataRow[0];
            }
            RetentionTimesAlignedToFile retentionTimesAlignedToFile =
                _loadedRetentionTimes.GetRetentionTimesAlignedToFile(targetTimes);
            return retentionTimesAlignedToFile.AlignedFiles.Select(alignedFile => new DataRow(alignedFile)).ToArray();
        }

        private void UpdateRetentionTimes(LoadedRetentionTimes newValue)
        {
            if (_loadedRetentionTimes != null)
            {
                Debug.Assert(_loadedRetentionTimes.IsValidFor(Document));
                return;
            }
            if (newValue != null && newValue.IsValidFor(Document))
            {
                _loadedRetentionTimes = newValue;
                return;
            }
            LoadedRetentionTimes.StartLoadFromAllLibraries(Document).ContinueWith(task =>
            {
                if (_loadedRetentionTimes == null)
                {
                    UpdateRetentionTimes(task.Result);
                    if (_loadedRetentionTimes != null)
                    {
                        UpdateAll();
                    }
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        internal class DataRow
        {
            public DataRow(AlignedFile alignedFile)
            {
                AlignedFile = alignedFile;
            }
            public AlignedFile AlignedFile { get; private set; }
            public string Library { get { return AlignedFile.OriginalLibrary.Name; } }
            public String DataFile { get { return Path.GetFileNameWithoutExtension(AlignedFile.OriginalTimes.Name); } }
            public double Slope
            {
                get { return AlignedFile.RegressionLine.Slope; }
            }

            public double Intercept { get { return AlignedFile.RegressionLine.Intercept; } }
#region Object Overrides
            public bool Equals(DataRow other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Equals(other.AlignedFile, AlignedFile);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != typeof (DataRow)) return false;
                return Equals((DataRow) obj);
            }

            public override int GetHashCode()
            {
                return AlignedFile.GetHashCode();
            }
#endregion
        }

        internal struct DataFileKey
        {
            public DataFileKey(Library library, string path) : this()
            {
                Library = library;
                DataFilePath = path;
            }

            public Library Library { get; private set; }
            public string DataFilePath { get; private set; }
            public override string ToString()
            {
                return string.Format("{0}:{1}", Library.Name, Path.GetFileNameWithoutExtension(DataFilePath));
            }
        }

        private void comboAlignAgainst_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateRows();
        }

        private void bindingSource_CurrentChanged(object sender, EventArgs e)
        {
            UpdateGraph();
        }
    }
}
