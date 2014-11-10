/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;

namespace pwiz.Skyline.Model.Results
{
    public struct PrecursorTextId
    {
        public PrecursorTextId(double precursorMz, string textId, ChromExtractor extractor) : this()
        {
            PrecursorMz = precursorMz;
            TextId = textId;
            Extractor = extractor;
        }

        public double PrecursorMz { get; private set; }
        public string TextId { get; private set; }  // Peptide Modifed Sequence or custom ion ID
        public ChromExtractor Extractor { get; private set; }

        #region object overrides

        public bool Equals(PrecursorTextId other)
        {
            return PrecursorMz.Equals(other.PrecursorMz) &&
                string.Equals(TextId, other.TextId) &&
                Extractor == other.Extractor;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is PrecursorTextId && Equals((PrecursorTextId) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = PrecursorMz.GetHashCode();
                hashCode = (hashCode*397) ^ (TextId != null ? TextId.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (int) Extractor;
                return hashCode;
            }
        }

        private sealed class PrecursorMzTextIdComparer : IComparer<PrecursorTextId>
        {
            public int Compare(PrecursorTextId x, PrecursorTextId y)
            {
                int c = Comparer.Default.Compare(x.PrecursorMz, y.PrecursorMz);
                if (c != 0)
                    return c;
                c = string.CompareOrdinal(x.TextId, y.TextId);
                if (c != 0)
                    return c;
                return x.Extractor - y.Extractor;
            }
        }

        private static readonly IComparer<PrecursorTextId> PRECURSOR_TEXT_ID_COMPARER_INSTANCE = new PrecursorMzTextIdComparer();

        public static IComparer<PrecursorTextId> PrecursorTextIdComparerInstance
        {
            get { return PRECURSOR_TEXT_ID_COMPARER_INSTANCE; }
        }

        #endregion
    }
}