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
            return this with
            {
                Position = 0,
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

            return GetTranslation(language)?.Value;
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

            var issueDetails = GetIssueDetails(language);
            if (issueDetails != null)
            {
                commentLines.Add(LocalizationIssue.NeedsReviewPrefix + issueDetails);
            }
            if (commentLines.Count == 0)
            {
                return null;
            }

            return TextUtil.LineSeparate(commentLines);
        }

        public string? GetIssueDetails(string language)
        {
            var localizedValue = GetTranslation(language);
            return localizedValue?.Issue?.GetIssueDetails(this);
        }

        public LocalizedValue LocalizedValueIssue(LocalizationIssue issue)
        {
            if (issue.AppliesToTextOnly && !Invariant.IsLocalizableText)
            {
                return new LocalizedValue(Invariant.Value);
            }
            return new LocalizedValue(Invariant.Value, issue);
        }
    }
}
