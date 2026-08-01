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
        /// <summary>
        /// No replicates and so no files, which is what a map holding nothing is laid out by.
        /// </summary>
        public static readonly ChromFileIds EMPTY =
            new ChromFileIds(ReplicatePositions.FromCounts(new int[0]), new ChromFileInfoId[0]);

        public ChromFileIds(ReplicatePositions replicatePositions, IEnumerable<ChromFileInfoId> fileIds)
        {
            ReplicatePositions = replicatePositions;
            FileIds = ImmutableList.ValueOf(fileIds.Select(ReferenceValue.Of));
        }

        public ReplicatePositions ReplicatePositions { get; private set; }
        public ImmutableList<ReferenceValue<ChromFileInfoId>> FileIds { get; private set; }

        /// <summary>
        /// The flat position of a file's entry in one replicate, or -1 when the replicate has
        /// nothing for that file.
        /// <para>
        /// This is how a position is found. Nothing should count its way to one, because the
        /// entries of a replicate are in no order a caller can rely on. A replicate almost
        /// always has exactly one entry, so searching it is cheap.
        /// </para>
        /// </summary>
        /// <summary>
        /// The flat position of a file's entry, or -1. A file belongs to one replicate, so it
        /// identifies a position on its own when the replicate is not already known.
        /// </summary>
        public int IndexOfFile(ChromFileInfoId fileId)
        {
            for (int position = 0; position < FileIds.Count; position++)
            {
                if (ReferenceEquals(FileIds[position].Value, fileId))
                {
                    return position;
                }
            }

            return -1;
        }

        /// <summary>
        /// The files one replicate has entries for. Costs nothing beyond walking the positions,
        /// which is what makes it the answer for callers that only need to know which files there
        /// are rather than anything about their peaks.
        /// </summary>
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
    }
}
