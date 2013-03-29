/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

// Feature calculators as specified in the mQuest/mProphet paper
// http://www.ncbi.nlm.nih.gov/pubmed/21423193

namespace pwiz.Skyline.Model.Results.Scoring
{
    /// <summary>
    /// Calculates summed areas of light transitions
    /// </summary>
    class MQuestLightAreaCalc : SummaryPeakFeatureCalculator
    {
        public MQuestLightAreaCalc() : base(Resources.MQuestLightAreaCalc_MQuestLightAreaCalc_mQuest_light_area) { }

        protected override float Calculate(PeakScoringContext context, IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            return summaryPeakData.TransitionGroupPeakData
                                  .Where(pd => pd.NodeGroup == null || pd.NodeGroup.TransitionGroup.LabelType.IsLight)
                                  .SelectMany(pd => pd.TranstionPeakData)
                                  .Sum(p => p.PeakData.Area);
        }
    }

    /// <summary>
    /// Calculates Normalized Contrast Angle between experimental intensity
    /// (i.e. peak areas) and spectral library intensity.
    /// </summary>
    class MQuestIntensityCorrelationCalc : SummaryPeakFeatureCalculator
    {
        public MQuestIntensityCorrelationCalc() : base(Resources.MQuestIntensityCorrelationCalc_MQuestIntensityCorrelationCalc_mQuest_intensity_correlation) { }

        protected override float Calculate(PeakScoringContext context, IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            var tranPeakDatas = summaryPeakData.TransitionGroupPeakData
                .Where(pd => pd.NodeGroup == null || pd.NodeGroup.TransitionGroup.LabelType.IsLight)
                .SelectMany(pd => pd.TranstionPeakData)
                .Where(p => !p.NodeTran.IsMs1)
                .ToArray();
            var statExperiment = new Statistics(tranPeakDatas.Select(p => (double) p.PeakData.Area));
            var statLib = new Statistics(tranPeakDatas.Select(p => (double) (p.NodeTran.LibInfo != null
                                                                                 ? p.NodeTran.LibInfo.Intensity
                                                                                 : 0)));
            return (float) statExperiment.NormalizedContrastAngleSqrt(statLib);
        }        
    }

    /// <summary>
    /// Calculates Normalize Contrast Angle between experimental light intensity
    /// and reference intensity.
    /// </summary>
    class MQuestReferenceCorrelationCalc : SummaryPeakFeatureCalculator
    {
        public MQuestReferenceCorrelationCalc() : base(Resources.MQuestReferenceCorrelationCalc_MQuestReferenceCorrelationCalc_mQuest_reference_correlation) { }

        protected override float Calculate(PeakScoringContext context, IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            var lightGroupPeakData = summaryPeakData.TransitionGroupPeakData
                .Where(pd => pd.NodeGroup == null || pd.NodeGroup.TransitionGroup.LabelType.IsLight).ToArray();
            var refGroupPeakDataFirst = summaryPeakData.TransitionGroupPeakData
                .FirstOrDefault(pd => pd.IsStandard);
            var refLabelType = refGroupPeakDataFirst != null
                ? refGroupPeakDataFirst.NodeGroup.TransitionGroup.LabelType
                : IsotopeLabelType.heavy;
            var refGroupPeakData = summaryPeakData.TransitionGroupPeakData
                .Where(pd => ReferenceEquals(refLabelType, pd.NodeGroup.TransitionGroup.LabelType)).ToArray();
            if (lightGroupPeakData.Length == 0 || refGroupPeakData.Length == 0)
                return 0;

            var referencePeakAreas = lightGroupPeakData.SelectMany(pd => pd.TranstionPeakData)
                .Join(refGroupPeakData.SelectMany(pd => pd.TranstionPeakData),
                      TransitionKey.Create, TransitionKey.Create, ReferencePeakAreas.Create).ToArray();

            var statAnalyte = new Statistics(referencePeakAreas.Select(p => p.Area));
            var statReference = new Statistics(referencePeakAreas.Select(p => p.ReferenceArea));
            return (float) statAnalyte.NormalizedContrastAngleSqrt(statReference);
        }

        private struct TransitionKey
        {
            public static TransitionKey Create(ITransitionPeakData<ISummaryPeakData> transitionPeakData)
            {
                var tran = transitionPeakData.NodeTran.Transition;
                return new TransitionKey(tran.IonType, tran.Ordinal, tran.Charge, tran.Group.PrecursorCharge);
            }

            private readonly IonType _ionType;
            private readonly int _ionOrdinal;
            private readonly int _charge;
            private readonly int _precursorCharge;

