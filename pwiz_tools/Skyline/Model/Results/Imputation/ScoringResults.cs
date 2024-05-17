using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.Results.Scoring;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class ScoringResults
    {
        public static readonly Producer<Parameters, ScoringResults> PRODUCER = new ScoringProducer();
        public ScoringResults(MProphetResultsHandler resultsHandler, SrmDocument reintegratedDocument)
        {
            ResultsHandler = resultsHandler;
            ReintegratedDocument = reintegratedDocument;
            SortedScores = ImmutableList.ValueOf(reintegratedDocument.MoleculeTransitionGroups
                .SelectMany(tg => tg.Results.SelectMany(chromInfoList => chromInfoList))
                .Select(transitionGroupChromInfo => transitionGroupChromInfo.ZScore).OfType<float>()
                .OrderBy(score => score));
        }
        public MProphetResultsHandler ResultsHandler { get; }
        public SrmDocument ReintegratedDocument { get; }
        public ImmutableList<float> SortedScores { get; }

        public ScoreQValueMap ScoreQValueMap
        {
            get
            {
                return ReintegratedDocument.Settings.PeptideSettings.Integration.ScoreQValueMap;
            }
        }

        public class Parameters
        {
            public Parameters(SrmDocument document, PeakScoringModelSpec scoringModel, bool overwriteManual)
            {
                Document = document;
                ScoringModel = scoringModel;
                OverwriteManual = overwriteManual;
            }
            public ReferenceValue<SrmDocument> Document { get; }
            public PeakScoringModelSpec ScoringModel { get; }
            public bool OverwriteManual { get; }

            protected bool Equals(Parameters other)
            {
                return Document.Equals(other.Document) && Equals(ScoringModel, other.ScoringModel) && OverwriteManual == other.OverwriteManual;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Parameters)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = Document.GetHashCode();
                    hashCode = (hashCode * 397) ^ (ScoringModel != null ? ScoringModel.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ OverwriteManual.GetHashCode();
                    return hashCode;
                }
            }
        }


        private class ScoringProducer : Producer<Parameters, ScoringResults>
        {
            private static readonly FeatureSetProducer FEATURE_SET_PRODUCER = new FeatureSetProducer();

            public override ScoringResults ProduceResult(ProductionMonitor productionMonitor, Parameters parameter,
                IDictionary<WorkOrder, object> inputs)
            {
                var featureSet = (PeakTransitionGroupFeatureSet)inputs.First().Value;
                var resultsHandler = new MProphetResultsHandler(parameter.Document, parameter.ScoringModel, featureSet);
                resultsHandler.ScoreFeatures();
                SrmDocument reintegratedDocument = null;
                if (!resultsHandler.IsMissingScores())
                {
                    reintegratedDocument =
                        resultsHandler.ChangePeaks(new SilentProgressMonitor(productionMonitor.CancellationToken));
                }

                return new ScoringResults(resultsHandler, reintegratedDocument);
            }

            public override IEnumerable<WorkOrder> GetInputs(Parameters parameter)
            {
                yield return FEATURE_SET_PRODUCER.MakeWorkOrder(new FeatureSetParameters(
                    parameter.Document,
                    parameter.ScoringModel.PeakFeatureCalculators));
            }

            private class FeatureSetProducer : Producer<FeatureSetParameters, PeakTransitionGroupFeatureSet>
            {

                public override PeakTransitionGroupFeatureSet ProduceResult(ProductionMonitor productionMonitor,
                    FeatureSetParameters parameter,
                    IDictionary<WorkOrder, object> inputs)
                {
                    return parameter.Document.Value.GetPeakFeatures(parameter.FeatureCalculators,
                        new SilentProgressMonitor(productionMonitor.CancellationToken));
                }
            }

            private class FeatureSetParameters
            {
                public FeatureSetParameters(SrmDocument document, FeatureCalculators featureCalculators)
                {
                    Document = document;
                    FeatureCalculators = featureCalculators;
                }

                public ReferenceValue<SrmDocument> Document { get; }
                public FeatureCalculators FeatureCalculators { get; }

                protected bool Equals(FeatureSetParameters other)
                {
                    return Document.Equals(other.Document) && Equals(FeatureCalculators, other.FeatureCalculators);
                }

                public override bool Equals(object obj)
                {
                    if (ReferenceEquals(null, obj)) return false;
                    if (ReferenceEquals(this, obj)) return true;
                    if (obj.GetType() != this.GetType()) return false;
                    return Equals((FeatureSetParameters)obj);
                }

                public override int GetHashCode()
                {
                    unchecked
                    {
                        return (Document.GetHashCode() * 397) ^
                               (FeatureCalculators != null ? FeatureCalculators.GetHashCode() : 0);
                    }
                }
            }
        }

        public double? GetPercentileOfScore(double score)
        {
            if (SortedScores.Count == 0)
            {
                return null;
            }

            var index = CollectionUtil.BinarySearch(SortedScores, (float) score);
            if (index >= 0)
            {
                return (double)index / SortedScores.Count;
            }
            index = ~index;

            if (index <= 0)
            {
                return SortedScores[0];
            }

            if (index >= SortedScores.Count - 1)
            {
                return SortedScores[SortedScores.Count - 1];
            }

            double prev = SortedScores[index];
            double next = SortedScores[index + 1];
            return (index + (score - prev) / (next - prev)) / SortedScores.Count;
        }

        public double? GetScoreAtPercentile(double percentile)
        {
            if (SortedScores.Count == 0)
            {
                return null;
            }

            double doubleIndex = percentile * SortedScores.Count;
            if (doubleIndex <= 0)
            {
                return SortedScores[0];
            }

            if (doubleIndex >= SortedScores.Count - 1)
            {
                return SortedScores[SortedScores.Count - 1];
            }

            int prevIndex = (int)Math.Floor(doubleIndex);
            int nextIndex = (int)Math.Ceiling(doubleIndex);
            var prevValue = SortedScores[prevIndex];
            if (prevIndex == nextIndex)
            {
                return prevValue;
            }
            var nextValue = SortedScores[nextIndex];
            return prevValue * (nextIndex - doubleIndex) + nextValue * (doubleIndex - prevIndex);
        }
    }
}
