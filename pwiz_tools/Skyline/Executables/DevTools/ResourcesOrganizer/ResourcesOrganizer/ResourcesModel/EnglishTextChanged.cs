namespace ResourcesOrganizer.ResourcesModel;

public record EnglishTextChanged() : LocalizationIssue("English text changed")
{
    private const string OldEnglish = "Old English:";
    private const string CurrentEnglish = "Current English:";
    private const string OldLocalized = "Old Localized:";

    public EnglishTextChanged(string reviewedEnglish, string reviewedLocalized) : this()
    {
        ReviewedInvariantValue = reviewedEnglish;
        ReviewedLocalizedValue = reviewedLocalized;
    }

    public string? ReviewedInvariantValue { get; init; }
    public string? ReviewedLocalizedValue { get; init; }

    public override string GetIssueDetails(ResourceEntry? resourceEntry)
    {
        var lines = new List<string>
        {
            Name,
            OldEnglish + ReviewedInvariantValue,
        };
        if (resourceEntry != null)
        {
            lines.Add(CurrentEnglish + resourceEntry.Invariant.Value);
        };
        lines.Add(OldLocalized + ReviewedLocalizedValue);
        return TextUtil.LineSeparate(lines);
    }

    public override LocalizationIssue ParseCommentText(string commentText)
    {
        var oldEnglishList = new List<string>();
        var currentEnglishList = new List<string>();
        var oldLocalizedList = new List<string>();
        List<string>? currentList = null;
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

        return this with
        {
            ReviewedLocalizedValue = TextUtil.LineSeparate(oldLocalizedList),
            ReviewedInvariantValue = TextUtil.LineSeparate(oldEnglishList)
        };


    }

    public override bool AppliesToTextOnly => false;
}