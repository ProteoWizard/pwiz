using System.Collections.Immutable;

namespace ResourcesOrganizer.ResourcesModel
{
    public record ResourceEntry
    {
        public string Name { get; init; }
        public InvariantResourceKey Invariant { get; init; }
        public string? MimeType { get; init; }
        public string? XmlSpace { get; init; }
        /// <summary>
        /// Position of the element in the XML relative to <see cref="ResourcesFile.PreserveNode"/> nodes.
        /// </summary>
        public int Position { get; init; }
        public ImmutableDictionary<string, LocalizedValue> LocalizedValues { get; init; } 
            = ImmutableDictionary<string, LocalizedValue>.Empty;

        public LocalizedValue? GetTranslation(string language)
        {
            LocalizedValues.TryGetValue(language, out var localizedValue);
            return localizedValue;
        }

        public object GetContentKey()
        {
            return this with { Position = 0 };
        }

        public virtual bool Equals(ResourceEntry? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (Name == other.Name && Invariant.Equals(other.Invariant) && MimeType == other.MimeType &&
                XmlSpace == other.XmlSpace && Position == other.Position &&
                LocalizedValues.OrderBy(kvp=>kvp.Key).SequenceEqual(other.LocalizedValues.OrderBy(kvp=>kvp.Key)))
            {
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hashCode = HashCode.Combine(Name, Invariant, MimeType, XmlSpace, Position);
            foreach (var kvp in LocalizedValues)
            {
                hashCode ^= kvp.GetHashCode();
            }

            return hashCode;
        }
    }
}
