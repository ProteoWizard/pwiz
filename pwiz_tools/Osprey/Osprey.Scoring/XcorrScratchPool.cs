/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// A single set of scratch buffers reused across XCorr calls to avoid
    /// per-call LOH allocation on HRAM (NBins ~100K, ~800 KB per array).
    ///
    /// All preprocessing math runs in f64 on these buffers; the HRAM per-
    /// spectrum cache that feeds the dot product is the sparse form built by
    /// <c>SpectralScorer.PreprocessSpectrumForXcorrSparse</c> (issue #4398).
    /// The f64 scratch is shared between the calibration path
    /// (XcorrAtScan) and the HRAM main-search cache build path, so a
    /// single rented scratch services both.
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
        private readonly ConcurrentBag<XcorrScratch> _scratchBag = new ConcurrentBag<XcorrScratch>();
        private readonly int _nBins;
        private int _scratchAllocCount;

        public XcorrScratchPool(int nBins)
        {
            _nBins = nBins;
        }

        public int NBins { get { return _nBins; } }

        /// <summary>Number of scratch sets ever allocated (high-water mark).</summary>
        public int ScratchAllocCount { get { return _scratchAllocCount; } }

        public XcorrScratch Rent()
        {
            XcorrScratch s;
            if (_scratchBag.TryTake(out s))
                return s;
            Interlocked.Increment(ref _scratchAllocCount);
            return new XcorrScratch(_nBins);
        }

        /// <summary>
        /// Return a scratch to the pool. Clears the fields that accumulate
        /// by += so the next Rent starts from zeros; leaves the fully-
        /// overwritten fields untouched.
        /// </summary>
        public void Return(XcorrScratch s)
        {
            if (s == null)
                return;
            Array.Clear(s.Binned, 0, s.Binned.Length);
            Array.Clear(s.VisitedBins, 0, s.VisitedBins.Length);
            // Windowed / Prefix / Preprocessed are fully overwritten on every
            // preprocess call, so zeroing them here would only duplicate work.
            _scratchBag.Add(s);
        }

    }
}
