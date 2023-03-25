using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;
using SampleType = pwiz.Skyline.Model.DocSettings.AbsoluteQuantification.SampleType;

namespace pwiz.Skyline.Controls.Graphs.Calibration
{
    public partial class CalibrationGraphControl : UserControl
    {
        private CurveList _scatterPlots;
        public CalibrationGraphControl()
        {
            InitializeComponent();
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
            zedGraphControl.ContextMenuBuilder += zedGraphControl_ContextMenuBuilder;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public SkylineWindow SkylineWindow { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public Helpers.ModeUIAwareFormHelper ModeUIAwareFormHelper
        {
            get;
            set;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public CalibrationCurveOptions Options
        {
            get
            {
                return Properties.Settings.Default.CalibrationCurveOptions;
            }
            set
            {
                Properties.Settings.Default.CalibrationCurveOptions = value;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Properties.Settings.Default.PropertyChanged += Settings_OnPropertyChanged;
        }

        private void Settings_OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (DisplaySettings != null)
            {
                Update(DisplaySettings);
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            Properties.Settings.Default.PropertyChanged -= Settings_OnPropertyChanged;
            base.OnHandleDestroyed(e);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public Settings DisplaySettings { get; private set; }

        public void Update(Settings displaySettings)
        {
            DoUpdate(displaySettings);
            GraphHelper.FormatFontSize(zedGraphControl.GraphPane, Options.FontSize);
            zedGraphControl.AxisChange();
            zedGraphControl.Invalidate();
        }

        public void Clear()
        {
            zedGraphControl.GraphPane.GraphObjList.Clear();
            zedGraphControl.GraphPane.CurveList.Clear();
            _scatterPlots = null;
            CalibrationCurve = null;
            FiguresOfMerit = FiguresOfMerit.EMPTY;
        }

        private void DoUpdate(Settings displaySettings)
        {
            Clear();
            DisplaySettings = displaySettings;
            var options = Options;
            zedGraphControl.GraphPane.YAxis.Type = options.LogYAxis ? AxisType.Log : AxisType.Linear;
            zedGraphControl.GraphPane.XAxis.Type = options.LogXAxis ? AxisType.Log : AxisType.Linear;
            bool logPlot = options.LogXAxis || options.LogYAxis;
            zedGraphControl.GraphPane.Legend.IsVisible = options.ShowLegend;
            SrmDocument document = displaySettings.Document;
            if (document == null)
            {
                zedGraphControl.GraphPane.Title.Text = displaySettings.GraphTitle;
                return;
            }
            if (!document.Settings.HasResults)
            {
                zedGraphControl.GraphPane.Title.Text =
                    TextUtil.LineSeparate(displaySettings.GraphTitle, QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_No_results_available);
                return;
            }
            CalibrationCurveFitter curveFitter = displaySettings.CalibrationCurveFitter;
            var peptideQuantifier = curveFitter.PeptideQuantifier;
            var peptide = peptideQuantifier.PeptideDocNode;
            if (peptideQuantifier.QuantificationSettings.RegressionFit == RegressionFit.NONE)
            {
                if (!(peptideQuantifier.NormalizationMethod is NormalizationMethod.RatioToLabel))
                {
                    zedGraphControl.GraphPane.Title.Text =
                        TextUtil.LineSeparate(displaySettings.GraphTitle,
                            ModeUIAwareStringFormat(QuantificationStrings
                                .CalibrationForm_DisplayCalibrationCurve_Use_the_Quantification_tab_on_the_Peptide_Settings_dialog_to_control_the_conversion_of_peak_areas_to_concentrations_));
                }
                else
                {
                    if (!peptide.InternalStandardConcentration.HasValue)
                    {
                        zedGraphControl.GraphPane.Title.Text =
                            TextUtil.LineSeparate(displaySettings.GraphTitle,
                                ModeUIAwareStringFormat(
                                    QuantificationStrings
                                        .CalibrationForm_DisplayCalibrationCurve_To_convert_peak_area_ratios_to_concentrations__specify_the_internal_standard_concentration_for__0__,
                                    peptide));
                    }
                    else
                    {
                        zedGraphControl.GraphPane.Title.Text = displaySettings.GraphTitle;
                    }
                }
            }
            else
            {
                if (curveFitter.GetStandardConcentrations().Any())
                {
                    zedGraphControl.GraphPane.Title.Text = displaySettings.GraphTitle;
                }
                else
                {
                    zedGraphControl.GraphPane.Title.Text = TextUtil.LineSeparate(displaySettings.GraphTitle, QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_To_fit_a_calibration_curve__set_the_Sample_Type_of_some_replicates_to_Standard__and_specify_their_concentration_);
                }
            }

            zedGraphControl.GraphPane.XAxis.Title.Text = curveFitter.GetXAxisTitle();
            zedGraphControl.GraphPane.YAxis.Title.Text = curveFitter.GetYAxisTitle();
            curveFitter.GetCalibrationCurveAndMetrics(out CalibrationCurve calibrationCurve, out CalibrationCurveMetrics calibrationCurveRow);
            CalibrationCurve = calibrationCurve;
            CalibrationCurveMetrics = calibrationCurveRow;

            var bootstrapCurves = new List<ImmutableList<PointPair>>();
            FiguresOfMerit = curveFitter.GetFiguresOfMerit(CalibrationCurve, bootstrapCurves);
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            _scatterPlots = new CurveList();

            IEnumerable<SampleType> sampleTypes = SampleType.ListSampleTypes()
                .Where(options.DisplaySampleType);
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
                        PointPair point = new PointPair(x.Value, y.Value) { Tag = standardIdentifier };
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
                        maxY = Math.Max(maxY, y.Value);
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
            List<string> labelLines = new List<string>();
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
                    if (CalibrationCurve is CalibrationCurve.Bilinear bilinearCalibrationCurve)
                    {
                        xValues = new[] { minX, bilinearCalibrationCurve.TurningPoint, maxX };
                    }
                    else
                    {
                        xValues = new[] { minX, maxX };
                    }
                    Array.Sort(xValues);
                    LineItem interpolatedLine = CreateInterpolatedLine(CalibrationCurve, xValues,
                        interpolatedLinePointCount, logPlot);
                    if (null != interpolatedLine)
                    {
                        zedGraphControl.GraphPane.CurveList.Add(interpolatedLine);
                    }

                    maxY = Math.Max(maxY, GetMaxY(interpolatedLine.Points));
                }
                labelLines.Add(CalibrationCurveMetrics.ToString());

                if (CalibrationCurveMetrics.RSquared.HasValue)
                {
                    labelLines.Add(CalibrationCurveMetrics.RSquaredDisplayText(CalibrationCurveMetrics.RSquared.Value));
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
            if (options.ShowSelection && displaySettings.SelectedResultsIndex.HasValue)
            {
                if (curveFitter.IsotopologResponseCurve)
                {
                    var labelType = displaySettings.SelectedLabelType;
                    if (labelType != null)
                    {
                        selectionIdentifier =
                            new CalibrationPoint(displaySettings.SelectedResultsIndex.Value,
                                labelType);
                    }
                }
                else
                {
                    selectionIdentifier =
                        new CalibrationPoint(displaySettings.SelectedResultsIndex.Value, null);
                }
            }
            if (selectionIdentifier.HasValue)
            {
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
                            ySelected.Value)
                        { Line = { Color = GraphSummary.ColorSelected } };
                        zedGraphControl.GraphPane.GraphObjList.Insert(0, arrow);
                        var verticalLine = new LineObj(xSelected.Value, ySelected.Value, xSelected.Value,
                            options.LogYAxis ? minY / 10 : 0)
                        {
                            Line = { Color = selectedLineColor, Width = selectedLineWidth },
                            Location = { CoordinateFrame = CoordType.AxisXYScale },
                            ZOrder = ZOrder.E_BehindCurves,
                            IsClippedToChartRect = true
                        };
                        zedGraphControl.GraphPane.GraphObjList.Add(verticalLine);
                        if (IsNumber(xSpecified))
                        {
                            var horizontalLine = new LineObj(xSpecified.Value, ySelected.Value, xSelected.Value,
                                ySelected.Value)
                            {
                                Line = { Color = selectedLineColor, Width = selectedLineWidth },
                                Location = { CoordinateFrame = CoordType.AxisXYScale },
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
                                    ySelected.Value)
                                { Line = { Color = GraphSummary.ColorSelected } };
                            zedGraphControl.GraphPane.GraphObjList.Insert(0, arrow);
                        }
                        else
                        {
                            // Otherwise, draw a horizontal line at the appropriate y-value.
                            var horizontalLine = new LineObj(minX, ySelected.Value, maxX, ySelected.Value)
                            {
                                Line = { Color = selectedLineColor, Width = selectedLineWidth },
                                Location = { CoordinateFrame = CoordType.AxisXYScale },
                                IsClippedToChartRect = true,
                            };
                            zedGraphControl.GraphPane.GraphObjList.Add(horizontalLine);
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
                    quantificationResult = curveFitter.GetPeptideQuantificationResult(selectionIdentifier.Value.ReplicateIndex);
                    calculatedConcentration = quantificationResult?.CalculatedConcentration;
                }
                if (calculatedConcentration.HasValue)
                {
                    labelLines.Add(string.Format(@"{0} = {1}",
                        QuantificationStrings.Calculated_Concentration,
                        QuantificationResult.FormatCalculatedConcentration(calculatedConcentration.Value,
                            curveFitter.QuantificationSettings.Units)));
                }
                else if (quantificationResult != null && !quantificationResult.NormalizedArea.HasValue)
                {
                    labelLines.Add(QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_The_selected_replicate_has_missing_or_truncated_transitions);
                }
            }
            if (options.ShowBootstrapCurves)
            {
                var color = Color.FromArgb(40, Color.Teal);
                foreach (var points in bootstrapCurves)
                {
                    var curve = new LineItem(null, new PointPairList(points), color,
                        SymbolType.None, options.LineWidth);
                    maxY = Math.Max(maxY, GetMaxY(curve.Points));
                    zedGraphControl.GraphPane.CurveList.Add(curve);
                }
            }
            if (options.ShowFiguresOfMerit)
            {
                if (IsNumber(FiguresOfMerit.LimitOfDetection))
                {
                    var lod = FiguresOfMerit.LimitOfDetection.Value;
                    var points = new PointPairList(new[] { lod, lod }, new[] { minY, maxY });
                    var lodLine = new LineItem("Lower Limit of Detection", points, Color.Black, SymbolType.None)
                    {
                        Line = { Style = DashStyle.Dot, Width = options.LineWidth}
                    };
                    zedGraphControl.GraphPane.CurveList.Add(lodLine);
                }
                if (IsNumber(FiguresOfMerit.LimitOfQuantification))
                {
                    var loq = FiguresOfMerit.LimitOfQuantification.Value;
                    var points = new PointPairList(new[] { loq, loq }, new[] { minY, maxY });
                    var loqLine = new LineItem("Lower Limit of Quantification", points, Color.Black, SymbolType.None)
                    {
                        Line = { Style = DashStyle.Dash, Width = options.LineWidth}
                    };
                    zedGraphControl.GraphPane.CurveList.Add(loqLine);
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
                text.FontSpec.Size = options.FontSize;
                zedGraphControl.GraphPane.GraphObjList.Add(text);
            }

        }

        public CalibrationCurve CalibrationCurve { get; private set; }
        public CalibrationCurveMetrics CalibrationCurveMetrics { get; private set; }
        public FiguresOfMerit FiguresOfMerit { get; private set; }

        public class Settings : Immutable
        {
            public static readonly Settings EMPTY = new Settings(null, null);
            public Settings(SrmDocument document, CalibrationCurveFitter calibrationCurveFitter)
            {
                Document = document;
                CalibrationCurveFitter = calibrationCurveFitter;
            }

            public SrmDocument Document { get; }
            public CalibrationCurveFitter CalibrationCurveFitter { get; }

            public int? SelectedResultsIndex { get; private set; }

            public Settings ChangeSelectedResultsIndex(int? value)
            {
                return ChangeProp(ImClone(this), im => im.SelectedResultsIndex = value);
            }

            public IsotopeLabelType SelectedLabelType { get; private set; }

            public Settings ChangeSelectedLabelType(IsotopeLabelType value)
            {
                return ChangeProp(ImClone(this), im => im.SelectedLabelType = value);
            }

            public string GraphTitle { get; private set; }

            public Settings ChangeGraphTitle(string value)
            {
                return ChangeProp(ImClone(this), im => im.GraphTitle = value ?? string.Empty);
            }
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
                    double y = calibrationCurve.GetY(x);
                        pointPairList.Add(x, y);
                }
            }
            if (!pointPairList.Any())
            {
                return null;
            }
            return new LineItem(QuantificationStrings.Calibration_Curve, pointPairList, Color.Gray, SymbolType.None, Options.LineWidth);
        }


        public string ModeUIAwareStringFormat(string format, params object[] args)
        {
            return ModeUIAwareFormHelper?.Format(format, args) ?? string.Format(format, args);
        }

        public static bool IsNumber(double? value)
        {
            return CalibrationForm.IsNumber(value);
        }

        public void DisplayError(string message)
        {
            Update(Settings.EMPTY.ChangeGraphTitle(message));
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

        public ZedGraphControl ZedGraphControl
        {
            get
            {
                return zedGraphControl;
            }
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
                PointClicked?.Invoke(replicateIndex.Value);
                return true;
            }
            return false;
        }

        public event Action<CalibrationPoint> PointClicked;
        private void zedGraphControl_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            if (DisplaySettings == null)
            {
                return;
            }
            var calibrationCurveOptions = Options;
            singleBatchContextMenuItem.Checked = calibrationCurveOptions.SingleBatch;
            if (IsEnableIsotopologResponseCurve())
            {
                singleBatchContextMenuItem.Visible = true;
            }
            else
            {
                singleBatchContextMenuItem.Visible = CalibrationCurveFitter.AnyBatchNames(DisplaySettings.Document.Settings);
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
            showBootstrapCurvesToolStripMenuItem.Checked = Options.ShowBootstrapCurves;
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
                menuStrip.Items.Insert(index++, showBootstrapCurvesToolStripMenuItem);
                menuStrip.Items.Insert(index++, moreDisplayOptionsContextMenuItem);
                menuStrip.Items.Insert(index++, new ToolStripSeparator());
            }
        }
        private bool IsEnableIsotopologResponseCurve()
        {
            return TryGetSelectedPeptide(out _, out var peptide) &&
                   peptide.TransitionGroups.Any(tg => tg.PrecursorConcentration.HasValue);
        }

        private bool TryGetSelectedPeptide(out PeptideGroupDocNode peptideGroup, out PeptideDocNode peptide)
        {
            peptide = null;
            peptideGroup = null;
            SequenceTree sequenceTree = SkylineWindow?.SequenceTree;
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


        public ToolStripMenuItem MakeExcludeStandardMenuItem(int replicateIndex)
        {
            var measuredResults =SkylineWindow?.DocumentUI.Settings.MeasuredResults;
            if (measuredResults == null)
            {
                return null;
            }
            ChromatogramSet chromatogramSet = null;
            if (replicateIndex >= 0 &&
                replicateIndex < measuredResults.Chromatograms.Count)
            {
                chromatogramSet = measuredResults.Chromatograms[replicateIndex];
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
                SkylineWindow.ModifyDocument(menuItemText,
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
                Checked = Options.DisplaySampleTypes.Contains(sampleType)
            };
            menuItem.Click += (sender, args) =>
            {
                Options = Options.SetDisplaySampleType(sampleType, !menuItem.Checked);
            };
            return menuItem;
        }
        private SrmDocument SetExcludeStandard(SrmDocument document, IdentityPath peptideIdPath, int resultsIndex, bool exclude)
        {
            if (!document.Settings.HasResults)
            {
                return document;
            }
            var peptideDocNode = (PeptideDocNode)document.FindNode(peptideIdPath);
            if (peptideDocNode == null)
            {
                return document;
            }
            if (resultsIndex < 0 || resultsIndex >= document.Settings.MeasuredResults.Chromatograms.Count)
            {
                return document;
            }
            bool wasExcluded = peptideDocNode.IsExcludeFromCalibration(resultsIndex);
            return (SrmDocument)document.ReplaceChild(peptideIdPath.Parent,
                peptideDocNode.ChangeExcludeFromCalibration(resultsIndex, !wasExcluded));
        }

        private void logXAxisContextMenuItem_Click(object sender, EventArgs e)
        {
            Options = Options.ChangeLogXAxis(!Options.LogXAxis);
        }

        private void logYAxisContextMenuItem_Click(object sender, EventArgs e)
        {
            Options = Options.ChangeLogYAxis(!Options.LogYAxis);
        }
        private void showLegendContextMenuItem_Click(object sender, EventArgs e)
        {
            Options = Options.ChangeShowLegend(!Options.ShowLegend);
        }

        private void showSelectionContextMenuItem_Click(object sender, EventArgs e)
        {
            Options = Options.ChangeShowSelection(!Options.ShowSelection);
        }


        private void showFiguresOfMeritContextMenuItem_Click(object sender, EventArgs e)
        {
            Options = Options.ChangeShowFiguresOfMerit(!Options.ShowFiguresOfMerit);
        }

        private void singleBatchContextMenuItem_Click(object sender, EventArgs e)
        {
            Options = Options.ChangeSingleBatch(!Options.SingleBatch);
        }

        private void showBootstrapCurvesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Options = Options.ChangeShowBootstrapCurves(!Options.ShowBootstrapCurves);
        }

        private void moreDisplayOptionsContextMenuItem_Click(object sender, EventArgs e)
        {
            using (var dlg = new CalibrationCurveOptionsDlg())
            {
                dlg.ShowDialog(this);
            }
        }

        private double GetMaxY(IPointList pointList)
        {
            var max = double.MinValue;
            for (int i = 0; i < pointList.Count; i++)
            {
                max = Math.Max(max, pointList[i].Y);
            }

            return max;
        }
    }
}
