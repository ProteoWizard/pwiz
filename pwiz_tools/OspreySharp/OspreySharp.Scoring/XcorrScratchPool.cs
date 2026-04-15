using System;
using System.Collections.Concurrent;
using System.Threading;

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// A single set of scratch buffers reused across XCorr calls to avoid
    /// per-call LOH allocation on HRAM (NBins ~100K, ~800 KB per array).
    ///
    /// Each field is a plain Large Object once pooled; the pool hands out
    /// and takes back sets of four arrays, never the arrays individually,
    /// so the binned/windowed/prefix/preprocessed buffers stay co-located
    /// across the full preprocess -> score pipeline.
    /// </summary>
    public sealed class XcorrScratch
    {
        public readonly double[] Binned;        // NBins, accumulated via +=, needs zeroing on return
        public readonly double[] Windowed;      // NBins, fully overwritten, no zeroing needed
        public readonly double[] Prefix;        // NBins+1, fully overwritten, no zeroing needed
        public readonly double[] Preprocessed;  // NBins, fully overwritten, no zeroing needed
        public readonly bool[] VisitedBins;     // NBins, touched at fragment positions, needs zeroing on return

        public XcorrScratch(int nBins)
        {
            Binned = new double[nBins];
            Windowed = new double[nBins];
            Prefix = new double[nBins + 1];
            Preprocessed = new double[nBins];
            VisitedBins = new bool[nBins];
        }
    }

    /// <summary>
    /// Pool of XcorrScratch sets reused across all XCorr calls in a scoring
    /// run. The pool grows organically on demand up to its natural high-water
    /// mark (typically NThreads sets) and never shrinks. Arrays inside pooled
    /// sets live in gen-2 LOH for the lifetime of the run, so repeated
    /// XcorrAtScan calls do not trigger LOH allocation / gen-2 GC.
    ///
    /// Create one pool per scoring run on <see cref="ScoringContext"/>.
    /// Usage:
    ///   var scratch = pool.Rent();
    ///   try { scorer.XcorrAtScan(spectrum, entry, scratch); }
    ///   finally { pool.Return(scratch); }
    /// </summary>
    public sealed class XcorrScratchPool
    {
        private readonly ConcurrentBag<XcorrScratch> _bag = new ConcurrentBag<XcorrScratch>();
        private readonly int _nBins;
        private int _allocCount;

        public XcorrScratchPool(int nBins)
        {
            _nBins = nBins;
        }

        public int NBins { get { return _nBins; } }

        /// <summary>Number of scratch sets ever allocated (high-water mark).</summary>
        public int AllocCount { get { return _allocCount; } }

        public XcorrScratch Rent()
        {
            XcorrScratch s;
            if (_bag.TryTake(out s))
                return s;
            Interlocked.Increment(ref _allocCount);
            return new XcorrScratch(_nBins);
        }

        /// <summary>
        /// Return a scratch to the pool. Clears the fields that accumulate
        /// by += so the next Rent starts from zeros; leaves the fully-
        /// overwritten fields untouched.
        /// </summary>
        public void Return(XcorrScratch s)
        {
            if (s == null) return;
            Array.Clear(s.Binned, 0, s.Binned.Length);
            Array.Clear(s.VisitedBins, 0, s.VisitedBins.Length);
            _bag.Add(s);
        }
    }
}
