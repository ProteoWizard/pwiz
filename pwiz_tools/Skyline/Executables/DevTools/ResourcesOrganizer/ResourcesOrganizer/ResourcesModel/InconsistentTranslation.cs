using System.Collections.Immutable;

namespace ResourcesOrganizer.ResourcesModel
{
    public record InconsistentTranslation() : LocalizationIssue("Inconsistent translation")
    {
        private const string CandidatePrefix = "Candidate Translation:";
        public InconsistentTranslation(IEnumerable<string> candidates) : this()
        {
            Candidates = candidates.Distinct().OrderBy(text => text).ToImmutableList();
        }

        public ImmutableList<string> Candidates { get; init; } = [];

        public override string GetIssueDetails(ResourceEntry? resourceEntry)
        {
            return TextUtil.LineSeparate(Candidates.Select(candidate => CandidatePrefix + candidate).Prepend(Name));
        }

        public override LocalizationIssue ParseCommentText(string commentText)
        {
            return this with { Candidates = ParseCandidates(commentText).ToImmutableList() };
        }

        public override LocalizationCsvRecord StoreInCsvRecord(LocalizationCsvRecord csvRecord)
        {
            return base.StoreInCsvRecord(csvRecord) with {OldLocalized = TextUtil.LineSeparate(Candidates.Select(candidate=>CandidatePrefix + candidate))};
        }

        private List<string> ParseCandidates(string text)
        {
            List<string> candidates = new List<string>();
            List<string>? currentCandidate = null;
            using var reader = new StringReader(text);
            while (reader.ReadLine() is { } line)
            {
                if (line.StartsWith(CandidatePrefix))
                {
                    if (currentCandidate != null)
                    {
                        candidates.Add(TextUtil.LineSeparate(currentCandidate));
                    }
                    currentCandidate = [line.Substring(CandidatePrefix.Length)];
                }
                else
                {
                    currentCandidate?.Add(line);
                }
            }

            if (currentCandidate != null)
            {
                candidates.Add(TextUtil.LineSeparate(currentCandidate));
            }

            return candidates;
        }

        public override LocalizationIssue ParseFromCsvRecord(LocalizationCsvRecord csvRecord)
        {
            return (InconsistentTranslation)base.ParseFromCsvRecord(csvRecord) with
            {
                Candidates = ParseCandidates(csvRecord.OldLocalized).ToImmutableList()
            };
        }
    }
}
