/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;
using SampleType = pwiz.Skyline.Model.DocSettings.AbsoluteQuantification.SampleType;

namespace pwiz.Skyline.Controls.Graphs.Calibration
{
    public partial class CalibrationForm : DockableFormEx, IUpdatable
    {
        private readonly SkylineWindow _skylineWindow;
        private CurveList _scatterPlots;
        public CalibrationForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            _skylineWindow = skylineWindow;

            zedGraphControl.MasterPane.Border.IsVisible = false;
            zedGraphControl.GraphPane.Border.IsVisible = false;
            zedGraphControl.GraphPane.Chart.Border.IsVisible = false;
            zedGraphControl.GraphPane.XAxis.Title.Text = QuantificationStrings.Calculated_Concentration;
            zedGraphControl.GraphPane.YAxis.Title.Text = QuantificationStrings.Intensity;
            zedGraphControl.GraphPane.Legend.IsVisible = false;
            zedGraphControl.GraphPane.Title.Text = null;
            zedGraphControl.GraphPane.XAxis.MajorTic.IsOpposite = false;
            zedGraphControl.GraphPane.XAxis.MinorTic.IsOpposite = false;
            zedGraphControl.GraphPane.YAxis.MajorTic.IsOpposite = false;
            zedGraphControl.GraphPane.YAxis.MinorTic.IsOpposite = false;
        }

