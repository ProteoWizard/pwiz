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
            : this(name, aa, term, formula, labelAtoms, RelativeRT.Matching, monoMass, avgMass, null, null, null)
        {
            
        }

        public StaticMod(string name,
                         char? aa,
                         ModTerminus? term,
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
            return ChangeProp(ImClone(this), (im, v) => im.IsExplicit = v, prop);
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
            // static modification.
//            if (!Terminus.HasValue && !AA.HasValue)
//                throw new InvalidDataException("Modification must specify amino acid or terminus.");
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
                obj.AverageMass.Equals(AverageMass) &&
                obj.AverageLoss.Equals(AverageLoss) &&
                Equals(obj.Formula, Formula) &&
                Equals(obj.FormulaLoss, FormulaLoss) &&
                Equals(obj.LabelAtoms, LabelAtoms) &&
                Equals(obj.RelativeRT, RelativeRT) &&
                obj.MonoisotopicMass.Equals(MonoisotopicMass) &&
                obj.MonoisotopicLoss.Equals(MonoisotopicLoss) &&
                obj.Terminus.Equals(Terminus);
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
                result = (result*397) ^ (AverageMass.HasValue ? AverageMass.Value.GetHashCode() : 0);
                result = (result*397) ^ (AverageLoss.HasValue ? AverageLoss.Value.GetHashCode() : 0);
                result = (result*397) ^ (Formula != null ? Formula.GetHashCode() : 0);
                result = (result*397) ^ (FormulaLoss != null ? FormulaLoss.GetHashCode() : 0);
                result = (result*397) ^ IsExplicit.GetHashCode();
                result = (result*397) ^ LabelAtoms.GetHashCode();
                result = (result*397) ^ RelativeRT.GetHashCode();
                result = (result*397) ^ (MonoisotopicMass.HasValue ? MonoisotopicMass.Value.GetHashCode() : 0);
                result = (result*397) ^ (MonoisotopicLoss.HasValue ? MonoisotopicLoss.Value.GetHashCode() : 0);
                result = (result*397) ^ (Terminus.HasValue ? Terminus.Value.GetHashCode() : 0);
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

    public sealed class ExplicitMods : Immutable
    {
        private ReadOnlyCollection<ExplicitMod> _staticModifications;
        private ReadOnlyCollection<ExplicitMod> _heavyModifications;
        // Cached masses for faster calculation
        private ReadOnlyCollection<double> _staticModMassesMono;
        private ReadOnlyCollection<double> _staticModMassesAvg;
        private ReadOnlyCollection<double> _heavyModMassesMono;
        private ReadOnlyCollection<double> _heavyModMassesAvg;

        public ExplicitMods(Peptide peptide, IList<ExplicitMod> staticMods, IList<ExplicitMod> heavyMods)
        {
            Peptide = peptide;
            StaticModifications = staticMods;
            HeavyModifications = heavyMods;

            CacheModMasses();
        }

        public ExplicitMods(Peptide peptide,
            IEnumerable<StaticMod> staticMods, MappedList<string, StaticMod> listStaticMods,
            IEnumerable<StaticMod> heavyMods, MappedList<string, StaticMod> listHeavyMods)
        {
            Peptide = peptide;
            StaticModifications = GetImplicitMods(staticMods, listStaticMods);
            HeavyModifications = GetImplicitMods(heavyMods, listHeavyMods);

            CacheModMasses();
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

        private void CacheModMasses()
        {
            // Cache modification masses
            var staticModMassesMono = CalcModMasses(StaticModifications, null, SrmSettings.MonoisotopicMassCalc);
            _staticModMassesMono = MakeReadOnly(staticModMassesMono);
            var staticModMassesAvg = CalcModMasses(StaticModifications, null, SrmSettings.AverageMassCalc);
            _staticModMassesAvg = MakeReadOnly(staticModMassesAvg);
            var heavyModMassesMono = CalcModMasses(HeavyModifications, staticModMassesMono, SrmSettings.MonoisotopicMassCalc);
            _heavyModMassesMono = MakeReadOnly(heavyModMassesMono);
            var heavyModMassesAvg = CalcModMasses(HeavyModifications, staticModMassesAvg, SrmSettings.AverageMassCalc);
            _heavyModMassesAvg = MakeReadOnly(heavyModMassesAvg);
        }

        public Peptide Peptide { get; private set; }

        public IList<ExplicitMod> StaticModifications
        {
            get { return _staticModifications; }
            private set { _staticModifications = MakeReadOnly(value); }
        }

        public IList<ExplicitMod> HeavyModifications
        {
            get { return _heavyModifications; }
            private set { _heavyModifications = MakeReadOnly(value); }
        }

        public bool HasHeavyModifications { get { return _heavyModifications.Count > 0; } }

        public IList<double> GetModMasses(MassType massType, IsotopeLabelType labelType)
        {
            if (massType == MassType.Monoisotopic)
            {
                if (labelType == IsotopeLabelType.light)
                    return _staticModMassesMono;
                else
                    return _heavyModMassesMono;
            }
            else
            {
                if (labelType == IsotopeLabelType.light)
                    return _staticModMassesAvg;
                else
                    return _heavyModMassesAvg;
            }
        }

        private double[] CalcModMasses(IEnumerable<ExplicitMod> mods, double[] massesBase, SequenceMassCalc massCalc)
        {
            double[] masses = new double[Peptide.Length];
            if (massesBase != null)
                Array.Copy(massesBase, masses, Math.Min(massesBase.Length, masses.Length));
            string seq = Peptide.Sequence;
            foreach (ExplicitMod mod in mods)
                masses[mod.IndexAA] += massCalc.GetModMass(seq[mod.IndexAA], mod.Modification);
            return masses;
        }

        public ExplicitMods ChangeGlobalMods(SrmSettings settingsNew)
        {
            var modSettings = settingsNew.PeptideSettings.Modifications;
            return ChangeGlobalMods(modSettings.StaticModifications, modSettings.HeavyModifications);
        }

        public ExplicitMods ChangeGlobalMods(IList<StaticMod> staticMods, IList<StaticMod> heavyMods)
        {
            IList<ExplicitMod> staticExplicitMods = ChangeGlobalMods(staticMods , StaticModifications);
            IList<ExplicitMod> heavyExplicitMods = ChangeGlobalMods(heavyMods, HeavyModifications);
            if (ReferenceEquals(staticExplicitMods, StaticModifications) &&
                    ReferenceEquals(heavyExplicitMods, HeavyModifications))
                return this;
            return new ExplicitMods(Peptide, staticExplicitMods, heavyExplicitMods);            
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
            return ChangeProp(ImClone(this), (im, v) => im.StaticModifications = v, prop);
        }

        public ExplicitMods ChangeHeavyModifications(IList<ExplicitMod> prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.HeavyModifications = v, prop);
        }

        #endregion

        #region object overrides

        public bool Equals(ExplicitMods obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return ArrayUtil.EqualsDeep(obj._heavyModifications, _heavyModifications) &&
                ArrayUtil.EqualsDeep(obj._staticModifications, _staticModifications) &&
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
                int result = _heavyModifications.GetHashCodeDeep();
                result = (result*397) ^ _staticModifications.GetHashCodeDeep();
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
}
