/*
 * Original author: Max Horowitz-Gelb <maxhg .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Xml;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    public class CustomIon : CustomMolecule, IAuditLogComparable
    {
        public static readonly CustomIon EMPTY = new CustomIon();

        [TrackChildren(ignoreName:true, ignoreDefaultParent:true)]
        public Adduct Adduct { get; private set; }

        /// <summary>
        /// A simple object used to represent any ionized molecule
        /// </summary>
        /// <param name="formula">The molecular formula of the molecule, possibly with adduct embedded</param>
        /// <param name="adduct">The adduct description, if its not embedded in formula</param>
        /// <param name="monoisotopicMass">The monoisotopic mass of the molecule(can be calculated from formula)</param>
        /// <param name="averageMass">The average mass of the molecule (can be calculated by the formula)</param>
        /// <param name="name">The arbitrary name given to this molecule</param>
        public CustomIon(string formula, Adduct adduct, double? monoisotopicMass = null, double? averageMass = null, string name = null)
            : this(formula, adduct, new TypedMass(monoisotopicMass ?? averageMass ?? 0, MassType.Monoisotopic),
                                    new TypedMass(averageMass ?? monoisotopicMass ?? 0, MassType.Average), name)
        {
        }

        public CustomIon(string formula) : this(formula, Adduct.EMPTY)
        {
        }

        public CustomIon(CustomMolecule mol, Adduct adduct)
            : this(mol.Formula, adduct, mol.MonoisotopicMass, mol.AverageMass, mol.Name)
        {
        }

        public CustomIon(SmallMoleculeLibraryAttributes mol, Adduct adduct, double? monoisotopicMass = null, double? averageMass = null)
        : this(mol.ChemicalFormula, adduct,monoisotopicMass, averageMass, mol.MoleculeName)
        {
        }

        public CustomIon(string formula, Adduct adduct, TypedMass monoisotopicMass, TypedMass averageMass, string name)
            : base(formula, monoisotopicMass, averageMass, name)
        {
            if (adduct.IsEmpty)
            {
                var ionInfo = new IonInfo(NeutralFormula, adduct); // Analyzes the formula to see if it's something like "CH12[M+Na]"
                if (!Equals(NeutralFormula, ionInfo.NeutralFormula))
                {
                    Formula = ionInfo.NeutralFormula;
                }
                Adduct = Adduct.FromStringAssumeProtonated(ionInfo.AdductText);
            }
            else
            {
                Adduct = adduct;
            }
        }

        public new bool IsEmpty { get { return ReferenceEquals(this, EMPTY) || (Adduct.IsEmpty && base.IsEmpty); } }

        /// <summary>
        /// For serialization
        /// </summary>
        protected CustomIon()
        {
            Adduct = Adduct.EMPTY;
        }

        public CustomIon ChangeName(string name)
        {
            if (Equals(Name, name))
                return this;
            return new CustomIon(Formula, Adduct, MonoisotopicMass, AverageMass, name);
        }

        public static CustomIon Deserialize(XmlReader reader)
        {
            var ion = new CustomIon();
            Adduct adduct;
            ion.ReadAttributes(reader, out adduct);
            ion.Adduct = adduct;
            return ion;
        }

        public string NeutralFormula { get { return Formula; } }

        public string FormulaWithAdductApplied
        {
            get
            {
                var ionInfo = new IonInfo(NeutralFormula, Adduct);
                return ionInfo.FormulaWithAdductApplied;
            }
        }

        [Track]
        public double MonoisotopicMassMz { get { return Adduct.MzFromNeutralMass(MonoisotopicMass, MassType.Monoisotopic); } }
        [Track]
        public double AverageMassMz { get { return Adduct.MzFromNeutralMass(AverageMass, MassType.Average); } }

        public new string ToSerializableString()
        {
            // Replace tab with something that XML parsers won't mess with
            var tsv = ToTSV();
            return AccessionNumbers.EscapeTabsForXML(tsv);
        }
        public new static CustomMolecule FromSerializableString(string val)
        {
            var tsv = MoleculeAccessionNumbers.UnescapeTabsForXML(val);
            return FromTSV(tsv);
        }

        public new List<string> AsFields()
        {
            var list = base.AsFields() ?? new List<string>();
            list.Add(Adduct.ToString());
            return list;
        }

        public new string ToTSV()
        {
            return base.ToTSV() + TextUtil.SEPARATOR_TSV + Adduct;
        }

        public new static CustomIon FromTSV(string val)
        {
            var lastTab = val.LastIndexOf(TextUtil.SEPARATOR_TSV_STR, StringComparison.Ordinal);
            var adduct = Adduct.FromStringAssumeChargeOnly(val.Substring(lastTab + 1));
            var mol = CustomMolecule.FromTSV(val.Substring(0, lastTab));
            if (adduct.IsEmpty && mol.IsEmpty)
            {
                return EMPTY;
            }
            return new CustomIon(mol, adduct);
        }

        public override string DisplayNameDetail { get { return Resources.CustomIon_DisplayName_Ion; } }

        public override string InvariantNameDetail { get { return @"Ion"; } }
        public object GetDefaultObject(ObjectInfo<object> info)
        {
            var ion = (CustomIon) info.NewObject;
            return new CustomIon(ion, Adduct.EMPTY); // Ignore CustomMolecule properties
        }
    }

    /// <summary>
    /// Special subclass of custom ion for use in settings
    /// For use as a reference in a document, and not to be edited.
    /// </summary>

    public class SettingsCustomIon : CustomIon
    {
        public SettingsCustomIon(string formula, Adduct adduct, double? monoisotopicMass, double? averageMass, string name)
            : base(formula, adduct, monoisotopicMass, averageMass, name)
        {
        }

        /// <summary>
        /// For serialization
        /// </summary>
        protected SettingsCustomIon()
        {
        }

    }
}
