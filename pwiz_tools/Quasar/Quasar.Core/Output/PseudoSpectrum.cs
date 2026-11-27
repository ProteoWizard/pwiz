/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Collections.Generic;

namespace pwiz.Quasar.Core.Output
{
    /// <summary>A precursor candidate for a cluster (spec Stage 3f).</summary>
    public sealed class PrecursorCandidate
    {
        public PrecursorCandidate(double mz, int charge, int tier)
        {
            Mz = mz;
            Charge = charge;
            Tier = tier;
        }

        /// <summary>Monoisotopic m/z.</summary>
        public double Mz { get; }

        /// <summary>Charge, or 0 when unknown.</summary>
        public int Charge { get; }

        /// <summary>1 for an MS1 feature, 2 for an unfragmented precursor seen in MS2.</summary>
        public int Tier { get; }
    }

    /// <summary>
    /// One cluster as a centroided MS2 spectrum (spec Stage 4): its members' areas at their
    /// deconvoluted m/z, its apex time, its isolation window and its precursor candidates. With no
    /// candidate (tier 3) the search uses the isolation window as the precursor tolerance.
    /// </summary>
    public sealed class PseudoSpectrum
    {
        public PseudoSpectrum(string nativeId, double retentionTime, double isolationTarget,
            double isolationLowerOffset, double isolationUpperOffset, IReadOnlyList<PrecursorCandidate> precursors,
            double[] mzs, double[] intensities, IReadOnlyList<KeyValuePair<string, double>> userParams)
        {
            if (mzs.Length != intensities.Length)
                throw new ArgumentException($@"{mzs.Length} m/z values for {intensities.Length} intensities");
            NativeId = nativeId;
            RetentionTime = retentionTime;
            IsolationTarget = isolationTarget;
            IsolationLowerOffset = isolationLowerOffset;
            IsolationUpperOffset = isolationUpperOffset;
            Precursors = precursors;
            Mzs = mzs;
            Intensities = intensities;
            UserParams = userParams;
        }

        /// <summary>Stable spectrum id: <c>run=&lt;stem&gt; window=&lt;n&gt; cluster=&lt;k&gt;</c>.</summary>
        public string NativeId { get; }

        /// <summary>Cluster apex, in minutes.</summary>
        public double RetentionTime { get; }

        public double IsolationTarget { get; }
        public double IsolationLowerOffset { get; }
        public double IsolationUpperOffset { get; }

        /// <summary>Precursor candidates, tier 1 first; empty for a tier 3 cluster.</summary>
        public IReadOnlyList<PrecursorCandidate> Precursors { get; }

        public double[] Mzs { get; }
        public double[] Intensities { get; }

        /// <summary>Extra numeric values written as userParams (boundaries, EMG shape, member counts).</summary>
        public IReadOnlyList<KeyValuePair<string, double>> UserParams { get; }
    }
}
