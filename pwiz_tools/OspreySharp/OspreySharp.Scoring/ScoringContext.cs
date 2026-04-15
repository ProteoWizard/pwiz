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

        /// <summary>
        /// Pool of per-spectrum XCorr scratch buffers reused across the
        /// main search. Lazily created by the pipeline right before Stage 4
        /// (<see cref="EnsureXcorrScratchPool"/>); null during calibration.
        /// Keeping a single pool per scoring run lets gen-2 hold onto the
        /// 100K-bin HRAM arrays instead of triggering LOH churn per scan.
        /// </summary>
        public XcorrScratchPool XcorrScratchPool { get; private set; }

        public ScoringContext(OspreyConfig config, string fileName)
        {
            Config = config;
            FileName = fileName;
            Resolution = ResolutionStrategy.Create(config.ResolutionMode);
        }

        public XcorrScratchPool EnsureXcorrScratchPool(int nBins)
        {
            if (XcorrScratchPool == null || XcorrScratchPool.NBins != nBins)
                XcorrScratchPool = new XcorrScratchPool(nBins);
            return XcorrScratchPool;
        }
    }
}
