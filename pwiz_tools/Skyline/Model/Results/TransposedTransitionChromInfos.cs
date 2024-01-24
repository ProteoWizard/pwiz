using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.Storage;

namespace pwiz.Skyline.Model.Results
{
    public sealed class ReplicatePositions : IReadOnlyList<IEnumerable<int>>
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

        public int Count
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

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<IEnumerable<int>> GetEnumerator()
        {
            return Enumerable.Range(0, Count).Select(i => this[i]).GetEnumerator();
        }

        public IEnumerable<int> this[int index]
        {
            get { return Enumerable.Range(GetStart(index), GetCount(index)); }
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
                .Concat(Enumerable.Range(index + 1, Count - index - 1)));
        }

    }
    public abstract class TransposedResults<TChromInfo> : Transposition<TChromInfo> where TChromInfo : ChromInfo
    {
    }

    public class TransposedTransitionChromInfos : TransposedResults<TransitionChromInfo>
    {
        public static readonly TransposedTransitionChromInfos EMPTY = new TransposedTransitionChromInfos();
        protected override ITransposer Transposer
        {
            get { return TransitionChromInfo.TRANSPOSER; }
        }

        public static void StoreResults<T>(IList<T> transitionDocNodes) where T : DocNode
        {
            var transposedResults = new TransposedTransitionChromInfos[transitionDocNodes.Count];
            for (int i = 0; i < transitionDocNodes.Count; i++)
            {
                var docNode = (TransitionDocNode)(object)transitionDocNodes[i];
                transposedResults[i] = FromResults(docNode.Results);
            }

            TransitionChromInfo.TRANSPOSER.EfficientlyStore(transposedResults);
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
            return new TransposedTransitionChromInfos().ChangeResults(results.FlatList);
        }
    }
}