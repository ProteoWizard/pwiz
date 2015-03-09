/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// Data structure containing information on a single peptide match with
    /// spectral library spectrum. It is mostly a very lightweight wrapper for
    /// the LibKey object.  It now also may contain an associated <see cref="PeptideDocNode"/>
    /// in order to correctly display neutral loss ions, display heavy modifications
    /// correctly, and facilitate adding peptides to a document.
    /// </summary>
    public struct ViewLibraryPepInfo
    {
        private const int PEPTIDE_CHARGE_OFFSET = 3;
        //            private const int PEPTIDE_MODIFICATION_OFFSET_LOWER = 2;
        //            private const int PEPTIDE_MODIFICATION_OFFSET_UPPER = 1;

        public ViewLibraryPepInfo(LibKey key, ICollection<byte> lookupPool)
            : this()
        {
            Key = key;
            IndexLookup = lookupPool.Count;
            foreach (char aa in key.AminoAcids)
                lookupPool.Add((byte)aa);

            // Order extra bytes so that a byte-by-byte comparison of the
            // lookup bytes will order correctly.
            lookupPool.Add((byte)key.Charge);
            int countMods = key.ModificationCount;
            lookupPool.Add((byte)(countMods & 0xFF));
            lookupPool.Add((byte)((countMods >> 8) & 0xFF)); // probably never non-zero, but to be safe
            LengthLookup = lookupPool.Count - IndexLookup;
        }

        public LibKey Key { get; private set; }
        private int IndexLookup { get; set; }
        private int LengthLookup { get; set; }

        public PeptideDocNode PeptideNode { get; set; }

        /// <summary>
        /// The modified peptide string and charge indicator for a spectrum,
        /// e.g. PEPT[+80]IDER++
        /// </summary>
        public string DisplayString { get { return Key.ToString(); } }

        /// <summary>
        /// The charge state of the peptide matched to a spectrum
        /// </summary>
        public int Charge { get { return Key.Charge; } }

        /// <summary>
        /// The modified peptide sequence associated with a spectrum
        /// </summary>
        public string Sequence { get { return Key.Sequence; } }

        /// <summary>
        /// True if the peptide sequence associated with a spectrum is contains
        /// modification specifiers, e.g. [+80]
        /// </summary>
        public bool IsModified { get { return Key.IsModified; } }

        /// <summary>
        /// True if a <see cref="PeptideDocNode"/> has been successfully associated
        /// with this entry
        /// </summary>
        public bool HasPeptide { get { return PeptideNode != null; }}

        /// <summary>
        /// Unmodified length of the peptide sequence for a spectrum
        /// </summary>
        private int SequenceLength { get { return LengthLookup - PEPTIDE_CHARGE_OFFSET; } }

        /// <summary>
        /// The plain peptide string, with modifications removed for a spectrum
        /// </summary>
        public string GetAASequence(byte[] lookupPool)
        {
            return Encoding.UTF8.GetString(lookupPool, IndexLookup, SequenceLength);
        }

        /// <summary>
        /// The plain peptide string, with modifications removed, and charge indicator
        /// for a spectrum
        /// </summary>
        public string GetPlainDisplayString(byte[] lookupPool)
        {
            return (HasPeptide ? PeptideNode.Peptide.Sequence : GetAASequence(lookupPool)) +
                Transition.GetChargeIndicator(Charge);
        }

        public static int Compare(ViewLibraryPepInfo p1, ViewLibraryPepInfo p2, IList<byte> lookupPool)
        {
            // If they point to the same look-up index, then they are equal.
            if (p1.IndexLookup == p2.IndexLookup)
                return 0;

            int lenP1 = p1.SequenceLength;
            int lenP2 = p2.SequenceLength;
            // If sequences are equal length compare charge and mod count also
            int lenCompare = lenP1 == lenP2 ? p1.LengthLookup : Math.Min(lenP1, lenP2);
            // Compare bytes in the lookup pool
            for (int i = 0; i < lenCompare; i++)
            {
                byte b1 = lookupPool[p1.IndexLookup + i];
                byte b2 = lookupPool[p2.IndexLookup + i];
                // If unequal bytes are found, compare the bytes
                if (b1 != b2)
                    return b1 - b2;
            }

            // If sequence length is not equal, the shorter should be first.
            if (lenP1 != lenP2)
                return lenP1 - lenP2;

            // p1 and p2 have the same unmodified sequence, same number
            // of charges, and same number of modifications. Just 
            // compare their display strings directly in this case.
            return Comparer.Default.Compare(p1.DisplayString, p2.DisplayString);
        }
    }
}
