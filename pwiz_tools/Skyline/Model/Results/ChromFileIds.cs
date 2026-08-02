using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Which file each flat position belongs to, and which replicate.
    /// <para>
    /// Nearly every node in a document has the same replicate and file layout, so these are
    /// worth interning once they are held somewhere long lived, such as on a
    /// <see cref="DocNode"/>. Equality is by value, which is what will make that work.
    /// </para>
    /// </summary>
    public class ChromFileIds : Immutable
    {
        public static readonly ChromFileIds Empty = new ChromFileIds(Results.ReplicatePositions.FromCounts(ImmutableList.Empty<int>()), Enumerable.Empty<ChromFileInfoId>());
        public ChromFileIds(ReplicatePositions replicatePositions, IEnumerable<ChromFileInfoId> fileIds)
        {
            ReplicatePositions = replicatePositions;
            FileIds = ImmutableList.ValueOf(fileIds.Select(ReferenceValue.Of));
        }

        public ReplicatePositions ReplicatePositions { get; private set; }
        public ImmutableList<ReferenceValue<ChromFileInfoId>> FileIds { get; private set; }

        /// <summary>
        /// Every file, in position order. A file belongs to one replicate, so none appears twice.
        /// </summary>
        public IEnumerable<ChromFileInfoId> GetFileIds()
        {
            return FileIds.Select(fileId => fileId.Value);
        }

        public IEnumerable<ChromFileInfoId> GetFileIds(int replicateIndex)
        {
            return ReplicatePositions[replicateIndex].Select(position => FileIds[position].Value);
        }

        /// <summary>
        /// Whether each replicate has any entry at all, one flag per replicate. The columnar
        /// answer to what used to be asked as ChromInfoList.IsEmpty for each replicate in turn.
        /// </summary>
        public IEnumerable<bool> GetReplicatesWithResults()
        {
            for (int replicateIndex = 0; replicateIndex < ReplicatePositions.ReplicateCount; replicateIndex++)
            {
                yield return ReplicatePositions.GetCount(replicateIndex) > 0;
            }
        }

        /// <summary>
        /// The flat position of a file's entry in one replicate, or -1 when the replicate has
        /// nothing for that file.
        /// <para>
        /// This is the only way a position is found, and the replicate is always known: nothing
        /// searches every file of the document for one, and nothing counts its way to a position,
        /// because the entries of a replicate are in no order a caller can rely on. A replicate
        /// almost always has exactly one entry, so searching it is cheap.
        /// </para>
        /// </summary>
        public int IndexOfFile(int replicateIndex, ChromFileInfoId fileId)
        {
            foreach (int position in ReplicatePositions[replicateIndex])
            {
                if (ReferenceEquals(FileIds[position].Value, fileId))
                {
                    return position;
                }
            }

            return -1;
        }

        protected bool Equals(ChromFileIds other)
        {
            return ReplicatePositions.Equals(other.ReplicatePositions) && FileIds.Equals(other.FileIds);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((ChromFileIds)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ReplicatePositions.GetHashCode() * 397) ^ FileIds.GetHashCode();
            }
        }

        /// <summary>
        /// The files of both, replicate by replicate, this one's first. Returns this when
        /// <paramref name="other"/> has no file it does not already have, so a layout which does not
        /// change stays reference equal - which is what makes a union across many precursors sharing
        /// one layout cost nothing.
        /// </summary>
        public ChromFileIds Union(ChromFileIds other)
        {
            return UnionAll(new[] { this, other });
        }

        public static ChromFileIds UnionAll(IEnumerable<ChromFileIds> chromFileIds)
        {
            var list = chromFileIds.Where(fileIds => null != fileIds && fileIds.ReplicatePositions.Count > 0).Distinct().ToList();
            if (list.Count == 0)
            {
                return Empty;
            }

            if (list.Count == 1)
            {
                return list[0];
            }

            int replicateCount = list.Max(fileIds => fileIds.ReplicatePositions.ReplicateCount);
            var allFileIds = new List<List<ChromFileInfoId>>(replicateCount);
            for (int replicateIndex = 0; replicateIndex < replicateCount; replicateIndex++)
            {
                var replicateFileIds = list.SelectMany(fileIds => fileIds.GetFileIds(replicateIndex))
                    .Distinct<ChromFileInfoId>(ReferenceValue.EQUALITY_COMPARER).ToList();
                allFileIds.Add(replicateFileIds);
            }

            var result = new ChromFileIds(ReplicatePositions.FromCounts(allFileIds.Select(row=>row.Count)), allFileIds.SelectMany(row=>row));
            return list.FirstOrDefault(result.Equals) ?? result;
        }

        /// <summary>
        /// The same layout without the replicates at the end which have no file, or null when it
        /// has no file at all. See <see cref="ReplicatePositions.Normalize"/>.
        /// </summary>
        public ChromFileIds Normalize()
        {
            var replicatePositions = ReplicatePositions.Normalize();
            if (replicatePositions == null)
            {
                return null;
            }

            // The files do not change: the replicates which go had none.
            return ReferenceEquals(replicatePositions, ReplicatePositions)
                ? this
                : new ChromFileIds(replicatePositions, FileIds.Select(fileId => fileId.Value));
        }
    }
}