            private TransitionKey(IonType ionType, int ionOrdinal, int charge, int precursorCharge)
            {
                _ionType = ionType;
                _ionOrdinal = ionOrdinal;
                _charge = charge;
                _precursorCharge = precursorCharge;
            }

            #region object overrides

            private bool Equals(TransitionKey other)
            {
                return Equals(other._ionType, _ionType) &&
                    other._ionOrdinal == _ionOrdinal &&
                    other._charge == _charge &&
                    other._precursorCharge == _precursorCharge;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (obj.GetType() != typeof (TransitionKey)) return false;
                return Equals((TransitionKey) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int result = _ionType.GetHashCode();
                    result = (result*397) ^ _ionOrdinal;
                    result = (result*397) ^ _charge;
                    result = (result*397) ^ _precursorCharge;
                    return result;
                }
            }

            #endregion
        }

        private struct ReferencePeakAreas
        {
            public static ReferencePeakAreas Create(ITransitionPeakData<ISummaryPeakData> lightPeakData,
                                                    ITransitionPeakData<ISummaryPeakData> referencePeakData)
            {
                return new ReferencePeakAreas(lightPeakData.PeakData.Area, referencePeakData.PeakData.Area);
            }

            private ReferencePeakAreas(double area, double referenceArea) : this()
            {
                Area = area;
                ReferenceArea = referenceArea;
            }

            public double Area { get; private set; }
            public double ReferenceArea { get; private set; }
        }
    }

    /// <summary>
    /// Calculates a MQuest cross-correlation based score on the analyte transitions.
    /// </summary>
    abstract class MQuestWeightedLightCalc : DetailedPeakFeatureCalculator
    {
        protected MQuestWeightedLightCalc(string name) : base(name) { }

        protected override float Calculate(PeakScoringContext context,
                                            IPeptidePeakData<IDetailedPeakData> summaryPeakData)
        {
            var lightGroup = summaryPeakData.TransitionGroupPeakData.FirstOrDefault(
                pd => pd.NodeGroup == null || pd.NodeGroup.TransitionGroup.LabelType.IsLight);
            if (lightGroup == null)
                return 0;

            MQuestAnalyteCrossCorrelations crossCorrMatrix;
            if (!context.TryGetInfo(out crossCorrMatrix))
            {
                crossCorrMatrix = new MQuestAnalyteCrossCorrelations(lightGroup.TranstionPeakData);
                context.AddInfo(crossCorrMatrix);
            }

            var statValues = crossCorrMatrix.GetStats(GetValue);
            var statWeights = crossCorrMatrix.GetStats(GetWeight);
            return Calculate(statValues, statWeights);
        }

        protected abstract float Calculate(Statistics statValues, Statistics statWeigths);
        
        protected abstract double GetValue(MQuestCrossCorrelation xcorr);

        protected virtual double GetWeight(MQuestCrossCorrelation xcorr)
        {
            return (double)xcorr.TranPeakData1.PeakData.Area + xcorr.TranPeakData2.PeakData.Area;
        }
    }

    /// <summary>
    /// Calculates the MQuest shape score, weighted by the sum of the transition peak areas.
    /// </summary>
    class MQuestWeightedShapeCalc : MQuestWeightedLightCalc
    {
        public MQuestWeightedShapeCalc() : base(Resources.MQuestWeightedShapeCalc_MQuestWeightedShapeCalc_mQuest_weighted_shape) {}

        protected override float Calculate(Statistics statValues, Statistics statWeigths)
        {
            return (float) statValues.Mean(statWeigths);
        }

        protected override double GetValue(MQuestCrossCorrelation xcorr)
        {
            return xcorr.MaxCorr;
        }
    }

    /// <summary>
    /// Calculates the MQuest shape score.
    /// </summary>
    class MQuestShapeCalc : MQuestWeightedShapeCalc
    {
        protected override double GetWeight(MQuestCrossCorrelation xcorr)
        {
            // Use weights of 1.0 for unweighted mean
            return 1.0;
        }
    }

    /// <summary>
    /// Calculates the MQuest co elution score, weighted by the sum of the transition peak areas.
    /// </summary>
    class MQuestWeightedCoElutionCalc : MQuestWeightedLightCalc
    {
        public MQuestWeightedCoElutionCalc() : base(Resources.MQuestWeightedCoElutionCalc_MQuestWeightedCoElutionCalc_mQuest_weighted_coelution) { }

