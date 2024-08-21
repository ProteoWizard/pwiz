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
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class CalibrationCurveFitter
    {
        private readonly IndexedList<IdentityPath> _identityPaths = new IndexedList<IdentityPath>();
        private readonly IDictionary<CalibrationPoint, ImmutableList<PeptideQuantifier.Quantity>> _replicateQuantities
            = new Dictionary<CalibrationPoint, ImmutableList<PeptideQuantifier.Quantity>>();
        private HashSet<IdentityPath> _transitionsToQuantifyOn;
        
        internal CalibrationCurveFitter(PeptideQuantifier peptideQuantifier, PeptideQuantifier standardPeptideQuantifier, SrmSettings srmSettings)
        {
            PeptideQuantifier = peptideQuantifier;
            StandardPeptideQuantifier = standardPeptideQuantifier;
            SrmSettings = srmSettings;
            IsotopologResponseCurve = peptideQuantifier.PeptideDocNode.HasPrecursorConcentrations;
            FiguresOfMeritCalculator = QuantificationSettings.GetFiguresOfMeritCalculator();
        }

        public static CalibrationCurveFitter GetCalibrationCurveFitter(Lazy<NormalizationData> getNormalizationDataFunc, SrmSettings settings,
            IdPeptideDocNode idPeptideDocNode)
        {
            var peptideQuantifier = PeptideQuantifier.GetPeptideQuantifier(getNormalizationDataFunc, settings, idPeptideDocNode.PeptideGroup, idPeptideDocNode.PeptideDocNode);
            PeptideQuantifier standardPeptideQuantifier = null;
            if (null != idPeptideDocNode.PeptideDocNode.SurrogateCalibrationCurve)
            {
                var standard = settings.GetSurrogateStandards(idPeptideDocNode.PeptideDocNode.SurrogateCalibrationCurve).FirstOrDefault();
                if (standard != null)
                {
                    standardPeptideQuantifier = PeptideQuantifier.GetPeptideQuantifier(getNormalizationDataFunc, settings, standard.PeptideGroup, standard.PeptideDocNode);
                }
            }
            return new CalibrationCurveFitter(peptideQuantifier, standardPeptideQuantifier, settings);
        }

        public static CalibrationCurveFitter GetCalibrationCurveFitter(SrmDocument document,
            IdPeptideDocNode idPeptideDocNode)
        {
            return GetCalibrationCurveFitter(NormalizationData.LazyNormalizationData(document), document.Settings,
                idPeptideDocNode);
        }


        public static CalibrationCurveFitter GetCalibrationCurveFitter(SrmDocument document,
            PeptideGroupDocNode peptideGroup, PeptideDocNode peptide)
        {
            return GetCalibrationCurveFitter(document, new IdPeptideDocNode(peptideGroup.PeptideGroup, peptide));
        }

        public PeptideQuantifier PeptideQuantifier { get; private set; }

        public PeptideQuantifier StandardPeptideQuantifier { get; private set; }

        public PeptideQuantifier GetPeptideQuantifier(SampleType sampleType)
        {
            if (SampleType.UNKNOWN.Equals(sampleType) || SampleType.QC.Equals(sampleType))
            {
                return PeptideQuantifier;
            }
            return StandardPeptideQuantifier ?? PeptideQuantifier;
        }

        public PeptideQuantifier GetPeptideQuantifier(CalibrationPoint calibrationPoint)
        {
            if (calibrationPoint.LabelType != null)
            {
                return PeptideQuantifier;
            }
            return GetPeptideQuantifier(GetSampleType(calibrationPoint));
        }

        public QuantificationSettings QuantificationSettings
        {
            get { return PeptideQuantifier.QuantificationSettings; }
        }
        public SrmSettings SrmSettings { get; private set; }

        public bool IsotopologResponseCurve { get; set; }

        public int? SingleBatchReplicateIndex { get; set; }
        public bool CombinePointsWithSameConcentration { get; set; }
        public IFiguresOfMeritCalculator FiguresOfMeritCalculator { get; set; }

        public IDictionary<IdentityPath, PeptideQuantifier.Quantity> GetTransitionQuantities(CalibrationPoint calibrationPoint)
        {
            ImmutableList<PeptideQuantifier.Quantity> quantities;
            if (!_replicateQuantities.TryGetValue(calibrationPoint, out quantities))
            {
                IDictionary<IdentityPath, PeptideQuantifier.Quantity> quantityDictionary;
                if (calibrationPoint.LabelType == null)
                {
                    quantityDictionary = GetPeptideQuantifier(calibrationPoint).GetTransitionIntensities(SrmSettings, calibrationPoint.ReplicateIndex, false);
                }
                else
                {
                    quantityDictionary = new Dictionary<IdentityPath, PeptideQuantifier.Quantity>
                    {
                        {
                            IdentityPath.ROOT,
                            new PeptideQuantifier.Quantity(GetPeptideQuantifier(calibrationPoint).GetIsotopologArea(SrmSettings, calibrationPoint.ReplicateIndex,
                                calibrationPoint.LabelType), 1, false)
                        }
                    };
                }

                var quantityList = new PeptideQuantifier.Quantity[_identityPaths.Count].ToList();
                foreach (var entry in quantityDictionary)
                {
                    int index = _identityPaths.IndexOf(entry.Key);
                    if (index >= 0)
                    {
                        quantityList[index] = entry.Value;
                    }
                    else
                    {
                        _identityPaths.Add(entry.Key);
                        quantityList.Add(entry.Value);
                    }
                }

                quantities = ImmutableList.ValueOf(quantityList);
                _replicateQuantities.Add(calibrationPoint, quantities);
            }
            return MakeQuantityDictionary(quantities);
        }

        public double? GetPeptideConcentration(int replicateIndex)
        {
            var chromatogramSet = GetChromatogramSet(replicateIndex);
            if (null == chromatogramSet)
            {
                return null;
            }

            var peptideDocNode = GetPeptideQuantifier(new CalibrationPoint(replicateIndex, null)).PeptideDocNode;
            var results = peptideDocNode.Results;
            if (null != results && 0 <= replicateIndex && replicateIndex < results.Count)
            {
                var peptideChromInfo = results[replicateIndex].FirstOrDefault();
                if (peptideChromInfo != null && peptideChromInfo.AnalyteConcentration.HasValue)
                {
                    return peptideChromInfo.AnalyteConcentration.Value;
                }
            }
            double concentrationMultiplier = peptideDocNode.ConcentrationMultiplier.GetValueOrDefault(1.0);
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
            return GetPeptideConcentration(calibrationPoint.ReplicateIndex);
        }

        public IEnumerable<CalibrationPoint> EnumerateCalibrationPoints()
        {
            return EnumerateLabelTypes().SelectMany(labelType =>
                EnumerateReplicates().Select(replicateIndex => new CalibrationPoint(replicateIndex, labelType)));
        }

        public IEnumerable<WeightedPoint> GetStandardPoints()
        {
            return GetValidStandardReplicates().Select(GetWeightedPoint).OfType<WeightedPoint>();
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
                return Array.Empty<int>();
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
                    if (GetPeptideQuantifier(chromatogramSet.SampleType).PeptideDocNode.IsExcludeFromCalibration(replicateIndex))
                    {
                        continue;
                    }
                    double? concentration = GetPeptideConcentration(replicateIndex);
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
            return GetAnnotatedNormalizedPeakArea(calibrationPoint)?.Strict;
        }

        public AnnotatedDouble GetAnnotatedNormalizedPeakArea(CalibrationPoint calibrationPoint)
        {
            var allTransitionQuantities = GetTransitionQuantities(calibrationPoint);
            if (allTransitionQuantities.Count == 0)
            {
                return null;
            }
            ICollection<IdentityPath> completeTransitionSet;
            if (IsAllowMissingTransitions())
            {
                if (allTransitionQuantities.Values.Any(v => v.Truncated) && !allTransitionQuantities.Values.All(v=>v.Truncated))
                {
                    completeTransitionSet = allTransitionQuantities.Where(kvp=>!kvp.Value.Truncated).Select(kvp=>kvp.Key).ToHashSet();
                }
                else
                {
                    completeTransitionSet = allTransitionQuantities.Keys;
                }
            }
            else
            {
                completeTransitionSet = GetTransitionsToQuantifyOn();
            }

            return GetPeptideQuantifier(calibrationPoint).SumTransitionQuantities(completeTransitionSet, allTransitionQuantities);
        }

        public CalibrationCurve GetCalibrationCurve()
        {
            GetCalibrationCurveAndMetrics(out CalibrationCurve calibrationCurve, out _);
            return calibrationCurve;
        }

        public CalibrationCurveMetrics GetCalibrationCurveMetrics()
        {
            GetCalibrationCurveAndMetrics(out CalibrationCurve _, out CalibrationCurveMetrics row);
            return row;
        }

        public void GetCalibrationCurveAndMetrics(out CalibrationCurve calibrationCurve,
            out CalibrationCurveMetrics calibrationCurveMetrics)
        {
            List<WeightedPoint> points = new List<WeightedPoint>();
            calibrationCurve = GetCalibrationCurveAndPoints(points);
            calibrationCurveMetrics = calibrationCurve.GetMetrics(points);
        }

        public CalibrationCurve GetCalibrationCurveAndPoints(List<WeightedPoint> points) 
        {
            if (RegressionFit.NONE.Equals(QuantificationSettings.RegressionFit))
            {
                if (HasInternalStandardConcentration())
                {
                    return new CalibrationCurve.Simple(1 / PeptideQuantifier.PeptideDocNode.InternalStandardConcentration.GetValueOrDefault(1.0));
                }

                return new CalibrationCurve.Simple(1);
            }

            var allPoints = new List<WeightedPoint>();
            foreach (var replicateIndex in GetValidStandardReplicates())
            {
                WeightedPoint? weightedPoint = GetWeightedPoint(replicateIndex);
                if (weightedPoint.HasValue)
                {
                    allPoints.Add(weightedPoint.Value);
                }
            }

            if (CombinePointsWithSameConcentration)
            {
                allPoints = allPoints.GroupBy(pt => pt.X).Select(group =>
                    new WeightedPoint(group.Key, group.Average(pt => pt.Y), group.First().Weight)).ToList();
            }

            points.AddRange(allPoints);

            if (points.Count == 0)
            {
                return new CalibrationCurve.Error(QuantificationStrings
                    .CalibrationCurveFitter_GetCalibrationCurve_All_of_the_external_standards_are_missing_one_or_more_peaks_);
            }
            return GetCalibrationCurveFromPoints(points);
        }

        public WeightedPoint? GetWeightedPoint(CalibrationPoint calibrationPoint)
        {
            double? intensity = GetYValue(calibrationPoint);
            if (!intensity.HasValue)
            {
                return null;
            }
            double x = GetSpecifiedXValue(calibrationPoint).Value;
            double weight = QuantificationSettings.RegressionWeighting.GetWeighting(x, intensity.Value);
            WeightedPoint weightedPoint = new WeightedPoint(x, intensity.Value, weight);
            return weightedPoint;
        }

        public FiguresOfMerit GetFiguresOfMerit(CalibrationCurve calibrationCurve, List<ImmutableList<PointPair>> bootstrapCurves = null)
        {
            var measuredResults = SrmSettings.MeasuredResults;
            var standardPoints = ImmutableList.ValueOf(GetStandardPoints());
            var blanks = new List<double>();
            foreach (int replicateIndex in EnumerateReplicates())
            {
                var chromatogramSet = measuredResults.Chromatograms[replicateIndex];
                if (Equals(SampleType.BLANK, chromatogramSet.SampleType))
                {
                    var peakArea = GetNormalizedPeakArea(new CalibrationPoint(replicateIndex, null));
                    if (peakArea.HasValue)
                    {
                        blanks.Add(peakArea.Value);
                    }
                }
            }

            return FiguresOfMeritCalculator.GetFiguresOfMerit(calibrationCurve, standardPoints, blanks,
                bootstrapCurves);
        }

        public double? GetTargetIonRatio(TransitionGroupDocNode transitionGroupDocNode)
        {
            var measuredResults = SrmSettings.MeasuredResults;
            double totalQualitativeIonRatio = 0;
            int qualitativeIonRatioCount = 0;
            foreach (var replicateIndex in EnumerateReplicates())
            {
                if (IsExcluded(replicateIndex))
                {
                    continue;
                }
                var chromatogramSet = measuredResults.Chromatograms[replicateIndex];
                if (!SampleType.STANDARD.Equals(chromatogramSet.SampleType))
                {
                    continue;
                }

                var qualitativeIonRatio = PeptideQuantifier.GetQualitativeIonRatio(SrmSettings, transitionGroupDocNode, replicateIndex);
                if (qualitativeIonRatio.HasValue)
                {
                    totalQualitativeIonRatio += qualitativeIonRatio.Value;
                    qualitativeIonRatioCount++;
                }
            }

            if (qualitativeIonRatioCount != 0)
            {
                return totalQualitativeIonRatio / qualitativeIonRatioCount;
            }

            return null;
        }

        private CalibrationCurve GetCalibrationCurveFromPoints(IList<WeightedPoint> points)
        {
            return QuantificationSettings.RegressionFit.Fit(points);
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
                var peptideQuantifier = GetPeptideQuantifier(calibrationPoint);
                var transitionGroup = peptideQuantifier.PeptideDocNode.TransitionGroups.FirstOrDefault(tg =>
                    Equals(tg.LabelType, calibrationPoint.LabelType) && tg.PrecursorConcentration.HasValue);
                if (transitionGroup != null)
                {
                    return transitionGroup.PrecursorConcentration / chromatogramSet.SampleDilutionFactor;
                }
                return null;
            }

            double? peptideConcentration = GetPeptideConcentration(calibrationPoint.ReplicateIndex);
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
            return GetAnnotatedConcentration(calibrationCurve, calibrationPoint)?.Strict;
        }

        public AnnotatedDouble GetAnnotatedConcentration(CalibrationCurve calibrationCurve,
            CalibrationPoint calibrationPoint)
        {
            if (!HasExternalStandards() && !HasInternalStandardConcentration())
            {
                return null;
            }

            var normalizedPeakArea = GetAnnotatedNormalizedPeakArea(calibrationPoint);
            var concentration = GetConcentrationFromXValue(calibrationCurve.GetX(normalizedPeakArea?.Raw) *
                                                           GetDilutionFactor(calibrationPoint.ReplicateIndex));
            return normalizedPeakArea?.ChangeValue(concentration);
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

            if (StandardPeptideQuantifier != null)
            {
                return true;
            }
            return PeptideQuantifier.NormalizationMethod is NormalizationMethod.RatioToLabel;
        }



        public QuantificationResult GetPeptideQuantificationResult(int replicateIndex)
        {
            CalibrationCurve calibrationCurve = GetCalibrationCurve();
            QuantificationResult result = new QuantificationResult();
            result = result.ChangeNormalizedArea(GetAnnotatedNormalizedPeakArea(new CalibrationPoint(replicateIndex, null)));
            
            if (HasExternalStandards() || HasInternalStandardConcentration())
            {
                AnnotatedDouble calculatedConcentration = GetAnnotatedConcentration(calibrationCurve, new CalibrationPoint(replicateIndex, null));
                if (calculatedConcentration != null)
                {
                    result = result.ChangeCalculatedConcentration(calculatedConcentration);
                    double? expectedConcentration = GetPeptideConcentration(replicateIndex) * GetChromatogramSet(replicateIndex)?.SampleDilutionFactor;
                    result = result.ChangeAccuracy(calculatedConcentration.Raw / expectedConcentration);
                    result = result.ChangeUnits(QuantificationSettings.Units);
                }
            }


            return result;
        }

        public PrecursorQuantificationResult GetPrecursorQuantificationResult(int replicateIndex, TransitionGroupDocNode transitionGroupDocNode)
        {
            PrecursorQuantificationResult result = null;
            if (IsotopologResponseCurve)
            {
                result = new PrecursorQuantificationResult();
                var calibrationPoint = new CalibrationPoint(replicateIndex, transitionGroupDocNode.LabelType);
                CalibrationCurve calibrationCurve = GetCalibrationCurve();
                result = (PrecursorQuantificationResult)result.ChangeNormalizedArea(GetAnnotatedNormalizedPeakArea(calibrationPoint));
                if (HasExternalStandards() || HasInternalStandardConcentration())
                {
                    var calculatedConcentration = GetAnnotatedConcentration(calibrationCurve, calibrationPoint);
                    if (calculatedConcentration != null)
                    {
                        result = (PrecursorQuantificationResult)result.ChangeCalculatedConcentration(calculatedConcentration);
                        double? expectedConcentration = transitionGroupDocNode.PrecursorConcentration;
                        result = (PrecursorQuantificationResult)result.ChangeAccuracy(calculatedConcentration.Raw / expectedConcentration);
                        result = (PrecursorQuantificationResult)result.ChangeUnits(QuantificationSettings.Units);
                    }
                }
            }

            var targetIonRatio = GetTargetIonRatio(transitionGroupDocNode);
            var ionRatio = PeptideQuantifier.GetQualitativeIonRatio(SrmSettings, transitionGroupDocNode, replicateIndex);
            if (targetIonRatio.HasValue || ionRatio.HasValue)
            {
                result = result ?? new PrecursorQuantificationResult();
                var status = ValueStatus.GetStatus(ionRatio, GetTargetIonRatio(transitionGroupDocNode),
                    SrmSettings.PeptideSettings.Quantification.QualitativeIonRatioThreshold / 100);
                result = result.ChangeIonRatio(targetIonRatio, ionRatio, status);
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

        public CalibrationCurveFitter MakeCalibrationCurveFitterWithTransitions(IEnumerable<IdentityPath> transitionIdentityPaths)
        {
            return new CalibrationCurveFitter(PeptideQuantifier.WithQuantifiableTransitions(transitionIdentityPaths),null,
                SrmSettings)
            {
                CombinePointsWithSameConcentration = CombinePointsWithSameConcentration
            };
        }

        /// <summary>
        /// Given a flat list of <see cref="GroupComparison.PeptideQuantifier.Quantity"/>  objects,
        /// create a dictionary using the elements from <see cref="_identityPaths"/> as the keys.
        /// </summary>
        private IDictionary<IdentityPath, PeptideQuantifier.Quantity> MakeQuantityDictionary(
            IList<PeptideQuantifier.Quantity> quantities)
        {
            var dictionary = new Dictionary<IdentityPath, PeptideQuantifier.Quantity>();
            for (int i = 0; i < quantities.Count; i++)
            {
                var quantity = quantities[i];
                if (Equals(quantity, default(PeptideQuantifier.Quantity)))
                {
                    continue;
                }
                dictionary.Add(_identityPaths[i], quantity);
            }

            return dictionary;
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
