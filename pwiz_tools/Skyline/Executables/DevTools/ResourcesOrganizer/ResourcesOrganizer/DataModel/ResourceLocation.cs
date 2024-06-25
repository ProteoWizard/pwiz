using NHibernate.Mapping.Attributes;

namespace ResourcesOrganizer.DataModel
{
    [Class(Lazy = false)]
    public class ResourceLocation : Entity<ResourceLocation>
    {
        [Property]
        public long InvariantResourceId { get; set; }
        [Property]
        public long ResxFileId { get; set; }
        [Property]
        public int SortIndex { get; set; }
        [Property]
        public int Position { get; set; }
        [Property]
        public string? Name { get; set; }
    }
}
