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

using System.Globalization;

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// Which bins become unknowns in the local block solved for each spectrum.
    /// </summary>
    public enum DemuxBlockMode
    {
        /// <summary>
        /// Every bin that a selected window covers is a column, so the forward model is
        /// exact. The block is then short of full rank by k-1 along a sign-alternating
        /// direction, and non-negativity resolves it for sparse channels. The default.
        /// </summary>
        covered_bins,

        /// <summary>
        /// pwiz's block: a fixed slice of bins, with edge windows cut down to the part inside
        /// the slice. The block is square and full rank, but a cut window still carries
        /// signal from its bin outside the slice. That error alternates in sign along the
        /// ladder into the target bins whenever a channel is shared with that outside bin
        /// (y1, immonium and b2 ions are the common case). Kept to attribute differences
        /// against msconvert.
        /// </summary>
        truncated_slice,
    }

    /// <summary>
    /// What intensity a demultiplexed peak carries.
    /// </summary>
    public enum DemuxOutputMode
    {
        /// <summary>
        /// Each observed peak is split across the spectrum's own bins in proportion to the
        /// solution, so the pieces sum back to what was measured at the time it was
        /// measured. This is pwiz's behavior and the default. Mass balance is 1.0 by
        /// construction, so it cannot reveal solver bias.
        /// </summary>
        apportioned,

        /// <summary>The solution itself, for each of the spectrum's bins.</summary>
        solution,
    }

    /// <summary>
    /// Settings for demultiplexing. Every setting that changes numeric output must also be
    /// part of the demux cache's parameter hash.
    /// </summary>
    public sealed class DemuxParams
    {
        /// <summary>
        /// Version of the demultiplexing algorithm. Bump it whenever a change alters the output
        /// for unchanged settings, so demultiplexed caches written before the change are rebuilt.
        /// </summary>
        public const int ALGORITHM_VERSION = 2;

        /// <summary>Default fragment-channel tolerance, the pwiz massError default.</summary>
        public const double DEFAULT_CHANNEL_TOLERANCE_PPM = 10;

        /// <summary>Bins in the core of each local block (pwiz's OverlapRegionsInApprox).</summary>
        public const int DEFAULT_BLOCK_BINS = 7;

        /// <summary>
        /// Half-width of a fragment channel: a peak in another spectrum within this distance
        /// of a target centroid is the same channel. Parts per million, or Th when
        /// <see cref="ChannelToleranceIsPpm"/> is false.
        /// </summary>
        public double ChannelTolerance { get; set; } = DEFAULT_CHANNEL_TOLERANCE_PPM;

        public bool ChannelToleranceIsPpm { get; set; } = true;

        public RtInterpolation Interpolation { get; set; } = RtInterpolation.makima;

        public DemuxBlockMode BlockMode { get; set; } = DemuxBlockMode.covered_bins;

        public DemuxOutputMode OutputMode { get; set; } = DemuxOutputMode.apportioned;

        /// <summary>Window boundaries closer than this (Th) are one boundary.</summary>
        public double MinimumBinWidth { get; set; } = DemuxSchemeDetector.DEFAULT_MINIMUM_BIN_WIDTH;

        public int BlockBins { get; set; } = DEFAULT_BLOCK_BINS;

        /// <summary>
        /// Degree of parallelism across spectra. The output does not depend on it.
        /// </summary>
        public int Threads { get; set; } = 1;

        /// <summary>
        /// The algorithm version and every setting that changes the output, in a stable,
        /// culture-invariant form. A demultiplexed cache records it and is rebuilt when it
        /// differs. <see cref="Threads"/> is left out because it never changes the output.
        /// </summary>
        public string Descriptor
        {
            get
            {
                var ic = CultureInfo.InvariantCulture;
                return string.Format(ic,
                    @"osprey-demux/{0};block={1};interpolation={2};output={3};channel_tolerance={4}{5};min_bin_width={6};block_bins={7}",
                    ALGORITHM_VERSION, BlockMode, Interpolation, OutputMode,
                    ChannelTolerance.ToString(@"R", ic), ChannelToleranceIsPpm ? @"ppm" : @"th",
                    MinimumBinWidth.ToString(@"R", ic), BlockBins);
            }
        }
    }
}
