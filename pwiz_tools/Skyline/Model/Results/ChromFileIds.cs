using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results
{
    public class ChromFileIds : Immutable
    {
        public ChromFileIds(ReplicatePositions replicatePositions, IEnumerable<ChromFileInfoId> fileIds)
        {
            ReplicatePositions = replicatePositions;
            FileIds = ImmutableList.ValueOf(fileIds.Select(ReferenceValue.Of));
        }
        /// <summary>
        /// Nearly every node in a document has the same replicate and file layout, so these
        /// get interned to avoid holding on to many identical instances. Equality is by
        /// value, which is what makes the interning work.
        /// </summary>
        public static ChromFileIds Intern(ValueCache valueCache, ReplicatePositions replicatePositions,
            IEnumerable<ChromFileInfoId> fileIds)
        {
            return valueCache.CacheValue(new ChromFileIds(replicatePositions, fileIds));
        }

        public ReplicatePositions ReplicatePositions { get; private set; }
        public ImmutableList<ReferenceValue<ChromFileInfoId>> FileIds { get; private set; }

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
