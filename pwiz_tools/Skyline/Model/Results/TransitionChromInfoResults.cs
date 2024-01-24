using System;
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

        public static ReplicatePositions FromResults<T>(IResults<T> results) where T : ChromInfo
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
            get
            {
                return Enumerable.Range(GetStart(index), GetCount(index));
            }
        }

        public int GetStart(int index)
        {
            return index == 0 ? 0 : _replicateEndPositions[index - 1];
        }

        public int GetCount(int index)
        {
            int start = index == 0 ? 0 : _replicateEndPositions[index - 1];
            return _replicateEndPositions[index] - start;
        }
    }

    public abstract class TransposedResults<TChromInfo> : Transposition where TChromInfo : ChromInfo
    {

    }

    public class TransitionChromInfoResults : TransposedResults<TransitionChromInfo>
    {
        protected override ITransposer Transposer
        {
            get
            {
                return TransitionChromInfo.TRANSPOSER;
            }
        }

        public static void StoreResults<T>(IList<T> transitionDocNodes) where T : DocNode
        {
            var transposedResults = new TransitionChromInfoResults[transitionDocNodes.Count];
            for (int i = 0; i < transitionDocNodes.Count; i++)
            {
                var docNode = (TransitionDocNode)(object) transitionDocNodes[i];
                if (docNode.Results != null)
                {
                    if (docNode.Results is ResultsImpl<TransitionChromInfo> resultsImpl)
                    {
                        transposedResults[i] = (TransitionChromInfoResults)resultsImpl.List;
                    }
                    else
                    {
                        var transitionChromInfos = docNode.Results.SelectMany(c => c).ToList();
                        transposedResults[i] =
                            (TransitionChromInfoResults)new TransitionChromInfoResults().ChangeColumns(
                                transitionChromInfos.Count,
                                TransitionChromInfo.TRANSPOSER.ToColumns(transitionChromInfos));
                    }
                }
            }

            TransitionChromInfo.TRANSPOSER.EfficientlyStore(transposedResults);
            for (int i = 0; i < transposedResults.Length; i++)
            {
                if (transposedResults[i] != null)
                {
                    var docNode = (TransitionDocNode)(object)transitionDocNodes[i];
                    docNode = docNode.ChangeResults(new ResultsImpl<TransitionChromInfo>(
                        ReplicatePositions.FromResults(docNode.Results), 
                        transposedResults[i]));
                    transitionDocNodes[i] = (T)(object)docNode;
                }
            }
        }

        public static TransitionChromInfoResults FromResults(IResults<TransitionChromInfo> results)
        {
            if (results == null)
            {
                return null;
            }

            if (results is ResultsImpl<TransitionChromInfo> resultsImpl)
            {
                return (TransitionChromInfoResults)resultsImpl.List;
            }
            var transitionChromInfos = results.SelectMany(c => c).ToList();
            return (TransitionChromInfoResults)new TransitionChromInfoResults().ChangeColumns(
                transitionChromInfos.Count,
                TransitionChromInfo.TRANSPOSER.ToColumns(transitionChromInfos));

        }
    }

    public class ResultsImpl<T> : IResults<T> where T : ChromInfo
    {
        public ResultsImpl(ReplicatePositions replicatePositions, TransposedResults<T> list)
        {
            ReplicatePositions = replicatePositions;
            List = list;
        }

        public ReplicatePositions ReplicatePositions { get; private set; }
        public TransposedResults<T> List { get; private set; }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<ChromInfoList<T>> GetEnumerator()
        {
            return Enumerable.Range(0, Count).Select(i => this[i]).GetEnumerator();
        }

        public int Count
        {
            get { return ReplicatePositions.Count; }
        }

        public ChromInfoList<T> this[int index]
        {
            get
            {
                return new ChromInfoList<T>((T[])
                    List.ToRows(ReplicatePositions.GetStart(index), ReplicatePositions.GetCount(index)));
            }
        }
    }
}
