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
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public class CalibrationCurveFitter
    {
        private readonly IDictionary<int, IDictionary<IdentityPath, PeptideQuantifier.Quantity>> _replicateQuantities
            = new Dictionary<int, IDictionary<IdentityPath, PeptideQuantifier.Quantity>>();

        private HashSet<IdentityPath> _transitionsToQuantifyOn;

        public CalibrationCurveFitter(PeptideQuantifier peptideQuantifier, SrmSettings srmSettings)
        {
            PeptideQuantifier = peptideQuantifier;
            SrmSettings = srmSettings;
        }

        public static CalibrationCurveFitter GetCalibrationCurveFitter(SrmSettings srmSettings,
            PeptideGroupDocNode peptideGroup, PeptideDocNode peptide)
        {
            return new CalibrationCurveFitter(PeptideQuantifier.GetPeptideQuantifier(srmSettings, peptideGroup, peptide), srmSettings);
        }

        public PeptideQuantifier PeptideQuantifier { get; private set; }

        public QuantificationSettings QuantificationSettings
        {
            get { return PeptideQuantifier.QuantificationSettings; }
        }
        public SrmSettings SrmSettings { get; private set; }

        public IDictionary<IdentityPath, PeptideQuantifier.Quantity> GetTransitionQuantities(int replicateIndex)
        {
            IDictionary<IdentityPath, PeptideQuantifier.Quantity> result;
            if (!_replicateQuantities.TryGetValue(replicateIndex, out result))
            {
                result = PeptideQuantifier.GetTransitionIntensities(SrmSettings, replicateIndex);
                _replicateQuantities.Add(replicateIndex, result);
            }
            return result;
        }

        public double? GetPeptideConcentration(ChromatogramSet chromatogramSet)
        {
            double concentrationMultiplier = PeptideQuantifier.PeptideDocNode.ConcentrationMultiplier.GetValueOrDefault(1.0);
            return chromatogramSet.AnalyteConcentration*concentrationMultiplier;
        }

        public IDictionary<int, double> GetStandardConcentrations()
        {
            Dictionary<int, double> result = new Dictionary<int, double>();
            var measuredResults = SrmSettings.MeasuredResults;
            if (null != measuredResults)
            {
                for (int replicateIndex = 0; replicateIndex < measuredResults.Chromatograms.Count; replicateIndex++)
                {
                    var chromatogramSet = measuredResults.Chromatograms[replicateIndex];
                    if (!SampleType.STANDARD.Equals(chromatogramSet.SampleType))
                    {
                        continue;
                    }
                    double? concentration = GetPeptideConcentration(chromatogramSet);
                    if (concentration.HasValue)
                    {
                        result.Add(replicateIndex, concentration.Value);
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
                            identities.UnionWith(GetTransitionQuantities(replicateIndex).Keys);
                        }
                    }
                }
                _transitionsToQuantifyOn = identities;
            }
            return _transitionsToQuantifyOn;
        }

        public IEnumerable<int> GetValidStandardReplicates()
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

        public double? GetNormalizedPeakArea(int replicateIndex)
        {
            var allTransitionQuantities = GetTransitionQuantities(replicateIndex);
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
            CalibrationCurve calibrationCurve = QuantificationSettings.RegressionFit.Fit(weightedPoints);
            return calibrationCurve;
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
            if (null != PeptideQuantifier.NormalizationMethod.IsotopeLabelTypeName)
            {
                return PeakAreaRatioText(PeptideQuantifier.MeasuredLabelTypes, PeptideQuantifier.RatioLabelType);
            }
            return QuantificationStrings.CalibrationCurveFitter_GetYAxisTitle_Normalized_Peak_Area;
        }

        public double? GetSpecifiedXValue(int replicateIndex)
        {
            double? peptideConcentration = GetPeptideConcentration(SrmSettings.MeasuredResults.Chromatograms[replicateIndex]);
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

        public double? GetCalculatedXValue(CalibrationCurve calibrationCurve, int replicateIndex)
        {
            return calibrationCurve.GetX(GetYValue(replicateIndex));
        }

        public double? GetYValue(int replicateIndex)
        {
            return GetNormalizedPeakArea(replicateIndex);
        }

        public double? GetCalculatedConcentration(CalibrationCurve calibrationCurve, int replicateIndex)
        {
            if (!HasExternalStandards() && !HasInternalStandardConcentration())
            {
                return null;
            }
            double? xValue = GetCalculatedXValue(calibrationCurve, replicateIndex);
            if (HasExternalStandards() && HasInternalStandardConcentration())
            {
                return xValue*PeptideQuantifier.PeptideDocNode.InternalStandardConcentration;
            }
            return xValue;
        }

        public bool HasExternalStandards()
        {
            return RegressionFit.NONE != QuantificationSettings.RegressionFit;
        }

        public bool HasInternalStandardConcentration()
        {
            return null != PeptideQuantifier.NormalizationMethod.IsotopeLabelTypeName
                   && PeptideQuantifier.PeptideDocNode.InternalStandardConcentration.HasValue;
        }

        public bool IsAllowMissingTransitions()
        {
            return null != PeptideQuantifier.NormalizationMethod.IsotopeLabelTypeName;
        }

        public QuantificationResult GetQuantificationResult(int replicateIndex)
        {
            QuantificationResult result = new QuantificationResult();

            CalibrationCurve calibrationCurve = GetCalibrationCurve();
            result = result.ChangeNormalizedArea(GetNormalizedPeakArea(replicateIndex));
            if (HasExternalStandards() || HasInternalStandardConcentration())
            {
                double? calculatedConcentration = GetCalculatedConcentration(calibrationCurve, replicateIndex);
                result = result.ChangeCalculatedConcentration(calculatedConcentration);
                double? expectedConcentration = GetPeptideConcentration(SrmSettings.MeasuredResults.Chromatograms[replicateIndex]);
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
            if (numerator.Count == 1)
            {
                return string.Format(QuantificationStrings.CalibrationCurveFitter_PeakAreaRatioText__0___1__Peak_Area_Ratio, numerator.First().Title, denominator.Title);
            }
            return string.Format(QuantificationStrings.CalibrationCurveFitter_PeakAreaRatioText_Peak_Area_Ratio_to__0_, denominator.Title);
        }

        public static string ConcentrationRatioText(IsotopeLabelType numerator, IsotopeLabelType denominator)
        {
            return ConcentrationRatioText(new[] {numerator}, denominator);
        }

        public static string ConcentrationRatioText(ICollection<IsotopeLabelType> numerator, IsotopeLabelType denominator)
        {
            if (numerator.Count == 1)
            {
                return string.Format(QuantificationStrings.CalibrationCurveFitter_ConcentrationRatioText__0___1__Concentration_Ratio, numerator.First().Title, denominator.Title);
            }
            return string.Format(QuantificationStrings.CalibrationCurveFitter_ConcentrationRatioText_Concentration_Ratio_to__0_, denominator.Title);
        }
    }
}
