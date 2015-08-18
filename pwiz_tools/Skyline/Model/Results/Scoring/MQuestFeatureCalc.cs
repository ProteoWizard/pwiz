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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

// Feature calculators as specified in the mQuest/mProphet paper
// http://www.ncbi.nlm.nih.gov/pubmed/21423193

namespace pwiz.Skyline.Model.Results.Scoring
{
    sealed class RetentionTimePrediction
    {
        public RetentionTimePrediction(double? time, double window)
        {
            Time = time;
            Window = window;
        }

        public double? Time { get; private set; }
        public double Window { get; private set; }
    }

    public abstract class AbstractMQuestRetentionTimePredictionCalc : SummaryPeakFeatureCalculator
    {
        protected AbstractMQuestRetentionTimePredictionCalc(string headerName) : base(headerName) {}

        protected override float Calculate(PeakScoringContext context, IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            if (context.Document == null)
                return float.NaN;

            float maxHeight = float.MinValue;
            double? measuredRT = null;
            foreach (var tranPeakData in MQuestHelpers.GetDefaultIonTypes(summaryPeakData.TransitionGroupPeakData))
            {
                if (tranPeakData.PeakData.Height > maxHeight)
                {
                    maxHeight = tranPeakData.PeakData.Height;
                    measuredRT = tranPeakData.PeakData.RetentionTime;
                }
            }
            if (!measuredRT.HasValue)
                return float.NaN;

            RetentionTimePrediction prediction;
            if (!context.TryGetInfo(out prediction))
            {
                double? explicitRT = null;
                double? windowRT = null;
                if (summaryPeakData.NodePep.ExplicitRetentionTime != null)
                {
                    explicitRT = summaryPeakData.NodePep.ExplicitRetentionTime.RetentionTime;
                    windowRT = summaryPeakData.NodePep.ExplicitRetentionTime.RetentionTimeWindow ?? context.Document.Settings.PeptideSettings.Prediction.MeasuredRTWindow;
                }
                if (explicitRT.HasValue && windowRT.HasValue)
                {
                    prediction = new RetentionTimePrediction(explicitRT, windowRT.Value);
                }
                else
                {
                    var fileId = summaryPeakData.FileInfo != null ? summaryPeakData.FileInfo.FileId : null;
                    var settings = context.Document.Settings;
                    var predictor = settings.PeptideSettings.Prediction.RetentionTime;
                    string seqModified = settings.GetSourceTextId(summaryPeakData.NodePep);
                    if (predictor != null)
                    {
                        prediction = new RetentionTimePrediction(predictor.GetRetentionTime(seqModified, fileId),
                            predictor.TimeWindow);
                    }

                    var fullScan = settings.TransitionSettings.FullScan;
                    if (prediction == null && fullScan.IsEnabled && fullScan.RetentionTimeFilterType == RetentionTimeFilterType.ms2_ids)
                    {
                        var filePath = summaryPeakData.FileInfo != null ? summaryPeakData.FileInfo.FilePath : null;
                        var times = settings.GetBestRetentionTimes(summaryPeakData.NodePep, filePath);
                        if (times.Length > 0)
                        {
                            var statTimes = new Statistics(times);
                            double predictedRT = statTimes.Median();
                            double window = statTimes.Range() + fullScan.RetentionTimeFilterLength * 2;
                            prediction = new RetentionTimePrediction(predictedRT, window);
                        }
                    }
                    if (prediction == null)
                        prediction = new RetentionTimePrediction(null, 0);
                }
                context.AddInfo(prediction);
            }
            if (prediction == null || !prediction.Time.HasValue)
                return float.NaN;
            // CONSIDER: Do the division first, and then the cast
            return ((float) RtScoreFunction(measuredRT.Value - prediction.Time.Value) / (float) RtScoreNormalizer(prediction.Window));
        }

        public double RtScoreNormalizer(double timeWindow)
        {
            double normalizedTime = timeWindow > 30 ? 30 : timeWindow < 6 ? 6 : timeWindow;
            return normalizedTime / 10;
        }

        public override bool IsReversedScore { get { return true; } }

        public abstract double RtScoreFunction(double rtValue);
    }

    public class MQuestRetentionTimePredictionCalc : AbstractMQuestRetentionTimePredictionCalc
    {
        public MQuestRetentionTimePredictionCalc() : base("Retention time difference") { }  // Not L10N

        public override string Name
        {
            get { return Resources.MQuestRetentionTimePredictionCalc_MQuestRetentionTimePredictionCalc_Retention_time_difference; }
        }

        public override double RtScoreFunction(double rtValue)
        {
            return Math.Abs(rtValue);
        }
    }

    public class MQuestRetentionTimeSquaredPredictionCalc : AbstractMQuestRetentionTimePredictionCalc
    {
        public MQuestRetentionTimeSquaredPredictionCalc() : base("Retention time squared difference") { }  // Not L10N

        public override string Name
        {
            get { return Resources.MQuestRetentionTimeSquaredPredictionCalc_MQuestRetentionTimeSquaredPredictionCalc_Retention_time_difference_squared; }
        }

        public override double RtScoreFunction(double rtValue)
        {
            return rtValue * rtValue;
        }
    }

    static class MQuestHelpers
    {
        public static IList<ITransitionGroupPeakData<TData>> GetAnalyteGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData)
        {
            return summaryPeakData.AnalyteGroupPeakData;
        }

