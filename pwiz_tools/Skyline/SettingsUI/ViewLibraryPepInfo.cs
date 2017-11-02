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

using pwiz.Common.SystemUtil;
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
    public class ViewLibraryPepInfo : Immutable
    {
        public ViewLibraryPepInfo(LibKey key)
        {
            Key = key;
            UnmodifiedTargetText = GetUnmodifiedTargetText(key.LibraryKey);
        }

        public LibKey Key { get; private set; }
        public PeptideDocNode PeptideNode { get; private set; }

        public ViewLibraryPepInfo ChangePeptideNode(PeptideDocNode peptideDocNode)
        {
            return ChangeProp(ImClone(this), im => im.PeptideNode = peptideDocNode);
        }

        /// <summary>
        /// The modified peptide string and charge indicator for a spectrum,
        /// e.g. PEPT[+80]IDER++. This is used by test code that wants to select
        /// a particular peptide.
        /// </summary>
        public string AnnotatedDisplayText 
        {
            get
            {
                var peptideKey = Key.LibraryKey as PeptideLibraryKey;
                if (peptideKey != null)
                {
                    return peptideKey.ModifiedSequence + Transition.GetChargeIndicator(peptideKey.Adduct);
                }
                return DisplayText;
            } 
        }

        /// <summary>
        /// Returns the unmodified peptide sequence. This, plus the Adduct is what gets displayed
        /// in the list box.
        /// </summary>
        public string UnmodifiedTargetText { get; private set; }

        public string DisplayText
        {
            get
            {
                return UnmodifiedTargetText + (Key.Adduct.IsEmpty
                           ? string.Empty
                           : Transition.GetChargeIndicator(Key.Adduct));
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

        public SmallMoleculeLibraryAttributes GetSmallMoleculeLibraryAttributes()
        {
            var smallMoleculeKey = Key.LibraryKey as MoleculeLibraryKey;
            if (smallMoleculeKey == null)
            {
                return SmallMoleculeLibraryAttributes.EMPTY;
            }
            return smallMoleculeKey.SmallMoleculeLibraryAttributes;
        }

        /// <summary>
        /// The moleculeID or plain peptide string, with modifications removed, and charge indicator
        /// for a spectrum
        /// </summary>
        public string GetPlainDisplayString()
        {
            return DisplayText;
        }

        public static string GetUnmodifiedTargetText(LibraryKey key)
        {
            var peptideKey = key as PeptideLibraryKey;
            if (peptideKey != null)
            {
                return peptideKey.UnmodifiedSequence;
            }
            var moleculeKey = key as MoleculeLibraryKey;
            if (moleculeKey != null)
            {
                if (!string.IsNullOrEmpty(moleculeKey.SmallMoleculeLibraryAttributes.MoleculeName))
                {
                    return moleculeKey.SmallMoleculeLibraryAttributes.MoleculeName;
                }
                return moleculeKey.ToString();
            }
            return key.ToString();
        }
    }
}
