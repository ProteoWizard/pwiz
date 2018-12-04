﻿/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Read a small molecule transition list in CSV form, where header values are restricted to
    /// those found in SmallMoleculeTransitionListColumnHeaders.KnownHeaders()
    /// </summary>
    public abstract class SmallMoleculeTransitionListReader
    {
        protected IFormatProvider _cultureInfo;
        protected List<Row> Rows { get; set; }
        public abstract void UpdateCellBackingStore(int row, int col, object value);
        public abstract void ShowTransitionError(PasteError error);
        public abstract int ColumnIndex(string columnName);

        private double MzMatchTolerance { get; set; }

        protected SmallMoleculeTransitionListReader()
        {
            Rows = new List<Row>();
        }

        public class Row
        {
            public int Index { get; private set; }
            private SmallMoleculeTransitionListReader _parent;
            protected List<string> _cells { get; set; }

            public Row(SmallMoleculeTransitionListReader parent, int index, List<string> cells)
            {
                _parent = parent;
                Index = index;
                _cells = cells;
            }

            public void UpdateCell(int col, object value)
            {
                if (col < 0)
                    return;
                while (_cells.Count < col)
                {
                    _cells.Add(null);
                }
                _cells[col] = Convert.ToString(value, _parent._cultureInfo); // Update local copy
                _parent.UpdateCellBackingStore(Index, col, value); // Update gridviewcontrol etc
            }

            public string GetCell(int index)
            {
                return index >= 0 ? _cells[index] : null;
            }

            public bool GetCellAsDouble(int index, out double val)
            {
                return Double.TryParse(GetCell(index), NumberStyles.Float, _parent._cultureInfo, out val);
            }

            public void SetCell(int index, string value)
            {
                if (index >= 0)
                    _cells[index] = value;
            }
        }

        private bool RowHasDistinctProductValue(Row row, int productCol, int precursorCol)
        {
            var productVal = row.GetCell(productCol);
            return !string.IsNullOrEmpty(productVal) && !Equals(productVal, row.GetCell(precursorCol));
        }

        public SrmDocument CreateTargets(SrmDocument document, IdentityPath to, out IdentityPath firstAdded)
        {
            firstAdded = null;
            MzMatchTolerance = document.Settings.TransitionSettings.Instrument.MzMatchTolerance;

            // We will accept a completely empty product list as meaning 
            // "these are all precursor transitions"
            var requireProductInfo = false;
            var hasAnyMoleculeMz = Rows.Any(row => !string.IsNullOrEmpty(row.GetCell(INDEX_MOLECULE_MZ)));
            var hasAnyMoleculeFormula = Rows.Any(row => !string.IsNullOrEmpty(row.GetCell(INDEX_MOLECULE_FORMULA)));
            var hasAnyMoleculeCharge = Rows.Any(row => !string.IsNullOrEmpty(row.GetCell(INDEX_MOLECULE_CHARGE)));
            var hasAnyMoleculeAdduct = Rows.Any(row => !string.IsNullOrEmpty(row.GetCell(INDEX_MOLECULE_ADDUCT)));
            foreach (var row in Rows)
            {
                if ((hasAnyMoleculeMz && RowHasDistinctProductValue(row, INDEX_PRODUCT_MZ, INDEX_MOLECULE_MZ)) ||
                    (hasAnyMoleculeFormula &&
                     RowHasDistinctProductValue(row, INDEX_PRODUCT_FORMULA, INDEX_MOLECULE_FORMULA)) ||
                    (hasAnyMoleculeCharge &&
                     RowHasDistinctProductValue(row, INDEX_PRODUCT_CHARGE, INDEX_MOLECULE_CHARGE)) ||
                    (hasAnyMoleculeAdduct &&
                     RowHasDistinctProductValue(row, INDEX_PRODUCT_ADDUCT, INDEX_MOLECULE_ADDUCT)))
                {
                    requireProductInfo = true; // Product list is not completely empty, or not just precursors
                    break;
                }
                // More expensive check to see whether calculated precursor mz matches any declared product mz
                var precursor = ReadPrecursorOrProductColumns(document, row, null); // Get precursor values
                if (precursor != null)
                {
                    try
                    {
                        var product = ReadPrecursorOrProductColumns(document, row, precursor); // Get product values, if available
                        if (product != null && (Math.Abs(precursor.Mz.Value - product.Mz.Value) > MzMatchTolerance))
                        {
                            requireProductInfo = true; // Product list is not completely empty, or not just precursors
                            break;
                        }
                    }
                    catch (LineColNumberedIoException)
                    {
                        // No product info to be had in this line (so this is a precursor) but there may be others, keep looking
                    }
                }
            }
            string defaultPepGroupName = null;
            // For each row in the grid, add to or begin MoleculeGroup|Molecule|TransitionList tree
            foreach (var row in Rows)
            {
                var precursor = ReadPrecursorOrProductColumns(document, row, null); // Get molecule values
                if (precursor == null)
                    return null;
                if (requireProductInfo && ReadPrecursorOrProductColumns(document, row, precursor) == null)
                {
                    return null;
                }
                var adduct = precursor.Adduct;
                var precursorMonoMz = adduct.MzFromNeutralMass(precursor.MonoMass);
                var precursorAverageMz = adduct.MzFromNeutralMass(precursor.AverageMass);

                // Preexisting molecule group?
                bool pepGroupFound = false;
                foreach (var pepGroup in document.MoleculeGroups)
                {
                    var pathPepGroup = new IdentityPath(pepGroup.Id);
                    var groupName = row.GetCell(INDEX_MOLECULE_GROUP);
                    if (string.IsNullOrEmpty(groupName))
                    {
                        groupName = defaultPepGroupName;
                    }

                    if (Equals(pepGroup.Name, groupName))
                    {
                        // Found a molecule group with the same name - can we find an existing transition group to which we can add a transition?
                        pepGroupFound = true;
                        bool pepFound = false;
                        foreach (var pep in pepGroup.SmallMolecules)
                        {
                            var pepPath = new IdentityPath(pathPepGroup, pep.Id);
                            var ionMonoMz =
                                adduct.MzFromNeutralMass(pep.CustomMolecule.MonoisotopicMass, MassType.Monoisotopic);
                            var ionAverageMz =
                                adduct.MzFromNeutralMass(pep.CustomMolecule.AverageMass, MassType.Average);
                            var labelType = precursor.IsotopeLabelType ?? IsotopeLabelType.light;
                            // Match existing molecule if same name
                            if (!string.IsNullOrEmpty(precursor.Name))
                            {
                                pepFound =
                                    Equals(pep.CustomMolecule.Name,
                                        precursor.Name); // If user says they're the same, believe them
                            }
                            else // If no names, look to other cues
                            {
                                // Match existing molecule if same formula or identical formula when stripped of labels
                                pepFound |= !string.IsNullOrEmpty(pep.CustomMolecule.Formula) &&
                                            (Equals(pep.CustomMolecule.Formula, precursor.NeutralFormula) ||
                                             Equals(pep.CustomMolecule.Formula, precursor.Formula) ||
                                             Equals(pep.CustomMolecule.UnlabeledFormula,
                                                 BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula(precursor
                                                     .NeutralFormula)) ||
                                             Equals(pep.CustomMolecule.UnlabeledFormula, precursor.UnlabeledFormula));
                                // Match existing molecule if similar m/z at the precursor charge
                                pepFound |= Math.Abs(ionMonoMz - precursorMonoMz) <= MzMatchTolerance &&
                                            Math.Abs(ionAverageMz - precursorAverageMz) <=
                                            MzMatchTolerance; // (we don't just check mass since we don't have a tolerance value for that)
                                // Or no formula, and different isotope labels or matching label and mz
                                pepFound |= string.IsNullOrEmpty(pep.CustomMolecule.Formula) &&
                                            string.IsNullOrEmpty(precursor.Formula) &&
                                            (!pep.TransitionGroups.Any(t => Equals(t.TransitionGroup.LabelType,
                                                 labelType)) || // First label of this kind
                                             pep.TransitionGroups.Any(
                                                 t => Equals(t.TransitionGroup.LabelType,
                                                          labelType) && // Already seen this label, and
                                                      Math.Abs(precursor.Mz - t.PrecursorMz) <=
                                                      MzMatchTolerance)); // Matches precursor mz of similar labels
                            }
                            if (pepFound)
                            {
                                bool tranGroupFound = false;
                                foreach (var tranGroup in pep.TransitionGroups)
                                {
                                    var pathGroup = new IdentityPath(pepPath, tranGroup.Id);
                                    if (Math.Abs(tranGroup.PrecursorMz - precursor.Mz) <= MzMatchTolerance)
                                    {
                                        tranGroupFound = true;
                                        var tranFound = false;
                                        string errmsg = null;
                                        try
                                        {
                                            var tranNode = GetMoleculeTransition(document, row, pep.Peptide,
                                                tranGroup.TransitionGroup, requireProductInfo);
                                            if (tranNode == null)
                                                return null;
                                            foreach (var tran in tranGroup.Transitions)
                                            {
                                                if (Equals(tranNode.Transition.CustomIon, tran.Transition.CustomIon))
                                                {
                                                    tranFound = true;
                                                    break;
                                                }
                                            }
                                            if (!tranFound)
                                            {
                                                document = (SrmDocument) document.Add(pathGroup, tranNode);
                                                firstAdded = firstAdded ?? pathGroup;
                                            }
                                        }
                                        catch (InvalidDataException x)
                                        {
                                            errmsg = x.Message;
                                        }
                                        catch (InvalidOperationException x) // Adduct handling code can throw these
                                        {
                                            errmsg = x.Message;
                                        }
                                         if (errmsg != null)
                                        {
                                            // Some error we didn't catch in the basic checks
                                            ShowTransitionError(new PasteError
                                            {
                                                Column = 0,
                                                Line = row.Index,
                                                Message = errmsg
                                            });
                                            return null;
                                        }
                                        break;
                                    }
                                }
                                if (!tranGroupFound)
                                {
                                    var node =
                                        GetMoleculeTransitionGroup(document, row, pep.Peptide, requireProductInfo);
                                    if (node == null)
                                        return null;
                                    document = (SrmDocument) document.Add(pepPath, node);
                                    firstAdded = firstAdded ?? pepPath;
                                }
                                break;
                            }
                        }
                        if (!pepFound)
                        {
                            var node = GetMoleculePeptide(document, row, pepGroup.PeptideGroup, requireProductInfo);
                            if (node == null)
                                return null;
                            document = (SrmDocument) document.Add(pathPepGroup, node);
                            firstAdded = firstAdded ?? pathPepGroup;
                        }
                        break;
                    }
                }
                if (!pepGroupFound)
                {
                    var node = GetMoleculePeptideGroup(document, row, requireProductInfo);
                    if (node == null)
                        return null;
                    IdentityPath first;
                    IdentityPath next;
                    document = document.AddPeptideGroups(new[] {node}, false, to, out first, out next);
                    if (string.IsNullOrEmpty(defaultPepGroupName))
                    {
                        defaultPepGroupName = node.Name;
                    }
                    firstAdded = firstAdded ?? first;
                }
            }
            return document;
        }

        private int INDEX_MOLECULE_GROUP
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.moleculeGroup); }
        }

        private int INDEX_MOLECULE_NAME
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.namePrecursor); }
        }

        private int INDEX_PRODUCT_NAME
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.nameProduct); }
        }

        private int INDEX_MOLECULE_FORMULA
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.formulaPrecursor); }
        }

        private int INDEX_MOLECULE_ADDUCT
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.adductPrecursor); }
        }

        private int INDEX_PRODUCT_FORMULA
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.formulaProduct); }
        }

        private int INDEX_PRODUCT_ADDUCT
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.adductProduct); }
        }

        private int INDEX_MOLECULE_MZ
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.mzPrecursor); }
        }

        private int INDEX_PRODUCT_MZ
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.mzProduct); }
        }

        private int INDEX_MOLECULE_CHARGE
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.chargePrecursor); }
        }

        private int INDEX_PRODUCT_CHARGE
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.chargeProduct); }
        }

        private int INDEX_LABEL_TYPE
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.labelType); }
        }

        private int INDEX_RETENTION_TIME
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.rtPrecursor); }
        }

        private int INDEX_RETENTION_TIME_WINDOW
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.rtWindowPrecursor); }
        }

        private int INDEX_COLLISION_ENERGY
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.cePrecursor); }
        }

        private int INDEX_NOTE
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.note); }
        }

        private int INDEX_MOLECULE_DRIFT_TIME_MSEC
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.dtPrecursor); }
        }

        private int INDEX_HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.dtHighEnergyOffset); }
        }

        private int INDEX_MOLECULE_ION_MOBILITY
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.imPrecursor); }
        }

        private int INDEX_MOLECULE_ION_MOBILITY_UNITS
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.imUnits); }
        }

        private int INDEX_HIGH_ENERGY_ION_MOBILITY_OFFSET
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.imHighEnergyOffset); }
        }

        private int INDEX_MOLECULE_CCS
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.ccsPrecursor); }
        }

        private int INDEX_SLENS
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.slens); }
        }

        private int INDEX_CONE_VOLTAGE
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.coneVoltage); }
        }

        private int INDEX_COMPENSATION_VOLTAGE
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.compensationVoltage); }
        }

        private int INDEX_DECLUSTERING_POTENTIAL
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.declusteringPotential); }
        }

        private static int? ValidateFormulaWithMz(SrmDocument document, ref string moleculeFormula, Adduct adduct,
            TypedMass mz, int? charge, out TypedMass monoMass, out TypedMass averageMass, out double? mzCalc)
        {
            // Is the ion's formula the old style where user expected us to add a hydrogen?
            var tolerance = document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            int massShift;
            var ion = new CustomIon(moleculeFormula);
            monoMass = ion.GetMass(MassType.Monoisotopic);
            averageMass = ion.GetMass(MassType.Average);
            var mass = mz.IsMonoIsotopic()
                ? monoMass
                : averageMass;
            // Does given charge, if any, agree with mass and mz?
            if (adduct.IsEmpty && charge.HasValue)
            {
                adduct = Adduct.NonProteomicProtonatedFromCharge(charge.Value);
            }
            mzCalc = adduct.AdductCharge != 0 ? adduct.MzFromNeutralMass(mass) : (double?) null;
            if (mzCalc.HasValue && tolerance >= (Math.Abs(mzCalc.Value - mz)))
            {
                return charge;
            }
            int nearestCharge;
            var calculatedCharge = TransitionCalc.CalcCharge(mass, mz, tolerance, true,
                TransitionGroup.MIN_PRECURSOR_CHARGE,
                TransitionGroup.MAX_PRECURSOR_CHARGE, new int[0],
                TransitionCalc.MassShiftType.none, out massShift, out nearestCharge);
            if (calculatedCharge.IsEmpty)
            {
                // That formula and this mz don't yield a reasonable charge state - try adding an H
                var ion2 = new CustomMolecule(BioMassCalc.AddH(ion.FormulaWithAdductApplied));
                monoMass = ion2.GetMass(MassType.Monoisotopic);
                averageMass = ion2.GetMass(MassType.Average);
                mass = (document.Settings.TransitionSettings.Prediction.FragmentMassType.IsMonoisotopic())
                    ? monoMass
                    : averageMass;
                calculatedCharge = TransitionCalc.CalcCharge(mass, mz, tolerance, true,
                    TransitionGroup.MIN_PRECURSOR_CHARGE,
                    TransitionGroup.MAX_PRECURSOR_CHARGE, new int[0], TransitionCalc.MassShiftType.none, out massShift,
                    out nearestCharge);
                if (!calculatedCharge.IsEmpty)
                {
                    moleculeFormula = ion2.Formula;
                }
                else
                {
                    monoMass = TypedMass.ZERO_MONO_MASSNEUTRAL;
                    averageMass = TypedMass.ZERO_AVERAGE_MASSNEUTRAL;
                }
            }
            charge = calculatedCharge.IsEmpty ? (int?) null : calculatedCharge.AdductCharge;
            return charge;
        }

        private TypedMass ValidateFormulaWithCharge(MassType massType, string moleculeFormula, Adduct adduct,
            out TypedMass monoMass, out TypedMass averageMass)
        {
            var ion = new CustomMolecule(moleculeFormula);
            monoMass = ion.GetMass(MassType.Monoisotopic);
            averageMass = ion.GetMass(MassType.Average);
            return new TypedMass(adduct.MzFromNeutralMass(massType.IsMonoisotopic() ? monoMass : averageMass, massType), massType); // m/z is not actually a mass, of course, but mono vs avg is interesting
        }

        public static string NullForEmpty(string str)
        {
            if (str == null)
                return null;
            return (str.Length == 0) ? null : str;
        }

        private class ParsedIonInfo : IonInfo
        {
            public string Name { get; private set; }
            public string Note { get; private set; }
            public TypedMass Mz { get; private set; } // Not actually a mass, of course, but useful to know if its based on mono vs avg mass
            public Adduct Adduct { get; private set; }
            public TypedMass MonoMass { get; private set; }
            public TypedMass AverageMass { get; private set; }
            public IsotopeLabelType IsotopeLabelType { get; private set; }
            public ExplicitRetentionTimeInfo ExplicitRetentionTime { get; private set; }
            public ExplicitTransitionGroupValues ExplicitTransitionGroupValues { get; private set; }
            public MoleculeAccessionNumbers MoleculeAccessionNumbers { get; private set; } // InChiKey, CAS etc

            public ParsedIonInfo(string name, string formula, Adduct adduct, 
                TypedMass mz, // Not actually a mass, of course, but still useful to know if based on Mono or Average mass
                TypedMass monoMass,
                TypedMass averageMass,
                IsotopeLabelType isotopeLabelType,
                ExplicitRetentionTimeInfo explicitRetentionTime,
                ExplicitTransitionGroupValues explicitTransitionGroupValues,
                string note,
                MoleculeAccessionNumbers accessionNumbers) : base(formula)
            {
                Name = name;
                Adduct = adduct;
                Mz = mz;
                MonoMass = monoMass;
                AverageMass = averageMass;
                IsotopeLabelType = isotopeLabelType;
                ExplicitRetentionTime = explicitRetentionTime;
                ExplicitTransitionGroupValues = explicitTransitionGroupValues;
                Note = note;
                MoleculeAccessionNumbers = accessionNumbers;
            }

            public ParsedIonInfo ChangeNote(string note)
            {
                return ChangeProp(ImClone(this), im =>
                {
                    im.Note = note;
                });
            }

            public CustomMolecule ToCustomMolecule()
            {
                return new CustomMolecule(Formula, MonoMass, AverageMass, Name ?? string.Empty,
                    MoleculeAccessionNumbers);
            }
        }

        private bool ValidateCharge(int? charge, bool getPrecursorColumns, out string errMessage)
        {
            var absCharge = Math.Abs(charge ?? 0);
            if (getPrecursorColumns && absCharge != 0 && (absCharge < TransitionGroup.MIN_PRECURSOR_CHARGE ||
                                                          absCharge > TransitionGroup.MAX_PRECURSOR_CHARGE))
            {
                errMessage = String.Format(
                    Resources.Transition_Validate_Precursor_charge__0__must_be_non_zero_and_between__1__and__2__,
                    charge, -TransitionGroup.MAX_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE);
                return false;
            }
            else if (!getPrecursorColumns && absCharge != 0 &&
                     (absCharge < Transition.MIN_PRODUCT_CHARGE || absCharge > Transition.MAX_PRODUCT_CHARGE))
            {
                errMessage = String.Format(
                    Resources.Transition_Validate_Product_ion_charge__0__must_be_non_zero_and_between__1__and__2__,
                    charge, -Transition.MAX_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE);
                return false;
            }
            errMessage = null;
            return true;
        }

        private MoleculeAccessionNumbers ReadMoleculeAccessionNumberColumns(Row row)
        {
            var moleculeIdKeys = new Dictionary<string, string>();

            var inchikeyCol = ColumnIndex(SmallMoleculeTransitionListColumnHeaders.idInChiKey);
            var inchikey = NullForEmpty(row.GetCell(inchikeyCol));
            if (inchikey != null)
            {
                // Should have form like BQJCRHHNABKAKU-KBQPJGBKSA-N
                inchikey = inchikey.Trim();
                if (inchikey.Length != 27 || inchikey[14] != '-' || inchikey[25] != '-')
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = inchikeyCol,
                        Line = row.Index,
                        Message = string.Format(
                            Resources.SmallMoleculeTransitionListReader_ReadMoleculeIdColumns__0__is_not_a_valid_InChiKey_,
                            inchikey)
                    });
                    return null;
                }
            }
            moleculeIdKeys.Add(MoleculeAccessionNumbers.TagInChiKey, inchikey);

            var hmdbCol = ColumnIndex(SmallMoleculeTransitionListColumnHeaders.idHMDB);
            var hmdb = NullForEmpty(row.GetCell(hmdbCol));
            if (hmdb != null)
            {
                // Should have form like HMDB0001, though we will accept just 00001
                hmdb = hmdb.Trim();
                if (!hmdb.StartsWith(MoleculeAccessionNumbers.TagHMDB) && !hmdb.All(char.IsDigit))
                {
                    hmdb = MoleculeAccessionNumbers.TagHMDB + hmdb;
                }
                if ((hmdb.Length < 5) || !hmdb.Skip(4).All(char.IsDigit))
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = hmdbCol,
                        Line = row.Index,
                        Message =
                            string.Format(
                                Resources.SmallMoleculeTransitionListReader_ReadMoleculeIdColumns__0__is_not_a_valid_HMDB_identifier_,
                                hmdb)
                    });
                    return null;
                }
                moleculeIdKeys.Add(MoleculeAccessionNumbers.TagHMDB, hmdb.Substring(4));
            }

            var inchiCol = ColumnIndex(SmallMoleculeTransitionListColumnHeaders.idInChi);
            var inchi = NullForEmpty(row.GetCell(inchiCol));
            if (inchi != null)
            {
                // Should have form like "InChI=1S/C4H8O3/c1-3(5)2-4(6)7/h3,5H,2H2,1H3,(H,6,7)/t3-/m1/s", 
                // though we will accept just "1S/C4H8O3/c1-3(5)2-4(6)7/h3,5H,2H2,1H3,(H,6,7)/t3-/m1/s"
                inchi = inchi.Trim();
                if (!inchi.StartsWith(MoleculeAccessionNumbers.TagInChI + @"="))
                {
                    inchi = MoleculeAccessionNumbers.TagInChI + @"=" + inchi;
                }
                if (inchi.Length < 6 || inchi.Count(c => c == '/') < 2)
                {
                    // CONSIDER(bspratt) more robust regex check on this?
                    ShowTransitionError(new PasteError
                    {
                        Column = inchiCol,
                        Line = row.Index,
                        Message =
                            string.Format(
                                Resources
                                    .SmallMoleculeTransitionListReader_ReadMoleculeIdColumns__0__is_not_a_valid_InChI_identifier_,
                                inchi)
                    });
                    return null;
                }
                moleculeIdKeys.Add(MoleculeAccessionNumbers.TagInChI, inchi.Substring(6));
            }

            var casCol = ColumnIndex(SmallMoleculeTransitionListColumnHeaders.idCAS);
            var cas = NullForEmpty(row.GetCell(casCol));
            if (cas != null)
            {
                // Should have form like "123-45-6", 
                var parts = cas.Trim().Split('-');
                if (parts.Length != 3 || parts.Any(part => !part.All(char.IsDigit)))
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = casCol,
                        Line = row.Index,
                        Message =
                            string.Format(
                                Resources
                                    .SmallMoleculeTransitionListReader_ReadMoleculeIdColumns__0__is_not_a_valid_CAS_registry_number_,
                                cas)
                    });
                    return null;
                }
                moleculeIdKeys.Add(MoleculeAccessionNumbers.TagCAS, cas);
            }

            var smilesCol = ColumnIndex(SmallMoleculeTransitionListColumnHeaders.idSMILES);
            var smiles = NullForEmpty(row.GetCell(smilesCol));
            if (smiles != null)
            {
                // Should have form like CCc1nn(C)c2c(=O)[nH]c(nc12)c3cc(ccc3OCC)S(=O)(=O)N4CCN(C)CC4 but we'll accept anything for now, having no proper parser
                smiles = smiles.Trim();
                moleculeIdKeys.Add(MoleculeAccessionNumbers.TagSMILES, smiles);
            }

            return !moleculeIdKeys.Any()
                ? MoleculeAccessionNumbers.EMPTY
                : new MoleculeAccessionNumbers(moleculeIdKeys);
        }

        public static eIonMobilityUnits IonMobilityUnitsFromAttributeValue(string xmlAttributeValue)
        {
            return string.IsNullOrEmpty(xmlAttributeValue) ?
                eIonMobilityUnits.none :
                TypeSafeEnum.Parse<eIonMobilityUnits>(xmlAttributeValue);
        }
        

        // Recognize XML attribute values, enum strings, and various other synonyms
        public static readonly Dictionary<string, eIonMobilityUnits> IonMobilityUnitsSynonyms =
             Enum.GetValues(typeof(eIonMobilityUnits)).Cast<eIonMobilityUnits>().ToDictionary(e => e.ToString(), e => e)
            .Concat(new Dictionary<string, eIonMobilityUnits> {
            { @"msec", eIonMobilityUnits.drift_time_msec },
            { @"Vsec/cm2", eIonMobilityUnits.inverse_K0_Vsec_per_cm2 },
            { @"Vsec/cm^2", eIonMobilityUnits.inverse_K0_Vsec_per_cm2 },
            { @"1/K0", eIonMobilityUnits.inverse_K0_Vsec_per_cm2 }
            }).ToDictionary(x => x.Key, x=> x.Value);

        public static string GetAcceptedIonMobilityUnitsString()
        {
            return string.Join(@", ", IonMobilityUnitsSynonyms.Keys);
        }



        // We need some combination of:
        //  Formula and mz
        //  Formula and charge
        //  mz and charge
        private ParsedIonInfo ReadPrecursorOrProductColumns(SrmDocument document,
            Row row,
            ParsedIonInfo precursorInfo)
        {

            var getPrecursorColumns = precursorInfo == null;
            int indexName = getPrecursorColumns ? INDEX_MOLECULE_NAME : INDEX_PRODUCT_NAME;
            int indexFormula = getPrecursorColumns ? INDEX_MOLECULE_FORMULA : INDEX_PRODUCT_FORMULA;
            int indexAdduct = getPrecursorColumns ? INDEX_MOLECULE_ADDUCT : INDEX_PRODUCT_ADDUCT;
            int indexMz = getPrecursorColumns ? INDEX_MOLECULE_MZ : INDEX_PRODUCT_MZ;
            int indexCharge = getPrecursorColumns ? INDEX_MOLECULE_CHARGE : INDEX_PRODUCT_CHARGE;
            var name = NullForEmpty(row.GetCell(indexName));
            var formula = NullForEmpty(row.GetCell(indexFormula));
            var adductText = NullForEmpty(row.GetCell(indexAdduct));
            var note = NullForEmpty(row.GetCell(INDEX_NOTE));
            // TODO(bspratt) use CAS or HMDB etc lookup to fill in missing inchikey - and use any to fill in formula
            var moleculeID = ReadMoleculeAccessionNumberColumns(row); 
            IsotopeLabelType isotopeLabelType = null;
            bool badMz = false;
            var mzType = getPrecursorColumns 
                ? document.Settings.TransitionSettings.Prediction.PrecursorMassType
                : document.Settings.TransitionSettings.Prediction.FragmentMassType;
            double mzParsed;
            if (!row.GetCellAsDouble(indexMz, out mzParsed))
            {
                if (!String.IsNullOrEmpty(row.GetCell(indexMz)))
                {
                    badMz = true;
                }
                mzParsed = 0;
            }
            var mz = new TypedMass(mzParsed, mzType); // mz is not actually a mass, of course, but we want to track mass type it was calculated from
            if ((mz < 0) || badMz)
            {
                ShowTransitionError(new PasteError
                {
                    Column = indexMz,
                    Line = row.Index,
                    Message = String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_m_z_value__0_, row.GetCell(indexMz))
                });
                return null;
            }
            int? charge = null;
            var adduct = Adduct.EMPTY;
            int trycharge;
            if (Int32.TryParse(row.GetCell(indexCharge), out trycharge))
                charge = trycharge;
            else if (!String.IsNullOrEmpty(row.GetCell(indexCharge)))
            {
                Adduct test;
                if (Adduct.TryParse(row.GetCell(indexCharge), out test))
                {
                    // Adduct formula in charge column, let's allow it
                    adduct = test;
                    charge = adduct.AdductCharge;
                }
                else
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = indexCharge,
                        Line = row.Index,
                        Message = String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_charge_value__0_, row.GetCell(indexCharge))
                    });
                    return null;
                }
            }
            double dtmp;
            double? collisionEnergy = null;
            double? slens = null;
            double? coneVoltage = null;
            double? retentionTime = null;
            double? retentionTimeWindow = null;
            double? declusteringPotential = null;
            double? compensationVoltage = null;
            if (getPrecursorColumns)
            {
                // Do we have any molecule IDs?
                moleculeID = ReadMoleculeAccessionNumberColumns(row);
                if (moleculeID == null)
                {
                    return null; // Some error occurred
                }
                var label = NullForEmpty(row.GetCell(INDEX_LABEL_TYPE));
                if (label != null)
                {
                    var typedMods = document.Settings.PeptideSettings.Modifications.GetModificationsByName(label);
                    if (typedMods == null)
                    {
                        ShowTransitionError(new PasteError
                        {
                            Column = INDEX_LABEL_TYPE,
                            Line = row.Index,
                            Message = string.Format(Resources.SrmDocument_ReadLabelType_The_isotope_modification_type__0__does_not_exist_in_the_document_settings, label)
                        });
                        return null;
                    }
                    isotopeLabelType = typedMods.LabelType;
                }
                if (row.GetCellAsDouble(INDEX_COLLISION_ENERGY, out dtmp))
                    collisionEnergy = dtmp;
                else if (!String.IsNullOrEmpty(row.GetCell(INDEX_COLLISION_ENERGY)))
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = INDEX_COLLISION_ENERGY,
                        Line = row.Index,
                        Message = String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_collision_energy_value__0_, row.GetCell(INDEX_COLLISION_ENERGY))
                    });
                    return null;
                }
                if (row.GetCellAsDouble(INDEX_SLENS, out dtmp))
                    slens = dtmp;
                else if (!String.IsNullOrEmpty(row.GetCell(INDEX_SLENS)))
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = INDEX_SLENS,
                        Line = row.Index,
                        Message = String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_S_Lens_value__0_,row.GetCell(INDEX_SLENS))
                    });
                    return null;
                }
                if (row.GetCellAsDouble(INDEX_CONE_VOLTAGE, out dtmp))
                    coneVoltage = dtmp;
                else if (!String.IsNullOrEmpty(row.GetCell(INDEX_CONE_VOLTAGE)))
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = INDEX_CONE_VOLTAGE,
                        Line = row.Index,
                        Message = String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_cone_voltage_value__0_, row.GetCell(INDEX_CONE_VOLTAGE))
                    });
                    return null;
                }
                if (row.GetCellAsDouble(INDEX_DECLUSTERING_POTENTIAL, out dtmp))
                    declusteringPotential = dtmp;
                else if (!String.IsNullOrEmpty(row.GetCell(INDEX_DECLUSTERING_POTENTIAL)))
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = INDEX_DECLUSTERING_POTENTIAL,
                        Line = row.Index,
                        Message = String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_declustering_potential__0_, row.GetCell(INDEX_DECLUSTERING_POTENTIAL))
                    });
                    return null;
                }
                if (row.GetCellAsDouble(INDEX_COMPENSATION_VOLTAGE, out dtmp))
                    compensationVoltage = dtmp;
                else if (!String.IsNullOrEmpty(row.GetCell(INDEX_COMPENSATION_VOLTAGE)))
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = INDEX_COMPENSATION_VOLTAGE,
                        Line = row.Index,
                        Message = String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_compensation_voltage__0_, row.GetCell(INDEX_COMPENSATION_VOLTAGE))
                    });
                    return null;
                }
                if (row.GetCellAsDouble(INDEX_RETENTION_TIME, out dtmp))
                    retentionTime = dtmp;
                else if (!String.IsNullOrEmpty(row.GetCell(INDEX_RETENTION_TIME)))
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = INDEX_RETENTION_TIME,
                        Line = row.Index,
                        Message = String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_retention_time_value__0_, row.GetCell(INDEX_RETENTION_TIME))
                    });
                    return null;
                }
                if (row.GetCellAsDouble(INDEX_RETENTION_TIME_WINDOW, out dtmp))
                {
                    retentionTimeWindow = dtmp;
                    if (!retentionTime.HasValue)
                    {
                        ShowTransitionError(new PasteError
                        {
                            Column = INDEX_RETENTION_TIME_WINDOW,
                            Line = row.Index,
                            Message = Resources.Peptide_ExplicitRetentionTimeWindow_Explicit_retention_time_window_requires_an_explicit_retention_time_value_
                        });
                        return null;
                    }
                }
                else if (!String.IsNullOrEmpty(row.GetCell(INDEX_RETENTION_TIME_WINDOW)))
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = INDEX_RETENTION_TIME_WINDOW,
                        Line = row.Index,
                        Message = String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_retention_time_window_value__0_, row.GetCell(INDEX_RETENTION_TIME_WINDOW))
                    });
                    return null;
                }
            }
            double? ionMobility = null;
            var ionMobilityUnits = eIonMobilityUnits.none;

            if (row.GetCellAsDouble(INDEX_MOLECULE_DRIFT_TIME_MSEC, out dtmp))
            {
                ionMobility = dtmp;
                ionMobilityUnits = eIonMobilityUnits.drift_time_msec;
            }
            else if (!String.IsNullOrEmpty(row.GetCell(INDEX_MOLECULE_DRIFT_TIME_MSEC)))
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_MOLECULE_DRIFT_TIME_MSEC,
                    Line = row.Index,
                    Message = String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_drift_time_value__0_, row.GetCell(INDEX_MOLECULE_DRIFT_TIME_MSEC))
                });
                return null;
            }
            double? ionMobilityHighEnergyOffset = null;
            if (row.GetCellAsDouble(INDEX_HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC, out dtmp))
            {
                ionMobilityHighEnergyOffset = dtmp;
                ionMobilityUnits = eIonMobilityUnits.drift_time_msec;
            }
            else if (!String.IsNullOrEmpty(row.GetCell(INDEX_HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC)))
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC,
                    Line = row.Index,
                    Message = String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_drift_time_high_energy_offset_value__0_, row.GetCell(INDEX_HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC))
                });
                return null;
            }
            string unitsIM = row.GetCell(INDEX_MOLECULE_ION_MOBILITY_UNITS);
            if (!string.IsNullOrEmpty(unitsIM))
            {
                if (!IonMobilityUnitsSynonyms.TryGetValue(unitsIM.Trim(), out ionMobilityUnits))
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = INDEX_MOLECULE_ION_MOBILITY_UNITS,
                        Line = row.Index,
                        Message = String.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_ion_mobility_units_value__0___accepted_values_are__1__, row.GetCell(INDEX_MOLECULE_ION_MOBILITY_UNITS), GetAcceptedIonMobilityUnitsString())
                    });
                    return null;
                }
            }

            if (row.GetCellAsDouble(INDEX_MOLECULE_ION_MOBILITY, out dtmp))
            {
                ionMobility = dtmp;
            }
            else if (!String.IsNullOrEmpty(row.GetCell(INDEX_MOLECULE_ION_MOBILITY)))
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_MOLECULE_ION_MOBILITY,
                    Line = row.Index,
                    Message = String.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_ion_mobility_value__0_, row.GetCell(INDEX_MOLECULE_ION_MOBILITY))
                });
                return null;
            }
            if (row.GetCellAsDouble(INDEX_HIGH_ENERGY_ION_MOBILITY_OFFSET, out dtmp))
            {
                ionMobilityHighEnergyOffset = dtmp;
            }
            else if (!String.IsNullOrEmpty(row.GetCell(INDEX_HIGH_ENERGY_ION_MOBILITY_OFFSET)))
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_HIGH_ENERGY_ION_MOBILITY_OFFSET,
                    Line = row.Index,
                    Message = String.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_ion_mobility_high_energy_offset_value__0_, row.GetCell(INDEX_HIGH_ENERGY_ION_MOBILITY_OFFSET))
                });
                return null;
            }
            double? ccsPrecursor = null;
            if (row.GetCellAsDouble(INDEX_MOLECULE_CCS, out dtmp))
                ccsPrecursor = dtmp;
            else if (!String.IsNullOrEmpty(row.GetCell(INDEX_MOLECULE_CCS)))
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_MOLECULE_CCS,
                    Line = row.Index,
                    Message = String.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_collisional_cross_section_value__0_, row.GetCell(INDEX_MOLECULE_CCS))
                });
                return null;
            }
            string errMessage = String.Format(getPrecursorColumns
                ? Resources.PasteDlg_ValidateEntry_Error_on_line__0___Precursor_needs_values_for_any_two_of__Formula__m_z_or_Charge_
                : Resources.PasteDlg_ValidateEntry_Error_on_line__0___Product_needs_values_for_any_two_of__Formula__m_z_or_Charge_, row.Index + 1);
            // Do we have an adduct description?  If so, pull charge from that.
            if ((!string.IsNullOrEmpty(formula) && formula.Contains('[') && formula.Contains(']')) || !string.IsNullOrEmpty(adductText))
            {
                if (!string.IsNullOrEmpty(formula))
                {
                    var parts = formula.Split('[');
                    var formulaAdduct = formula.Substring(parts[0].Length);
                    if (string.IsNullOrEmpty(adductText))
                    {
                        adductText = formulaAdduct;
                    }
                    else if (!string.IsNullOrEmpty(formulaAdduct) &&
                        // ReSharper disable LocalizableElement
                        !Equals(adductText.Replace("[", "").Replace("]", ""), formulaAdduct.Replace("[", "").Replace("]", "")))
                        // ReSharper restore LocalizableElement
                    {
                        ShowTransitionError(new PasteError
                        {
                            Column = indexAdduct,
                            Line = row.Index,
                            Message = Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Formula_already_contains_an_adduct_description__and_it_does_not_match_
                        });
                        return null;
                    }
                    formula = parts[0];
                }
                try
                {
                    adduct = Adduct.FromStringAssumeChargeOnly(adductText);
                    IonInfo.ApplyAdductToFormula(formula??string.Empty, adduct); // Just to see if it throws
                }
                catch (InvalidOperationException x)
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = indexFormula,
                        Line = row.Index,
                        Message = x.Message
                    });
                    return null;
                }
                if (charge.HasValue && charge.Value != adduct.AdductCharge)
                {
                    // Explict charge disagrees with adduct - is this because adduct charge is not recognized?
                    if (adduct.AdductCharge == 0)
                    {
                        // Update the adduct to contain the explicit charge
                        adduct = adduct.ChangeCharge(charge.Value);
                    }
                    else
                    {
                        ShowTransitionError(new PasteError
                        {
                            Column = indexAdduct >=0 ? indexAdduct : indexFormula,
                            Line = row.Index,
                            Message = string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Adduct__0__charge__1__does_not_agree_with_declared_charge__2_, adductText, adduct.AdductCharge, charge.Value)
                        });
                        return null;
                    }
                }
                else
                {
                    charge = adduct.AdductCharge;
                }
                if (!ValidateCharge(charge, getPrecursorColumns, out errMessage))
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = indexAdduct >=0 ? indexAdduct : indexFormula,
                        Line = row.Index,
                        Message = errMessage
                    });
                    return null;
                }
            }
            int errColumn = indexFormula;
            int countValues = 0;
            if (charge.HasValue && charge.Value != 0)
            {
                countValues++;
                if (adduct.IsEmpty)
                {
                    // When no adduct is given, either it's implied (de)protonation, or formula is inherently charged. Formula and mz are a clue.
                    adduct = DetermineAdductFromFormulaChargeAndMz(formula, charge.Value, mz);
                    row.SetCell(indexAdduct, adduct.AdductFormula);
                }
            }
            if (mz > 0)
                countValues++;
            if (NullForEmpty(formula) != null)
                countValues++;
            if (countValues == 0 && !getPrecursorColumns &&
                (string.IsNullOrEmpty(name) || Equals(precursorInfo.Name, name)))
            {
                // No product info found in this row, assume that this is a precursor declaration
                return precursorInfo.ChangeNote(note);
            }
            if (countValues >= 2) // Do we have at least 2 of charge, mz, formula?
            {
                TypedMass monoMass;
                TypedMass averageMmass;
                if (ionMobility.HasValue || ccsPrecursor.HasValue)
                {
                    if (!ionMobilityHighEnergyOffset.HasValue)
                    {
                        ionMobilityHighEnergyOffset = 0;
                    }
                }
                else
                {
                    ionMobilityHighEnergyOffset = null; // Offset without a base value isn't useful
                }
                if (ionMobility.HasValue && ionMobilityUnits == eIonMobilityUnits.none)
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = INDEX_MOLECULE_ION_MOBILITY,
                        Line = row.Index,
                        Message = Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Missing_ion_mobility_units
                    });
                    return null;

                }
                var retentionTimeInfo = retentionTime.HasValue
                    ? new ExplicitRetentionTimeInfo(retentionTime.Value, retentionTimeWindow)
                    : null;
                var explicitTransitionGroupValues = new ExplicitTransitionGroupValues(collisionEnergy, ionMobility, ionMobilityHighEnergyOffset, ionMobilityUnits, ccsPrecursor, slens,
                    coneVoltage, declusteringPotential, compensationVoltage);
                var massOk = true;
                var massTooLow = false;
                string massErrMsg = null;
                if (!ValidateCharge(charge, getPrecursorColumns, out errMessage))
                {
                    errColumn = indexCharge;
                }
                else if (NullForEmpty(formula) != null)
                {
                    // We have a formula
                    try
                    {
                        // Can we infer a heavy label from the formula if none specified?
                        if (getPrecursorColumns && isotopeLabelType == null) 
                        {
                            var ion = new IonInfo(formula, adduct);
                            if (!IonInfo.EquivalentFormulas(ion.FormulaWithAdductApplied, ion.UnlabeledFormula)) // Formula+adduct contained some heavy isotopes
                            {
                                isotopeLabelType = IsotopeLabelType.heavy;
                                if (INDEX_LABEL_TYPE >= 0)
                                {
                                    row.UpdateCell(INDEX_LABEL_TYPE, isotopeLabelType.ToString());
                                }
                            }
                        }
                        // If formula contains isotope info, move it to the adduct
                        if (!adduct.IsEmpty)
                        {
                            var labels = BioMassCalc.MONOISOTOPIC.FindIsotopeLabelsInFormula(formula);
                            if (labels.Any())
                            {
                                adduct = adduct.ChangeIsotopeLabels(labels);
                                formula = BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula(formula);
                                row.SetCell(indexFormula, formula);
                                row.SetCell(indexAdduct, adduct.AsFormulaOrSignedInt());
                            }
                        }
                        if (mz > 0)
                        {
                            // Is the ion's formula the old style where user expected us to add a hydrogen? 
                            double? mzCalc;
                            charge = ValidateFormulaWithMz(document, ref formula, adduct,  mz, charge, out monoMass, out averageMmass, out mzCalc);
                            row.SetCell(indexFormula, formula);
                            massOk = monoMass < CustomMolecule.MAX_MASS && averageMmass < CustomMolecule.MAX_MASS &&
                                     !(massTooLow = charge.HasValue && (monoMass < CustomMolecule.MIN_MASS || averageMmass < CustomMolecule.MIN_MASS)); // Null charge => masses are 0 but meaningless
                            if (adduct.IsEmpty && charge.HasValue)
                            {
                                adduct = Adduct.FromChargeProtonated(charge);
                            }
                            if (massOk)
                            {
                                if (charge.HasValue)
                                {
                                    row.UpdateCell(indexCharge, charge.Value);
                                    return new ParsedIonInfo(name, formula, adduct, mz, monoMass, averageMmass, isotopeLabelType, retentionTimeInfo, explicitTransitionGroupValues, note, moleculeID);
                                }
                                else if (mzCalc.HasValue)
                                {
                                    // There was an initial charge value, but it didn't make sense with formula and proposed mz
                                    errMessage = String.Format(getPrecursorColumns
                                        ? Resources.PasteDlg_ReadPrecursorOrProductColumns_Error_on_line__0___Precursor_m_z__1__does_not_agree_with_value__2__as_calculated_from_ion_formula_and_charge_state__delta____3___Transition_Settings___Instrument___Method_match_tolerance_m_z____4_____Correct_the_m_z_value_in_the_table__or_leave_it_blank_and_Skyline_will_calculate_it_for_you_
                                        : Resources.PasteDlg_ReadPrecursorOrProductColumns_Error_on_line__0___Product_m_z__1__does_not_agree_with_value__2__as_calculated_from_ion_formula_and_charge_state__delta____3___Transition_Settings___Instrument___Method_match_tolerance_m_z____4_____Correct_the_m_z_value_in_the_table__or_leave_it_blank_and_Skyline_will_calculate_it_for_you_,
                                        row.Index + 1, (float)mz, (float)mzCalc.Value, (float)(mzCalc.Value - mz), (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance);
                                    errColumn = indexMz;
                                }
                                else
                                {
                                    // No charge state given, and mz makes no sense with formula
                                    errMessage = String.Format(getPrecursorColumns
                                        ? Resources.PasteDlg_ValidateEntry_Error_on_line__0___Precursor_formula_and_m_z_value_do_not_agree_for_any_charge_state_
                                        : Resources.PasteDlg_ValidateEntry_Error_on_line__0___Product_formula_and_m_z_value_do_not_agree_for_any_charge_state_, row.Index + 1);
                                    errColumn = indexMz;
                                }
                            }
                        }
                        else if (charge.HasValue)
                        {
                            if (adduct.IsEmpty)
                            {
                                adduct = Adduct.FromChargeProtonated(charge);
                            }
                            // Get the mass from the formula, and mz from that and adduct
                            mz = ValidateFormulaWithCharge(mzType, formula, adduct, out monoMass, out averageMmass);
                            massOk = !((monoMass >= CustomMolecule.MAX_MASS || averageMmass >= CustomMolecule.MAX_MASS)) &&
                                     !(massTooLow = (monoMass < CustomMolecule.MIN_MASS || averageMmass < CustomMolecule.MIN_MASS));
                            row.UpdateCell(indexMz, mz);
                            if (massOk)
                                return new ParsedIonInfo(name, formula, adduct, mz, monoMass, averageMmass, isotopeLabelType, retentionTimeInfo, explicitTransitionGroupValues, note, moleculeID);
                        }
                    }
                    catch (InvalidDataException x)
                    {
                        massErrMsg = x.Message;
                    }
                    catch (InvalidOperationException x)  // Adduct handling code can throw these
                    {
                        massErrMsg = x.Message;
                    }
                    if (massErrMsg != null)
                    {
                        massOk = false;
                    }
                }
                else if (mz != 0 && !adduct.IsEmpty)
                {
                    // No formula, just use charge and m/z
                    monoMass = adduct.MassFromMz(mz, MassType.Monoisotopic);
                    averageMmass =  adduct.MassFromMz(mz, MassType.Average);
                    massOk = monoMass < CustomMolecule.MAX_MASS && averageMmass < CustomMolecule.MAX_MASS &&
                             !(massTooLow = (monoMass < CustomMolecule.MIN_MASS || averageMmass < CustomMolecule.MIN_MASS));
                    errColumn = indexMz;
                    if (massOk)
                        return new ParsedIonInfo(name, formula, adduct, mz, monoMass, averageMmass, isotopeLabelType, retentionTimeInfo, explicitTransitionGroupValues, note, moleculeID);
                }
                if (massTooLow)
                {
                    errMessage = massErrMsg ?? String.Format(
                        Resources
                            .EditCustomMoleculeDlg_OkDialog_Custom_molecules_must_have_a_mass_greater_than_or_equal_to__0__,
                        CustomMolecule.MIN_MASS);
                }
                else if (!massOk)
                {
                    errMessage = massErrMsg ?? String.Format(
                        Resources
                            .EditCustomMoleculeDlg_OkDialog_Custom_molecules_must_have_a_mass_less_than_or_equal_to__0__,
                        CustomMolecule.MAX_MASS);
                }
            }
            if (string.IsNullOrEmpty(errMessage))
            {
                if (!string.IsNullOrEmpty(adduct.AdductFormula) && adduct.AdductCharge == 0)
                {
                    // Adduct with unknown charge state
                    errMessage =
                        string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Cannot_derive_charge_from_adduct_description___0____Use_the_corresponding_Charge_column_to_set_this_explicitly__or_change_the_adduct_description_as_needed_, adduct.AdductFormula);
                }
                else
                {
                    // Don't just leave it blank
                    errMessage = Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_unknown_error;
                }
            }
            ShowTransitionError(new PasteError
            {
                Column = errColumn,
                Line = row.Index,
                Message = errMessage
            });
            return null;
        }

        // When a charge but no adduct is given, either it's implied (de)protonation, or formula is inherently charged. Formula and mz are a clue.
        private static Adduct DetermineAdductFromFormulaChargeAndMz(string formula, int charge, TypedMass mz)
        {
            Adduct adduct;
            if (string.IsNullOrEmpty(formula))
            {
                adduct = Adduct.FromChargeNoMass(charge); // If all we have is mz, don't make guesses at proton gain or loss
            }
            else if (mz == 0)
            {
                adduct = Adduct.NonProteomicProtonatedFromCharge(charge); // Formula but no mz, just assume protonation
            }
            else
            {
                // Get mass from formula, then look at declared mz to decide if protonation is implied by charge
                var adductH = Adduct.NonProteomicProtonatedFromCharge(charge); // [M-H] etc
                var adductM = Adduct.FromChargeNoMass(charge); // [M-] etc
                var ionH = new CustomMolecule(adductH.ApplyToFormula(formula));
                var ionM = new CustomMolecule(adductM.ApplyToFormula(formula));
                var mass = mz * Math.Abs(charge);
                adduct = Math.Abs(ionH.GetMass(MassType.Monoisotopic) - mass) <
                         Math.Abs(ionM.GetMass(MassType.Monoisotopic) - mass)
                    ? adductH
                    : adductM;
            }

            return adduct;
        }

        private PeptideGroupDocNode GetMoleculePeptideGroup(SrmDocument document, Row row, bool requireProductInfo)
        {
            var pepGroup = new PeptideGroup();
            var pep = GetMoleculePeptide(document, row, pepGroup, requireProductInfo);
            if (pep == null)
                return null;
            var name = row.GetCell(INDEX_MOLECULE_GROUP);
            if (String.IsNullOrEmpty(name))
                name = document.GetSmallMoleculeGroupId();
            var metadata = new ProteinMetadata(name, String.Empty).SetWebSearchCompleted();  // FUTURE: some kind of lookup for small molecules
            return new PeptideGroupDocNode(pepGroup, metadata, new[] { pep });
        }

        private PeptideDocNode GetMoleculePeptide(SrmDocument document, Row row, PeptideGroup group, bool requireProductInfo)
        {

            CustomMolecule molecule;
            ParsedIonInfo parsedIonInfo;
            try
            {
                parsedIonInfo = ReadPrecursorOrProductColumns(document, row, null); // Re-read the precursor columns
                if (parsedIonInfo == null)
                    return null; // Some failure, but exception was already handled
                // Identify items with same formula and different adducts
                var neutralFormula = parsedIonInfo.NeutralFormula;
                var shortName = row.GetCell(INDEX_MOLECULE_NAME);
                if (!string.IsNullOrEmpty(neutralFormula))
                {
                    molecule = new CustomMolecule(neutralFormula, shortName, parsedIonInfo.MoleculeAccessionNumbers);
                }
                else
                {
                    molecule = new CustomMolecule(parsedIonInfo.Formula, parsedIonInfo.MonoMass, parsedIonInfo.AverageMass, shortName, parsedIonInfo.MoleculeAccessionNumbers);
                }
            }
            catch (ArgumentException e)
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_MOLECULE_FORMULA,
                    Line = row.Index,
                    Message = e.Message
                });
                return null;
            }
            try
            {
                var pep = new Peptide(molecule);
                var tranGroup = GetMoleculeTransitionGroup(document, row, pep, requireProductInfo);
                if (tranGroup == null)
                    return null;
                return new PeptideDocNode(pep, document.Settings, null, null, parsedIonInfo.ExplicitRetentionTime, new[] { tranGroup }, true);
            }
            catch (InvalidOperationException e)
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_MOLECULE_FORMULA,
                    Line = row.Index,
                    Message = e.Message
                });
                return null;
            }
        }

        private TransitionGroupDocNode GetMoleculeTransitionGroup(SrmDocument document, Row row, Peptide pep, bool requireProductInfo)
        {
            var moleculeInfo = ReadPrecursorOrProductColumns(document, row, null); // Re-read the precursor columns
            if (moleculeInfo == null)
            {
                return null; // Some parsing error, user has already been notified
            }
            if (!document.Settings.TransitionSettings.IsMeasurablePrecursor(moleculeInfo.Mz))
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_MOLECULE_MZ,
                    Line = row.Index,
                    Message = String.Format(Resources.PasteDlg_GetMoleculeTransitionGroup_The_precursor_m_z__0__is_not_measureable_with_your_current_instrument_settings_, moleculeInfo.Mz)
                });
                return null;
            }

            var customIon = moleculeInfo.ToCustomMolecule();
            var isotopeLabelType = moleculeInfo.IsotopeLabelType ?? IsotopeLabelType.light;
            Assume.IsTrue(Equals(pep.CustomMolecule.PrimaryEquivalenceKey, customIon.PrimaryEquivalenceKey));  // TODO(bspratt) error handling here
            var adduct = moleculeInfo.Adduct;
            if (!Equals(pep.CustomMolecule.MonoisotopicMass, customIon.MonoisotopicMass) && !adduct.HasIsotopeLabels)
            {
                // Some kind of undescribed isotope labeling going on
                if ((!string.IsNullOrEmpty(pep.CustomMolecule.Formula) && Equals(pep.CustomMolecule.Formula, customIon.Formula)) ||
                    (string.IsNullOrEmpty(pep.CustomMolecule.Formula) && string.IsNullOrEmpty(customIon.Formula)))
                {
                    // No formula for label, describe as mass
                    var labelMass = customIon.MonoisotopicMass - pep.CustomMolecule.MonoisotopicMass;
                    if (labelMass > 0)
                    {
                        adduct = adduct.ChangeIsotopeLabels(labelMass); // Isostopes add weight
                        isotopeLabelType = moleculeInfo.IsotopeLabelType ?? IsotopeLabelType.heavy;
                    }
                }
            }
            var group = new TransitionGroup(pep, adduct, isotopeLabelType);
            string errmsg;
            try
            {
                var tran = GetMoleculeTransition(document, row, pep, group, requireProductInfo);
                if (tran == null)
                    return null;
                return new TransitionGroupDocNode(group, document.Annotations, document.Settings, null,
                    null, moleculeInfo.ExplicitTransitionGroupValues, null, new[] { tran }, true);
            }
            catch (InvalidDataException x)
            {
                errmsg = x.Message;
            }
            catch (InvalidOperationException x) // Adduct handling code can throw these
            {
                errmsg = x.Message;
            }
            ShowTransitionError(new PasteError
            {
                Column = INDEX_PRODUCT_MZ, // Don't actually know that mz was the issue, but at least it's the right row, and in the product columns
                Line = row.Index,
                Message = errmsg
            });
            return null;
        }

        private TransitionDocNode GetMoleculeTransition(SrmDocument document, Row row, Peptide pep, TransitionGroup group, bool requireProductInfo)
        {
            var precursorIon = ReadPrecursorOrProductColumns(document, row, null); // Re-read the precursor columns
            var ion = requireProductInfo ? ReadPrecursorOrProductColumns(document, row, precursorIon) : precursorIon; // Re-read the product columns, or copy precursor
            if (requireProductInfo && ion == null)
            {
                return null;
            }
            var customMolecule = ion.ToCustomMolecule();
            var ionType = !requireProductInfo || // We inspected the input list and found only precursor info
                          // Or the mass is explained by an isotopic label in the adduct
                          (Math.Abs(customMolecule.MonoisotopicMass.Value - group.PrecursorAdduct.ApplyIsotopeLabelsToMass(pep.CustomMolecule.MonoisotopicMass)) <= MzMatchTolerance &&
                           Math.Abs(customMolecule.AverageMass.Value - group.PrecursorAdduct.ApplyIsotopeLabelsToMass(pep.CustomMolecule.AverageMass)) <= MzMatchTolerance) // Same mass, must be a precursor transition
                ? IonType.precursor
                : IonType.custom;
            var massType = (ionType == IonType.precursor)
                 ? document.Settings.TransitionSettings.Prediction.PrecursorMassType
                 : document.Settings.TransitionSettings.Prediction.FragmentMassType;
            if (ionType == IonType.precursor)
            {
                customMolecule = pep.CustomMolecule; // Some mz-only lists will give precursor mz as double, and product mz as int, even though they're meant to be the same thing
            }
            var mass = customMolecule.GetMass(massType);

            var transition = new Transition(group, ion.Adduct, null, customMolecule, ionType);
            var annotations = document.Annotations;
            if (!String.IsNullOrEmpty(ion.Note))
            {
                var note = document.Annotations.Note;
                // ReSharper disable LocalizableElement
                note = String.IsNullOrEmpty(note) ? ion.Note : (note + "\r\n" + ion.Note);
                // ReSharper restore LocalizableElement
                annotations = new Annotations(note, document.Annotations.ListAnnotations(), 0);
            }
            return new TransitionDocNode(transition, annotations, null, mass, TransitionDocNode.TransitionQuantInfo.DEFAULT, null);
        }
    }

    public class SmallMoleculeTransitionListCSVReader : SmallMoleculeTransitionListReader
    {
        private readonly DsvFileReader _csvReader;

        public SmallMoleculeTransitionListCSVReader(IEnumerable<string> csvText) : 
            // ReSharper disable LocalizableElement
            this(string.Join("\n", csvText))
            // ReSharper restore LocalizableElement
        {

        }

        public SmallMoleculeTransitionListCSVReader(string csvText)
        {
            // Accept either true CSV or currentculture equivalent
            Type[] columnTypes;
            IFormatProvider formatProvider;
            char separator;
            // Skip over header line to deduce decimal format
            var endLine = csvText.IndexOf('\n');
            var line = (endLine != -1 ? csvText.Substring(endLine+1) : csvText);
            MassListImporter.IsColumnar(line, out formatProvider, out separator, out columnTypes);
            // Double check that separator - does it appear in header row, or was it just an unlucky hit in a text field?
            var header = (endLine != -1 ? csvText.Substring(0, endLine) : csvText);
            if (!header.Contains(separator))
            {
                // Try again, this time without the distraction of a plausible but clearly incorrect seperator
                MassListImporter.IsColumnar(line.Replace(separator,'_'), out formatProvider, out separator, out columnTypes);
            }
            _cultureInfo = formatProvider;
            var reader = new StringReader(csvText);
            _csvReader = new DsvFileReader(reader, separator, SmallMoleculeTransitionListColumnHeaders.KnownHeaderSynonyms);
            // Do we recognize all the headers?
            var badHeaders =
                _csvReader.FieldNames.Where(
                    fn => SmallMoleculeTransitionListColumnHeaders.KnownHeaderSynonyms.All(kvp => string.Compare(kvp.Key, fn, StringComparison.OrdinalIgnoreCase) != 0)).ToList();
            if (badHeaders.Any())
            {
                badHeaders.Add(string.Empty); // Add an empty line for more whitespace
                throw new LineColNumberedIoException(
                    string.Format(
                        Resources.SmallMoleculeTransitionListReader_SmallMoleculeTransitionListReader_,
                        TextUtil.LineSeparate(badHeaders),
                        TextUtil.LineSeparate(SmallMoleculeTransitionListColumnHeaders.KnownHeaderSynonyms.Keys)),
                        1, _csvReader.FieldNames.IndexOf(badHeaders.First())+1);
            }
            string[] columns;
            var index = 0;
            while ((columns = _csvReader.ReadLine()) != null)
            {
                var row = new Row(this, index++, new List<string>(columns));
                Rows.Add(row);
            }
        }

        public int RowCount
        {
            get { return Rows.Count; }
        }

        public static bool IsPlausibleSmallMoleculeTransitionList(IEnumerable<string> csvText)
        {
            // ReSharper disable LocalizableElement
            return IsPlausibleSmallMoleculeTransitionList(string.Join("\n", csvText));
            // ReSharper restore LocalizableElement
        }

        public static bool IsPlausibleSmallMoleculeTransitionList(string csvText)
        {
            try
            {
                // This will throw if the headers don't look right
                var probe = new SmallMoleculeTransitionListCSVReader(csvText);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                return probe != null;
            }
            catch
            {
                // Not a proper small molecule transition list, but was it trying to be one?
                var header = csvText.Split('\n')[0];
                if (header.ToLowerInvariant().Contains(@"peptide"))
                {
                    return false;
                }
                return new[]
                {
                    // These are pretty basic hints, without much overlap in peptide lists
                    SmallMoleculeTransitionListColumnHeaders.moleculeGroup, // May be seen in Agilent peptide lists
                    SmallMoleculeTransitionListColumnHeaders.namePrecursor, 
                    SmallMoleculeTransitionListColumnHeaders.nameProduct, 
                    SmallMoleculeTransitionListColumnHeaders.formulaPrecursor, 
                    SmallMoleculeTransitionListColumnHeaders.adductPrecursor, 
                    SmallMoleculeTransitionListColumnHeaders.idCAS, 
                    SmallMoleculeTransitionListColumnHeaders.idInChiKey, 
                    SmallMoleculeTransitionListColumnHeaders.idInChi, 
                    SmallMoleculeTransitionListColumnHeaders.idHMDB, 
                    SmallMoleculeTransitionListColumnHeaders.idSMILES,
                }.Count(hint => SmallMoleculeTransitionListColumnHeaders.KnownHeaderSynonyms.Where(
                    p => string.Compare(p.Value, hint, StringComparison.OrdinalIgnoreCase) == 0).Any(kvp => header.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)) > 1;
            }
        }

        public override void UpdateCellBackingStore(int row, int col, object value)
        {
            // We don't have a backing store, unlike the dialog implementaion with its gridview 
        }

        public override void ShowTransitionError(PasteError error)
        {
            throw new LineColNumberedIoException(
                string.Format(
                    Resources
                        .InsertSmallMoleculeTransitionList_InsertSmallMoleculeTransitionList_Error_on_line__0___column_1____2_,
                    error.Line + 1, error.Column + 1, error.Message),
                    error.Line + 1, error.Column + 1);
        }

        public override int ColumnIndex(string columnName)
        {
            return _csvReader.GetFieldIndex(columnName);
        }
    }

    // Custom molecule transition list internal column names, for saving to settings
    public static class SmallMoleculeTransitionListColumnHeaders
    {
        public const string moleculeGroup = "MoleculeGroup";
        public const string namePrecursor = "PrecursorName";
        public const string nameProduct = "ProductName";
        public const string formulaPrecursor = "PrecursorFormula";
        public const string formulaProduct = "ProductFormula";
        public const string mzPrecursor = "PrecursorMz";
        public const string mzProduct = "ProductMz";
        public const string chargePrecursor = "PrecursorCharge";
        public const string chargeProduct = "ProductCharge";
        public const string rtPrecursor = "PrecursorRT";
        public const string rtWindowPrecursor = "PrecursorRTWindow";
        public const string cePrecursor = "PrecursorCE";
        public const string dtPrecursor = "PrecursorDT"; // Drift time - IMUnits is implied
        public const string dtHighEnergyOffset = "HighEnergyDTOffset";  // Drift time - IMUnits is implied
        public const string imPrecursor = "PrecursorIM";
        public const string imHighEnergyOffset = "HighEnergyIMOffset";
        public const string imUnits = "IMUnits";
        public const string ccsPrecursor = "PrecursorCCS";
        public const string slens = "SLens";
        public const string coneVoltage = "ConeVoltage";
        public const string compensationVoltage = "CompensationVoltage";
        public const string declusteringPotential = "DeclusteringPotential";
        public const string note = "Note";
        public const string labelType = "LabelType";
        public const string adductPrecursor = "PrecursorAdduct";
        public const string adductProduct = "ProductAdduct";
        public const string idCAS = "CAS";
        public const string idInChiKey = "InChiKey";
        public const string idInChi = "InChi";
        public const string idHMDB = "HMDB";
        public const string idSMILES = "SMILES";

        public static readonly List<string> KnownHeaders;

        public static IReadOnlyDictionary<string, string> KnownHeaderSynonyms;

        static SmallMoleculeTransitionListColumnHeaders()
        {
            // The list of internal values, as used in serialization
            KnownHeaders =  new List<string>(new[]
            {
                moleculeGroup,
                namePrecursor,
                nameProduct,
                formulaPrecursor,
                formulaProduct,
                mzPrecursor,
                mzProduct,
                chargePrecursor,
                chargeProduct,
                adductPrecursor,
                adductProduct,
                rtPrecursor,
                rtWindowPrecursor,
                cePrecursor,
                dtPrecursor, // Drift time - IMUnits implied
                dtHighEnergyOffset, // Drift time - IMUnits implied
                imPrecursor, // General ion mobility, imUnits required
                imHighEnergyOffset,
                imUnits,
                ccsPrecursor,
                slens,
                coneVoltage,
                compensationVoltage,
                declusteringPotential,
                note,
                labelType,
                idInChiKey,
                idCAS,
                idHMDB,
                idInChi,
                idSMILES,
            });

            // A dictionary of terms that can be understood as column headers - this includes
            // the internal names, and the names presented in the UI (for all supported cultures)
            var currentCulture = Thread.CurrentThread.CurrentCulture;
            var currentUICulture = Thread.CurrentThread.CurrentUICulture;
            var knownColumnHeadersAllCultures = KnownHeaders.ToDictionary( hdr => hdr, hdr => hdr);
            foreach (var culture in new[] { @"en", @"zh-CHS", @"ja" })
            {
                Thread.CurrentThread.CurrentUICulture =
                    Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                foreach (var pair in new[] {
                    Tuple.Create(moleculeGroup, Resources.PasteDlg_UpdateMoleculeType_Molecule_List_Name),
                    Tuple.Create(namePrecursor, Resources.PasteDlg_UpdateMoleculeType_Precursor_Name),
                    Tuple.Create(namePrecursor, Resources.SmallMoleculeTransitionListColumnHeaders_SmallMoleculeTransitionListColumnHeaders_Molecule),
                    Tuple.Create(namePrecursor, Resources.SmallMoleculeTransitionListColumnHeaders_SmallMoleculeTransitionListColumnHeaders_Compound),
                    Tuple.Create(nameProduct, Resources.PasteDlg_UpdateMoleculeType_Product_Name),
                    Tuple.Create(formulaPrecursor, Resources.PasteDlg_UpdateMoleculeType_Precursor_Formula),
                    Tuple.Create(formulaProduct, Resources.PasteDlg_UpdateMoleculeType_Product_Formula),
                    Tuple.Create(mzPrecursor, Resources.PasteDlg_UpdateMoleculeType_Precursor_m_z),
                    Tuple.Create(mzProduct, Resources.PasteDlg_UpdateMoleculeType_Product_m_z),
                    Tuple.Create(chargePrecursor, Resources.PasteDlg_UpdateMoleculeType_Precursor_Charge),
                    Tuple.Create(chargeProduct, Resources.PasteDlg_UpdateMoleculeType_Product_Charge),
                    Tuple.Create(adductPrecursor, Resources.PasteDlg_UpdateMoleculeType_Precursor_Adduct),
                    Tuple.Create(adductProduct, Resources.PasteDlg_UpdateMoleculeType_Product_Adduct),
                    Tuple.Create(rtPrecursor, Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time),
                    Tuple.Create(rtPrecursor, Resources.SmallMoleculeTransitionListColumnHeaders_SmallMoleculeTransitionListColumnHeaders_RT__min_), // ""RT (min)"
                    Tuple.Create(rtWindowPrecursor, Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time_Window),
                    Tuple.Create(cePrecursor, Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Energy),
                    Tuple.Create(dtPrecursor, Resources.PasteDlg_UpdateMoleculeType_Explicit_Drift_Time__msec_),
                    Tuple.Create(dtHighEnergyOffset, Resources.PasteDlg_UpdateMoleculeType_Explicit_Drift_Time_High_Energy_Offset__msec_),
                    Tuple.Create(imPrecursor, Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility),
                    Tuple.Create(imHighEnergyOffset, Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility_High_Energy_Offset),
                    Tuple.Create(imUnits, Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility_Units),
                    Tuple.Create(ccsPrecursor, Resources.PasteDlg_UpdateMoleculeType_Collisional_Cross_Section__sq_A_),
                    Tuple.Create(slens, Resources.PasteDlg_UpdateMoleculeType_S_Lens),
                    Tuple.Create(coneVoltage, Resources.PasteDlg_UpdateMoleculeType_Cone_Voltage),
                    Tuple.Create(compensationVoltage, Resources.PasteDlg_UpdateMoleculeType_Explicit_Compensation_Voltage),
                    Tuple.Create(declusteringPotential, Resources.PasteDlg_UpdateMoleculeType_Explicit_Declustering_Potential),
                    Tuple.Create(note, Resources.PasteDlg_UpdateMoleculeType_Note),
                    Tuple.Create(labelType, Resources.PasteDlg_UpdateMoleculeType_Label_Type),
                    Tuple.Create(idInChiKey, idInChiKey),
                    Tuple.Create(idCAS, idCAS),
                    Tuple.Create(idHMDB, idHMDB),
                    Tuple.Create(idInChi, idInChi),
                    Tuple.Create(idSMILES, idSMILES),
                })
                {
                    if (!knownColumnHeadersAllCultures.ContainsKey(pair.Item2))
                    {
                        knownColumnHeadersAllCultures.Add(pair.Item2, pair.Item1);
                    }

                    var mz = pair.Item2.Replace(@"m/z", @"mz"); // Accept either m/z or mz
                    if (!knownColumnHeadersAllCultures.ContainsKey(mz))
                    {
                        knownColumnHeadersAllCultures.Add(mz, pair.Item1);
                    }
                }
            }
            Thread.CurrentThread.CurrentCulture = currentCulture;
            Thread.CurrentThread.CurrentUICulture = currentUICulture;
            KnownHeaderSynonyms = knownColumnHeadersAllCultures;
        }
    }
}
