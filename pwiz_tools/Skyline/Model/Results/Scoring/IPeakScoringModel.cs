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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public interface IPeakScoringModel : IXmlSerializable
    {
        /// <summary>
        /// Name used in the UI for this Scoring model
        /// </summary>
        string Name { get; }

        /// <summary>
        /// List of feature calculators used by this model in scoring.
        /// </summary>
        IList<IPeakFeatureCalculator> PeakFeatureCalculators { get; }

        /// <summary>
        /// Method called to train the model.  Features scores for positive and negative distributions
        /// are supplied grouped by transition grouping, i.e. scores for all of the peak groups for each
        /// transition grouping, such that the final training should only use one score per group,
        /// as described in the mProphet paper.
        /// </summary>
        /// <param name="targets">Scores for positive targets</param>
        /// <param name="decoys">Scores for null distribution</param>
        /// <param name="initParameters">Initial model parameters</param>
        /// <param name="includeSecondBest"> Include the second best peaks in the targets as decoys?</param>
        IPeakScoringModel Train(IList<IList<double[]>> targets, IList<IList<double[]>> decoys, LinearModelParams initParameters, bool includeSecondBest = false);

        /// <summary>
        /// Scoring function for the model
        /// </summary>
        double Score(IList<double> features);

        /// <summary>
        /// Mean of score values for decoy data.
        /// </summary>
        double DecoyMean { get; }

        /// <summary>
        /// Standard deviation of score values for decoy data.
        /// </summary>
        double DecoyStdev { get; }

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

        public abstract IList<IPeakFeatureCalculator> PeakFeatureCalculators { get; }
        public abstract IPeakScoringModel Train(IList<IList<double[]>> targets, IList<IList<double[]>> decoys, LinearModelParams initParameters, bool includeSecondBest = false);
        public double Score(IList<double> features)
        {
            return Parameters.Score(features);
        }
        public double DecoyMean { get; protected set; }
        public double DecoyStdev { get; protected set; }
        public bool UsesDecoys { get; protected set; }
        public bool UsesSecondBest { get; protected set; }
        public LinearModelParams Parameters { get; protected set; }

        public virtual void Validate()
        {
        }
    }

    public interface IModelParams
    {
        double Score(IList<double> features);
    }

    public class LinearModelParams : Immutable, IModelParams
    {
        private ReadOnlyCollection<double> _weights;

        public LinearModelParams(int numWeights)
        {
            Weights = new List<double>(numWeights);
            Bias = 0;
        }

        public LinearModelParams(IList<double> weights, double bias = 0)
        {
            Weights = weights;
            Bias = bias;
        }

        public IList<double> Weights
        {
            get { return _weights; }
            protected set { _weights = MakeReadOnly(value); }
        }

        public double Bias { get; set; }

        public static double Score(IList<double> features, IList<double> weights, double bias)
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

        public static double Score(IList<double> features, LinearModelParams parameters)
        {
            return parameters.Score(features);
        }

        public double Score(IList<double> features)
        {
            return Score(features, Weights, Bias);
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

    public interface IPeakFeatureCalculator
    {
        float Calculate(PeakScoringContext context, IPeptidePeakData peakGroupData);
        string Name { get; }
        
        /// <summary>
        /// True if low scores are better for this calculator, false if high scores are better
        /// </summary>
        bool IsReversedScore { get; }
    }

    /// <summary>
    /// Abstract class for features that can be calculated from just summary data (areas, retention times, etc.).
    /// </summary>
    public abstract class SummaryPeakFeatureCalculator : IPeakFeatureCalculator
    {
        protected SummaryPeakFeatureCalculator(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }

        public abstract bool IsReversedScore { get; }

        public float Calculate(PeakScoringContext context, IPeptidePeakData peakGroupData)
        {
            return Calculate(context, (IPeptidePeakData<ISummaryPeakData>) peakGroupData);
        }

        protected abstract float Calculate(PeakScoringContext context, IPeptidePeakData<ISummaryPeakData> summaryPeakData);
    }

    /// <summary>
    /// Abstract class for features which require detailed data like chromatograms and spectra to calculate.
    /// </summary>
    public abstract class DetailedPeakFeatureCalculator : IPeakFeatureCalculator
    {
        protected DetailedPeakFeatureCalculator(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }

        public abstract bool IsReversedScore { get; }

        public float Calculate(PeakScoringContext context, IPeptidePeakData peakGroupData)
        {
            return Calculate(context, (IPeptidePeakData<IDetailedPeakData>)peakGroupData);
        }

        protected abstract float Calculate(PeakScoringContext context, IPeptidePeakData<IDetailedPeakData> summaryPeakData);
    }

    public interface IPeptidePeakData
    {
        PeptideDocNode NodePep { get; }

        ChromFileInfo FileInfo { get; }
    }

    public interface IPeptidePeakData<TData> : IPeptidePeakData
    {
        IList<ITransitionGroupPeakData<TData>> TransitionGroupPeakData { get; }
    }

    public interface ITransitionGroupPeakData<TData>
    {
        TransitionGroupDocNode NodeGroup { get; }

        bool IsStandard { get; }

        IList<ITransitionPeakData<TData>> TranstionPeakData { get; }
    }

    public interface ITransitionPeakData<out TData>
    {
        TransitionDocNode NodeTran { get; }

        TData PeakData { get; }
    }

    public interface IDetailedPeakData : ISummaryPeakData
    {
        int TimeIndex { get; }
        int EndIndex { get; }
        int StartIndex { get; }
        int Length { get; }

        /// <summary>
        /// Time array shared by all transitions of a precursor, and on the
        /// same scale as all other precursors of a peptide.
        /// </summary>
        float[] Times { get; }

        /// <summary>
        /// Intensity array linear-interpolated to the shared time scale.
        /// </summary>
        float[] Intensities { get; }
    }

    // ReSharper disable InconsistentNaming
    public enum PeakIdentification { FALSE, TRUE, ALIGNED }
    // ReSharper restore InconsistentNaming

    public interface ISummaryPeakData
    {
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
    }

    public static class PeakScoringModel
    {
        private static readonly IPeakScoringModel[] MODELS =
        {
            new LegacyScoringModel()
        };

        public static IEnumerable<IPeakScoringModel> Models
        {
            get { return MODELS; }
        }
    }

    public static class PeakFeatureCalculator
    {
        private static readonly IPeakFeatureCalculator[] CALCULATORS =  new IPeakFeatureCalculator[]
        {
            // Intensity, retention time, library dotp
            new MQuestIntensityCalc(),
            new MQuestRetentionTimePredictionCalc(), 
            new MQuestIntensityCorrelationCalc(), 

            // Shape-based and related calculators
            new LegacyUnforcedCountScoreCalc(),
            new MQuestWeightedShapeCalc(), 
            new MQuestWeightedCoElutionCalc(), 
            new NextGenSignalNoiseCalc(),
            new NextGenProductMassErrorCalc(),
            new LegacyIdentifiedCountCalc(),

            // Reference standard calculators
            new MQuestReferenceCorrelationCalc(),
            new MQuestWeightedReferenceShapeCalc(), 
            new MQuestWeightedReferenceCoElutionCalc(),
            new LegacyUnforcedCountScoreStandardCalc(),

            // Precursor calculators
            new NextGenCrossWeightedShapeCalc(),
            new NextGenPrecursorMassErrorCalc(),
            new NextGenIsotopeDotProductCalc() 
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

        public PeakScoringContext(SrmDocument document)
        {
            Document = document;
        }

        /// <summary>
        /// The document in which the peaks are being scored
        /// </summary>
        public SrmDocument Document { get; private set; }

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
