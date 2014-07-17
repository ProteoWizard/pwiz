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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.SettingsUI
{
    public partial class DiaIsolationWindowsGraphForm : FormEx
    {
        private const int TOTAL_CYCLES_SHOWN = 3;
        private const double Y_SCALE_MAX = TOTAL_CYCLES_SHOWN;
        private const double Y_SCALE_MIN = -0.25;
        private readonly double _smallesMz;
        private readonly List<BoxObj> _windows;
        private readonly List<BoxObj> _leftMargins;
        private readonly List<BoxObj> _rightMargins; 
        private readonly double _largestMz;
        private readonly Color _marginColor = Color.Salmon;
        private readonly Color _windowColor = Color.SteelBlue;
        private readonly Color _overlapRayColor = Color.FromArgb(100,70,130,180);
        private readonly Color _gapColor = Color.Red;
        private readonly Color _overlapColor = Color.Orange;
        private readonly List<BoxObj> _gapBoxes;
        private readonly List<BoxObj> _overlapBoxes;
        private readonly List<BoxObj> _overlapRayBoxes; 


        public DiaIsolationWindowsGraphForm(List<IsolationWindow> isolationWindows, bool usesMargins, object deconv, int windowsPerScan)
        {
            InitializeComponent();
            Icon = Resources.Skyline;
            if (isolationWindows.Count == 0) 
                return;

            bool overlap = Equals(deconv, EditIsolationSchemeDlg.DeconvolutionMethod.MSX_OVERLAP) ||
                           Equals(deconv, EditIsolationSchemeDlg.DeconvolutionMethod.OVERLAP);
            

            //Setup Graph
            zgIsolationGraph.GraphPane.Title.Text = Resources.DiaIsolationWindowsGraphForm_DiaIsolationWindowsGraphForm_Measurement_Windows;
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

            //Draw Lines and rectangles
            _windows = new List<BoxObj>(20);
            _leftMargins = new List<BoxObj>(20);
            _rightMargins = new List<BoxObj>(20);
            _smallesMz = Double.MaxValue;
            _largestMz = 0;
            var isolationWindowArray = isolationWindows.ToArray();  // ReSharper
            int windowCount = isolationWindowArray.Length;

            for (int cycle = 0; cycle < TOTAL_CYCLES_SHOWN; cycle ++)
            {
                int firstIndex = overlap && cycle%2 == 1 ? windowCount/2 : 0;
                int stopIndex = overlap && cycle%2 == 0 ? windowCount/2 : windowCount;
                for (int i = firstIndex; i < stopIndex; i++)
                {
                    IsolationWindow window = isolationWindowArray[i];
                    double windowY = cycle + (double) (i - firstIndex)/(stopIndex - firstIndex);
                    double windowX = window.Start;
                    double windowWidth = window.End - window.Start;
                    double windowHeight = 1.0/(stopIndex - firstIndex);    
                    BoxObj windowBox = new BoxObj(windowX,windowY,windowWidth,windowHeight,_windowColor,_windowColor)
                    {
                        IsClippedToChartRect = true
                    };
                    zgIsolationGraph.GraphPane.GraphObjList.Add(windowBox);
                    _windows.Add(windowBox);
                    if (usesMargins)
                    {
                        double marginLeftX = windowX - window.StartMargin ?? 0;
                        double marginLeftWidth = window.StartMargin ?? 0;
                        BoxObj marginLeftBox = new BoxObj(marginLeftX,windowY,marginLeftWidth,windowHeight,_marginColor,_marginColor)
                        {
                            IsClippedToChartRect = true
                        };
                        zgIsolationGraph.GraphPane.GraphObjList.Add(marginLeftBox);
                        _leftMargins.Add(marginLeftBox);

                        double marginRightX = windowX + windowWidth;
                        double marginRightWidth = window.EndMargin ?? window.StartMargin ?? 0;
                        BoxObj marginRightBox = new BoxObj(marginRightX, windowY, marginRightWidth, windowHeight, _marginColor, _marginColor)
                        {
                            IsClippedToChartRect = true
                        };
                        zgIsolationGraph.GraphPane.GraphObjList.Add(marginRightBox);
                        _rightMargins.Add(marginRightBox);
                        _largestMz = Math.Max(marginRightX + marginRightWidth, _largestMz);
                        _smallesMz = Math.Min(marginLeftX,_smallesMz);
                    }
                    _largestMz = Math.Max(_largestMz, windowX + windowWidth);
                    _smallesMz = Math.Min(_smallesMz, windowX);
                }    
            }
            
            _overlapRayBoxes = overlap ? new List<BoxObj>() : null;
            _gapBoxes= new List<BoxObj>();
            _overlapBoxes = new List<BoxObj>();

            
            for (int cycle = 0; cycle < TOTAL_CYCLES_SHOWN; cycle ++)
            {
                int currentCycleStart;
                int currentCycleCount;
                int nextCycleStart;
                int nextCycleCount;
                if (overlap)
                {
                    currentCycleStart = cycle * windowCount/2;
                    currentCycleCount = windowCount/2;
                    nextCycleStart = currentCycleStart + currentCycleCount;
                    nextCycleCount = windowCount/2;
                }
                else
                {
                    currentCycleStart = cycle * windowCount;
                    currentCycleCount = windowCount;
                    nextCycleStart = currentCycleStart + windowCount;
                    nextCycleCount = windowCount;
                }
                List<BoxObj> currentCycleWindows =
                    _windows.GetRange(currentCycleStart, currentCycleCount).OrderBy(o => Location.X).ToList();
                List<BoxObj> nextCycleWindows = cycle < TOTAL_CYCLES_SHOWN - 1
                    ? _windows.GetRange(nextCycleStart, nextCycleCount).OrderBy(o => Location.X).ToList()
                    : null;
                
                for (int i = 0; i < currentCycleWindows.Count; i++)
                {
                    BoxObj currentCycleCurrentBox = currentCycleWindows.ElementAt(i);
                    BoxObj currentCycleNextBox = i < currentCycleWindows.Count - 1 ? currentCycleWindows.ElementAt(i+1) : null;
                   
                    if (currentCycleNextBox != null)
                    {
                        checkGaps(currentCycleCurrentBox,currentCycleNextBox);
                        checkOverlaps(currentCycleCurrentBox,currentCycleNextBox);
                    }
                    
                    if (overlap && i%2 == 0 && nextCycleWindows != null)
                    {
                        int leftBoxIndex = cycle % 2 == 0 ? i : i - 1;
                        BoxObj nextCycleLeftBox = leftBoxIndex >= 0 ? nextCycleWindows.ElementAt(leftBoxIndex) : null;
                        int rightBoxIndex = cycle % 2 == 0 ? i + 1 : i;
                        BoxObj nextCycleRightBox = rightBoxIndex < nextCycleWindows.Count
                            ? nextCycleWindows.ElementAt(rightBoxIndex)
                            : null;
                        BoxObj rayBoxLeft = null;
                        BoxObj rayBoxRight = null;
                        if (nextCycleLeftBox != null)
                        {
                            rayBoxLeft = GetOverLapRay(currentCycleCurrentBox, nextCycleLeftBox);
                            if (rayBoxLeft != null)
                            {
                                zgIsolationGraph.GraphPane.GraphObjList.Add(rayBoxLeft);
                                _overlapRayBoxes.Add(rayBoxLeft);
                            }
                        }
                        if (nextCycleRightBox != null)
                        {
                            rayBoxRight = GetOverLapRay(currentCycleCurrentBox, nextCycleRightBox);
                            if (rayBoxRight != null)
                            {
                                zgIsolationGraph.GraphPane.GraphObjList.Add(rayBoxRight);
                                _overlapRayBoxes.Add(rayBoxRight);
                            }
                        }
                        if (rayBoxLeft != null && rayBoxRight == null)
                        {
                            rayBoxLeft.Location.X1 = currentCycleCurrentBox.Location.X1;
                            rayBoxLeft.Location.Width = currentCycleCurrentBox.Location.Width;
                        }
                        else if (rayBoxRight != null && rayBoxLeft == null)
                        {
                            rayBoxRight.Location.X1 = currentCycleCurrentBox.Location.X1;
                            rayBoxRight.Location.Width = currentCycleCurrentBox.Location.Width;
                        }
                        
                    }
                }
            }

            zgIsolationGraph.GraphPane.XAxis.Scale.Min = _smallesMz;
            zgIsolationGraph.GraphPane.XAxis.Scale.Max = _largestMz;
            zgIsolationGraph.GraphPane.YAxis.Scale.Min = Y_SCALE_MIN;
            zgIsolationGraph.GraphPane.YAxis.Scale.Max = Y_SCALE_MAX;
            zgIsolationGraph.AxisChange();
            zgIsolationGraph.Invalidate();

            //Setup check boxes and color labels
            if (usesMargins)
            {
                cbMargin.Checked = true;
            }
            else
            {
                cbMargin.Hide();
                labelMarginColor.Hide();
                int width = cbMargin.Width;
                cbShowOverlapRays.Left -= width;
                labelOverlapColor.Left -= width;
                cbShowOverlapsSingle.Left -= width;
                labelSingleOverlapColor.Left -= width;
                cbShowGaps.Left -= width;
                labelGapColor.Left -= width;
            }
            cbShowGaps.Checked = true;
            cbShowOverlapsSingle.Checked = true;
            if (overlap)
            {
                cbShowOverlapRays.Checked = true;
                labelOverlapColor.Visible = true;
            }
            else
            {
                cbShowOverlapRays.Hide();
                labelOverlapColor.Hide();
            }
            labelMarginColor.BackColor = _marginColor;
            labelWindowColor.BackColor = _windowColor;
            labelGapColor.BackColor = _gapColor;
            labelSingleOverlapColor.BackColor = _overlapColor;
            labelOverlapColor.BackColor = _overlapRayColor;
        }

        private BoxObj GetOverLapRay(GraphObj top, GraphObj bottom)
        {   

            if (bottom.Location.X2 > top.Location.X1 &&
                bottom.Location.X2 <= top.Location.X2)
            {
                double x1 = Math.Max(top.Location.X1, bottom.Location.X1);
                double y1 = top.Location.Y1;
                double width = bottom.Location.X2 - x1;
                double height = bottom.Location.Y1 - top.Location.Y1;
                return new BoxObj(x1,y1,width,height,
                    Color.FromArgb(0,0,0,0),_overlapRayColor)
                {
                    IsClippedToChartRect = true
                };
            }
            else if (bottom.Location.X1 < top.Location.X2 &&
                     bottom.Location.X1 >= top.Location.X1)
            {
                double x1 = bottom.Location.X1;
                double y1 = top.Location.Y1;
                double height = bottom.Location.Y1 - top.Location.Y1;
                double width = Math.Min(bottom.Location.X2, top.Location.X2) - x1;
                return new BoxObj(x1, y1, width, height, 
                    Color.FromArgb(0, 0, 0, 0), _overlapRayColor)
                {
                    IsClippedToChartRect = true
                
                };
            }
            else if (top.Location.X1 > bottom.Location.X1 && top.Location.X2 < bottom.Location.X2)
            {
                double x1 = top.Location.X1;
                double y1 = top.Location.Y1;
                double width = top.Location.Width;
                double height = bottom.Location.Y1 - top.Location.Y1;
                return new BoxObj(x1, y1, width, height,
                    Color.FromArgb(0, 0, 0, 0), _overlapRayColor)
                {
                    IsClippedToChartRect = true
                };
            }
            else
            {
                return null;
            }
        }
        
        private void checkGaps(GraphObj current,GraphObj next)
        {
            if (current.Location.X2 < next.Location.X1)
            {
                double gapWidth = next.Location.X1 - current.Location.X2;
                BoxObj gapBox1 = new BoxObj(current.Location.X2, Math.Floor(current.Location.Y), gapWidth, 1, _gapColor, _gapColor)
                {
                    IsClippedToChartRect = true
                };
                zgIsolationGraph.GraphPane.GraphObjList.Add(gapBox1);
                _gapBoxes.Add(gapBox1);
            }     
        }

        private void checkOverlaps(GraphObj current, GraphObj next)
        {
            if (current.Location.X2 > next.Location.X1)
            {
                double overlapWidth = current.Location.X2 - next.Location.X1;
                BoxObj overlapBox1 = new BoxObj(next.Location.X1, Math.Floor(current.Location.Y), overlapWidth, 1, _overlapColor, _overlapColor)
                {
                    IsClippedToChartRect = true
                };
                zgIsolationGraph.GraphPane.GraphObjList.Add(overlapBox1);
                _overlapBoxes.Add(overlapBox1);
            }
        }

        private void RescaleGraph()
        {
            if (zgIsolationGraph.GraphPane.XAxis.Scale.Min < _smallesMz)
                zgIsolationGraph.GraphPane.XAxis.Scale.Min = _smallesMz; 
            if (zgIsolationGraph.GraphPane.XAxis.Scale.Max > _largestMz)
                zgIsolationGraph.GraphPane.XAxis.Scale.Max = _largestMz;
            zgIsolationGraph.GraphPane.YAxis.Scale.Max = Y_SCALE_MAX;
            zgIsolationGraph.GraphPane.YAxis.Scale.Min = Y_SCALE_MIN;
            zgIsolationGraph.Refresh();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            zgIsolationGraph.GraphPane.Title.Text = cbMargin.Checked
                ? Resources.DiaIsolationWindowsGraphForm_DiaIsolationWindowsGraphForm_Measurement_Windows
                : Resources.DiaIsolationWindowsGraphForm_checkBox1_CheckedChanged_Extraction_Windows;
            
            foreach (BoxObj margin in _leftMargins)
            {
                margin.IsVisible = cbMargin.Checked;
            }
            foreach (BoxObj margin in _rightMargins)
            {
                margin.IsVisible = cbMargin.Checked;
            }
            zgIsolationGraph.Refresh();
        }

        private void zgIsolationWindow_ZoomEvent(ZedGraphControl sender, ZoomState oldstate, ZoomState newstate, PointF mousePosition)
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

        public List<BoxObj> LeftMargins
        {
            get { return _leftMargins; }
        }

        public List<BoxObj> RightMargins
        {
            get { return _rightMargins; }
        }

        public List<BoxObj> Windows
        {
            get { return _windows; }
        }

        public List<BoxObj> Gaps
        {
            get { return _gapBoxes; }
        }

        public List<BoxObj> Overlaps
        {
            get { return _overlapBoxes; }
        } 

        public int CyclesPerGraph
        {
            get { return TOTAL_CYCLES_SHOWN; }
        }


        private void zgIsolationGraph_ScrollEvent(object sender, ScrollEventArgs e)
        {
            RescaleGraph();
        }

        private void cbShowGaps_CheckedChanged(object sender, EventArgs e)
        {
            foreach (BoxObj gap in _gapBoxes)
                gap.IsVisible = cbShowGaps.Checked;
            zgIsolationGraph.Refresh();
        }

        private void cbShowOverlaps_CheckedChanged(object sender, EventArgs e)
        {
            foreach (BoxObj overlap in _overlapBoxes)
                overlap.IsVisible = cbShowOverlapsSingle.Checked;
            zgIsolationGraph.Refresh();
        }

        private void cbShowOverlapRays_CheckedChanged(object sender, EventArgs e)
        {
            foreach (BoxObj box in _overlapRayBoxes)
            {
                box.IsVisible = cbShowOverlapRays.Checked;
            }
            zgIsolationGraph.Refresh();
        }
    }
}
