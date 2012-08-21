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

using System.Collections.Generic;
using System.Linq;
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
        protected override double Calculate(PeakScoringContext context, IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            return summaryPeakData.TransitionGroupPeakData
                                  .Where(pd => pd.NodeGroup.TransitionGroup.LabelType.IsLight)
                                  .SelectMany(pd => pd.TranstionPeakData)
                                  .Sum(p => p.Peak.Area);
        }
    }

    /// <summary>
    /// Calculates Normalized Contrast Angle between experimental intensity
    /// (i.e. peak areas) and spectral library intensity.
    /// </summary>
    class MQuestIntensityCorrelationCalc : SummaryPeakFeatureCalculator
    {
        protected override double Calculate(PeakScoringContext context, IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            var tranPeakDatas = summaryPeakData.TransitionGroupPeakData
                .Where(pd => pd.NodeGroup.TransitionGroup.LabelType.IsLight)
                .SelectMany(pd => pd.TranstionPeakData)
                .Where(p => !p.NodeTran.IsMs1)
                .ToArray();
            var statExperiment = new Statistics(tranPeakDatas.Select(p => (double) p.Peak.Area));
            var statLib = new Statistics(tranPeakDatas.Select(p => (double) p.NodeTran.LibInfo.Intensity));
            return statExperiment.NormalizedContrastAngleSqrt(statLib);
        }        
    }

    /// <summary>
    /// Calculates Normalize Contrast Angle between experimental light intensity
    /// and reference intensity.
    /// </summary>
    class MQuestReferenceCorrelationCalc : SummaryPeakFeatureCalculator
    {
        protected override double Calculate(PeakScoringContext context, IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            var lightGroupPeakData = summaryPeakData.TransitionGroupPeakData
                .Where(pd =>pd.NodeGroup.TransitionGroup.LabelType.IsLight).ToArray();
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
            return statAnalyte.NormalizedContrastAngleSqrt(statReference);
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
                return new ReferencePeakAreas(lightPeakData.Peak.Area, referencePeakData.Peak.Area);
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
    /// Calculates the MQuest shape score.
    /// </summary>
    class MQuestShapeCalc : DetailedPeakFeatureCalculator
    {
        protected override double Calculate(PeakScoringContext context, IPeptidePeakData<IDetailedPeakData> summaryPeakData)
        {
            var lightGroup = summaryPeakData.TransitionGroupPeakData.FirstOrDefault(
                pd => pd.NodeGroup.TransitionGroup.LabelType.IsLight);
            if (lightGroup == null)
                return 0;

            MQuestAnalyteCrossCorrelations crossCorrMatrix;
            if (!context.TryGetInfo(out crossCorrMatrix))
            {
                crossCorrMatrix = new MQuestAnalyteCrossCorrelations(lightGroup.TranstionPeakData);
                context.AddInfo(crossCorrMatrix);
            }

            var statMaxCrossCorrelations = new Statistics(crossCorrMatrix.CrossCorrelations.Select(xcorr => xcorr.MaxPeak));
            return statMaxCrossCorrelations.Mean();
        }
    }

    /// <summary>
    /// Calculates the MQuest cross-correlation matrix used by the co elution and shape scores,
    /// modified to remove O(n^2) algorithms:
    /// - Rather than pairwise comparison of all transitions against eachother, transitions are compared
    ///   against the trasition with the maximum peak area.
    /// - Rather than calculating the dot-product of every point against every other point, dot-products
    ///   are calculated only for points above half-max intensity.
    /// </summary>
    class MQuestAnalyteCrossCorrelations
    {
        private readonly IList<MQuestCrossCorrelation> _xcorrMatrix;

        public MQuestAnalyteCrossCorrelations(IList<ITransitionPeakData<IDetailedPeakData>>  tranPeakDatas)
        {
            _xcorrMatrix = new List<MQuestCrossCorrelation>();
            foreach (var tranPeakDataPair in GetCrossCorrelationPairsAll(tranPeakDatas))
            {
                var tranMax = tranPeakDataPair.Key;
                var tranOther = tranPeakDataPair.Value;
                if (tranMax.Peak.Area < tranOther.Peak.Area)
                    Helpers.Swap(ref tranMax, ref tranOther);

                _xcorrMatrix.Add(new MQuestCrossCorrelation(tranMax, tranOther,
                    tranMax.Peak.StartIndex, tranMax.Peak.EndIndex, true));
            }
        }

        public IEnumerable<MQuestCrossCorrelation> CrossCorrelations { get { return _xcorrMatrix; } }

        /// <summary>
        /// Get all unique combinations of transition pairs excluding pairing transitions with themselves
        /// </summary>
        private IEnumerable<KeyValuePair<ITransitionPeakData<IDetailedPeakData>,
            ITransitionPeakData<IDetailedPeakData>>> GetCrossCorrelationPairsAll(IList<ITransitionPeakData<IDetailedPeakData>> tranPeakDatas)
        {
            for (int i = 0; i < tranPeakDatas.Count - 1; i++)
            {
                var tran1 = tranPeakDatas[i];
                for (int j = i; j < tranPeakDatas.Count; j++)
                {
                    var tran2 = tranPeakDatas[j];
                    yield return new KeyValuePair<ITransitionPeakData<IDetailedPeakData>,
                        ITransitionPeakData<IDetailedPeakData>>(tran1, tran2);
                }
            }
        }

        /// <summary>
        /// Get all transitions paired with the transition with the maximum area.
        /// </summary>
        private IEnumerable<KeyValuePair<ITransitionPeakData<IDetailedPeakData>,
            ITransitionPeakData<IDetailedPeakData>>> GetCrossCorrelationPairsMax(IList<ITransitionPeakData<IDetailedPeakData>> tranPeakDatas)
        {
            // Find the peak with the maximum area.
            double maxArea = 0;
            ITransitionPeakData<IDetailedPeakData> tranMax = null;
            foreach (var tranPeakData in tranPeakDatas)
            {
                if (tranPeakData.Peak.Area > maxArea)
                {
                    maxArea = tranPeakData.Peak.Area;
                    tranMax = tranPeakData;
                }
            }
            return from tranPeakData in tranPeakDatas
                   where tranMax != null && !ReferenceEquals(tranMax, tranPeakData)
                   select new KeyValuePair<ITransitionPeakData<IDetailedPeakData>,
                                           ITransitionPeakData<IDetailedPeakData>>(tranMax, tranPeakData);
        }
    }

    class MQuestCrossCorrelation
    {
        public MQuestCrossCorrelation(ITransitionPeakData<IDetailedPeakData> tranPeakData1,
                                      ITransitionPeakData<IDetailedPeakData> tranPeakData2,
                                      int startIndex,
                                      int endIndex,
                                      bool normalize)
        {
            TranPeakData1 = tranPeakData1;
            TranPeakData2 = tranPeakData2;

            int pointCount = endIndex - startIndex + 1; // inclusive of endIndex

            var stat1 = GetStatistics(tranPeakData1.Peak.Intensities, startIndex, pointCount);
            var stat2 = GetStatistics(tranPeakData2.Peak.Intensities, startIndex, pointCount);

            XcorrDict = stat1.CrossCorrelation(stat2, normalize);
        }

        public ITransitionPeakData<IDetailedPeakData> TranPeakData1 { get; private set; }
        public ITransitionPeakData<IDetailedPeakData> TranPeakData2 { get; private set; }
        public IDictionary<int, double> XcorrDict { get; private set; }

        public double MaxPeak { get { return XcorrDict.Max(p => p.Value); } }

        private static Statistics GetStatistics(float[] intensities, int startIndex, int count)
        {
            var result = new double[count];
            for (int i = 0; i < count; i++)
                result[i] = intensities[startIndex + i];
            return new Statistics(result);
        }
    }
}
