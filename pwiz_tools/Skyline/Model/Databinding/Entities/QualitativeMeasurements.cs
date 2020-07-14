using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public class QualitativeMeasurements
    {
        private readonly PeptideResult _peptideResult;
        private readonly CachedValue<ImmutableList<TransitionResult>> _transitionResults;
        public QualitativeMeasurements(PeptideResult peptideResult)
        {
            _peptideResult = peptideResult;
            _transitionResults = CachedValue.Create(peptideResult.DataSchema,
                () => ImmutableList.ValueOf(GetTransitionResults()));
        }

        [Format(Formats.STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double? IonRatio
        {
            get
            {
                double numerator = 0;
                int numeratorCount = 0;
                double denominator = 0;
                int denominatorCount = 0;
                foreach (var transitionResult in _transitionResults.Value)
                {
                    double? area = transitionResult.Area;
                    if (!area.HasValue)
                    {
                        return null;
                    }
                    if (transitionResult.Transition.Quantitative)
                    {
                        denominator += area.Value;
                        denominatorCount++;
                    }
                    else
                    {
                        numerator += area.Value;
                        numeratorCount++;
                    }
                }

                if (numeratorCount == 0 || denominatorCount == 0)
                {
                    return null;
                }

                return numerator / denominator;
            }
        }

        public string IonRatioStatus
        {
            get
            {
                return GetStatus(IonRatio, _peptideResult.Peptide.AcceptanceCriteria.TargetIonRatio,
                    _peptideResult.Peptide.AcceptanceCriteria.IonRatioThreshold);
            }
        }

        private IEnumerable<TransitionResult> GetTransitionResults()
        {
            var resultKey = _peptideResult.ResultFile.ToResultKey();
            foreach (var precursor in _peptideResult.Peptide.Precursors)
            {
                foreach (var transition in precursor.Transitions)
                {
                    TransitionResult transitionResult;
                    if (transition.Results.TryGetValue(resultKey, out transitionResult))
                    {
                        yield return transitionResult;
                    }
                }
            }
        }

        public static string GetStatus(double? observedValue, double? targetValue, double? targetThreshold)
        {
            if (!observedValue.HasValue)
            {
                return null;
            }

            if (double.IsNaN(observedValue.Value) || double.IsNaN(observedValue.Value))
            {
                return @"undefined";
            }

            if (!targetValue.HasValue)
            {
                return @"present";
            }

            if (!targetThreshold.HasValue)
            {
                if (observedValue == targetValue)
                {
                    return @"equal";
                }

                if (observedValue < targetValue)
                {
                    return @"low";
                }

                if (observedValue > targetValue)
                {
                    return @"high";
                }
            }

            if (observedValue >= targetValue - targetValue * targetThreshold &&
                observedValue <= targetValue + targetValue * targetThreshold)
            {
                return @"pass";
            }

            return @"fail";
        }

        public override string ToString()
        {
            var parts = new List<string>();
            string ionRatioStatus = IonRatioStatus;
            if (ionRatioStatus != null)
            {
                parts.Add("Ion Ratio: " + ionRatioStatus);
            }

            return TextUtil.SpaceSeparate(parts);
        }
    }
}
