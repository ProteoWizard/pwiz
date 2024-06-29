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
    public record LocalizationIssue(string Name)
    {
        public static readonly string NeedsReviewPrefix = "Needs Review:";
        public static readonly LocalizationIssue NewResource = new ("New resource");
        public static readonly LocalizationIssue InconsistentTranslation =
            new ("Inconsistent translation");
        public static readonly LocalizationIssue MissingTranslation =
            new ("Missing translation");

        public static IEnumerable<LocalizationIssue> All
        {
            get
            {
                return new[] { NewResource, new EnglishTextChanged(), InconsistentTranslation, MissingTranslation };
            }
        }
        public static LocalizationIssue? FromName(string? name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            return All.FirstOrDefault(issueType => issueType.Name == name);
        }

        public override string ToString()
        {
            return Name;
        }

        public static LocalizationIssue? ParseComment(string? comment)
        {
            if (comment == null)
            {
                return null;
            }
            var reader = new StringReader(comment);
            while (reader.ReadLine() is { } line)
            {
                if (!line.StartsWith(NeedsReviewPrefix))
                {
                    continue;
                }

                return ParseIssue(TextUtil.LineSeparate(line.Substring(NeedsReviewPrefix.Length), reader.ReadToEnd()));
            }
            return null;
        }
        public static LocalizationIssue? ParseIssue(string? issueText)
        {
            if (string.IsNullOrEmpty(issueText))
            {
                return null;
            }
            using var reader = new StringReader(issueText);
            var issueTypeName = reader.ReadLine();
            var issueType = FromName(issueTypeName);
            if (issueType == null)
            {
                Console.Error.WriteLine("Unrecognized issue type: {0}", issueTypeName);
                return null;
            }

            return issueType.ParseCommentText(reader.ReadToEnd());
        }

        public static LocalizationIssue? FromCsvRecord(LocalizationCsvRecord csvRecord)
        {
            return FromName(csvRecord.Issue)?.ParseFromCsvRecord(csvRecord);
        }

        public virtual string GetIssueDetails(ResourceEntry? entry)
        {
            return Name;
        }

        public virtual LocalizationIssue ParseCommentText(string commentText)
        {
            return this;
        }

        public virtual LocalizationIssue ParseFromCsvRecord(LocalizationCsvRecord csvRecord)
        {
            return this;
        }

        public virtual LocalizationCsvRecord StoreInCsvRecord(LocalizationCsvRecord csvRecord)
        {
            return csvRecord with { Issue = Name };
        }

        public virtual bool AppliesToTextOnly => true;
    }
}
