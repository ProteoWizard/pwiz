using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public class PeakScoreCache
    {
        private FeatureCalculators _scoreTypes;
        private IDictionary<Key, ImmutableList<float>> _scores;

        public PeakScoreCache(SrmDocument document) : this(FeatureCalculators.ALL, document)
        {
        }
        public PeakScoreCache(FeatureCalculators scoreTypes, SrmDocument document)
        {
            _scoreTypes = scoreTypes;
            _scores = new Dictionary<Key, ImmutableList<float>>();
            Document = document;
        }

        public SrmDocument Document { get; }

        public int? GetScoreTypeIndex(Type type)
        {
            return _scoreTypes.IndexOf(type);
        }

        public ImmutableList<float> GetDetailScores(IdentityPath precursorIdentityPath, int replicateIndex, ChromFileInfo chromFileInfo)
        {
            ImmutableList<float> scores;
            var key = new Key(precursorIdentityPath, replicateIndex, chromFileInfo.FileId);
            lock (_scores)
            {
                if (_scores.TryGetValue(key, out scores))
                {
                    return scores;
                }
            }
            CalculateScores(precursorIdentityPath.Parent, replicateIndex, chromFileInfo);
            lock (_scores)
            {
                _scores.TryGetValue(key, out scores);
                return scores;
            }
        }

        private void CalculateScores(IdentityPath peptideIdentityPath, int replicateIndex, ChromFileInfo chromFileInfo)
        {
            var peptideDocNode = (PeptideDocNode)Document.FindNode(peptideIdentityPath);
            if (peptideDocNode == null)
            {
                return;
            }

            var detailScoreCalculator = new OnDemandFeatureCalculator(_scoreTypes, Document, peptideDocNode,
                replicateIndex, chromFileInfo);
            var scoreTuples = detailScoreCalculator.CalculateAllComparableGroupScores();
            lock (_scores)
            {
                foreach (var tuple in scoreTuples)
                {
                    foreach (var transitionGroup in tuple.Item1)
                    {
                        var key = new Key(new IdentityPath(peptideIdentityPath, transitionGroup.TransitionGroup),
                            replicateIndex, chromFileInfo.FileId);
                        _scores[key] = tuple.Item2.Values;
                    }
                }
            }
        }

        private class Key
        {
            public Key(IdentityPath precursorIdentityPath, int replicateIndex, ChromFileInfoId chromFileInfoId)
            {
                PrecursorIdentityPath = precursorIdentityPath;
                ReplicateIndex = replicateIndex;
                ChromFileInfoId = chromFileInfoId;
            }
            public IdentityPath PrecursorIdentityPath { get; }
            public int ReplicateIndex { get; }
            public ChromFileInfoId ChromFileInfoId { get; }

            protected bool Equals(Key other)
            {
                return PrecursorIdentityPath.Equals(other.PrecursorIdentityPath) && ReplicateIndex == other.ReplicateIndex && ReferenceEquals(ChromFileInfoId, other.ChromFileInfoId);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Key) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = PrecursorIdentityPath.GetHashCode();
                    hashCode = (hashCode * 397) ^ ReplicateIndex;
                    hashCode = (hashCode * 397) ^ RuntimeHelpers.GetHashCode(ChromFileInfoId);
                    return hashCode;
                }
            }
        }

        public class ScoreTypes
        {
            private IDictionary<Type, int> _scoreTypeIndexes;
            public ScoreTypes(IEnumerable<DetailedPeakFeatureCalculator> calculators)
            {
                Calculators = ImmutableList.ValueOf(calculators);
                _scoreTypeIndexes = new Dictionary<Type, int>();
                for (int i = 0; i < Calculators.Count; i++)
                {
                    _scoreTypeIndexes[Calculators[i].GetType()] = i;
                }
            }

            public int? GetScoreTypeIndex(Type type)
            {
                if (_scoreTypeIndexes.TryGetValue(type, out int index))
                {
                    return index;
                }

                return null;
            }

            public ImmutableList<DetailedPeakFeatureCalculator> Calculators { get; }
        }
    }
}
