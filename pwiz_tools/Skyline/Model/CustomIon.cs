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

using System.Xml;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class CustomIon : CustomMolecule
    {
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

        /// <summary>
        /// For serialization
        /// </summary>
        protected CustomIon()
        {
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

        public override string DisplayNameDetail { get { return Resources.CustomIon_DisplayName_Ion; } }

        public override string InvariantNameDetail { get { return "Ion"; } } // Not L10N
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
