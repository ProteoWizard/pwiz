/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
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
