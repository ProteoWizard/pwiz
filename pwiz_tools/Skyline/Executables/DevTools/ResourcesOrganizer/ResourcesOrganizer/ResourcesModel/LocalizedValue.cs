namespace ResourcesOrganizer.ResourcesModel
{
    public record LocalizedValue
    {
        public string Value { get; init; }
        public string? Problem { get; init; }
        public string? OriginalInvariantValue { get; init; }
    }
}