        public static IList<ITransitionGroupPeakData<TData>> GetStandardGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData)
        {
            return summaryPeakData.StandardGroupPeakData;
        }

        /// <summary>
        /// Gets standard groups if there are any, otherwise analyte groups
        /// </summary>
        /// <typeparam name="TData">Peak scoring data type (summary or detail)</typeparam>
        /// <param name="summaryPeakData">The peptide-level peak data containing the transition groups from which analytes will be selected</param>
        /// <returns></returns>
        public static IList<ITransitionGroupPeakData<TData>> GetBestAvailableGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData)
        {
            return summaryPeakData.BestAvailableGroupPeakData;
        }

        /// <summary>
        /// Get ms2 ions
        /// </summary>
        /// <typeparam name="TData">Peak scoring data type (summary or detail)</typeparam>
        /// <param name="tranGroupPeakDatas">Transition group peak datas to be scored</param>
        /// <returns></returns>
        public static IList<ITransitionPeakData<TData>> GetMs1IonTypes<TData>(IList<ITransitionGroupPeakData<TData>> tranGroupPeakDatas)
        {
            return GetIonTypes(tranGroupPeakDatas, tg => tg.Ms1TranstionPeakData);
        }

        /// <summary>
        /// Get ms2 ions
        /// </summary>
        /// <typeparam name="TData">Peak scoring data type (summary or detail)</typeparam>
        /// <param name="tranGroupPeakDatas">Transition group peak datas to be scored</param>
        /// <returns></returns>
        public static IList<ITransitionPeakData<TData>> GetMs2IonTypes<TData>(IList<ITransitionGroupPeakData<TData>> tranGroupPeakDatas)
        {
            return GetIonTypes(tranGroupPeakDatas, tg => tg.Ms2TranstionPeakData);
        }

        /// <summary>
        /// Get ms2 ions if there are any available, otherwise get the ms1 ions
        /// </summary>
        /// <typeparam name="TData">Peak scoring data type (summary or detail)</typeparam>
        /// <param name="tranGroupPeakDatas">Transition group peak datas to be scored</param>
        /// <returns></returns>
        public static IList<ITransitionPeakData<TData>> GetDefaultIonTypes<TData>(IList<ITransitionGroupPeakData<TData>> tranGroupPeakDatas)
        {
            return GetIonTypes(tranGroupPeakDatas, tg => tg.DefaultTranstionPeakData);
        }

        private static IList<ITransitionPeakData<TData>> GetIonTypes<TData>(IList<ITransitionGroupPeakData<TData>> tranGroupPeakDatas,
            Func<ITransitionGroupPeakData<TData>, IList<ITransitionPeakData<TData>>> getTrans)
        {
            if (tranGroupPeakDatas.Count == 1)
                return getTrans(tranGroupPeakDatas[0]);
            var listTrans = new List<ITransitionPeakData<TData>>();
            foreach (var transitionGroupPeakData in tranGroupPeakDatas)
            {
                listTrans.AddRange(getTrans(transitionGroupPeakData));
            }
            return listTrans;
        }

        public static double GetMaximumProductMassError(PeakScoringContext context)
        {
            var productMz = context.Document.Settings.TransitionSettings.Instrument.MaxMz;
            return context.Document.Settings.TransitionSettings.FullScan.GetProductFilterWindow(productMz) / 2.0;
        }

        public static double GetMaximumPrecursorMassError(PeakScoringContext context)
        {
            var precursorMz = context.Document.Settings.TransitionSettings.Instrument.MaxMz;
            return context.Document.Settings.TransitionSettings.FullScan.GetPrecursorFilterWindow(precursorMz) / 2.0;
        }

        public static float CalculateIdotp(PeakScoringContext context, IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            var tranGroupPeakDatas = GetAnalyteGroups(summaryPeakData);

            if (tranGroupPeakDatas.Count == 0)
                return float.NaN;

            // CONSIDER: With dot-products, when one charge state performs very well, and another
            //           performs poorly, we do not want to penalize the score, since this may
            //           occur for many reasons. Ideally, we would like a score that is improved
            //           by multiple high scoring charge states, but not decreased by the addition
            //           of low scoring charge states. For now we just take the maximum score, since
            //           this is fast and easy to code correctly.
            float maxDotProduct = 0;
            foreach (var pdGroup in tranGroupPeakDatas)
            {
                var pds = pdGroup.TransitionPeakData.Where(pd => pd.NodeTran != null && pd.NodeTran.HasDistInfo).ToList();
                if (!pds.Any())
                    continue;
                var peakAreas = pds.Select(pd => (double)pd.PeakData.Area);
                var isotopeProportions = pds.Select(pd => (double)pd.NodeTran.IsotopeDistInfo.Proportion);
                var statPeakAreas = new Statistics(peakAreas);
                var statIsotopeProportions = new Statistics(isotopeProportions);
                var isotopeDotProduct = (float)statPeakAreas.NormalizedContrastAngleSqrt(statIsotopeProportions);
                if (double.IsNaN(isotopeDotProduct))
                    isotopeDotProduct = 0;
                if (isotopeDotProduct > maxDotProduct)
                    maxDotProduct = isotopeDotProduct;
            }
            if (maxDotProduct == 0)
                return float.NaN;
            return maxDotProduct;
        }
    }

    /// <summary>
    /// Calculates summed areas of light transitions, for specified ions
    /// </summary>
    public abstract class AbstractMQuestIntensityCalc : SummaryPeakFeatureCalculator
    {
        protected AbstractMQuestIntensityCalc(string headerName) : base(headerName) { }

        protected override float Calculate(PeakScoringContext context, IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            double lightArea = GetIonTypes(GetTransitionGroups(summaryPeakData)).Sum(p => p.PeakData.Area);
            return (float)Math.Max(0, Math.Log10(lightArea));
        }

        public override bool IsReversedScore { get { return false; } }

        protected abstract IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> peptidePeakData);

        protected virtual IList<ITransitionPeakData<TData>> GetIonTypes<TData>(IList<ITransitionGroupPeakData<TData>> tranGroupPeakDatas)
        {
            return MQuestHelpers.GetDefaultIonTypes(tranGroupPeakDatas);
        }
    }

    public class MQuestIntensityCalc : AbstractMQuestIntensityCalc
    {
        public MQuestIntensityCalc() : base("Intensity") { }  // Not L10N

        public override string Name
        {
            get { return Resources.MQuestIntensityCalc_MQuestIntensityCalc_Intensity; }
        }

        protected override IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(IPeptidePeakData<TData> peptidePeakData)
        {
            return MQuestHelpers.GetAnalyteGroups(peptidePeakData);
        }
    }

    public class MQuestStandardIntensityCalc : AbstractMQuestIntensityCalc
    {
        public MQuestStandardIntensityCalc() : base("Standard Intensity") { }  // Not L10N

        public override string Name
        {
            get { return Resources.MQuestStandardIntensityCalc_Name_Standard_Intensity; }
        }

        protected override IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> peptidePeakData)
        {
            return MQuestHelpers.GetStandardGroups(peptidePeakData);
        }
    }

    public class MQuestDefaultIntensityCalc : AbstractMQuestIntensityCalc
    {
       public MQuestDefaultIntensityCalc() : base("Default Intensity") { }  // Not L10N

        public override string Name
        {
            get { return Resources.MQuestDefaultIntensityCalc_Name_Default_Intensity; }
        }

        protected override IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> peptidePeakData)
        {
            return MQuestHelpers.GetBestAvailableGroups(peptidePeakData);
        }
    }

    /// <summary>
    /// Calculates Normalized Contrast Angle between experimental intensity
    /// (i.e. peak areas) and spectral library intensity.
    /// </summary>
    public abstract class AbstractMQuestIntensityCorrelationCalc : SummaryPeakFeatureCalculator
    {
        protected AbstractMQuestIntensityCorrelationCalc(string headerName) : base(headerName) { }
        
        protected override float Calculate(PeakScoringContext context, IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            var tranGroupPeakDatas = GetTransitionGroups(summaryPeakData).ToArray();

            // If there are no light transition groups with library intensities,
            // then this score does not apply.
            if (tranGroupPeakDatas.Length == 0 || tranGroupPeakDatas.All(pd => pd.NodeGroup == null || pd.NodeGroup.LibInfo == null))
                return float.NaN;

            // CONSIDER: With dot-products, when one charge state performs very well, and another
            //           performs poorly, we do not want to penalize the score, since this may
            //           occur for many reasons. Ideally, we would like a score that is improved
            //           by multiple high scoring charge states, but not decreased by the addition
            //           of low scoring charge states. For now we just take the maximum score, since
            //           this is fast and easy to code correctly.
            float maxDotProduct = float.MinValue;
            foreach (var pdGroup in tranGroupPeakDatas)
            {
                if (pdGroup.NodeGroup == null || pdGroup.NodeGroup.LibInfo == null)
                    continue;
                var transitionPeakData = GetIonTypes(new[] {pdGroup});
                int count = transitionPeakData.Count;
                var peakAreas = new double[count];
                var libIntensities = new double[count];
                for (int i = 0; i < count; i++)
                {
                    var pd = transitionPeakData[i];
                    if (pd.NodeTran == null)
                        continue;
                    peakAreas[i] = pd.PeakData.Area;
                    libIntensities[i] = pd.NodeTran.LibInfo != null ? pd.NodeTran.LibInfo.Intensity : 0;
                }
                var statPeakAreas = new Statistics(peakAreas);
                var statLibIntensities = new Statistics(libIntensities);
                var dotProduct = (float)statPeakAreas.NormalizedContrastAngleSqrt(statLibIntensities);
                if (double.IsNaN(dotProduct))
                    dotProduct = 0;
                if (dotProduct > maxDotProduct)
                    maxDotProduct = dotProduct;
            }
            if (maxDotProduct < 0)
                return float.NaN;
            return maxDotProduct;
        }

        public override bool IsReversedScore { get { return false; } }

        protected abstract IList<ITransitionPeakData<TData>> GetIonTypes<TData>(IList<ITransitionGroupPeakData<TData>> tranGroupPeakDatas);

        protected abstract IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData);
    }

    public class MQuestIntensityCorrelationCalc : AbstractMQuestIntensityCorrelationCalc
    {
        public MQuestIntensityCorrelationCalc() : base("Library intensity dot-product") { }  // Not L10N

        public override string Name
        {
            get { return Resources.MQuestIntensityCorrelationCalc_Name_Library_intensity_dot_product; }
        }

        protected override IList<ITransitionPeakData<TData>> GetIonTypes<TData>(IList<ITransitionGroupPeakData<TData>> tranGroupPeakDatas)
        {
            return MQuestHelpers.GetMs2IonTypes(tranGroupPeakDatas);
        }

        protected override IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData)
        {
            return MQuestHelpers.GetAnalyteGroups(summaryPeakData);
        }

    }

    public class MQuestStandardIntensityCorrelationCalc : AbstractMQuestIntensityCorrelationCalc
    {
        public MQuestStandardIntensityCorrelationCalc() : base("Standard library dot-product") { }  // Not L10N

        public override string Name
        {
            get { return Resources.MQuestIntensityStandardCorrelationCalc_Name_Standard_library_dot_product; }
        }

        protected override IList<ITransitionPeakData<TData>> GetIonTypes<TData>(IList<ITransitionGroupPeakData<TData>> tranGroupPeakDatas)
        {
            return MQuestHelpers.GetMs2IonTypes(tranGroupPeakDatas);
        }

        protected override IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData)
        {
            return MQuestHelpers.GetStandardGroups(summaryPeakData);
        }

    }

    public class MQuestDefaultIntensityCorrelationCalc : AbstractMQuestIntensityCorrelationCalc
    {
        public MQuestDefaultIntensityCorrelationCalc() : base("Default dotp or idotp") { }  // Not L10N

        public override string Name
        {
            get { return Resources.MQuestDefaultIntensityCorrelationCalc_Name_Default_dotp_or_idotp; }
        }

        protected override float Calculate(PeakScoringContext context,
            IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            var tranGroupPeakDatas = GetTransitionGroups(summaryPeakData);
            return MQuestHelpers.GetMs2IonTypes(tranGroupPeakDatas).Any() ? 
                base.Calculate(context, summaryPeakData) : 
                MQuestHelpers.CalculateIdotp(context, summaryPeakData);
        }

        protected override IList<ITransitionPeakData<TData>> GetIonTypes<TData>(IList<ITransitionGroupPeakData<TData>> tranGroupPeakDatas)
        {
            return MQuestHelpers.GetMs2IonTypes(tranGroupPeakDatas);
        }

        protected override IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData)
        {
            return MQuestHelpers.GetBestAvailableGroups(summaryPeakData);
        }
    }

    /// <summary>
    /// Calculates Normalize Contrast Angle between experimental light intensity
    /// and reference intensity.
    /// </summary>
    public abstract class AbstractMQuestReferenceCorrelationCalc : SummaryPeakFeatureCalculator
    {
        protected AbstractMQuestReferenceCorrelationCalc(string headerName) : base(headerName) { }

        protected override float Calculate(PeakScoringContext context, IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            var analyteGroups = new List<ITransitionGroupPeakData<ISummaryPeakData>>();
            var referenceGroups = new List<ITransitionGroupPeakData<ISummaryPeakData>>();
            foreach (var pdGroup in summaryPeakData.TransitionGroupPeakData)
            {
                if (pdGroup.IsStandard)
                    referenceGroups.Add(pdGroup);
                else
                    analyteGroups.Add(pdGroup);
            }
            if (analyteGroups.Count == 0 || referenceGroups.Count == 0)
                return float.NaN;

            var analyteAreas = new List<double>();
            var referenceAreas = new List<double>();
            foreach (var analyteGroup in analyteGroups)
            {
                foreach (var referenceGroup in referenceGroups)
                {
                    if (analyteGroup.NodeGroup.TransitionGroup.PrecursorCharge !=
                            referenceGroup.NodeGroup.TransitionGroup.PrecursorCharge)
                        continue;

                    foreach (var tranPeakDataPair in TransitionPeakDataPair<ISummaryPeakData>
                        .GetMatchingReferencePairs(analyteGroup, referenceGroup))
                    {
                        var analyteTran = tranPeakDataPair.First;
                        var referenceTran = tranPeakDataPair.Second;
                        if (!IsIonType(analyteTran.NodeTran))
                            continue;
                        analyteAreas.Add(analyteTran.PeakData.Area);
                        referenceAreas.Add(referenceTran.PeakData.Area);
                    }                    
                }
            }
            var statAnalyte = new Statistics(analyteAreas.ToArray());
            var statReference = new Statistics(referenceAreas.ToArray());
            return (float) statAnalyte.NormalizedContrastAngleSqrt(statReference);
        }

        public override bool IsReversedScore { get { return false; } }

        protected abstract bool IsIonType(TransitionDocNode nodeTran);
    }

    public class MQuestReferenceCorrelationCalc : AbstractMQuestReferenceCorrelationCalc
    {
        public MQuestReferenceCorrelationCalc() : base("Reference intensity dot-product") { }  // Not L10N

        public override string Name
        {
            get { return Resources.MQuestReferenceCorrelationCalc_MQuestReferenceCorrelationCalc_mQuest_reference_correlation; }
        }

        protected override bool IsIonType(TransitionDocNode nodeTran)
        {
            return !nodeTran.IsMs1;
        }

    }

    /// <summary>
    /// Calculates a MQuest cross-correlation based score on the analyte transitions.
    /// </summary>
    public abstract class MQuestWeightedLightCalc<TData> : DetailedPeakFeatureCalculator where TData : MQuestAnalyteCrossCorrelations, new()
    {
        protected MQuestWeightedLightCalc(string headerName) : base(headerName) { }

        protected override float Calculate(PeakScoringContext context,
            IPeptidePeakData<IDetailedPeakData> summaryPeakData)
        {
            var tranGroups = GetTransitionGroups(summaryPeakData);
            float? cachedScore = GetCachedScore(context, tranGroups);
            if (cachedScore.HasValue)
                return cachedScore.Value;
            return SetCachedScore(context, tranGroups, Calculate(context, tranGroups));
        }

        private float Calculate(PeakScoringContext context,
            IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroups)
        {
            var lightTransitionPeakData = FilterIons(tranGroups);
            var crossTransitionPeakData = FilterCrossIons(tranGroups);
            if (lightTransitionPeakData.Count == 0 || (crossTransitionPeakData != null && crossTransitionPeakData.Count == 0))
                return float.NaN;

            TData crossCorrMatrix;
            if (!context.TryGetInfo(out crossCorrMatrix))
            {
                crossCorrMatrix = new TData();
                crossCorrMatrix.Initialize(lightTransitionPeakData, crossTransitionPeakData);
                context.AddInfo(crossCorrMatrix);
            }
            if (!crossCorrMatrix.CrossCorrelations.Any())
                return DefaultScore;
            MaxPossibleShift = lightTransitionPeakData.Max(pd => pd.PeakData.Length);

            var statValues = crossCorrMatrix.GetStats(GetValue);
            var statWeights = crossCorrMatrix.GetStats(GetWeight);
            return statValues.Length == 0 ? float.NaN : Calculate(statValues, statWeights);
        }

        protected abstract float? GetCachedScore(PeakScoringContext context,
            IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroups);

        protected abstract float SetCachedScore(PeakScoringContext context,
            IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroups, float score);

        protected abstract float Calculate(Statistics statValues, Statistics statWeigths);
        
        protected abstract double GetValue(MQuestCrossCorrelation xcorr);

        protected virtual double GetWeight(MQuestCrossCorrelation xcorr)
        {
            return xcorr.AreaSum;
        }

        /// <summary>
        /// For assigning the worst possible score when all weights are zero
        /// </summary>
        protected int MaxPossibleShift { get; set; }

        protected virtual float DefaultScore { get { return float.NaN; } }

        protected abstract IList<ITransitionGroupPeakData<TDetails>> GetTransitionGroups<TDetails>(
            IPeptidePeakData<TDetails> summaryPeakData);

        protected const int MAX_CROSS_ION_COUNT = 6;

        protected virtual IList<ITransitionPeakData<IDetailedPeakData>> FilterIons(IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroupPeakDatas)
        {
            return MaxIons(MQuestHelpers.GetDefaultIonTypes(tranGroupPeakDatas), MAX_CROSS_ION_COUNT);
        }

        protected virtual IList<ITransitionPeakData<IDetailedPeakData>> FilterCrossIons(IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroupPeakDatas)
        {
            return null;
        }

        protected IList<ITransitionPeakData<IDetailedPeakData>> MaxIons(
            IList<ITransitionPeakData<IDetailedPeakData>> tranPeakDatas, int maxTrans)
        {
            // Find the peaks with the most area.
            if (tranPeakDatas.Count < maxTrans)
                return tranPeakDatas;

            var peakList = new List<ITransitionPeakData<IDetailedPeakData>>(maxTrans);
            float minArea = float.MaxValue;
            int i;
            for (i = 0; i < tranPeakDatas.Count && peakList.Count < maxTrans; i++)
            {
                if (tranPeakDatas[i].PeakData == null)
                    continue;
                float area = tranPeakDatas[i].PeakData.Area;
                minArea = Math.Min(minArea, area);
                peakList.Add(tranPeakDatas[i]);
            }

            for (; i < tranPeakDatas.Count; i++)
            {
                if (tranPeakDatas[i].PeakData == null)
                    continue;
                float area = tranPeakDatas[i].PeakData.Area;
                if (area > minArea)
                {
                    // Replace the lowest-area peak.
                    for (int j = 0; j < peakList.Count; j++)
                    {
                        if (Equals(peakList[j].PeakData.Area, minArea))
                        {
                            peakList[j] = tranPeakDatas[i];
                            break;
                        }
                    }
                    // Get new minimum and maximum area.
                    minArea = peakList[0].PeakData.Area;
                    for (int j = 1; j < peakList.Count; j++)
                        minArea = Math.Min(minArea, peakList[j].PeakData.Area);
                }
            }
            return peakList;
        }
    }

    internal class ScoreCache
    {
        private float? _analyteScore;
        private float? _standardScore;

        public float? GetScore(IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroups)
        {
            if (tranGroups.Count == 0)
                return null;
            return (tranGroups[0].IsStandard ? _standardScore : _analyteScore);
        }

        public float SetScore(IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroups, float score)
        {
            if (tranGroups.Count != 0)
            {
                if (tranGroups[0].IsStandard)
                    _standardScore = score;
                else
                    _analyteScore = score;
            }
            return score;
        }
    }

    internal class ShapeScoreCache : ScoreCache
    {
    }

    internal class CoElutionScoreCache : ScoreCache
    {        
    }

    /// <summary>
    /// Calculates the MQuest shape score, weighted by the sum of the transition peak areas.
    /// </summary>
    public abstract class AbstractMQuestWeightedShapeCalc<TData> : MQuestWeightedLightCalc<TData> where TData : MQuestAnalyteCrossCorrelations, new()
    {
        protected AbstractMQuestWeightedShapeCalc(string headerName) : base(headerName) { }

        protected override float Calculate(Statistics statValues, Statistics statWeigths)
        {
            double result = statValues.Mean(statWeigths);
            if (double.IsNaN(result))
                return DefaultScore;
            return (float) result;
        }

        public override bool IsReversedScore { get { return false; } }

        protected override double GetValue(MQuestCrossCorrelation xcorr)
        {
            return xcorr.MaxCorr;
        }

        protected override float DefaultScore { get { return 0; } }

        protected override float? GetCachedScore(PeakScoringContext context, IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroups)
        {
            ShapeScoreCache cache;
            if (context.TryGetInfo(out cache))
                return cache.GetScore(tranGroups);
            return null;
        }

        protected override float SetCachedScore(PeakScoringContext context, IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroups, float score)
        {
            ShapeScoreCache cache;
            if (!context.TryGetInfo(out cache))
            {
                cache = new ShapeScoreCache();
                context.AddInfo(cache);
            }
            return cache.SetScore(tranGroups, score);
        }
    }

    public class MQuestWeightedShapeCalc : AbstractMQuestWeightedShapeCalc<MQuestAnalyteCrossCorrelations>
    {
        public MQuestWeightedShapeCalc() : base("Shape (weighted)") { }  // Not L10N
        protected MQuestWeightedShapeCalc(string headerName) : base(headerName) { }

        public override string Name
        {
            get { return Resources.MQuestWeightedShapeCalc_MQuestWeightedShapeCalc_mQuest_weighted_shape; }
        }

        protected override IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData)
        {
            return MQuestHelpers.GetAnalyteGroups(summaryPeakData);
        }
    }

    public class MQuestStandardWeightedShapeCalc : AbstractMQuestWeightedShapeCalc<MQuestStandardCrossCorrelations>
    {
        public MQuestStandardWeightedShapeCalc() : base("Standard shape (weighted)") { }  // Not L10N

        public override string Name
        {
            get { return Resources.MQuestStandardWeightedShapeCalc_Name_Standard_shape__weighted_; }
        }

        protected override IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData)
        {
            return MQuestHelpers.GetStandardGroups(summaryPeakData);
        }
    }

    public class MQuestDefaultWeightedShapeCalc : AbstractMQuestWeightedShapeCalc<MQuestDefaultCrossCorrelations>
    {
        public MQuestDefaultWeightedShapeCalc() : base("Default shape (weighted)") { }  // Not L10N

        public override string Name
        {
            get { return Resources.MQuestDefaultWeightedShapeCalc_Name_Default_shape__weighted_; }
        }

        protected override IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData)
        {
            return MQuestHelpers.GetBestAvailableGroups(summaryPeakData);
        }
    }

    /// <summary>
    /// Calculates the MQuest shape score.
    /// </summary>
    public class MQuestShapeCalc : MQuestWeightedShapeCalc
    {
        public MQuestShapeCalc() : base("Shape")  // Not L10N
        {
        }

        public override string Name
        {
            get { return Resources.MQuestShapeCalc_MQuestShapeCalc_Shape; }
        }

        protected override double GetWeight(MQuestCrossCorrelation xcorr)
        {
            // Use weights of 1.0 for unweighted mean
            return 1.0;
        }

        /// <summary>
        /// No score caching for the unweighted version
        /// </summary>
        protected override float? GetCachedScore(PeakScoringContext context,
            IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroups)
        {
            return null;
        }

        /// <summary>
        /// No score caching for the unweighted version
        /// </summary>
        protected override float SetCachedScore(PeakScoringContext context,
            IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroups, float score)
        {
            return score;
        }
    }

     /// <summary>
    /// Calculates the MQuest co elution score, weighted by the sum of the transition peak areas.
    /// </summary>
    public abstract class AbstractMQuestWeightedCoElutionCalc<TData> : MQuestWeightedLightCalc<TData> where TData : MQuestAnalyteCrossCorrelations, new()
    {
         protected AbstractMQuestWeightedCoElutionCalc(string headerName) : base(headerName) { }

        protected override float Calculate(Statistics statValues, Statistics statWeigths)
        {
            double result = statValues.Mean(statWeigths) + statValues.StdDev(statWeigths);
            if (double.IsNaN(result))
                return DefaultScore;
            return (float) result;
        }

        public override bool IsReversedScore { get { return true; } }

        protected override double GetValue(MQuestCrossCorrelation xcorr)
        {
            return Math.Abs(xcorr.MaxShift);
        }

        protected override float DefaultScore { get { return MaxPossibleShift; } }

        protected override float? GetCachedScore(PeakScoringContext context, IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroups)
        {
            CoElutionScoreCache cache;
            if (context.TryGetInfo(out cache))
                return cache.GetScore(tranGroups);
            return null;
        }

        protected override float SetCachedScore(PeakScoringContext context, IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroups, float score)
        {
            CoElutionScoreCache cache;
            if (!context.TryGetInfo(out cache))
            {
                cache = new CoElutionScoreCache();
                context.AddInfo(cache);
            }
            return cache.SetScore(tranGroups, score);
        }
    }

    public class MQuestWeightedCoElutionCalc : AbstractMQuestWeightedCoElutionCalc<MQuestAnalyteCrossCorrelations>
    {
        public MQuestWeightedCoElutionCalc() : base("Co-elution (weighted)") { }  // Not L10N
        protected MQuestWeightedCoElutionCalc(string headerName) : base(headerName) { }

        public override string Name
        {
            get { return Resources.MQuestWeightedCoElutionCalc_MQuestWeightedCoElutionCalc_mQuest_weighted_coelution; }
        }

        protected override IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData)
        {
            return MQuestHelpers.GetAnalyteGroups(summaryPeakData);
        }
    }

    public class MQuestStandardWeightedCoElutionCalc : AbstractMQuestWeightedCoElutionCalc<MQuestStandardCrossCorrelations>
    {
        public MQuestStandardWeightedCoElutionCalc() : base("Standard co-elution (weighted)") { }  // Not L10N

        public override string Name
        {
            get { return Resources.MQuestStandardWeightedCoElutionCalc_Name_Standard_co_elution__weighted_; }
        }

        protected override IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData)
        {
            return MQuestHelpers.GetStandardGroups(summaryPeakData);
        }
    }

    public class MQuestDefaultWeightedCoElutionCalc : AbstractMQuestWeightedCoElutionCalc<MQuestDefaultCrossCorrelations>
    {
        public MQuestDefaultWeightedCoElutionCalc() : base("Default co-elution (weighted)") { }  // Not L10N

        public override string Name
        {
            get { return Resources.MQuestDefaultWeightedCoElutionCalc_Name_Default_co_elution__weighted_; }
        }

        protected override IList<ITransitionGroupPeakData<TData>> GetTransitionGroups<TData>(
            IPeptidePeakData<TData> summaryPeakData)
        {
            return MQuestHelpers.GetBestAvailableGroups(summaryPeakData);
        }
    }

    /// <summary>
    /// Calculates the MQuest co elution score.
    /// </summary>
    public class MQuestCoElutionCalc : MQuestWeightedCoElutionCalc
    {
        public MQuestCoElutionCalc() : base("Co-elution")  // Not L10N
        {
        }

        public override string Name
        {
            get { return Resources.MQuestCoElutionCalc_MQuestCoElutionCalc_Coelution; }
        }

        protected override double GetWeight(MQuestCrossCorrelation xcorr)
        {
            // Use weights of 1.0 for unweighted mean
            return 1.0;
        }

        /// <summary>
        /// No score caching for the unweighted version
        /// </summary>
        protected override float? GetCachedScore(PeakScoringContext context,
            IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroups)
        {
            return null;
        }

        /// <summary>
        /// No score caching for the unweighted version
        /// </summary>
        protected override float SetCachedScore(PeakScoringContext context,
            IList<ITransitionGroupPeakData<IDetailedPeakData>> tranGroups, float score)
        {
            return score;
        }
    }

    /// <summary>
    /// Calculates the MQuest cross-correlation matrix used by the co elution and shape scores
    /// </summary>
    public class MQuestAnalyteCrossCorrelations
    {
        private readonly IList<MQuestCrossCorrelation> _xcorrMatrix;

        public MQuestAnalyteCrossCorrelations()
        {
            _xcorrMatrix = new List<MQuestCrossCorrelation>();
        }

        public void Initialize(IList<ITransitionPeakData<IDetailedPeakData>> tranPeakDatas,
                               IList<ITransitionPeakData<IDetailedPeakData>> crossPeakDatas)
        {
            foreach (var tranPeakDataPair in GetCrossCorrelationPairsAll(tranPeakDatas, crossPeakDatas))
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
        private IEnumerable<TransitionPeakDataPair<IDetailedPeakData>>
            GetCrossCorrelationPairsAll(IList<ITransitionPeakData<IDetailedPeakData>> tranPeakDatas,
                                        IList<ITransitionPeakData<IDetailedPeakData>> crossPeakDatas)
        {
            bool crossFull = crossPeakDatas != null;
            var correlationList = new List<TransitionPeakDataPair<IDetailedPeakData>>();
            int tranCount = tranPeakDatas.Count;
            if (!crossFull)
                tranCount--;
            for (int i = 0; i < tranCount; i++)
            {
                var tran1 = tranPeakDatas[i];
                int startIndex = crossFull ? 0 : i + 1;
                int crossCount = crossFull ? crossPeakDatas.Count : tranPeakDatas.Count;
                for (int j = startIndex; j < crossCount; j++)
                {
                    var tran2 = crossFull ? crossPeakDatas[j] : tranPeakDatas[j];
                    correlationList.Add(new TransitionPeakDataPair<IDetailedPeakData>(tran1, tran2));
                }
            }
//            Trace.WriteLine(correlationList.Count);
            return correlationList;
        }

        /// <summary>
        /// Get all transitions paired with the transition with the maximum area.
        /// </summary>
// ReSharper disable UnusedMember.Local
        private IEnumerable<TransitionPeakDataPair<IDetailedPeakData>>
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
                   select new TransitionPeakDataPair<IDetailedPeakData>(tranMax, tranPeakData);
        }
    }

    public class MQuestStandardCrossCorrelations : MQuestAnalyteCrossCorrelations
    {
    }

    public class MQuestDefaultCrossCorrelations : MQuestAnalyteCrossCorrelations
    {
    }

    /// <summary>
    /// Calculates a MQuest cross-correlation based score on the correlation between analyte and standard
    /// transitions.
    /// </summary>
    public abstract class MQuestWeightedReferenceCalc : DetailedPeakFeatureCalculator
    {
        protected MQuestWeightedReferenceCalc(string headerName) : base(headerName) { }

        protected override float Calculate(PeakScoringContext context,
                                            IPeptidePeakData<IDetailedPeakData> summaryPeakData)
        {
            MQuestReferenceCrossCorrelations crossCorrMatrix;
            if (!context.TryGetInfo(out crossCorrMatrix))
            {
                crossCorrMatrix = new MQuestReferenceCrossCorrelations(summaryPeakData.TransitionGroupPeakData);
                context.AddInfo(crossCorrMatrix);
            }
            if (!crossCorrMatrix.CrossCorrelations.Any())
                return float.NaN;

            var transitionPeakDatas = summaryPeakData.TransitionGroupPeakData.SelectMany(pd => pd.TransitionPeakData);
            MaxPossibleShift = transitionPeakDatas.Max(pd => pd.PeakData.Length);

            var statValues = crossCorrMatrix.GetStats(GetValue, FilterIons);
            var statWeights = crossCorrMatrix.GetStats(GetWeight, FilterIons);
            return statValues.Length == 0 ? float.NaN : Calculate(statValues, statWeights);
        }

        protected abstract float Calculate(Statistics statValues, Statistics statWeigths);

        protected abstract double GetValue(MQuestCrossCorrelation xcorr);

        protected virtual double GetWeight(MQuestCrossCorrelation xcorr)
        {
            return xcorr.AreaSum;
        }

        protected virtual IEnumerable<MQuestCrossCorrelation> FilterIons(IEnumerable<MQuestCrossCorrelation> crossCorrMatrix)
        {
            return crossCorrMatrix.Where(xcorr => xcorr.TranPeakData1.NodeTran != null && xcorr.TranPeakData2.NodeTran != null)
                                  .Where(xcorr => !xcorr.TranPeakData1.NodeTran.IsMs1 && !xcorr.TranPeakData2.NodeTran.IsMs1);
        }

        /// <summary>
        /// For assigning the worst possible score when all weights are zero
        /// </summary>
        protected int MaxPossibleShift { get; private set; }
    }

    /// <summary>
    /// Calculates the MQuest shape score, weighted by the sum of the transition peak areas.
    /// </summary>
    public class MQuestWeightedReferenceShapeCalc : MQuestWeightedReferenceCalc
    {
        public MQuestWeightedReferenceShapeCalc() : base("Reference shape (weighted)") { }  // Not L10N
        public MQuestWeightedReferenceShapeCalc(string headerName) : base(headerName) {}

        public override string Name
        {
            get { return Resources.MQuestWeightedReferenceShapeCalc_MQuestWeightedReferenceShapeCalc_mProphet_weighted_reference_shape; }
        }

        protected override float Calculate(Statistics statValues, Statistics statWeigths)
        {
            double result = statValues.Mean(statWeigths);
            if (double.IsNaN(result))
                return 0;
            return (float) result;
        }

        public override bool IsReversedScore { get { return false; } }

        protected override double GetValue(MQuestCrossCorrelation xcorr)
        {
            return xcorr.MaxCorr;
        }
    }

    /// <summary>
    /// Calculates the MQuest shape score.
    /// </summary>
    public class MQuestReferenceShapeCalc : MQuestWeightedReferenceShapeCalc
    {
        public MQuestReferenceShapeCalc() : base("Reference shape") { }  // Not L10N

        public override string Name
        {
            get { return Resources.MQuestReferenceShapeCalc_MQuestReferenceShapeCalc_Reference_shape; }
        }

        protected override double GetWeight(MQuestCrossCorrelation xcorr)
        {
            // Use weights of 1.0 for unweighted mean
            return 1.0;
        }
    }

    /// <summary>
    /// Calculates the MQuest co elution score, weighted by the sum of the transition peak areas.
    /// </summary>
    public class MQuestWeightedReferenceCoElutionCalc : MQuestWeightedReferenceCalc
    {
        public MQuestWeightedReferenceCoElutionCalc() : base("Reference co-elution (weighted)") { }  // Not L10N
        public MQuestWeightedReferenceCoElutionCalc(string headerName) : base(headerName) { }

        public override string Name
        {
            get { return Resources.MQuestWeightedReferenceCoElutionCalc_MQuestWeightedReferenceCoElutionCalc_mQuest_weighted_reference_coelution; }
        }

        protected override float Calculate(Statistics statValues, Statistics statWeigths)
        {
            double result = statValues.Mean(statWeigths) + statValues.StdDev(statWeigths);
            if (double.IsNaN(result))
                return MaxPossibleShift;
            return (float) result;
        }

        public override bool IsReversedScore { get { return true; } }

        protected override double GetValue(MQuestCrossCorrelation xcorr)
        {
            return Math.Abs(xcorr.MaxShift);
        }
    }

    /// <summary>
    /// Calculates the MQuest co elution score.
    /// </summary>
    public class MQuestReferenceCoElutionCalc : MQuestWeightedReferenceCoElutionCalc
    {
        public MQuestReferenceCoElutionCalc()
            : base("Reference co-elution")  // Not L10N
        {
        }

        public override string Name
        {
            get { return Resources.MQuestReferenceCoElutionCalc_MQuestReferenceCoElutionCalc_Reference_coelution; }
        }

        protected override double GetWeight(MQuestCrossCorrelation xcorr)
        {
            // Use weights of 1.0 for unweighted mean
            return 1.0;
        }
    }

    /// <summary>
    /// Calculates the MQuest cross-correlation matrix used by the reference co elution and shape scores
    /// </summary>
    class MQuestReferenceCrossCorrelations
    {
        private readonly IList<MQuestCrossCorrelation> _xcorrMatrix;

        public MQuestReferenceCrossCorrelations(IEnumerable<ITransitionGroupPeakData<IDetailedPeakData>> tranGroupPeakDatas)
        {
            _xcorrMatrix = new List<MQuestCrossCorrelation>();
            var analyteGroups = new List<ITransitionGroupPeakData<IDetailedPeakData>>();
            var referenceGroups = new List<ITransitionGroupPeakData<IDetailedPeakData>>();
            foreach (var pdGroup in tranGroupPeakDatas)
            {
                if (pdGroup.IsStandard)
                    referenceGroups.Add(pdGroup);
                else
                    analyteGroups.Add(pdGroup);
            }
            if (analyteGroups.Count == 0 || referenceGroups.Count == 0)
                return;

            foreach (var analyteGroup in analyteGroups)
            {
                foreach (var referenceGroup in referenceGroups)
                {
                    if (analyteGroup.NodeGroup.TransitionGroup.PrecursorCharge !=
                            referenceGroup.NodeGroup.TransitionGroup.PrecursorCharge)
                        continue;

                    foreach (var tranPeakDataPair in TransitionPeakDataPair<IDetailedPeakData>
                        .GetMatchingReferencePairs(analyteGroup, referenceGroup))
                    {
                        var analyteTran = tranPeakDataPair.First;
                        var referenceTran = tranPeakDataPair.Second;
                        _xcorrMatrix.Add(new MQuestCrossCorrelation(analyteTran, referenceTran, true));
                    }
                }
            }
        }

        public IEnumerable<MQuestCrossCorrelation> CrossCorrelations { get { return _xcorrMatrix; } }

        public Statistics GetStats(Func<MQuestCrossCorrelation, double> getValue,
                                   Func<IEnumerable<MQuestCrossCorrelation>, IEnumerable<MQuestCrossCorrelation>> ionFilter = null)
        {
            var selectedCorrelations = ionFilter == null ? CrossCorrelations : ionFilter(CrossCorrelations);
            return new Statistics(selectedCorrelations.Select(getValue));
        }
    }

    /// <summary>
    /// A single cross-correlation vector between the intensities of two different transitions.
    /// </summary>
    public class MQuestCrossCorrelation
    {
        public MQuestCrossCorrelation(ITransitionPeakData<IDetailedPeakData> tranPeakData1,
                                      ITransitionPeakData<IDetailedPeakData> tranPeakData2,
                                      bool normalize)
        {
                TranPeakData1 = tranPeakData1;
                TranPeakData2 = tranPeakData2;

            if (ReferenceEquals(tranPeakData1, tranPeakData2))
                throw new ArgumentException("Cross-correlation attempted on a single transition with itself");  // Not L10N
            int len1 = tranPeakData1.PeakData.Length, len2 = tranPeakData2.PeakData.Length;
            if (len1 == 0 || len2 == 0)
                XcorrDict = new Dictionary<int, double> { {0, 0.0} };
            else if (len1 != len2)
                throw new ArgumentException(string.Format("Cross-correlation attempted on peaks of different lengths {0} and {1}", len1, len2)); // Not L10N
            else
            {
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

        public double AreaSum
        {
            get { return (double)TranPeakData1.PeakData.Area + TranPeakData2.PeakData.Area; }
        }

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

    struct TransitionPeakDataPair<TData>
    {
        public TransitionPeakDataPair(ITransitionPeakData<TData> first,
                                      ITransitionPeakData<TData> second)
            : this()
        {
            First = first;
            Second = second;
        }

        public ITransitionPeakData<TData> First { get; private set; }
        public ITransitionPeakData<TData> Second { get; private set; }

        /// <summary>
        /// Get all combinations of matching analyte and reference transitions
        /// </summary>
        public static IEnumerable<TransitionPeakDataPair<TData>>
            GetMatchingReferencePairs(ITransitionGroupPeakData<TData> lightGroup,
                                     ITransitionGroupPeakData<TData> standardGroup)
        {
            // Enumerate as many elements as match by position
            Assume.IsTrue(lightGroup.NodeGroup.TransitionGroup.PrecursorCharge == standardGroup.NodeGroup.TransitionGroup.PrecursorCharge);
            int i = 0;
            while (i < lightGroup.TransitionPeakData.Count && i < standardGroup.TransitionPeakData.Count)
            {
                var lightTran = lightGroup.TransitionPeakData[i];
                var standardTran = standardGroup.TransitionPeakData[i];
                if (!EquivalentTrans(lightTran.NodeTran, standardTran.NodeTran))
                    break;
                yield return new TransitionPeakDataPair<TData>(lightTran, standardTran);
                i++;
            }
            // Enumerate any remaining light transitions doing exhaustive search or the remaining
            // standard transitions for match
            int startUnmatchedStandard = i;
            while (i < lightGroup.TransitionPeakData.Count)
            {
                var lightTran = lightGroup.TransitionPeakData[i];
                for (int j = startUnmatchedStandard; j < standardGroup.TransitionPeakData.Count; j++)
                {
                    var standardTran = standardGroup.TransitionPeakData[j];
                    if (EquivalentTrans(lightTran.NodeTran, standardTran.NodeTran))
                        yield return new TransitionPeakDataPair<TData>(lightTran, standardTran);
                }
                i++;
            }
        }

        private static bool EquivalentTrans(TransitionDocNode nodeTran1, TransitionDocNode nodeTran2)
        {
            // Note: assumes caller has already verifed precursorCharge match
            if (nodeTran1 == null && nodeTran2 == null)
                return true;
            if (nodeTran1 == null || nodeTran2 == null)
                return false;
            if (!nodeTran1.Transition.Equivalent(nodeTran2.Transition))
                return false;
            return Equals(nodeTran1.Losses, nodeTran2.Losses);
        }
    }
}