        public static CalibrationCurveOptions Options { get { return Settings.Default.CalibrationCurveOptions; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (null != _skylineWindow)
            {
                _skylineWindow.DocumentUIChangedEvent += SkylineWindowOnDocumentUIChangedEvent;
                UpdateUI(false);
            }
        }

        private void SkylineWindowOnDocumentUIChangedEvent(object sender, DocumentChangedEventArgs documentChangedEventArgs)
        {
            UpdateUI(false);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (null != _skylineWindow)
            {
                _skylineWindow.DocumentUIChangedEvent -= SkylineWindowOnDocumentUIChangedEvent;
            }
            base.OnHandleDestroyed(e);
        }

        public IDocumentUIContainer DocumentUiContainer
        {
            get { return _skylineWindow; }
        }

        public void UpdateUI(bool selectionChanged)
        {
            try
            {
                if (IsDisposed)
                {
                    return;
                }
                zedGraphControl.GraphPane.GraphObjList.Clear();
                zedGraphControl.GraphPane.CurveList.Clear();
                DisplayCalibrationCurve();
                zedGraphControl.AxisChange();
                zedGraphControl.Invalidate();
            }
            catch (Exception e)
            {
                Program.ReportException(e);
            }
        }

        public CalibrationCurve CalibrationCurve { get; private set; }

        private void DisplayCalibrationCurve()
        {
            CalibrationCurveOptions options = Settings.Default.CalibrationCurveOptions;
            zedGraphControl.GraphPane.YAxis.Type = zedGraphControl.GraphPane.XAxis.Type 
                = options.LogPlot ? AxisType.Log : AxisType.Linear;
            zedGraphControl.GraphPane.Legend.IsVisible = options.ShowLegend;
            _scatterPlots = null;
            CalibrationCurve = null;
            SrmDocument document = DocumentUiContainer.DocumentUI;
            if (!document.Settings.HasResults)
            {
                zedGraphControl.GraphPane.Title.Text = QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_No_results_available;
                return;
            }
            PeptideDocNode peptide = null;
            PeptideGroupDocNode peptideGroup = null;
            SequenceTree sequenceTree = _skylineWindow.SequenceTree;
            if (null != sequenceTree)
            {
                PeptideTreeNode peptideTreeNode = sequenceTree.GetNodeOfType<PeptideTreeNode>();
                if (null != peptideTreeNode)
                {
                    peptide = peptideTreeNode.DocNode;
                }
                PeptideGroupTreeNode peptideGroupTreeNode = sequenceTree.GetNodeOfType<PeptideGroupTreeNode>();
                if (null != peptideGroupTreeNode)
                {
                    peptideGroup = peptideGroupTreeNode.DocNode;
                }
            }

            if (null == peptide)
            {
                zedGraphControl.GraphPane.Title.Text = QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_Select_a_peptide_to_see_its_calibration_curve;
                return;
            }
            zedGraphControl.GraphPane.Title.Text = null;
            PeptideQuantifier peptideQuantifier = PeptideQuantifier.GetPeptideQuantifier(document.Settings, peptideGroup, peptide);
            CalibrationCurveFitter curveFitter = new CalibrationCurveFitter(peptideQuantifier, document.Settings);
            CalibrationCurve = curveFitter.GetCalibrationCurve(null);

            zedGraphControl.GraphPane.XAxis.Title.Text = curveFitter.GetXAxisTitle();
            zedGraphControl.GraphPane.YAxis.Title.Text = curveFitter.GetYAxisTitle();
            ICollection<IdentityPath> transitionIds = null;
            string selectionErrorMessage = null;
            if (Options.ShowSelection)
            {
                transitionIds = curveFitter.GetTransitionQuantities(_skylineWindow.SelectedResultsIndex).Keys;
                if (!transitionIds.Any())
                {
                    selectionErrorMessage = QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_The_selected_replicate_has_no_transitions_that_can_be_quantified_;
                    transitionIds = null;
                }
                
            }
            double minX = double.MaxValue, maxX = double.MinValue;
            _scatterPlots = new CurveList();
            foreach (var sampleType in SampleType.ListSampleTypes())
            {
                PointPairList pointPairList = new PointPairList();
                for (int iReplicate = 0;
                    iReplicate < document.Settings.MeasuredResults.Chromatograms.Count;
                    iReplicate++)
                {
                    ChromatogramSet chromatogramSet = document.Settings.MeasuredResults.Chromatograms[iReplicate];
                    if (!Equals(sampleType, chromatogramSet.SampleType))
                    {
                        continue;
                    }
                    double? y = curveFitter.GetReplicateIntensity(iReplicate, transitionIds);
                    double? x = curveFitter.DilutionFactorToConcentration(chromatogramSet.DilutionFactor);
                    if (!x.HasValue)
                    {
                        x = CalibrationCurve.GetX(y);
                    }
                    if (!Options.DisplaySampleType(chromatogramSet.SampleType))
                    {
                        continue;
                    }
                    if (y.HasValue && x.HasValue)
                    {
                        PointPair point = new PointPair(x.Value, y.Value) { Tag = iReplicate };
                        pointPairList.Add(point);
                        if (!Options.LogPlot || x.Value > 0)
                        {
                            minX = Math.Min(minX, x.Value);
                        }
                        maxX = Math.Max(maxX, x.Value);
                    }
                }
                if (pointPairList.Any())
                {
                    var lineItem = zedGraphControl.GraphPane.AddCurve(sampleType.ToString(), pointPairList, sampleType.Color, sampleType.SymbolType);
                    lineItem.Line.IsVisible = false;
                    lineItem.Symbol.Fill = new Fill(sampleType.Color);
                    _scatterPlots.Add(lineItem);
                }
            }
            List<string> labelLines = new List<String>();
            if (minX <= maxX)
            {
                int interpolatedLinePointCount = 100;
                RegressionFit regressionFit = document.Settings.PeptideSettings.Quantification.RegressionFit;
                if (!options.LogPlot)
                {
                    if (regressionFit == RegressionFit.LINEAR_THROUGH_ZERO)
                    {
                        minX = Math.Min(0, minX);
                    }
                    if (regressionFit != RegressionFit.QUADRATIC)
                    {
                        interpolatedLinePointCount = 2;
                    }
                }
                LineItem interpolatedLine = CreateInterpolatedLine(CalibrationCurve, minX, maxX,
                    interpolatedLinePointCount, Options.LogPlot);
                if (null != interpolatedLine)
                {
                    zedGraphControl.GraphPane.CurveList.Add(interpolatedLine);
                }
            }
            labelLines.Add(CalibrationCurve.ToString());
            if (CalibrationCurve.RSquared.HasValue)
            {
                labelLines.Add(QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_ + 
                    CalibrationCurve.RSquared.Value.ToString("0.####")); // Not L10N
            }
            if (!Equals(curveFitter.QuantificationSettings.RegressionWeighting, RegressionWeighting.NONE))
            {
                labelLines.Add(string.Format("{0}: {1}", // Not L10N
                    QuantificationStrings.Weighting, curveFitter.QuantificationSettings.RegressionWeighting));
            }
            if (options.ShowSelection)
            {
                if (selectionErrorMessage != null)
                {
                    labelLines.Add(selectionErrorMessage);
                }
                else
                {
                    double? ySelected = curveFitter.GetReplicateIntensity(_skylineWindow.SelectedResultsIndex,
                        transitionIds);
                    double? estimatedValue = CalibrationCurve.GetX(ySelected);
                    if (estimatedValue.HasValue)
                    {
                        ArrowObj arrow = new ArrowObj(estimatedValue.Value, ySelected.Value, estimatedValue.Value,
                            ySelected.Value);
                        arrow.Line.Color = GraphSummary.ColorSelected;
                        zedGraphControl.GraphPane.GraphObjList.Add(arrow);
                        string strConcentration;
                        if (string.IsNullOrEmpty(peptide.ConcentrationUnits))
                        {
                            strConcentration = estimatedValue.Value.ToString(Formats.CalibrationCurve);
                        }
                        else
                        {
                            strConcentration = TextUtil.SpaceSeparate(estimatedValue.Value.ToString(Formats.Concentration), peptide.ConcentrationUnits);
                        }

                        labelLines.Add(string.Format("{0} = {1}", // Not L10N
                            QuantificationStrings.Calculated_Concentration, strConcentration));
                    }
                }
            }
            if (labelLines.Any())
            {
                TextObj text = new TextObj(TextUtil.LineSeparate(labelLines), .01, 0,
                    CoordType.ChartFraction, AlignH.Left, AlignV.Top)
                {
                    IsClippedToChartRect = true,
                    ZOrder = ZOrder.E_BehindCurves,
                    FontSpec = GraphSummary.CreateFontSpec(Color.Black),
                };
                zedGraphControl.GraphPane.GraphObjList.Add(text);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Dispose();
        }

        private bool zedGraphControl_MouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            int? replicateIndex = ReplicateIndexFromPoint(e.Location);
            if (replicateIndex.HasValue)
            {
                zedGraphControl.Cursor = Cursors.Hand;
                return true;
            }
            return false;
        }

        private bool zedGraphControl_MouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return false;
            }
            int? replicateIndex = ReplicateIndexFromPoint(e.Location);
            if (replicateIndex.HasValue)
            {
                _skylineWindow.SelectedResultsIndex = replicateIndex.Value;
                return true;
            }
            return false;
        }

        public int? ReplicateIndexFromPoint(Point pt)
        {
            if (null == _scatterPlots)
            {
                return null;
            }
            PointF ptF = new PointF(pt.X, pt.Y);
            CurveItem nearestCurve;
            int iNeareast;
            if (!zedGraphControl.GraphPane.FindNearestPoint(ptF, _scatterPlots, out nearestCurve, out iNeareast))
            {
                return null;
            }
            PointPair nearPoint = nearestCurve.Points[iNeareast];
            PointF nearPointScreen = zedGraphControl.GraphPane.GeneralTransform(nearPoint.X, nearPoint.Y, CoordType.AxisXYScale);
            if (Math.Abs(nearPointScreen.X - pt.X) > 5 || Math.Abs(nearPointScreen.Y - pt.Y) > 5)
            {
                return null;
            }
            return nearestCurve.Points[iNeareast].Tag as int?;
        }

        private void zedGraphControl_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            ZedGraphHelper.BuildContextMenu(sender, menuStrip, true);
            if (!menuStrip.Items.Contains(logPlotContextMenuItem))
            {
                int index = 0;
                menuStrip.Items.Insert(index++, logPlotContextMenuItem);
                menuStrip.Items.Insert(index++, showSampleTypesContextMenuItem);
                menuStrip.Items.Insert(index++, showLegendContextMenuItem);
                menuStrip.Items.Insert(index++, showSelectionContextMenuItem);
                menuStrip.Items.Insert(index++, new ToolStripSeparator());
            }
            
            showSampleTypesContextMenuItem.DropDownItems.Clear();
            foreach (var sampleType in SampleType.ListSampleTypes())
            {
                showSampleTypesContextMenuItem.DropDownItems.Add(MakeShowSampleTypeMenuItem(sampleType));
            }
            logPlotContextMenuItem.Checked = Options.LogPlot;
            showLegendContextMenuItem.Checked = Options.ShowLegend;
            showSelectionContextMenuItem.Checked = Options.ShowSelection;
        }

        private ToolStripMenuItem MakeShowSampleTypeMenuItem(SampleType sampleType)
        {
            ToolStripMenuItem menuItem = new ToolStripMenuItem(sampleType.ToString())
            {
                Checked = Options.DisplaySampleTypes.Contains(sampleType.Name)
            };
            menuItem.Click += (sender, args) =>
            {
                if (menuItem.Checked)
                {
                    Options.DisplaySampleTypes = Options.DisplaySampleTypes.Except(new[] {sampleType.Name}).ToArray();
                }
                else
                {
                    Options.DisplaySampleTypes =
                        Options.DisplaySampleTypes.Concat(new[] {sampleType.Name}).Distinct().ToArray();
                }
                UpdateUI(false);
            };
            return menuItem;
        }


        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            Dispose();
        }

        private void logPlotToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Options.LogPlot = !Options.LogPlot;
            UpdateUI(false);
        }

        private LineItem CreateInterpolatedLine(CalibrationCurve calibrationCurve, double minX, double maxX, int pointCount, bool logPlot)
        {
            PointPairList pointPairList = new PointPairList();
            for (int i = 0; i < pointCount; i++)
            {
                double x;
                if (logPlot)
                {
                    x = Math.Exp((Math.Log(minX)*(pointCount - 1 - i) + Math.Log(maxX)*i)/(pointCount - 1));
                }
                else
                {
                    x = (minX*(pointCount - 1 - i) + maxX*i)/(pointCount - 1);
                }
                double? y = calibrationCurve.GetY(x);
                if (y.HasValue)
                {
                    pointPairList.Add(x, y.Value);
                }
            }
            if (!pointPairList.Any())
            {
                return null;
            }
            return new LineItem(QuantificationStrings.Calibration_Curve, pointPairList, Color.Gray, SymbolType.None);
        }

        private void showLegendContextMenuItem_Click(object sender, EventArgs e)
        {
            Options.ShowLegend = !Options.ShowLegend;
            UpdateUI(false);
        }

        private void showSelectionContextMenuItem_Click(object sender, EventArgs e)
        {
            Options.ShowSelection = !Options.ShowSelection;
            UpdateUI(false);
        }

        #region Test Methods
        public ZedGraphControl ZedGraphControl { get { return zedGraphControl; } }
        #endregion
    }
}
