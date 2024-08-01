using System.Collections.Immutable;

namespace ResourcesOrganizer.ResourcesModel
{
    public abstract record LocalizableFile(string RelativePath)
    {
        private ImmutableList<ResourceEntry> _entries = [];

        public ImmutableList<ResourceEntry> Entries
        {
            get
            {
                return _entries;
            }
            init
            {
                foreach (var entry in value)
                {
                    if (entry.Invariant.File != null && entry.Invariant.File != RelativePath)
                    {
                        string message = string.Format("File {0} in entry {1} should be {2}", entry.Invariant.File,
                            entry.Invariant.Name, RelativePath);
                        throw new ArgumentException(message);
                    }
                }
                _entries = value;
            }
        }

        public string XmlContent { get; init; } = string.Empty;


        public abstract LocalizableFile ImportLocalizationRecords(string language,
            ILookup<string, LocalizationCsvRecord> records,
            out int matchCount, out int changeCount);

        public abstract string ExportFile(string? language, bool overrideAll);

        public virtual LocalizableFile Add(LocalizableFile other)
        {
            return this;
        }
    }
}
