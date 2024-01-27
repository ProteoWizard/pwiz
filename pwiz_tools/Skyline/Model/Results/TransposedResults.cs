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
using pwiz.Common.Collections;
using pwiz.Common.Collections.Transpositions;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Associates items in a flat list with particular replicates.
    /// </summary>
    public sealed class ReplicatePositions
    {
        private ImmutableList<int> _replicateEndPositions;

        /// <summary>
        /// Returns a ReplicatePositions where there is one item per replicate
        /// </summary>
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

        /// <summary>
        /// Returns the position in the flat list of the first item associated with a particular replicate.
        /// </summary>
        public int GetStart(int replicateIndex)
        {
            if (replicateIndex <= 0)
            {
                return 0;
            }

            if (replicateIndex >= _replicateEndPositions.Count)
            {
                return TotalCount;
            }

            return _replicateEndPositions[replicateIndex - 1];
        }

        
        public int GetCount(int replicateIndex)
        {
            if (replicateIndex < 0 || replicateIndex >= _replicateEndPositions.Count)
            {
                return 0;
            }

            return _replicateEndPositions[replicateIndex] - GetStart(replicateIndex);
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
        public static int MIN_RESULTS_TO_TRANSPOSE = 10;
        public override Transposer<TransitionChromInfo> GetTransposer()
        {
            return TransitionChromInfo.TRANSPOSER;
        }

        public static void StoreResults<T>(ValueCache valueCache, IList<T> transitionDocNodes) where T : DocNode
        {
            if (MIN_RESULTS_TO_TRANSPOSE > 0)
            {
                if (!transitionDocNodes.Cast<TransitionDocNode>().Any(d => d.Results?.ReplicatePositions.TotalCount >= MIN_RESULTS_TO_TRANSPOSE))
                {
                    return;
                }
            }

            if (transitionDocNodes.Cast<TransitionDocNode>().All(docNode => false != docNode.Results?.IsColumnar))
            {
                // Everything already transposed: no work to do
                return;
            }
            var transposedResults = new TransposedTransitionChromInfos[transitionDocNodes.Count];
            for (int i = 0; i < transitionDocNodes.Count; i++)
            {
                var docNode = (TransitionDocNode)(object)transitionDocNodes[i];
                transposedResults[i] = FromResults(docNode.Results);
#if DEBUG
                if (docNode.Results == null)
                {
                    Assume.IsNull(transposedResults[i]);
                }
                else
                {
                    var roundTrip =
                        Results<TransitionChromInfo>.FromColumns(docNode.Results.ReplicatePositions,
                            transposedResults[i]);
                    if (!Equals(docNode.Results, roundTrip))
                    {
                        Assume.Fail();
                    }
                }
#endif
            }

            TransitionChromInfo.TRANSPOSER.EfficientlyStore(valueCache, transposedResults);
            for (int i = 0; i < transposedResults.Length; i++)
            {
                if (transposedResults[i] != null)
                {
                    var docNode = (TransitionDocNode)(object)transitionDocNodes[i];
                    docNode = docNode.StoreOptimizedResults(
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