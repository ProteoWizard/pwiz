/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using pwiz.CommonMsData;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// A new peak boundary for one result file, to be applied to whichever precursors of a molecule match
    /// <see cref="Charge"/>. <see cref="MoleculeIntegrator"/> applies these to a <see cref="PeptideDocNode"/>.
    /// </summary>
    public class PeakBoundaryChange
    {
        /// <summary>
        /// Index of the replicate in <see cref="MeasuredResults.Chromatograms"/> that <see cref="FileId"/>
        /// belongs to
        /// </summary>
        public int ReplicateIndex { get; set; }
        public ChromFileInfoId FileId { get; set; }
        public MsDataFileUri FilePath { get; set; }
        /// <summary>
        /// Start of the new peak, or null to remove the peak. Must be null if <see cref="EndTime"/> is null.
        /// </summary>
        public double? StartTime { get; set; }
        public double? EndTime { get; set; }
        /// <summary>
        /// Annotations to add to the precursor result, which may be empty
        /// </summary>
        public Dictionary<string, string> Annotations { get; set; }
        /// <summary>
        /// The charge state or adduct this change applies to. When <see cref="ChargeSpecified"/> is false the
        /// change applies to every precursor of the molecule.
        /// </summary>
        public Adduct Charge { get; set; }
        public bool ChargeSpecified { get; set; }
        /// <summary>
        /// True when the charge was given as a bare integer (e.g. "2") rather than an adduct formula, in
        /// which case precursors are matched on charge value only, not on the exact adduct.
        /// </summary>
        public bool ChargeIsNumeric { get; set; }

        public bool MatchesAdduct(Adduct precursorAdduct)
        {
            if (!ChargeSpecified)
                return true;
            if (ChargeIsNumeric)
                return Charge.AdductCharge == precursorAdduct.AdductCharge;
            return Charge == precursorAdduct;
        }
    }
}
