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
using pwiz.Quasar.Core.Elution;

namespace pwiz.Quasar.Core.Clustering
{
    /// <summary>Where a template came from.</summary>
    public enum TemplateSource
    {
        /// <summary>Grouped anchors, in the first pass (spec Stage 2d).</summary>
        anchors,

        /// <summary>Grouped anchors found in the residuals of an earlier fit (spec Stage 3c).</summary>
        residual_pass,

        /// <summary>A precursor Osprey already identified (spec Stage 3a, known templates).</summary>
        known
    }

    /// <summary>
    /// A shared elution profile: the unit-area EMG shape every member of a cluster is fitted to,
    /// with the anchors it was built from.
    /// </summary>
    public sealed class ElutionTemplate
    {
        public ElutionTemplate(int id, EmgShape shape, IReadOnlyList<int> anchorTraceIds, TemplateSource source)
        {
            Id = id;
            Shape = shape;
            AnchorTraceIds = anchorTraceIds;
            Source = source;
        }

        /// <summary>Unique within the window.</summary>
        public int Id { get; }

        public EmgShape Shape { get; }

        /// <summary>The anchor traces the shape was fitted to; empty for a known template.</summary>
        public IReadOnlyList<int> AnchorTraceIds { get; }

        public TemplateSource Source { get; }

        public override string ToString()
        {
            return $@"Template[{Id}, {Source}, {Shape}, {AnchorTraceIds.Count} anchors]";
        }
    }
}
