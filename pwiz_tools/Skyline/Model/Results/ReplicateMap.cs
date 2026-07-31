using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Results
{
    public class ReplicateMap<T> : IReadOnlyList<IEnumerable<T>>
    {
        public static readonly ReplicateMap<T> EMPTY =
            new ReplicateMap<T>(ReplicatePositions.Simple(0), ImmutableList.Empty<T>());

        public ReplicateMap(IList<IList<T>> items)
        {
            ReplicatePositions = ReplicatePositions.FromCounts(items.Select(item => item.Count));
            Items = items.SelectMany(list=>list).ToImmutable();
        }

        public ReplicateMap(ReplicatePositions replicatePositions, IEnumerable<T> items)
        {
            ReplicatePositions = replicatePositions;
            Items = items.ToImmutable();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<IEnumerable<T>> GetEnumerator()
        {
            return Enumerable.Range(0, Count).Select(replicateIndex => this[replicateIndex]).GetEnumerator();
        }

        public int Count
        {
            get { return ReplicatePositions.ReplicateCount; }
        }

        public IEnumerable<T> this[int index]
        {
            get
            {
                return ReplicatePositions.EnumeratePositions(index)
                    .Select(i => Items[i]);
            }
        }

        public ReplicatePositions ReplicatePositions { get; private set; }
        public ImmutableList<T> Items { get; private set; }
    }
}
