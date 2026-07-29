/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Classifies a searched library entry as target / entrapment / decoy /
    /// p_decoy directly from its protein accessions -- the single source of
    /// truth for the run, since it is the library Osprey actually searched.
    ///
    /// FDRBench-style entrapment libraries (as produced by carafe / the
    /// entrapment-database step) encode the class in the protein accession:
    /// a <c>decoy_</c> prefix marks the decoy side, and a <c>_p_target</c>
    /// fragment marks entrapment. The four members of a quartet -- target, its
    /// entrapment (a shuffle of the target), its decoy, and its p_decoy -- all
    /// carry these markers, so the class is fully recoverable without the
    /// separate pairing manifest.
    ///
    /// (The manifest's per-peptide pairing token, <c>_pepNNNNN</c>, is stripped
    /// from the accession when the manifest's clean proteins are substituted into
    /// the library for protein parsimony, so entrapment-to-target PAIRING is not
    /// recoverable here and still comes from the manifest; the CLASS is.)
    /// </summary>
    internal static class EntrapmentLibraryClassifier
    {
        /// <summary>FDRBench default entrapment label embedded in entrapment protein accessions.</summary>
        private const string EntrapmentMarker = @"_p_target";

        /// <summary>FDRBench default decoy prefix on decoy-side protein accessions.</summary>
        private const string DecoyPrefix = @"decoy_";

        /// <summary>True if any accession marks this as an entrapment (p_target / p_decoy) sequence.</summary>
        public static bool IsEntrapment(IReadOnlyList<string> proteinIds)
        {
            if (proteinIds == null)
                return false;
            for (int i = 0; i < proteinIds.Count; i++)
            {
                var p = proteinIds[i];
                if (p != null && p.IndexOf(EntrapmentMarker, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>True if any accession marks this as a decoy-side (decoy / p_decoy) sequence.</summary>
        public static bool IsDecoySide(IReadOnlyList<string> proteinIds)
        {
            if (proteinIds == null)
                return false;
            for (int i = 0; i < proteinIds.Count; i++)
            {
                var p = proteinIds[i];
                if (p != null && p.StartsWith(DecoyPrefix, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>The four-way peptide kind for a library entry, from its accessions.</summary>
        public static PeptideKind Classify(IReadOnlyList<string> proteinIds)
        {
            bool decoy = IsDecoySide(proteinIds);
            bool entrap = IsEntrapment(proteinIds);
            if (decoy)
                return entrap ? PeptideKind.PDecoy : PeptideKind.Decoy;
            return entrap ? PeptideKind.PTarget : PeptideKind.Target;
        }
    }
}
