/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
// ReSharper disable InconsistentNaming
    public enum ModTerminus { C, N };  // Not L10N

    [Flags]
    public enum LabelAtoms // Not L10N
    {
        None = 0,
        C13 = 0x1,
        N15 = 0x2,
        O18 = 0x4,
        H2 = 0x8
    }

    public enum RelativeRT { Matching, Overlapping, Preceding, Unknown }

    public static class RelativeRTExtension
    {
        private static string[] LOCALIZED_VALUES
        {
            get
            {
                return new[]
                {
                    Resources.RelativeRTExtension_LOCALIZED_VALUES_Matching,
                    Resources.RelativeRTExtension_LOCALIZED_VALUES_Overlapping,
                    Resources.RelativeRTExtension_LOCALIZED_VALUES_Preceding,
                    Resources.RelativeRTExtension_LOCALIZED_VALUES_Unknown
                };
            }
        }

        public static string GetLocalizedString(this RelativeRT val)
        {
            return LOCALIZED_VALUES[(int) val];
        }

        public static RelativeRT GetEnum(string enumValue)
        {
            return Helpers.EnumFromLocalizedString<RelativeRT>(enumValue, LOCALIZED_VALUES);
        }

        public static RelativeRT GetEnum(string enumValue, RelativeRT defaultEnum)
        {
            return Helpers.EnumFromLocalizedString(enumValue, LOCALIZED_VALUES, defaultEnum);
        }

    }

