/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Linq;
using System.Xml;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Serialization
{

    /// <summary>
    /// Helper class for dealing with older Skyline small molecule documents which may not
    /// have the expected "neutral parent molecule with child adducts" structure.
    /// Its purpose is to read in the current transition groups's precursors and try to
    /// modernize the XML so that it's in terms of a common neutral molecule with adducts,
    /// while preserving the precursor mz values
    /// </summary>
    public class Pre372CustomIonTransitionGroupHandler
    {

        // Read ahead examining transition groups for current molecule and try to discern the actual neutral formula of the molecule
        // Needed since we actually stored the precursor description rather than the molecule description in v3.71 and earlier
        private class PrecursorRawDetails
        {
            public string _formulaUnlabeled;
            public IDictionary<string,int> _labels;
            public Adduct _nominalAdduct;
            public Adduct _proposedAdduct;
            public int _declaredCharge;
            public double _declaredMz;
            public bool _declaredHeavy;
        }

        private XmlReadAhead ReadAhead { get; set; }
        private readonly List<PrecursorRawDetails> _precursorRawDetails;

        private double MzToler
        {
            get { return _mzToler; }
            set { _mzToler = value; _mzDecimalPlaces = (_mzToler < .00001) ? 6 : 5; }
        }
        private double _mzToler; 
        private int _mzDecimalPlaces; 
        private CustomMolecule ProposedMolecule { get; set; }

        public Pre372CustomIonTransitionGroupHandler(XmlReader readerIn, double settingsMzMatchToler)
        {
            ReadAhead = new XmlReadAhead(readerIn); // Read the current element and its children

            // Put the contents of the readahead buffer into parseable XML form, return a reader for that
            var reader = ReadAhead.CreateXmlReader();

            // Advance the reader to the start of the precursors list, if any
            Assume.IsTrue(reader.IsStartElement(DocumentSerializer.EL.molecule));
            while (!reader.IsStartElement(DocumentSerializer.EL.precursor) && !reader.EOF)
                reader.Read();

            // Gather info on all declared precursors
            _precursorRawDetails = new List<PrecursorRawDetails>();
            while (reader.IsStartElement(DocumentSerializer.EL.precursor))
            {
                var details = new PrecursorRawDetails
                {
                    _formulaUnlabeled = string.Empty,
                    _labels = null,
                    _nominalAdduct = Adduct.EMPTY,
                    _proposedAdduct = Adduct.EMPTY,
                    _declaredCharge = reader.GetIntAttribute(DocumentSerializer.ATTR.charge),
                    _declaredMz = reader.GetDoubleAttribute(DocumentSerializer.ATTR.precursor_mz),
                    _declaredHeavy = !IsotopeLabelType.LIGHT_NAME.Equals(reader.GetAttribute(DocumentSerializer.ATTR.isotope_label) ?? IsotopeLabelType.LIGHT_NAME)
                };
                var formula = reader.GetAttribute(DocumentSerializer.ATTR.ion_formula);
                if (formula != null)
                {
                    details._formulaUnlabeled = formula.Trim(); // We've seen tailing spaces in the wild
                    string precursorFormula;
                    Adduct precursorAdduct;
                    Molecule precursorMol;
                    if (IonInfo.IsFormulaWithAdduct(formula, out precursorMol, out precursorAdduct, out precursorFormula))
                    {
                        details._formulaUnlabeled = precursorFormula;
                        details._nominalAdduct = precursorAdduct;
                    }
                    else
                    {
                        details._labels = BioMassCalc.MONOISOTOPIC.FindIsotopeLabelsInFormula(details._formulaUnlabeled);
                        details._formulaUnlabeled = BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula(details._formulaUnlabeled);
                    }
                }
                _precursorRawDetails.Add(details);
                reader.ReadToNextSibling(DocumentSerializer.EL.precursor);
            }

            // We want to use the recorded mz values as nearly as possible
            // Inspect the XML to find the expected precision, compare that with current settings
            MzToler = ReadAhead.ElementPrecision(DocumentSerializer.EL.precursor_mz);
            if (MzToler < 1.0)
            {
                MzToler *= 5;
            }
            MzToler = Math.Min(MzToler, settingsMzMatchToler);
        }

        /// <summary>
        /// Read in the current transition groups's precursors and try to modernize the XML so that it's in terms of a common neutral
        /// molecule with adducts, while preserving the precursor mz values
        /// </summary>
        /// <param name="peptide">Neutral molecule associated with current reader position, may be replaced during this call</param>
        /// <returns>Reader positioned at end of current precursor opening, but using cached and possibly altered XML</returns>
        public XmlReader Read(ref Peptide peptide)
        {
            if (_precursorRawDetails.Count == 0)
            {
                return ReadAhead.CreateXmlReader(); // No precursors to process
            }

            // Find a formula for the parent molecule, if possible
            ProposeMoleculeWithCommonFormula(peptide);

            // Deal with mass-only declarations if no formulas were found
            if (string.IsNullOrEmpty(ProposedMolecule.Formula))
            {
                return HandleMassOnlyDeclarations(ref peptide);
            } 

            // Check to see if description already makes sense
            if (_precursorRawDetails.TrueForAll(d => 
                !Adduct.IsNullOrEmpty(d._nominalAdduct) 
                && Math.Abs(d._declaredMz - d._nominalAdduct.MzFromNeutralMass(ProposedMolecule.MonoisotopicMass)) <= MzToler))
            {
                return UpdatePeptideAndInsertAdductsInXML(ref peptide, _precursorRawDetails.Select(d => d._nominalAdduct));
            }

            // See if there are simple adducts that work with common formula
            if (DeriveAdductsForCommonFormula(peptide))
            {
                // Found a molecule that works for all precursors that declared a formula (though that may not be all precursors, users create highly variable documents)
                return UpdatePeptideAndInsertAdductsInXML(ref peptide, _precursorRawDetails.Select(d => d._proposedAdduct));
            }

            // Done adjusting parent molecule, must come up with rest of adducts to make mz and base molecule agree
            return ConsiderMassShiftAdducts(ref peptide);
        }

        private XmlReader ConsiderMassShiftAdducts(ref Peptide peptide)
        {
            var commonFormula = ProposedMolecule.Formula;
            var molMass = ProposedMolecule.MonoisotopicMass;
            foreach (var d in _precursorRawDetails.Where(d => Adduct.IsNullOrEmpty(d._proposedAdduct)))
            {
                if (!Adduct.IsNullOrEmpty(d._nominalAdduct) && Math.Abs(d._declaredMz - d._nominalAdduct.MzFromNeutralMass(molMass)) <= MzToler)
                {
                    d._proposedAdduct = d._nominalAdduct;
                }
            }

            // Final attempt at assigning adducts in the event that no adjustment to neutral molecule has worked out
            foreach (var d in _precursorRawDetails.Where(d => Adduct.IsNullOrEmpty(d._proposedAdduct)))
            {
                // So far we haven't hit on a common molecules + adducts scenario that explains the given mz
                // We're forced to add weird mass shifts to adducts
                for (var retry = 0; retry < 4; retry++)
                {
                    Adduct adduct;
                    switch (retry)
                    {
                        case 0: // Try [M+H] style
                            adduct = Adduct.ProtonatedFromFormulaDiff(d._formulaUnlabeled, commonFormula, d._declaredCharge)
                                .ChangeIsotopeLabels(d._labels);
                            break;
                        case 1: // Try [M+] style
                            adduct = Adduct.FromFormulaDiff(d._formulaUnlabeled, commonFormula, d._declaredCharge)
                                .ChangeIsotopeLabels(d._labels);
                            break;
                        default:
                            adduct = retry == 2
                                ? Adduct.ProtonatedFromFormulaDiff(d._formulaUnlabeled, commonFormula,
                                    d._declaredCharge) // Try [M1.2345+H] style
                                : Adduct.FromFormulaDiff(d._formulaUnlabeled, commonFormula, d._declaredCharge); // Try [M1.2345+] style
                            var ionMass = adduct.MassFromMz(d._declaredMz, MassType.Monoisotopic);
                            var unexplainedMass = ionMass - molMass;
                            adduct = adduct.ChangeIsotopeLabels(unexplainedMass, _mzDecimalPlaces);
                            break;
                    }
                    if (Math.Abs(d._declaredMz - adduct.MzFromNeutralMass(molMass)) <= MzToler)
                    {
                        d._proposedAdduct = adduct;
                        break;
                    }
                }
            }
            Assume.IsTrue(_precursorRawDetails.TrueForAll(d => !Adduct.IsNullOrEmpty(d._proposedAdduct)),
                @"Unable to to deduce adducts and common molecule for " + peptide);

            // We found a satisfactory set of adducts and neutral molecule, update the "peptide" and
            // modify the readahead buffer with the new adduct info
            return UpdatePeptideAndInsertAdductsInXML(ref peptide, _precursorRawDetails.Select(d => d._proposedAdduct));
        }

        private bool DeriveAdductsForCommonFormula(Peptide peptide)
        {
            // Try to come up with a set of adducts to common formula that explain the declared mz values
            var success = false;
            if (_precursorRawDetails.TrueForAll(d => Adduct.IsNullOrEmpty(d._nominalAdduct)))
            {
                // No explicit adducts, just charges.
                // Start with the most common scenario, which is that the user meant (de)protonation
                // See if we can arrive at a common formula ( by adding or removing H) that works with all charges as (de)protonations
                // N.B. the parent molecule may well be completely unrelated to the children, as users were allowed to enter anything they wanted
                var commonFormula = ProposedMolecule.Formula;
                var precursorsWithFormulas = _precursorRawDetails.Where(d => !string.IsNullOrEmpty(d._formulaUnlabeled)).ToList();
                foreach (var detail in precursorsWithFormulas)
                {
                    var revisedCommonFormula = Molecule.AdjustElementCount(commonFormula, BioMassCalc.H, -detail._declaredCharge);
                    var adjustedMolecule = new CustomMolecule(revisedCommonFormula, peptide.CustomMolecule.Name);
                    var mass = adjustedMolecule.MonoisotopicMass;
                    if (precursorsWithFormulas.TrueForAll(d =>
                    {
                        d._proposedAdduct = Adduct.ProtonatedFromFormulaDiff(d._formulaUnlabeled, revisedCommonFormula, d._declaredCharge)
                            .ChangeIsotopeLabels(d._labels);
                        return Math.Abs(d._declaredMz - d._proposedAdduct.MzFromNeutralMass(mass)) <= MzToler;
                    }))
                    {
                        ProposedMolecule = adjustedMolecule;
                        success = true;
                        break;
                    }
                }
                success &= _precursorRawDetails.All(d => !Adduct.IsNullOrEmpty(d._proposedAdduct));
                if (!success)
                {
                    foreach (var d in _precursorRawDetails)
                    {
                        d._proposedAdduct = Adduct.EMPTY;
                    }
                }
            }
            return success;
        }

        private void ProposeMoleculeWithCommonFormula(Peptide peptide)
        {
            // Examine any provided formulas (including parent molecule and/or precursor ions) and find common basis
            ProposedMolecule = peptide.CustomMolecule;
            var precursorsWithFormulas = _precursorRawDetails.Where(d => !string.IsNullOrEmpty(d._formulaUnlabeled)).ToList();
            var parentFormula = peptide.CustomMolecule.UnlabeledFormula;
            var commonFormula = string.IsNullOrEmpty(parentFormula)
                ? BioMassCalc.MONOISOTOPIC.FindFormulaIntersectionUnlabeled(
                    precursorsWithFormulas.Select(p => p._formulaUnlabeled))
                : parentFormula;

            // Check for consistent and correctly declared precursor formula+adduct
            var precursorsWithFormulasAndAdducts = precursorsWithFormulas.Where(d => !Adduct.IsNullOrEmpty(d._nominalAdduct)).ToList();
            if (precursorsWithFormulasAndAdducts.Any() &&
                precursorsWithFormulas.All(
                    d => d._formulaUnlabeled.Equals(precursorsWithFormulasAndAdducts[0]._formulaUnlabeled)))
            {
                commonFormula = precursorsWithFormulasAndAdducts[0]._formulaUnlabeled;
            }

            if (!string.IsNullOrEmpty(commonFormula))
            {
                var parentComposition = Molecule.ParseExpression(commonFormula);
                // Check for children proposing to label more atoms than parent provides, adjust parent as needed
                foreach (var precursor in _precursorRawDetails.Where(d => d._labels != null))
                {
                    foreach (var kvpIsotopeCount in precursor._labels)
                    {
                        var unlabeled = BioMassCalc.GetMonoisotopicSymbol(kvpIsotopeCount.Key);
                        int parentCount;
                        parentComposition.TryGetValue(unlabeled, out parentCount);
                        if (kvpIsotopeCount.Value > parentCount)
                        {
                            // Child proposes to label more of an atom than the parent possesses (seen in the wild) - update the parent
                            commonFormula =
                                Molecule.AdjustElementCount(commonFormula, unlabeled, kvpIsotopeCount.Value - parentCount);
                            parentComposition = Molecule.ParseExpression(commonFormula);
                        }
                    }
                }
                if (!Equals(peptide.CustomMolecule.Formula, commonFormula))
                {
                    ProposedMolecule = new CustomMolecule(commonFormula, peptide.CustomMolecule.Name);
                }
            }
        }

        private XmlReader HandleMassOnlyDeclarations(ref Peptide peptide)
        {
            for (var retry = 0; retry < 4; retry++) // Looking for a common mass and set of adducts that all agree
            {
                var adjustParentMass = retry < 2; // Do/don't try adjusting the neutral mass as if it had proton gain or loss built in
                var assumeProtonated = retry % 2 == 0; // Do/don't try [M+H] vs [M+] 
                foreach (var detail in _precursorRawDetails.OrderBy(d => d._declaredHeavy ? 1 : 0)) // Look at lights first
                {
                    var parentMassAdjustment = adjustParentMass
                        ? Adduct.NonProteomicProtonatedFromCharge(detail._declaredCharge).ApplyToMass(TypedMass.ZERO_MONO_MASSH)
                        : TypedMass.ZERO_MONO_MASSH;
                    var parentMonoisotopicMass = ProposedMolecule.MonoisotopicMass - parentMassAdjustment;
                    if (_precursorRawDetails.TrueForAll(d =>
                    {
                        var adduct = assumeProtonated
                            ? Adduct.NonProteomicProtonatedFromCharge(d._declaredCharge)
                            : Adduct.FromChargeNoMass(d._declaredCharge);
                        if (d._declaredHeavy)
                        {
                            var unexplainedMass = adduct.MassFromMz(d._declaredMz, MassType.Monoisotopic) - parentMonoisotopicMass;
                            adduct = adduct.ChangeIsotopeLabels(unexplainedMass, _mzDecimalPlaces);
                        }
                        d._proposedAdduct = adduct;
                        return Math.Abs(d._declaredMz - d._proposedAdduct.MzFromNeutralMass(parentMonoisotopicMass)) <= MzToler;
                    }))
                    {
                        var parentAverageMass = ProposedMolecule.AverageMass - parentMassAdjustment;
                        ProposedMolecule = new CustomMolecule(parentMonoisotopicMass, parentAverageMass,
                            peptide.CustomMolecule.Name);
                        return UpdatePeptideAndInsertAdductsInXML(ref peptide, _precursorRawDetails.Select(d => d._proposedAdduct));
                    }
                }
            }

            // Unexplained masses can be expressed as mass labels
            if (_precursorRawDetails.TrueForAll(d =>
            {
                var adduct = Adduct.FromChargeNoMass(d._declaredCharge);
                var unexplainedMass = adduct.MassFromMz(d._declaredMz, MassType.Monoisotopic) - ProposedMolecule.MonoisotopicMass;
                d._proposedAdduct = adduct.ChangeIsotopeLabels(unexplainedMass, _mzDecimalPlaces);
                return Math.Abs(d._declaredMz - d._proposedAdduct.MzFromNeutralMass(ProposedMolecule.MonoisotopicMass)) <= MzToler;
            }))
            {
                return UpdatePeptideAndInsertAdductsInXML(ref peptide, _precursorRawDetails.Select(d => d._proposedAdduct));
            }

            // Should never arrive here
            Assume.Fail(@"Unable to to deduce adducts and common molecule for " + peptide);
            return UpdatePeptideAndInsertAdductsInXML(ref peptide, _precursorRawDetails.Select(d => d._nominalAdduct));
        }

        private XmlReader UpdatePeptideAndInsertAdductsInXML(ref Peptide peptide, IEnumerable<Adduct> adducts)
        {
            var updatedPeptide = ReferenceEquals(ProposedMolecule, peptide.CustomMolecule) ? peptide : new Peptide(ProposedMolecule);
            var ionFormulas = adducts.Select(a => updatedPeptide.CustomMolecule.Formula + a.ToString()).ToList();
            ReadAhead.ModifyAttributesInElement(DocumentSerializer.EL.precursor, DocumentSerializer.ATTR.ion_formula,
                ionFormulas); // N.B. "ion_formula" consists of just the adduct for mass only molecules
            peptide = updatedPeptide;
            return ReadAhead.CreateXmlReader(); // Return a reader that uses the updated XML
        }

    }
}
