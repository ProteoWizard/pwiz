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

        public PeptideQuantifier PeptideQuantifier { get; private set; }
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

        public double? DilutionFactorToConcentration(double? dilutionFactor)
        {
            double stockConcentration = PeptideQuantifier.PeptideDocNode.StockConcentration.GetValueOrDefault(1.0);
            return stockConcentration/dilutionFactor;
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
                    double? concentration = DilutionFactorToConcentration(chromatogramSet.DilutionFactor);
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

        public double? GetReplicateIntensity(int replicateIndex, ICollection<IdentityPath> transitionFilter)
        {
            var allTransitionQuantities = GetTransitionQuantities(replicateIndex);
            if (transitionFilter == null)
            {
                return PeptideQuantifier.SumQuantities(allTransitionQuantities.Values);
            }
            else
            {
                return PeptideQuantifier.SumQuantities(allTransitionQuantities.Where(
                    entry => transitionFilter.Contains(entry.Key))
                    .Select(entry => entry.Value));
            }
        }

        public QuantificationSettings QuantificationSettings
        {
            get { return SrmSettings.PeptideSettings.Quantification; }
        }

        public CalibrationCurve GetCalibrationCurve(int? targetReplicateIndex)
        {
            if (!GetStandardConcentrations().Any())
            {
                return CalibrationCurve.NO_EXTERNAL_STANDARDS;
            }
            var transitionIds = GetCompleteTransitions();
            if (!transitionIds.Any())
            {
                return new CalibrationCurve().ChangeTransitionCount(0)
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
                double? intensity = GetReplicateIntensity(replicateIndex, transitionIds);
                if (!intensity.HasValue)
                {
                    continue;
                }
                double x = GetStandardConcentrations()[replicateIndex];
                double weight = QuantificationSettings.RegressionWeighting.GetWeighting(x, intensity.Value);
                WeightedPoint weightedPoint = new WeightedPoint(x, intensity.Value, weight);
                weightedPoints.Add(weightedPoint);
            }
            CalibrationCurve calibrationCurve = QuantificationSettings.RegressionFit.Fit(weightedPoints);
            calibrationCurve = calibrationCurve.ChangeTransitionCount(transitionIds.Count);
            return calibrationCurve;
        }

        public string GetXAxisTitle()
        {
            var peptideDocNode = PeptideQuantifier.PeptideDocNode;
            if (!peptideDocNode.StockConcentration.HasValue)
            {
                return QuantificationStrings.Calculated_Concentration;
            }
            return AppendUnits(QuantificationStrings.Calculated_Concentration, peptideDocNode.ConcentrationUnits);
        }

        public string GetYAxisTitle()
        {
            PeptideDocNode peptideDocNode = PeptideQuantifier.PeptideDocNode;
            if (null != PeptideQuantifier.NormalizationMethod.IsotopeLabelTypeName)
            {
                IsotopeLabelType isotopeLabelType 
                    = new IsotopeLabelType(PeptideQuantifier.NormalizationMethod.IsotopeLabelTypeName, 0);
                string title = string.Format(QuantificationStrings.CalibrationCurveFitter_GetYAxisTitle_Ratio_to__0_, isotopeLabelType.Title);
                if (!peptideDocNode.InternalStandardConcentration.HasValue)
                {
                    return title;
                }
                title = String.Format(QuantificationStrings.CalibrationCurveFitter_GetYAxisTitle_Concentration_from__0_, title);
                return AppendUnits(title, peptideDocNode.ConcentrationUnits);
            }
            if (ReferenceEquals(NormalizationMethod.NONE, PeptideQuantifier.NormalizationMethod))
            {
                return QuantificationStrings.Intensity;
            }
            return QuantificationStrings.Normalized_Intensity;
        }

        private String AppendUnits(String title, String units)
        {
            if (String.IsNullOrEmpty(units))
            {
                return title;
            }
            return TextUtil.SpaceSeparate(title, '(' + units + ')');
        }
    }
}
