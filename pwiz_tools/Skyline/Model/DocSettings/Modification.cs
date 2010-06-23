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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
// ReSharper disable InconsistentNaming
    public enum ModTerminus { C, N };

    [Flags]
    public enum LabelAtoms
    {
        None = 0,
        C13 = 0x1,
        N15 = 0x2,
        O18 = 0x4,
        H2 = 0x8
    }

    public enum RelativeRT { Matching, Overlapping, Preceding, Unknown }
// ReSharper restore InconsistentNaming

    /// <summary>
    /// Represents a document-wide  or explicit static modification that applies
    /// to all amino acids of a specific type or a single amino acid, or all peptides in the
    /// case of C-terminal or N-terminal modifications.
    /// </summary>
    [XmlRoot("static_modification")]
    public sealed class StaticMod : XmlNamedElement
    {
        public StaticMod(string name, char aa, string formula)
            : this(name, aa, null, formula, LabelAtoms.None, null, null)
        {            
        }

        public StaticMod(string name, char aa, LabelAtoms labelAtoms)
            : this(name, aa, null, null, labelAtoms, null, null)
        {
        }

        public StaticMod(string name, char? aa, ModTerminus? term,
            string formula, LabelAtoms labelAtoms, double? monoMass, double? avgMass)
            : this(name, aa, term, false, formula, labelAtoms, RelativeRT.Matching, monoMass, avgMass, null, null, null)
        {
            
        }

        public StaticMod(string name,
                         char? aa,
                         ModTerminus? term,
                         bool isVariable,
                         string formula,
                         LabelAtoms labelAtoms,
                         RelativeRT relativeRT,
                         double? monoMass,
                         double? avgMass,
                         string formulaLoss,
                         double? monoLoss,
                         double? avgLoss)
            : base(name)
        {
            AA = aa;
            Terminus = term;
            IsVariable = IsExplicit = isVariable;   // All variable mods are explicit
            Formula = formula;
            LabelAtoms = labelAtoms;
            RelativeRT = relativeRT;

            // Only allow masses, if formula is not specified.
            if (string.IsNullOrEmpty(formula))
            {
                MonoisotopicMass = monoMass;
                AverageMass = avgMass;                
            }

            FormulaLoss = formulaLoss;

            // Only allow masses, if formula is not specified.
            if (string.IsNullOrEmpty(formulaLoss))
            {
                MonoisotopicLoss = monoLoss;
                AverageLoss = avgLoss;
            }

            Validate();
        }

        public char? AA { get; private set; }

        public ModTerminus? Terminus { get; private set; }

        public bool IsVariable { get; private set; }

        public string Formula { get; private set; }
        public string FormulaLoss { get; private set; }

        public LabelAtoms LabelAtoms { get; private set; }
        public bool Label13C { get { return (LabelAtoms & LabelAtoms.C13) != 0; } }
        public bool Label15N { get { return (LabelAtoms & LabelAtoms.N15) != 0; } }
        public bool Label18O { get { return (LabelAtoms & LabelAtoms.O18) != 0; } }
        public bool Label2H { get { return (LabelAtoms & LabelAtoms.H2) != 0; } }
        public RelativeRT RelativeRT { get; private set; }

        public double? MonoisotopicMass { get; private set; }
        public double? MonoisotopicLoss { get; private set; }

        public double? AverageMass { get; private set; }
        public double? AverageLoss { get; private set; }

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

        public bool IsMod(char aa, int indexAA, int len)
        {
            if (AA.HasValue && AA.Value != aa)
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

        public bool IsMod(string sequence)
        {
            for (int i = 0; i < sequence.Length; i++)
            {
                if (IsMod(sequence[i], i, sequence.Length))
                    return true;
            }
            return false;
        }

        public bool IsLoss
        {
            get { return FormulaLoss != null || MonoisotopicLoss.HasValue; }
        }

        #region Property change methods

        public StaticMod ChangeExplicit(bool prop)
        {
            return ChangeProp(ImClone(this), im =>
                                                 {
                                                     im.IsExplicit = prop;
                                                     im.IsVariable = false;
                                                 });
        }

        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private StaticMod()
        {
        }

        private enum ATTR
        {
            aminoacid,
            terminus,
            variable,
            formula,
            formula_loss,
// ReSharper disable InconsistentNaming
            label_13C,
            label_15N,
            label_18O,
            label_2H,
// ReSharper restore InconsistentNaming
            relative_rt,
            massdiff_monoisotopic,
            massloss_monoisotopic,
            massdiff_average,
            massloss_average,
            explicit_decl
        }

        private static readonly BioMassCalc VALIDATION_MASS_CALC = new BioMassCalc(MassType.Monoisotopic);

        private void Validate()
        {
            // It is now valid to specify modifications that apply to every amino acid.
            // This is important for 15N labeling, and reasonable for an explicit
            // static modification... but not for variable modifications.
            if (IsVariable && !Terminus.HasValue && !AA.HasValue)
                throw new InvalidDataException("Variable modifications must specify amino acid or terminus.");
            if (AA.HasValue && !AminoAcid.IsAA(AA.Value))
                throw new InvalidDataException(string.Format("Invalid amino acid {0}.", AA.Value));
            if (!AA.HasValue && Terminus.HasValue && LabelAtoms != LabelAtoms.None)
                throw new InvalidDataException("Terminal modification with labeled atoms not allowed.");
            if (Formula == null && LabelAtoms == LabelAtoms.None)
            {
                if (MonoisotopicMass == null || AverageMass == null)
                    throw new InvalidDataException("Modification must specify a formula, labeled atoms or valid monoisotopic and average masses.");
            }
            else
            {
                if (Formula != null)
                {
                    if (LabelAtoms != LabelAtoms.None)
                        throw new InvalidDataException("Formula not allowed with labeled atoms.");
                    // Throws an exception, if given an invalid formula.
                    SequenceMassCalc.ParseModMass(VALIDATION_MASS_CALC, Formula);                    
                }
                // No explicit masses with formula or label atoms
                if (MonoisotopicMass != null || AverageMass != null)
                    throw new InvalidDataException("Modification with a formula may not specify modification masses.");
            }
            if (FormulaLoss == null)
            {
                // Most both be not null or both null
                if ((MonoisotopicLoss == null || AverageLoss == null) &&
                        (MonoisotopicLoss != null || AverageLoss != null))
                    throw new InvalidDataException("Modification without a loss formula, must specify both monoisotopic and average losses or neither.");
            }
            else
            {
                // Throws an exception, if given an invalid formula.
                SequenceMassCalc.ParseModMass(VALIDATION_MASS_CALC, FormulaLoss);

                // No explicit loss masses with formula
                if (MonoisotopicLoss != null || AverageLoss != null)
                    throw new InvalidDataException("Modification with a loss formula may not also specify loss masses.");
            }
        }

        private static ModTerminus ToModTerminus(String value)
        {
            try
            {
                return (ModTerminus)Enum.Parse(typeof(ModTerminus), value, true);
            }
            catch (ArgumentException)
            {
                throw new ArgumentException(string.Format("Invalid terminus '{0}'.", value));
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
            string aa = reader.GetAttribute(ATTR.aminoacid);
            if (!string.IsNullOrEmpty(aa))
            {
                if (aa.Length > 1)
                    throw new InvalidDataException(string.Format("Invalid amino acid '{0}'.", aa));

                AA = aa[0];
                // Support v0.1 format.
                if (AA == '\0')
                    AA = null;
            }

            Terminus = reader.GetAttribute<ModTerminus>(ATTR.terminus, ToModTerminus);
            IsVariable = IsExplicit = reader.GetBoolAttribute(ATTR.variable);
            Formula = reader.GetAttribute(ATTR.formula);
            FormulaLoss = reader.GetAttribute(ATTR.formula_loss);
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
            MonoisotopicLoss = reader.GetNullableDoubleAttribute(ATTR.massloss_monoisotopic);
            AverageMass = reader.GetNullableDoubleAttribute(ATTR.massdiff_average);
            AverageLoss = reader.GetNullableDoubleAttribute(ATTR.massloss_average);

            if (!IsVariable)
                IsExplicit = reader.GetBoolAttribute(ATTR.explicit_decl);

            // Consume tag
            reader.Read();

            Validate();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttributeNullable(ATTR.aminoacid, AA);
            writer.WriteAttributeNullable(ATTR.terminus, Terminus);
            writer.WriteAttribute(ATTR.variable, IsVariable);
            writer.WriteAttributeIfString(ATTR.formula, Formula);
            writer.WriteAttributeIfString(ATTR.formula_loss, FormulaLoss);
            writer.WriteAttribute(ATTR.label_13C, Label13C);
            writer.WriteAttribute(ATTR.label_15N, Label15N);
            writer.WriteAttribute(ATTR.label_18O, Label18O);
            writer.WriteAttribute(ATTR.label_2H, Label2H);
            writer.WriteAttribute(ATTR.relative_rt, RelativeRT, RelativeRT.Matching);
            writer.WriteAttributeNullable(ATTR.massdiff_monoisotopic, MonoisotopicMass);
            writer.WriteAttributeNullable(ATTR.massloss_monoisotopic, MonoisotopicLoss);
            writer.WriteAttributeNullable(ATTR.massdiff_average, AverageMass);
            writer.WriteAttributeNullable(ATTR.massloss_average, AverageLoss);
            if (!IsVariable)
                writer.WriteAttribute(ATTR.explicit_decl, IsExplicit);
        }

        #endregion

        #region object overrides

        /// <summary>
        /// Equality minus the <see cref="IsExplicit"/> flag.
        /// </summary>
        public bool Equivalent(StaticMod obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) && obj.AA.Equals(AA) &&
                obj.Terminus.Equals(Terminus) &&
                obj.IsVariable.Equals(IsVariable) &&
                obj.AverageMass.Equals(AverageMass) &&
                obj.AverageLoss.Equals(AverageLoss) &&
                Equals(obj.Formula, Formula) &&
                Equals(obj.FormulaLoss, FormulaLoss) &&
                Equals(obj.LabelAtoms, LabelAtoms) &&
                Equals(obj.RelativeRT, RelativeRT) &&
                obj.MonoisotopicMass.Equals(MonoisotopicMass) &&
                obj.MonoisotopicLoss.Equals(MonoisotopicLoss);
        }

        public bool Equals(StaticMod obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equivalent(obj) &&
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
                result = (result*397) ^ (AA.HasValue ? AA.Value.GetHashCode() : 0);
                result = (result*397) ^ (Terminus.HasValue ? Terminus.Value.GetHashCode() : 0);
                result = (result*397) ^ IsVariable.GetHashCode();
                result = (result*397) ^ (AverageMass.HasValue ? AverageMass.Value.GetHashCode() : 0);
                result = (result*397) ^ (AverageLoss.HasValue ? AverageLoss.Value.GetHashCode() : 0);
                result = (result*397) ^ (Formula != null ? Formula.GetHashCode() : 0);
                result = (result*397) ^ (FormulaLoss != null ? FormulaLoss.GetHashCode() : 0);
                result = (result*397) ^ IsExplicit.GetHashCode();
                result = (result*397) ^ LabelAtoms.GetHashCode();
                result = (result*397) ^ RelativeRT.GetHashCode();
                result = (result*397) ^ (MonoisotopicMass.HasValue ? MonoisotopicMass.Value.GetHashCode() : 0);
                result = (result*397) ^ (MonoisotopicLoss.HasValue ? MonoisotopicLoss.Value.GetHashCode() : 0);
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
        public IList<StaticMod> Modifications { get; private set; }

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

        #endregion
    }

    public sealed class ExplicitMods : Immutable
    {
        private ReadOnlyCollection<TypedExplicitModifications> _modifications;

        public ExplicitMods(Peptide peptide, IList<ExplicitMod> staticMods,
            IEnumerable<TypedExplicitModifications> heavyMods)
            : this(peptide, staticMods, heavyMods, false)
        {
            
        }
        public ExplicitMods(Peptide peptide, IList<ExplicitMod> staticMods,
            IEnumerable<TypedExplicitModifications> heavyMods, bool isVariable)
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

        public ExplicitMods(Peptide peptide,
            IEnumerable<StaticMod> staticMods, MappedList<string, StaticMod> listStaticMods,
            IEnumerable<TypedModifications> heavyMods, MappedList<string, StaticMod> listHeavyMods)
        {
            Peptide = peptide;

            var modifications = new List<TypedExplicitModifications>();
            TypedExplicitModifications staticTypedMods = null;
            // Add static mods, if applicable
            if (staticMods != null)
            {
                var explicitMods = GetImplicitMods(staticMods, listStaticMods);
                staticTypedMods = new TypedExplicitModifications(peptide,
                    IsotopeLabelType.light, explicitMods);
                modifications.Add(staticTypedMods);
            }
            foreach (TypedModifications typedMods in heavyMods)
            {
                var explicitMods = GetImplicitMods(typedMods.Modifications, listHeavyMods);
                var typedHeavyMods = new TypedExplicitModifications(peptide,
                    typedMods.LabelType, explicitMods);
                modifications.Add(typedHeavyMods.AddModMasses(staticTypedMods));
            }
            _modifications = MakeReadOnly(modifications.ToArray());
        }

        /// <summary>
        /// Builds a list of <see cref="ExplicitMod"/> objects from the implicit modifications
        /// on the document.
        /// </summary>
        /// <param name="mods">Implicit modifications on the document</param>
        /// <param name="listSettingsMods">All modifications available in the settings</param>
        /// <returns>List of <see cref="ExplicitMod"/> objects for the implicit modifications</returns>
        private ExplicitMod[] GetImplicitMods(IEnumerable<StaticMod> mods, MappedList<string, StaticMod> listSettingsMods)
        {
            List<ExplicitMod> listImplicitMods = new List<ExplicitMod>();

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

            return listImplicitMods.ToArray();
        }

        public Peptide Peptide { get; private set; }

        public bool IsVariableStaticMods { get; private set; }

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
            if (index == -1)
                return null;
            return _modifications[index].Modifications;
        }

        private int GetModIndex(IsotopeLabelType labelType)
        {
            return _modifications.IndexOf(mod => Equals(labelType, mod.LabelType));
        }

        public IEnumerable<TypedExplicitModifications> GetHeavyModifications()
        {
            for (int i = 0; i < _modifications.Count; i++)
            {
                var typedMods = _modifications[i];
                if (!typedMods.LabelType.IsLight)
                    yield return typedMods;
            }
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
                modSettings.GetHeavyModificationTypes());
        }

        public ExplicitMods ChangeGlobalMods(IList<StaticMod> staticMods, IList<StaticMod> heavyMods,
            IEnumerable<IsotopeLabelType> heavyLabelTypes)
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

        public static IList<ExplicitMod> ChangeGlobalMods(IList<StaticMod> staticMods, IList<ExplicitMod> explicitMods)
        {
            IList<ExplicitMod> modsNew = explicitMods.ToArray();
            for (int i = 0; i < modsNew.Count; i++)
            {
                var mod = modsNew[i];
                int iStaticMod = staticMods.IndexOf(mg => Equals(mg.Name, mod.Modification.Name));
                if (iStaticMod == -1)
                    throw new InvalidDataException(string.Format("The explicit modification {0} is not present in the document settings.", mod.Modification.Name));
                var staticMod = staticMods[iStaticMod];
                if (!mod.Modification.Equivalent(staticMod))
                    modsNew[i] = mod.ChangeModification(staticMod);
            }
            ArrayUtil.AssignIfEqualsDeep(modsNew, explicitMods);
            if (ArrayUtil.ReferencesEqual(modsNew, explicitMods))
                return explicitMods;
            return modsNew;
        }

        #region Property change methods

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
                throw new IndexOutOfRangeException(string.Format("Modification type {0} not found.", labelType));
            var modifications = _modifications.ToArrayStd();
            var typedMods = new TypedExplicitModifications(Peptide, labelType, prop);
            if (index != 0)
                typedMods = typedMods.AddModMasses(modifications[0]);
            modifications[index] = typedMods;
            return ChangeProp(ImClone(this), im => im._modifications = MakeReadOnly(modifications));
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
            // Explicit should only be used in the settings context
            Debug.Assert(!modification.IsExplicit);
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
        private ReadOnlyCollection<double> _modMassesMono;
        private ReadOnlyCollection<double> _modMassesAvg;
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
                throw new InvalidOperationException("Static mod masses have already been added for this heavy type.");
            if (LabelType.IsLight)
                throw new InvalidOperationException("Static mod masses may not be added to light type.");

            var im = ImClone(this);
            im._typedStaticMods = typedStaticMods;
            im._modMassesMono = AddModMasses(im._modMassesMono, typedStaticMods._modMassesMono);
            im._modMassesAvg = AddModMasses(im._modMassesAvg, typedStaticMods._modMassesAvg);
            return im;
        }

        private static ReadOnlyCollection<double> AddModMasses(IList<double> modMasses1, IList<double> modMasses2)
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