        protected override float Calculate(Statistics statValues, Statistics statWeigths)
        {
            return (float) (statValues.Mean(statWeigths) + statValues.StdDev(statWeigths));
        }

        protected override double GetValue(MQuestCrossCorrelation xcorr)
        {
            return Math.Abs(xcorr.MaxShift);
        }
    }

    /// <summary>
    /// Calculates the MQuest co elution score.
    /// </summary>
    class MQuestCoElutionCalc : MQuestWeightedCoElutionCalc
    {
        protected override double GetWeight(MQuestCrossCorrelation xcorr)
        {
            // Use weights of 1.0 for unweighted mean
            return 1.0;
        }
    }

    /// <summary>
    /// Calculates the MQuest cross-correlation matrix used by the co elution and shape scores
    /// </summary>
    class MQuestAnalyteCrossCorrelations
    {
        private readonly IList<MQuestCrossCorrelation> _xcorrMatrix;

        public MQuestAnalyteCrossCorrelations(IList<ITransitionPeakData<IDetailedPeakData>>  tranPeakDatas)
        {
            _xcorrMatrix = new List<MQuestCrossCorrelation>();
            foreach (var tranPeakDataPair in GetCrossCorrelationPairsAll(tranPeakDatas))
            {
                var tranMax = tranPeakDataPair.First;
                var tranOther = tranPeakDataPair.Second;
                if (tranMax.PeakData.Area < tranOther.PeakData.Area)
                    Helpers.Swap(ref tranMax, ref tranOther);

                _xcorrMatrix.Add(new MQuestCrossCorrelation(tranMax, tranOther, true));
            }
        }

        public IEnumerable<MQuestCrossCorrelation> CrossCorrelations { get { return _xcorrMatrix; } }

        public Statistics GetStats(Func<MQuestCrossCorrelation, double> getValue)
        {
            return new Statistics(CrossCorrelations.Select(getValue));
        }

        /// <summary>
        /// Get all unique combinations of transition pairs excluding pairing transitions with themselves
        /// </summary>
        private IEnumerable<TransitionPeakDataPair>
            GetCrossCorrelationPairsAll(IList<ITransitionPeakData<IDetailedPeakData>> tranPeakDatas)
        {
            for (int i = 0; i < tranPeakDatas.Count - 1; i++)
            {
                var tran1 = tranPeakDatas[i];
                for (int j = i + 1; j < tranPeakDatas.Count; j++)
                {
                    var tran2 = tranPeakDatas[j];
                    yield return new TransitionPeakDataPair(tran1, tran2);
                }
            }
        }

        /// <summary>
        /// Get all transitions paired with the transition with the maximum area.
        /// </summary>
// ReSharper disable UnusedMember.Local
        private IEnumerable<TransitionPeakDataPair>
            GetCrossCorrelationPairsMax(IList<ITransitionPeakData<IDetailedPeakData>> tranPeakDatas)
// ReSharper restore UnusedMember.Local
        {
            // Find the peak with the maximum area.
            double maxArea = 0;
            ITransitionPeakData<IDetailedPeakData> tranMax = null;
            foreach (var tranPeakData in tranPeakDatas)
            {
                if (tranPeakData.PeakData.Area > maxArea)
                {
                    maxArea = tranPeakData.PeakData.Area;
                    tranMax = tranPeakData;
                }
            }
            return from tranPeakData in tranPeakDatas
                   where tranMax != null && !ReferenceEquals(tranMax, tranPeakData)
                   select new TransitionPeakDataPair(tranMax, tranPeakData);
        }
    }

    /// <summary>
    /// Calculates a MQuest cross-correlation based score on the correlation between analyte and standard
    /// transitions.
    /// </summary>
    abstract class MQuestWeightedReferenceCalc : DetailedPeakFeatureCalculator
    {
        protected MQuestWeightedReferenceCalc(string name) : base(name) { }

        protected override float Calculate(PeakScoringContext context,
                                            IPeptidePeakData<IDetailedPeakData> summaryPeakData)
        {
            MQuestReferenceCrossCorrelations crossCorrMatrix;
            if (!context.TryGetInfo(out crossCorrMatrix))
            {
                crossCorrMatrix = new MQuestReferenceCrossCorrelations(summaryPeakData.TransitionGroupPeakData);
                context.AddInfo(crossCorrMatrix);
            }

            var statValues = crossCorrMatrix.GetStats(GetValue);
            var statWeights = crossCorrMatrix.GetStats(GetWeight);
            return Calculate(statValues, statWeights);
        }

        protected abstract float Calculate(Statistics statValues, Statistics statWeigths);

