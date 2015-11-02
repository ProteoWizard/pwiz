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

        public HashSet<IdentityPath> GetCompleteTransitions()
        {
            HashSet<IdentityPath> identities = new HashSet<IdentityPath>();
            foreach (var entry in GetStandardConcentrations())
            {
                identities.UnionWith(GetTransitionQuantities(entry.Key).Keys);
            }
            return identities;
        }

        public IEnumerable<int> GetValidStandardReplicates()
        {
            var completeTransitions = GetCompleteTransitions();
            foreach (var entry in GetStandardConcentrations())
            {
                if (GetTransitionQuantities(entry.Key).Count == completeTransitions.Count)
                {
                    yield return entry.Key;
                }
            }
        }

        public double? GetNormalizedPeakArea(int replicateIndex, ICollection<IdentityPath> transitionFilter)
        {
            var allTransitionQuantities = GetTransitionQuantities(replicateIndex);
            if (transitionFilter == null)
            {
                return PeptideQuantifier.SumQuantities(allTransitionQuantities.Values, PeptideQuantifier.NormalizationMethod);
            }
            else
            {
                return PeptideQuantifier.SumQuantities(allTransitionQuantities.Where(
                    entry => transitionFilter.Contains(entry.Key))
                    .Select(entry => entry.Value), PeptideQuantifier.NormalizationMethod);
            }
        }

        public CalibrationCurve GetCalibrationCurve(int? targetReplicateIndex)
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
            var transitionIds = GetCompleteTransitions();
            if (!transitionIds.Any())
            {
                return new CalibrationCurve().ChangeTransitionIds(transitionIds)
                    .ChangeErrorMessage(QuantificationStrings.CalibrationCurveFitter_GetCalibrationCurve_All_of_the_external_standards_are_missing_one_or_more_peaks_);
            }

            if (targetReplicateIndex.HasValue)
            {
                transitionIds.IntersectWith(GetTransitionQuantities(targetReplicateIndex.Value).Keys);
                if (!transitionIds.Any())
                {
                    return new CalibrationCurve().ChangeErrorMessage(QuantificationStrings.CalibrationCurveFitter_GetCalibrationCurve_The_external_standards_and_the_target_replicate_have_no_peaks_in_common_);
                }
            }
            List<WeightedPoint> weightedPoints = new List<WeightedPoint>();
            foreach (var replicateIndex in GetValidStandardReplicates())
            {
                double? intensity = GetYValue(replicateIndex, transitionIds);
                if (!intensity.HasValue)
                {
                    continue;
                }
                double x = GetSpecifiedXValue(replicateIndex).Value;
                double weight = QuantificationSettings.RegressionWeighting.GetWeighting(x, intensity.Value);
                WeightedPoint weightedPoint = new WeightedPoint(x, intensity.Value, weight);
                weightedPoints.Add(weightedPoint);
            }
            CalibrationCurve calibrationCurve = QuantificationSettings.RegressionFit.Fit(weightedPoints);
            calibrationCurve = calibrationCurve.ChangeTransitionIds(transitionIds);
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
                return ConcentrationRatioText(PeptideQuantifier.MeasuredLabelType, PeptideQuantifier.RatioLabelType);
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
                return PeakAreaRatioText(PeptideQuantifier.MeasuredLabelType, PeptideQuantifier.RatioLabelType);
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
            return calibrationCurve.GetX(GetYValue(replicateIndex, calibrationCurve.TransitionIds));
        }

        public double? GetYValue(int replicateIndex, ICollection<IdentityPath> transitionIds)
        {
            return GetNormalizedPeakArea(replicateIndex, transitionIds);
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

        public static String AppendUnits(String title, String units)
        {
            if (String.IsNullOrEmpty(units))
            {
                return title;
            }
            return TextUtil.SpaceSeparate(title, '(' + units + ')');
        }

        public static String PeakAreaRatioText(IsotopeLabelType numerator, IsotopeLabelType denominator)
        {
            return string.Format(QuantificationStrings.CalibrationCurveFitter_PeakAreaRatioText__0___1__Peak_Area_Ratio, numerator.Title, denominator.Title);
        }

        public static String ConcentrationRatioText(IsotopeLabelType numerator, IsotopeLabelType denominator)
        {
            return String.Format(QuantificationStrings.CalibrationCurveFitter_ConcentrationRatioText__0___1__Concentration_Ratio, numerator.Title, denominator.Title);
        }
    }
}
