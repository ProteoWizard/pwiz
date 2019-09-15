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
using System.Linq;
using MathNet.Numerics.Statistics;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class CalibrationCurveFitter
    {
        private readonly IDictionary<CalibrationPoint, IDictionary<IdentityPath, PeptideQuantifier.Quantity>> _replicateQuantities
            = new Dictionary<CalibrationPoint, IDictionary<IdentityPath, PeptideQuantifier.Quantity>>();

        private HashSet<IdentityPath> _transitionsToQuantifyOn;

        public CalibrationCurveFitter(PeptideQuantifier peptideQuantifier, SrmSettings srmSettings)
        {
            PeptideQuantifier = peptideQuantifier;
            SrmSettings = srmSettings;
            IsotopologResponseCurve = peptideQuantifier.PeptideDocNode.HasPrecursorConcentrations;
        }

        public static CalibrationCurveFitter GetCalibrationCurveFitter(SrmSettings srmSettings,
            PeptideGroupDocNode peptideGroup, PeptideDocNode peptide)
        {
            return new CalibrationCurveFitter(PeptideQuantifier.GetPeptideQuantifier(null, srmSettings, peptideGroup, peptide), srmSettings);
        }

        public PeptideQuantifier PeptideQuantifier { get; private set; }

        public QuantificationSettings QuantificationSettings
        {
            get { return PeptideQuantifier.QuantificationSettings; }
        }
        public SrmSettings SrmSettings { get; private set; }

        public bool IsotopologResponseCurve { get; set; }

        public int? SingleBatchReplicateIndex { get; set; }

        public IDictionary<IdentityPath, PeptideQuantifier.Quantity> GetTransitionQuantities(CalibrationPoint calibrationPoint)
        {
            IDictionary<IdentityPath, PeptideQuantifier.Quantity> result;
            if (!_replicateQuantities.TryGetValue(calibrationPoint, out result))
            {
                if (calibrationPoint.LabelType == null)
                {
                    result = PeptideQuantifier.GetTransitionIntensities(SrmSettings, calibrationPoint.ReplicateIndex, false);
                }
                else
                {
                    result = new Dictionary<IdentityPath, PeptideQuantifier.Quantity>
                    {
                        {
                            IdentityPath.ROOT,
                            new PeptideQuantifier.Quantity(PeptideQuantifier.GetIsotopologArea(SrmSettings, calibrationPoint.ReplicateIndex,
                                calibrationPoint.LabelType), 1)
                        }
                    };
                }
                _replicateQuantities.Add(calibrationPoint, result);
            }
            return result;
        }

        public double? GetPeptideConcentration(ChromatogramSet chromatogramSet)
        {
            if (null == chromatogramSet)
            {
                return null;
            }
            double concentrationMultiplier = PeptideQuantifier.PeptideDocNode.ConcentrationMultiplier.GetValueOrDefault(1.0);
            return chromatogramSet.AnalyteConcentration*concentrationMultiplier/chromatogramSet.SampleDilutionFactor;
        }

        public double? GetPeptideConcentration(CalibrationPoint calibrationPoint)
        {
            var chromatogramSet = GetChromatogramSet(calibrationPoint.ReplicateIndex);
            if (chromatogramSet == null)
            {
                return null;
            }
            if (calibrationPoint.LabelType != null)
            {
                var transitionGroup = PeptideQuantifier.PeptideDocNode.TransitionGroups.FirstOrDefault(tg =>
                    Equals(tg.LabelType, calibrationPoint.LabelType) && tg.PrecursorConcentration.HasValue);
                if (transitionGroup == null)
                {
                    return null;
                }
                return transitionGroup.PrecursorConcentration / chromatogramSet.SampleDilutionFactor;
            }
            return GetPeptideConcentration(chromatogramSet);
        }

        public IEnumerable<CalibrationPoint> EnumerateCalibrationPoints()
        {
            return EnumerateLabelTypes().SelectMany(labelType =>
                EnumerateReplicates().Select(replicateIndex => new CalibrationPoint(replicateIndex, labelType)));
        }

        public IEnumerable<IsotopeLabelType> EnumerateLabelTypes()
        {
            if (!IsotopologResponseCurve)
            {
                return new IsotopeLabelType[] {null};
            }
            return PeptideQuantifier.PeptideDocNode.TransitionGroups.Select(tg => tg.LabelType)
                .Distinct().OrderBy(labelType=>labelType);
        }

        public IEnumerable<int> EnumerateReplicates()
        {
            if (!SrmSettings.HasResults)
            {
                return new int[0];
            }
            if (SingleBatchReplicateIndex.HasValue)
            {
                var chromatogramSet = SrmSettings.MeasuredResults.Chromatograms[SingleBatchReplicateIndex.Value];
                if (string.IsNullOrEmpty(chromatogramSet.BatchName))
                {
                    return new[] { SingleBatchReplicateIndex.Value };
                }

                return Enumerable.Range(0, SrmSettings.MeasuredResults.Chromatograms.Count)
                    .Where(i => SrmSettings.MeasuredResults.Chromatograms[i].BatchName == chromatogramSet.BatchName);
            }
            return Enumerable.Range(0, SrmSettings.MeasuredResults.Chromatograms.Count);
        }

        public IDictionary<CalibrationPoint, double> GetStandardConcentrations()
        {
            Dictionary<CalibrationPoint, double> result = new Dictionary<CalibrationPoint, double>();
            var measuredResults = SrmSettings.MeasuredResults;
            if (null == measuredResults)
            {
                return result;
            }
            if (IsotopologResponseCurve)
            {
                foreach (var precursor in PeptideQuantifier.PeptideDocNode.TransitionGroups)
                {
                    if (precursor.PrecursorConcentration.HasValue)
                    {
                        foreach (var replicateIndex in EnumerateReplicates())
                        {
                            var standardIdentifier = new CalibrationPoint(replicateIndex, precursor.LabelType);
                            result[standardIdentifier] = precursor.PrecursorConcentration.Value / measuredResults.Chromatograms[replicateIndex].SampleDilutionFactor;
                        }
                    }
                }
            }
            else
            {
                foreach (int replicateIndex in EnumerateReplicates())
                {
                    var chromatogramSet = measuredResults.Chromatograms[replicateIndex];
                    if (!SampleType.STANDARD.Equals(chromatogramSet.SampleType))
                    {
                        continue;
                    }
                    if (PeptideQuantifier.PeptideDocNode.IsExcludeFromCalibration(replicateIndex))
                    {
                        continue;
                    }
                    double? concentration = GetPeptideConcentration(chromatogramSet);
                    if (concentration.HasValue)
                    {
                        result.Add(new CalibrationPoint(replicateIndex, null), concentration.Value);
                    }
                }
            }
            return result;
        }

        public HashSet<IdentityPath> GetTransitionsToQuantifyOn()
        {
            if (IsAllowMissingTransitions())
            {
                return null;
            }
            if (null == _transitionsToQuantifyOn)
            {
                HashSet<IdentityPath> identities = new HashSet<IdentityPath>();
                foreach (var entry in GetStandardConcentrations())
                {
                    identities.UnionWith(GetTransitionQuantities(entry.Key).Keys);
                }
                if (!identities.Any())
                {
                    var measuredResults = SrmSettings.MeasuredResults;
                    if (null != measuredResults)
                    {
                        for (int replicateIndex = 0;
                            replicateIndex < measuredResults.Chromatograms.Count;
                            replicateIndex++)
                        {
                            identities.UnionWith(GetTransitionQuantities(new CalibrationPoint(replicateIndex, null)).Keys);
                        }
                    }
                }
                _transitionsToQuantifyOn = identities;
            }
            return _transitionsToQuantifyOn;
        }

        public IEnumerable<CalibrationPoint> GetValidStandardReplicates()
        {
            var completeTransitions = GetTransitionsToQuantifyOn();
            foreach (var entry in GetStandardConcentrations())
            {
                var transitionQuantities = GetTransitionQuantities(entry.Key);
                if (null == completeTransitions)
                {
                    if (transitionQuantities.Count == 0)
                    {
                        continue;
                    }
                }
                else
                {
                    if (transitionQuantities.Count != completeTransitions.Count)
                    {
                        continue;
                    }
                }
                yield return entry.Key;
            }
        }

        public double? GetNormalizedPeakArea(CalibrationPoint calibrationPoint)
        {
            var allTransitionQuantities = GetTransitionQuantities(calibrationPoint);
            ICollection<PeptideQuantifier.Quantity> quantitiesToSum;
            if (!IsAllowMissingTransitions())
            {
                var completeTransitionSet = GetTransitionsToQuantifyOn();
                quantitiesToSum = allTransitionQuantities
                    .Where(entry => completeTransitionSet.Contains(entry.Key))
                    .Select(entry => entry.Value)
                    .ToArray();
                if (quantitiesToSum.Count != completeTransitionSet.Count)
                {
                    return null;
                }
            }
            else
            {
                quantitiesToSum = allTransitionQuantities.Values;
            }
            return PeptideQuantifier.SumQuantities(quantitiesToSum, PeptideQuantifier.NormalizationMethod);
        }

        public CalibrationCurve GetCalibrationCurve()
        {
            if (RegressionFit.NONE.Equals(QuantificationSettings.RegressionFit))
            {
                if (HasInternalStandardConcentration())
                {
                    return CalibrationCurve.NO_EXTERNAL_STANDARDS
                        .ChangeSlope(1/PeptideQuantifier.PeptideDocNode.InternalStandardConcentration.GetValueOrDefault(1.0));
                }
                return CalibrationCurve.NO_EXTERNAL_STANDARDS;
            }
            if (IsotopologResponseCurve)
            {
                var concentrationsByLabel = new Dictionary<IsotopeLabelType, double>();
                foreach (var transitionGroup in PeptideQuantifier.PeptideDocNode.TransitionGroups)
                {
                    if (!transitionGroup.PrecursorConcentration.HasValue)
                    {
                        continue;
                    }
                    double prevConcentration;
                    if (concentrationsByLabel.TryGetValue(transitionGroup.LabelType, out prevConcentration))
                    {
                        if (!Equals(prevConcentration, transitionGroup.PrecursorConcentration.Value))
                        {
                            string message =
                                string.Format(
                                    Resources
                                        .CalibrationCurveFitter_GetCalibrationCurve_Unable_to_calculate_the_calibration_curve_for_the_because_there_are_different_Precursor_Concentrations_specified_for_the_label__0__,
                                    transitionGroup.LabelType);
                            return new CalibrationCurve().ChangeErrorMessage(message);
                        }
                    }
                    else
                    {
                        concentrationsByLabel.Add(transitionGroup.LabelType, transitionGroup.PrecursorConcentration.Value);
                    }
                }
            }
            List<WeightedPoint> weightedPoints = new List<WeightedPoint>();
            foreach (var replicateIndex in GetValidStandardReplicates())
            {
                double? intensity = GetYValue(replicateIndex);
                if (!intensity.HasValue)
                {
                    continue;
                }
                double x = GetSpecifiedXValue(replicateIndex).Value;
                double weight = QuantificationSettings.RegressionWeighting.GetWeighting(x, intensity.Value);
                WeightedPoint weightedPoint = new WeightedPoint(x, intensity.Value, weight);
                weightedPoints.Add(weightedPoint);
            }
            if (weightedPoints.Count == 0)
            {
                return new CalibrationCurve()
                    .ChangeErrorMessage(QuantificationStrings.CalibrationCurveFitter_GetCalibrationCurve_All_of_the_external_standards_are_missing_one_or_more_peaks_);
            }
            return FindBestLodForPoints(weightedPoints);
        }

        public FiguresOfMerit GetFiguresOfMerit(CalibrationCurve calibrationCurve)
        {
            var figuresOfMerit = FiguresOfMerit.EMPTY;
            if (calibrationCurve != null)
            {
                figuresOfMerit = figuresOfMerit.ChangeLimitOfDetection(
                    QuantificationSettings.LodCalculation.CalculateLod(calibrationCurve, this));
            }
            figuresOfMerit = figuresOfMerit.ChangeLimitOfQuantification(GetLimitOfQuantification(calibrationCurve));
            if (!FiguresOfMerit.EMPTY.Equals(figuresOfMerit))
            {
                figuresOfMerit = figuresOfMerit.ChangeUnits(QuantificationSettings.Units);
            }
            return figuresOfMerit;
        }

        public double? GetLimitOfQuantification(CalibrationCurve calibrationCurve)
        {
            if (!QuantificationSettings.MaxLoqBias.HasValue && !QuantificationSettings.MaxLoqCv.HasValue)
            {
                return null;
            }
            var concentrationReplicateLookup = GetStandardConcentrations().ToLookup(entry=>entry.Value, entry=>entry.Key);
            foreach (var concentrationReplicate in concentrationReplicateLookup.OrderBy(grouping=>grouping.Key))
            {
                var peakAreas = new List<double>();
                foreach (var standardIdentifier in concentrationReplicate)
                {
                    double? peakArea = GetNormalizedPeakArea(standardIdentifier);
                    if (peakArea.HasValue)
                    {
                        peakAreas.Add(peakArea.Value);
                    }
                }
                if (QuantificationSettings.MaxLoqCv.HasValue)
                {
                    double cv = peakAreas.StandardDeviation() / peakAreas.Mean();
                    if (double.IsNaN(cv) || double.IsInfinity(cv))
                    {
                        continue;
                    }
                    if (cv * 100 > QuantificationSettings.MaxLoqCv)
                    {
                        continue;
                    }
                }
                if (QuantificationSettings.MaxLoqBias.HasValue)
                {
                    if (calibrationCurve == null)
                    {
                        continue;
                    }
                    double meanPeakArea = peakAreas.Mean();
                    double? backCalculatedConcentration =
                        GetConcentrationFromXValue(calibrationCurve.GetFittedX(meanPeakArea));
                    if (!backCalculatedConcentration.HasValue)
                    {
                        continue;
                    }
                    double bias = (concentrationReplicate.Key - backCalculatedConcentration.Value) /
                                  concentrationReplicate.Key;
                    if (double.IsNaN(bias) || double.IsInfinity(bias))
                    {
                        continue;
                    }
                    if (Math.Abs(bias * 100) > QuantificationSettings.MaxLoqBias.Value)
                    {
                        continue;
                    }
                }
                return concentrationReplicate.Key;
            }
            return null;
        }

        private CalibrationCurve GetCalibrationCurveFromPoints(IList<WeightedPoint> points)
        {
            return QuantificationSettings.RegressionFit.Fit(points);
        }

        private CalibrationCurve FindBestLodForPoints(IList<WeightedPoint> weightedPoints)
        {
            return GetCalibrationCurveFromPoints(weightedPoints);
        }

        public string GetXAxisTitle()
        {
            if (!HasExternalStandards() && !HasInternalStandardConcentration())
            {
                return GetYAxisTitle();
            }
            if (HasExternalStandards() && HasInternalStandardConcentration())
            {
                return ConcentrationRatioText(PeptideQuantifier.MeasuredLabelTypes, PeptideQuantifier.RatioLabelType);
            }
            return AppendUnits(QuantificationStrings.Analyte_Concentration, QuantificationSettings.Units);
        }

        public string GetYAxisTitle()
        {
            if (Equals(PeptideQuantifier.NormalizationMethod, NormalizationMethod.NONE))
            {
                return QuantificationStrings.CalibrationCurveFitter_GetYAxisTitle_Peak_Area;
            }
            if (null != PeptideQuantifier.RatioLabelType)
            {
                return PeakAreaRatioText(PeptideQuantifier.MeasuredLabelTypes, PeptideQuantifier.RatioLabelType);
            }
            return QuantificationStrings.CalibrationCurveFitter_GetYAxisTitle_Normalized_Peak_Area;
        }

        public SampleType GetSampleType(CalibrationPoint calibrationPoint)
        {
            if (null == calibrationPoint.LabelType)
            {
                ChromatogramSet chromatogramSet = SrmSettings.MeasuredResults.Chromatograms[calibrationPoint.ReplicateIndex];
                return chromatogramSet.SampleType;
            }

            return GetSpecifiedXValue(calibrationPoint).HasValue ? SampleType.STANDARD : SampleType.UNKNOWN;
        }

        public double? GetSpecifiedXValue(CalibrationPoint calibrationPoint)
        {
            var chromatogramSet = GetChromatogramSet(calibrationPoint.ReplicateIndex);
            if (chromatogramSet == null)
            {
                return null;
            }
            if (null != calibrationPoint.LabelType)
            {
                var transitionGroup = PeptideQuantifier.PeptideDocNode.TransitionGroups.FirstOrDefault(tg =>
                    Equals(tg.LabelType, calibrationPoint.LabelType) && tg.PrecursorConcentration.HasValue);
                if (transitionGroup != null)
                {
                    return transitionGroup.PrecursorConcentration / chromatogramSet.SampleDilutionFactor;
                }
                return null;
            }

            double? peptideConcentration = GetPeptideConcentration(chromatogramSet);
            if (peptideConcentration.HasValue)
            {
                if (HasExternalStandards() && HasInternalStandardConcentration())
                {
                    return peptideConcentration / PeptideQuantifier.PeptideDocNode.InternalStandardConcentration;
                }
                return peptideConcentration;
            }
            return null;            
        }

        public double? GetCalculatedXValue(CalibrationCurve calibrationCurve, CalibrationPoint calibrationPoint)
        {
            return calibrationCurve.GetX(GetYValue(calibrationPoint));
        }

        public double? GetCalculatedXValue(CalibrationCurve calibrationCurve, int iReplicate)
        {
            return GetCalculatedXValue(calibrationCurve, new CalibrationPoint(iReplicate, null));
        }

        public double? GetYValue(CalibrationPoint calibrationPoint)
        {
            return GetNormalizedPeakArea(calibrationPoint);
        }

        public double? GetYValue(int iReplicate)
        {
            return GetYValue(new CalibrationPoint(iReplicate, null));
        }
        public double? GetCalculatedConcentration(CalibrationCurve calibrationCurve, CalibrationPoint calibrationPoint)
        {
            if (!HasExternalStandards() && !HasInternalStandardConcentration())
            {
                return null;
            }
            return GetConcentrationFromXValue(GetCalculatedXValue(calibrationCurve, calibrationPoint) * GetDilutionFactor(calibrationPoint.ReplicateIndex));
        }

        public double? GetCalculatedConcentration(CalibrationCurve calibrationCurve, int iReplicate)
        {
            return GetCalculatedConcentration(calibrationCurve, new CalibrationPoint(iReplicate, null));
        }

        public double? GetConcentrationFromXValue(double? xValue)
        {
            if (HasExternalStandards() && HasInternalStandardConcentration())
            {
                return xValue * PeptideQuantifier.PeptideDocNode.InternalStandardConcentration;
            }
            return xValue;
        }

        public double GetDilutionFactor(int replicateIndex)
        {
            return SrmSettings.MeasuredResults.Chromatograms[replicateIndex].SampleDilutionFactor;
        }

        public bool IsExcluded(int replicateIndex)
        {
            ChromatogramSet chromatogramSet = SrmSettings.MeasuredResults.Chromatograms[replicateIndex];
            if (!chromatogramSet.SampleType.AllowExclude)
            {
                return false;
            }
            var peptideResults = PeptideQuantifier.PeptideDocNode.Results;
            if (null == peptideResults)
            {
                return false;
            }
            if (replicateIndex >= peptideResults.Count)
            {
                return false;
            }
            var peptideChromInfos = peptideResults[replicateIndex];
            return peptideChromInfos.Any(
                peptideChromInfo => null != peptideChromInfo && peptideChromInfo.ExcludeFromCalibration);
        }

        public bool HasExternalStandards()
        {
            return RegressionFit.NONE != QuantificationSettings.RegressionFit;
        }

        public bool HasInternalStandardConcentration()
        {
            return (PeptideQuantifier.NormalizationMethod is NormalizationMethod.RatioToLabel
                    || PeptideQuantifier.NormalizationMethod is NormalizationMethod.RatioToSurrogate)
                   && PeptideQuantifier.PeptideDocNode.InternalStandardConcentration.HasValue;
        }

        public bool IsAllowMissingTransitions()
        {
            if (IsotopologResponseCurve)
            {
                return true;
            }
            return PeptideQuantifier.NormalizationMethod is NormalizationMethod.RatioToLabel;
        }

        public QuantificationResult GetQuantificationResult(int replicateIndex)
        {
            QuantificationResult result = new QuantificationResult();
            CalibrationCurve calibrationCurve = GetCalibrationCurve();
            result = result.ChangeNormalizedArea(GetNormalizedPeakArea(new CalibrationPoint(replicateIndex, null)));
            if (HasExternalStandards() || HasInternalStandardConcentration())
            {
                double? calculatedConcentration = GetCalculatedConcentration(calibrationCurve, new CalibrationPoint(replicateIndex, null));
                result = result.ChangeCalculatedConcentration(calculatedConcentration);
                double? expectedConcentration = GetPeptideConcentration(GetChromatogramSet(replicateIndex));
                result = result.ChangeAccuracy(calculatedConcentration / expectedConcentration);
                result = result.ChangeUnits(SrmSettings.PeptideSettings.Quantification.Units);
            }
            return result;
        }

        public QuantificationResult GetPrecursorQuantificationResult(int replicateIndex, TransitionGroupDocNode transitionGroupDocNode)
        {
            QuantificationResult result = new QuantificationResult();
            var calibrationPoint = new CalibrationPoint(replicateIndex, transitionGroupDocNode.LabelType);
            CalibrationCurve calibrationCurve = GetCalibrationCurve();
            result = result.ChangeNormalizedArea(GetNormalizedPeakArea(calibrationPoint));
            if (HasExternalStandards() || HasInternalStandardConcentration())
            {
                double? calculatedConcentration = GetCalculatedConcentration(calibrationCurve, calibrationPoint);
                result = result.ChangeCalculatedConcentration(calculatedConcentration);
                double? expectedConcentration = transitionGroupDocNode.PrecursorConcentration;
                result = result.ChangeAccuracy(calculatedConcentration / expectedConcentration);
                result = result.ChangeUnits(SrmSettings.PeptideSettings.Quantification.Units);
            }
            return result;

        }

        public static String AppendUnits(String title, String units)
        {
            if (String.IsNullOrEmpty(units))
            {
                return title;
            }
            return TextUtil.SpaceSeparate(title, '(' + units + ')');
        }

        public static string PeakAreaRatioText(IsotopeLabelType numerator, IsotopeLabelType denominator)
        {
            return PeakAreaRatioText(new[]{numerator}, denominator);
        }

        public static string PeakAreaRatioText(ICollection<IsotopeLabelType> numerator, IsotopeLabelType denominator)
        {
            String denominatorTitle = denominator == null ? StandardType.SURROGATE_STANDARD.Title : denominator.Title;
            if (numerator.Count == 1)
            {
                return string.Format(QuantificationStrings.CalibrationCurveFitter_PeakAreaRatioText__0___1__Peak_Area_Ratio, numerator.First().Title, denominatorTitle);
            }
            return string.Format(QuantificationStrings.CalibrationCurveFitter_PeakAreaRatioText_Peak_Area_Ratio_to__0_, denominatorTitle);
        }

        public static string ConcentrationRatioText(IsotopeLabelType numerator, IsotopeLabelType denominator)
        {
            return ConcentrationRatioText(new[] {numerator}, denominator);
        }

        public static string ConcentrationRatioText(ICollection<IsotopeLabelType> numerator, IsotopeLabelType denominator)
        {
            String denominatorTitle = denominator == null ? StandardType.SURROGATE_STANDARD.Title : denominator.Title;
            if (numerator.Count == 1)
            {
                return string.Format(QuantificationStrings.CalibrationCurveFitter_ConcentrationRatioText__0___1__Concentration_Ratio, numerator.First().Title, denominatorTitle);
            }
            return string.Format(QuantificationStrings.CalibrationCurveFitter_ConcentrationRatioText_Concentration_Ratio_to__0_, denominatorTitle);
        }

        private ChromatogramSet GetChromatogramSet(int replicateIndex)
        {
            if (!SrmSettings.HasResults)
            {
                return null;
            }
            if (replicateIndex < 0 || replicateIndex >= SrmSettings.MeasuredResults.Chromatograms.Count)
            {
                return null;
            }
            return SrmSettings.MeasuredResults.Chromatograms[replicateIndex];
        }

        public bool IsEnableSingleBatch
        {
            get
            {
                if (IsotopologResponseCurve)
                {
                    return true;
                }

                return AnyBatchNames(SrmSettings);
            }
        }

        public static bool AnyBatchNames(SrmSettings srmSettings)
        {
            return srmSettings.HasResults &&
                srmSettings.MeasuredResults.Chromatograms.Any(c => !string.IsNullOrEmpty(c.BatchName));
        }
    }

    public struct CalibrationPoint
    {
        public CalibrationPoint(int replicateIndex, IsotopeLabelType labelType) : this()
        {
            ReplicateIndex = replicateIndex;
            LabelType = labelType;
        }
        public int ReplicateIndex { get; private set; }
        public IsotopeLabelType LabelType { get; private set; }
    }
}