        protected abstract double GetValue(MQuestCrossCorrelation xcorr);

        protected virtual double GetWeight(MQuestCrossCorrelation xcorr)
        {
            return (double)xcorr.TranPeakData1.PeakData.Area + xcorr.TranPeakData2.PeakData.Area;
        }
    }

    /// <summary>
    /// Calculates the MQuest shape score, weighted by the sum of the transition peak areas.
    /// </summary>
    class MQuestWeightedReferenceShapeCalc : MQuestWeightedReferenceCalc
    {
        public MQuestWeightedReferenceShapeCalc() : base(Resources.MQuestWeightedReferenceShapeCalc_MQuestWeightedReferenceShapeCalc_mQuest_weighted_reference) {}
        protected MQuestWeightedReferenceShapeCalc(string name) : base(name) {}

        protected override float Calculate(Statistics statValues, Statistics statWeigths)
        {
            return (float) statValues.Mean(statWeigths);
        }

        protected override double GetValue(MQuestCrossCorrelation xcorr)
        {
            return xcorr.MaxCorr;
        }
    }

    /// <summary>
    /// Calculates the MQuest shape score.
    /// </summary>
    class MQuestReferenceShapeCalc : MQuestWeightedReferenceShapeCalc
    {
        public MQuestReferenceShapeCalc() : base(Resources.MQuestWeightedReferenceShapeCalc_MQuestWeightedReferenceShapeCalc_mQuest_weighted_reference) {}

        protected override double GetWeight(MQuestCrossCorrelation xcorr)
        {
            // Use weights of 1.0 for unweighted mean
            return 1.0;
        }
    }

    /// <summary>
    /// Calculates the MQuest co elution score, weighted by the sum of the transition peak areas.
    /// </summary>
    class MQuestWeightedReferenceCoElutionCalc : MQuestWeightedReferenceCalc
    {
        public MQuestWeightedReferenceCoElutionCalc() : base(Resources.MQuestWeightedReferenceCoElutionCalc_MQuestWeightedReferenceCoElutionCalc_mQuest_weighted_reference_coelution) { }

        protected override float Calculate(Statistics statValues, Statistics statWeigths)
        {
            return (float) (statValues.Mean(statWeigths) + statValues.StdDev(statWeigths));
        }

        protected override double GetValue(MQuestCrossCorrelation xcorr)
        {
            return Math.Abs(xcorr.MaxShift);
        }
    }

    /// <summary>
    /// Calculates the MQuest co elution score.
    /// </summary>
    class MQuestReferenceCoElutionCalc : MQuestWeightedReferenceCoElutionCalc
    {
        protected override double GetWeight(MQuestCrossCorrelation xcorr)
        {
            // Use weights of 1.0 for unweighted mean
            return 1.0;
        }
    }

    /// <summary>
    /// Calculates the MQuest cross-correlation matrix used by the co elution and shape scores
    /// </summary>
    class MQuestReferenceCrossCorrelations
    {
        private readonly IList<MQuestCrossCorrelation> _xcorrMatrix;

        public MQuestReferenceCrossCorrelations(IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroupPeakDatas)
        {
            _xcorrMatrix = new List<MQuestCrossCorrelation>();
            var lightGroup = tranGroupPeakDatas.FirstOrDefault(pd => !pd.IsStandard);
            if (lightGroup == null)
                return;
            foreach (var standardGroup in tranGroupPeakDatas.Where(pd => pd.IsStandard))
            {
                foreach (var tranPeakDataPair in GetCrossCorrelationPairs(lightGroup, standardGroup))
                {
                    var tranLight = tranPeakDataPair.First;
                    var tranStandard = tranPeakDataPair.Second;

                    _xcorrMatrix.Add(new MQuestCrossCorrelation(tranLight, tranStandard, true));
                }
            }
        }

        public IEnumerable<MQuestCrossCorrelation> CrossCorrelations { get { return _xcorrMatrix; } }

        public Statistics GetStats(Func<MQuestCrossCorrelation, double> getValue)
        {
            return new Statistics(CrossCorrelations.Select(getValue));
        }

