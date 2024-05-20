using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
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
            SortedScores = ImmutableList.ValueOf(reintegratedDocument?.MoleculeTransitionGroups
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
                return ReintegratedDocument?.Settings.PeptideSettings.Integration.ScoreQValueMap ?? ScoreQValueMap.EMPTY;
            }
        }

        public class Parameters
        {
            public Parameters(SrmDocument document, PeakScoringModelSpec scoringModel, bool overwriteManual, ImmutableList<IdentityPath> peptideIdentityPaths)
            {
                Document = document;
                ScoringModel = scoringModel;
                OverwriteManual = overwriteManual;
                PeptideIdentityPaths = peptideIdentityPaths;
            }
            public SrmDocument Document { get; }
            public PeakScoringModelSpec ScoringModel { get; }
            public bool OverwriteManual { get; }
            public ImmutableList<IdentityPath> PeptideIdentityPaths { get; }

            protected bool Equals(Parameters other)
            {
                return ReferenceEquals(Document, other.Document) && Equals(ScoringModel, other.ScoringModel) &&
                       OverwriteManual == other.OverwriteManual &&
                       Equals(PeptideIdentityPaths, other.PeptideIdentityPaths);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Parameters)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = Document == null ? 0 : RuntimeHelpers.GetHashCode(Document);
                    hashCode = (hashCode * 397) ^ (ScoringModel != null ? ScoringModel.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ OverwriteManual.GetHashCode();
                    hashCode = (hashCode * 397) ^
                               (PeptideIdentityPaths != null ? PeptideIdentityPaths.GetHashCode() : 0);
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
                var featureSet = (PeakTransitionGroupFeatureSet)inputs.FirstOrDefault().Value;
                MProphetResultsHandler resultsHandler = null;
                SrmDocument reintegratedDocument = null;
                if (featureSet != null)
                {
                    resultsHandler = new MProphetResultsHandler(
                        RemoveExceptPeptides(parameter.Document, parameter.PeptideIdentityPaths?.ToHashSet()),
                        parameter.ScoringModel, featureSet);
                    resultsHandler.ScoreFeatures(new ProductionMonitor(productionMonitor.CancellationToken,
                        v => productionMonitor.SetProgress(v / 2)).AsProgressMonitor());
                    if (!resultsHandler.IsMissingScores())
                    {
                        reintegratedDocument =
                            resultsHandler.ChangePeaks(new ProductionMonitor(productionMonitor.CancellationToken,
                                v => productionMonitor.SetProgress(50 + v / 2)).AsProgressMonitor());
                    }
                }

                return new ScoringResults(resultsHandler, reintegratedDocument ?? parameter.Document);
            }

            public override IEnumerable<WorkOrder> GetInputs(Parameters parameter)
            {
                yield return FEATURE_SET_PRODUCER.MakeWorkOrder(new FeatureSetParameters(
                    parameter.Document,
                    parameter.ScoringModel.PeakFeatureCalculators, parameter.PeptideIdentityPaths));
            }

            private SrmDocument RemoveExceptPeptides(SrmDocument document, HashSet<IdentityPath> peptideIdentityPaths)
            {
                if (peptideIdentityPaths == null)
                {
                    return document;
                }
                var moleculeGroups = new List<PeptideGroupDocNode>();
                foreach (var moleculeGroup in document.MoleculeGroups)
                {
                    var molecules = new List<PeptideDocNode>();
                    foreach (var molecule in moleculeGroup.Molecules)
                    {
                        if (molecule.GlobalStandardType != null ||
                            peptideIdentityPaths.Contains(
                                new IdentityPath(moleculeGroup.PeptideGroup, molecule.Peptide)))
                        {
                            molecules.Add(molecule);
                        }
                    }

                    if (molecules.Count > 0)
                    {
                        if (molecules.Count == moleculeGroup.Children.Count)
                        {
                            moleculeGroups.Add(moleculeGroup);
                        }
                        else
                        {
                            moleculeGroups.Add((PeptideGroupDocNode) moleculeGroup.ChangeChildren(molecules.ToArray()));
                        }
                    }
                }

                return (SrmDocument)document.ChangeChildren(moleculeGroups.ToArray());
            }

            private class FeatureSetProducer : Producer<FeatureSetParameters, PeakTransitionGroupFeatureSet>
            {

                public override PeakTransitionGroupFeatureSet ProduceResult(ProductionMonitor productionMonitor,
                    FeatureSetParameters parameter,
                    IDictionary<WorkOrder, object> inputs)
                {
                    return parameter.Document.Value.GetPeakFeatures(parameter.FeatureCalculators,
                        productionMonitor.AsProgressMonitor(),
                        includedPeptidePaths: parameter.PeptideIdentityPaths?.ToHashSet());
                }
            }

            private class FeatureSetParameters : Immutable
            {
                public FeatureSetParameters(SrmDocument document, FeatureCalculators featureCalculators, ImmutableList<IdentityPath> peptideIdentityPaths)
                {
                    Document = document;
                    FeatureCalculators = featureCalculators;
                    PeptideIdentityPaths = peptideIdentityPaths;
                }

                public ReferenceValue<SrmDocument> Document { get; }
                public FeatureCalculators FeatureCalculators { get; }

                public ImmutableList<IdentityPath> PeptideIdentityPaths { get; }

                protected bool Equals(FeatureSetParameters other)
                {
                    return Document.Equals(other.Document) && Equals(FeatureCalculators, other.FeatureCalculators);
                }

                public override bool Equals(object obj)
                {
                    if (ReferenceEquals(null, obj)) return false;
                    if (ReferenceEquals(this, obj)) return true;
                    if (obj.GetType() != GetType()) return false;
                    return Equals((FeatureSetParameters)obj);
                }

                public override int GetHashCode()
                {
                    unchecked
                    {
                        int result = Document.GetHashCode();
                        result = (result * 397) ^ (FeatureCalculators != null ? FeatureCalculators.GetHashCode() : 0);
                        result = (result * 397) ^
                                 (PeptideIdentityPaths != null ? PeptideIdentityPaths.GetHashCode() : 0);
                        return result;
                    }
                }
            }
        }
    }
}
