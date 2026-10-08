using Pwiz.Analysis.PeakFilters;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Util.Misc;

namespace Pwiz.Analysis.Tests.SpectrumProcessing;

/// <summary>
/// Port of cpp's <c>SpectrumList_PeakFilterTest.cpp::testIntensityThresholding()</c>, driving
/// <see cref="ThresholdFilter"/> through <see cref="SpectrumListPeakFilter"/> over cpp's own
/// 74-row table. Every expected array below is copied verbatim from that table, so these cases
/// check the port against cpp rather than against itself.
/// </summary>
/// <remarks>
/// cpp runs the table twice: once with the default m/s levels, where the filter must apply, and
/// once with the filter restricted to MS2 against an MS1 spectrum, where it must be a no-op.
/// Both loops are reproduced. Comparisons are exact, as they are in cpp - the filter only selects
/// peaks, it never recomputes them.
/// </remarks>
[TestClass]
public class ThresholdFilterTests
{
    private sealed record ThresholdCase(
        string Label,
        string InputMz, string InputIntensity,
        string ExpectedMz, string ExpectedIntensity,
        ThresholdingBy By, double Threshold, ThresholdingOrientation Orientation);

    private static readonly ThresholdCase[] Cases =
    {
        new("test empty spectrum",
            InputMz: "", InputIntensity: "",
            ExpectedMz: "", ExpectedIntensity: "",
            By: ThresholdingBy.AbsoluteIntensity, Threshold: 0.1, Orientation: ThresholdingOrientation.MostIntense),

        new("test empty spectrum",
            InputMz: "", InputIntensity: "",
            ExpectedMz: "", ExpectedIntensity: "",
            By: ThresholdingBy.FractionOfBasePeakIntensity, Threshold: 0.1, Orientation: ThresholdingOrientation.MostIntense),

        new("test empty spectrum",
            InputMz: "", InputIntensity: "",
            ExpectedMz: "", ExpectedIntensity: "",
            By: ThresholdingBy.FractionOfTotalIntensity, Threshold: 0.1, Orientation: ThresholdingOrientation.MostIntense),

        new("test empty spectrum",
            InputMz: "", InputIntensity: "",
            ExpectedMz: "", ExpectedIntensity: "",
            By: ThresholdingBy.FractionOfTotalIntensityCutoff, Threshold: 0.99, Orientation: ThresholdingOrientation.MostIntense),

        new("test empty spectrum",
            InputMz: "", InputIntensity: "",
            ExpectedMz: "", ExpectedIntensity: "",
            By: ThresholdingBy.Count, Threshold: 5, Orientation: ThresholdingOrientation.MostIntense),

        new("test one peak spectrum",
            InputMz: "1", InputIntensity: "10",
            ExpectedMz: "1", ExpectedIntensity: "10",
            By: ThresholdingBy.AbsoluteIntensity, Threshold: 0.1, Orientation: ThresholdingOrientation.MostIntense),

        new("test one peak spectrum",
            InputMz: "1", InputIntensity: "10",
            ExpectedMz: "1", ExpectedIntensity: "10",
            By: ThresholdingBy.FractionOfBasePeakIntensity, Threshold: 0.1, Orientation: ThresholdingOrientation.MostIntense),

        new("test one peak spectrum",
            InputMz: "1", InputIntensity: "10",
            ExpectedMz: "1", ExpectedIntensity: "10",
            By: ThresholdingBy.FractionOfTotalIntensity, Threshold: 0.1, Orientation: ThresholdingOrientation.MostIntense),

        new("test one peak spectrum",
            InputMz: "1", InputIntensity: "10",
            ExpectedMz: "1", ExpectedIntensity: "10",
            By: ThresholdingBy.FractionOfTotalIntensityCutoff, Threshold: 0.99, Orientation: ThresholdingOrientation.MostIntense),

        new("test one peak spectrum",
            InputMz: "1", InputIntensity: "10",
            ExpectedMz: "1", ExpectedIntensity: "10",
            By: ThresholdingBy.Count, Threshold: 5, Orientation: ThresholdingOrientation.MostIntense),

        new("test two peak spectrum with a zero data point",
            InputMz: "1 2", InputIntensity: "10 0",
            ExpectedMz: "1", ExpectedIntensity: "10",
            By: ThresholdingBy.AbsoluteIntensity, Threshold: 0.1, Orientation: ThresholdingOrientation.MostIntense),

        new("test two peak spectrum with a zero data point",
            InputMz: "1 2", InputIntensity: "10 0",
            ExpectedMz: "1", ExpectedIntensity: "10",
            By: ThresholdingBy.FractionOfBasePeakIntensity, Threshold: 0.1, Orientation: ThresholdingOrientation.MostIntense),

        new("test two peak spectrum with a zero data point",
            InputMz: "1 2", InputIntensity: "10 0",
            ExpectedMz: "1", ExpectedIntensity: "10",
            By: ThresholdingBy.FractionOfTotalIntensity, Threshold: 0.1, Orientation: ThresholdingOrientation.MostIntense),

        new("test two peak spectrum with a zero data point",
            InputMz: "1 2", InputIntensity: "10 0",
            ExpectedMz: "1", ExpectedIntensity: "10",
            By: ThresholdingBy.FractionOfTotalIntensityCutoff, Threshold: 0.99, Orientation: ThresholdingOrientation.MostIntense),

        new("test two peak spectrum with a zero data point",
            InputMz: "1 2", InputIntensity: "10 0",
            ExpectedMz: "1 2", ExpectedIntensity: "10 0",
            By: ThresholdingBy.Count, Threshold: 5, Orientation: ThresholdingOrientation.MostIntense),

        new("absolute thresholding, keeping the most intense points",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 2 3 4 5", ExpectedIntensity: "10 20 30 20 10",
            By: ThresholdingBy.AbsoluteIntensity, Threshold: 5, Orientation: ThresholdingOrientation.MostIntense),

        new("absolute thresholding, keeping the most intense points",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "2 3 4", ExpectedIntensity: "20 30 20",
            By: ThresholdingBy.AbsoluteIntensity, Threshold: 10, Orientation: ThresholdingOrientation.MostIntense),

        new("absolute thresholding, keeping the most intense points",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "2 3 4", ExpectedIntensity: "20 30 20",
            By: ThresholdingBy.AbsoluteIntensity, Threshold: 15, Orientation: ThresholdingOrientation.MostIntense),

        new("absolute thresholding, keeping the most intense points",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "", ExpectedIntensity: "",
            By: ThresholdingBy.AbsoluteIntensity, Threshold: 30, Orientation: ThresholdingOrientation.MostIntense),

        new("absolute thresholding, keeping the least intense points",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "", ExpectedIntensity: "",
            By: ThresholdingBy.AbsoluteIntensity, Threshold: 5, Orientation: ThresholdingOrientation.LeastIntense),

        new("absolute thresholding, keeping the least intense points",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "", ExpectedIntensity: "",
            By: ThresholdingBy.AbsoluteIntensity, Threshold: 10, Orientation: ThresholdingOrientation.LeastIntense),

        new("absolute thresholding, keeping the least intense points",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 5", ExpectedIntensity: "10 10",
            By: ThresholdingBy.AbsoluteIntensity, Threshold: 15, Orientation: ThresholdingOrientation.LeastIntense),

        new("absolute thresholding, keeping the least intense points",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 2 4 5", ExpectedIntensity: "10 20 20 10",
            By: ThresholdingBy.AbsoluteIntensity, Threshold: 30, Orientation: ThresholdingOrientation.LeastIntense),

        new("absolute thresholding, keeping the least intense points",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 2 3 4 5", ExpectedIntensity: "10 20 30 20 10",
            By: ThresholdingBy.AbsoluteIntensity, Threshold: 50, Orientation: ThresholdingOrientation.LeastIntense),

        new("relative thresholding to the base peak, keeping the most intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 2 3 4 5", ExpectedIntensity: "10 20 30 20 10",
            By: ThresholdingBy.FractionOfBasePeakIntensity, Threshold: 0.1, Orientation: ThresholdingOrientation.MostIntense),

        new("relative thresholding to the base peak, keeping the most intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "2 3 4", ExpectedIntensity: "20 30 20",
            By: ThresholdingBy.FractionOfBasePeakIntensity, Threshold: 0.34, Orientation: ThresholdingOrientation.MostIntense),

        new("relative thresholding to the base peak, keeping the most intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "2 3 4", ExpectedIntensity: "20 30 20",
            By: ThresholdingBy.FractionOfBasePeakIntensity, Threshold: 0.65, Orientation: ThresholdingOrientation.MostIntense),

        new("relative thresholding to the base peak, keeping the most intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "3", ExpectedIntensity: "30",
            By: ThresholdingBy.FractionOfBasePeakIntensity, Threshold: 0.67, Orientation: ThresholdingOrientation.MostIntense),

        new("relative thresholding to the base peak, keeping the most intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "", ExpectedIntensity: "",
            By: ThresholdingBy.FractionOfBasePeakIntensity, Threshold: 1.0, Orientation: ThresholdingOrientation.MostIntense),

        new("relative thresholding to the base peak, keeping the least intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "", ExpectedIntensity: "",
            By: ThresholdingBy.FractionOfBasePeakIntensity, Threshold: 0.1, Orientation: ThresholdingOrientation.LeastIntense),

        new("relative thresholding to the base peak, keeping the least intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "", ExpectedIntensity: "",
            By: ThresholdingBy.FractionOfBasePeakIntensity, Threshold: 0.32, Orientation: ThresholdingOrientation.LeastIntense),

        new("relative thresholding to the base peak, keeping the least intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 5", ExpectedIntensity: "10 10",
            By: ThresholdingBy.FractionOfBasePeakIntensity, Threshold: 0.34, Orientation: ThresholdingOrientation.LeastIntense),

        new("relative thresholding to the base peak, keeping the least intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 2 4 5", ExpectedIntensity: "10 20 20 10",
            By: ThresholdingBy.FractionOfBasePeakIntensity, Threshold: 0.67, Orientation: ThresholdingOrientation.LeastIntense),

        new("relative thresholding to the base peak, keeping the least intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 2 4 5", ExpectedIntensity: "10 20 20 10",
            By: ThresholdingBy.FractionOfBasePeakIntensity, Threshold: 1.0, Orientation: ThresholdingOrientation.LeastIntense),

        new("relative thresholding to total intensity, keeping the most intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 2 3 4 5", ExpectedIntensity: "10 20 30 20 10",
            By: ThresholdingBy.FractionOfTotalIntensity, Threshold: 0.1, Orientation: ThresholdingOrientation.MostIntense),

        new("relative thresholding to total intensity, keeping the most intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "2 3 4", ExpectedIntensity: "20 30 20",
            By: ThresholdingBy.FractionOfTotalIntensity, Threshold: 0.12, Orientation: ThresholdingOrientation.MostIntense),

        new("relative thresholding to total intensity, keeping the most intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "2 3 4", ExpectedIntensity: "20 30 20",
            By: ThresholdingBy.FractionOfTotalIntensity, Threshold: 0.21, Orientation: ThresholdingOrientation.MostIntense),

        new("relative thresholding to total intensity, keeping the most intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "3", ExpectedIntensity: "30",
            By: ThresholdingBy.FractionOfTotalIntensity, Threshold: 0.23, Orientation: ThresholdingOrientation.MostIntense),

        new("relative thresholding to total intensity, keeping the most intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "", ExpectedIntensity: "",
            By: ThresholdingBy.FractionOfTotalIntensity, Threshold: 0.34, Orientation: ThresholdingOrientation.MostIntense),

        new("relative thresholding to total intensity, keeping the least intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "", ExpectedIntensity: "",
            By: ThresholdingBy.FractionOfTotalIntensity, Threshold: 0.1, Orientation: ThresholdingOrientation.LeastIntense),

        new("relative thresholding to total intensity, keeping the least intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 5", ExpectedIntensity: "10 10",
            By: ThresholdingBy.FractionOfTotalIntensity, Threshold: 0.12, Orientation: ThresholdingOrientation.LeastIntense),

        new("relative thresholding to total intensity, keeping the least intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 5", ExpectedIntensity: "10 10",
            By: ThresholdingBy.FractionOfTotalIntensity, Threshold: 0.21, Orientation: ThresholdingOrientation.LeastIntense),

        new("relative thresholding to total intensity, keeping the least intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 2 4 5", ExpectedIntensity: "10 20 20 10",
            By: ThresholdingBy.FractionOfTotalIntensity, Threshold: 0.23, Orientation: ThresholdingOrientation.LeastIntense),

        new("relative thresholding to total intensity, keeping the least intense peaks",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 2 3 4 5", ExpectedIntensity: "10 20 30 20 10",
            By: ThresholdingBy.FractionOfTotalIntensity, Threshold: 0.34, Orientation: ThresholdingOrientation.LeastIntense),

        new("at threshold .15 ---^ cut here",
            InputMz: "1 2 3 4 5 6 7 8 9", InputIntensity: "0 1 2 1 0 1 2 12 1",
            ExpectedMz: "2 3 4 6 7 8 9", ExpectedIntensity: "1 2 1 1 2 12 1",
            By: ThresholdingBy.FractionOfTotalIntensityCutoff, Threshold: 1.0, Orientation: ThresholdingOrientation.MostIntense),

        new("at threshold .15 ---^ cut here",
            InputMz: "1 2 3 4 5 6 7 8 9", InputIntensity: "0 1 2 1 0 1 2 12 1",
            ExpectedMz: "2 3 4 6 7 8 9", ExpectedIntensity: "1 2 1 1 2 12 1",
            By: ThresholdingBy.FractionOfTotalIntensityCutoff, Threshold: 0.99, Orientation: ThresholdingOrientation.MostIntense),

        new("at threshold .15 ---^ cut here",
            InputMz: "1 2 3 4 5 6 7 8 9", InputIntensity: "0 1 2 1 0 1 2 12 1",
            ExpectedMz: "2 3 4 6 7 8 9", ExpectedIntensity: "1 2 1 1 2 12 1",
            By: ThresholdingBy.FractionOfTotalIntensityCutoff, Threshold: 0.90, Orientation: ThresholdingOrientation.MostIntense),

        new("at threshold .15 ---^ cut here",
            InputMz: "1 2 3 4 5 6 7 8 9", InputIntensity: "0 1 2 1 0 1 2 12 1",
            ExpectedMz: "3 7 8", ExpectedIntensity: "2 2 12",
            By: ThresholdingBy.FractionOfTotalIntensityCutoff, Threshold: 0.80, Orientation: ThresholdingOrientation.MostIntense),

        new("at threshold .15 ---^ cut here",
            InputMz: "1 2 3 4 5 6 7 8 9", InputIntensity: "0 1 2 1 0 1 2 12 1",
            ExpectedMz: "3 7 8", ExpectedIntensity: "2 2 12",
            By: ThresholdingBy.FractionOfTotalIntensityCutoff, Threshold: 0.65, Orientation: ThresholdingOrientation.MostIntense),

        new("at threshold .15 ---^ cut here",
            InputMz: "1 2 3 4 5 6 7 8 9", InputIntensity: "0 1 2 1 0 1 2 12 1",
            ExpectedMz: "8", ExpectedIntensity: "12",
            By: ThresholdingBy.FractionOfTotalIntensityCutoff, Threshold: 0.60, Orientation: ThresholdingOrientation.MostIntense),

        new("at threshold .15 ---^ cut here",
            InputMz: "1 2 3 4 5 6 7 8 9", InputIntensity: "0 1 2 1 0 1 2 12 1",
            ExpectedMz: "8", ExpectedIntensity: "12",
            By: ThresholdingBy.FractionOfTotalIntensityCutoff, Threshold: 0.15, Orientation: ThresholdingOrientation.MostIntense),

        new("at threshold .01 -----------------------^ cut here",
            InputMz: "1 2 3 4 5 6 7 8 9", InputIntensity: "0 1 2 1 0 1 2 12 1",
            ExpectedMz: "1 2 3 4 5 6 7 8 9", ExpectedIntensity: "0 1 2 1 0 1 2 12 1",
            By: ThresholdingBy.FractionOfTotalIntensityCutoff, Threshold: 1.0, Orientation: ThresholdingOrientation.LeastIntense),

        new("at threshold .01 -----------------------^ cut here",
            InputMz: "1 2 3 4 5 6 7 8 9", InputIntensity: "0 1 2 1 0 1 2 12 1",
            ExpectedMz: "1 2 3 4 5 6 7 8 9", ExpectedIntensity: "0 1 2 1 0 1 2 12 1",
            By: ThresholdingBy.FractionOfTotalIntensityCutoff, Threshold: 0.45, Orientation: ThresholdingOrientation.LeastIntense),

        new("at threshold .01 -----------------------^ cut here",
            InputMz: "1 2 3 4 5 6 7 8 9", InputIntensity: "0 1 2 1 0 1 2 12 1",
            ExpectedMz: "1 2 3 4 5 6 7 9", ExpectedIntensity: "0 1 2 1 0 1 2 1",
            By: ThresholdingBy.FractionOfTotalIntensityCutoff, Threshold: 0.40, Orientation: ThresholdingOrientation.LeastIntense),

        new("at threshold .01 -----------------------^ cut here",
            InputMz: "1 2 3 4 5 6 7 8 9", InputIntensity: "0 1 2 1 0 1 2 12 1",
            ExpectedMz: "1 2 3 4 5 6 7 9", ExpectedIntensity: "0 1 2 1 0 1 2 1",
            By: ThresholdingBy.FractionOfTotalIntensityCutoff, Threshold: 0.35, Orientation: ThresholdingOrientation.LeastIntense),

        new("at threshold .01 -----------------------^ cut here",
            InputMz: "1 2 3 4 5 6 7 8 9", InputIntensity: "0 1 2 1 0 1 2 12 1",
            ExpectedMz: "1 2 3 4 5 6 7 9", ExpectedIntensity: "0 1 2 1 0 1 2 1",
            By: ThresholdingBy.FractionOfTotalIntensityCutoff, Threshold: 0.25, Orientation: ThresholdingOrientation.LeastIntense),

        new("at threshold .01 -----------------------^ cut here",
            InputMz: "1 2 3 4 5 6 7 8 9", InputIntensity: "0 1 2 1 0 1 2 12 1",
            ExpectedMz: "1 2 4 5 6 9", ExpectedIntensity: "0 1 1 0 1 1",
            By: ThresholdingBy.FractionOfTotalIntensityCutoff, Threshold: 0.20, Orientation: ThresholdingOrientation.LeastIntense),

        new("at threshold .01 -----------------------^ cut here",
            InputMz: "1 2 3 4 5 6 7 8 9", InputIntensity: "0 1 2 1 0 1 2 12 1",
            ExpectedMz: "1 2 4 5 6 9", ExpectedIntensity: "0 1 1 0 1 1",
            By: ThresholdingBy.FractionOfTotalIntensityCutoff, Threshold: 0.15, Orientation: ThresholdingOrientation.LeastIntense),

        new("keep the <threshold> most intense points, excluding ties",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "3", ExpectedIntensity: "30",
            By: ThresholdingBy.Count, Threshold: 1, Orientation: ThresholdingOrientation.MostIntense),

        new("keep the <threshold> most intense points, excluding ties",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "3", ExpectedIntensity: "30",
            By: ThresholdingBy.Count, Threshold: 2, Orientation: ThresholdingOrientation.MostIntense),

        new("keep the <threshold> most intense points, excluding ties",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "2 3 4", ExpectedIntensity: "20 30 20",
            By: ThresholdingBy.Count, Threshold: 3, Orientation: ThresholdingOrientation.MostIntense),

        new("keep the <threshold> most intense points, excluding ties",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "2 3 4", ExpectedIntensity: "20 30 20",
            By: ThresholdingBy.Count, Threshold: 4, Orientation: ThresholdingOrientation.MostIntense),

        new("keep the <threshold> least intense points, excluding ties",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "", ExpectedIntensity: "",
            By: ThresholdingBy.Count, Threshold: 1, Orientation: ThresholdingOrientation.LeastIntense),

        new("keep the <threshold> least intense points, excluding ties",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 5", ExpectedIntensity: "10 10",
            By: ThresholdingBy.Count, Threshold: 2, Orientation: ThresholdingOrientation.LeastIntense),

        new("keep the <threshold> least intense points, excluding ties",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 5", ExpectedIntensity: "10 10",
            By: ThresholdingBy.Count, Threshold: 3, Orientation: ThresholdingOrientation.LeastIntense),

        new("keep the <threshold> least intense points, excluding ties",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 2 4 5", ExpectedIntensity: "10 20 20 10",
            By: ThresholdingBy.Count, Threshold: 4, Orientation: ThresholdingOrientation.LeastIntense),

        new("keep the <threshold> most intense points, including ties",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "3", ExpectedIntensity: "30",
            By: ThresholdingBy.CountAfterTies, Threshold: 1, Orientation: ThresholdingOrientation.MostIntense),

        new("keep the <threshold> most intense points, including ties",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "2 3 4", ExpectedIntensity: "20 30 20",
            By: ThresholdingBy.CountAfterTies, Threshold: 2, Orientation: ThresholdingOrientation.MostIntense),

        new("keep the <threshold> most intense points, including ties",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "2 3 4", ExpectedIntensity: "20 30 20",
            By: ThresholdingBy.CountAfterTies, Threshold: 3, Orientation: ThresholdingOrientation.MostIntense),

        new("keep the <threshold> most intense points, including ties",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 2 3 4 5", ExpectedIntensity: "10 20 30 20 10",
            By: ThresholdingBy.CountAfterTies, Threshold: 4, Orientation: ThresholdingOrientation.MostIntense),

        new("keep the <threshold> least intense points, including ties",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 5", ExpectedIntensity: "10 10",
            By: ThresholdingBy.CountAfterTies, Threshold: 1, Orientation: ThresholdingOrientation.LeastIntense),

        new("keep the <threshold> least intense points, including ties",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 5", ExpectedIntensity: "10 10",
            By: ThresholdingBy.CountAfterTies, Threshold: 2, Orientation: ThresholdingOrientation.LeastIntense),

        new("keep the <threshold> least intense points, including ties",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 2 4 5", ExpectedIntensity: "10 20 20 10",
            By: ThresholdingBy.CountAfterTies, Threshold: 3, Orientation: ThresholdingOrientation.LeastIntense),

        new("keep the <threshold> least intense points, including ties",
            InputMz: "1 2 3 4 5", InputIntensity: "10 20 30 20 10",
            ExpectedMz: "1 2 4 5", ExpectedIntensity: "10 20 20 10",
            By: ThresholdingBy.CountAfterTies, Threshold: 4, Orientation: ThresholdingOrientation.LeastIntense),

    };