        /// <summary>
        /// Get all unique combinations of transition pairs excluding pairing transitions with themselves
        /// </summary>
        private IEnumerable<TransitionPeakDataPair>
            GetCrossCorrelationPairs(ITransitionGroupPeakData<IDetailedPeakData> lightGroup,
                                     ITransitionGroupPeakData<IDetailedPeakData> standardGroup)
        {
            // Enumerate as many elements as match by position
            int i = 0;
            while (i < lightGroup.TranstionPeakData.Count && i < standardGroup.TranstionPeakData.Count)
            {
                var lightTran = lightGroup.TranstionPeakData[i];
                var standardTran = standardGroup.TranstionPeakData[i];
                if (!EquivalentTrans(lightTran.NodeTran, standardTran.NodeTran))
                    break;
                yield return new TransitionPeakDataPair(lightTran, standardTran);
                i++;
            }
            // Enumerate any remaining light transitions doing exhaustive search for a standard match
            while (i < lightGroup.TranstionPeakData.Count)
            {
                var lightTran = lightGroup.TranstionPeakData[i];
                var standardTran = standardGroup.TranstionPeakData.FirstOrDefault(
                        p => EquivalentTrans(lightTran.NodeTran, p.NodeTran));
                if (standardTran != null)
                    yield return new TransitionPeakDataPair(lightTran, standardTran);
                i++;
            }
        }

        public bool EquivalentTrans(TransitionDocNode nodeTran1, TransitionDocNode nodeTran2)
        {
            if (nodeTran1 == null && nodeTran2 == null)
                return true;
            if (nodeTran1 == null || nodeTran2 == null)
                return false;
            return nodeTran1.Transition.Equivalent(nodeTran2.Transition);
        }
    }

    /// <summary>
    /// A single cross-correlation vector between the intensities of two different transitions.
    /// </summary>
    class MQuestCrossCorrelation
    {
        public MQuestCrossCorrelation(ITransitionPeakData<IDetailedPeakData> tranPeakData1,
                                      ITransitionPeakData<IDetailedPeakData> tranPeakData2,
                                      bool normalize)
        {
            if (ReferenceEquals(tranPeakData1, tranPeakData2))
                throw new ArgumentException("Cross-correlation attempted on a single transition with itself");  // Not L10N
            int len1 = tranPeakData1.PeakData.Length, len2 = tranPeakData2.PeakData.Length;
            if (len1 == 0 || len2 == 0)
                XcorrDict = new Dictionary<int, double> { {0, 0.0} };
            else if (len1 != len2)
                throw new ArgumentException(string.Format("Cross-correlation attempted on peaks of different lengths {0} and {1}", len1, len2)); // Not L10N
            else
            {
                TranPeakData1 = tranPeakData1;
                TranPeakData2 = tranPeakData2;

                var stat1 = GetStatistics(tranPeakData1.PeakData.Intensities, tranPeakData1.PeakData.StartIndex, len1);
                var stat2 = GetStatistics(tranPeakData2.PeakData.Intensities, tranPeakData2.PeakData.StartIndex, len2);

                XcorrDict = stat1.CrossCorrelation(stat2, normalize);
            }
        }

        public ITransitionPeakData<IDetailedPeakData> TranPeakData1 { get; private set; }
        public ITransitionPeakData<IDetailedPeakData> TranPeakData2 { get; private set; }

        /// <summary>
        /// The full vector of cross-correlation scores between the points of the first transition
        /// and the points of the second transition shifted by some amount.  The keys in the dictionary
        /// are the shift, and the values are the dot-products.
        /// </summary>
        public IDictionary<int, double> XcorrDict { get; private set; }

        /// <summary>
        /// The maximum cross-correlation score between the points of the two transitions
        /// </summary>
        public double MaxCorr { get { return XcorrDict.Max(p => p.Value); } }

        public int MaxShift
        {
            get
            {
                int maxShift = 0;
                double maxCorr = 0;
                foreach (var p in XcorrDict)
                {
                    int shift = p.Key;
                    double corr = p.Value;
                    if (corr > maxCorr || (corr == maxCorr && Math.Abs(shift) < Math.Abs(maxShift)))
                    {
                        maxShift = shift;
                        maxCorr = corr;
                    }
                }
                return maxShift;
            }
        }

        private static Statistics GetStatistics(float[] intensities, int startIndex, int count)
        {
            var result = new double[count];
            for (int i = 0; i < count; i++)
                result[i] = intensities[startIndex + i];
            return new Statistics(result);
        }
    }

    struct TransitionPeakDataPair
    {
        public TransitionPeakDataPair(ITransitionPeakData<IDetailedPeakData> first,
                                      ITransitionPeakData<IDetailedPeakData> second)
            : this()
        {
            First = first;
            Second = second;
        }

        public ITransitionPeakData<IDetailedPeakData> First { get; private set; }
        public ITransitionPeakData<IDetailedPeakData> Second { get; private set; }
    }
}
