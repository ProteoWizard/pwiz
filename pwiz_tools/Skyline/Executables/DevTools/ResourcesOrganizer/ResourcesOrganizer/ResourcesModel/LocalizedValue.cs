namespace ResourcesOrganizer.ResourcesModel
{
    public record LocalizedValue
    {
        public static readonly LocalizedValue Empty = new();
        /// <summary>
        /// Value found in the .resx that was read using "add" command
        /// </summary>
        public string? OriginalValue { get; init; }
        /// <summary>
        /// Value that was imported using "importtranslations" command
        /// </summary>
        public string? ImportedValue { get; init; }

        public string CurrentValue
        {
            get { return ImportedValue ?? OriginalValue ?? string.Empty; }
        }
        public LocalizationIssueType? IssueType { get; init; }
        /// <summary>
        /// Invariant value in the database that was imported with the "importtranslations" command
        /// </summary>
        public string? OriginalInvariantValue { get; init; }
    }
}
