namespace ResourcesOrganizer.ResourcesModel
{
    public class LocalizationIssueType(string name)
    {
        public static string NeedsReviewPrefix = "Needs Review:";
        public static readonly LocalizationIssueType NewResource = new LocalizationIssueType("New resource");
        public static readonly LocalizationIssueType EnglishTextChanged = new EnglishTextChangedType();
        public static readonly LocalizationIssueType InconsistentTranslation =
            new LocalizationIssueType("Inconsistent translation");
        public static readonly LocalizationIssueType MissingTranslation =
            new LocalizationIssueType("Missing translation");

        public static IEnumerable<LocalizationIssueType> All
        {
            get
            {
                return new[] { NewResource, EnglishTextChanged, InconsistentTranslation, MissingTranslation };
            }
        }
        public static LocalizationIssueType? FromName(string? name)
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

        public static ResourceEntry ParseComment(ResourceEntry resourceEntry, string language, string? comment)
        {
            if (comment == null)
            {
                return resourceEntry;
            }
            var reader = new StringReader(comment);
            while (reader.ReadLine() is { } line)
            {
                if (!line.StartsWith(NeedsReviewPrefix))
                {
                    continue;
                }
                var issueTypeName = line.Substring(NeedsReviewPrefix.Length);
                var issueType = FromName(issueTypeName);
                if (issueType == null)
                {
                    Console.Error.WriteLine("Unrecognized issue type: {0}", line);
                    continue;
                }

                var remainder = reader.ReadToEnd();
                return issueType.Parse(resourceEntry, language, remainder);
            }

            return resourceEntry;
        }

        public string Name { get; } = name;

        public virtual string? FormatIssueAsComment(ResourceEntry resourceEntry, LocalizedValue localizedValue)
        {
            return NeedsReviewPrefix + Name;
        }

        public virtual string? GetLocalizedText(ResourceEntry resourceEntry, LocalizedValue localizedValue)
        {
            return localizedValue.ImportedValue ?? localizedValue.OriginalValue;
        }

        public virtual ResourceEntry Parse(ResourceEntry resourceEntry, string language, string commentText)
        {
            var localizedValue = resourceEntry.GetTranslation(language) ?? LocalizedValue.Empty;
            localizedValue = localizedValue with { IssueType = this };
            resourceEntry = resourceEntry with
            {
                LocalizedValues = resourceEntry.LocalizedValues.SetItem(language, localizedValue)
            };
            return resourceEntry;
        }

        private class EnglishTextChangedType() : LocalizationIssueType("English text changed")
        {
            private const string OldEnglish = "Old English:";
            private const string CurrentEnglish = "Current English:";
            private const string OldLocalized = "Old Localized:";

            public override string? FormatIssueAsComment(ResourceEntry resourceEntry, LocalizedValue localizedValue)
            {
                if (localizedValue.ImportedValue == localizedValue.OriginalInvariantValue)
                {
                    // If old translated value was the same as old English, default to new English
                    return null;
                }
                var lines = new List<string>
                {
                    NeedsReviewPrefix + Name,
                    OldEnglish + localizedValue.OriginalInvariantValue,
                    CurrentEnglish + resourceEntry.Invariant.Value,
                    OldLocalized + localizedValue.ImportedValue
                };
                return string.Join(TextUtil.NewLine, lines);
            }

            public override string? GetLocalizedText(ResourceEntry resourceEntry, LocalizedValue localizedValue)
            {
                // If old translated value was the same as old English, default to new English
                if (localizedValue.ImportedValue == localizedValue.OriginalInvariantValue)
                {
                    return null;
                }

                return localizedValue.ImportedValue ?? localizedValue.OriginalValue;
            }

            public override ResourceEntry Parse(ResourceEntry resourceEntry, string language, string commentText)
            {
                var oldEnglishList = new List<string>();
                var currentEnglishList = new List<string>();
                var oldLocalizedList = new List<string>();
                List<string> currentList = null;
                using var reader = new StringReader(commentText);
                while (reader.ReadLine() is { } line)
                {
                    if (line.StartsWith(OldEnglish))
                    {
                        currentList = oldEnglishList;
                        line = line.Substring(OldEnglish.Length);
                    }
                    else if (line.StartsWith(CurrentEnglish))
                    {
                        currentList = currentEnglishList;
                        line = line.Substring(CurrentEnglish.Length);
                    }
                    else if (line.StartsWith(OldLocalized))
                    {
                        currentList = oldLocalizedList;
                        line = line.Substring(OldLocalized.Length);
                    }

                    if (currentList != null)
                    {
                        currentList.Add(line);
                    }
                }
                var localizedValue = resourceEntry.GetTranslation(language) ?? LocalizedValue.Empty;
                localizedValue = localizedValue with
                {
                    IssueType = this,
                    OriginalInvariantValue = string.Join(TextUtil.NewLine, oldEnglishList),
                    OriginalValue = string.Join(TextUtil.NewLine, oldLocalizedList)
                };
                return resourceEntry with
                {
                    LocalizedValues = resourceEntry.LocalizedValues.SetItem(language, localizedValue)
                };
            }
        }
    }
}