    [TestMethod]
    public void ThresholdFilter_MatchesCppTable()
    {
        for (int i = 0; i < Cases.Length; i++)
        {
            var c = Cases[i];
            var filtered = Filter(c, new ThresholdFilter(c.By, c.Threshold, c.Orientation), msLevel: 2);
            AssertArrays($"[{i}] {c.Label}", c.ExpectedMz, c.ExpectedIntensity, filtered);
        }
    }

    /// <summary>cpp's second loop: restricted to MS2, the filter must leave an MS1 spectrum alone.</summary>
    [TestMethod]
    public void ThresholdFilter_RestrictedToMs2_LeavesMs1Untouched()
    {
        for (int i = 0; i < Cases.Length; i++)
        {
            var c = Cases[i];
            var filter = new ThresholdFilter(c.By, c.Threshold, c.Orientation, new IntegerSet(2));
            var unfiltered = Filter(c, filter, msLevel: 1);
            AssertArrays($"[{i}] {c.Label} (ms1 untouched)", c.InputMz, c.InputIntensity, unfiltered);
        }
    }

    private static Spectrum Filter(ThresholdCase c, ThresholdFilter filter, int msLevel)
    {
        var inner = new SpectrumListSimple();
        var s = new Spectrum { Index = 0, Id = "scan=1" };
        s.Params.Set(CVID.MS_ms_level, msLevel);
        s.SetMZIntensityArrays(ParseDoubles(c.InputMz), ParseDoubles(c.InputIntensity),
            CVID.MS_number_of_detector_counts);
        inner.Spectra.Add(s);
        return new SpectrumListPeakFilter(inner, filter).GetSpectrum(0, getBinaryData: true);
    }

    private static void AssertArrays(string label, string expectedMz, string expectedIntensity, Spectrum actual)
    {
        var expectedMzArr = ParseDoubles(expectedMz);
        var expectedIntArr = ParseDoubles(expectedIntensity);
        var actualMz = actual.GetMZArray()?.Data ?? new List<double>();
        var actualInt = actual.GetIntensityArray()?.Data ?? new List<double>();

        Assert.AreEqual(expectedMzArr.Length, actual.DefaultArrayLength, $"{label}: defaultArrayLength");
        CollectionAssert.AreEqual(expectedMzArr, actualMz.ToArray(), $"{label}: m/z array");
        CollectionAssert.AreEqual(expectedIntArr, actualInt.ToArray(), $"{label}: intensity array");
    }

    private static double[] ParseDoubles(string s) =>
        s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
         .Select(t => double.Parse(t, System.Globalization.NumberStyles.Float,
             System.Globalization.CultureInfo.InvariantCulture))
         .ToArray();
}
