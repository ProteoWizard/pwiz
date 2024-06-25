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
    }
}
