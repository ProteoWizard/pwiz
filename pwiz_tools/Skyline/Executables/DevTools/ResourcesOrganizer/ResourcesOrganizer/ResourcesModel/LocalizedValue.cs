namespace ResourcesOrganizer.ResourcesModel
{
    public record LocalizedValue(string Value)
    {
        public LocalizedValue(string value, LocalizationIssue? issue) : this(value)
        {
            Issue = issue;
        }
        public LocalizationIssue? Issue { get; init; }
    }
}