// ReSharper restore InconsistentNaming

    /// <summary>
    /// Represents a document-wide  or explicit static modification that applies
    /// to all amino acids of a specific type or a single amino acid, or all peptides in the
    /// case of C-terminal or N-terminal modifications.
    /// </summary>
    [XmlRoot("static_modification")]
    public sealed class StaticMod : XmlNamedElement
    {
        private ImmutableList<FragmentLoss> _losses;

        public StaticMod(string name, string aas, ModTerminus? term, string formula)
            : this(name, aas, term, formula, LabelAtoms.None, null, null)
        {            
        }

        public StaticMod(string name, string aas, ModTerminus? term, LabelAtoms labelAtoms)
            : this(name, aas, term, null, labelAtoms, null, null)
        {
        }

        public StaticMod(string name, string aas, ModTerminus? term,
            string formula, LabelAtoms labelAtoms, double? monoMass, double? avgMass)
            : this(name, aas, term, false, formula, labelAtoms, RelativeRT.Matching, monoMass, avgMass, null, null, null)
        {
            
        }

        public StaticMod(string name, string aas, ModTerminus? term, bool isVariable, string formula,
                         LabelAtoms labelAtoms, RelativeRT relativeRT, double? monoMass, double? avgMass, IList<FragmentLoss> losses)
            : this(name, aas, term, isVariable, formula, labelAtoms, relativeRT, monoMass, avgMass, losses, null, null)
        {
            
        }

        public StaticMod(string name,
                         string aas,
                         ModTerminus? term,
                         bool isVariable,
                         string formula,
                         LabelAtoms labelAtoms,
                         RelativeRT relativeRT,
                         double? monoMass,
                         double? avgMass,
                         IList<FragmentLoss> losses,
                         int? uniModId,
                         string shortName)
            : base(name)
        {
            AAs = aas;
            Terminus = term;
            IsVariable = IsExplicit = isVariable;   // All variable mods are explicit
            Formula = formula;
            LabelAtoms = labelAtoms;
            RelativeRT = relativeRT;
            MonoisotopicMass = monoMass;
            AverageMass = avgMass;                

            Losses = losses;

            UnimodId = uniModId;
            ShortName = shortName;

            Validate();
        }

        public string AAs { get; private set; }

        public ModTerminus? Terminus { get; private set; }

        public bool IsVariable { get; private set; }

        public string Formula { get; private set; }
        public double? MonoisotopicMass { get; private set; }
        public double? AverageMass { get; private set; }

        public LabelAtoms LabelAtoms { get; private set; }
        public bool Label13C { get { return (LabelAtoms & LabelAtoms.C13) != 0; } }
        public bool Label15N { get { return (LabelAtoms & LabelAtoms.N15) != 0; } }
        public bool Label18O { get { return (LabelAtoms & LabelAtoms.O18) != 0; } }
        public bool Label2H { get { return (LabelAtoms & LabelAtoms.H2) != 0; } }
        public RelativeRT RelativeRT { get; private set; }

        public IList<FragmentLoss> Losses
        {
            get { return _losses; }
            private set
            {
                _losses = (value != null ? MakeReadOnly(FragmentLoss.SortByMz(value)) : null);
            }
        }

        public IEnumerable<char> AminoAcids
        {
            get
            {
                foreach (var aaPart in AAs.Split(','))
                    yield return aaPart.Trim()[0];
            }
        }

        public int? UnimodId { get; private set; }

        /// <summary>
        /// True if the modification must be declared on a peptide to have
        /// any effect.  Modifications on the document settings with this
        /// value off apply to all peptides without explicit modifications.
        /// <para>
        /// This value is always off for explicit modifications in the
        /// document tree, where it is not necessary, in order to support
        /// reliable equality checks.</para>
        /// </summary>
        public bool IsExplicit { get; private set; }

        /// <summary>
        /// Because variable modifications are now also set as "explicit",
        /// the IsUserSet field was added to destinguish between explict
        /// variable modification and explicit user set modifications.
        /// </summary>
        public bool IsUserSet { get { return IsExplicit && !IsVariable; } }

        public bool IsMod(string sequence)
        {
            if (sequence == null) // As when looking at a non-peptide molecule
                return false;

            for (int i = 0; i < sequence.Length; i++)
            {
                if (IsMod(sequence[i], i, sequence.Length))
                    return true;
            }
            return false;
        }

        public bool IsMod(char aa, int indexAA, int len)
        {
            return IsApplicable(aa, indexAA, len) && HasMod;
        }

        /// <summary>
        /// True if this modification impacts the precursor mass value.
        /// </summary>
        public bool HasMod
        {
            get { return (Formula != null || LabelAtoms != LabelAtoms.None || MonoisotopicMass.HasValue); }
        }

        public bool IsLoss(char aa, int indexAA, int len)
        {
            return IsApplicable(aa, indexAA, len) && HasLoss;
        }

        private bool IsApplicable(char aa, int indexAA, int len)
        {
            if (AAs != null && !AAs.Contains(aa))
                return false;
            if (Terminus.HasValue)
            {
                if (Terminus.Value == ModTerminus.N && indexAA != 0)
                    return false;
                if (Terminus.Value == ModTerminus.C && indexAA != len - 1)
                    return false;                
            }
            return true;
        }

        /// <summary>
        /// True if this modification impacts fragment mass values different
        /// from the precursor modification through neutral loss.
        /// </summary>
        public bool HasLoss
        {
            get { return Losses != null; }
        }

        public string ShortName { get; private set; }

        #region Property change methods

        public StaticMod ChangeExplicit(bool prop)
        {
            return ChangeProp(ImClone(this), im =>
                                                 {
                                                     im.IsExplicit = prop;
                                                     im.IsVariable = false;
                                                 });
        }

        public StaticMod ChangeVariable(bool prop)
        {
            return ChangeProp(ImClone(this), im => im.IsVariable = im.IsExplicit = prop);
        }

        public StaticMod ChangeFormula(string prop)
        {
            return ChangeProp(ImClone(this), im => im.Formula = prop);
        }

        public StaticMod ChangeLabelAtoms(LabelAtoms prop)
        {
            return ChangeProp(ImClone(this), im => im.LabelAtoms = prop);
        }

        public StaticMod ChangeRelativeRT(RelativeRT prop)
        {
            return ChangeProp(ImClone(this), im => im.RelativeRT = prop);
        }

        public StaticMod ChangeLosses(IList<FragmentLoss> prop)
        {
            return ChangeProp(ImClone(this), im => im.Losses = prop);
        }

        public StaticMod MatchVariableAndLossInclusion(StaticMod mod)
        {
            // Only allowed if the mods are equivalent
            Assume.IsTrue(Equivalent(mod));

            var result = this;
            if (!Equals(result.IsVariable, mod.IsVariable))
                result = result.ChangeVariable(mod.IsVariable);
            if (result.Losses != null && !ArrayUtil.EqualsDeep(result.Losses, mod.Losses))
            {
                int len = result.Losses.Count;
                var newLosses = new FragmentLoss[len];
                for (int i = 0; i < len; i++)
                {
                    newLosses[i] = result.Losses[i].ChangeInclusion(mod.Losses[i].Inclusion);
                }
                result = result.ChangeLosses(newLosses);
            }
            return result;
        }

        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private StaticMod()
        {
        }

        private enum ATTR // Not L10N
        {
            aminoacid,
            terminus,
            variable,
            formula,
// ReSharper disable InconsistentNaming
            label_13C,
            label_15N,
            label_18O,
            label_2H,
// ReSharper restore InconsistentNaming
            relative_rt,
            massdiff_monoisotopic,
            massdiff_average,
            explicit_decl, 
            unimod_id,
            short_name
        }

        private void Validate()
        {
            // It is now valid to specify modifications that apply to every amino acid.
            // This is important for 15N labeling, and reasonable for an explicit
            // static modification... but not for variable modifications.
            if (IsVariable && !Terminus.HasValue && AAs == null)
                throw new InvalidDataException(Resources.StaticMod_Validate_Variable_modifications_must_specify_amino_acid_or_terminus);
            if (AAs != null)
            {
                foreach (string aaPart in AAs.Split(',')) // Not L10N
                {
                    string aa = aaPart.Trim();
                    if (aa.Length != 1 || !AminoAcid.IsAA(aa[0]))
                        throw new InvalidDataException(string.Format(Resources.StaticMod_Validate_Invalid_amino_acid___0___, aa));
                }
            }
            else if (Terminus.HasValue && LabelAtoms != LabelAtoms.None)
            {
                throw new InvalidDataException(Resources.StaticMod_Validate_Terminal_modification_with_labeled_atoms_not_allowed);
            }
            if (Formula == null && LabelAtoms == LabelAtoms.None)
            {
                if (MonoisotopicMass == null || AverageMass == null)
                {
                    // Allow a modification that just specifies potential neutral losses
                    // from unmodified amino acid residues.
                    if (Losses == null)
                        throw new InvalidDataException(Resources.StaticMod_Validate_Modification_must_specify_a_formula_labeled_atoms_or_valid_monoisotopic_and_average_masses);
                    if (IsVariable)
                        throw new InvalidDataException(Resources.StaticMod_Validate_Loss_only_modifications_may_not_be_variable);
                    if (IsExplicit)
                        throw new InvalidDataException(Resources.StaticMod_Validate_Loss_only_modifications_may_not_be_explicit);
                }
            }
            else
            {
                // No explicit masses with formula or label atoms
                if (MonoisotopicMass != null || AverageMass != null)
                    throw new InvalidDataException(Resources.StaticMod_Validate_Modification_with_a_formula_may_not_specify_modification_masses);
                if (Formula != null)
                {
                    if (string.IsNullOrEmpty(Formula))
                        throw new InvalidDataException(Resources.StaticMod_Validate_Modification_formula_may_not_be_empty);
                    if (LabelAtoms != LabelAtoms.None)
                        throw new InvalidDataException(Resources.StaticMod_Validate_Formula_not_allowed_with_labeled_atoms);
                    // Cache mass values to improve performance of variable modifications
                    // Throws an exception, if given an invalid formula.
                    MonoisotopicMass = SequenceMassCalc.ParseModMass(BioMassCalc.MONOISOTOPIC, Formula);
                    AverageMass = SequenceMassCalc.ParseModMass(BioMassCalc.AVERAGE, Formula);
                }
            }

            // We no longer validate or guarantee that the Unimod ID is correct.  Previously, we checked the ID and
            // removed it if we didn't think it was correct, but that strategy is of dubious value.  Maybe someone
            // knows better than we do.
            //if (!UniMod.ValidateID(this))
            //    UnimodId = null;
        }

        private static ModTerminus ToModTerminus(String value)
        {
            try
            {
                return (ModTerminus)Enum.Parse(typeof(ModTerminus), value, true);
            }
            catch (ArgumentException)
            {
                throw new ArgumentException(string.Format(Resources.StaticMod_ToModTerminus_Invalid_terminus__0__, value));
            }            
        }

        public static StaticMod Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new StaticMod());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            string aas = reader.GetAttribute(ATTR.aminoacid);
            if (!string.IsNullOrEmpty(aas))
            {                
                AAs = aas;
                // Support v0.1 format.
                if (AAs[0] == '\0') // Not L10N
                    AAs = null;
            }

            Terminus = reader.GetAttribute(ATTR.terminus, ToModTerminus);
            IsVariable = IsExplicit = reader.GetBoolAttribute(ATTR.variable);
            Formula = reader.GetAttribute(ATTR.formula);
            if (reader.GetBoolAttribute(ATTR.label_13C))
                LabelAtoms |= LabelAtoms.C13;
            if (reader.GetBoolAttribute(ATTR.label_15N))
                LabelAtoms |= LabelAtoms.N15;
            if (reader.GetBoolAttribute(ATTR.label_18O))
                LabelAtoms |= LabelAtoms.O18;
            if (reader.GetBoolAttribute(ATTR.label_2H))
                LabelAtoms |= LabelAtoms.H2;
            RelativeRT = reader.GetEnumAttribute(ATTR.relative_rt, RelativeRT.Matching);

            // Allow specific masses always, but they will generate an error,
            // in Validate() if there is already a formula.
            MonoisotopicMass = reader.GetNullableDoubleAttribute(ATTR.massdiff_monoisotopic);
            AverageMass = reader.GetNullableDoubleAttribute(ATTR.massdiff_average);

            if (!IsVariable)
                IsExplicit = reader.GetBoolAttribute(ATTR.explicit_decl);

            UnimodId = reader.GetNullableIntAttribute(ATTR.unimod_id);
            // Backward compatibility with early code that assigned -1 to some custom modifications
            if (UnimodId.HasValue && UnimodId.Value == -1)
                UnimodId = null;

            ShortName = reader.GetAttribute(ATTR.short_name);

            // Consume tag
            reader.Read();

            var listLosses = new List<FragmentLoss>();
            reader.ReadElements(listLosses);
            if (listLosses.Count > 0)
            {
                Losses = listLosses.ToArray();
                reader.ReadEndElement();
            }

            Validate();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttributeIfString(ATTR.aminoacid, AAs);
            writer.WriteAttributeNullable(ATTR.terminus, Terminus);
            writer.WriteAttribute(ATTR.variable, IsVariable);
            writer.WriteAttributeIfString(ATTR.formula, Formula);
            writer.WriteAttribute(ATTR.label_13C, Label13C);
            writer.WriteAttribute(ATTR.label_15N, Label15N);
            writer.WriteAttribute(ATTR.label_18O, Label18O);
            writer.WriteAttribute(ATTR.label_2H, Label2H);
            writer.WriteAttribute(ATTR.relative_rt, RelativeRT, RelativeRT.Matching);
            if (string.IsNullOrEmpty(Formula))
            {
                writer.WriteAttributeNullable(ATTR.massdiff_monoisotopic, MonoisotopicMass);
                writer.WriteAttributeNullable(ATTR.massdiff_average, AverageMass);
            }
            if (!IsVariable)
                writer.WriteAttribute(ATTR.explicit_decl, IsExplicit);

            writer.WriteAttributeNullable(ATTR.unimod_id, UnimodId);

            writer.WriteAttributeIfString(ATTR.short_name, ShortName);

            if (Losses != null)
                writer.WriteElements(Losses);
        }

        #endregion

        #region object overrides

        /// <summary>
        /// Equality minus the <see cref="IsExplicit"/> flag.
        /// </summary>
        public bool EquivalentAll(StaticMod obj)
        {
            return Equivalent(obj) 
                && base.Equals(obj) 
                && obj.IsVariable.Equals(IsVariable) 
                && Equals(obj.UnimodId, UnimodId)
                && Equals(obj.ShortName, ShortName)
                && ArrayUtil.EqualsDeep(obj._losses, _losses);
        }

        /// <summary>
        /// Equality minus <see cref="IsExplicit"/>, <see cref="UnimodId"/>, <see cref="IsVariable"/> and <see cref="XmlNamedElement.Equals(object)"/>.
        /// Used checking for matches between user defined modifications and UniMod modifications.
        /// </summary>
        public bool Equivalent(StaticMod obj)
        {
            if (!Equals(obj.AAs, AAs) ||
                !obj.Terminus.Equals(Terminus) ||
                !obj.AverageMass.Equals(AverageMass) ||
                !obj.MonoisotopicMass.Equals(MonoisotopicMass) ||
                !Equals(obj.RelativeRT, RelativeRT))
            {
                return false;
            }

            if (!ArrayUtil.EqualsDeep(obj._losses, _losses))
            {
                if (obj._losses != null && _losses != null)
                {
                    if (obj._losses.Count != _losses.Count)
                        return false;

                    var losses1 = _losses.OrderBy(l => l.MonoisotopicMass).ToArray();
                    var losses2 = obj._losses.OrderBy(l => l.MonoisotopicMass).ToArray();
                    if (losses1.Where((t, i) => !EquivalentFormulas(t, losses2[i])).Any())
                    {
                        return false;
                    }
                }
                else if (obj._losses == null || _losses == null)
                {
                    return false;
                }
            }

            if (AAs != null)
            {
                foreach (var aa in AminoAcids)
                {
                    if (!EquivalentFormulas(aa, obj))
                        return false;
                }                
            }
            else if (Terminus != null)
            {
                return EquivalentFormulas('\0', obj); // Not L10N
            }
            else
            {
                // Label all amino acids with this label
                for (char aa = 'A'; aa <= 'Z'; aa++) // Not L10N
                {
                    if (AminoAcid.IsAA(aa) && !EquivalentFormulas(aa, obj))
                        return false;
                }
            }

            return true;
        }

        private bool EquivalentFormulas(FragmentLoss loss1, FragmentLoss loss2)
        {
            return ArrayUtil.EqualsDeep(GetFormulaCounts(loss1.Formula).ToArray(),
                                        GetFormulaCounts(loss2.Formula).ToArray());
        }

        private IDictionary<string, int> GetFormulaCounts(string formula)
        {
            SortedDictionary<string, int> dictCounts = new SortedDictionary<string, int>();
            BioMassCalc.MONOISOTOPIC.ParseCounts(ref formula, dictCounts, false);
            return dictCounts;
        }

        private bool EquivalentFormulas(char aa, StaticMod obj)
        {
            SequenceMassCalc modCalc = new SequenceMassCalc(MassType.Monoisotopic);

            double unexplainedMassThis, unexplainedMassObj;

            string formulaThis = modCalc.GetModFormula(aa, this, out unexplainedMassThis);
            string formulaObj = modCalc.GetModFormula(aa, obj, out unexplainedMassObj);

            // If either is null, both must be null.
            if (formulaThis == null || formulaObj == null)
                return formulaThis == null && formulaObj == null;

            return unexplainedMassThis == unexplainedMassObj &&
                   ArrayUtil.EqualsDeep(GetFormulaModCounts(formulaThis).ToArray(),
                                        GetFormulaModCounts(formulaObj).ToArray());
        }

        private IDictionary<string, int> GetFormulaModCounts(string formula)
        {
            SortedDictionary<string, int> dictCounts = new SortedDictionary<string, int>();
            SequenceMassCalc.ParseModCounts(BioMassCalc.MONOISOTOPIC, formula, dictCounts);
            return dictCounts;
        }

        public bool Equals(StaticMod obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return EquivalentAll(obj) &&
                obj.IsExplicit.Equals(IsExplicit);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as StaticMod);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ (AAs != null ? AAs.GetHashCode() : 0);
                result = (result*397) ^ (Terminus.HasValue ? Terminus.Value.GetHashCode() : 0);
                result = (result*397) ^ IsVariable.GetHashCode();
                result = (result*397) ^ (AverageMass.HasValue ? AverageMass.Value.GetHashCode() : 0);
                result = (result*397) ^ (Formula != null ? Formula.GetHashCode() : 0);
                result = (result*397) ^ IsExplicit.GetHashCode();
                result = (result*397) ^ LabelAtoms.GetHashCode();
                result = (result*397) ^ RelativeRT.GetHashCode();
                result = (result*397) ^ (MonoisotopicMass.HasValue ? MonoisotopicMass.Value.GetHashCode() : 0);
                result = (result*397) ^ (_losses != null ? _losses.GetHashCodeDeep() : 0);
                result = (result*397) ^ (ShortName != null ? ShortName.GetHashCode() : 0);
                return result;
            }
        }

        #endregion

        public static bool EquivalentImplicitMods(IEnumerable<StaticMod> mods1, IEnumerable<StaticMod> mods2)
        {
            var modsImp1 = new List<StaticMod>(from mod1 in mods1 where !mod1.IsExplicit select mod1);
            var modsImp2 = new List<StaticMod>(from mod2 in mods2 where !mod2.IsExplicit select mod2);
            return ArrayUtil.ReferencesEqual(modsImp1, modsImp2);
        }
    }

    public sealed class TypedModifications : Immutable
    {
        public TypedModifications(IsotopeLabelType labelType, IList<StaticMod> modifications)
        {
            LabelType = labelType;
            Modifications = MakeReadOnly(modifications);
        }

        public IsotopeLabelType LabelType { get; private set; }
        public ImmutableList<StaticMod> Modifications { get; private set; }

        public bool HasImplicitModifications
        {
            get { return Modifications.Contains(mod => !mod.IsExplicit); }
        }

        #region object overrides

        public bool Equals(TypedModifications other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.LabelType, LabelType) &&
                ArrayUtil.EqualsDeep(other.Modifications, Modifications);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(TypedModifications)) return false;
            return Equals((TypedModifications)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (LabelType.GetHashCode() * 397) ^ Modifications.GetHashCodeDeep();
            }
        }

        /// <summary>
        /// Label type combo box in peptide settings Modifications tab depends on this
        /// </summary>
        public override string ToString()
        {
            return LabelType.ToString();
        }

        #endregion
    }

    public sealed class ExplicitMods : Immutable
    {
        private ImmutableList<TypedExplicitModifications> _modifications;

        /// <summary>
        /// Create a new set of explicit or variable modifications.  Assumes that
        /// static modifications have already been added to the heavy mods.
        /// </summary>
        public ExplicitMods(Peptide peptide, IList<ExplicitMod> staticMods,
            IEnumerable<TypedExplicitModifications> heavyMods, bool isVariable = false)
        {
            Peptide = peptide;
            IsVariableStaticMods = isVariable;

            var modifications = new List<TypedExplicitModifications>();
            // Add static mods, if applicable
            if (staticMods != null)
            {
                modifications.Add(new TypedExplicitModifications(peptide,
                    IsotopeLabelType.light, staticMods));
            }
            // Add isotope mods
            modifications.AddRange(heavyMods);
            _modifications = MakeReadOnly(modifications.ToArray());
        }

        /// <summary>
        /// Create a new set of explicit modifications on a peptide from a list of desired
        /// modifications of each type, and the global lists of available modifications.
        /// </summary>
        /// <param name="nodePep">The peptide to modify</param>
        /// <param name="staticMods">Static modifications to be applied</param>
        /// <param name="listStaticMods">Global static modifications</param>
        /// <param name="heavyMods">All sets of isotope labeled mods to be applied</param>
        /// <param name="listHeavyMods">Global isotope labeled mods</param>
        /// <param name="implicitOnly">True to create an explicit expression of the implicit modifications</param>
        public ExplicitMods(PeptideDocNode nodePep,
            IList<StaticMod> staticMods, MappedList<string, StaticMod> listStaticMods,
            IEnumerable<TypedModifications> heavyMods, MappedList<string, StaticMod> listHeavyMods,
            bool implicitOnly = false)
        {
            Peptide = nodePep.Peptide;

            var modifications = new List<TypedExplicitModifications>();
            TypedExplicitModifications staticTypedMods = null;
            // Add static mods, if applicable
            if (staticMods != null)
            {
                IList<ExplicitMod> explicitMods = GetImplicitMods(staticMods, listStaticMods);
                // If the peptide has variable modifications, make them all override
                // the modification state of the default implicit mods
                if (!implicitOnly && nodePep.HasVariableMods)
                {
                    explicitMods = MergeExplicitMods(nodePep.ExplicitMods.StaticModifications,
                        explicitMods, staticMods);
                }
                staticTypedMods = new TypedExplicitModifications(Peptide,
                    IsotopeLabelType.light, explicitMods);
                modifications.Add(staticTypedMods);
            }
            foreach (TypedModifications typedMods in heavyMods)
            {
                var explicitMods = GetImplicitMods(typedMods.Modifications, listHeavyMods);
                var typedHeavyMods = new TypedExplicitModifications(Peptide,
                    typedMods.LabelType, explicitMods);
                modifications.Add(typedHeavyMods.AddModMasses(staticTypedMods));
            }
            _modifications = MakeReadOnly(modifications.ToArray());
        }

        private static IList<ExplicitMod> MergeExplicitMods(IList<ExplicitMod> modsPrimary,
            IList<ExplicitMod> modsSecondary, IList<StaticMod> modifications)
        {
            var listExplicitMods = new List<ExplicitMod>();
            int iPrimary = 0, iSecondary = 0;
            while (iPrimary < modsPrimary.Count || iSecondary < modsSecondary.Count)
            {
                if (iSecondary >= modsSecondary.Count)
                    MergeMod(modsPrimary[iPrimary++], modifications, listExplicitMods);
                else if (iPrimary >= modsPrimary.Count)
                    MergeMod(modsSecondary[iSecondary++], modifications, listExplicitMods);
                else if (modsPrimary[iPrimary].IndexAA < modsSecondary[iSecondary].IndexAA)
                    MergeMod(modsPrimary[iPrimary++], modifications, listExplicitMods);
                else if (modsPrimary[iPrimary].IndexAA > modsSecondary[iSecondary].IndexAA)
                    MergeMod(modsSecondary[iSecondary++], modifications, listExplicitMods);
                else  // Equal
                {
                    MergeMod(modsPrimary[iPrimary++], modifications, listExplicitMods);
                    iSecondary++;
                }
            }
            return listExplicitMods.ToArray();
        }

        private static void MergeMod(ExplicitMod explicitMod, IEnumerable<StaticMod> docMods,
            ICollection<ExplicitMod> explicitMods)
        {
            if (docMods.Contains(explicitMod.Modification))
                explicitMods.Add(explicitMod);
        }


        /// <summary>
        /// Builds a list of <see cref="ExplicitMod"/> objects from the implicit modifications
        /// on the document.
        /// </summary>
        /// <param name="mods">Implicit modifications on the document</param>
        /// <param name="listSettingsMods">All modifications available in the settings</param>
        /// <returns>List of <see cref="ExplicitMod"/> objects for the implicit modifications</returns>
        private ExplicitMod[] GetImplicitMods(IList<StaticMod> mods, MappedList<string, StaticMod> listSettingsMods)
        {
            List<ExplicitMod> listImplicitMods = new List<ExplicitMod>();

            if (!Peptide.IsCustomIon)
            {
                string seq = Peptide.Sequence;
                for (int i = 0; i < seq.Length; i++)
                {
                    char aa = seq[i];
                    foreach (StaticMod mod in mods)
                    {
                        // Skip explicit mods, since only considering implicit
                        if (mod.IsExplicit || !mod.IsMod(aa, i, seq.Length))
                            continue;
                        // Always use the modification from the settings to ensure expected
                        // equality comparisons.
                        StaticMod modAdd;
                        if (listSettingsMods.TryGetValue(mod.Name, out modAdd))
                            listImplicitMods.Add(new ExplicitMod(i, modAdd));
                    }
                }
            }

            return listImplicitMods.ToArray();
        }

        public Peptide Peptide { get; private set; }

        public bool IsVariableStaticMods { get; private set; }

        public bool HasNeutralLosses { get { return StaticModifications.Contains(mod => mod.Modification.HasLoss); } }

        public IEnumerable<ExplicitMod> NeutralLossModifications
        {
            get
            {
                return from mod in StaticModifications
                       where mod.Modification.HasLoss
                       select mod;
            }
        }

        public IList<ExplicitMod> StaticModifications
        {
            get { return GetModifications(IsotopeLabelType.light); }
        }

        public IList<ExplicitMod> HeavyModifications
        {
            get { return GetModifications(IsotopeLabelType.heavy); }
        }

        public IList<ExplicitMod> GetModifications(IsotopeLabelType labelType)
        {
            int index = GetModIndex(labelType);
            return (index != -1 ? _modifications[index].Modifications : null);
        }

        public IList<ExplicitMod> GetStaticBaseMods(IsotopeLabelType labelType)
        {
            int index = GetModIndex(labelType);
            return (index != -1 ? _modifications[index].StaticBaseMods : null);
        }

        private int GetModIndex(IsotopeLabelType labelType)
        {
            return _modifications.IndexOf(mod => ReferenceEquals(labelType, mod.LabelType));
        }

        public IEnumerable<TypedExplicitModifications> GetHeavyModifications()
        {
            return _modifications.Where(typedMods => !typedMods.LabelType.IsLight);
        }

        public bool IsModified(IsotopeLabelType labelType)
        {
            return GetModIndex(labelType) != -1;
        }

        public bool HasHeavyModifications
        {
            get { return GetHeavyModifications().Contains(mods => mods.Modifications.Count > 0); }
        }

        public bool HasModifications(IsotopeLabelType labelType)
        {
            return _modifications.Contains(mods =>
                Equals(labelType, mods.LabelType) && mods.Modifications.Count > 0);
        }

        public IList<double> GetModMasses(MassType massType, IsotopeLabelType labelType)
        {
            var index = GetModIndex(labelType);
            // This will throw, if the modification type is not found.
            return _modifications[index].GetModMasses(massType);
        }

        public ExplicitMods ChangeGlobalMods(SrmSettings settingsNew)
        {
            var modSettings = settingsNew.PeptideSettings.Modifications;
            IList<StaticMod> heavyMods = null;
            List<StaticMod> heavyModsList = null;
            foreach (var typedMods in modSettings.GetHeavyModifications())
            {
                if (heavyMods == null)
                    heavyMods = typedMods.Modifications;
                else
                {
                    if (heavyModsList == null)
                        heavyModsList = new List<StaticMod>(heavyMods);
                    heavyModsList.AddRange(typedMods.Modifications);
                }
            }
            heavyMods = heavyModsList ?? heavyMods ?? new StaticMod[0];
            return ChangeGlobalMods(modSettings.StaticModifications, heavyMods,
                modSettings.GetHeavyModificationTypes().ToArray());
        }

        public ExplicitMods ChangeGlobalMods(IList<StaticMod> staticMods, IList<StaticMod> heavyMods,
            IList<IsotopeLabelType> heavyLabelTypes)
        {
            var modifications = new List<TypedExplicitModifications>();
            int index = GetModIndex(IsotopeLabelType.light);
            TypedExplicitModifications typedStaticMods = (index != -1 ? _modifications[index] : null);
            if (typedStaticMods != null)
            {
                IList<ExplicitMod> staticExplicitMods = ChangeGlobalMods(staticMods, typedStaticMods.Modifications);
                if (!ReferenceEquals(staticExplicitMods, typedStaticMods.Modifications))
                    typedStaticMods = new TypedExplicitModifications(Peptide, IsotopeLabelType.light, staticExplicitMods);
                modifications.Add(typedStaticMods);
            }

            foreach (TypedExplicitModifications typedMods in GetHeavyModifications())
            {
                // Discard explicit modifications for label types that no longer exist
                if (!heavyLabelTypes.Contains(typedMods.LabelType))
                    continue;

                var heavyExplicitMods = ChangeGlobalMods(heavyMods, typedMods.Modifications);
                var typedHeavyMods = typedMods;
                if (!ReferenceEquals(heavyExplicitMods, typedMods.Modifications))
                {
                    typedHeavyMods = new TypedExplicitModifications(Peptide, typedMods.LabelType, heavyExplicitMods);
                    typedHeavyMods = typedHeavyMods.AddModMasses(typedStaticMods);
                }
                modifications.Add(typedHeavyMods);                
            }
            if (ArrayUtil.ReferencesEqual(modifications, _modifications))
                return this;
            if (modifications.Count == 0)
                return null;
            return ChangeProp(ImClone(this), im => im._modifications = MakeReadOnly(modifications));
        }

        /// <summary>
        /// Replaces the <see cref="StaticMod"/> objects in a list of <see cref="ExplicitMod"/>
        /// objects.
        /// </summary>
        /// <param name="staticMods">The new <see cref="StaticMod"/> objects</param>
        /// <param name="explicitMods">The original <see cref="ExplicitMod"/> objects</param>
        /// <returns>A new list of <see cref="ExplicitMod"/> objects with <see cref="StaticMod"/> objects updated</returns>
        public IList<ExplicitMod> ChangeGlobalMods(IList<StaticMod> staticMods, IList<ExplicitMod> explicitMods)
        {
            var modsNew = new List<ExplicitMod>();
            if (!Peptide.IsCustomIon)
            {
                foreach (var mod in explicitMods)
                {
                    string name = mod.Modification.Name;
                    int iStaticMod = staticMods.IndexOf(mg => Equals(name, mg.Name));
                    // If the modification by this name has been removed, then remove
                    // it from the modification list.
                    if (iStaticMod == -1)
                        continue;
                    var staticMod = staticMods[iStaticMod];
                    if(!staticMod.IsMod(Peptide.Sequence[mod.IndexAA], mod.IndexAA, Peptide.Sequence.Length))
                        continue;
                    modsNew.Add(mod.Modification.EquivalentAll(staticMod)
                        ? mod
                        : mod.ChangeModification(staticMod));
                }
            }
            ArrayUtil.AssignIfEqualsDeep(modsNew, explicitMods);
            if (ArrayUtil.ReferencesEqual(modsNew, explicitMods))
                return explicitMods;
            return modsNew;
        }

        #region Property change methods

        public ExplicitMods ChangePeptide(Peptide prop)
        {
            return ChangeProp(ImClone(this), im => im.Peptide = prop);
        }

        public ExplicitMods ChangeStaticModifications(IList<ExplicitMod> prop)
        {
            return ChangeModifications(IsotopeLabelType.light, prop);
        }

        public ExplicitMods ChangeHeavyModifications(IList<ExplicitMod> prop)
        {
            return ChangeModifications(IsotopeLabelType.heavy, prop);
        }

        public ExplicitMods ChangeModifications(IsotopeLabelType labelType, IList<ExplicitMod> prop)
        {
            int index = GetModIndex(labelType);
            if (index == -1)
                throw new IndexOutOfRangeException(string.Format(Resources.ExplicitMods_ChangeModifications_Modification_type__0__not_found, labelType));
            var modifications = _modifications.ToArrayStd();
            var typedMods = new TypedExplicitModifications(Peptide, labelType, prop);
            if (index != 0)
                typedMods = typedMods.AddModMasses(modifications[0]);
            modifications[index] = typedMods;
            return ChangeProp(ImClone(this), im => im._modifications = MakeReadOnly(modifications));
        }

        /// <summary>
        /// Allow the variable modification flag to be cleared for consistent
        /// <see cref="PeptideSequenceModKey"/> keys.
        /// </summary>
        public ExplicitMods ChangeIsVariableStaticMods(bool prop)
        {
            return ChangeProp(ImClone(this), im => im.IsVariableStaticMods = prop);
        }

        #endregion

        #region object overrides

        public bool Equals(ExplicitMods obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return ArrayUtil.EqualsDeep(obj._modifications, _modifications) &&
                Equals(obj.Peptide, Peptide);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (ExplicitMods)) return false;
            return Equals((ExplicitMods) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = _modifications.GetHashCodeDeep();
                result = (result*397) ^ Peptide.GetHashCode();
                return result;
            }
        }

        #endregion
    }

    public sealed class ExplicitMod : Immutable
    {
        public ExplicitMod(int indexAA, StaticMod modification)
        {
            IndexAA = indexAA;
            // In the document context, all static non-variable mods must have the explicit
            // flag off to behave correctly for equality checks.  Only in the
            // settings context is the explicit flag necessary to destinguish
            // between the global implicit modifications and the explicit modifications
            // which do not apply to everything.
            if (modification.IsUserSet)
                modification = modification.ChangeExplicit(false);
            Modification = modification;
        }

        public int IndexAA { get; private set; }
        public StaticMod Modification { get; private set; }

        #region Property change methods

        public ExplicitMod ChangeModification(StaticMod prop)
        {
            if (prop.IsExplicit)
                prop = prop.ChangeExplicit(false);
            return ChangeProp(ImClone(this), (im, v) => im.Modification = v, prop);
        }

        #endregion

        #region object overrides

        public bool Equals(ExplicitMod obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.IndexAA == IndexAA &&
                Equals(obj.Modification, Modification);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (ExplicitMod)) return false;
            return Equals((ExplicitMod) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = IndexAA;
                result = (result*397) ^ Modification.GetHashCode();
                return result;
            }
        }

        #endregion
    }

    public sealed class TypedExplicitModifications : Immutable
    {
        // Cached masses for faster calculation
        private ImmutableList<double> _modMassesMono;
        private ImmutableList<double> _modMassesAvg;
        private TypedExplicitModifications _typedStaticMods;

        public TypedExplicitModifications(Peptide peptide, IsotopeLabelType labelType,
            IList<ExplicitMod> modifications)
        {
            LabelType = labelType;
            Modifications = MakeReadOnly(modifications);

            // Cache modification masses
            var modMassesMono = CalcModMasses(peptide, Modifications, SrmSettings.MonoisotopicMassCalc);
            _modMassesMono = MakeReadOnly(modMassesMono);
            var modMassesAvg = CalcModMasses(peptide, Modifications, SrmSettings.AverageMassCalc);
            _modMassesAvg = MakeReadOnly(modMassesAvg);
        }

        public IsotopeLabelType LabelType { get; private set; }
        public IList<ExplicitMod> Modifications { get; private set; }
        public IList<ExplicitMod> StaticBaseMods
        {
            get
            {
                return _typedStaticMods != null ? _typedStaticMods.Modifications : null;
            }
        }

        public IList<double> GetModMasses(MassType massType)
        {
            return (massType == MassType.Monoisotopic ? _modMassesMono : _modMassesAvg);
        }

        private static double[] CalcModMasses(Peptide peptide, IEnumerable<ExplicitMod> mods,
            SequenceMassCalc massCalc)
        {
            double[] masses = new double[peptide.Length];
            string seq = peptide.Sequence;
            foreach (ExplicitMod mod in mods)
                masses[mod.IndexAA] += massCalc.GetModMass(seq[mod.IndexAA], mod.Modification);
            return masses;
        }

        public TypedExplicitModifications AddModMasses(TypedExplicitModifications typedStaticMods)
        {
            if (typedStaticMods == null)
                return this;
            if (_typedStaticMods != null)
                throw new InvalidOperationException(Resources.TypedExplicitModifications_AddModMasses_Static_mod_masses_have_already_been_added_for_this_heavy_type);
            if (LabelType.IsLight)
                throw new InvalidOperationException(Resources.TypedExplicitModifications_AddModMasses_Static_mod_masses_may_not_be_added_to_light_type);

            var im = ImClone(this);
            im._typedStaticMods = typedStaticMods;
            im._modMassesMono = AddModMasses(im._modMassesMono, typedStaticMods._modMassesMono);
            im._modMassesAvg = AddModMasses(im._modMassesAvg, typedStaticMods._modMassesAvg);
            return im;
        }

        private static ImmutableList<double> AddModMasses(IList<double> modMasses1, IList<double> modMasses2)
        {
            double[] masses = modMasses1.ToArrayStd();
            for (int i = 0, count = Math.Min(masses.Length, modMasses2.Count); i < count; i++)
                masses[i] += modMasses2[i];
            return MakeReadOnly(masses);
        }

        #region object overrides

        public bool Equals(TypedExplicitModifications other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.LabelType, LabelType) &&
                ArrayUtil.EqualsDeep(other.Modifications, Modifications);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(TypedExplicitModifications)) return false;
            return Equals((TypedExplicitModifications)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (LabelType.GetHashCode() * 397) ^ Modifications.GetHashCodeDeep();
            }
        }

        #endregion
    }
}
