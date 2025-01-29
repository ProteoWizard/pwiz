/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
        public static ConsensusAlignmentResults PerformAlignment(ProductionMonitor productionMonitor, IDictionary<ReplicateFileId, Dictionary<Target, double>> fileTimesDictionaries)
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

            return new ConsensusAlignmentResults(alignmentFunctions, unsortedConsensusTimes.ToDictionary(kvp=>kvp.Value, kvp=>kvp.Key));
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
                    return values[i];
                }
                double result = (values[i - 1] * (nextKey - key) + values[i] * (key - prevKey)) / (nextKey - prevKey);
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

    public class ConsensusAlignmentResults
    {
        public ConsensusAlignmentResults(IEnumerable<KeyValuePair<ReplicateFileId, AlignmentFunction>> alignmentFunctions,
            IEnumerable<KeyValuePair<Target, double>> standardTimes)
        {
            AlignmentFunctions = ImmutableList.ValueOf(alignmentFunctions);
            StandardTimes = ImmutableList.ValueOf(standardTimes);
        }

        public ImmutableList<KeyValuePair<Target, double>> StandardTimes { get; }
        public ImmutableList<KeyValuePair<ReplicateFileId, AlignmentFunction>> AlignmentFunctions { get; }
    }

    public class ConsensusScoreCalculator : RetentionScoreCalculatorSpec
    {
        private Dictionary<Target, double> _standardTimes;
        public ConsensusScoreCalculator(string name, AlignmentResults alignmentResults) : base(name)
        {
            AlignmentResults = alignmentResults;
            _standardTimes = CollectionUtil.SafeToDictionary(alignmentResults.StandardTimes);
        }

        public AlignmentResults AlignmentResults { get; }

        public override double? ScoreSequence(Target sequence)
        {
            if (_standardTimes.TryGetValue(sequence, out var score))
            {
                return score;
            }
            return null;
        }

        public override double UnknownScore
        {
            get { return 0; }
        }
        public override IEnumerable<Target> ChooseRegressionPeptides(IEnumerable<Target> peptides, out int minCount)
        {
            minCount = 2;
            return peptides.Where(_standardTimes.ContainsKey);
        }

        public override IEnumerable<Target> GetStandardPeptides(IEnumerable<Target> peptides)
        {
            return _standardTimes.Keys;
        }
    }
}
