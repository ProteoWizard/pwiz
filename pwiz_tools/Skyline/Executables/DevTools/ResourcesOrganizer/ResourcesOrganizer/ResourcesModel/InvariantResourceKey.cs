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
namespace ResourcesOrganizer.ResourcesModel
{
    public sealed record InvariantResourceKey : IComparable<InvariantResourceKey>, IComparable
    {
        public string? Name { get; init; }
        public string? Comment { get; init; }
        public string? File { get; init; }
        public string? Type { get; init; }
        public string? MimeType { get; init; }
        public string Value { get; init; } = string.Empty;

        public int CompareTo(InvariantResourceKey? other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            var nameComparison = string.Compare(Name, other.Name, StringComparison.OrdinalIgnoreCase);
            if (nameComparison != 0) return nameComparison;
            var commentComparison = string.Compare(Comment, other.Comment, StringComparison.OrdinalIgnoreCase);
            if (commentComparison != 0) return commentComparison;
            var fileComparison = string.Compare(File, other.File, StringComparison.OrdinalIgnoreCase);
            if (fileComparison != 0) return fileComparison;
            var valueComparison = string.Compare(Value, other.Value, StringComparison.OrdinalIgnoreCase);
            if (valueComparison != 0) return valueComparison;
            return string.Compare(Type, other.Type, StringComparison.OrdinalIgnoreCase);
        }

        public int CompareTo(object? obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            if (ReferenceEquals(this, obj)) return 0;
            return obj is InvariantResourceKey other
                ? CompareTo(other)
                : throw new ArgumentException($"Object must be of type {nameof(InvariantResourceKey)}");
        }

        public override string ToString()
        {
            var parts = new List<string>();
            if (Name != null)
            {
                parts.Add($"Name:{Name}");
            }

            parts.Add($"Value:{TextUtil.Quote(Value)}");

            if (Comment != null)
            {
                parts.Add($"Comment:{TextUtil.Quote(Comment)}");
            }

            if (Type != null)
            {
                parts.Add($"Type:{Type}");
            }

            if (MimeType != null)
            {
                parts.Add($"MimeType:{MimeType}");
            }

            return string.Join(" ", parts);
        }

        public InvariantResourceKey FileQualified(string file)
        {
            return this with { File = file };
        }

        public bool IsLocalizableText
        {
            get
            {
                if (true == Name?.StartsWith(">>"))
                {
                    return false;
                }

                if (Type != null || MimeType != null)
                {
                    return false;
                }

                return true;
            }
        }
    }
}
