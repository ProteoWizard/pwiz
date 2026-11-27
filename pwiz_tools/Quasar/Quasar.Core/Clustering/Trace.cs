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

namespace pwiz.Quasar.Core.Clustering
{
    /// <summary>
    /// One MS2 trace in one isolation window (spec Stage 1): a fragment followed across the
    /// window's scans, with zeros where it is absent.
    /// </summary>
    public sealed class Trace
    {
        public Trace(int id, bool isAnchor, double mz, int charge, double[] intensities)
        {
            Id = id;
            IsAnchor = isAnchor;
            Mz = mz;
            Charge = charge;
            Intensities = intensities;
        }

        /// <summary>Unique within the window.</summary>
        public int Id { get; }

        /// <summary>
        /// True for a Hardklor/Kronik feature (an isotope envelope with a monoisotopic mass and
        /// charge), false for a plain centroid trace. Only anchors build templates.
        /// </summary>
        public bool IsAnchor { get; }

        /// <summary>For an anchor, the singly charged monoisotopic m/z; otherwise the observed m/z.</summary>
        public double Mz { get; }

        /// <summary>The observed charge, or 0 when unknown (centroid traces).</summary>
        public int Charge { get; }

        /// <summary>Intensities on the window's full scan axis.</summary>
        public double[] Intensities { get; }

        public override string ToString()
        {
            return $@"Trace[{Id}, {(IsAnchor ? @"anchor" : @"centroid")}, m/z {Mz}]";
        }
    }
}
