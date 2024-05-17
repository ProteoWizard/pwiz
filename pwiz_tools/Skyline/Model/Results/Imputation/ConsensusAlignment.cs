using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MathNet.Numerics.Statistics;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.RetentionTimes;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class ConsensusAlignment
    {
        public static readonly ConsensusAlignment EMPTY = new ConsensusAlignment(
            ImmutableSortedList<double, Target>.EMPTY, new Dictionary<ReplicateFileId, AlignmentFunction>());
        private Dictionary<ReplicateFileId, AlignmentFunction> _alignmentFunctions;
        public AlignmentFunction GetAlignment(ReplicateFileId replicateFileId)
        {
            _alignmentFunctions.TryGetValue(replicateFileId, out var result);
            return result;
        }

        private ConsensusAlignment(ImmutableSortedList<double, Target> consensusValues,
            Dictionary<ReplicateFileId, AlignmentFunction> alignmentFunctions)
        {
            ConsensusValues = consensusValues;
            _alignmentFunctions = alignmentFunctions;
        }

        public ImmutableSortedList<double, Target> ConsensusValues { get; }

        public static ConsensusAlignment MakeConsensusAlignment(IDictionary<ReplicateFileId, Dictionary<Target, double>> fileTimesDictionaries)
        {
            var lcsFinder = new LongestCommonSequenceFinder<Target>(fileTimesDictionaries.Values.Select(dictionary =>
                dictionary.OrderBy(kvp => kvp.Value).Select(kvp => kvp.Key).ToList()));
            var unsortedConsensusTimes = new List<KeyValuePair<double, Target>>();
            foreach (var target in lcsFinder.GetLongestCommonSubsequence())
            {
                var times = new List<double>();
                foreach (var fileTimes in fileTimesDictionaries)
                {
                    if (fileTimes.Value.TryGetValue(target, out var time))
                    {
                        times.Add(time);
                    }
                }

                if (times.Count > 0)
                {
                    unsortedConsensusTimes.Add(new KeyValuePair<double, Target>(times.Mean(), target));
                }
            }

            var consensusTimes = ImmutableSortedList.FromValues(unsortedConsensusTimes);

            var alignmentFunctions = new Dictionary<ReplicateFileId, AlignmentFunction>();
            foreach (var entry in fileTimesDictionaries)
            {
                var xValues = new List<double>();
                var yValues = new List<double>();
                foreach (var consensusEntry in consensusTimes)
                {
                    if (entry.Value.TryGetValue(consensusEntry.Value, out var time))
                    {
                        xValues.Add(time);
                        yValues.Add(consensusEntry.Key);
                    }
                }

                var forwardFunction = new PiecewiseLinearRegressionFunction(xValues.ToArray(), yValues.ToArray(), 0);
                var reverseFunction = new PiecewiseLinearRegressionFunction(yValues.ToArray(), xValues.ToArray(), 0);
                alignmentFunctions.Add(entry.Key, AlignmentFunction.Define(forwardFunction.GetY, reverseFunction.GetY));
            }

            return new ConsensusAlignment(consensusTimes, alignmentFunctions);
        }

        public class Parameters : Immutable
        {
            public Parameters(SrmDocument document, RtValueType rtValueType)
            {
                Document = document;
                RtValueType = rtValueType;
            }
            public SrmDocument Document { get; private set; }

            public RtValueType RtValueType { get; private set; }

            protected bool Equals(Parameters other)
            {
                return ReferenceEquals(Document, other.Document) && Equals(RtValueType, other.RtValueType);
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
                    return (RuntimeHelpers.GetHashCode(Document) * 397) ^ RtValueType.GetHashCode();
                }
            }
        }

        public static readonly Producer<Parameters, ConsensusAlignment> PRODUCER = new Producer();

        private class Producer : Producer<Parameters, ConsensusAlignment>
        {
            public override ConsensusAlignment ProduceResult(ProductionMonitor productionMonitor, Parameters parameter, IDictionary<WorkOrder, object> inputs)
            {
                var fileTimesDictionaries = new Dictionary<ReplicateFileId, Dictionary<Target, double>>();
                foreach (var replicateFileInfo in ReplicateFileInfo.List(parameter.Document.MeasuredResults))
                {
                    var times = parameter.RtValueType.GetRetentionTimes(parameter.Document,
                        replicateFileInfo.ReplicateFileId);
                    if (times.Count > 0)
                    {
                        fileTimesDictionaries.Add(replicateFileInfo.ReplicateFileId, times);
                    }
                }

                if (fileTimesDictionaries.Count == 0)
                {
                    return EMPTY;
                }

                return MakeConsensusAlignment(fileTimesDictionaries);
            }
        }

    }
}
