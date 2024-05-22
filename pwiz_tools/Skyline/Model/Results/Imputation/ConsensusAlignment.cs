using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using MathNet.Numerics.Statistics;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.RetentionTimes;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class ConsensusAlignment
    {
        public static Dictionary<ReplicateFileId, AlignmentFunction> PerformAlignment(ProductionMonitor productionMonitor, IDictionary<ReplicateFileId, Dictionary<Target, double>> fileTimesDictionaries)
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

                alignmentFunctions.Add(entry.Key, new InterpolatingAlignmentFunction(xValues, yValues));
            }

            return alignmentFunctions;
        }

        private class InterpolatingAlignmentFunction : AlignmentFunction
        {
            public InterpolatingAlignmentFunction(IEnumerable<double> xValues, IEnumerable<double> yValues)
            {
                XValues = ImmutableList.ValueOf(xValues);
                YValues = ImmutableList.ValueOf(yValues);
            }

            public override double GetY(double x)
            {
                return Interpolate(x, XValues, YValues);
            }

            public override double GetX(double y)
            {
                return Interpolate(y, YValues, XValues);
            }

            public ImmutableList<double> XValues { get; }
            public ImmutableList<double> YValues { get; }

            private double Interpolate(double key, IList<double> keys, IList<double> values)
            {
                if (keys.Count == 0)
                {
                    return key;
                }

                if (key <= keys[0])
                {
                    return values[0] + key - keys[0];
                }

                if (key >= keys[keys.Count - 1])
                {
                    return values[keys.Count - 1] + key - keys[keys.Count - 1];
                }

                int i = CollectionUtil.BinarySearch(keys, key);
                if (i >= 0)
                {
                    return values[i];
                }

                i = ~i;
                var prevKey = keys[i - 1];
                var nextKey = keys[i];
                if (nextKey - prevKey <= 0)
                {
                    Trace.TraceWarning("{0} - {1} <= 0", nextKey, prevKey);
                    return values[i];
                }
                double result = (values[i - 1] * (nextKey - key) + values[i] * (key - prevKey)) / (nextKey - prevKey);
                if (double.IsNaN(result))
                {
                    Trace.TraceWarning("NaN looking for {0}", key);
                }
                return result;
            }

            protected bool Equals(InterpolatingAlignmentFunction other)
            {
                return Equals(XValues, other.XValues) && Equals(YValues, other.YValues);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((InterpolatingAlignmentFunction)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (XValues.GetHashCode()* 397) ^ YValues.GetHashCode();
                }
            }
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
    }
}
