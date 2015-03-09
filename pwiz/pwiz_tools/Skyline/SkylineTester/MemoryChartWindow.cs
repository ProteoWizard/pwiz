/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using ZedGraph;

namespace SkylineTester
{
    public partial class MemoryChartWindow : Form
    {
        private static MemoryChartWindow _memoryChartWindow;
        private static readonly GraphPane _graphPane = new GraphPane();
        private static readonly PointPairList _pointList = new PointPairList();

        protected MemoryChartWindow()
        {
            InitializeComponent();

            _graphPane.Title.IsVisible = false;
            _graphPane.Chart.Border.IsVisible = false;
            _graphPane.Y2Axis.IsVisible = false;
            _graphPane.X2Axis.IsVisible = false;
            _graphPane.XAxis.MajorTic.IsOpposite = false;
            _graphPane.YAxis.MajorTic.IsOpposite = false;
            _graphPane.XAxis.MinorTic.IsOpposite = false;
            _graphPane.YAxis.MinorTic.IsOpposite = false;
            _graphPane.IsFontsScaled = false;
            _graphPane.XAxis.Scale.MaxGrace = 0.05;
            _graphPane.YAxis.Scale.MaxGrace = 0.1;
            _graphPane.XAxis.Scale.FontSpec.Angle = 90;
        }

        public static void Start(string logFile)
        {
            // Clear out memory log file.
            using (File.CreateText(logFile))
            {
            }

            var worker = new BackgroundWorker();
            worker.DoWork += (sender, args) =>
            {
                while (true)
                {
                    Thread.Sleep(500);

                    try
                    {
                        string[] lines;
                        using (var reader = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var stringReader = new StreamReader(reader))
                        {
                            lines = stringReader.ReadToEnd().Split('\n');
                        }

                        lock (_graphPane)
                        {
                            _pointList.Clear();
                            foreach (var line in lines)
                            {
                                int megabytes;
                                if (int.TryParse(line, out megabytes))
                                    _pointList.Add(_pointList.Count, megabytes);
                            }
                            _graphPane.AxisChange();
                            if (_memoryChartWindow != null)
                                _memoryChartWindow.graphControl.Invalidate();
                        }
                    }
// ReSharper disable once EmptyGeneralCatchClause
                    catch
                    {
                    }
                }
// ReSharper disable once FunctionNeverReturns
            };
            worker.RunWorkerAsync();
        }

        public static void ShowMemoryChart()
        {
            if (_memoryChartWindow != null)
                _memoryChartWindow.BringToFront();
            else
            {
                _memoryChartWindow = new MemoryChartWindow();
                _memoryChartWindow.Show();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            lock (_graphPane)
            {
                _memoryChartWindow = null;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            _graphPane.Rect = DisplayRectangle;
            base.OnResize(e);
        }

        private void MemoryChartWindow_Load(object sender, EventArgs e)
        {
            graphControl.GraphPane = _graphPane;

            _graphPane.Border.IsVisible = false;
            DoubleBuffered = true;

            _graphPane.CurveList.Clear();
            var curve = _graphPane.AddCurve("MB", _pointList, Color.Red, SymbolType.None);
            curve.Line.Width = 2;
            curve.Line.Fill = new Fill(Color.FromArgb(120,Color.Green), Color.FromArgb(230,Color.Red), -90);

            _graphPane.XAxis.MajorTic.IsAllTics = false;
            _graphPane.XAxis.MinorTic.IsAllTics = false;
            _graphPane.XAxis.Scale.IsVisible = false;
            _graphPane.XAxis.Scale.Format = "#";
            _graphPane.XAxis.Scale.Mag = 0;

            _graphPane.YAxis.MinorTic.IsAllTics = false;
            _graphPane.YAxis.Title.Text = "MB";
            _graphPane.YAxis.Scale.Format = "#";
            _graphPane.YAxis.Scale.Mag = 0;

            _graphPane.Legend.IsVisible = false;
        }
    }
}
