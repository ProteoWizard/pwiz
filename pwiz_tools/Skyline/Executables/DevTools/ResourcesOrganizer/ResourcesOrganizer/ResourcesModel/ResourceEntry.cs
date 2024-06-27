using System.Collections.Immutable;

namespace ResourcesOrganizer.ResourcesModel
{
    public record ResourceEntry(string Name, InvariantResourceKey Invariant)
    {
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

        public ResourceEntry Normalize()
        {
            var localizedValues = LocalizedValues
                .Where(kvp => kvp.Value.OriginalValue != null || kvp.Value.ReviewedValue != Invariant.Value)
                .ToImmutableDictionary(kvp => kvp.Key, kvp => new LocalizedValue 
                    { ReviewedValue = kvp.Value.ReviewedValue ?? kvp.Value.OriginalValue });
            return this with
            {
                Position = 0,
                LocalizedValues = localizedValues
            };
        }

        public virtual bool Equals(ResourceEntry? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (Name == other.Name && Invariant.Equals(other.Invariant) && 
                XmlSpace == other.XmlSpace && Position == other.Position &&
                LocalizedValues.ToHashSet().SetEquals(other.LocalizedValues))
            {
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hashCode = HashCode.Combine(Name, Invariant, XmlSpace, Position);
            foreach (var kvp in LocalizedValues)
            {
                hashCode ^= kvp.GetHashCode();
            }

            return hashCode;
        }

        public string? GetLocalizedText(string? language)
        {
            if (language == null)
            {
                return Invariant.Value;
            }

            var localizedValue = GetTranslation(language);
            if (localizedValue == null)
            {
                return null;
            }

            if (localizedValue.IssueType == null)
            {
                if (localizedValue.OriginalValue == null)
                {
                    if (localizedValue.ReviewedValue == null || localizedValue.ReviewedValue == Invariant.Value)
                    {
                        return null;
                    }

                    return localizedValue.ReviewedValue;
                }

                return localizedValue.ReviewedValue ?? localizedValue.OriginalValue;
            }

            return localizedValue.IssueType.GetLocalizedText(this, localizedValue);
        }

        public string? GetComment(string? language)
        {
            if (language == null)
            {
                return Invariant.Comment;
            }

            var commentLines = new List<string>();
            if (Invariant.Comment != null)
            {
                commentLines.Add(Invariant.Comment);
            }
            var localizedValue = GetTranslation(language);
            var issueComment = localizedValue?.IssueType?.FormatIssueAsComment(this, localizedValue);
            if (issueComment != null)
            {
                commentLines.Add(issueComment);
            }

            if (commentLines.Count == 0)
            {
                return null;
            }

            return TextUtil.LineSeparate(commentLines);
        }
    }
}
