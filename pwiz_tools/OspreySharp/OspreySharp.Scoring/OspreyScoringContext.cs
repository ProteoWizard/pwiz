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
using System.Collections.Generic;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// Shared per-candidate scoring context, mirroring Skyline's
    /// <c>PeakScoringContext</c>: it carries the machinery shared across a
    /// candidate's feature calculators plus a typed byproduct cache
    /// (<see cref="AddInfo{TInfo}"/> / <see cref="TryGetInfo{TInfo}"/>) so one
    /// producer can publish an intermediate (e.g. the reference-XIC selection) for
    /// its sibling calculators to read instead of recomputing it.
    ///
    /// The instance is reused across candidates within a file/window;
    /// <see cref="ClearByproducts"/> is called between candidates so the byproduct
    /// dictionary is not reallocated in the per-candidate hot loop. The machinery
    /// members (scorer, preprocessed-xcorr cache, calibration, ...) are added by
    /// the feature families that read them.
    /// </summary>
    public class OspreyScoringContext
    {
        private readonly Dictionary<Type, object> _byproducts = new Dictionary<Type, object>();

        public OspreyScoringContext(OspreyConfig config)
        {
            Config = config;
        }

        /// <summary>The live (post-calibration) configuration the pipeline scores with.</summary>
        public OspreyConfig Config { get; }

        /// <summary>
        /// Publish a byproduct keyed by its type for sibling calculators to read.
        /// Throws if one of this type was already published for the current
        /// candidate (mirrors Skyline's <c>AddInfo</c>); producers publish once,
        /// guarded by a <see cref="TryGetInfo{TInfo}"/> check.
        /// </summary>
        public void AddInfo<TInfo>(TInfo info)
        {
            _byproducts.Add(typeof(TInfo), info);
        }

        /// <summary>
        /// Get a byproduct published earlier during the current candidate's
        /// scoring. Returns false (and <paramref name="info"/> = default) when none
        /// was published, so callers can apply a family-specific default.
        /// </summary>
        public bool TryGetInfo<TInfo>(out TInfo info)
        {
            if (_byproducts.TryGetValue(typeof(TInfo), out var infoObj))
            {
                info = (TInfo) infoObj;
                return true;
            }
            info = default(TInfo);
            return false;
        }

        /// <summary>
        /// Reset the byproduct cache between candidates. The context instance is
        /// reused, so this is called once per candidate before its calculators run.
        /// </summary>
        public void ClearByproducts()
        {
            _byproducts.Clear();
        }
    }
}
