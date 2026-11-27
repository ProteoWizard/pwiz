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

namespace pwiz.Quasar.Core.Output
{
    /// <summary>
    /// One member of one accepted cluster, a row of the clusters sidecar (spec Stage 4): what the
    /// pseudo-spectrum mzML cannot carry neatly, for rescoring features and the blib.
    /// </summary>
    public sealed class ClusterMemberRow
    {
        public ClusterMemberRow(int window, int cluster, string spectrumId, int traceId, double mz, int charge,
            bool isAnchor, double area, double sharedFraction, double deltaChiSquare, double apexRt, double startRt,
            double endRt)
        {
            Window = window;
            Cluster = cluster;
            SpectrumId = spectrumId;
            TraceId = traceId;
            Mz = mz;
            Charge = charge;
            IsAnchor = isAnchor;
            Area = area;
            SharedFraction = sharedFraction;
            DeltaChiSquare = deltaChiSquare;
            ApexRt = apexRt;
            StartRt = startRt;
            EndRt = endRt;
        }

        /// <summary>The isolation window's place in the run's scheme.</summary>
        public int Window { get; }

        /// <summary>The cluster's template id, unique within the window.</summary>
        public int Cluster { get; }

        /// <summary>The native id of the cluster's spectrum in the pseudo-spectrum mzML, which search results carry.</summary>
        public string SpectrumId { get; }

        public int TraceId { get; }
        public double Mz { get; }
        public int Charge { get; }
        public bool IsAnchor { get; }

        /// <summary>The member's fitted amplitude, which is its area: templates have unit area.</summary>
        public double Area { get; }

        public double SharedFraction { get; }

        /// <summary>The likelihood-ratio statistic of the membership (spec Stage 3b).</summary>
        public double DeltaChiSquare { get; }

        /// <summary>The cluster's template apex and boundaries, in minutes.</summary>
        public double ApexRt { get; }
        public double StartRt { get; }
        public double EndRt { get; }
    }
}
