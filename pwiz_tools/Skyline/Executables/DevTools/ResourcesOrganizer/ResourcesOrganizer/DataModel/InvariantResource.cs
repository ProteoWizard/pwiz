using NHibernate.Mapping.Attributes;

namespace ResourcesOrganizer.DataModel
{
    [Class(Lazy = false, Table="InvariantResource")]
    public class InvariantResource : Entity<InvariantResource>
    {
        [Property]
        public string? Name { get; set; }
        [Property]
        public string? Comment { get; set; }
        [Property]
        public string? File { get; set; }
        [Property]
        public string? Type { get; set; }
        [Property]
        public string? Value { get; set; }
        [Property]
        public string? MimeType { get; set; }
        [Property]
        public string? XmlSpace { get; set; }

        public ResourcesModel.InvariantResourceKey GetKey()
        {
            var key = new ResourcesModel.InvariantResourceKey
            {
                Name = Name,
                Type = Type,
                MimeType = MimeType,
                Value = Value!,
                Comment = Comment
            };
            if (!key.IsLocalizableText)
            {
                key = key with { File = File };
            }

            return key;
        }
    }
}
