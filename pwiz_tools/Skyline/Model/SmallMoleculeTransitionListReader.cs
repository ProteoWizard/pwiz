/*
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
        protected CultureInfo _cultureInfo;
        protected List<Row> Rows { get; set; }
        public abstract void UpdateCellBackingStore(int row, int col, object value);
        public abstract void ShowTransitionError(PasteError error);
        public abstract int ColumnIndex(string columnName);

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

        public SrmDocument CreateTargets(SrmDocument document, IdentityPath to, out IdentityPath firstAdded)
        {
            firstAdded = null;

            // We will accept a completely empty product list as meaning 
            // "these are all precursor transitions"
            var requireProductInfo = false;
            foreach (var row in Rows)
            {
                var productMz = row.GetCell(INDEX_PRODUCT_MZ);
                var productFormula = row.GetCell(INDEX_PRODUCT_FORMULA);
                var productCharge = row.GetCell(INDEX_PRODUCT_CHARGE);
                if (!string.IsNullOrEmpty(productMz) ||
                    !string.IsNullOrEmpty(productFormula) ||
                    !string.IsNullOrEmpty(productCharge))
                {
                    requireProductInfo = true; // Product list is not completely empty
                    break;
                }
            }
            string defaultPepGroupName = null;
            // For each row in the grid, add to or begin MoleculeGroup|Molecule|TransitionList tree
            foreach (var row in Rows)
            {
                var precursor = ReadPrecursorOrProductColumns(document, row, true); // Get molecule values
                if (precursor == null)
                    return null;
                if (requireProductInfo && ReadPrecursorOrProductColumns(document, row, false) == null)
                {
                    return null;
                }
                var charge = precursor.Charge;
                var precursorMonoMz = BioMassCalc.CalculateIonMz(precursor.MonoMass, charge);
                var precursorAverageMz = BioMassCalc.CalculateIonMz(precursor.AverageMass, charge);

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
                            var ionMonoMz = BioMassCalc.CalculateIonMz(pep.CustomIon.MonoisotopicMass, charge);
                            var ionAverageMz = BioMassCalc.CalculateIonMz(pep.CustomIon.AverageMass, charge);
                            var mzMatchTolerance = document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
                            var labelType = precursor.IsotopeLabelType ?? IsotopeLabelType.light;
                            // Match existing molecule if same name
                            if (!string.IsNullOrEmpty(precursor.Name))
                            {
                                pepFound = Equals(pep.CustomIon.Name, precursor.Name);  // If user says they're the same, believe them
                            }
                            else  // If no names, look to other cues
                            {
                                // Match existing molecule if same formula or identical formula when stripped of labels
                                pepFound |= !string.IsNullOrEmpty(pep.CustomIon.Formula) && 
                                    (Equals(pep.CustomIon.Formula, precursor.NeutralFormula) ||
                                     Equals(pep.CustomIon.Formula, precursor.Formula) ||
                                     Equals(pep.CustomIon.UnlabeledFormula, BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula(precursor.NeutralFormula)) ||
                                     Equals(pep.CustomIon.UnlabeledFormula, precursor.UnlabeledFormula));
                                // Match existing molecule if similar m/z at the precursor charge
                                pepFound |=  Math.Abs(ionMonoMz - precursorMonoMz) <= mzMatchTolerance &&
                                             Math.Abs(ionAverageMz - precursorAverageMz) <= mzMatchTolerance; // (we don't just check mass since we don't have a tolerance value for that)
                                // Or no formula, and different isotope labels or matching label and mz
                                pepFound |= string.IsNullOrEmpty(pep.CustomIon.Formula) && string.IsNullOrEmpty(precursor.Formula) &&
                                  (!pep.TransitionGroups.Any(t => Equals(t.TransitionGroup.LabelType, labelType)) || // First label of this kind
                                   pep.TransitionGroups.Any(t => Equals(t.TransitionGroup.LabelType, labelType) && // Already seen this label, and
                                           Math.Abs(precursor.Mz - t.PrecursorMz) <= mzMatchTolerance)); // Matches precursor mz of similar labels
                            }
                            if (pepFound)
                            {
                                bool tranGroupFound = false;
                                foreach (var tranGroup in pep.TransitionGroups)
                                {
                                    var pathGroup = new IdentityPath(pepPath, tranGroup.Id);
                                    if (Math.Abs(tranGroup.PrecursorMz - precursor.Mz) <= mzMatchTolerance)
                                    {
                                        tranGroupFound = true;
                                        var tranFound = false;
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
                                                document = (SrmDocument)document.Add(pathGroup, tranNode);
                                                firstAdded = firstAdded ?? pathGroup;
                                            }
                                        }
                                        catch (InvalidDataException e)
                                        {
                                            // Some error we didn't catch in the basic checks
                                            ShowTransitionError(new PasteError
                                            {
                                                Column = 0,
                                                Line = row.Index,
                                                Message = e.Message
                                            });
                                            return null;
                                        }
                                        break;
                                    }
                                }
                                if (!tranGroupFound)
                                {
                                    var node = GetMoleculeTransitionGroup(document, row, pep.Peptide, requireProductInfo);
                                    if (node == null)
                                        return null;
                                    document = (SrmDocument)document.Add(pepPath, node);
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
                            document = (SrmDocument)document.Add(pathPepGroup, node);
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
                    document = document.AddPeptideGroups(new[] { node }, false, to, out first, out next);
                    if (string.IsNullOrEmpty(defaultPepGroupName))
                    {
                        defaultPepGroupName = node.Name;
                    }
                    firstAdded = firstAdded ?? first;
                }
            }
            return document;
        }

        private int INDEX_MOLECULE_GROUP { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.moleculeGroup); } }
        private int INDEX_MOLECULE_NAME { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.namePrecursor); } }
        private int INDEX_PRODUCT_NAME { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.nameProduct); } }
        private int INDEX_MOLECULE_FORMULA { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.formulaPrecursor); } }
        private int INDEX_MOLECULE_ADDUCT { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.adductPrecursor); } }
        private int INDEX_PRODUCT_FORMULA { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.formulaProduct); } }
        private int INDEX_PRODUCT_ADDUCT { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.adductProduct); } }
        private int INDEX_MOLECULE_MZ { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.mzPrecursor); } }
        private int INDEX_PRODUCT_MZ { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.mzProduct); } }
        private int INDEX_MOLECULE_CHARGE { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargePrecursor); } }
        private int INDEX_PRODUCT_CHARGE { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.chargeProduct); } }
        private int INDEX_LABEL_TYPE { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.labelType); } }
        private int INDEX_RETENTION_TIME { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.rtPrecursor); } }
        private int INDEX_RETENTION_TIME_WINDOW { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.rtWindowPrecursor); } }
        private int INDEX_COLLISION_ENERGY { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.cePrecursor); } }
        private int INDEX_NOTE { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.note); } }
        private int INDEX_MOLECULE_DRIFT_TIME_MSEC { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.dtPrecursor); } }
        private int INDEX_HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.dtHighEnergyOffset); } }
        private int INDEX_SLENS { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.slens); } }
        private int INDEX_CONE_VOLTAGE { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.coneVoltage); } }
        private int INDEX_COMPENSATION_VOLTAGE { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.compensationVoltage); } }
        private int INDEX_DECLUSTERING_POTENTIAL { get { return ColumnIndex(PasteDlg.SmallMoleculeTransitionListColumnHeaders.declusteringPotential); } }

        private static int? ValidateFormulaWithMz(SrmDocument document, ref string moleculeFormula, double mz, int? charge, out double monoMass, out double averageMass, out double? mzCalc)
        {
            // Is the ion's formula the old style where user expected us to add a hydrogen?
            var tolerance = document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            int massShift;
            var ion = new DocNodeCustomIon(moleculeFormula);
            monoMass = ion.GetMass(MassType.Monoisotopic);
            averageMass = ion.GetMass(MassType.Average);
            double mass = (document.Settings.TransitionSettings.Prediction.FragmentMassType == MassType.Monoisotopic)
                ? monoMass
                : averageMass;
            // Does given charge, if any, agree with mass and mz?
            mzCalc = charge.HasValue ? BioMassCalc.CalculateIonMz(mass, charge.Value) : (double?)null;
            if (mzCalc.HasValue && tolerance >= (Math.Abs(mzCalc.Value - mz)))
            {
                return charge;
            }
            int nearestCharge;
            charge = TransitionCalc.CalcCharge(mass, mz, tolerance, true, TransitionGroup.MIN_PRECURSOR_CHARGE,
                TransitionGroup.MAX_PRECURSOR_CHARGE, new int[0],
                TransitionCalc.MassShiftType.none, out massShift, out nearestCharge);
            if (!charge.HasValue)
            {
                // That formula and this mz don't yield a reasonable charge state - try adding an H
                var ion2 = new DocNodeCustomIon(BioMassCalc.AddH(ion.FormulaWithAdductApplied));
                monoMass = ion2.GetMass(MassType.Monoisotopic);
                averageMass = ion2.GetMass(MassType.Average);
                mass = (document.Settings.TransitionSettings.Prediction.FragmentMassType == MassType.Monoisotopic)
                    ? monoMass
                    : averageMass;
                charge = TransitionCalc.CalcCharge(mass, mz, tolerance, true, TransitionGroup.MIN_PRECURSOR_CHARGE,
                    TransitionGroup.MAX_PRECURSOR_CHARGE, new int[0], TransitionCalc.MassShiftType.none, out massShift, out nearestCharge);
                if (charge.HasValue)
                {
                    moleculeFormula = ion2.Formula;
                }
                else
                {
                    monoMass = 0;
                    averageMass = 0;
                }
            }
            return charge;
        }

        private double ValidateFormulaWithCharge(SrmDocument document, string moleculeFormula, int charge, out double monoMass, out double averageMass)
        {
            var massType = document.Settings.TransitionSettings.Prediction.PrecursorMassType;
            var ion = new DocNodeCustomIon(moleculeFormula);
            double mass = ion.GetMass(massType);
            monoMass = ion.GetMass(MassType.Monoisotopic);
            averageMass = ion.GetMass(MassType.Average);
            return BioMassCalc.CalculateIonMz(mass, charge);
        }

        public static string NullForEmpty(string str)
        {
            if (str == null)
                return null;
            return (str.Length == 0) ? null : str;
        }

        private class MoleculeInfo : IonInfo
        {
            public string Name { get; private set; }
            public string Note { get; private set; }
            public double Mz { get; private set; }
            public int Charge { get; private set; }
            public double MonoMass { get; private set; }
            public double AverageMass { get; private set; }
            public IsotopeLabelType IsotopeLabelType { get; private set; }
            public ExplicitRetentionTimeInfo ExplicitRetentionTime { get; private set; }
            public ExplicitTransitionGroupValues ExplicitTransitionGroupValues { get; private set; }
            public MoleculeInfo(string name, string formula, int charge, double mz, double monoMass, double averageMass,
                IsotopeLabelType isotopeLabelType,
                ExplicitRetentionTimeInfo explicitRetentionTime,
                ExplicitTransitionGroupValues explicitTransitionGroupValues,
                string note) : base(formula)
            {
                Name = name;
                Charge = charge;
                Mz = mz;
                MonoMass = monoMass;
                AverageMass = averageMass;
                IsotopeLabelType = isotopeLabelType;
                ExplicitRetentionTime = explicitRetentionTime;
                ExplicitTransitionGroupValues = explicitTransitionGroupValues;
                Note = note;
            }

            public DocNodeCustomIon ToCustomIon()
            {
                return new DocNodeCustomIon(Formula, MonoMass, AverageMass, Name);
            }
        }

        // We need some combination of:
        //  Formula and mz
        //  Formula and charge
        //  mz and charge
        private MoleculeInfo ReadPrecursorOrProductColumns(SrmDocument document,
            Row row,
            bool getPrecursorColumns)
        {
            int indexName = getPrecursorColumns ? INDEX_MOLECULE_NAME : INDEX_PRODUCT_NAME;
            int indexFormula = getPrecursorColumns ? INDEX_MOLECULE_FORMULA : INDEX_PRODUCT_FORMULA;
            int indexAdduct = getPrecursorColumns ? INDEX_MOLECULE_ADDUCT : INDEX_PRODUCT_ADDUCT;
            int indexMz = getPrecursorColumns ? INDEX_MOLECULE_MZ : INDEX_PRODUCT_MZ;
            int indexCharge = getPrecursorColumns ? INDEX_MOLECULE_CHARGE : INDEX_PRODUCT_CHARGE;
            var name = NullForEmpty(row.GetCell(indexName));
            var formula = NullForEmpty(row.GetCell(indexFormula));
            var adduct = NullForEmpty(row.GetCell(indexAdduct));
            var note = NullForEmpty(row.GetCell(INDEX_NOTE));
            IsotopeLabelType isotopeLabelType = null;
            double mz;
            bool badMz = false;
            if (!row.GetCellAsDouble(indexMz, out mz))
            {
                if (!String.IsNullOrEmpty(row.GetCell(indexMz)))
                {
                    badMz = true;
                }
                mz = 0;
            }
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
            int trycharge;
            if (Int32.TryParse(row.GetCell(indexCharge), out trycharge))
                charge = trycharge;
            else if (!String.IsNullOrEmpty(row.GetCell(indexCharge)))
            {
                ShowTransitionError(new PasteError
                {
                    Column = indexCharge,
                    Line = row.Index,
                    Message = String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_charge_value__0_, row.GetCell(indexCharge))
                });
                return null;
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
            double? driftTimePrecursorMsec = null;
            if (row.GetCellAsDouble(INDEX_MOLECULE_DRIFT_TIME_MSEC, out dtmp))
                driftTimePrecursorMsec = dtmp;
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
            double? driftTimeHighEnergyOffsetMsec = null;
            if (row.GetCellAsDouble(INDEX_HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC, out dtmp))
                driftTimeHighEnergyOffsetMsec = dtmp;
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
            string errMessage = String.Format(getPrecursorColumns
                ? Resources.PasteDlg_ValidateEntry_Error_on_line__0___Precursor_needs_values_for_any_two_of__Formula__m_z_or_Charge_
                : Resources.PasteDlg_ValidateEntry_Error_on_line__0___Product_needs_values_for_any_two_of__Formula__m_z_or_Charge_, row.Index + 1);
            // Do we have an adduct description?  If so, pull charge from that.
            if ((!string.IsNullOrEmpty(formula) && formula.Contains('[') && formula.Contains(']')) || !string.IsNullOrEmpty(adduct))
            {
                if (!string.IsNullOrEmpty(formula))
                {
                    var parts = formula.Split('[');
                    var formulaAdduct = formula.Substring(parts[0].Length);
                    if (string.IsNullOrEmpty(adduct))
                    {
                        adduct = formulaAdduct;
                    }
                    else if (!string.IsNullOrEmpty(formulaAdduct) &&
                        !Equals(adduct.Replace("[", "").Replace("]", ""), formulaAdduct.Replace("[", "").Replace("]", ""))) // Not L10N
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
                int z;
                try
                {
                    IonInfo.ApplyAdductToFormula(formula??string.Empty, adduct, out z);
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
                if (charge.HasValue && charge.Value != z)
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = indexAdduct >=0 ? indexAdduct : indexFormula,
                        Line = row.Index,
                        Message = string.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Adduct__0__charge__1__does_not_agree_with_declared_charge__2_, adduct, z, charge.Value)
                    });
                    return null;
                }
                else
                {
                    charge = z;
                }
                if (!string.IsNullOrEmpty(formula) && !string.IsNullOrEmpty(adduct))
                {
                    if (!adduct.StartsWith("[")) // Not L10N
                    {
                        adduct = "[" + adduct + "]"; // Not L10N
                    }
                    formula += adduct;
                }
            }
            int errColumn = indexFormula;
            int countValues = 0;
            if (charge.HasValue && charge.Value != 0)
                countValues++;
            if (mz > 0)
                countValues++;
            if (NullForEmpty(formula) != null)
                countValues++;
            if (countValues >= 2) // Do we have at least 2 of charge, mz, formula?
            {
                double monoMass;
                double averageMmass;
                if (driftTimePrecursorMsec.HasValue)
                {
                    if (!driftTimeHighEnergyOffsetMsec.HasValue)
                    {
                        driftTimeHighEnergyOffsetMsec = 0;
                    }
                }
                else
                {
                    driftTimeHighEnergyOffsetMsec = null; // Offset without a base value isn't useful
                }
                var retentionTimeInfo = retentionTime.HasValue
                    ? new ExplicitRetentionTimeInfo(retentionTime.Value, retentionTimeWindow)
                    : null;
                var explicitTransitionGroupValues = new ExplicitTransitionGroupValues(collisionEnergy, driftTimePrecursorMsec, driftTimeHighEnergyOffsetMsec, slens,
                    coneVoltage, declusteringPotential, compensationVoltage);
                var massOk = true;
                var massTooLow = false;
                string massErrMsg = null;
                var absCharge = Math.Abs(charge ?? 0);
                if (getPrecursorColumns && absCharge != 0 && (absCharge < TransitionGroup.MIN_PRECURSOR_CHARGE || absCharge > TransitionGroup.MAX_PRECURSOR_CHARGE))
                {
                    errMessage = String.Format(Resources.Transition_Validate_Precursor_charge__0__must_be_non_zero_and_between__1__and__2__,
                        charge, -TransitionGroup.MAX_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE);
                    errColumn = indexCharge;
                }
                else if (!getPrecursorColumns && absCharge != 0 && (absCharge < Transition.MIN_PRODUCT_CHARGE || absCharge > Transition.MAX_PRODUCT_CHARGE))
                {
                    errMessage = String.Format(Resources.Transition_Validate_Product_ion_charge__0__must_be_non_zero_and_between__1__and__2__,
                        charge, -Transition.MAX_PRODUCT_CHARGE, Transition.MAX_PRODUCT_CHARGE);
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
                            var ion = new IonInfo(formula);
                            if (!Equals(ion.FormulaWithAdductApplied, ion.UnlabeledFormula)) // Formula+adduct contained some heavy isotopes
                            {
                                isotopeLabelType = IsotopeLabelType.heavy;
                                if (INDEX_LABEL_TYPE >= 0)
                                {
                                    row.UpdateCell(INDEX_LABEL_TYPE, isotopeLabelType.ToString());
                                }
                            }
                        }

                        if (mz > 0)
                        {
                            // Is the ion's formula the old style where user expected us to add a hydrogen? 
                            double? mzCalc;
                            charge = ValidateFormulaWithMz(document, ref formula, mz, charge, out monoMass, out averageMmass, out mzCalc);
                            row.SetCell(indexFormula, formula);
                            massOk = monoMass < CustomIon.MAX_MASS && averageMmass < CustomIon.MAX_MASS &&
                                     !(massTooLow = charge.HasValue && (monoMass < CustomIon.MIN_MASS || averageMmass < CustomIon.MIN_MASS)); // Null charge => masses are 0 but meaningless
                            if (massOk)
                            {
                                if (charge.HasValue)
                                {
                                    row.UpdateCell(indexCharge, charge.Value);
                                    return new MoleculeInfo(name, formula, charge.Value, mz, monoMass, averageMmass, isotopeLabelType, retentionTimeInfo, explicitTransitionGroupValues, note);
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
                            // Get the mass from the formula, and mz from that and charge
                            mz = ValidateFormulaWithCharge(document, formula, charge.Value, out monoMass, out averageMmass);
                            massOk = !((monoMass >= CustomIon.MAX_MASS || averageMmass >= CustomIon.MAX_MASS)) &&
                                     !(massTooLow = (monoMass < CustomIon.MIN_MASS || averageMmass < CustomIon.MIN_MASS));
                            row.UpdateCell(indexMz, mz);
                            if (massOk)
                                return new MoleculeInfo(name, formula, charge.Value, mz, monoMass, averageMmass, isotopeLabelType, retentionTimeInfo, explicitTransitionGroupValues, note);
                        }
                    }
                    catch (InvalidDataException x)
                    {
                        massOk = false;
                        massErrMsg = x.Message;
                    }
                }
                else if (mz != 0 && charge.HasValue)
                {
                    // No formula, just use charge and m/z
                    monoMass = averageMmass = BioMassCalc.CalculateIonMassFromMz(mz, charge.Value);
                    massOk = monoMass < CustomIon.MAX_MASS && averageMmass < CustomIon.MAX_MASS &&
                             !(massTooLow = (monoMass < CustomIon.MIN_MASS || averageMmass < CustomIon.MIN_MASS));
                    errColumn = indexMz;
                    if (massOk)
                        return new MoleculeInfo(name, formula, charge.Value, mz, monoMass, averageMmass, isotopeLabelType, retentionTimeInfo, explicitTransitionGroupValues, note);
                }
                if (massTooLow)
                {
                    errMessage = massErrMsg ?? String.Format(
                        Resources
                            .EditCustomMoleculeDlg_OkDialog_Custom_molecules_must_have_a_mass_greater_than_or_equal_to__0__,
                        CustomIon.MIN_MASS);
                }
                else if (!massOk)
                {
                    errMessage = massErrMsg ?? String.Format(
                        Resources
                            .EditCustomMoleculeDlg_OkDialog_Custom_molecules_must_have_a_mass_less_than_or_equal_to__0__,
                        CustomIon.MAX_MASS);
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

            DocNodeCustomIon ion;
            MoleculeInfo moleculeInfo;
            try
            {
                moleculeInfo = ReadPrecursorOrProductColumns(document, row, true); // Re-read the precursor columns
                if (moleculeInfo == null)
                    return null; // Some failure, but exception was already handled
                // Identify items with same formula and different adducts
                var neutralFormula = moleculeInfo.NeutralFormula;
                var shortName = row.GetCell(INDEX_MOLECULE_NAME);
                if (!string.IsNullOrEmpty(neutralFormula))
                {
                    ion = new DocNodeCustomIon(neutralFormula, shortName);
                }
                else
                {
                    ion = new DocNodeCustomIon(moleculeInfo.Formula, moleculeInfo.MonoMass, moleculeInfo.AverageMass, shortName);
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
                var pep = new Peptide(ion);
                var tranGroup = GetMoleculeTransitionGroup(document, row, pep, requireProductInfo);
                if (tranGroup == null)
                    return null;
                return new PeptideDocNode(pep, document.Settings, null, null, moleculeInfo.ExplicitRetentionTime, new[] { tranGroup }, true);
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
            var moleculeInfo = ReadPrecursorOrProductColumns(document, row, true); // Re-read the precursor columns
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

            var customIon = moleculeInfo.ToCustomIon();
            var isotopeLabelType = moleculeInfo.IsotopeLabelType ?? IsotopeLabelType.light;
            var group = new TransitionGroup(pep, customIon, moleculeInfo.Charge, isotopeLabelType);
            try
            {
                var tran = GetMoleculeTransition(document, row, pep, group, requireProductInfo);
                if (tran == null)
                    return null;
                return new TransitionGroupDocNode(group, document.Annotations, document.Settings, null,
                    null, moleculeInfo.ExplicitTransitionGroupValues, null, new[] { tran }, true);
            }
            catch (InvalidDataException e)
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_PRODUCT_MZ, // Don't actually know that mz was the issue, but at least it's the right row, and in the product columns
                    Line = row.Index,
                    Message = e.Message
                });
                return null;
            }
        }

        private TransitionDocNode GetMoleculeTransition(SrmDocument document, Row row, Peptide pep, TransitionGroup group, bool requireProductInfo)
        {
            var massType =
                document.Settings.TransitionSettings.Prediction.FragmentMassType;

            var molecule = ReadPrecursorOrProductColumns(document, row, !requireProductInfo); // Re-read the product columns, or copy precursor
            if (requireProductInfo && molecule == null)
            {
                return null;
            }
            var ion = molecule.ToCustomIon();
            var ionType = (!requireProductInfo || // We inspected the input list and found only precursor info
                          (ion.MonoisotopicMass.Equals(pep.CustomIon.MonoisotopicMass) &&
                           ion.AverageMass.Equals(pep.CustomIon.AverageMass)) || // Same mass, must be a precursor transition
                          (ion.MonoisotopicMass.Equals(group.CustomIon.MonoisotopicMass) &&
                           ion.AverageMass.Equals(group.CustomIon.AverageMass))) // Same mass, must be a precursor transition
                ? IonType.precursor
                : IonType.custom;
            double mass = ion.GetMass(massType);

            var transition = new Transition(group, molecule.Charge, null, ion, ionType);
            var annotations = document.Annotations;
            if (!String.IsNullOrEmpty(molecule.Note))
            {
                var note = document.Annotations.Note;
                note = String.IsNullOrEmpty(note) ? molecule.Note : (note + "\r\n" + molecule.Note); // Not L10N
                annotations = new Annotations(note, document.Annotations.ListAnnotations(), 0);
            }
            return new TransitionDocNode(transition, annotations, null, mass, null, null, null);
        }
    }

    public class SmallMoleculeTransitionListCSVReader : SmallMoleculeTransitionListReader
    {
        private readonly DsvFileReader _csvReader;

        public SmallMoleculeTransitionListCSVReader(IEnumerable<string> csvText) : 
            this(string.Join("\n", csvText)) // Not L10N
        {

        }

        public SmallMoleculeTransitionListCSVReader(string csvText)
        {
            // Accept either true CSV or currentculture equivalent
            var badHeaders = new List<string>();
            int maxHeaders = -1;
            for (var tryCultures = 0; tryCultures < 3; tryCultures++)
            {
                var reader = new StringReader(csvText);
                _cultureInfo = CultureInfo.InvariantCulture; // Initally assume actual CSV
                var separator = TextUtil.SEPARATOR_CSV;
                if (tryCultures == 1)
                {
                    // In retry - is the problem that this isn't actually CSV, but rather a local L10N format?
                    _cultureInfo = LocalizationHelper.CurrentCulture; // Perhaps it's in local culture
                    separator = TextUtil.CsvSeparator;
                }
                _csvReader = new DsvFileReader(reader, separator);
                // Do we recognize all the headers?
                if (_csvReader.FieldNames.Count > maxHeaders)
                {
                    // This was probably at least the correct CultureInfo
                    badHeaders =
                        _csvReader.FieldNames.Where(
                            n => !PasteDlg.SmallMoleculeTransitionListColumnHeaders.KnownHeaders().Contains(n)).ToList();
                    maxHeaders = _csvReader.FieldNames.Count;
                }
                if (!badHeaders.Any())
                {
                    break;
                }
            }
            if (badHeaders.Any())
            {
                throw new LineColNumberedIoException(
                    string.Format(
                        Resources.SmallMoleculeTransitionListReader_SmallMoleculeTransitionListReader_,
                        TextUtil.LineSeparate(badHeaders),
                        TextUtil.LineSeparate(PasteDlg.SmallMoleculeTransitionListColumnHeaders.KnownHeaders())),
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

        public static bool IsPlausibleSmallMoleculeTransitionList(IEnumerable<string> csvText)
        {
            return IsPlausibleSmallMoleculeTransitionList(string.Join("\n", csvText)); // Not L10N
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
                var hints = new[]
                {
                    // These are pretty basic, without overlap in peptide lists
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.nameProduct,
                    PasteDlg.SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                    // Perhaps they were trying to use header names as seen in the UI (these have spaces, may be localized)
                    Resources.PasteDlg_UpdateMoleculeType_Molecule_List_Name,  
                    Resources.PasteDlg_UpdateMoleculeType_Precursor_Name,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Name,
                    Resources.PasteDlg_UpdateMoleculeType_Precursor_Ion_Formula
                };
                return hints.Any(h => header.Contains(h));
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
}
