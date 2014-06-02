/*
 * Original author: Max Horowitz-Gelb <maxhg .at. washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.SettingsUI
{
    public partial class DiaIsolationWindowsGraphForm : FormEx
    {
        private const int LINE_WIDTH = 3;

        private readonly double _smallesMz;
        private readonly List<LineObj> _windows; 
        private readonly List<LineObj> _leftMargins;
        private readonly List<LineObj> _rightMargins; 
        private readonly double _largestMz;

        public DiaIsolationWindowsGraphForm(IEnumerable<EditIsolationWindow> isolationWindows, string marginType, bool isIsolation)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            this.marginType = marginType;
            this.isIsolation = isIsolation;
            zgIsolationGraph.GraphPane.Title.Text = Resources.DiaIsolationWindowsGraphForm_DiaIsolationWindowsGraphForm_Isolation_Windows;
            zgIsolationGraph.GraphPane.XAxis.Title.Text = Resources.DiaIsolationWindowsGraphForm_DiaIsolationWindowsGraphForm_m_z;
            zgIsolationGraph.GraphPane.YAxis.Title.Text = Resources.DiaIsolationWindowsGraphForm_DiaIsolationWindowsGraphForm_Cycle;
            zgIsolationGraph.GraphPane.IsFontsScaled = false;
            zgIsolationGraph.GraphPane.YAxis.Scale.IsReverse = true;
            zgIsolationGraph.GraphPane.YAxisList[0].MajorTic.IsOpposite = false;
            zgIsolationGraph.GraphPane.YAxisList[0].MinorTic.IsOpposite = false;
            zgIsolationGraph.GraphPane.XAxis.MajorTic.IsOpposite = false;
            zgIsolationGraph.GraphPane.XAxis.MinorTic.IsOpposite = false;
            zgIsolationGraph.MasterPane.Border.IsVisible = false;
            zgIsolationGraph.GraphPane.YAxis.MajorGrid.IsZeroLine = false;
            zgIsolationGraph.GraphPane.Border.IsVisible = false;
            zgIsolationGraph.GraphPane.Chart.Border.IsVisible = false;
            _windows = new List<LineObj>(20);
            _leftMargins = new List<LineObj>(20);
            _rightMargins = new List<LineObj>(20);
            _smallesMz = Double.MaxValue;
            _largestMz = 0;

            var isolationWindowArray = isolationWindows.ToArray();  // ReSharper
            int windowCount = isolationWindowArray.Length;
            int windowNum = 0;
            foreach(EditIsolationWindow window in isolationWindowArray)
            {
                if (window.Start == null || window.End == null)
                    continue;
                
                double windowY = (double) windowNum/windowCount;
                double windowNextY = (double) windowNum/windowCount + 1;
                double startMargin = window.StartMargin ?? 0;
                double endMargin = startMargin;
                if (Equals(marginType, CalculateIsolationSchemeDlg.WindowMargin.ASYMMETRIC))
                    endMargin = window.EndMargin ?? 0;

                LineObj window1 = GetWindow(window, windowY);
                window1.IsClippedToChartRect = true;
                window1.Line.Width = LINE_WIDTH;
                LineObj window2 = GetWindow(window, windowNextY);
                window2.IsClippedToChartRect = true;
                window2.Line.Width = LINE_WIDTH;

                LineObj leftMargin1 = new LineObj(Color.Red, window1.Location.X1 - startMargin, windowY, window1.Location.X1, windowY)
                {
                    IsClippedToChartRect = true
                };
                LineObj leftMargin2 = new LineObj(Color.Red, window1.Location.X1 - startMargin, windowNextY, window1.Location.X1, windowNextY)
                {
                    IsClippedToChartRect = true
                };
                LineObj rightMargin1 = new LineObj(Color.Red, window1.Location.X2, windowY, window1.Location.X2 + endMargin, windowY)
                {
                    IsClippedToChartRect = true
                };
                LineObj rightMargin2 = new LineObj(Color.Red, window1.Location.X2, windowNextY, window1.Location.X2 + endMargin, windowNextY)
                {
                    IsClippedToChartRect = true
                };
                leftMargin1.Line.Width = LINE_WIDTH;
                leftMargin2.Line.Width = LINE_WIDTH;
                rightMargin1.Line.Width = LINE_WIDTH;
                rightMargin2.Line.Width = LINE_WIDTH;

                leftMargin1.IsVisible = false;
                rightMargin1.IsVisible = false;
                leftMargin2.IsVisible = false;
                rightMargin2.IsVisible = false;
                _windows.Add(window1);
                _windows.Add(window2);
                _rightMargins.Add(rightMargin1);
                _leftMargins.Add(leftMargin1);
                _rightMargins.Add(rightMargin2);
                _leftMargins.Add(leftMargin2);
                zgIsolationGraph.GraphPane.GraphObjList.Add(window1);
                zgIsolationGraph.GraphPane.GraphObjList.Add(leftMargin1);
                zgIsolationGraph.GraphPane.GraphObjList.Add(rightMargin1);
                zgIsolationGraph.GraphPane.GraphObjList.Add(window2);
                zgIsolationGraph.GraphPane.GraphObjList.Add(leftMargin2);
                zgIsolationGraph.GraphPane.GraphObjList.Add(rightMargin2);
                _largestMz = Math.Max(_largestMz,rightMargin1.Location.X2);
                _smallesMz = Math.Min(_smallesMz, leftMargin1.Location.X1);
                windowNum++;
            }
            zgIsolationGraph.GraphPane.XAxis.Scale.Min = _smallesMz;
            zgIsolationGraph.GraphPane.XAxis.Scale.Max = _largestMz;  
            zgIsolationGraph.GraphPane.YAxis.Scale.Min = -0.25;
            zgIsolationGraph.GraphPane.YAxis.Scale.Max = 2;
            zgIsolationGraph.AxisChange();
            zgIsolationGraph.Invalidate();
            if (!Equals(marginType, CalculateIsolationSchemeDlg.WindowMargin.NONE))
            {
                cbMargin.Checked = true;
            }
            else
            {
                cbMargin.Hide();
                zgIsolationGraph.Refresh();
            }
        }

        private LineObj GetWindow(EditIsolationWindow editWindow,double yPos)
        {
            double x1 = editWindow.Start ?? 0;
            double x2 = editWindow.End ?? 0;
            if (isIsolation)
            {
                if (Equals(marginType, CalculateIsolationSchemeDlg.WindowMargin.SYMMETRIC))
                {
                    x1 += editWindow.StartMargin ?? 0;
                    x2 -= editWindow.StartMargin ?? 0;
                }   
                else if (Equals(marginType, CalculateIsolationSchemeDlg.WindowMargin.ASYMMETRIC))
                {
                    x1 += editWindow.StartMargin ?? 0;
                    x2 -= editWindow.EndMargin ?? 0;
                }
            }
            return new LineObj(Color.Blue, x1, yPos, x2, yPos);
        }

        private void RescaleGraph()
        {
            if (zgIsolationGraph.GraphPane.XAxis.Scale.Min < _smallesMz)
                zgIsolationGraph.GraphPane.XAxis.Scale.Min = _smallesMz; 
            if (zgIsolationGraph.GraphPane.XAxis.Scale.Max > _largestMz)
                zgIsolationGraph.GraphPane.XAxis.Scale.Max = _largestMz;
            zgIsolationGraph.GraphPane.YAxis.Scale.Max = 2;
            zgIsolationGraph.GraphPane.YAxis.Scale.Min = -0.25;
            zgIsolationGraph.Refresh();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            zgIsolationGraph.GraphPane.Title.Text = cbMargin.Checked
                ? Resources.DiaIsolationWindowsGraphForm_DiaIsolationWindowsGraphForm_Isolation_Windows
                : Resources.DiaIsolationWindowsGraphForm_checkBox1_CheckedChanged_Extraction_Windows;
            
            foreach (LineObj margin in _leftMargins)
            {
                margin.IsVisible = cbMargin.Checked;
            }
            foreach (LineObj margin in _rightMargins)
            {
                margin.IsVisible = cbMargin.Checked;
            }
            zgIsolationGraph.Refresh();
        }

        private void zgIsolationWindow_ZoomEvent(ZedGraphControl sender, ZoomState oldstate, ZoomState newstate)
        {
            RescaleGraph();
        }

        private void zgIsolationGraph_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            ZedGraphHelper.BuildContextMenu(sender, menuStrip);
        }

        public void CloseButton()
        {
            btnClose.PerformClick();
        }

        public List<LineObj> LeftMargins
        {
            get { return _leftMargins; }
        }

        public List<LineObj> RightMargins
        {
            get { return _rightMargins; }
        }

        public List<LineObj> Windows
        {
            get { return _windows; }
        }

        private void zgIsolationGraph_ScrollEvent(object sender, ScrollEventArgs e)
        {
            RescaleGraph();
        } 
    }
}
