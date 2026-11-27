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
using System.Linq;

namespace pwiz.Quasar.Core.Clustering
{
    /// <summary>One trace's membership in a cluster (spec Stage 3b).</summary>
    public sealed class ClusterMember
    {
        public ClusterMember(Trace trace, double amplitude, double deltaChiSquare, double sharedFraction)
        {
            Trace = trace;
            Amplitude = amplitude;
            DeltaChiSquare = deltaChiSquare;
            SharedFraction = sharedFraction;
        }

        public Trace Trace { get; }

        /// <summary>The trace's area under the cluster's template, from the window fit.</summary>
        public double Amplitude { get; }

        /// <summary>The membership statistic: how much the fit worsens without this template.</summary>
        public double DeltaChiSquare { get; }

        /// <summary>
        /// This cluster's share of the trace's area over all the clusters it is a member of: 1 for
        /// an unshared member.
        /// </summary>
        public double SharedFraction { get; }

        /// <summary>True when the trace is also a member of another cluster.</summary>
        public bool IsShared
        {
            get { return SharedFraction < 1.0; }
        }
    }

    /// <summary>Why a cluster was not passed on to the pseudo-spectrum (spec Stage 3d).</summary>
    public enum ClusterRejection
    {
        none,
        no_anchor_member,
        too_few_members
    }

    /// <summary>A template and the traces it explains: the unit that becomes one pseudo-spectrum.</summary>
    public sealed class Cluster
    {
        public Cluster(ElutionTemplate template, IReadOnlyList<ClusterMember> members, ClusterRejection rejection)
        {
            Template = template;
            Members = members;
            Rejection = rejection;
        }

        public ElutionTemplate Template { get; }

        /// <summary>Members in descending order of amplitude.</summary>
        public IReadOnlyList<ClusterMember> Members { get; }

        public ClusterRejection Rejection { get; }

        public bool IsAccepted
        {
            get { return Rejection == ClusterRejection.none; }
        }

        public int AnchorCount
        {
            get { return Members.Count(m => m.Trace.IsAnchor); }
        }

        public int SharedCount
        {
            get { return Members.Count(m => m.IsShared); }
        }

        public override string ToString()
        {
            return $@"Cluster[{Template.Id}, {Members.Count} members, {AnchorCount} anchors, {Rejection}]";
        }
    }
}
