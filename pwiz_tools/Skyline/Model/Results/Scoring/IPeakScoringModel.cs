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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public interface IPeakScoringModel : IXmlSerializable, IAuditLogObject
    {
        /// <summary>
        /// Name used in the UI for this Scoring model
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Is the model trained?
        /// </summary>
        bool IsTrained { get; }

        /// <summary>
        /// List of feature calculators used by this model in scoring.
        /// </summary>
        FeatureCalculators PeakFeatureCalculators { get; }

        /// <summary>
        /// Method called to train the model.  Features scores for positive and negative distributions
        /// are supplied grouped by transition grouping, i.e. scores for all of the peak groups for each
        /// transition grouping, such that the final training should only use one score per group,
        /// as described in the mProphet paper.
        /// </summary>
        /// <param name="targets">Scores for positive targets</param>
        /// <param name="decoys">Scores for null distribution</param>
        /// <param name="targetDecoyGenerator">Target decoy generator used to calculate contribution percentages</param>
        /// <param name="initParameters">Initial model parameters</param>
        /// <param name="cutoffs">A list of q value cutoffs used in the training</param>
        /// <param name="iterations">Number of iterations of LDA in model training</param>
        /// <param name="includeSecondBest"> Include the second best peaks in the targets as decoys?</param>
        /// <param name="preTrain">Use a pre-trained model to bootstrap the learning?</param>
        /// <param name="progressMonitor">Progress monitor for displaying progress to the user</param>
        /// <param name="documentPath">Path on disk of the document for writing diagnostic files</param>
        IPeakScoringModel Train(IList<IList<FeatureScores>> targets, IList<IList<FeatureScores>> decoys, TargetDecoyGenerator targetDecoyGenerator, LinearModelParams initParameters,
            IList<double> cutoffs, int? iterations = null, bool includeSecondBest = false, bool preTrain = true, IProgressMonitor progressMonitor = null, string documentPath = null);

        /// <summary>
        /// Scoring function for the model
        /// </summary>
        double Score(IList<float> features);

        /// <summary>
        /// Scoring function for the model
        /// </summary>
        string ScoreText(IList<float> features);

        /// <summary>
        /// Was the model trained with a decoy set?
        /// </summary>
        bool UsesDecoys { get; }

        /// <summary>
        /// Was the model trained with false targets (second best peaks) from the target set
        /// </summary>
        bool UsesSecondBest { get; }

        /// <summary>
        /// Parameter structure for the model, including weights and bias
        /// </summary>
        LinearModelParams Parameters { get;  }

        /// <summary>
        /// True if this type of model ignores unknown (NaN or infinity) scores and replaces them with zero.
        /// </summary>
        bool ReplaceUnknownFeatureScores { get; }
    }


    public class EnabledFeatureScores
    {
        public EnabledFeatureScores(IPeakFeatureCalculator calculator, double weight, double percentageContribution)
        {
            Calculator = calculator;
            Weight = weight;
            PercentageContribution = percentageContribution;
        }

        [Track]
        public IPeakFeatureCalculator Calculator { get; private set; }
        [Track]
        public double Weight { get; private set; }
        [Track(decimalPlaces: 3)]   // Information only - store 3 decimal places, i.e. 0.105 == 10.5%
        public double PercentageContribution { get; private set; }
    }

    public abstract class PeakScoringModelSpec : XmlNamedElement, IPeakScoringModel, IValidating
    {
        protected PeakScoringModelSpec()
        {
            UsesDecoys = true;
            UsesSecondBest = false;
        }

        protected PeakScoringModelSpec(string name) : base(name)
        {
            UsesDecoys = true;
            UsesSecondBest = false;
        }

        [TrackChildren]
        public IList<EnabledFeatureScores> EnabledFeatureScores
        {
            get
            {
                var result = new List<EnabledFeatureScores>();
                if (IsTrained)
                {
                    for (var i = 0; i < Parameters.Weights.Count; ++i)
                    {
                        if (!double.IsNaN(Parameters.Weights[i]))
                            result.Add(new EnabledFeatureScores(PeakFeatureCalculators[i], Parameters.Weights[i],
                                Parameters.PercentContributions[i]));
                    }
                }
                return result;
            }
        }

        public bool IsTrained { get { return Parameters != null && Parameters.Weights != null; } }

        public abstract bool ReplaceUnknownFeatureScores { get; }
        public abstract FeatureCalculators PeakFeatureCalculators { get; }
        public abstract IPeakScoringModel Train(IList<IList<FeatureScores>> targets, IList<IList<FeatureScores>> decoys, TargetDecoyGenerator targetDecoyGenerator, LinearModelParams initParameters,
            IList<double> cutoffs, int? iterations = null, bool includeSecondBest = false, bool preTrain = true, IProgressMonitor progressMonitor = null, string documentPath = null);
        public double Score(IList<float> features)
        {
            if (ReplaceUnknownFeatureScores)
            {
                features = LinearModelParams.ReplaceUnknownFeatureScores(features);
            }
            return Parameters.Score(features);
        }
        public string ScoreText(IList<float> features)
        {
            if (ReplaceUnknownFeatureScores)
            {
                features = LinearModelParams.ReplaceUnknownFeatureScores(features);
            }
            return Parameters.ScoreText(features);
        }
        [Track]
        public bool UsesDecoys { get; protected set; }
        [Track]
        public bool UsesSecondBest { get; protected set; }
        public LinearModelParams Parameters { get; protected set; }

        public static bool AreSameCalculators(IList<IPeakFeatureCalculator> peakCalculator1, IList<IPeakFeatureCalculator> peakCalculator2)
        {
            if (peakCalculator1 == null ||
                peakCalculator2 == null ||
                peakCalculator1.Count != peakCalculator2.Count)
                return false;
            for (int i = 0; i < peakCalculator1.Count; ++i)
            {
                if (peakCalculator1[i].GetType() != peakCalculator2[i].GetType())
                    return false;
            }
            return true;
        }

        protected bool Equals(PeakScoringModelSpec other)
        {
            return base.Equals(other) && 
                UsesDecoys.Equals(other.UsesDecoys) && 
                UsesSecondBest.Equals(other.UsesSecondBest) && 
                PeakFeatureCalculators.Equals(other.PeakFeatureCalculators) &&
                Equals(Parameters, other.Parameters);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((PeakScoringModelSpec) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode*397) ^ UsesDecoys.GetHashCode();
                hashCode = (hashCode*397) ^ UsesSecondBest.GetHashCode();
                hashCode = (hashCode*397) ^ (Parameters != null ? Parameters.GetHashCode() : 0);
                return hashCode;
            }
        }

        public virtual void Validate()
        {
        }
    }

    public interface IModelParams
    {
        // ReSharper disable UnusedMemberInSuper.Global
        double Score(IList<float> features);
        // ReSharper restore UnusedMemberInSuper.Global
    }

    public class LinearModelParams : Immutable, IModelParams
    {
        private ImmutableList<double> _weights;
        private ImmutableList<double> _percentContributions;

        public LinearModelParams(int count)
        {
            Weights = new double[count];
            PercentContributions = new double[count];
            Bias = 0;
        }

        public LinearModelParams(IList<double> weights, double bias = 0)
        {
            Weights = weights;
            PercentContributions = new double[weights.Count];
            Bias = bias;
        }

        public IList<double> Weights
        {
            get { return _weights; }
            protected set { _weights = MakeReadOnly(value); }
        }

        public IList<double> PercentContributions
        {
            get { return _percentContributions; }
            protected set { _percentContributions = MakeReadOnly(value); }
        }

        public double Bias { get; set; }

        public static double Score(IList<float> features, IList<double> weights, double bias)
        {
            if (features.Count != weights.Count)
            {
                throw new InvalidDataException(string.Format(Resources.LinearModelParams_Score_Attempted_to_score_a_peak_with__0__features_using_a_model_with__1__trained_scores_,
                                               features.Count, weights.Count));
            }
            double score = bias;
            for (int i = 0; i < features.Count; ++i)
            {
                if (!double.IsNaN(weights[i]))
                    score += weights[i] * features[i];
            }
            return score;
        }
        public double Score(IList<float> features)
        {
            return Score(features, Weights, Bias);
        }
        public static IList<float> ReplaceUnknownFeatureScores(IList<float> features)
        {
            if (features.Any(IsUnknownFeatureScore))
            {
                return features.Select(feature => IsUnknownFeatureScore(feature) ? 0 : feature).ToList();
            }
            return features;
        }

        private static bool IsUnknownFeatureScore(float feature)
        {
            return float.IsNaN(feature) || float.IsInfinity(feature);
        }

        public static string ScoreText(IList<float> features, IList<double> weights, double bias)
        {
            if (features.Count != weights.Count)
            {
                throw new InvalidDataException(string.Format(Resources.LinearModelParams_Score_Attempted_to_score_a_peak_with__0__features_using_a_model_with__1__trained_scores_,
                                               features.Count, weights.Count));
            }
            var scoreText = new StringBuilder();
            if (bias > 0)
                scoreText.Append(bias.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < features.Count; ++i)
            {
                if (!double.IsNaN(weights[i]))
                {
                    if (scoreText.Length > 0)
                        scoreText.Append(@" + ");
                    scoreText.Append(string.Format(@"{0}*{1}", weights[i], features[i]));
                }
            }
            scoreText.Append(string.Format(@" = {0}", Score(features, weights, bias)));
            return scoreText.ToString();
        }

        public LinearModelParams CalculatePercentContributions(IPeakScoringModel model, TargetDecoyGenerator targetDecoyGenerator)
        {
            var percentContributions = new double[_percentContributions.Count];
            for (var i = 0; i < _percentContributions.Count; ++i)
                percentContributions[i] = (targetDecoyGenerator != null ? targetDecoyGenerator.GetPercentContribution(model, i) : null) ?? double.NaN;

            return ChangeProp(ImClone(this), im =>
            {
                im.PercentContributions = percentContributions;
            });
        }

        public string ScoreText(IList<float> features)
        {
            return ScoreText(features, Weights, Bias);
        }

        /// <summary>
        /// Return a parameter set rescaled so that the null distribution has mean zero and standard deviation 1.
        /// </summary>
        /// <param name="mean"> The current null distribution mean.</param>
        /// <param name="standardDev"> The current null distribution standard deviation.</param>
        /// <returns></returns>
        public LinearModelParams RescaleParameters(double mean, double standardDev)
        {
            if (double.IsNaN(mean) || double.IsNaN(standardDev) || standardDev == 0)
            {
                throw new InvalidDataException(string.Format(Resources.LinearModelParams_RescaleParameters_Every_calculator_in_the_model_either_has_an_unknown_value__or_takes_on_only_one_value_));
            }
            return ChangeProp(ImClone(this), im =>
                {
                    var weights = Weights.Select(t => t/standardDev).ToList();
                    double bias = (Bias - mean) / standardDev;
                    im.Weights = weights;
                    im.Bias = bias;
                });

        }

        #region object overrides

        protected bool Equals(LinearModelParams other)
        {
            if (Weights.Count != other.Weights.Count)
                return false;
            for (int i = 0; i < Weights.Count; ++i)
            {
                if (Weights[i] != other.Weights[i])
                    return false;
            }
            return Bias == other.Bias;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((LinearModelParams) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_weights.GetHashCode()*397) ^ Bias.GetHashCode();
            }
        }

        #endregion
    }

    public interface IPeakFeatureCalculator : IAuditLogObject
    {
        float Calculate(PeakScoringContext context, IPeptidePeakData peakGroupData);
        string Name { get; }

        /// <summary>
        /// A calculator name that does not get localized
        /// </summary>
        string HeaderName { get; }
        /// <summary>
        /// The full name of the calculator class
        /// </summary>
        string FullyQualifiedName { get; }

        string Tooltip { get; }
        
        /// <summary>
        /// True if low scores are better for this calculator, false if high scores are better
        /// </summary>
        bool IsReversedScore { get; }

        /// <summary>
        /// True if this score applies only to documents with labeled reference standards
        /// </summary>
        bool IsReferenceScore { get; }

        /// <summary>
        /// True if this score applies only to documents with MS1 filtering enabled
        /// </summary>
        bool IsMs1Score { get; }
    }

    /// <summary>
    /// Abstract class for features that can be calculated from just summary data (areas, retention times, etc.).
    /// </summary>
    public abstract class SummaryPeakFeatureCalculator : IPeakFeatureCalculator
    {
        protected SummaryPeakFeatureCalculator(string headerName)
        {
            HeaderName = headerName;
        }

        public abstract string Name { get; }

        public string HeaderName { get; private set; }
        public string FullyQualifiedName
        {
            get { return GetType().FullName; }
        }

        public string Tooltip
        {
            get { return FeatureTooltips.ResourceManager.GetString(FullyQualifiedName); }
        }

        public abstract bool IsReversedScore { get; }

        public float Calculate(PeakScoringContext context, IPeptidePeakData peakGroupData)
        {
            return Calculate(context, (IPeptidePeakData<ISummaryPeakData>) peakGroupData);
        }

        public virtual bool IsReferenceScore { get { return false; } }
        
        public virtual bool IsMs1Score { get { return false; } }

        protected abstract float Calculate(PeakScoringContext context, IPeptidePeakData<ISummaryPeakData> summaryPeakData);

        public string AuditLogText { get { return Name; } }
        public bool IsName { get { return true; }}
    }

    /// <summary>
    /// Abstract class for features which require detailed data like chromatograms and spectra to calculate.
    /// </summary>
    public abstract class DetailedPeakFeatureCalculator : IPeakFeatureCalculator
    {
        protected DetailedPeakFeatureCalculator(string headerName)
        {
            HeaderName = headerName;
        }

        public abstract string Name { get; }

        public string HeaderName { get; private set; }

        public string FullyQualifiedName
        {
            get { return GetType().FullName; }
        }

        public string Tooltip
        {
            get { return FeatureTooltips.ResourceManager.GetString(FullyQualifiedName); }
        }

        public abstract bool IsReversedScore { get; }

        public float Calculate(PeakScoringContext context, IPeptidePeakData peakGroupData)
        {
            return Calculate(context, (IPeptidePeakData<IDetailedPeakData>)peakGroupData);
        }

        public virtual bool IsReferenceScore { get { return false; } }

        public virtual bool IsMs1Score { get { return false; } }

        protected abstract float Calculate(PeakScoringContext context, IPeptidePeakData<IDetailedPeakData> summaryPeakData);

        public string AuditLogText { get { return Name; } }
        public bool IsName { get { return true; } }
    }

    /// <summary>
    /// Wrapper class to convert between IPeptidePeakData templated types
    /// </summary>
    public class PeptidePeakDataConverter<TData> : IPeptidePeakData<ISummaryPeakData> where TData : ISummaryPeakData
    {
        public PeptidePeakDataConverter(IPeptidePeakData<TData> peptidePeakData)
        {
            _peptidePeakData = peptidePeakData;
            var groupPeakData = _peptidePeakData.TransitionGroupPeakData;
            TransitionGroupPeakData = groupPeakData == null ? new List<ITransitionGroupPeakData<ISummaryPeakData>>() 
                                        : groupPeakData.Select(gp => 
                                            new TransitionGroupPeakDataConverter<TData>(gp) as ITransitionGroupPeakData<ISummaryPeakData>).ToList();
            AnalyteGroupPeakData = TransitionGroupPeakData.Where(t => !t.IsStandard).ToArray();
            StandardGroupPeakData = TransitionGroupPeakData.Where(t => t.IsStandard).ToArray();
        }

        private readonly IPeptidePeakData<TData> _peptidePeakData;

        public PeptideDocNode NodePep { get { return _peptidePeakData.NodePep; } }

        public ChromFileInfo FileInfo { get { return _peptidePeakData.FileInfo; } }

        public IList<ITransitionGroupPeakData<ISummaryPeakData>> TransitionGroupPeakData { get; private set; }

        public IList<ITransitionGroupPeakData<ISummaryPeakData>> AnalyteGroupPeakData { get; private set; }

        public IList<ITransitionGroupPeakData<ISummaryPeakData>> StandardGroupPeakData { get; private set; }

        public IList<ITransitionGroupPeakData<ISummaryPeakData>> BestAvailableGroupPeakData
        {
            get { return StandardGroupPeakData.Count > 0 ? StandardGroupPeakData : AnalyteGroupPeakData; }
        }

    }

    /// <summary>
    /// Wrapper class to convert between ITransitionGroupPeakData templated types
    /// </summary>
    public class TransitionGroupPeakDataConverter<TData> : ITransitionGroupPeakData<ISummaryPeakData> where TData : ISummaryPeakData
    {
        public TransitionGroupPeakDataConverter(ITransitionGroupPeakData<TData> groupPeakData)
        {
            _groupPeakData = groupPeakData;
            TransitionPeakData = ConvertTransitionPeakDatas(_groupPeakData.TransitionPeakData);
            Ms1TranstionPeakData = ConvertTransitionPeakDatas(_groupPeakData.Ms1TranstionPeakData);
            Ms2TranstionPeakData = ConvertTransitionPeakDatas(_groupPeakData.Ms2TranstionPeakData);
            Ms2TranstionDotpData = ConvertTransitionPeakDatas(_groupPeakData.Ms2TranstionDotpData);
        }

        private readonly ITransitionGroupPeakData<TData> _groupPeakData;

        public TransitionGroupDocNode NodeGroup { get { return _groupPeakData.NodeGroup; } }

        public bool IsStandard { get { return _groupPeakData.IsStandard; } }

        public IList<ITransitionPeakData<ISummaryPeakData>> TransitionPeakData { get; private set; }

        public IList<ITransitionPeakData<ISummaryPeakData>> Ms1TranstionPeakData { get; private set; }

        public IList<ITransitionPeakData<ISummaryPeakData>> Ms2TranstionPeakData { get; private set; }

        public IList<ITransitionPeakData<ISummaryPeakData>> Ms2TranstionDotpData { get; private set; }

        public IList<ITransitionPeakData<ISummaryPeakData>> DefaultTranstionPeakData
        {
            get { return Ms2TranstionPeakData.Count > 0 ? Ms2TranstionPeakData : Ms1TranstionPeakData; }
        }

        private IList<ITransitionPeakData<ISummaryPeakData>> ConvertTransitionPeakDatas(
            IEnumerable<ITransitionPeakData<TData>> peakDatas)
        {
            if (peakDatas == null)
            {
                return Array.Empty<ITransitionPeakData<ISummaryPeakData>>();
            }

            return peakDatas.Select(peakData => new TransitionPeakDataConverter<TData>(peakData))
                .Cast<ITransitionPeakData<ISummaryPeakData>>().ToList();
        }
    }

    /// <summary>
    /// Wrapper class to convert between ITransitionPeakData templated types
    /// </summary>
    public class TransitionPeakDataConverter<TData> : ITransitionPeakData<ISummaryPeakData> where TData : ISummaryPeakData
    {
        public TransitionPeakDataConverter(ITransitionPeakData<TData> transitionPeakData)
        {
            _transitionPeakData = transitionPeakData;
        }

        private readonly ITransitionPeakData<TData> _transitionPeakData;

        public TransitionDocNode NodeTran { get { return _transitionPeakData.NodeTran; } }

        public ISummaryPeakData PeakData { get { return _transitionPeakData.PeakData; } }
    }

    public interface IPeptidePeakData
    {
        PeptideDocNode NodePep { get; }

        ChromFileInfo FileInfo { get; }
    }

    public interface IPeptidePeakData<TData> : IPeptidePeakData
    {
        IList<ITransitionGroupPeakData<TData>> TransitionGroupPeakData { get; }

        IList<ITransitionGroupPeakData<TData>> AnalyteGroupPeakData { get; }

        IList<ITransitionGroupPeakData<TData>> StandardGroupPeakData { get; }

        IList<ITransitionGroupPeakData<TData>> BestAvailableGroupPeakData { get; }
    }

    public interface ITransitionGroupPeakData<TData>
    {
        TransitionGroupDocNode NodeGroup { get; }

        bool IsStandard { get; }

        IList<ITransitionPeakData<TData>> TransitionPeakData { get; }

        IList<ITransitionPeakData<TData>> Ms1TranstionPeakData { get; }

        IList<ITransitionPeakData<TData>> Ms2TranstionPeakData { get; }

        IList<ITransitionPeakData<TData>> Ms2TranstionDotpData { get; }

        IList<ITransitionPeakData<TData>> DefaultTranstionPeakData { get; }
    }

    public interface ITransitionPeakData<out TData>
    {
        TransitionDocNode NodeTran { get; }

        TData PeakData { get; }
    }

    public interface IDetailedPeakData : ISummaryPeakData
    {
        // ReSharper disable UnusedMemberInSuper.Global
        int TimeIndex { get; }
        int EndIndex { get; }
        int StartIndex { get; }
        int Length { get; }

        /// <summary>
        /// Time array shared by all transitions of a precursor, and on the
        /// same scale as all other precursors of a peptide.
        /// </summary>
        IList<float> Times { get; }

        /// <summary>
        /// Intensity array linear-interpolated to the shared time scale.
        /// </summary>
        IList<float> Intensities { get; }
        // ReSharper restore UnusedMemberInSuper.Global
    }

    // ReSharper disable InconsistentNaming
    public enum PeakIdentification : byte { FALSE, TRUE, ALIGNED }
    // ReSharper restore InconsistentNaming

    public static class PeakIdentificationFastLookup
    {
        public static readonly Dictionary<string, PeakIdentification> Dict = XmlUtil.GetEnumLookupDictionary(
            PeakIdentification.FALSE, PeakIdentification.TRUE, PeakIdentification.ALIGNED);
    }

    public interface ISummaryPeakData
    {
        // ReSharper disable UnusedMemberInSuper.Global
        float RetentionTime { get; }
        float StartTime { get; }
        float EndTime { get; }
        float Area { get; }
        float BackgroundArea { get; }
        float Height { get; }
        float Fwhm { get; }
        float? MassError { get; }
        bool IsEmpty { get; }
        bool IsFwhmDegenerate { get; }
        bool IsForcedIntegration { get; }
        PeakIdentification Identified { get; }
        bool? IsTruncated { get; }
        // ReSharper restore UnusedMemberInSuper.Global
    }

    public static class PeakFeatureCalculator
    {
        private static readonly IPeakFeatureCalculator[] CALCULATORS =
        {
            // Intensity, retention time, library dotp
            new MQuestIntensityCalc(),
            new MQuestRetentionTimePredictionCalc(), 
            new MQuestRetentionTimeSquaredPredictionCalc(),
            new MQuestIntensityCorrelationCalc(), 

            // Shape-based and related calculators
            new MQuestWeightedShapeCalc(), 
            new MQuestWeightedCoElutionCalc(), 
            new LegacyUnforcedCountScoreCalc(),
            new NextGenSignalNoiseCalc(),
            new NextGenProductMassErrorCalc(),

            // Reference standard calculators
            new MQuestReferenceCorrelationCalc(),
            new MQuestWeightedReferenceShapeCalc(), 
            new MQuestWeightedReferenceCoElutionCalc(),
            new LegacyUnforcedCountScoreStandardCalc(),

            // Reference standard self-calculators
            new MQuestStandardIntensityCalc(), 
            new MQuestStandardIntensityCorrelationCalc(), 
            new NextGenStandardSignalNoiseCalc(),
            new NextGenStandardProductMassErrorCalc(),
            new MQuestStandardWeightedShapeCalc(),
            new MQuestStandardWeightedCoElutionCalc(), 

            // Precursor calculators
            new NextGenCrossWeightedShapeCalc(),
            new NextGenPrecursorMassErrorCalc(),
            new NextGenIsotopeDotProductCalc(),
            new LegacyIdentifiedCountCalc(),

            // Default calculators for scoring on import
            new MQuestDefaultIntensityCalc(), 
            new LegacyUnforcedCountScoreDefaultCalc(), 
            new MQuestDefaultIntensityCorrelationCalc(), 
            new MQuestDefaultWeightedCoElutionCalc(), 
            new MQuestDefaultWeightedShapeCalc(), 
        };

        public static IEnumerable<IPeakFeatureCalculator> Calculators
        {
            get { return CALCULATORS; }
        }

        public static IPeakFeatureCalculator GetCalculator(Type calcType)
        {
            var calculator = Activator.CreateInstance(calcType) as IPeakFeatureCalculator;
            if (calculator == null)
                throw new InvalidDataException();
            return calculator;
        }
    }

    /// <summary>
    /// Allows <see cref="IPeakFeatureCalculator"/> objects to share information.
    /// </summary>
    public class PeakScoringContext
    {
        private readonly Dictionary<Type, object> _dictInfo = new Dictionary<Type, object>();

        public PeakScoringContext(SrmSettings settings)
        {
            Settings = settings;
        }

        /// <summary>
        /// The document in which the peaks are being scored
        /// </summary>
        public SrmSettings Settings { get; private set; }

        /// <summary>
        /// Stores information that can be used by other <see cref="IPeakFeatureCalculator"/> objects.
        /// </summary>
        /// <param name="info">An object with extra information to be stored by type</param>
        public void AddInfo<TInfo>(TInfo info)
        {
            _dictInfo.Add(typeof(TInfo), info);
        }

        /// <summary>
        /// Get an object potentially stored by another <see cref="IPeakFeatureCalculator"/> during
        /// its scoring of a peak group.
        /// </summary>
        /// <typeparam name="TInfo">The type of the object to get</typeparam>
        /// <param name="info">If successful the stored object is stored in this parameter</param>
        /// <returns>True if an object was found for the given type</returns>
        public bool TryGetInfo<TInfo>(out TInfo info)
        {
            object infoObj;
            if (!_dictInfo.TryGetValue(typeof(TInfo), out infoObj))
            {
                info = default(TInfo);
                return false;
            }
            info = (TInfo) infoObj;
            return true;
        }
    }
}
