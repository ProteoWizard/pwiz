using pwiz.Common.SystemUtil;

namespace pwiz.Common.Storage
{
    public class ListStorageRequirements : Immutable
    {
        public int ItemSize { get; private set; }
        public long TotalItemCount { get; private set; }
        public int UniqueItemCount { get; private set; }
        public int ListCount { get; private set; }
    }

    public interface IListStorageType
    {
        public long? GetMemoryFootprint(ListStorageRequirements listStorageRequirements);
    }
}
