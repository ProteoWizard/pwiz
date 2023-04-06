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

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib
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
        public ViewLibraryPepInfo(LibKey key, SpectrumHeaderInfo libInfo = null)
        {
            Key = key;
            LibInfo = libInfo;
            UnmodifiedTargetText = GetUnmodifiedTargetText(key.LibraryKey);
            KeyString = key.ToString();
        }

        public LibKey Key { get; private set; }
        public SpectrumHeaderInfo LibInfo { get; private set; } // Provides peptide/protein association when available
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
        /// A dictionary of key names (like HMDB, SMILES) and key values associated with a molecule
        /// </summary>
        public Dictionary<string, string> OtherKeysDict { get; set; }

        /// <summary>
        /// Returns the unmodified peptide sequence. This, plus the Adduct is what gets displayed
        /// in the list box.
        /// </summary>
        public string UnmodifiedTargetText { get; private set; }

        public string KeyString { get; private set; } // Used in sorting, may be expensive to calculate so we cache it here

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
        /// The precursor m/z of the peptide or small molecule associated with a spectrum
        /// </summary>
        public double PrecursorMz { get; set; }

        /// <summary>
        /// Ion mobility value for a spectrum
        /// </summary>
        public double? IonMobility { get; set; }

        /// <summary>
        /// The collision cross section value for a spectrum
        /// </summary>
        public double? CCS { get; set; }

        /// <summary>
        /// The units of the ion mobility value for a spectrum
        /// </summary>
        public string IonMobilityUnits { get; set; }

        public int Charge
        {
            get { return Key.Adduct.AdductCharge; }
        }

        /// <summary>
        /// The charge state of the peptide or molecule matched to a spectrum
        /// </summary>
        public Adduct Adduct { get { return Key.Adduct; } }

        /// <summary>
        /// The modified peptide sequence or small molecule associated with a spectrum
        /// </summary>
        public Target Target { get { return Key.Target; } }

        /// <summary>
        /// True if the peptide sequence associated with a spectrum is contains
        /// modification specifiers, e.g. [+80]
        /// </summary>
        public bool IsModified { get { return Key.IsModified; } }

        /// <summary>
        /// Formula representation of an adduct which is used when filtering by adduct
        /// </summary>
        public string AdductAsFormula
        {
            get { return Key.Adduct.AsFormula(); }
        }

        /// <summary>
        /// International Chemical Identifier Key
        /// </summary>
        public string InchiKey
        {
            get { return Key.SmallMoleculeLibraryAttributes.InChiKey; }
        }

        /// <summary>
        /// The molecular formula associated with a spectrum
        /// </summary>
        public string Formula
        {
            get { return Key.SmallMoleculeLibraryAttributes.ChemicalFormula; }
        }
        /// <summary>
        /// Other Keys such as CAS registry number
        /// </summary>
        public string OtherKeys
        {
            get { return Key.SmallMoleculeLibraryAttributes.OtherKeys; }
        }
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

            var crosslinkKey = key as CrosslinkLibraryKey;
            if (crosslinkKey != null)
            {
                return crosslinkKey.PeptideLibraryKeys.First().UnmodifiedSequence;
            }
            return key.ToString();
        }

        /// <summary>
        /// Get the information necessary to calculate precursor m/z and then calculate it
        /// </summary>
        public double CalcMz(LibKeyModificationMatcher matcher)
        {
            GetPeptideInfo(matcher, out var _settings, out var transitionGroup, out var mods);
            return CalcMz(_settings, transitionGroup, mods);
        }

        /// <summary>
        /// Calculate precursor m/z
        /// </summary>
        public double CalcMz(SrmSettings settings,
            TransitionGroupDocNode transitionGroup, ExplicitMods mods)
        {
            TypedMass massH;
            if (Target != null)
                massH = settings.GetPrecursorCalc(transitionGroup.TransitionGroup.LabelType, mods)
                    .GetPrecursorMass(Target);
            else
                massH = new TypedMass(Key.PrecursorMz ?? 0, MassType.Monoisotopic);
            return SequenceMassCalc.PersistentMZ(SequenceMassCalc.GetMZ(massH, transitionGroup.PrecursorAdduct));
        }

        public void GetPeptideInfo(LibKeyModificationMatcher matcher,
            out SrmSettings settings, out TransitionGroupDocNode transitionGroup, out ExplicitMods mods)
        {
            settings = Program.ActiveDocument.Settings;

            if (matcher.HasMatches)
                settings = settings.ChangePeptideModifications(modifications => matcher.MatcherPepMods);

            var nodePep = PeptideNode;
            if (nodePep != null)
            {
                mods = nodePep.ExplicitMods;
                transitionGroup =
                    nodePep.TransitionGroups.FirstOrDefault(precursor => precursor.PrecursorCharge == Charge);
                transitionGroup ??= nodePep.TransitionGroups.First();
            }
            else if (null != Key.LibraryKey.Target)
            {
                var peptide = Key.LibraryKey.CreatePeptideIdentityObj();
                transitionGroup = new TransitionGroupDocNode(new TransitionGroup(peptide, Adduct,
                    IsotopeLabelType.light, true, null), null);
                if (Key.IsSmallMoleculeKey)
                {
                    mods = null;
                    return;
                }

                // Because the document modifications do not explain this peptide, a set of
                // explicit modifications must be constructed, even if they are empty.
                IList<ExplicitMod> staticModList = new List<ExplicitMod>();
                IEnumerable<ModificationInfo> modList = GetModifications();
                foreach (var modInfo in modList)
                {
                    var aa = modInfo.ModifiedAminoAcid;
                    var smod = new StaticMod(@"temp",
                                             aa != 'X' ? aa.ToString(CultureInfo.InvariantCulture) : string.Join(@",", AminoAcid.All),
                                             null,
                                             null,
                                             LabelAtoms.None,
                                             modInfo.ModifiedMass,
                                             modInfo.ModifiedMass);
                    var exmod = new ExplicitMod(modInfo.IndexMod, smod);
                    staticModList.Add(exmod);
                }

                mods = new ExplicitMods(peptide, staticModList, new TypedExplicitModifications[0]);
            }
            else
            {
                // Create custom ion node for midas library
                var precursor = Key.PrecursorMz.GetValueOrDefault();
                var precursorMono = new TypedMass(precursor, MassType.Monoisotopic);
                var precursorAverage = new TypedMass(precursor, MassType.Average);
                var peptide = new Peptide(new CustomMolecule(precursorMono, precursorAverage, precursor.ToString(CultureInfo.CurrentCulture)));
                transitionGroup = new TransitionGroupDocNode(new TransitionGroup(peptide, Adduct.EMPTY, IsotopeLabelType.light, true, null), null);
                mods = new ExplicitMods(peptide, new ExplicitMod[0], new TypedExplicitModifications[0]);
            }
        }

        /// <summary>
        /// Computes each ModificationInfo for the given peptide and returns a
        /// list of all modifications.
        /// </summary>
        private IEnumerable<ModificationInfo> GetModifications()
        {
            IList<ModificationInfo> modList = new List<ModificationInfo>();
            string sequence;
            if (Key.LibraryKey is PeptideLibraryKey peptideLibraryKey)
            {
                sequence = peptideLibraryKey.ModifiedSequence;
            }
            else
            {
                return modList;
            }
            int iMod = -1;
            for (int i = 0; i < sequence.Length; i++)
            {
                if (sequence[i] != '[')
                {
                    iMod++;
                    continue;
                }
                int iAa = i - 1;
                // Invalid format. Should never happen.
                if (iAa < 0)
                    break;
                i++;
                int signVal = 1;
                if (sequence[i] == '+')
                    i++;
                else if (sequence[i] == '-')
                {
                    i++;
                    signVal = -1;
                }
                int iEnd = sequence.IndexOf(']', i);
                // Invalid format. Should never happen.
                if (iEnd == -1)
                    break;
                // NIST libraries sometimes use [?] when modification cannot be identified
                double massDiff;
                if (double.TryParse(sequence.Substring(i, iEnd - i), NumberStyles.Number, CultureInfo.InvariantCulture, out massDiff))
                {
                    modList.Add(new ModificationInfo(iMod, sequence[iAa], massDiff * signVal));
                }
                i = iEnd;
            }
            return modList;
        }

        /// <summary>
        /// Data structure to store information on a modification for a given
        /// peptide sequence.
        /// </summary>
        public class ModificationInfo
        {
            public int IndexMod { get; private set; }
            public char ModifiedAminoAcid { get; private set; }
            public double ModifiedMass { get; private set; }

            public ModificationInfo(int indexMod, char modifiedAminoAcid, double modifiedMass)
            {
                IndexMod = indexMod;
                ModifiedAminoAcid = modifiedAminoAcid;
                ModifiedMass = modifiedMass;
            }
        }
    }
}
