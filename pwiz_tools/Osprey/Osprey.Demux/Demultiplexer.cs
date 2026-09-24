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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// Entry point: demultiplexes a run's MS2 spectra to the narrowest bins its isolation
    /// scheme supports.
    /// </summary>
    public static class Demultiplexer
    {
        /// <summary>
        /// Demultiplexes MS2 spectra given in acquisition order. A non-overlapping scheme is
        /// returned unchanged, as the same spectrum objects.
        /// </summary>
        public static DemuxResult Demultiplex(IReadOnlyList<Spectrum> ms2Spectra, DemuxParams parameters)
        {
            if (ms2Spectra == null)
                throw new ArgumentNullException(nameof(ms2Spectra));
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var scheme = DetectScheme(ms2Spectra, parameters.MinimumBinWidth);
            if (scheme.Kind == DemuxSchemeKind.non_overlapping)
            {
                var statistics = new DemuxStatistics
                {
                    SpectraIn = ms2Spectra.Count,
                    SpectraOut = ms2Spectra.Count,
                };
                return new DemuxResult(scheme, ms2Spectra, statistics);
            }
            return new OverlapDemultiplexer(scheme, ms2Spectra, parameters).Run();
        }

        /// <summary>
        /// Detects the isolation scheme of MS2 spectra given in acquisition order.
        /// </summary>
        public static DemuxScheme DetectScheme(IReadOnlyList<Spectrum> ms2Spectra,
            double minimumBinWidth = DemuxSchemeDetector.DEFAULT_MINIMUM_BIN_WIDTH)
        {
            var windows = new IsolationWindow[ms2Spectra.Count];
            for (int i = 0; i < windows.Length; i++)
                windows[i] = ms2Spectra[i].IsolationWindow;
            return DemuxSchemeDetector.Detect(windows, minimumBinWidth);
        }
    }
}
