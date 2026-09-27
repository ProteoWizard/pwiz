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
    /// Counts describing one demultiplexing run. All are exact integer sums, so they do not
    /// depend on thread count or scheduling.
    /// </summary>
    public sealed class DemuxStatistics
    {
        public long SpectraIn { get; set; }
        public long SpectraOut { get; set; }

        /// <summary>Distinct local block geometries, i.e. factorizations built.</summary>
        public int Geometries { get; set; }

        /// <summary>Fragment channels solved (one NNLS each).</summary>
        public long Channels { get; set; }

        public long ZeroSolves { get; set; }
        public long UnconstrainedSolves { get; set; }
        public long ActiveSetSolves { get; set; }
        public long IterationCapSolves { get; set; }

        internal void Add(DemuxStatistics other)
        {
            Channels += other.Channels;
            ZeroSolves += other.ZeroSolves;
            UnconstrainedSolves += other.UnconstrainedSolves;
            ActiveSetSolves += other.ActiveSetSolves;
            IterationCapSolves += other.IterationCapSolves;
        }

        internal void Count(NnlsPath path)
        {
            Channels++;
            switch (path)
            {
                case NnlsPath.zero:
                    ZeroSolves++;
                    break;
                case NnlsPath.unconstrained:
                    UnconstrainedSolves++;
                    break;
                case NnlsPath.active_set:
                    ActiveSetSolves++;
                    break;
                default:
                    IterationCapSolves++;
                    break;
            }
        }
    }

    /// <summary>
    /// The demultiplexed MS2 spectra of a run, with the scheme that produced them.
    /// </summary>
    public sealed class DemuxResult
    {
        public DemuxResult(DemuxScheme scheme, IReadOnlyList<Spectrum> spectra, DemuxStatistics statistics)
        {
            Scheme = scheme;
            Spectra = spectra;
            Statistics = statistics;
        }

        public DemuxScheme Scheme { get; }

        /// <summary>
        /// Output spectra in acquisition order of their parents, and within one parent in
        /// ascending bin order. Each keeps its parent's scan number and retention time.
        /// </summary>
        public IReadOnlyList<Spectrum> Spectra { get; }

        public DemuxStatistics Statistics { get; }
    }
}
