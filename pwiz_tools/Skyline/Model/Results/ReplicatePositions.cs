using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// A list of replicates, each of which is the positions belonging to it, so that walking a
    /// replicate is <c>replicatePositions[replicateIndex]</c>.
    /// </summary>
    public sealed class ReplicatePositions : IReadOnlyList<IEnumerable<int>>
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

        /// <summary>
        /// The positions in the flat list belonging to one replicate. This is how a caller walks a
        /// replicate: nothing should be counting its way from the start and the end, and putting the
        /// arithmetic here means no caller has to remember that
        /// <see cref="Enumerable.Range(int,int)"/> takes a count rather than an end.
        /// <para>
        /// Empty for a replicate index which does not exist, the way <see cref="GetCount"/> is
        /// zero for one, so callers do not have to range check first.
        /// </para>
        /// </summary>
        public IEnumerable<int> this[int replicateIndex]
        {
            get { return Enumerable.Range(GetStart(replicateIndex), GetCount(replicateIndex)); }
        }

        /// <summary>
        /// How many replicates, which is what this is a list of. Not to be confused with
        /// <see cref="TotalCount"/>, which is how many positions they hold between them.
        /// </summary>
        public int Count
        {
            get { return ReplicateCount; }
        }

        public IEnumerator<IEnumerable<int>> GetEnumerator()
        {
            return Enumerable.Range(0, ReplicateCount).Select(i => this[i]).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// The same positions with one replicate holding a different number of them. Grows to reach
        /// <paramref name="index"/> when it is past the end, the new replicates before it holding
        /// none.
        /// </summary>
        public ReplicatePositions ChangeCountAt(int index, int newCount)
        {
            if (newCount == GetCount(index))
            {
                return this;
            }

            // The counts of the other replicates, not their indexes, which is what the tail of this
            // used to concatenate. Nothing called it, so it never showed.
            int replicateCount = index < ReplicateCount ? ReplicateCount : index + 1;
            return FromCounts(Enumerable.Range(0, replicateCount)
                .Select(i => i == index ? newCount : GetCount(i)));
        }

        /// <summary>
        /// The same positions without the replicates at the end which hold none, or null when that
        /// leaves no positions at all.
        /// <para>
        /// Only the ones at the end go. A replicate with no positions in the middle is saying that
        /// the replicate is there and has nothing, which the ones after it depend on to be at the
        /// right index.
        /// </para>
        /// </summary>
        public ReplicatePositions Normalize()
        {
            if (TotalCount == 0)
            {
                return null;
            }

            // Terminates because something is somewhere: TotalCount is not zero.
            int replicateCount = ReplicateCount;
            while (GetCount(replicateCount - 1) == 0)
            {
                replicateCount--;
            }

            return replicateCount == ReplicateCount
                ? this
                : FromCounts(Enumerable.Range(0, replicateCount).Select(GetCount));
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
