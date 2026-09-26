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

using System.Collections.Generic;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// What kind of isolation scheme an acquisition used, as far as demultiplexing is
    /// concerned.
    /// </summary>
    public enum DemuxSchemeKind
    {
        /// <summary>No precursor bin is sampled by more than one isolation window.</summary>
        non_overlapping,

        /// <summary>
        /// Stepped overlapping windows (staggered DIA): each narrow bin is sampled by
        /// several windows acquired at different times.
        /// </summary>
        overlapping,
    }

    /// <summary>
    /// A narrow precursor m/z bin: the interval between two adjacent isolation window
    /// boundaries, which is the finest precursor specificity the acquisition supports.
    /// </summary>
    public readonly struct DemuxBin
    {
        public DemuxBin(double lowerBound, double upperBound)
        {
            LowerBound = lowerBound;
            UpperBound = upperBound;
        }

        public double LowerBound { get; }
        public double UpperBound { get; }
        public double Center { get { return (LowerBound + UpperBound) / 2.0; } }
        public double Width { get { return UpperBound - LowerBound; } }

        /// <summary>
        /// The bin as the symmetric isolation window a demultiplexed spectrum carries.
        /// </summary>
        public IsolationWindow ToIsolationWindow()
        {
            return IsolationWindow.Symmetric(Center, Width / 2.0);
        }
    }

    /// <summary>
    /// One distinct isolation window of the acquisition, with the contiguous run of
    /// narrow bins it covers.
    /// </summary>
    public sealed class AcquisitionWindow
    {
        public AcquisitionWindow(int index, double lowerBound, double upperBound,
            int firstBin, int lastBin)
        {
            Index = index;
            LowerBound = lowerBound;
            UpperBound = upperBound;
            FirstBin = firstBin;
            LastBin = lastBin;
        }

        /// <summary>Position in <see cref="DemuxScheme.Windows"/> (sorted by m/z).</summary>
        public int Index { get; }
        public double LowerBound { get; }
        public double UpperBound { get; }

        /// <summary>First bin covered, as an index into <see cref="DemuxScheme.Bins"/>.</summary>
        public int FirstBin { get; }

        /// <summary>Last bin covered (inclusive).</summary>
        public int LastBin { get; }

        public int BinCount { get { return LastBin - FirstBin + 1; } }

        /// <summary>Center of the covered bins in bin-index units.</summary>
        public double BinIndexCenter { get { return (FirstBin + LastBin) / 2.0; } }

        public bool Covers(int bin)
        {
            return FirstBin <= bin && bin <= LastBin;
        }
    }

    /// <summary>
    /// The isolation scheme of a run as the demultiplexer sees it: the narrow bins, the
    /// distinct acquisition windows over them, and which window each MS2 spectrum used.
    /// </summary>
    public sealed class DemuxScheme
    {
        public DemuxScheme(DemuxSchemeKind kind, IReadOnlyList<DemuxBin> bins,
            IReadOnlyList<AcquisitionWindow> windows, int overlapFactor, int maxCoverage,
            int[] windowOfSpectrum)
        {
            Kind = kind;
            Bins = bins;
            Windows = windows;
            OverlapFactor = overlapFactor;
            MaxCoverage = maxCoverage;
            WindowOfSpectrum = windowOfSpectrum;
        }

        public DemuxSchemeKind Kind { get; }

        /// <summary>Narrow bins in ascending m/z order.</summary>
        public IReadOnlyList<DemuxBin> Bins { get; }

        /// <summary>Distinct acquisition windows, sorted by lower bound then upper bound.</summary>
        public IReadOnlyList<AcquisitionWindow> Windows { get; }

        /// <summary>
        /// The number of windows covering most of the precursor range: k for a k-fold
        /// stagger, 1 for a non-overlapping scheme, including one whose adjacent windows
        /// share a thin margin.
        /// </summary>
        public int OverlapFactor { get; }

        /// <summary>The largest number of windows covering any one bin.</summary>
        public int MaxCoverage { get; }

        /// <summary>
        /// For each MS2 spectrum, in the acquisition order it was presented to the
        /// detector, the index of its window in <see cref="Windows"/>.
        /// </summary>
        public int[] WindowOfSpectrum { get; }
    }
}
