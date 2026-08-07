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
using System.Collections;
using System.Collections.Generic;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Represents a single entry in a spectral library.
    /// Maps to osprey-core/src/types.rs in the Rust implementation.
    /// </summary>
    public class LibraryEntry
    {
        /// <summary>
        /// High bit of <see cref="Id"/> marking a decoy entry. The
        /// <c>base_id = Id &amp; 0x7FFFFFFF</c> convention pairs a target
        /// with its decoy. Generated decoys (<c>DecoyGenerator</c>) inherit
        /// this from the target; library-supplied decoys get the bit set
        /// during post-load marking when they match a configured prefix.
        /// Maps to Rust <c>osprey_core::types::DECOY_ID_BIT</c>.
        /// </summary>
        public const uint DECOY_ID_BIT = 0x80000000u;

        /// <summary>Backing value for <see cref="ReleaseSpectrum"/>; see there for why it
        /// throws rather than being empty. Private - no caller needs to know it exists.</summary>
        private static readonly IReadOnlyList<LibraryFragment> RELEASED_SPECTRUM =
            new ReleasedFragmentList();

        public uint Id { get; set; }
        public string Sequence { get; set; }
        public string ModifiedSequence { get; set; }
        public IReadOnlyList<Modification> Modifications { get; set; }
        public byte Charge { get; set; }
        public double PrecursorMz { get; set; }
        public double RetentionTime { get; set; }
        public bool RtCalibrated { get; set; }
        public IReadOnlyList<LibraryFragment> Fragments { get; set; }
        public IReadOnlyList<string> ProteinIds { get; set; }
        public IReadOnlyList<string> GeneNames { get; set; }
        public bool IsDecoy { get; set; }

        public LibraryEntry(uint id, string sequence, string modifiedSequence,
            byte charge, double precursorMz, double retentionTime)
        {
            Id = id;
            Sequence = sequence;
            ModifiedSequence = modifiedSequence;
            Charge = charge;
            PrecursorMz = precursorMz;
            RetentionTime = retentionTime;
            // Default to shared empty arrays. The four collection members are
            // immutable containers (IReadOnlyList) that every producer -- the
            // loaders, DecoyGenerator, tests -- fully REASSIGNS (an interned
            // array, or a build-then-assign list); nothing mutates a member
            // after assignment. Array.Empty caches one zero-length instance per
            // element type, so the millions of entries with no modifications /
            // proteins / genes (a 3.1M-entry HeLa library has ~2.2M unmodified
            // peptides) share it instead of each owning a fresh empty list
            // header + backing array. Values are unchanged, so the regression
            // golden stays byte-identical.
            Modifications = Array.Empty<Modification>();
            Fragments = Array.Empty<LibraryFragment>();
            ProteinIds = Array.Empty<string>();
            GeneNames = Array.Empty<string>();
        }

        /// <summary>
        /// Tests whether this entry should be treated as a decoy based on a
        /// configured prefix list. Returns true if ANY protein accession
        /// starts (case-insensitively) with any of the prefixes.
        ///
        /// Used only when the user has set <c>DecoysInLibrary = true</c>
        /// (or <c>DecoyMethod = FromLibrary</c>). For Osprey-generated
        /// decoys, <see cref="IsDecoy"/> is set directly by
        /// <c>DecoyGenerator</c> and this function is not called.
        ///
        /// Returns false if the prefix list or the entry's protein list is
        /// empty. Empty prefix strings are ignored.
        /// Maps to Rust <c>LibraryEntry::looks_like_library_decoy</c>.
        /// </summary>
        public bool LooksLikeLibraryDecoy(IList<string> prefixes)
        {
            if (prefixes == null || prefixes.Count == 0 ||
                ProteinIds == null || ProteinIds.Count == 0)
                return false;
            for (int i = 0; i < ProteinIds.Count; i++)
            {
                string acc = ProteinIds[i];
                if (acc == null)
                    continue;
                for (int j = 0; j < prefixes.Count; j++)
                {
                    string p = prefixes[j];
                    if (string.IsNullOrEmpty(p))
                        continue;
                    if (acc.Length >= p.Length &&
                        acc.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// True once <see cref="ReleaseSpectrum"/> has dropped this entry's spectrum, and only
        /// then. It exists because inspecting <see cref="Fragments"/> to find out throws by
        /// design, which would otherwise leave the released state write-only - detectable only
        /// by catching an exception.
        ///
        /// <para>This answers "was the spectrum RELEASED", NOT "does this entry have a
        /// spectrum". An entry can legitimately hold no fragments and report false here: the
        /// constructor default and the <c>OmitFragments</c> load arm both leave
        /// <c>Array.Empty</c>, which is a readable empty spectrum rather than a released one.
        /// Asking the has-a-spectrum question is what the ordinary
        /// <c>Fragments == null || Fragments.Count == 0</c> guard is for - and on a released
        /// entry that guard throws, which is the point.</para>
        /// </summary>
        public bool IsSpectrumReleased => ReferenceEquals(Fragments, RELEASED_SPECTRUM);

        /// <summary>
        /// Drop this entry's spectrum, freeing the fragment array, and report whether it was
        /// still held (so a caller can count releases, and a second call is a no-op).
        /// Identity - sequence, protein ids, m/z, RT - is untouched: protein parsimony walks
        /// the whole library after the spectra are gone and reads exactly those fields.
        ///
        /// <para>The released state THROWS on any access to <see cref="Fragments"/>, which is
        /// the point of releasing through a method rather than assigning null or an empty
        /// list. Every scorer guards with
        /// <c>if (entry.Fragments == null || entry.Fragments.Count == 0)</c>, so both of those
        /// are silently absorbed as "this entry has no spectrum" - an entry released in error
        /// would score a degenerate zero instead of failing. Spectra are released only where
        /// nothing is believed to read them; this makes a wrong belief loud, by turning that
        /// same guard expression into a tripwire.</para>
        /// </summary>
        public bool ReleaseSpectrum()
        {
            if (IsSpectrumReleased)
                return false;
            Fragments = RELEASED_SPECTRUM;
            return true;
        }

        /// <summary>
        /// Backing type of <see cref="RELEASED_SPECTRUM"/>: a zero-state list that throws on every
        /// access. Private and instantiated exactly once - callers only ever see the singleton
        /// through <see cref="ReleaseSpectrum"/>, so there is no way to create a second one or
        /// to mistake it for an ordinary empty list.
        /// </summary>
        private sealed class ReleasedFragmentList : IReadOnlyList<LibraryFragment>
        {
            public int Count => throw Released();

            public LibraryFragment this[int index] => throw Released();

            public IEnumerator<LibraryFragment> GetEnumerator()
            {
                throw Released();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                throw Released();
            }

            // The message cannot name WHICH entry was wrongly released, because one shared
            // singleton stands in for every released spectrum - deliberately, since a
            // per-entry sentinel would reintroduce the per-entry allocation the release
            // exists to remove. The stack trace names the READER, which is the half that
            // matters: rerun with OSPREY_RELEASE_LIBRARY_FRAGMENTS=0 to get the entry.
            private static InvalidOperationException Released()
            {
                return new InvalidOperationException(
                    @"These library fragments were released after Stage 5 because the entry is " +
                    @"neither a compaction survivor nor a gap-fill candidate, so nothing should " +
                    @"score or write it. Reaching them means the retained set is wrong. Set " +
                    @"OSPREY_RELEASE_LIBRARY_FRAGMENTS=0 to keep the whole library resident.");
            }
        }
    }
}
