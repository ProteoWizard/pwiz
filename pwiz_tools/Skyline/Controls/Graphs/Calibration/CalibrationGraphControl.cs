using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
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
        }

        public Helpers.ModeUIAwareFormHelper 

        public Settings DisplaySettings { get; private set; }

        public void Update(Settings displaySettings)
        {
            DisplaySettings = displaySettings;
            var options = displaySettings.Options;
            zedGraphControl.GraphPane.YAxis.Type = options.LogYAxis ? AxisType.Log : AxisType.Linear;
            zedGraphControl.GraphPane.XAxis.Type = options.LogXAxis ? AxisType.Log : AxisType.Linear;
            bool logPlot = options.LogXAxis || options.LogYAxis;
            zedGraphControl.GraphPane.Legend.IsVisible = options.ShowLegend;
            _scatterPlots = null;
            CalibrationCurve = null;
            FiguresOfMerit = FiguresOfMerit.EMPTY;
            SrmDocument document = displaySettings.Document;
            if (!document.Settings.HasResults)
            {
                zedGraphControl.GraphPane.Title.Text =
                    QuantificationStrings.CalibrationForm_DisplayCalibrationCurve_No_results_available;
                return;
            }
            CalibrationCurveFitter curveFitter = displaySettings.CalibrationCurveFitter;
            var peptideQuantifier = curveFitter.PeptideQuantifier;
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
            curveFitter.GetCalibrationCurveAndMetrics(out CalibrationCurve calibrationCurve, out CalibrationCurveMetrics calibrationCurveRow);
            CalibrationCurve = calibrationCurve;
            CalibrationCurveMetrics = calibrationCurveRow;

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

        public CalibrationCurve CalibrationCurve { get; private set; }
        public FiguresOfMerit FiguresOfMerit { get; private set; }

        public class Settings
        {
            private CalibrationCurveOptions _options;
            public Settings(SrmDocument document, CalibrationCurveFitter calibrationCurveFitter,
                CalibrationCurveOptions options)
            {
                Document = document;
                CalibrationCurveFitter = calibrationCurveFitter;
                _options = options.Clone();
            }

            public SrmDocument Document { get; }
            public CalibrationCurveFitter CalibrationCurveFitter { get; }

            public CalibrationCurveOptions Options
            {
                get
                {
                    return _options.Clone();
                }
            }
        }
    }
}
