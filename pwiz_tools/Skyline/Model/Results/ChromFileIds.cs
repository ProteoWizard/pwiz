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
