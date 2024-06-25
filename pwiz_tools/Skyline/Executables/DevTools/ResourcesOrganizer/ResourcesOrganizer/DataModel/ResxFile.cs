using NHibernate.Mapping.Attributes;

namespace ResourcesOrganizer.DataModel
{
    [Class(Lazy = false)]
    public class ResxFile : Entity<ResxFile>
    {
        [Property]
        public string? FilePath { get; set; }
        [Property]
        public string? XmlContent { get; set; }
    }
}
