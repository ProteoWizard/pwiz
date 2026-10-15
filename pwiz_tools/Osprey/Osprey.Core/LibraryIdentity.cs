/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Finds a library entry by its <see cref="LibraryEntry.Id"/> in constant time.
    ///
    /// <para>A score row's charge, decoy flag and peptide are all properties of the library
    /// entry its <c>entry_id</c> names, so a reader that already holds the library can take
    /// them from here instead of decoding them from every row of a score file. Base ids are
    /// dense (deduplication numbers entries by position and a decoy carries its target's id
    /// with <see cref="LibraryEntry.DECOY_ID_BIT"/> set), so two arrays indexed by base id
    /// serve every lookup without a dictionary.</para>
    /// </summary>
    public sealed class LibraryIdentity
    {
        private const uint BASE_ID_MASK = ~LibraryEntry.DECOY_ID_BIT;

        private readonly IReadOnlyList<LibraryEntry> _library;
        // Position in _library plus one, by base id; 0 where the library has no such entry.
        private readonly int[] _targetPosition;
        private readonly int[] _decoyPosition;

        public LibraryIdentity(IReadOnlyList<LibraryEntry> library)
        {
            _library = library;
            uint maxBaseId = 0;
            foreach (var entry in library)
            {
                uint baseId = entry.Id & BASE_ID_MASK;
                if (baseId > maxBaseId)
                    maxBaseId = baseId;
            }
            _targetPosition = new int[maxBaseId + 1];
            _decoyPosition = new int[maxBaseId + 1];
            for (int i = 0; i < library.Count; i++)
            {
                uint id = library[i].Id;
                var positions = (id & LibraryEntry.DECOY_ID_BIT) != 0 ? _decoyPosition : _targetPosition;
                positions[id & BASE_ID_MASK] = i + 1;
            }
        }

        /// <summary>
        /// The library entry with exactly this id (decoy bit included), or null.
        /// </summary>
        public LibraryEntry Find(uint entryId)
        {
            uint baseId = entryId & BASE_ID_MASK;
            if (baseId >= _targetPosition.Length)
                return null;
            int position = (entryId & LibraryEntry.DECOY_ID_BIT) != 0
                ? _decoyPosition[baseId]
                : _targetPosition[baseId];
            if (position == 0)
                return null;
            var entry = _library[position - 1];
            return entry.Id == entryId ? entry : null;
        }
    }
}
