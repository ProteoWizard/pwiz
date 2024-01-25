using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results
{
    public sealed class ReplicatePositions
    {
        private ImmutableList<int> _replicateEndPositions;

        public static ReplicatePositions Simple(int replicateCount)
        {
            return FromCounts(Enumerable.Repeat(1, replicateCount));
        }

        public static ReplicatePositions FromResults<T>(Results<T> results) where T : ChromInfo
        {
            return FromCounts(results.Select(chromInfoList => chromInfoList.Count));
        }

        public static ReplicatePositions FromCounts(IEnumerable<int> counts)
        {
            int total = 0;
            var endPositions = ImmutableList.ValueOf(counts.Select(count => total += count));
            return new ReplicatePositions(endPositions);
        }

        private ReplicatePositions(ImmutableList<int> endPositions)
        {
            _replicateEndPositions = endPositions;
        }

        public int ReplicateCount
        {
            get { return _replicateEndPositions.Count; }
        }

        public int TotalCount
        {
            get
            {
                if (_replicateEndPositions.Count == 0)
                {
                    return 0;
                }

                return _replicateEndPositions[_replicateEndPositions.Count - 1];
            }
        }

        public int GetStart(int index)
        {
            if (index <= 0)
            {
                return 0;
            }

            if (index >= _replicateEndPositions.Count)
            {
                return TotalCount;
            }

            return _replicateEndPositions[index - 1];
        }

        public int GetCount(int index)
        {
            if (index < 0 || index >= _replicateEndPositions.Count)
            {
                return 0;
            }

            return _replicateEndPositions[index] - GetStart(index);
        }

        public ReplicatePositions ChangeCountAt(int index, int newCount)
        {
            if (newCount == GetCount(index))
            {
                return this;
            }

            return FromCounts(Enumerable.Range(0, index).Select(GetCount).Append(newCount)
                .Concat(Enumerable.Range(index + 1, ReplicateCount - index - 1)));
        }

        private bool Equals(ReplicatePositions other)
        {
            return _replicateEndPositions.Equals(other._replicateEndPositions);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is ReplicatePositions other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _replicateEndPositions.GetHashCode();
        }
    }
    public abstract class TransposedResults<TChromInfo> : Transposition<TChromInfo> where TChromInfo : ChromInfo
    {
    }

    public class TransposedTransitionChromInfos : TransposedResults<TransitionChromInfo>
    {
        public static readonly TransposedTransitionChromInfos EMPTY = new TransposedTransitionChromInfos();
        public override Transposer GetTransposer()
        {
            return TransitionChromInfo.TRANSPOSER;
        }

        public static void StoreResults<T>(ValueCache valueCache, IList<T> transitionDocNodes) where T : DocNode
        {
            var transposedResults = new TransposedTransitionChromInfos[transitionDocNodes.Count];
            for (int i = 0; i < transitionDocNodes.Count; i++)
            {
                var docNode = (TransitionDocNode)(object)transitionDocNodes[i];
                transposedResults[i] = FromResults(docNode.Results);
            }

            TransitionChromInfo.TRANSPOSER.EfficientlyStore(valueCache, transposedResults);
            for (int i = 0; i < transposedResults.Length; i++)
            {
                if (transposedResults[i] != null)
                {
                    var docNode = (TransitionDocNode)(object)transitionDocNodes[i];
                    docNode = docNode.ChangeResults(
                        Results<TransitionChromInfo>.FromColumns(docNode.Results.ReplicatePositions,
                            transposedResults[i]));
                    transitionDocNodes[i] = (T)(object)docNode;
                }
            }
        }

        public TransposedTransitionChromInfos ChangeResults(IEnumerable<TransitionChromInfo> results)
        {
            return (TransposedTransitionChromInfos)ChangeColumns(
                TransitionChromInfo.TRANSPOSER.ToColumns(results.ToList()));
        }

        public static TransposedTransitionChromInfos FromResults(Results<TransitionChromInfo> results)
        {
            if (results == null)
            {
                return null;
            }
            return EMPTY.ChangeResults(results.FlatList);
        }
    }
}