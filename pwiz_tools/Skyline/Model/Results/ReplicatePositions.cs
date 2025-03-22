using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Results
{
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
            var endPositions = counts.Select(count => total += count).ToImmutable();
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
}
