using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// Per-file scoring context carrying immutable inputs shared across the
    /// pipeline. Created once per file at the top of ProcessFile.
    /// </summary>
    public class ScoringContext
    {
        public OspreyConfig Config { get; }
        public IResolutionStrategy Resolution { get; }
        public string FileName { get; }

        public ScoringContext(OspreyConfig config, string fileName)
        {
            Config = config;
            FileName = fileName;
            Resolution = ResolutionStrategy.Create(config.ResolutionMode);
        }
    }
}
