/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model;
using ZedGraph;
using Settings = pwiz.Skyline.Controls.Graphs.DetectionsGraphController.Settings;

namespace pwiz.Skyline.Controls.Graphs
{
    public abstract class DetectionsPlotPane : SummaryReplicateGraphPane
    {
        protected Receiver<DetectionPlotData.WorkOrderParam, DetectionPlotData> Receiver;

        protected DetectionPlotData _detectionData = DetectionPlotData.INVALID;
        public int MaxRepCount { get; private set; }
        protected DetectionPlotData.DataSet TargetData => _detectionData.GetTargetData(Settings.TargetType);
        public override float YScale {
            get
            {
                if (Settings.YScaleFactor == DetectionsGraphController.YScaleFactorType.PERCENT)
                    return (float)TargetData.MaxCount / 100;
                else
                    return Settings.YScaleFactor.Value;
            }
        } 

        protected PaneProgressBar ProgressBar { get; private set; }

        protected DetectionsPlotPane(GraphSummary graphSummary) : base(graphSummary)
        {
            MaxRepCount = graphSummary.DocumentUIContainer.DocumentUI.MeasuredResults?.Chromatograms.Count ?? 0;

            Settings.RepCount = MaxRepCount / 2;
            if (GraphSummary.Toolbar is DetectionsToolbar toolbar)
                toolbar.UpdateUI();
             
            XAxis.Scale.Min = YAxis.Scale.Min = 0;
            XAxis.Scale.MinAuto = XAxis.Scale.MaxAuto = YAxis.Scale.MinAuto = YAxis.Scale.MaxAuto = false;
            ToolTip = new ToolTipImplementation(this);

            Receiver = DetectionPlotData.PRODUCER.RegisterCustomer(graphSummary, OnResultsAvailable);
            Receiver.ProgressChange += UpdateProgressHandler;
        }

        public void UpdateProgressHandler()
        {
            if (Receiver.IsProcessing())
            {
                ProgressBar ??= new PaneProgressBar(this);
                ProgressBar?.UpdateProgress(Receiver.GetProgressValue());
            }
            else
            {
                ProgressBar?.Dispose();
                ProgressBar = null;
            }
        }

        protected virtual void OnResultsAvailable()
        {
            var error = Receiver.GetError();
            if (error != null)
            {
                AddLabels();
            }
            else if (Receiver.IsProcessing())
            {
                ProgressBar = ProgressBar ?? new PaneProgressBar(this);
                ProgressBar.UpdateProgressUI(Receiver.GetProgressValue());
            }
            else
            {
                ProgressBar?.Dispose();
                ProgressBar = null;
            }
            GraphSummary.Toolbar.UpdateUI();
            UpdateGraph(false);
            GraphSummary.GraphControl.Invalidate();
            GraphSummary.GraphControl.Update();
        }

        public override bool HasToolbar { get { return true; } }


        public override void OnClose(EventArgs e)
        {
            Receiver.Dispose();
        }

        protected abstract void HandleMouseClick(int index);

        public override void HandleMouseClick(object sender, MouseEventArgs e)
        {
            if (sender is Control ctx)
            {
                using (var g = ctx.CreateGraphics())
                {
                    if (FindNearestObject(e.Location, g, out var nearestObject, out var index) && nearestObject is BarItem)
                    {
                        HandleMouseClick(index);
                    }
                }
            }
        }

        public override void Draw(Graphics g)
        {
            AxisChange(g);

            base.Draw(g);
        }

        public void DataCallback(DetectionPlotData data)
        {
            GraphSummary.GraphControl.BeginInvoke((Action)(() => { GraphSummary.UpdateUI(); }));
        }

        protected BarItem MakeBarItem(PointPairList points, Color color)
        {
            return new BarItem(null, points, color)
            {
                Bar =
                {
                    Fill = { Type = FillType.Solid }, Border = {InflateFactor = 0.7F}
                }
            };
        }

        protected virtual void AddLabels()
        {
            if (_detectionData.IsValid)
            {
                Title.Text = string.Empty;
                YAxis.Title.Text = string.Format(CultureInfo.CurrentCulture,
                    GraphsResources.DetectionPlotPane_YAxis_Name,
                    string.Format(CultureInfo.CurrentCulture, Settings.YScaleFactor.Label, Settings.TargetType.Label));
                return;
            }

            GraphObjList.Clear();
            string message = string.Empty;
            Exception error = Receiver.GetError();

            if (error != null)
            {
                Title.Text = GraphsResources.DetectionPlotPane_EmptyPlotError_Label;
                message = error.Message;
            }
            else if (Receiver.IsProcessing())
            {
                Title.Text = GraphsResources.DetectionPlotPane_WaitingForData_Label;
            }
            else
            {
                Title.Text = GraphsResources.DetectionPlotPane_EmptyPlot_Label;
            }

            var scaleFactor = CalcScaleFactor();
            SizeF titleSize;
            using (var g = GraphSummary.CreateGraphics())
            {
                titleSize = Title.FontSpec.BoundingBox(g, Title.Text, scaleFactor);
            }
            var subtitleLocation = new PointF(
                (Rect.Left + Rect.Right) / (2 * Rect.Width),
                (Rect.Top + Margin.Top * (1 + scaleFactor) + 2*titleSize.Height) / Rect.Height);

            var subtitle = new TextObj(message, subtitleLocation.X, subtitleLocation.Y,
                CoordType.PaneFraction, AlignH.Center, AlignV.Center)
            {
                IsClippedToChartRect = true,
                ZOrder = ZOrder.E_BehindCurves,
                FontSpec = GraphSummary.CreateFontSpec(Color.Black),
            };
            subtitle.FontSpec.Size = Title.FontSpec.Size * 0.75f;
            GraphObjList.Add(subtitle);
        }

        protected override IdentityPath GetIdentityPath(CurveItem curveItem, int barIndex)
        {
            return null;
        }

        protected override void ChangeSelection(int selectedIndex, IdentityPath identityPath)
        {
        }

        protected override int SelectedIndex => GraphSummary.StateProvider.SelectedResultsIndex;


        #region Functional Test Support

        public DetectionPlotData CurrentData { get { return _detectionData; } }

        #endregion

    }

}
