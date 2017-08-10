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
using pwiz.Skyline.Util;

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
        public ViewLibraryPepInfo(LibKey key, ICollection<byte> lookupPool)
            : this()
        {
            Key = key;
            IndexLookup = lookupPool.Count;
            foreach (byte aa in key.SequenceLookupBytes)
                lookupPool.Add(aa);

            // Order extra bytes so that a byte-by-byte comparison of the
            // lookup bytes will order correctly.

            ChargeInfoLength = Key.IsSmallMoleculeKey ?
                key.SequenceStart - 1 : // Length of adduct description, plus charge byte
                1;
            lookupPool.Add((byte) key.Charge); // Charge, then adduct description if any
            for (var i = 1; i < ChargeInfoLength; i++)
                lookupPool.Add(key.Key[i-1]);
            // Store small molecule info, if any
            if (Key.IsSmallMoleculeKey)
            {
                var smallMolBytes = SmallMoleculeLibraryAttributes.ToBytes(key.SmallMoleculeLibraryAttributes);
                SmallMolInfoLength = smallMolBytes.Length;
                for (var j = 0; j < SmallMolInfoLength; j++)
                {
                    lookupPool.Add(smallMolBytes[j]);
                }
            }
            int countMods = key.ModificationCount;
            lookupPool.Add((byte)(countMods & 0xFF));
            lookupPool.Add((byte)((countMods >> 8) & 0xFF)); // probably never non-zero, but to be safe
            LengthLookup = lookupPool.Count - IndexLookup;
        }

        public LibKey Key { get; private set; }
        private int IndexLookup { get; set; }
        private int LengthLookup { get; set; }
        private int ChargeInfoLength { get; set; }
        private int SmallMolInfoLength { get; set; }

        public PeptideDocNode PeptideNode { get; set; }

        /// <summary>
        /// The modified peptide string and charge indicator for a spectrum,
        /// e.g. PEPT[+80]IDER++
        /// </summary>
        public string DisplayString 
        {
            get
            {
                // Get human-friendly name if any
                if (Key.IsSmallMoleculeKey)
                {
                    var name = Key.SmallMoleculeLibraryAttributes.MoleculeName;
                    if (!string.IsNullOrEmpty(name))
                    {
                        return name + Transition.GetChargeIndicator(Adduct);
                    }
                }
                return Key.ToString();
            } 
        }

        /// <summary>
        /// The charge state of the peptide matched to a spectrum
        /// </summary>
        public Adduct Adduct { get { return Key.Adduct; } }

        /// <summary>
        /// The modified peptide sequence associated with a spectrum
        /// </summary>
        public Target Target { get { return Key.Target; } }

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
        /// Unmodified length of the peptide sequence or moleculeID for a spectrum
        /// </summary>
        private int SequenceLength { get { return LengthLookup - (SmallMolInfoLength + ChargeInfoLength + 2); } } // 2 bytes of mod count

        public SmallMoleculeLibraryAttributes GetSmallMoleculeLibraryAttributes(byte[] lookupPool)
        {
            return Key.IsSmallMoleculeKey ? 
                SmallMoleculeLibraryAttributes.FromBytes(lookupPool, IndexLookup + SequenceLength + ChargeInfoLength) :
                SmallMoleculeLibraryAttributes.EMPTY;
        }

        /// <summary>
        /// The moleculeID or plain peptide string, with modifications removed for a spectrum
        /// </summary>
        public string GetAASequence(byte[] lookupPool)
        {
            return Encoding.UTF8.GetString(lookupPool, IndexLookup, SequenceLength);
        }

        /// <summary>
        /// The moleculeID or plain peptide string, with modifications removed, and charge indicator
        /// for a spectrum
        /// </summary>
        public string GetPlainDisplayString(byte[] lookupPool)
        {
            return (HasPeptide ? PeptideNode.Peptide.Target.DisplayName : GetAASequence(lookupPool)) +
                Transition.GetChargeIndicator(Adduct);
        }

        /// <summary>
        /// Gets the ViewLibraryPepInfo sequence without the modification characters, with a long-form
        /// charge state inditcator that sorts like pepInfo sort order.
        /// </summary>
        public string GetPepInfoComparisonString(byte[] lookupPool)
        {
            return !Key.IsPrecursorKey
                ? GetAASequence(lookupPool) + Adduct.AsFormulaOrSigns()
                : DisplayString;
        }

        public static int Compare(ViewLibraryPepInfo p1, ViewLibraryPepInfo p2, byte[] lookupPool)
        {
            // If they point to the same look-up index, then they are equal.
            if (p1.IndexLookup == p2.IndexLookup)
                return 0;

            if (p1.Key.IsSmallMoleculeKey)
            {
                return p1.Compare(p2.DisplayString, lookupPool); // Deal with more complex sort order of adducts etc
            }

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

        // Compares the display string minus the modification characters for 
        // the given peptide with the string passed in.
        public int Compare(string s, byte[] lookupPool)
        {
            var pepInfoComparisonString = GetPepInfoComparisonString(lookupPool);
            var simpleCompareResult = string.Compare(pepInfoComparisonString, 0, s, 0, s.Length, StringComparison.OrdinalIgnoreCase);
            if (simpleCompareResult == 0 || !Key.IsSmallMoleculeKey)
            {
                return simpleCompareResult;
            }
            // Comparison for small molecules is a bit more complex because adduct text descriptions don't sort the same way adducts do - eg M+H M+2H alphasort as M+2H M+H
            // But comparing adducts is expensive, so try to compare the non-adduct (text up to "[M+...") portions first
            var adductStart = s.LastIndexOf("[", StringComparison.Ordinal); // Not L10N
            if (adductStart < 0) // No obvious adduct component, just do a string comparison
            {
                return string.Compare(pepInfoComparisonString, 0, s, 0, s.Length, StringComparison.OrdinalIgnoreCase);
            }
            var pepAdductStart = pepInfoComparisonString.LastIndexOf("[", StringComparison.Ordinal); // Not L10N
            if (pepAdductStart != adductStart) // Must be a difference in the non-adduct portions, just do a string comparison
            {
                return string.Compare(pepInfoComparisonString, 0, s, 0, s.Length, StringComparison.OrdinalIgnoreCase);
            }
            var result = string.Compare(pepInfoComparisonString, 0, s, 0, adductStart, StringComparison.OrdinalIgnoreCase); // Compare non-adduct portion
            if (result != 0)
            {
                return result; // Non-adduct portion disagrees
            }
            Adduct adduct; // Now the more expensive adduct comparison
            return !Adduct.TryParse(s.Substring(adductStart), out adduct) ? string.Compare(pepInfoComparisonString, 0, s, 0, s.Length, StringComparison.OrdinalIgnoreCase) : Adduct.CompareTo(adduct);
        }

    }
}
