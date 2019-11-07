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
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
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
        private string _originalFormTitle;
        public CalibrationForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            _skylineWindow = skylineWindow;

            zedGraphControl.MasterPane.Border.IsVisible = false;
            zedGraphControl.GraphPane.Border.IsVisible = false;
            zedGraphControl.GraphPane.Chart.Border.IsVisible = false;
            zedGraphControl.GraphPane.XAxis.Title.Text = QuantificationStrings.Analyte_Concentration;
            zedGraphControl.GraphPane.YAxis.Title.Text = QuantificationStrings.CalibrationCurveFitter_GetYAxisTitle_Peak_Area;
            zedGraphControl.GraphPane.Legend.IsVisible = false;
            zedGraphControl.GraphPane.Title.Text = null;
            zedGraphControl.GraphPane.Title.FontSpec.Size = 12f;
            zedGraphControl.GraphPane.IsFontsScaled = false;
            zedGraphControl.GraphPane.XAxis.MajorTic.IsOpposite = false;
            zedGraphControl.GraphPane.XAxis.MinorTic.IsOpposite = false;
            zedGraphControl.GraphPane.YAxis.MajorTic.IsOpposite = false;
            zedGraphControl.GraphPane.YAxis.MinorTic.IsOpposite = false;
            zedGraphControl.IsZoomOnMouseCenter = true;
            _originalFormTitle = Text;
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
        public FiguresOfMerit FiguresOfMerit { get; private set; }

        private void DisplayCalibrationCurve()
        {
            Text = TabText = _originalFormTitle;
            CalibrationCurveOptions options = Settings.Default.CalibrationCurveOptions;
            zedGraphControl.GraphPane.YAxis.Type = options.LogYAxis ? AxisType.Log : AxisType.Linear;
            zedGraphControl.GraphPane.XAxis.Type = options.LogXAxis ? AxisType.Log : AxisType.Linear;
            bool logPlot = options.LogXAxis || options.LogYAxis;
            zedGraphControl.GraphPane.Legend.IsVisible = options.ShowLegend;
            _scatterPlots = null;
            CalibrationCurve = null;
            FiguresOfMerit = FiguresOfMerit.EMPTY;
            SrmDocument document = DocumentUiContainer.DocumentUI;
            if (!document.Settings.HasResults)
            {
                zedGraphControl.GraphPane.Title.Text =
                    QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_No_results_available;
                return;
            }
            PeptideDocNode peptide;
            PeptideGroupDocNode peptideGroup;

            if (!TryGetSelectedPeptide(out peptideGroup,out peptide))
            {
                zedGraphControl.GraphPane.Title.Text =
                    ModeUIAwareStringFormat(QuantificationStrings
                        .CalibrationForm_DisplayCalibrationCurve_Select_a_peptide_to_see_its_calibration_curve);
                return;
            }
            if (-1 == document.Children.IndexOf(peptideGroup))
            {
                zedGraphControl.GraphPane.Title.Text = ModeUIAwareStringFormat(QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_The_selected_peptide_is_no_longer_part_of_the_Skyline_document_);
                return;
            }
            PeptideQuantifier peptideQuantifier = PeptideQuantifier.GetPeptideQuantifier(document, peptideGroup,
                peptide);
            CalibrationCurveFitter curveFitter = new CalibrationCurveFitter(peptideQuantifier, document.Settings);
            if (curveFitter.IsEnableSingleBatch && Settings.Default.CalibrationCurveOptions.SingleBatch)
            {
                curveFitter.SingleBatchReplicateIndex = _skylineWindow.SelectedResultsIndex;
            }

            Text = TabText = GetFormTitle(curveFitter);
            if (peptideQuantifier.QuantificationSettings.RegressionFit == RegressionFit.NONE)
            {
                if (!(peptideQuantifier.NormalizationMethod is NormalizationMethod.RatioToLabel))
                {
                    zedGraphControl.GraphPane.Title.Text =
                        ModeUIAwareStringFormat(QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_Use_the_Quantification_tab_on_the_Peptide_Settings_dialog_to_control_the_conversion_of_peak_areas_to_concentrations_);
                }
                else
                {
                    if (!peptide.InternalStandardConcentration.HasValue)
                    {
                        zedGraphControl.GraphPane.Title.Text =
                            ModeUIAwareStringFormat(QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_To_convert_peak_area_ratios_to_concentrations__specify_the_internal_standard_concentration_for__0__, peptide);
                    }
                    else
                    {
                        zedGraphControl.GraphPane.Title.Text = null;
                    }
                }
            }
            else
            {
                if (curveFitter.GetStandardConcentrations().Any())
                {
                    zedGraphControl.GraphPane.Title.Text = null;
                }
                else
                {
                    zedGraphControl.GraphPane.Title.Text = QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_To_fit_a_calibration_curve__set_the_Sample_Type_of_some_replicates_to_Standard__and_specify_their_concentration_;
                }
            }

            zedGraphControl.GraphPane.XAxis.Title.Text = curveFitter.GetXAxisTitle();
            zedGraphControl.GraphPane.YAxis.Title.Text = curveFitter.GetYAxisTitle();
            CalibrationCurve = curveFitter.GetCalibrationCurve();
            FiguresOfMerit = curveFitter.GetFiguresOfMerit(CalibrationCurve);
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue;
            _scatterPlots = new CurveList();

            IEnumerable<SampleType> sampleTypes = SampleType.ListSampleTypes()
                .Where(Options.DisplaySampleType);
            foreach (var sampleType in sampleTypes)
            {
                PointPairList pointPairList = new PointPairList();
                PointPairList pointPairListExcluded = new PointPairList();
                foreach (var standardIdentifier in curveFitter.EnumerateCalibrationPoints())
                {
                    if (!Equals(sampleType, curveFitter.GetSampleType(standardIdentifier)))
                    {
                        continue;
                    }

                    double? y = curveFitter.GetYValue(standardIdentifier);
                    double? xCalculated = curveFitter.GetCalculatedXValue(CalibrationCurve, standardIdentifier);
                    double? x = curveFitter.GetSpecifiedXValue(standardIdentifier)
                                ?? xCalculated;
                    if (y.HasValue && x.HasValue)
                    {
                        PointPair point = new PointPair(x.Value, y.Value) {Tag = standardIdentifier };
                        if (sampleType.AllowExclude && null == standardIdentifier.LabelType && peptide.IsExcludeFromCalibration(standardIdentifier.ReplicateIndex))
                        {
                            pointPairListExcluded.Add(point);
                        }
                        else
                        {
                            pointPairList.Add(point);
                        }
                        if (!IsNumber(x) || !IsNumber(y))
                        {
                            continue;
                        }
                        if (!logPlot || x.Value > 0)
                        {
                            minX = Math.Min(minX, x.Value);
                        }
                        if (!logPlot || y.Value > 0)
                        {
                            minY = Math.Min(minY, y.Value);
                        }
                        maxX = Math.Max(maxX, x.Value);
                        if (IsNumber(xCalculated))
                        {
                            maxX = Math.Max(maxX, xCalculated.Value);
                            if (!logPlot || xCalculated.Value > 0)
                            {
                                minX = Math.Min(minX, xCalculated.Value);
                            }
                        }
                    }
                }
                if (pointPairList.Any())
                {
                    var lineItem = zedGraphControl.GraphPane.AddCurve(sampleType.ToString(), pointPairList,
                        sampleType.Color, sampleType.SymbolType);
                    lineItem.Line.IsVisible = false;
                    lineItem.Symbol.Fill = new Fill(sampleType.Color);
                    _scatterPlots.Add(lineItem);
                }
                if (pointPairListExcluded.Any())
                {
                    string curveLabel = pointPairList.Any() ? null : sampleType.ToString();
                    var lineItem = zedGraphControl.GraphPane.AddCurve(curveLabel, pointPairListExcluded,
                        sampleType.Color, sampleType.SymbolType);
                    lineItem.Line.IsVisible = false;
                    _scatterPlots.Add(lineItem);
                }
            }
            List<string> labelLines = new List<String>();
            RegressionFit regressionFit = document.Settings.PeptideSettings.Quantification.RegressionFit;
            if (regressionFit != RegressionFit.NONE)
            {
                if (minX <= maxX)
                {
                    int interpolatedLinePointCount = 100;
                    if (!logPlot && regressionFit != RegressionFit.LINEAR_IN_LOG_SPACE)
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
                    double[] xValues;
                    if (CalibrationCurve.TurningPoint.HasValue)
                    {
                        xValues = new[] {minX, CalibrationCurve.TurningPoint.Value, maxX};
                    }
                    else
                    {
                        xValues = new[] {minX, maxX};
                    }
                    Array.Sort(xValues);
                    LineItem interpolatedLine = CreateInterpolatedLine(CalibrationCurve, xValues,
                        interpolatedLinePointCount, logPlot);
                    if (null != interpolatedLine)
                    {
                        zedGraphControl.GraphPane.CurveList.Add(interpolatedLine);
                    }
                }
                labelLines.Add(CalibrationCurve.ToString());

                if (CalibrationCurve.RSquared.HasValue)
                {
                    labelLines.Add(QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_ +
                                   CalibrationCurve.RSquared.Value.ToString(@"0.####"));
                }
                if (!Equals(curveFitter.QuantificationSettings.RegressionWeighting, RegressionWeighting.NONE))
                {
                    labelLines.Add(string.Format(@"{0}: {1}",
                        QuantificationStrings.Weighting, curveFitter.QuantificationSettings.RegressionWeighting));
                }
                if (options.ShowFiguresOfMerit)
                {
                    string strFiguresOfMerit = FiguresOfMerit.ToString();
                    if (!string.IsNullOrEmpty(strFiguresOfMerit))
                    {
                        labelLines.Add(strFiguresOfMerit);
                    }
                }
            }

            CalibrationPoint? selectionIdentifier = null;
            if (options.ShowSelection)
            {
                if (curveFitter.IsotopologResponseCurve)
                {
                    var labelType = (_skylineWindow.SequenceTree.SelectedNode as SrmTreeNode)
                        ?.GetNodeOfType<TransitionGroupTreeNode>()?.DocNode.LabelType;
                    if (labelType != null)
                    {
                        selectionIdentifier =
                            new CalibrationPoint(_skylineWindow.SelectedResultsIndex,
                                labelType);
                    }
                }
                else
                {
                    selectionIdentifier =
                        new CalibrationPoint(_skylineWindow.SelectedResultsIndex, null);
                }
            }
            if (selectionIdentifier.HasValue) {
                double? ySelected = curveFitter.GetYValue(selectionIdentifier.Value);
                if (IsNumber(ySelected))
                {
                    double? xSelected = curveFitter.GetCalculatedXValue(CalibrationCurve, selectionIdentifier.Value);
                    var selectedLineColor = Color.FromArgb(128, GraphSummary.ColorSelected);
                    const float selectedLineWidth = 2;
                    double? xSpecified = curveFitter.GetSpecifiedXValue(selectionIdentifier.Value);
                    if (IsNumber(xSelected))
                    {
                        ArrowObj arrow = new ArrowObj(xSelected.Value, ySelected.Value, xSelected.Value,
                            ySelected.Value) {Line = {Color = GraphSummary.ColorSelected}};
                        zedGraphControl.GraphPane.GraphObjList.Insert(0, arrow);
                        var verticalLine = new LineObj(xSelected.Value, ySelected.Value, xSelected.Value,
                            options.LogYAxis ? minY / 10 : 0)
                        {
                            Line = {Color = selectedLineColor, Width = selectedLineWidth},
                            Location = {CoordinateFrame = CoordType.AxisXYScale},
                            ZOrder = ZOrder.E_BehindCurves,
                            IsClippedToChartRect = true
                        };
                        zedGraphControl.GraphPane.GraphObjList.Add(verticalLine);
                        if (IsNumber(xSpecified))
                        {
                            var horizontalLine = new LineObj(xSpecified.Value, ySelected.Value, xSelected.Value,
                                ySelected.Value)
                            {
                                Line = {Color = selectedLineColor, Width = selectedLineWidth},
                                Location = {CoordinateFrame = CoordType.AxisXYScale},
                                ZOrder = ZOrder.E_BehindCurves,
                                IsClippedToChartRect = true
                            };
                            zedGraphControl.GraphPane.GraphObjList.Add(horizontalLine);
                        }
                    }
                    else
                    {
                        // We were not able to map the observed intensity back to the calibration curve, but we still want to
                        // indicate where the currently selected point is.
                        if (IsNumber(xSpecified))
                        {
                            // If the point has a specified concentration, then use that.
                            ArrowObj arrow = new ArrowObj(xSpecified.Value, ySelected.Value, xSpecified.Value,
                                ySelected.Value) {Line = {Color = GraphSummary.ColorSelected}};
                            zedGraphControl.GraphPane.GraphObjList.Insert(0, arrow);
                        }
                        else
                        {
                            // Otherwise, draw a horizontal line at the appropriate y-value.
                            var horizontalLine = new LineObj(minX, ySelected.Value, maxX, ySelected.Value)
                            {
                                Line = {Color = selectedLineColor, Width = selectedLineWidth},
                                Location = {CoordinateFrame = CoordType.AxisXYScale},
                                IsClippedToChartRect = true,
                            };
                            ZedGraphControl.GraphPane.GraphObjList.Add(horizontalLine);
                        }
                    }
                }

                QuantificationResult quantificationResult = null;
                double? calculatedConcentration;
                if (curveFitter.IsotopologResponseCurve)
                {
                    calculatedConcentration =
                        curveFitter.GetCalculatedConcentration(CalibrationCurve, selectionIdentifier.Value);
                }
                else
                {
                    quantificationResult = curveFitter.GetQuantificationResult(selectionIdentifier.Value.ReplicateIndex);
                    calculatedConcentration = quantificationResult?.CalculatedConcentration;
                }
                if (calculatedConcentration.HasValue)
                {
                    labelLines.Add(string.Format(@"{0} = {1}",
                        QuantificationStrings.Calculated_Concentration, calculatedConcentration));
                }
                else if (quantificationResult != null && !quantificationResult.NormalizedArea.HasValue)
                {
                    labelLines.Add(QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_The_selected_replicate_has_missing_or_truncated_transitions);
                }
            }
            if (Options.ShowFiguresOfMerit)
            {
                if (IsNumber(FiguresOfMerit.LimitOfDetection))
                {
                    var lodLine = new LineObj(Color.DarkMagenta, FiguresOfMerit.LimitOfDetection.Value, 0,
                        FiguresOfMerit.LimitOfDetection.Value, 1)
                    {
                        Location = { CoordinateFrame = CoordType.XScaleYChartFraction }
                    };
                    zedGraphControl.GraphPane.GraphObjList.Add(lodLine);
                }
                if (IsNumber(FiguresOfMerit.LimitOfQuantification))
                {
                    var loqLine = new LineObj(Color.DarkCyan, FiguresOfMerit.LimitOfQuantification.Value, 0,
                        FiguresOfMerit.LimitOfQuantification.Value, 1)
                    {
                        Location = { CoordinateFrame = CoordType.XScaleYChartFraction }
                    };
                    zedGraphControl.GraphPane.GraphObjList.Add(loqLine);
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

        private string GetFormTitle(CalibrationCurveFitter curveFitter)
        {
            string title = TextUtil.SpaceSeparate(_originalFormTitle + ':', curveFitter.PeptideQuantifier.PeptideDocNode.ModifiedSequenceDisplay);
            if (curveFitter.SingleBatchReplicateIndex.HasValue)
            {
                var chromatogramSet = _skylineWindow.Document.Settings.MeasuredResults.Chromatograms[
                    curveFitter.SingleBatchReplicateIndex.Value];
                if (string.IsNullOrEmpty(chromatogramSet.BatchName))
                {
                    title = TextUtil.SpaceSeparate(title, string.Format(QuantificationStrings.CalibrationForm_GetFormTitle__Replicate___0__, chromatogramSet.Name));
                }
                else
                {
                    title = TextUtil.SpaceSeparate(title, string.Format(QuantificationStrings.CalibrationForm_GetFormTitle__Batch___0__, chromatogramSet.BatchName));
                }
            }
            else
            {
                if (_skylineWindow.Document.Settings.MeasuredResults.Chromatograms.Select(c => c.BatchName).Distinct()
                        .Count() > 1)
                {
                    title = TextUtil.SpaceSeparate(title, QuantificationStrings.CalibrationForm_GetFormTitle__All_Replicates_);
                }
            }
            return title;
        }

        private bool TryGetSelectedPeptide(out PeptideGroupDocNode peptideGroup, out PeptideDocNode peptide)
        {
            peptide = null;
            peptideGroup = null;
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
            return peptide != null;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Dispose();
        }

        private bool zedGraphControl_MouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            var replicateIndex = ReplicateIndexFromPoint(e.Location);
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
            CalibrationPoint? replicateIndex = ReplicateIndexFromPoint(e.Location);
            if (replicateIndex.HasValue)
            {
                _skylineWindow.SelectedResultsIndex = replicateIndex.Value.ReplicateIndex;
                if (null != replicateIndex.Value.LabelType)
                {
                    var selectedTransitionGroup = (_skylineWindow.SequenceTree.SelectedNode as SrmTreeNode)
                        ?.GetNodeOfType<TransitionGroupTreeNode>();
                    if (selectedTransitionGroup == null || !Equals(selectedTransitionGroup.DocNode.LabelType,
                            replicateIndex.Value.LabelType))
                    {
                        var selectedPeptide = (_skylineWindow.SequenceTree.SelectedNode as SrmTreeNode)
                            ?.GetNodeOfType<PeptideTreeNode>();
                        if (selectedPeptide != null)
                        {
                            var transitionGroupToSelect = selectedPeptide.Nodes.OfType<TransitionGroupTreeNode>()
                                .FirstOrDefault(node => Equals(replicateIndex.Value.LabelType, node.DocNode.LabelType));
                            if (transitionGroupToSelect != null)
                            {
                                _skylineWindow.SequenceTree.SelectedPath = transitionGroupToSelect.Path;
                            }
                        }
                    }
                }
                return true;
            }
            return false;
        }

        public CalibrationPoint? ReplicateIndexFromPoint(Point pt)
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
            return nearestCurve.Points[iNeareast].Tag as CalibrationPoint?;
        }

        private bool IsEnableIsotopologResponseCurve()
        {
            return TryGetSelectedPeptide(out _, out var peptide) &&
                   peptide.TransitionGroups.Any(tg => tg.PrecursorConcentration.HasValue);
        }

        private void zedGraphControl_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            var calibrationCurveOptions = Settings.Default.CalibrationCurveOptions;
            singleBatchContextMenuItem.Checked = calibrationCurveOptions.SingleBatch;
            if (IsEnableIsotopologResponseCurve())
            {
                singleBatchContextMenuItem.Visible = true;
            }
            else
            {
                singleBatchContextMenuItem.Visible = CalibrationCurveFitter.AnyBatchNames(_skylineWindow.Document.Settings);
            }
            var replicateIndexFromPoint = ReplicateIndexFromPoint(mousePt);
            if (replicateIndexFromPoint.HasValue && null == replicateIndexFromPoint.Value.LabelType)
            {
                ToolStripMenuItem excludeStandardMenuItem 
                    = MakeExcludeStandardMenuItem(replicateIndexFromPoint.Value.ReplicateIndex);
                if (excludeStandardMenuItem != null)
                {
                    menuStrip.Items.Clear();
                    menuStrip.Items.Add(excludeStandardMenuItem);
                    return;
                }
            }
            
            showSampleTypesContextMenuItem.DropDownItems.Clear();
            foreach (var sampleType in SampleType.ListSampleTypes())
            {
                showSampleTypesContextMenuItem.DropDownItems.Add(MakeShowSampleTypeMenuItem(sampleType));
            }
            logXContextMenuItem.Checked = Options.LogXAxis;
            logYAxisContextMenuItem.Checked = Options.LogYAxis;
            showLegendContextMenuItem.Checked = Options.ShowLegend;
            showSelectionContextMenuItem.Checked = Options.ShowSelection;
            showFiguresOfMeritContextMenuItem.Checked = Options.ShowFiguresOfMerit;
            ZedGraphHelper.BuildContextMenu(sender, menuStrip, true);
            if (!menuStrip.Items.Contains(logXContextMenuItem))
            {
                int index = 0;
                menuStrip.Items.Insert(index++, logXContextMenuItem);
                menuStrip.Items.Insert(index++, logYAxisContextMenuItem);
                menuStrip.Items.Insert(index++, showSampleTypesContextMenuItem);
                menuStrip.Items.Insert(index++, singleBatchContextMenuItem);
                menuStrip.Items.Insert(index++, showLegendContextMenuItem);
                menuStrip.Items.Insert(index++, showSelectionContextMenuItem);
                menuStrip.Items.Insert(index++, showFiguresOfMeritContextMenuItem);
                menuStrip.Items.Insert(index++, new ToolStripSeparator());
            }
        }

        private ToolStripMenuItem MakeExcludeStandardMenuItem(int replicateIndex)
        {
            var document = DocumentUiContainer.DocumentUI;
            if (!document.Settings.HasResults)
            {
                return null;
            }
            ChromatogramSet chromatogramSet = null;
            if (replicateIndex >= 0 &&
                replicateIndex < document.Settings.MeasuredResults.Chromatograms.Count)
            {
                chromatogramSet = document.Settings.MeasuredResults.Chromatograms[replicateIndex];
            }
            if (chromatogramSet == null)
            {
                return null;
            }
            if (!chromatogramSet.SampleType.AllowExclude)
            {
                return null;
            }
            PeptideDocNode peptideDocNode;
            PeptideGroupDocNode peptideGroupDocNode;
            if (!TryGetSelectedPeptide(out peptideGroupDocNode, out peptideDocNode))
            {
                return null;
            }
            bool isExcluded = peptideDocNode.IsExcludeFromCalibration(replicateIndex);
            var menuItemText = isExcluded ? QuantificationStrings.CalibrationForm_MakeExcludeStandardMenuItem_Include_Standard 
                : QuantificationStrings.CalibrationForm_MakeExcludeStandardMenuItem_Exclude_Standard;
            var peptideIdPath = new IdentityPath(peptideGroupDocNode.Id, peptideDocNode.Id);
            var menuItem = new ToolStripMenuItem(menuItemText, null, (sender, args) =>
            {
                _skylineWindow.ModifyDocument(menuItemText,
                    doc => SetExcludeStandard(doc, peptideIdPath, replicateIndex, !isExcluded), docPair =>
                    {
                        var msgType = isExcluded
                            ? MessageType.set_included_standard
                            : MessageType.set_excluded_standard;
                        return AuditLogEntry.CreateSingleMessageEntry(new MessageInfo(msgType, docPair.NewDocumentType, PeptideTreeNode.GetLabel(peptideDocNode, string.Empty), chromatogramSet.Name));
                    });
            });
            return menuItem;
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

        private void logXAxisContextMenuItem_Click(object sender, EventArgs e)
        {
            Options.LogXAxis = !Options.LogXAxis;
            UpdateUI(false);
        }

        private void logYAxisContextMenuItem_Click(object sender, EventArgs e)
        {
            Options.LogYAxis = !Options.LogYAxis;
            UpdateUI(false);
        }


        private LineItem CreateInterpolatedLine(CalibrationCurve calibrationCurve, double[] xValues, int pointCount, bool logPlot)
        {
            PointPairList pointPairList = new PointPairList();
            for (int iRange = 0; iRange < xValues.Length - 1; iRange++)
            {
                double minX = xValues[iRange];
                double maxX = xValues[iRange + 1];
                for (int i = 0; i < pointCount; i++)
                {
                    double x;
                    if (logPlot)
                    {
                        x = Math.Exp((Math.Log(minX) * (pointCount - 1 - i) + Math.Log(maxX) * i) / (pointCount - 1));
                    }
                    else
                    {
                        x = (minX * (pointCount - 1 - i) + maxX * i) / (pointCount - 1);
                    }
                    double? y = calibrationCurve.GetY(x);
                    if (y.HasValue)
                    {
                        pointPairList.Add(x, y.Value);
                    }
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


        private void showFiguresOfMeritContextMenuItem_Click(object sender, EventArgs e)
        {
            Options.ShowFiguresOfMerit = !Options.ShowFiguresOfMerit;
            UpdateUI(false);
        }


        private void CalibrationForm_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    _skylineWindow.FocusDocument();
                    e.Handled = true;
                    break;
            }
        }

        public static bool IsNumber(double? value)
        {
            if (!value.HasValue)
            {
                return false;
            }
            if (double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            {
                return false;
            }
            return true;
        }

        #region Test Methods
        public ZedGraphControl ZedGraphControl { get { return zedGraphControl; } }
        #endregion

        private SrmDocument SetExcludeStandard(SrmDocument document, IdentityPath peptideIdPath, int resultsIndex, bool exclude)
        {
            if (!document.Settings.HasResults)
            {
                return document;
            }
            var peptideDocNode = (PeptideDocNode) document.FindNode(peptideIdPath);
            if (peptideDocNode == null)
            {
                return document;
            }
            if (resultsIndex < 0 || resultsIndex >= document.Settings.MeasuredResults.Chromatograms.Count)
            {
                return document;
            }
            bool wasExcluded = peptideDocNode.IsExcludeFromCalibration(resultsIndex);
            return (SrmDocument) document.ReplaceChild(peptideIdPath.Parent,
                peptideDocNode.ChangeExcludeFromCalibration(resultsIndex, !wasExcluded));
        }

        private void singleBatchContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.CalibrationCurveOptions.SingleBatch =
                !Settings.Default.CalibrationCurveOptions.SingleBatch;
            UpdateUI(false);
        }
    }
}
