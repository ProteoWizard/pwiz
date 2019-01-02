﻿/*
 * Original author: Dario Amodei <jegertso .at .u.washington.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Model.Results.Scoring
{
   /// <summary>
    /// Calculates the average mass error for one particular type of ion 
    /// </summary>
    public abstract class MassErrorCalc : SummaryPeakFeatureCalculator
    {
        protected MassErrorCalc(string headerName) : base(headerName) { }

        protected override float Calculate(PeakScoringContext context,
                                           IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            bool noTransitions = true;
            bool allZeroWeights = true;
            // Use running mean calculation to avoid allocations which show up in a profiler
            double weightedMeanMassError = 0;
            double totalWeight = 0;
            double unweightedMeanMassError = 0;
            int totalSeen = 0;
            foreach (var pdTran in GetIonTypes(GetTransitionGroups(summaryPeakData)))
            {
                noTransitions = false;
                var peakData = pdTran.PeakData;
                double? massError = peakData.MassError;
                if (massError.HasValue)
                {
                    double error = MassErrorFunction(massError.Value);
                    double weight = GetWeight(peakData);
                    if (weight != 0)
                    {
                        allZeroWeights = false;
                        totalWeight += weight;
                        weightedMeanMassError += (error - weightedMeanMassError) * weight / totalWeight;
                    }
                    totalSeen++;
                    unweightedMeanMassError += (error - unweightedMeanMassError) / totalSeen;
                }
            }

            // If there are no qualifying transitions, return NaN
            if (noTransitions)
                return float.NaN;
            // If there are qualifying tranistions but they all have null mass error,
            // then return maximum possible mass error
            if (totalSeen == 0)
                return (float) MaximumValue(context);
            if (allZeroWeights)
                return (float) unweightedMeanMassError;
            return (float) weightedMeanMassError;
        }

        protected double GetWeight(ISummaryPeakData peakData)
        {
            return peakData.Area;
        }

        public override bool IsReversedScore { get { return true; } }

        protected abstract double MassErrorFunction(double massError);

        protected abstract IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData);

        protected abstract IList<ITransitionPeakData<TData>> GetIonTypes<TData>(IList<ITransitionGroupPeakData<TData>> tranGroupPeakDatas);

        protected abstract double MaximumValue(PeakScoringContext context);
    }

    /// <summary>
    /// Calculates the average mass error over product ions 
    /// </summary>
    public class NextGenProductMassErrorCalc : MassErrorCalc
    {
        public NextGenProductMassErrorCalc() : base(@"Product mass error") {}

        public override string Name
        {
            get { return Resources.NextGenProductMassErrorCalc_NextGenProductMassErrorCalc_Product_mass_error; }
        }

        protected override double MassErrorFunction(double massError)
        {
            return Math.Abs(massError);
        }

        protected override IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData)
        {
            return MQuestHelpers.GetAnalyteGroups(summaryPeakData);
        }

        protected override IList<ITransitionPeakData<TData>> GetIonTypes<TData>(IList<ITransitionGroupPeakData<TData>> tranGroupPeakDatas)
        {
            return MQuestHelpers.GetMs2IonTypes(tranGroupPeakDatas);
        }

        protected override double MaximumValue(PeakScoringContext context)
        {
            return MQuestHelpers.GetMaximumProductMassError(context);
        }
    }

    /// <summary>
    /// Calculates the average mass error over product ions for the standard isotope label
    /// </summary>
    public class NextGenStandardProductMassErrorCalc : MassErrorCalc
    {
        public NextGenStandardProductMassErrorCalc() : base(@"Standard product mass error") { }

        public override string Name
        {
            get { return Resources.NextGenStandardProductMassErrorCalc_Name_Standard_product_mass_error; }
        }

        protected override double MassErrorFunction(double massError)
        {
            return Math.Abs(massError);
        }

        protected override IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData)
        {
            return MQuestHelpers.GetStandardGroups(summaryPeakData);
        }

        protected override IList<ITransitionPeakData<TData>> GetIonTypes<TData>(IList<ITransitionGroupPeakData<TData>> tranGroupPeakDatas)
        {
            return MQuestHelpers.GetMs2IonTypes(tranGroupPeakDatas);
        }

        protected override double MaximumValue(PeakScoringContext context)
        {
            return MQuestHelpers.GetMaximumProductMassError(context);
        }

        public override bool IsReferenceScore { get { return true; } }
    }

    /// <summary>
    /// Calculates the average mass error over product ions 
    /// </summary>
    public class NextGenProductMassErrorSquaredCalc : MassErrorCalc
    {
        public NextGenProductMassErrorSquaredCalc() : base(@"Product mass error squared") { }

        public override string Name
        {
            get { return Resources.NextGenProductMassErrorCalc_NextGenProductMassErrorCalc_Product_mass_error; }
        }

        protected override double MassErrorFunction(double massError)
        {
            return massError * massError;
        }

        protected override IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData)
        {
            return MQuestHelpers.GetAnalyteGroups(summaryPeakData);
        }

        protected override IList<ITransitionPeakData<TData>> GetIonTypes<TData>(IList<ITransitionGroupPeakData<TData>> tranGroupPeakDatas)
        {
            return MQuestHelpers.GetMs2IonTypes(tranGroupPeakDatas);
        }

        protected override double MaximumValue(PeakScoringContext context)
        {
            return MQuestHelpers.GetMaximumProductMassError(context);
        }
    }

    /// <summary>
    /// Calculates the average mass error over precursor ions 
    /// </summary>
    public class NextGenPrecursorMassErrorCalc : MassErrorCalc
    {
        public NextGenPrecursorMassErrorCalc() : base(@"Precursor mass error") {}

        public override string Name
        {
            get { return Resources.NextGenPrecursorMassErrorCalc_NextGenPrecursorMassErrorCalc_Precursor_mass_error; }
        }

        protected override double MassErrorFunction(double massError)
        {
            return Math.Abs(massError);
        }

        protected override IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData)
        {
            return MQuestHelpers.GetAnalyteGroups(summaryPeakData);
        }

        protected override IList<ITransitionPeakData<TData>> GetIonTypes<TData>(IList<ITransitionGroupPeakData<TData>> tranGroupPeakDatas)
        {
            return MQuestHelpers.GetMs1IonTypes(tranGroupPeakDatas);
        }

        protected override double MaximumValue(PeakScoringContext context)
        {
            return MQuestHelpers.GetMaximumPrecursorMassError(context);
        }

        public override bool IsMs1Score { get { return true; } }
    }

    /// <summary>
    /// Calculates the isotope dot product (idotp) of the MS1 transitions.
    /// If there is more than one transition group in the peptide,
    /// computes the average of the idotp of the transition groups
    /// </summary>
    public class NextGenIsotopeDotProductCalc : SummaryPeakFeatureCalculator
    {
        public NextGenIsotopeDotProductCalc() : base(@"Precursor isotope dot product") { }

        public override string Name
        {
            get { return Resources.NextGenIsotopeDotProductCalc_NextGenIsotopeDotProductCalc_Precursor_isotope_dot_product; }
        }

        protected override float Calculate(PeakScoringContext context, IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            return MQuestHelpers.CalculateIdotp(context, summaryPeakData);
        }

        public override bool IsReversedScore { get { return false; } }

        public override bool IsMs1Score { get { return true; } }
    }


    /// <summary>
    /// Calculates the shape correlation score between MS1 ions and MS2 ions
    /// </summary>
    public class NextGenCrossWeightedShapeCalc : AbstractMQuestWeightedShapeCalc<NextGenCrossCorrelations>
    {
        public NextGenCrossWeightedShapeCalc() : base(@"Precursor-product shape score") { }

        public override string Name
        {
            get { return Resources.NextGenCrossWeightedShapeCalc_NextGenCrossWeightedShapeCalc_Precursor_product_shape_score; }
        }

        public override bool IsMs1Score { get { return true; } }

        protected override IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData)
        {
            return MQuestHelpers.GetBestAvailableGroups(summaryPeakData);
        }

        protected override IList<ITransitionPeakData<IDetailedPeakData>> FilterIons(IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroupPeakDatas)
        {
            return GetMaxAreaIon(MQuestHelpers.GetMs2IonTypes(tranGroupPeakDatas));
        }

        protected override IList<ITransitionPeakData<IDetailedPeakData>> FilterCrossIons(IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroupPeakDatas)
        {
            return GetMaxAreaIon(MQuestHelpers.GetMs1IonTypes(tranGroupPeakDatas));
        }

        private ITransitionPeakData<IDetailedPeakData>[] GetMaxAreaIon(IEnumerable<ITransitionPeakData<IDetailedPeakData>> tranPeakDatas)
        {
            float maxArea = float.MinValue;
            ITransitionPeakData<IDetailedPeakData> maxTran = null;
            foreach (var tran in tranPeakDatas)
            {
                if (tran.PeakData.Area > maxArea)
                {
                    maxTran = tran;
                    maxArea = tran.PeakData.Area;
                }
            }
            return maxTran != null ? new[] {maxTran} : new ITransitionPeakData<IDetailedPeakData>[0];
        }


        /// <summary>
        /// No score caching for this calculator
        /// </summary>
        protected override float? GetCachedScore(PeakScoringContext context,
            IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroups)
        {
            return null;
        }

        /// <summary>
        /// No score caching for this calculator
        /// </summary>
        protected override float SetCachedScore(PeakScoringContext context,
            IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroups, float score)
        {
            return score;
        }
    }

    public class NextGenCrossCorrelations : MQuestAnalyteCrossCorrelations
    {
    }

    /// <summary>
    /// Calculates the signal to noise ratio in chromatographic space at the position of interest 
    /// </summary>
    public abstract class AbstractNextGenSignalNoiseCalc : DetailedPeakFeatureCalculator
    {
        protected AbstractNextGenSignalNoiseCalc(string headerName) : base(headerName) { }

        protected override float Calculate(PeakScoringContext context,
                                           IPeptidePeakData<IDetailedPeakData> summaryPeakData)
        {
            var tranGroupPeakDatas = GetTransitionGroups(summaryPeakData);
            if (tranGroupPeakDatas.Count == 0)
                return float.NaN;
            var snValues = new List<double>();
            var weights = new List<double>();
            foreach (var pdTran in GetIonTypes(tranGroupPeakDatas))
            {
                var peakData = pdTran.PeakData;
                if (peakData == null || peakData.Intensities == null || peakData.Intensities.Count == 0)
                    continue;
                double? snValue = GetSnValue(peakData);
                if (snValue.HasValue)
                {
                    snValues.Add(Math.Abs(snValue.Value));
                    weights.Add(GetWeight(peakData));
                }
            }
            if (snValues.Count == 0)
                return float.NaN;
            var snStats = new Statistics(snValues);
            var weightsStats = new Statistics(weights);
            if (weights.All(weight => weight == 0))
                return 0;
            return (float)snStats.Mean(weightsStats);
        }

        protected double GetWeight(ISummaryPeakData peakData)
        {
            return peakData.Area;
        }

        protected double? GetSnValue(IDetailedPeakData peakData)
        {
            const int halfRange = 500;
            int startNoiseCalc = Math.Max(peakData.StartIndex - halfRange, 0);
            int endNoiseCalc = Math.Min(peakData.EndIndex + halfRange, peakData.Intensities.Count);
            if (peakData.StartIndex > peakData.Intensities.Count || peakData.StartIndex == -1 || peakData.EndIndex == -1)
                return null;
            int intensityCount = (endNoiseCalc - peakData.EndIndex) + (peakData.StartIndex - startNoiseCalc);
            if (intensityCount == 0)
                return 0;
            var intensityList = new List<double>(intensityCount);
            for (int i = startNoiseCalc; i < peakData.StartIndex; ++i)
                intensityList.Add(peakData.Intensities[i]);
            for (int i = peakData.EndIndex; i < endNoiseCalc; ++i)
                intensityList.Add(peakData.Intensities[i]);
            double peakHeight = Math.Max(peakData.Intensities[peakData.TimeIndex], 1);
            // If there is no medianNoise, set it to 1.0
            double medianNoise = Math.Max(GetMedian(intensityList), 1);
            if (peakHeight == 0)
                return 0;
            return Math.Log10(peakHeight / medianNoise);

        }

        protected double GetMedian(List<double> dataList)
        {
            if (dataList.Count == 0)
                return double.NaN;
            // and potentially needing to call QNthItem twice.
            return Statistics.QNthItem(dataList, dataList.Count / 2);
        }

        public override bool IsReversedScore { get { return false; } }

        protected abstract IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData);

        protected virtual IList<ITransitionPeakData<TData>> GetIonTypes<TData>(IList<ITransitionGroupPeakData<TData>> tranGroupPeakDatas)
        {
            return MQuestHelpers.GetDefaultIonTypes(tranGroupPeakDatas);
        }
    }

    public class NextGenSignalNoiseCalc : AbstractNextGenSignalNoiseCalc
    {
        public NextGenSignalNoiseCalc() : base(@"Signal to noise") { }

        public override string Name
        {
            get { return Resources.NextGenSignalNoiseCalc_NextGenSignalNoiseCalc_Signal_to_noise; }
        }

        protected override IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData)
        {
            return MQuestHelpers.GetAnalyteGroups(summaryPeakData);
        }
    }

    public class NextGenStandardSignalNoiseCalc : AbstractNextGenSignalNoiseCalc
    {
        public NextGenStandardSignalNoiseCalc() : base(@"Standard signal to noise") { }

        public override string Name
        {
            get { return Resources.NextGenStandardSignalNoiseCalc_Name_Standard_signal_to_noise; }
        }

        protected override IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData)
        {
            return MQuestHelpers.GetStandardGroups(summaryPeakData);
        }

        public override bool IsReferenceScore { get { return true; } }
    }
}
