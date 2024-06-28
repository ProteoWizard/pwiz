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

        public virtual string GetIssueDetails(ResourceEntry? entry)
        {
            return Name;
        }

        public virtual LocalizationIssue ParseCommentText(string commentText)
        {
            return this;
        }

        public virtual bool AppliesToTextOnly => true;
    }
}
