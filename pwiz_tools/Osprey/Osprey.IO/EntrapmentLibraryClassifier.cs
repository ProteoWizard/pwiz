/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *                Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using pwiz.CarafeSharp.Contracts;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
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
    ///
    /// The one exception is entrapment placed on a real target protein, an audit of
    /// protein-aware scoring: such a peptide carries the target protein's clean
    /// accession, so the pairing (the manifest's <c>peptide_type</c>, the blib's
    /// <c>DecoyPairs.IsEntrapment</c>) sets <see cref="LibraryEntry.IsEntrapment"/>
    /// at load and the entry, not its accessions, is what the class is read from.
    /// </summary>
    public static class EntrapmentLibraryClassifier
    {
        /// <summary>FDRBench default entrapment label embedded in entrapment protein accessions.</summary>
        private const string EntrapmentMarker = @"_p_target";

        /// <summary>FDRBench default decoy prefix on decoy-side protein accessions.</summary>
        private const string DecoyPrefix = @"decoy_";

        /// <summary>
        /// True if <paramref name="entry"/> is entrapment (p_target / p_decoy): flagged by its
        /// pairing, or marked by its accessions. The one test of the entrapment class for every
        /// consumer, since entrapment on a target protein is flagged but not marked.
        /// </summary>
        public static bool IsEntrapment(LibraryEntry entry)
        {
            return entry.IsEntrapment || IsEntrapment(entry.ProteinIds);
        }

        /// <summary>
        /// True if <paramref name="entry"/> is entrapment that sits on an entrapment protein of its
        /// own (the <c>_p_target</c> marker), false for a target or for entrapment placed on a real
        /// target protein. The split the protein-aware audit reads.
        /// </summary>
        public static bool IsOnEntrapmentProtein(LibraryEntry entry)
        {
            return IsEntrapment(entry.ProteinIds);
        }

        /// <summary>
        /// True if every accession is an entrapment protein's: a protein group made only of
        /// entrapment proteins, which a stripped result leaves out.
        /// </summary>
        public static bool IsEntrapmentOnly(IReadOnlyList<string> proteinIds)
        {
            if (proteinIds == null || proteinIds.Count == 0)
                return false;
            for (int i = 0; i < proteinIds.Count; i++)
            {
                var p = proteinIds[i];
                if (p == null || p.IndexOf(EntrapmentMarker, StringComparison.Ordinal) < 0)
                    return false;
            }
            return true;
        }

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

        /// <summary>
        /// True if <paramref name="entry"/> is a decoy by its flag or by its accessions: the one
        /// test of the decoy side for the pairing and its consumers, since a generated decoy is
        /// flagged but carries Osprey's <c>DECOY_</c> prefix, not the manifest's.
        /// </summary>
        public static bool IsDecoySide(LibraryEntry entry)
        {
            return entry.IsDecoy || IsDecoySide(entry.ProteinIds);
        }

        /// <summary>The four-way peptide kind for a library entry, from its flags and its accessions.</summary>
        public static PeptideKind Classify(LibraryEntry entry)
        {
            return Classify(IsDecoySide(entry), IsEntrapment(entry));
        }

        /// <summary>The four-way peptide kind for a library entry, from its accessions.</summary>
        public static PeptideKind Classify(IReadOnlyList<string> proteinIds)
        {
            return Classify(IsDecoySide(proteinIds), IsEntrapment(proteinIds));
        }

        private static PeptideKind Classify(bool decoy, bool entrap)
        {
            if (decoy)
                return entrap ? PeptideKind.PDecoy : PeptideKind.Decoy;
            return entrap ? PeptideKind.PTarget : PeptideKind.Target;
        }
    }
}
