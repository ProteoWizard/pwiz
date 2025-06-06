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
using MathNet.Numerics.Statistics;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Read a small molecule transition list in CSV form, where header values are restricted to
    /// those found in SmallMoleculeTransitionListColumnHeaders.KnownHeaders() with the possible
    /// addition of columns needed for Assay Library input
    /// </summary>
    public abstract class SmallMoleculeTransitionListReader
    {
        protected IFormatProvider _cultureInfo;
        protected List<Row> Rows { get; set; }

        private Dictionary<Tuple<Peptide, TransitionGroup, Transition>, List<double>> LibraryIntensities { get; set; } // For use by Assay Library reader
        private Dictionary<Peptide, HashSet<double>> IRTs { get; set; } // For use by Assay Library reader - we take the median IRT for different isotopic labels
        
        public abstract void UpdateCellBackingStore(int row, int col, object value);
        public abstract void ShowTransitionError(PasteError error);
        public abstract int ColumnIndex(string columnName);

        private double MzMatchTolerance { get; set; }

        public List<PasteError> ErrorList { get; set; }
        public bool HasHeaders { get; set; }

        protected SmallMoleculeTransitionListReader()
        {
            Rows = new List<Row>();
            ErrorList = new List<PasteError>();
            LibraryIntensities = new Dictionary<Tuple<Peptide, TransitionGroup, Transition>, List<double>>();
            IRTs = new Dictionary<Peptide, HashSet<double>>(); // We take the median IRT for different isotopic labels
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

        }

        private bool RowHasDistinctProductValue(Row row, int productCol, int precursorCol)
        {
            var productVal = GetCellTrimmed(row, productCol);
            return !string.IsNullOrEmpty(productVal) && !Equals(productVal, GetCellTrimmed(row, precursorCol));
        }

        public SrmDocument CreateTargets(SrmDocument document, IdentityPath to, out IdentityPath firstAdded, bool tolerateErrors = false,
            List<PeptideGroupDocNode> peptideGroups = null,
            List<MeasuredRetentionTime> measuredRetentionTimes = null, List<SpectrumMzInfo> librarySpectra = null, string fileName = null)
        {
            _firstAddedPathPepGroup = firstAdded = null;
            var moleculeIDsSeen = document.CustomMolecules.
                Select(mol => new MoleculeNameAndAccessions(mol.CustomMolecule.Name, mol.CustomMolecule.AccessionNumbers))
                .ToHashSet();
            var groupNamesSeen = document.MoleculeGroups.Select(group => group.Name)
                .Where(n => !string.IsNullOrEmpty(n)).ToHashSet();
            var precursorsParsed = new Dictionary<MoleculeNameAndAccessions, List<ParsedIonInfo>>();
            MzMatchTolerance = document.Settings.TransitionSettings.Instrument.MzMatchTolerance;

            _hasAnyMoleculeMz = Rows.Any(row => !string.IsNullOrEmpty(GetCellTrimmed(row, INDEX_PRECURSOR_MZ)));
            _hasAnyMoleculeFormula = Rows.Any(row => !string.IsNullOrEmpty(GetCellTrimmed(row, INDEX_MOLECULE_FORMULA)));
            _hasAnyMoleculeCharge = Rows.Any(row => !string.IsNullOrEmpty(GetCellTrimmed(row, INDEX_PRECURSOR_CHARGE)));
            _hasAnyMoleculeAdduct = Rows.Any(row => !string.IsNullOrEmpty(GetCellTrimmed(row, INDEX_PRECURSOR_ADDUCT)));

            // Run through all rows gathering and possibly unifying molecule details if some have missing but not conflicting details
            GetMoleculeDetails();

            // Rearrange mz-only lists if necessary such that lowest mass for any given group+molecule appears before its heavy siblings,
            // so we can work out from there to identify implied isotope labels.
            if (_hasAnyMoleculeMz && !_hasAnyMoleculeFormula)
            {
                SortSiblingsByMass();
            }

            _requireProductInfo = GetRequireProductInfo(document); // Examine the first several lines of the file to determine columns will be needed
            ErrorList.Clear(); // We're going to reparse, so clear any errors found so far

            string defaultPepGroupName = null;
            var docStart = document;
            document = document.BeginDeferSettingsChanges(); // Prevents excessive calls to SetDocumentType etc
            var rowCount = 0;
            var rowSuccessCount = 0;
            var hasInconsistentPrecursorColumns = false;
            var hasInconsistentProductColumns = false;

            // For each row in the grid, add to or begin MoleculeGroup|Molecule|TransitionList tree
            foreach (var row in Rows)
            {
                rowCount++;
                var precursor = ReadPrecursorOrProductColumns(document, row, null, ref hasInconsistentPrecursorColumns); // Get molecule values
                if (precursor == null)
                {
                    continue; // This won't succeed, but keep gathering errors
                }
                if (_requireProductInfo && ReadPrecursorOrProductColumns(document, row, precursor, ref hasInconsistentProductColumns) == null)
                {
                    continue; // This won't succeed, but keep gathering errors
                }

                var groupName = GetCellTrimmed(row, INDEX_MOLECULE_GROUP);

                // Preexisting molecule group?
                bool pepGroupFound = false;
                if (string.IsNullOrEmpty(groupName) || !groupNamesSeen.Add(groupName)) // If group name is unique (so far), no need to search document for it
                {
                    if (ErrorAddingToExistingMoleculeGroup(ref document, precursor, groupName, defaultPepGroupName, moleculeIDsSeen, precursorsParsed, row, ref pepGroupFound))
                    {
                        continue; // This won't succeed, but keep gathering errors
                    }
                }

                if (!pepGroupFound)
                {
                    var node = GetMoleculePeptideGroup(document, row);
                    if (node == null)
                    {
                        continue; // This won't succeed, but keep gathering errors
                    }
                    IdentityPath first;
                    document = document.AddPeptideGroups(new[] {node}, false, to, out first, out _);
                    if (string.IsNullOrEmpty(defaultPepGroupName))
                    {
                        defaultPepGroupName = node.Name;
                    }

                    _firstAddedPathPepGroup = _firstAddedPathPepGroup ?? first;
                    moleculeIDsSeen.Add(precursor.MoleculeID);
                    groupNamesSeen.Add(node.Name);
                    peptideGroups?.Add(node);
                }

                rowSuccessCount++;
            }

            if (rowSuccessCount != rowCount && !tolerateErrors)
            {
                return null;
            }

            document = document.EndDeferSettingsChanges(docStart, null); // Process deferred calls to SetDocumentType etc

            firstAdded = _firstAddedPathPepGroup;

            if (peptideGroups != null)
            {
                // Update references to peptide groups in the document with the actual child nodes
                for (var index = 0; index < peptideGroups.Count; index++)
                {
                    // On the off chance that we somehow added a new node with an existing name, use the last one
                    var peptideGroupDocNode = document.MoleculeGroups.LastOrDefault(node => Equals(peptideGroups[index].Name, node.Name));
                    if (peptideGroupDocNode != null)
                    {
                        peptideGroups[index] = peptideGroupDocNode;
                    }
                }
            }

            // If this was an assay library read, return the iRT info
            if (measuredRetentionTimes != null)
            {
                foreach (var iRT in IRTs)
                {
                    measuredRetentionTimes.Add(new MeasuredRetentionTime(iRT.Key.Target, iRT.Value.Median(), true)); // N.B. we take the median IRT of differently labeled molecules
                }
            }
            // If this was an assay library read, return the library info
            if (librarySpectra != null && LibraryIntensities.Any())
            {
                foreach (var molecule in document.Molecules)
                {
                    double rt;
                    if (!IRTs.TryGetValue(molecule.Peptide, out var irts))
                    {
                        rt = 0;
                    }
                    else
                    {
                        rt = irts.Median(); // N.B. we take median value IRT of differently labeled molecules
                    }
                    foreach (var precursor in molecule.TransitionGroups)
                    {
                        var spectrum = new List<SpectrumPeaksInfo.MI>();

                        foreach (var transition in precursor.Transitions)
                        {
                            var pair = new Tuple<Peptide, TransitionGroup, Transition>(molecule.Peptide, precursor.TransitionGroup, transition.Transition);
                            if (LibraryIntensities.TryGetValue(pair, out var intensity))
                            {
                                var note = SpectrumPeakAnnotation.Create(transition.Transition.CustomIon, transition.Annotations.Note);
                                spectrum.Add(new SpectrumPeaksInfo.MI
                                {
                                    Mz = transition.Mz,
                                    Intensity = (float)intensity.Median(),
                                    Annotations = new List<SpectrumPeakAnnotation> { note }
                                });
                            }
                        }

                        if (spectrum.Any())
                        {
                            var ionMobility = IonMobilityAndCCS.GetIonMobilityAndCCS(precursor.ExplicitValues);
                            var m = new SpectrumMzInfo()
                            {
                                SourceFile = fileName,
                                IonMobility = ionMobility,
                                Key = precursor.GetLibKey(document.Settings, molecule),
                                Label = precursor.TransitionGroup.LabelType,
                                PrecursorMz = precursor.PrecursorMz,
                                RetentionTime = rt,
                                RetentionTimes = new[] { new SpectrumMzInfo.IonMobilityAndRT(fileName, ionMobility, rt, true) }.ToList(),
                                SpectrumPeaks = new SpectrumPeaksInfo(spectrum.ToArray())
                            };
                            librarySpectra.Add(m);
                        }
                    }
                }
            }
            return document;
        }

        // Returns true on error
        private bool ErrorAddingToExistingMoleculeGroup(ref SrmDocument document, ParsedIonInfo precursor, string groupName,
            string defaultPepGroupName, HashSet<MoleculeNameAndAccessions> moleculeIDsSeen,
            Dictionary<MoleculeNameAndAccessions, List<ParsedIonInfo>> parsedPrecursors,
            Row row, ref bool pepGroupFound)
        {
            var adduct = precursor?.Adduct;
            if (Adduct.IsNullOrEmpty(adduct))
            {
                return true; // Can't process
            }
            var precursorMonoMz = adduct!.MzFromNeutralMass(precursor.MonoMass);
            var precursorAverageMz = adduct!.MzFromNeutralMass(precursor.AverageMass);
            if (string.IsNullOrEmpty(groupName))
            {
                groupName = defaultPepGroupName;
            }

            // Check for consistent values across previously parsed precursors (currently just checking iRT)
            if (parsedPrecursors.TryGetValue(precursor.MoleculeID, out var knownPrecursors))
            {
                foreach (var knownPrecursor in knownPrecursors)
                {
                    // IRTs ought to agree for same label, even though we ultimately average IRT across labels
                    if (knownPrecursor.IRT.HasValue && precursor.IRT.HasValue &&
                        (Math.Abs(knownPrecursor.IRT.Value - precursor.IRT.Value) > DbIrtPeptide.IRT_MIN_DIFF) &&
                        Equals(precursor.Adduct.IsotopeLabels, knownPrecursor.Adduct.IsotopeLabels))
                    {
                        ShowTransitionError(new PasteError
                        {
                            Column = INDEX_IRT,
                            Line = row.Index,
                            Message = string.Format(Resources.PeptideGroupBuilder_FinalizeTransitionGroups_Two_transitions_of_the_same_precursor___0___m_z__1_____have_different_iRT_values___2__and__3___iRT_values_must_be_assigned_consistently_in_an_imported_transition_list_,
                                precursor.MoleculeID.Name, Math.Round(precursor.Mz, MassListImporter.MZ_ROUND_DIGITS), knownPrecursor.IRT ?? 0, precursor.IRT ?? 0)
                        });
                        return true;
                    }
                }
            }
            else
            {
                parsedPrecursors.Add(precursor.MoleculeID, new List<ParsedIonInfo>() { precursor });
            }

            foreach (var pepGroup in document.MoleculeGroups)
            {
                if (Equals(pepGroup.Name, groupName))
                {
                    // Found a molecule group with the same name - can we find an existing transition group to which we can add a transition?
                    pepGroupFound = true;
                    var pathPepGroup = new IdentityPath(pepGroup.Id);
                    bool pepFound = false;

                    if (moleculeIDsSeen.Any(p => p.IsMatchWith(precursor.MoleculeID))) // If precursor molecule is unique (so far), no need to hunt for other occurrences in the doc we're building
                    {
                        if (ErrorFindingTransitionGroupForPrecursor(ref document, precursor, row, pepGroup, adduct, precursorMonoMz, precursorAverageMz, pathPepGroup, ref pepFound))
                            return true;
                    }
                    else if (moleculeIDsSeen.Any(p => p.InconsistentWith(precursor.MoleculeID))) // e.g. same InChiKey but different CAS
                    {
                        var conflict = moleculeIDsSeen.First(p => p.InconsistentWith(precursor.MoleculeID));
                        ShowTransitionError(new PasteError
                        {
                            Column = -1,
                            Line = row.Index,
                            Message = Resources.SmallMoleculeTransitionListReader_GetMoleculeTransitionGroup_Inconsistent_molecule_description +
                                      $@" (""{conflict.AccessionNumbers.ToString(@" ")}"" / ""{precursor.MoleculeID.AccessionNumbers.ToString(@" ")}"")"
                        });
                    }
                    else if (!precursor.MoleculeID.IsEmpty)
                    {
                        moleculeIDsSeen.Add(precursor.MoleculeID); // First time seeing this, so we'll create a node rather than adding a child to an existing one
                    }

                    if (!pepFound)
                    {
                        var node = GetMoleculePeptide(document, row, pepGroup.PeptideGroup);
                        if (node == null)
                            return true;
                        document = (SrmDocument) document.Add(pathPepGroup, node);
                        _firstAddedPathPepGroup = _firstAddedPathPepGroup ?? pathPepGroup;
                    }

                    break;
                }
            }

            return false;
        }

        // Check for the various kinds of exceptions that may be thrown in the course of
        // parsing sometimes-wonky user data
        public static bool IsParserException(Exception exception)
        {
            return exception is InvalidOperationException
                   || exception is InvalidDataException
                   || exception is ArgumentException;
        }

        // Returns true on error
        private bool ErrorFindingTransitionGroupForPrecursor(ref SrmDocument document, ParsedIonInfo precursor, Row row,
            PeptideGroupDocNode pepGroup, Adduct adduct, double precursorMonoMz, double precursorAverageMz,
            IdentityPath pathPepGroup, ref bool pepFound)
        {
            foreach (var pep in pepGroup.SmallMolecules)
            {
                FindExistingMolecule(precursor, adduct, precursorMonoMz, precursorAverageMz, ref pepFound, pep);

                if (pepFound)
                {
                    bool tranGroupFound = false;
                    var pepPath = new IdentityPath(pathPepGroup, pep.Id);
                    foreach (var tranGroup in pep.TransitionGroups)
                    {
                        var pathGroup = new IdentityPath(pepPath, tranGroup.Id);
                        if (precursor.SignedMz.CompareTolerant(tranGroup.PrecursorMz, MzMatchTolerance) == 0)
                        {
                            // Special case for mz-only, charge-only transitions - may need to derive an isotope mass
                            var labeledAdduct = adduct;
                            if (adduct.IsChargeOnly && !tranGroup.CustomMolecule.HasChemicalFormula)
                            {
                                var mzCalc = adduct.ApplyToMass(pep.CustomMolecule.MonoisotopicMass);
                                if (!Equals(precursorMonoMz, mzCalc.Value))
                                {
                                    // Some kind of undescribed isotope labeling going on
                                    // No formula for label, describe as mass
                                    var massCalc = adduct.MassFromMz(precursorMonoMz, MassType.Monoisotopic);
                                    var labelMass = massCalc - pep.CustomMolecule.MonoisotopicMass;
                                    if (labelMass > 0)
                                    {
                                        labeledAdduct = adduct.ChangeIsotopeLabels(labelMass); // Isostopes add weight
                                    }
                                }
                            }

                            if (!labeledAdduct.SameEffect(tranGroup.PrecursorAdduct)) // Similar m/z, but isotope envelope might vary e.g. M2C13-H vs M1C131N15-H
                            {
                                continue;
                            }

                            tranGroupFound = true;
                            var tranFound = false;
                            string errmsg = null;
                            try
                            {
                                var tranNode = GetMoleculeTransition(document, row, pep.Peptide,
                                    tranGroup.TransitionGroup, tranGroup.ExplicitValues);
                                if (tranNode == null)
                                    return true;
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
                                    _firstAddedPathPepGroup = _firstAddedPathPepGroup ?? pathGroup;
                                }
                            }
                            catch (Exception exception) when (IsParserException(exception))
                            {
                                errmsg = exception.Message;
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
                                return true;
                            }

                            break;
                        }
                    }

                    if (!tranGroupFound)
                    {
                        var node =
                            GetMoleculeTransitionGroup(document, precursor, row, pep.Peptide);
                        if (node == null)
                            return true;
                        document = (SrmDocument) document.Add(pepPath, node);
                        _firstAddedPathPepGroup = _firstAddedPathPepGroup ?? pepPath;
                    }

                    break;
                }
            }

            return false;
        }

        private void FindExistingMolecule(ParsedIonInfo precursor, Adduct adduct, double precursorMonoMz,
            double precursorAverageMz, ref bool pepFound, PeptideDocNode pep)
        {
            // Match existing molecule if same name and accessions
            var pepMoleculeId = new MoleculeNameAndAccessions(pep.CustomMolecule.Name, pep.CustomMolecule.AccessionNumbers);
            if (!pepMoleculeId.IsEmpty)
            {
                // If user says they're the same, believe them unless accession numbers disagree
                pepFound = pepMoleculeId.IsMatchWith(precursor.MoleculeID);
            }
            else // If no names, look to other cues
            {
                var ionMonoMz =
                    adduct.MzFromNeutralMass(pep.CustomMolecule.MonoisotopicMass, MassType.Monoisotopic);
                var ionAverageMz =
                    adduct.MzFromNeutralMass(pep.CustomMolecule.AverageMass, MassType.Average);
                var labelType = precursor.IsotopeLabelType ?? IsotopeLabelType.light;
                // Match existing molecule if same formula or identical formula when stripped of labels
                pepFound |= !pep.CustomMolecule.ParsedMolecule.IsMassOnly &&
                            (Equals(pep.CustomMolecule.ParsedMolecule, precursor.NeutralFormula) ||
                             Equals(pep.CustomMolecule.ParsedMolecule, precursor.Formula) ||
                             Equals(pep.CustomMolecule.UnlabeledFormula, BioMassCalc.StripLabelsFromFormula(precursor.NeutralFormula)) ||
                             Equals(pep.CustomMolecule.UnlabeledFormula, precursor.UnlabeledFormula));
                // Match existing molecule if similar m/z at the precursor charge
                pepFound |= Math.Abs(ionMonoMz - precursorMonoMz) <= MzMatchTolerance &&
                            Math.Abs(ionAverageMz - precursorAverageMz) <=
                            MzMatchTolerance && // (we don't just check mass since we don't have a tolerance value for that)
                            (adduct.AdductCharge < 0 == precursor.Adduct.AdductCharge < 0);
                // Or no formula, and different isotope labels or matching label and mz
                pepFound |= pep.CustomMolecule.ParsedMolecule.IsMassOnly &&
                            precursor.Formula.IsMassOnly &&
                            (!pep.TransitionGroups.Any(t => Equals(t.TransitionGroup.LabelType,
                                 labelType)) || // First label of this kind
                             pep.TransitionGroups.Any(
                                 t => Equals(t.TransitionGroup.LabelType,
                                          labelType) && // Already seen this label, and
                                      precursor.SignedMz.CompareTolerant(t.PrecursorMz, MzMatchTolerance)==0)); // Matches precursor mz of similar labels
            }
        }

        // We will accept a completely empty product list as meaning 
        // "these are all precursor transitions"
        private bool GetRequireProductInfo(SrmDocument document)
        {
            var requireProductInfo = false;
            var hasInconsistentPrecursorColumns = false;
            var hasInconsistentProductColumns = false;
            foreach (var row in Rows)
            {
                if ((_hasAnyMoleculeMz && RowHasDistinctProductValue(row, INDEX_PRODUCT_MZ, INDEX_PRECURSOR_MZ)) ||
                    (_hasAnyMoleculeFormula &&
                     RowHasDistinctProductValue(row, INDEX_PRODUCT_FORMULA, INDEX_MOLECULE_FORMULA)) ||
                    (_hasAnyMoleculeCharge &&
                     RowHasDistinctProductValue(row, INDEX_PRODUCT_CHARGE, INDEX_PRECURSOR_CHARGE)) ||
                    (_hasAnyMoleculeAdduct &&
                     RowHasDistinctProductValue(row, INDEX_PRODUCT_ADDUCT, INDEX_PRECURSOR_ADDUCT)))
                {
                    requireProductInfo = true; // Product list is not completely empty, or not just precursors
                    break;
                }

                // More expensive check to see whether calculated precursor mz matches any declared product mz
                var precursor = ReadPrecursorOrProductColumns(document, row, null, ref hasInconsistentPrecursorColumns); // Get precursor values
                if (precursor != null)
                {
                    var product =
                        ReadPrecursorOrProductColumns(document, row, precursor, ref hasInconsistentProductColumns); // Get product values, if available
                    if ((product != null && precursor.SignedMz.CompareTolerant(product.SignedMz, MzMatchTolerance)!=0) || hasInconsistentProductColumns)
                    {
                        requireProductInfo = true; // Product list is not completely empty, or not just precursors
                        break;
                    }
                }
            }

            return requireProductInfo;
        }

        // Rearrange mz-only lists if necessary such that lowest mass for any given group+molecule appears before its heavy siblings,
        // so we can work out from there to identify implied isotope labels.
        //
        // We can detect implied labels only by combining declared m/z and charge (or adduct) to get the un-ionized mass,
        // then sorting on masses low to high. If they're not all the same, then the smallest derived mass must be the actual
        // mass and others must be labeled.
        private void SortSiblingsByMass()
        {
            var visited = new HashSet<Row>();
            for (var r = 0; r < Rows.Count; r++)
            {
                var row = Rows[r];
                if (!visited.Contains(row))
                {
                    visited.Add(row);
                    var group = GetCellTrimmed(row, INDEX_MOLECULE_GROUP) ?? string.Empty;
                    var name = ReadMoleculeAccessionNumberColumns(row);
                    if (row.GetCellAsDouble(INDEX_PRECURSOR_MZ, out var mzParsed))
                    {
                        var smallestMassRow = r;
                        var zString = GetCellTrimmed(row, INDEX_PRECURSOR_ADDUCT) ?? 
                                      GetCellTrimmed(row, INDEX_PRECURSOR_CHARGE) ?? string.Empty;
                        Adduct.TryParse(zString, out var adductInferred, Adduct.ADDUCT_TYPE.charge_only);
                        var smallestMass = adductInferred.MassFromMz(mzParsed, MassType.Monoisotopic);

                        for (var r2 = r + 1; r2 < Rows.Count; r2++)
                        {
                            var row2 = Rows[r2];
                            if (!visited.Contains(row2))
                            {
                                if (@group.Equals(GetCellTrimmed(row2, INDEX_MOLECULE_GROUP) ?? string.Empty) &&
                                    Equals(name, ReadMoleculeAccessionNumberColumns(row2)) &&
                                    row2.GetCellAsDouble(INDEX_PRECURSOR_MZ, out var mzParsed2))
                                {
                                    var zString2 = GetCellTrimmed(row2, INDEX_PRECURSOR_ADDUCT) ?? 
                                                   GetCellTrimmed(row2, INDEX_PRECURSOR_CHARGE) ?? string.Empty;
                                    Adduct.TryParse(zString2, out var adduct2, Adduct.ADDUCT_TYPE.charge_only);
                                    var mass2 = adduct2.MassFromMz(mzParsed2, MassType.Monoisotopic);
                                    visited.Add(row2);
                                    if (mass2 < smallestMass)
                                    {
                                        smallestMassRow = r2;
                                        smallestMass = mass2;
                                    }
                                }
                            }
                        }

                        if (smallestMassRow != r)
                        {
                            // Reorder the list such that the row with smallest calculated mass appears before its siblings
                            var rowSmallestMass = Rows[smallestMassRow];
                            Rows.RemoveAt(smallestMassRow);
                            Rows.Insert(r, rowSmallestMass);
                        }
                    }
                }
            }
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

        private int INDEX_PRECURSOR_ADDUCT
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.adductPrecursor); }
        }

        private int INDEX_PRODUCT_FORMULA
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.formulaProduct); }
        }

        private int INDEX_PRODUCT_NEUTRAL_LOSS
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.neutralLossProduct); }
        }

        private int INDEX_PRODUCT_ADDUCT
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.adductProduct); }
        }

        private int INDEX_PRECURSOR_MZ
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.mzPrecursor); }
        }

        private int INDEX_PRODUCT_MZ
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.mzProduct); }
        }

        private int INDEX_LIBRARY_INTENSITY
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.libraryIntensity); }
        }

        private int INDEX_IRT
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.iRT); }
        }

        private int INDEX_PRECURSOR_CHARGE
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

        private int INDEX_TRANSITION_NOTE
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.transitionNote); } // "Note" is legacy name for "Transition Note", we didn't handle other levels before
        }

        private int INDEX_PRECURSOR_NOTE
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.precursorNote); }
        }

        private int INDEX_MOLECULE_NOTE
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.moleculeNote); }
        }

        private int INDEX_MOLECULE_LIST_NOTE
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.moleculeListNote); }
        }

        private int INDEX_PRECURSOR_DRIFT_TIME_MSEC
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.dtPrecursor); }
        }

        private int INDEX_PRECURSOR_IM_INVERSE_K0
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.imPrecursor_invK0); }
        }

        private int INDEX_HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.dtHighEnergyOffset); }
        }

        private int INDEX_PRECURSOR_ION_MOBILITY
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.imPrecursor); }
        }

        private int INDEX_PRECURSOR_ION_MOBILITY_UNITS
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.imUnits); }
        }

        private int INDEX_HIGH_ENERGY_ION_MOBILITY_OFFSET
        {
            get { return ColumnIndex(SmallMoleculeTransitionListColumnHeaders.imHighEnergyOffset); }
        }

        private int INDEX_PRECURSOR_CCS
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

        public static int? ValidateFormulaWithMzAndAdduct(double tolerance, bool useMonoIsotopicMass, ref string moleculeFormula, ref Adduct adduct, 
            MoleculeAccessionNumbers accessionNumbers, TypedMass mz, int? charge, bool? isPositive, bool isPrecursor, out TypedMass monoMass, out TypedMass averageMass, out double? mzCalc)
        {
            var ion = new CustomIon(moleculeFormula);
            monoMass = ion.GetMass(MassType.Monoisotopic);
            averageMass = ion.GetMass(MassType.Average);
            var mass = mz.IsMonoIsotopic() ? monoMass : averageMass;
            var isotopeLabels = accessionNumbers.FindLabels(); // InChi may have label information

            // Does given charge, if any, agree with mass and mz?
            var adductInferred = adduct;
            if (adduct.IsEmpty && (charge??0) != 0)
            {
                adductInferred = Adduct.NonProteomicProtonatedFromCharge(charge.Value);
            }
            mzCalc = adductInferred.AdductCharge != 0 ? adductInferred.MzFromNeutralMass(mass) : (double?) null;
            if (mzCalc.HasValue && tolerance >= (Math.Abs(mzCalc.Value - mz)))
            {
                adduct = adductInferred;
                return charge;
            }

            // See if this can be explained by (de)protonation within acceptable charge range
            var minCharge = (isPositive ?? false)
                ? TransitionGroup.MIN_PRECURSOR_CHARGE
                : -TransitionGroup.MAX_PRECURSOR_CHARGE;
            var maxCharge = (isPositive ?? true)
                ? TransitionGroup.MAX_PRECURSOR_CHARGE
                : -TransitionGroup.MIN_PRECURSOR_CHARGE;
            adductInferred = TransitionCalc.CalcCharge(mass, mz, tolerance, true,
                minCharge,
                maxCharge, new int[0],
                TransitionCalc.MassShiftType.none, out _, out _);

            if (adductInferred.IsEmpty)
            {
                // See if this can be explained by the more common adduct types, possibly with water loss
                var leastError = double.MaxValue;
                var bestMatch = Adduct.EMPTY;
                foreach (var text in Adduct.DEFACTO_STANDARD_ADDUCTS.Concat(Adduct.COMMON_CHARGEONLY_ADDUCTS))
                {
                    adductInferred = Adduct.FromString(text, Adduct.ADDUCT_TYPE.non_proteomic, null);
                    if (isotopeLabels != null && isotopeLabels.Any())
                    {
                        adductInferred = adductInferred.ChangeIsotopeLabels(isotopeLabels);
                    }
                    if (minCharge <= adductInferred.AdductCharge && adductInferred.AdductCharge <= maxCharge)
                    {
                        bool CheckLeastError(double errVal)
                        {
                            if (errVal <= tolerance && errVal < leastError && // Close to m/z 
                                adductInferred.TryApplyToMolecule(ion.ParsedMolecule, out var _)) // Sanity check for formula with this adduct
                            {
                                bestMatch = adductInferred;
                                leastError = errVal;
                                return true;
                            }
                            return false;
                        }

                        var err = Math.Abs(adductInferred.MzFromNeutralMass(mass) - mz);
                        if (!CheckLeastError(err) && isPrecursor)
                        {
                            // Try water loss
                            var parts = text.Split('+', '-'); // Only for simple adducts like M+H, M+Na etc
                            if (parts.Length == 2)
                            {
                                var tail = text.Substring(parts[0].Length);
                                adductInferred = Adduct.FromString(parts[0] + @"-H2O" + tail, Adduct.ADDUCT_TYPE.non_proteomic, null);
                                err = Math.Abs(adductInferred.MzFromNeutralMass(mass) - mz);
                                if (!CheckLeastError(err))
                                {
                                    // Try double water loss (as in https://www.drugbank.ca/spectra/mzcal/DB01299 )
                                    adductInferred = Adduct.FromString(parts[0] + @"-2H2O" + tail, Adduct.ADDUCT_TYPE.non_proteomic, null);
                                    err = Math.Abs(adductInferred.MzFromNeutralMass(mass) - mz);
                                    CheckLeastError(err);
                                }
                            }
                        }
                    }
                }
                adductInferred = bestMatch;
            }

            if (adductInferred.IsEmpty)
            {
                // That formula and this mz don't yield a reasonable charge state - try adding an H
                var ion2 = ion.FormulaWithAdductApplied.AdjustElementCount(BioMassCalc.H,1);
                monoMass = BioMassCalc.MONOISOTOPIC.CalculateMass(ion2);
                averageMass = BioMassCalc.AVERAGE.CalculateMass(ion2);
                mass = useMonoIsotopicMass
                    ? monoMass
                    : averageMass;
                adductInferred = TransitionCalc.CalcCharge(mass, mz, tolerance, true,
                    minCharge,
                    maxCharge, new int[0], TransitionCalc.MassShiftType.none, out _, out _);
                if (!adductInferred.IsEmpty)
                {
                    moleculeFormula = ion2.ToString();
                }
                else
                {
                    monoMass = TypedMass.ZERO_MONO_MASSNEUTRAL;
                    averageMass = TypedMass.ZERO_AVERAGE_MASSNEUTRAL;
                }
            }

            charge = adductInferred.IsEmpty ? (int?) null : adductInferred.AdductCharge;
            if (charge.HasValue)
            {
                adduct = adductInferred;
            }
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
            var trimmed = str.Trim();
            return (trimmed.Length == 0) ? null : trimmed;
        }

        public static string GetCellTrimmed(Row row, int col)
        {
            return NullForEmpty(row.GetCell(col));
        }

        private class ParsedIonInfo : IonInfo
        {
            public MoleculeNameAndAccessions MoleculeID { get; private set; } // Name and InChiKey, CAS etc
            public string TransitionNote { get; private set; }
            public string PrecursorNote { get; private set; }
            public string MoleculeNote { get; private set; }
            public string MoleculeListNote { get; private set; }
            public TypedMass Mz { get; private set; } // Not actually a mass, of course, but useful to know if its based on mono vs avg mass
            public Adduct Adduct { get; private set; }
            public SignedMz SignedMz => new SignedMz(Mz, Adduct.AdductCharge < 0); 
            public TypedMass MonoMass { get; private set; }
            public TypedMass AverageMass { get; private set; }
            public IsotopeLabelType IsotopeLabelType { get; private set; }
            public ExplicitRetentionTimeInfo ExplicitRetentionTime { get; private set; }
            public ExplicitTransitionGroupValues ExplicitTransitionGroupValues { get; private set; }
            public ExplicitTransitionValues ExplicitTransitionValues { get; private set; }

            // Fields parsed for assay libraries
            public double? LibraryIntensity { get; private set; } // Per-fragment
            public double? IRT { get; private set; } // Per-precursor

            public ParsedIonInfo(string name, string formula, Adduct adduct, 
                TypedMass mz, // Not actually a mass, of course, but still useful to know if based on Mono or Average mass
                TypedMass monoMass,
                TypedMass averageMass,
                IsotopeLabelType isotopeLabelType,
                ExplicitRetentionTimeInfo explicitRetentionTime,
                ExplicitTransitionGroupValues explicitTransitionGroupValues,
                ExplicitTransitionValues explicitTransitionValues,
                double? libraryIntensity,
                double? iRT,
                string transitionNote,
                string precursorNote,
                string moleculeNote,
                string moleculeListNote,
                MoleculeAccessionNumbers accessionNumbers) : base(formula)
            {
                MoleculeID = string.IsNullOrEmpty(name) && MoleculeAccessionNumbers.IsNullOrEmpty(accessionNumbers)
                    ? MoleculeNameAndAccessions.EMPTY
                    : new MoleculeNameAndAccessions(name, accessionNumbers);
                Adduct = adduct;
                Mz = mz;
                MonoMass = monoMass;
                AverageMass = averageMass;
                IsotopeLabelType = isotopeLabelType;
                ExplicitRetentionTime = explicitRetentionTime;
                ExplicitTransitionGroupValues = explicitTransitionGroupValues;
                ExplicitTransitionValues = explicitTransitionValues;
                LibraryIntensity = libraryIntensity;
                IRT = iRT;
                TransitionNote = transitionNote; // "Note" is legacy name for "transition note", we didn't handle other kinds
                PrecursorNote = precursorNote;
                MoleculeNote = moleculeNote;
                MoleculeListNote = moleculeListNote;
                if (Formula.IsMassOnly && !Formula.IsEmpty)
                {
                    // BioMassCalc parser will accept a mass-only formula, but we don't want to allow that here
                    throw new ArgumentException(BioMassCalc.FormatArgumentExceptionMessage(formula));
                }
            }

            public ParsedIonInfo ChangeTransitionNote(string note)
            {
                return ChangeProp(ImClone(this), im =>
                {
                    im.TransitionNote = note;
                });
            }

            public ParsedIonInfo ChangePrecursorNote(string note)
            {
                return ChangeProp(ImClone(this), im =>
                {
                    im.PrecursorNote = note;
                });
            }

            public ParsedIonInfo ChangeMoleculeNote(string note)
            {
                return ChangeProp(ImClone(this), im =>
                {
                    im.MoleculeNote = note;
                });
            }

            public ParsedIonInfo ChangeMoleculeListNote(string note)
            {
                return ChangeProp(ImClone(this), im =>
                {
                    im.MoleculeListNote = note;
                });
            }

            public CustomMolecule ToCustomMolecule()
            {
                return Formula.IsMassOnly ?
                    new CustomMolecule(MonoMass, AverageMass, MoleculeID.Name ?? string.Empty, MoleculeID.AccessionNumbers) :
                    new CustomMolecule(Formula, MoleculeID.Name ?? string.Empty, MoleculeID.AccessionNumbers);
            }

            public override string ToString()
            {
                return base.ToString() + Adduct.ToString();
            }
        }

        private bool ValidateCharge(int? charge, bool getPrecursorColumns, out string errMessage)
        {
            var absCharge = Math.Abs(charge ?? 0);
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse (in case we ever set min charge > 1)
            if (getPrecursorColumns && absCharge != 0 && (absCharge < TransitionGroup.MIN_PRECURSOR_CHARGE ||
                                                          absCharge > TransitionGroup.MAX_PRECURSOR_CHARGE))
            {
                errMessage = String.Format(
                    Resources.Transition_Validate_Precursor_charge__0__must_be_non_zero_and_between__1__and__2__,
                    charge, -TransitionGroup.MAX_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE);
                return false;
            }
            else if (!getPrecursorColumns && absCharge != 0 &&
                     // ReSharper disable once ConditionIsAlwaysTrueOrFalse (in case we ever set min charge > 1)
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

        private MoleculeNameAndAccessions ReadMoleculeAccessionNumberColumns(Row row)
        {
            if (_consensusAccessionNumbers.TryGetValue(row.Index, out var id))
            {
                return id;
            }

            var name = GetCellTrimmed(row, INDEX_MOLECULE_NAME);

            var moleculeIdKeys = new Dictionary<string, string>();

            var inchikeyCol = ColumnIndex(SmallMoleculeTransitionListColumnHeaders.idInChiKey);
            var inchikey = GetCellTrimmed(row, inchikeyCol);
            if (inchikey != null)
            {
                // Should have form like BQJCRHHNABKAKU-KBQPJGBKSA-N
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
            var hmdb = GetCellTrimmed(row, hmdbCol);
            if (hmdb != null)
            {
                // Should have form like HMDB0001, though we will accept just 00001
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
            var inchi = GetCellTrimmed(row, inchiCol);
            if (inchi != null)
            {
                // Should have form like "InChI=1S/C4H8O3/c1-3(5)2-4(6)7/h3,5H,2H2,1H3,(H,6,7)/t3-/m1/s", 
                // though we will accept just "1S/C4H8O3/c1-3(5)2-4(6)7/h3,5H,2H2,1H3,(H,6,7)/t3-/m1/s"
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
            var cas = GetCellTrimmed(row, casCol);
            if (cas != null)
            {
                // Should have form like "123-45-6", 
                var parts = cas.Split('-');
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
            var smiles = GetCellTrimmed(row, smilesCol);
            if (smiles != null)
            {
                // Should have form like CCc1nn(C)c2c(=O)[nH]c(nc12)c3cc(ccc3OCC)S(=O)(=O)N4CCN(C)CC4 but we'll accept anything for now, having no proper parser
                moleculeIdKeys.Add(MoleculeAccessionNumbers.TagSMILES, smiles);
            }

            var keggCol = ColumnIndex(SmallMoleculeTransitionListColumnHeaders.idKEGG);
            var kegg = GetCellTrimmed(row, keggCol);
            if (kegg != null)
            {
                // Should have form like C07481 but we'll accept anything - might not be a compound ID, conceivably: e.g D00528 for Drug caffeine instead of C07481 for Compound caffeine
                moleculeIdKeys.Add(MoleculeAccessionNumbers.TagKEGG, kegg);
            }

            var accessionNumbers = !moleculeIdKeys.Any()
                ? MoleculeAccessionNumbers.EMPTY
                : new MoleculeAccessionNumbers(moleculeIdKeys);

            // Now go see if these parsed values complement and combine with any previously parsed rows
            return MoleculeNameAndAccessions.GetConsensusAccessionNumbers(name, accessionNumbers, row.Index, _consensusAccessionNumbers);
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
                    { @"1/K0", eIonMobilityUnits.inverse_K0_Vsec_per_cm2 },
                    { string.Empty, eIonMobilityUnits.none }
                }).ToDictionary(x => x.Key, x=> x.Value);

        private bool _hasAnyMoleculeMz;
        private bool _hasAnyMoleculeFormula;
        private bool _hasAnyMoleculeCharge;
        private bool _hasAnyMoleculeAdduct;
        private IdentityPath _firstAddedPathPepGroup;
        private bool _requireProductInfo;

        // Used in the case where later entries add details to accession numbers
        // We use a mapping from row index to name+accession as we may not see rows in original order (might be mass sorted)
        private Dictionary<int, MoleculeNameAndAccessions> _consensusAccessionNumbers = new Dictionary<int, MoleculeNameAndAccessions>(); 

        public static string GetAcceptedIonMobilityUnitsString()
        {
            return string.Join(@", ", IonMobilityUnitsSynonyms.Keys);
        }

        /// <summary>
        /// Run through all rows gathering and possibly unifying molecule details if some have missing but not conflicting details
        /// </summary>
        private void GetMoleculeDetails()
        {
            foreach (var row in Rows)
            {
                ReadMoleculeAccessionNumberColumns(row);
            }
        }

        // We need some combination of:
        //  Formula and mz
        //  Formula and charge
        //  mz and charge
        private ParsedIonInfo ReadPrecursorOrProductColumns(SrmDocument document,
            Row row,
            ParsedIonInfo precursorInfo,
            ref bool hasInconsistentColumns) // Set true when there are ion columns found but they aren't quite right - missing values etc
        {
            var getPrecursorColumns = precursorInfo == null;
            int indexFormula = getPrecursorColumns ? INDEX_MOLECULE_FORMULA : INDEX_PRODUCT_FORMULA;
            int indexAdduct = getPrecursorColumns ? INDEX_PRECURSOR_ADDUCT : INDEX_PRODUCT_ADDUCT;
            int indexMz = getPrecursorColumns ? INDEX_PRECURSOR_MZ : INDEX_PRODUCT_MZ;
            int indexCharge = getPrecursorColumns ? INDEX_PRECURSOR_CHARGE : INDEX_PRODUCT_CHARGE;
            int indexNeutralLoss = getPrecursorColumns ? -1 : INDEX_PRODUCT_NEUTRAL_LOSS;
            var formula = GetCellTrimmed(row, indexFormula);
            var transitionNote = GetCellTrimmed(row, INDEX_TRANSITION_NOTE);
            var precursorNote = GetCellTrimmed(row, INDEX_PRECURSOR_NOTE);
            var moleculeNote = GetCellTrimmed(row, INDEX_MOLECULE_NOTE);
            var moleculeListNote = GetCellTrimmed(row, INDEX_MOLECULE_LIST_NOTE);
            // TODO(bspratt) use CAS or HMDB etc lookup to fill in missing inchikey - and use any to fill in formula
            var moleculeID = (getPrecursorColumns ? ReadMoleculeAccessionNumberColumns(row) : null) ?? MoleculeNameAndAccessions.EMPTY;
            var name = getPrecursorColumns ? moleculeID.Name : GetCellTrimmed(row, INDEX_PRODUCT_NAME);
            IsotopeLabelType isotopeLabelType = null;
            bool badMz = false;
            var mzType = getPrecursorColumns 
                ? document.Settings.TransitionSettings.Prediction.PrecursorMassType
                : document.Settings.TransitionSettings.Prediction.FragmentMassType;
            double mzParsed;
            if (!row.GetCellAsDouble(indexMz, out mzParsed))
            {
                if (!String.IsNullOrEmpty(GetCellTrimmed(row, indexMz)))
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
                    Message = String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_m_z_value__0_, GetCellTrimmed(row, indexMz))
                });
                return null;
            }
            double? libraryIntensity = null;
            if (row.GetCellAsDouble(INDEX_LIBRARY_INTENSITY, out var li))
            {
                libraryIntensity = li;
            }
            double? iRT = null;
            if (row.GetCellAsDouble(INDEX_IRT, out var irt))
            {
                iRT = irt;
            }
            int? charge = null;
            var adduct = Adduct.EMPTY;
            int trycharge;
            if (Int32.TryParse(GetCellTrimmed(row, indexCharge), out trycharge))
                charge = trycharge;
            else if (!String.IsNullOrEmpty(GetCellTrimmed(row, indexCharge)))
            {
                Adduct test;
                if (Adduct.TryParse(GetCellTrimmed(row, indexCharge), out test))
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
            var ionMobility = new Dictionary<eIonMobilityUnits, double?>();
            TypedMass monoMass;
            TypedMass averageMass;
            ExplicitTransitionValues explicitTransitionValues;
            ExplicitTransitionGroupValues explicitTransitionGroupValues;
            ExplicitRetentionTimeInfo retentionTimeInfo;
            

            ParsedIonInfo VerifyAndCreateParsedIonInfo()
            {
                if (!iRT.HasValue && INDEX_IRT != -1 && getPrecursorColumns)
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = INDEX_IRT,
                        Line = row.Index,
                        Message = string.Format(
                            Resources.MassListImporter_AddRow_Invalid_iRT_value_at_precusor_m_z__0__for_peptide__1_,
                            mz,
                            moleculeID.Name)
                    });
                    return null;
                }

                if (!libraryIntensity.HasValue && INDEX_LIBRARY_INTENSITY != -1 && getPrecursorColumns)
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = INDEX_LIBRARY_INTENSITY,
                        Line = row.Index,
                        Message = string.Format(
                            Resources.MassListImporter_AddRow_Invalid_library_intensity_at_precursor__0__for_peptide__1_,
                            mz,
                            moleculeID.Name)
                    });
                    return null;
                }

                return new ParsedIonInfo(name, formula, adduct, mz, monoMass, averageMass, isotopeLabelType,
                    retentionTimeInfo, explicitTransitionGroupValues, explicitTransitionValues, libraryIntensity, iRT,
                    transitionNote, precursorNote, moleculeNote, moleculeListNote,
                    moleculeID.AccessionNumbers);
            }

            if (getPrecursorColumns)
            {
                // Do we have any molecule IDs?
                moleculeID = ReadMoleculeAccessionNumberColumns(row);
                if (moleculeID == null)
                {
                    return null; // Some error occurred
                }
                var label = GetCellTrimmed(row, INDEX_LABEL_TYPE);
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
                if (row.GetCellAsDouble(INDEX_COMPENSATION_VOLTAGE, out dtmp))
                {
                    ionMobility[eIonMobilityUnits.compensation_V] = dtmp;
                }
                else if (!String.IsNullOrEmpty(GetCellTrimmed(row, INDEX_COMPENSATION_VOLTAGE)))
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
                else if (!String.IsNullOrEmpty(GetCellTrimmed(row, INDEX_RETENTION_TIME)))
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
                else if (!String.IsNullOrEmpty(GetCellTrimmed(row, INDEX_RETENTION_TIME_WINDOW)))
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

            if (row.GetCellAsDouble(INDEX_COLLISION_ENERGY, out dtmp) && dtmp > 0)
            {
                collisionEnergy = dtmp;
            }
            else if (!String.IsNullOrEmpty(GetCellTrimmed(row, INDEX_COLLISION_ENERGY)))
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
            else if (!String.IsNullOrEmpty(GetCellTrimmed(row, INDEX_SLENS)))
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_SLENS,
                    Line = row.Index,
                    Message = String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_S_Lens_value__0_, row.GetCell(INDEX_SLENS))
                });
                return null;
            }
            if (row.GetCellAsDouble(INDEX_CONE_VOLTAGE, out dtmp))
                coneVoltage = dtmp;
            else if (!String.IsNullOrEmpty(GetCellTrimmed(row, INDEX_CONE_VOLTAGE)))
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_CONE_VOLTAGE,
                    Line = row.Index,
                    Message = String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_cone_voltage_value__0_, row.GetCell(INDEX_CONE_VOLTAGE))
                });
                return null;
            }
            if (row.GetCellAsDouble(INDEX_DECLUSTERING_POTENTIAL, out dtmp) && dtmp > 0)
            {
                declusteringPotential = dtmp;
            }
            else if (!String.IsNullOrEmpty(GetCellTrimmed(row, INDEX_DECLUSTERING_POTENTIAL)))
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_DECLUSTERING_POTENTIAL,
                    Line = row.Index,
                    Message = String.Format(Resources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_declustering_potential__0_, row.GetCell(INDEX_DECLUSTERING_POTENTIAL))
                });
                return null;
            }

            if (row.GetCellAsDouble(INDEX_PRECURSOR_IM_INVERSE_K0, out dtmp))
            {
                ionMobility[eIonMobilityUnits.inverse_K0_Vsec_per_cm2] = dtmp;
            }
            else if (!String.IsNullOrEmpty(GetCellTrimmed(row, INDEX_PRECURSOR_IM_INVERSE_K0)))
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_PRECURSOR_IM_INVERSE_K0,
                    Line = row.Index,
                    Message = String.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_ion_mobility_value__0_, row.GetCell(INDEX_PRECURSOR_IM_INVERSE_K0))
                });
                return null;
            }

            if (row.GetCellAsDouble(INDEX_PRECURSOR_DRIFT_TIME_MSEC, out dtmp))
            {
                ionMobility[eIonMobilityUnits.drift_time_msec] = dtmp;
            }
            else if (!String.IsNullOrEmpty(GetCellTrimmed(row, INDEX_PRECURSOR_DRIFT_TIME_MSEC)))
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_PRECURSOR_DRIFT_TIME_MSEC,
                    Line = row.Index,
                    Message = String.Format(ModelResources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_drift_time_value__0_, row.GetCell(INDEX_PRECURSOR_DRIFT_TIME_MSEC))
                });
                return null;
            }
            double? ionMobilityHighEnergyOffset = null;
            if (row.GetCellAsDouble(INDEX_HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC, out dtmp))
            {
                ionMobilityHighEnergyOffset = dtmp;
            }
            else if (GetCellTrimmed(row, INDEX_HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC) != null)
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC,
                    Line = row.Index,
                    Message = String.Format(ModelResources.PasteDlg_ReadPrecursorOrProductColumns_Invalid_drift_time_high_energy_offset_value__0_, row.GetCell(INDEX_HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC))
                });
                return null;
            }
            var unitsIM = GetCellTrimmed(row, INDEX_PRECURSOR_ION_MOBILITY_UNITS);
            eIonMobilityUnits declaredUnitsIM = eIonMobilityUnits.none;
            if (unitsIM != null)
            {
                if (!IonMobilityUnitsSynonyms.TryGetValue(unitsIM, out declaredUnitsIM))
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = INDEX_PRECURSOR_ION_MOBILITY_UNITS,
                        Line = row.Index,
                        Message = String.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_ion_mobility_units_value__0___accepted_values_are__1__, row.GetCell(INDEX_PRECURSOR_ION_MOBILITY_UNITS), GetAcceptedIonMobilityUnitsString())
                    });
                    return null;
                }
            }

            if (row.GetCellAsDouble(INDEX_PRECURSOR_ION_MOBILITY, out dtmp))
            {
                if (declaredUnitsIM == eIonMobilityUnits.none)
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = INDEX_PRECURSOR_ION_MOBILITY,
                        Line = row.Index,
                        Message = ModelResources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Missing_ion_mobility_units
                    });
                    return null;
                }
                ionMobility[declaredUnitsIM] = dtmp;
            }
            else if (GetCellTrimmed(row, INDEX_PRECURSOR_ION_MOBILITY) != null)
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_PRECURSOR_ION_MOBILITY,
                    Line = row.Index,
                    Message = String.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_ion_mobility_value__0_, row.GetCell(INDEX_PRECURSOR_ION_MOBILITY))
                });
                return null;
            }
            if (row.GetCellAsDouble(INDEX_HIGH_ENERGY_ION_MOBILITY_OFFSET, out dtmp))
            {
                ionMobilityHighEnergyOffset = dtmp;
            }
            else if (GetCellTrimmed(row, INDEX_HIGH_ENERGY_ION_MOBILITY_OFFSET) != null)
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_HIGH_ENERGY_ION_MOBILITY_OFFSET,
                    Line = row.Index,
                    Message = String.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_ion_mobility_high_energy_offset_value__0_, row.GetCell(INDEX_HIGH_ENERGY_ION_MOBILITY_OFFSET))
                });
                return null;
            }
            double? ccsPrecursor = precursorInfo == null ? null : precursorInfo.ExplicitTransitionGroupValues.CollisionalCrossSectionSqA;
            if (row.GetCellAsDouble(INDEX_PRECURSOR_CCS, out dtmp))
                ccsPrecursor = dtmp;
            else if (GetCellTrimmed(row, INDEX_PRECURSOR_CCS) != null)
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_PRECURSOR_CCS,
                    Line = row.Index,
                    Message = String.Format(Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Invalid_collisional_cross_section_value__0_, row.GetCell(INDEX_PRECURSOR_CCS))
                });
                return null;
            }

            if (!ProcessAdduct(row, indexAdduct, indexFormula, getPrecursorColumns, ref formula, ref adduct, ref charge))
            {
                return null;
            }

            if (!ProcessNeutralLoss(row, indexNeutralLoss, ref formula))
            {
                hasInconsistentColumns = true; // Neutral loss value is given, but there's an issue. N.B. a blank entry is not an issue.
                return null;
            }

            int errColumn = indexFormula;
            int countValues = 0;
            if (charge.HasValue && charge.Value != 0)
            {
                countValues++;
                if (adduct.IsEmpty)
                {
                    // When no adduct is given, either it's implied (de)protonation, or formula is inherently charged. Formula and mz are a clue. Or it might be a precursor declaration.
                    try
                    {
                        if (precursorInfo != null && charge.Value.Equals(precursorInfo.Adduct.AdductCharge) && Math.Abs(mz - precursorInfo.Mz) <= MzMatchTolerance )
                        {
                            adduct = precursorInfo.Adduct; // Charge matches, mz matches, this is probably a precursor fragment declaration
                        }
                        else
                        {
                            adduct = DetermineAdductFromFormulaChargeAndMz(formula, charge.Value, mz);
                        }
                    }
                    catch (Exception e) when (IsParserException(e))
                    {
                        ShowTransitionError(new PasteError
                        {
                            Column = indexFormula >= 0 ? indexFormula : indexMz,
                            Line = row.Index,
                            Message = e.Message
                        });
                        return null;
                    }
                    row.UpdateCell((indexAdduct < 0) ? indexCharge : indexAdduct, adduct.AdductFormula);
                }
            }
            if (mz > 0)
            {
                countValues++;
            }
            if (NullForEmpty(formula) != null)
            {
                countValues++;
            }
            if (countValues == 0 && !getPrecursorColumns &&
                // A precursor transition usually has no fragment name, or name is "precursor", or name is repeated molecule name
                (string.IsNullOrEmpty(name) || Equals(name, precursorInfo.MoleculeID.Name) ||
                 Equals(name, @"precursor") || Equals(name, IonTypeExtension.GetLocalizedString(IonType.precursor)))) // Handle local language as well
            {
                // No product info found in this row, assume that this is a precursor declaration
                return precursorInfo.ChangePrecursorNote(precursorNote ?? transitionNote);
            }

            if (countValues == 1 && !getPrecursorColumns && mz > 0 && Math.Abs(precursorInfo.Adduct.AdductCharge) == 2)
            {
                // If all we're given for product is m/z, and precursor charge is 2, assume product charge is 1
                charge = precursorInfo.Adduct.AdductCharge == -2 ? -1 : 1;
                adduct = Adduct.FromChargeNoMass(charge.Value);
                countValues++;
            }

            string errMessage = null;
            if (countValues >= 2) // Do we have at least 2 of charge, mz, formula?
            {
                if (ionMobility.Count > 1)
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = INDEX_PRECURSOR_ION_MOBILITY,
                        Line = row.Index,
                        Message = GetMultipleIonMobilitiesErrorMessage(ionMobility)
                    });
                    return null;

                }
                retentionTimeInfo = retentionTime.HasValue
                    ? new ExplicitRetentionTimeInfo(retentionTime.Value, retentionTimeWindow)
                    : null;
                explicitTransitionValues = ExplicitTransitionValues.Create(collisionEnergy,ionMobilityHighEnergyOffset, slens, coneVoltage, declusteringPotential);
                explicitTransitionGroupValues = ExplicitTransitionGroupValues.Create(collisionEnergy, ionMobility.FirstOrDefault().Value, ionMobility.FirstOrDefault().Key, ccsPrecursor);
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
                            if (!Equals(ion.FormulaWithAdductApplied, ion.UnlabeledFormula)) // Formula+adduct contained some heavy isotopes
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
                            var labels = BioMassCalc.FindIsotopeLabelsInFormula(formula);
                            if ((labels == null || !labels.Any()) && precursorInfo != null)
                            {
                                labels = precursorInfo.MoleculeID.FindLabels(); // InChi may have label information
                            }
                            if (labels != null && labels.Any())
                            {
                                adduct = adduct.ChangeIsotopeLabels(labels);
                                formula = BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula(formula);
                                row.UpdateCell(indexFormula, formula);
                                row.UpdateCell((indexAdduct < 0) ? indexCharge : indexAdduct, adduct.AsFormulaOrSignedInt());
                            }
                        }

                        if (mz > 0)
                        {
                            // Is the ion's formula the old style where user expected us to add a hydrogen? 
                            double? mzCalc;
                            var tolerance = document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
                            var useMonoisotopicMass = document.Settings.TransitionSettings.Prediction.FragmentMassType.IsMonoisotopic();
                            var expectIsPositiveCharge = (precursorInfo == null || precursorInfo.Adduct.IsEmpty) ? 
                                (charge ?? 0) != 0 ? (charge > 0) : (bool?)null:
                                precursorInfo.Adduct.AdductCharge > 0;
                            var initialCharge = charge;
                            var initialAdduct = adduct;
                            charge = ValidateFormulaWithMzAndAdduct(tolerance, useMonoisotopicMass,
                                ref formula, ref adduct, moleculeID?.AccessionNumbers, mz, charge, expectIsPositiveCharge, getPrecursorColumns, out monoMass, out averageMass, out mzCalc);
                            row.UpdateCell(indexFormula, formula);
                            massOk = monoMass < CustomMolecule.MAX_MASS && averageMass < CustomMolecule.MAX_MASS &&
                                     !(massTooLow = charge.HasValue && (monoMass < CustomMolecule.MIN_MASS || averageMass < CustomMolecule.MIN_MASS)); // Null charge => masses are 0 but meaningless
                            if (adduct.IsEmpty && charge.HasValue)
                            {
                                adduct = Adduct.FromCharge(charge.Value, Adduct.ADDUCT_TYPE.non_proteomic);
                            }
                            if (massOk)
                            {
                                if (charge.HasValue)
                                {
                                    row.UpdateCell(indexCharge, charge.Value);
                                    if (!Equals(adduct, initialAdduct))
                                    {
                                        row.UpdateCell((indexAdduct < 0) ? indexCharge : indexAdduct, adduct); // Show the deduced adduct
                                    }
                                    return VerifyAndCreateParsedIonInfo();
                                }
                                else if (mzCalc.HasValue)
                                {
                                    // There was an initial charge value, but it didn't make sense with formula and proposed mz
                                    errMessage = String.Format(getPrecursorColumns
                                        ? Resources.SmallMoleculeTransitionListReader_Precursor_mz_does_not_agree_with_calculated_value_
                                        : Resources.SmallMoleculeTransitionListReader_Product_mz_does_not_agree_with_calculated_value_,
                                        (float)mz, (float)mzCalc.Value, (float)(mzCalc.Value - mz), (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance);
                                    errColumn = indexMz;
                                }
                                else
                                {
                                    // No charge state given, and mz makes no sense with formula
                                    errMessage = getPrecursorColumns
                                        ? Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Precursor_formula_and_m_z_value_do_not_agree_for_any_charge_state_
                                        : Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Product_formula_and_m_z_value_do_not_agree_for_any_charge_state_;
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
                            mz = ValidateFormulaWithCharge(mzType, formula, adduct, out monoMass, out averageMass);
                            massOk = !((monoMass >= CustomMolecule.MAX_MASS || averageMass >= CustomMolecule.MAX_MASS)) &&
                                     !(massTooLow = (monoMass < CustomMolecule.MIN_MASS || averageMass < CustomMolecule.MIN_MASS));
                            row.UpdateCell(indexMz, mz);
                            if (massOk)
                            {
                                return VerifyAndCreateParsedIonInfo();
                            }
                        }
                    }
                    catch (Exception exception) when (IsParserException(exception))
                    {
                        massErrMsg = exception.Message;
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
                    averageMass =  adduct.MassFromMz(mz, MassType.Average);
                    massOk = monoMass < CustomMolecule.MAX_MASS && averageMass < CustomMolecule.MAX_MASS &&
                             !(massTooLow = (monoMass < CustomMolecule.MIN_MASS || averageMass < CustomMolecule.MIN_MASS));
                    errColumn = indexMz;
                    if (massOk)
                    {
                        return VerifyAndCreateParsedIonInfo();
                    }
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
                else if (countValues < 2)
                {
                    errMessage = getPrecursorColumns
                            ? Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Precursor_needs_values_for_any_two_of__Formula__m_z_or_Charge_
                            : Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Product_needs_values_for_any_two_of__Formula__m_z_or_Charge_;
                    hasInconsistentColumns = true;
                }
                else
                {
                    // Don't just leave it blank
                    errMessage = ModelResources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_unknown_error;
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

        public static string GetMultipleIonMobilitiesErrorMessage(Dictionary<eIonMobilityUnits, double?> ionMobility)
        {
            return Resources.SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Multiple_ion_mobility_declarations +
                   $@" ({string.Join(@", ", ionMobility.Select(kvp => $@"{IonMobilityFilter.IonMobilityUnitsL10NString(kvp.Key)} = {kvp.Value}"))}";
        }

        private bool ProcessNeutralLoss(Row row, int indexNeutralLoss, ref string formula)
        {
            // Derive product formula from neutral loss?
            var neutralLoss = GetCellTrimmed(row, indexNeutralLoss);
            if (!string.IsNullOrEmpty(neutralLoss))
            {

                var precursorFormula = Adduct.SplitFormulaAndTrailingAdduct(GetCellTrimmed(row, INDEX_MOLECULE_FORMULA), Adduct.ADDUCT_TYPE.charge_only, out _); // Removes any adduct description e.g. C12H5[M+H] => C12H5
                if (string.IsNullOrEmpty(precursorFormula))
                {
                    // There's no use for a loss formula if there's no precursor formula
                    ShowTransitionError(new PasteError
                    {
                        Column = indexNeutralLoss,
                        Line = row.Index,
                        Message = Resources.SmallMoleculeTransitionListReader_ProcessNeutralLoss_Cannot_use_product_neutral_loss_chemical_formula_without_a_precursor_chemical_formula
                    });
                    return false;
                }

                // Parse molecule and neutral loss formulas to dictionaries, with syntax checking
                // N.B. here we use pwiz.Skyline.Util.BioMassCalc rather than pwiz.Common.Chemistry.Molecule because it
                // understands Skyline isotope symbols (e.g. H', C" etc) while pwiz.Common.Chemistry.Molecule does not
                if (!ParsedMolecule.TryParseFormula(precursorFormula, out var precursorMolecule, out var errMessage))
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = INDEX_MOLECULE_FORMULA,
                        Line = row.Index,
                        Message = errMessage
                    });
                    return false;
                }

                if (precursorMolecule.IsMassOnly)
                {
                    // There's no use for a loss formula if there's no precursor formula
                    ShowTransitionError(new PasteError
                    {
                        Column = indexNeutralLoss,
                        Line = row.Index,
                        Message = Resources.SmallMoleculeTransitionListReader_ProcessNeutralLoss_Cannot_use_product_neutral_loss_chemical_formula_without_a_precursor_chemical_formula
                    });
                    return false;
                }

                if (!ParsedMolecule.TryParseFormula(neutralLoss, out var lossMolecule, out errMessage))
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = indexNeutralLoss,
                        Line = row.Index,
                        Message = errMessage
                    });
                    return false;
                }
                else if (lossMolecule.IsMassOnly)
                {
                    // BioMassCalc parser will accept a mass-only formula, but we don't want to allow that here
                    ShowTransitionError(new PasteError
                    {
                        Column = indexNeutralLoss,
                        Line = row.Index,
                        Message = BioMassCalc.FormatArgumentExceptionMessage(neutralLoss)
                    });
                    return false;
                }
                // Calculate the resulting fragment as precursor-loss, checking to see that we're not losing atoms that aren't there in the first place
                var fragmentMolecule = precursorMolecule.Difference(lossMolecule);
                if (fragmentMolecule.Molecule.Values.Any(v => v < 0))
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = indexNeutralLoss,
                        Line = row.Index,
                        Message = string.Format(
                            Resources.SmallMoleculeTransitionListReader_ProcessNeutralLoss_Precursor_molecular_formula__0__does_not_contain_sufficient_atoms_to_be_used_with_neutral_loss__1_,
                            precursorFormula, neutralLoss)
                    });
                    return false;
                }

                formula = fragmentMolecule.ToString();
            }

            return true; // Success
        }

        private bool ProcessAdduct(Row row, int indexAdduct, int indexFormula, bool getPrecursorColumns,
            ref string formula, ref Adduct adduct, ref int? charge)
        {
            if (indexAdduct < 0 && string.IsNullOrEmpty(formula))
            {
                return true; // No parsing to be done (row probably has only a "charge" column) but that's not an error
            }

            // Do we have an adduct description?  If so, pull charge from that.
            var adductText = GetCellTrimmed(row, indexAdduct);

            // If formula also contains an adduct description, use that. If there's also an adduct declared they must agree.
            formula = Adduct.SplitFormulaAndTrailingAdduct(formula, Adduct.ADDUCT_TYPE.charge_only, out var formulaAdduct); // Adduct may be declared in formula e.g "C12H5[M+H]"
            if (string.IsNullOrEmpty(adductText) && !formulaAdduct.IsEmpty)
            {
                adductText = formulaAdduct.AdductFormula;
            }

            if (string.IsNullOrEmpty(adductText) && charge.HasValue)
            {
                return true; // No further work to do here - caller will have to work out meaning of charge without adduct description
            }

            try
            {
                adduct = Adduct.FromStringAssumeChargeOnly(adductText);
                IonInfo.ApplyAdductToFormula(formula ?? string.Empty, adduct); // Just to see if it throws
            }
            catch (Exception x) when (IsParserException(x))
            {
                ShowTransitionError(new PasteError
                {
                    Column = indexAdduct,
                    Line = row.Index,
                    Message = x.Message
                });
                return false;
            }
            if (!formulaAdduct.IsEmpty && !Equals(adduct, formulaAdduct))
            {
                ShowTransitionError(new PasteError
                {
                    Column = indexAdduct,
                    Line = row.Index,
                    Message = Resources
                        .SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Formula_already_contains_an_adduct_description__and_it_does_not_match_
                });
                return false;
            }

            if (charge.HasValue && charge.Value != adduct.AdductCharge)
            {
                // Explicit charge disagrees with adduct - is this because adduct charge is not recognized?
                if (adduct.AdductCharge == 0)
                {
                    // Update the adduct to contain the explicit charge
                    adduct = adduct.ChangeCharge(charge.Value);
                }
                else
                {
                    ShowTransitionError(new PasteError
                    {
                        Column = indexAdduct >= 0 ? indexAdduct : indexFormula,
                        Line = row.Index,
                        Message = string.Format(
                            Resources
                                .SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Adduct__0__charge__1__does_not_agree_with_declared_charge__2_,
                            adductText, adduct.AdductCharge, charge.Value)
                    });
                    return false;
                }
            }
            else
            {
                charge = adduct.AdductCharge;
            }

            if (!ValidateCharge(charge, getPrecursorColumns, out var errMessage))
            {
                ShowTransitionError(new PasteError
                {
                    Column = indexAdduct >= 0 ? indexAdduct : indexFormula,
                    Line = row.Index,
                    Message = errMessage
                });
                return false;
            }

            return true; // Success
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
                var adductM = Adduct.FromChargeNoMass(charge); // [M-] tc
                var ionH = adductH.ApplyToFormula(formula);
                var ionM = adductM.ApplyToFormula(formula);
                var mass = mz * Math.Abs(charge);
                adduct = Math.Abs(BioMassCalc.MONOISOTOPIC.CalculateMass(ionH) - mass) <
                         Math.Abs(BioMassCalc.MONOISOTOPIC.CalculateMass(ionM) - mass)
                    ? adductH
                    : adductM;
            }

            return adduct;
        }

        private PeptideGroupDocNode GetMoleculePeptideGroup(SrmDocument document, Row row)
        {
            var pepGroup = new PeptideGroup();
            var pep = GetMoleculePeptide(document, row, pepGroup);
            if (pep == null)
                return null;
            var moleculeListNote =  GetCellTrimmed(row, INDEX_MOLECULE_LIST_NOTE);

            var annotations = string.IsNullOrEmpty(moleculeListNote)
                ? Annotations.EMPTY
                : new Annotations(moleculeListNote, null, 0);

            var name = GetCellTrimmed(row, INDEX_MOLECULE_GROUP);
            if (String.IsNullOrEmpty(name))
                name = document.GetSmallMoleculeGroupId();
            var metadata = new ProteinMetadata(name, String.Empty).SetWebSearchCompleted();  // FUTURE: some kind of lookup for small molecules
            return new PeptideGroupDocNode(pepGroup, annotations, metadata, new[] { pep }, false);
        }

        private PeptideDocNode GetMoleculePeptide(SrmDocument document, Row row, PeptideGroup group)
        {

            CustomMolecule molecule;
            ParsedIonInfo parsedIonInfo;
            bool hasInconsistentPrecursorIonColumns = false;
            try
            {
                parsedIonInfo = ReadPrecursorOrProductColumns(document, row, null, ref hasInconsistentPrecursorIonColumns); // Re-read the precursor columns
                if (parsedIonInfo == null)
                    return null; // Some failure, but exception was already handled
                // Identify items with same formula and different adducts
                var neutralFormula = parsedIonInfo.NeutralFormula;
                var shortName = parsedIonInfo.MoleculeID.Name;
                if (!ParsedMolecule.IsNullOrEmpty(neutralFormula))
                {
                    molecule = new CustomMolecule(neutralFormula, shortName, parsedIonInfo.MoleculeID.AccessionNumbers);
                }
                else
                {
                    molecule = new CustomMolecule(parsedIonInfo.MonoMass, parsedIonInfo.AverageMass, shortName, parsedIonInfo.MoleculeID.AccessionNumbers);
                }
            }
            catch (Exception e) when (IsParserException(e))
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
                var tranGroup = GetMoleculeTransitionGroup(document, parsedIonInfo, row, pep);
                if (tranGroup == null)
                    return null;

                var annotations = string.IsNullOrEmpty(parsedIonInfo.MoleculeNote)
                    ? Annotations.EMPTY
                    : new Annotations(parsedIonInfo.MoleculeNote, null, 0);

                return new PeptideDocNode(pep, document.Settings, null, null, parsedIonInfo.ExplicitRetentionTime, new[] { tranGroup }, annotations,false);
            }
            catch (Exception e) when (IsParserException(e))
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

        private TransitionGroupDocNode GetMoleculeTransitionGroup(SrmDocument document, ParsedIonInfo moleculeInfo, Row row, Peptide pep)
        {
            if (!document.Settings.TransitionSettings.IsMeasurablePrecursor(moleculeInfo.Mz))
            {
                ShowTransitionError(new PasteError
                {
                    Column = INDEX_PRECURSOR_MZ,
                    Line = row.Index,
                    Message = String.Format(ModelResources.PasteDlg_GetMoleculeTransitionGroup_The_precursor_m_z__0__is_not_measureable_with_your_current_instrument_settings_, moleculeInfo.Mz)
                });
                return null;
            }

            var customIon = moleculeInfo.ToCustomMolecule();
            var isotopeLabelType = moleculeInfo.IsotopeLabelType ?? IsotopeLabelType.light;
            if (!Equals(pep.CustomMolecule.PrimaryEquivalenceKey, customIon.PrimaryEquivalenceKey))
            {
                ShowTransitionError(new PasteError
                {
                    Column = -1, 
                    Line = row.Index,
                    Message = Resources.SmallMoleculeTransitionListReader_GetMoleculeTransitionGroup_Inconsistent_molecule_description
                });
                return null;
            }
            var adduct = moleculeInfo.Adduct;
            if (!Equals(pep.CustomMolecule.MonoisotopicMass, customIon.MonoisotopicMass) && !adduct.HasIsotopeLabels)
            {
                // Some kind of undescribed isotope labeling going on
                // No formula for label, describe as mass
                var labelMass = customIon.MonoisotopicMass - pep.CustomMolecule.MonoisotopicMass;
                if (labelMass > 0)
                {
                    adduct = adduct.ChangeIsotopeLabels(labelMass); // Isostopes add weight
                    isotopeLabelType = moleculeInfo.IsotopeLabelType ?? IsotopeLabelType.heavy;
                }
            }

            var annotations = string.IsNullOrEmpty(moleculeInfo.PrecursorNote)
                ? Annotations.EMPTY
                : new Annotations(moleculeInfo.PrecursorNote, null, 0);

            var group = new TransitionGroup(pep, adduct, isotopeLabelType);
            string errmsg;
            try
            {
                var tran = GetMoleculeTransition(document, row, pep, group, moleculeInfo.ExplicitTransitionGroupValues);
                if (tran == null)
                    return null;
                return new TransitionGroupDocNode(group, annotations, document.Settings, null,
                    null, moleculeInfo.ExplicitTransitionGroupValues, null, new[] { tran }, false);
            }
            catch (Exception x) when (IsParserException(x))
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

        private bool FragmentColumnsIdenticalToPrecursorColumns(ParsedIonInfo precursor, ParsedIonInfo fragment)
        {
            // Adducts must me non-empty, and match
            if (Adduct.IsNullOrEmpty(precursor.Adduct) || !Equals(precursor.Adduct, fragment.Adduct))
            {
                return false;
            }
            // Formulas and/or masses must be non-empty, and match
            return !((ParsedMolecule.IsNullOrEmpty(precursor.Formula) || !Equals(precursor.Formula, fragment.Formula)) &&
                     !Equals(precursor.MonoMass, fragment.MonoMass));
        }

        private TransitionDocNode GetMoleculeTransition(SrmDocument document, Row row, Peptide pep, TransitionGroup group, ExplicitTransitionGroupValues explicitTransitionGroupValues)
        {
            bool hasInconsistentPrecursorIonColumns = false;
            var precursorIon = ReadPrecursorOrProductColumns(document, row, null, ref hasInconsistentPrecursorIonColumns); // Re-read the precursor columns
            if (precursorIon == null)
            {
                return null;
            }

            bool hasInconsistentProductIonColumns = false;
            var ion = _requireProductInfo ? ReadPrecursorOrProductColumns(document, row, precursorIon, ref hasInconsistentProductIonColumns) : precursorIon; // Re-read the product columns, or copy precursor
            if (ion == null)
            {
                return null;
            }
            var customMolecule = ion.ToCustomMolecule();
            var ionType = !_requireProductInfo || // We inspected the input list and found only precursor info
                          FragmentColumnsIdenticalToPrecursorColumns(precursorIon, ion) ||
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

            var adduct = ionType == IonType.precursor ? group.PrecursorAdduct : ion.Adduct;
            var transition = new Transition(group, adduct, null, customMolecule, ionType);

            var annotations = string.IsNullOrEmpty(ion.TransitionNote)
                ? Annotations.EMPTY
                : new Annotations(ion.TransitionNote, null, 0);

            var ionExplicitTransitionValues = ion.ExplicitTransitionValues;
            if (explicitTransitionGroupValues?.CollisionEnergy == ion.ExplicitTransitionValues?.CollisionEnergy)
            {
                // No need for per-transition CE override if it matches precursor CE override
                ionExplicitTransitionValues = ionExplicitTransitionValues.ChangeCollisionEnergy(null);
            }

            if (ion.LibraryIntensity.HasValue) // In case we are reading an assay library
            {
                var pair = new Tuple<Peptide, TransitionGroup, Transition>(pep, group, transition);
                if (LibraryIntensities.TryGetValue(pair, out var list)) // In case of conflict, take first seen
                {
                    list.Add(ion.LibraryIntensity.Value);
                }
                else
                {
                    LibraryIntensities.Add(pair, new List<double>(){ion.LibraryIntensity.Value});
                }
            }

            if (ion.IRT.HasValue)  // In case we are reading an assay library
            {
                // Note we keep a list of all unique IRTs for each molecule+label, and take the median value.
                if (!IRTs.TryGetValue(pep, out var iRTs))
                {
                    IRTs.Add(pep, new HashSet<double>() {ion.IRT.Value});
                }
                else
                {
                    iRTs.Add(ion.IRT.Value);
                }
            }

            var transitionQuantInfo = TransitionDocNode.TransitionQuantInfo.DEFAULT;
            if (ionType == IonType.precursor && customMolecule.ParsedMolecule.HasChemicalFormula)
            {
                var fullScan = document.Settings.TransitionSettings.FullScan;
                if (fullScan.IsHighResPrecursor)
                {
                    var calc = document.Settings.GetPrecursorCalc(IsotopeLabelType.light, null);
                    var massDist = calc.GetMZDistribution(customMolecule.ParsedMolecule.GetMoleculeMassOffset(), adduct,
                        fullScan.IsotopeAbundances);
                    var isotopeDistInfo = IsotopeDistInfo.MakeIsotopeDistInfo(massDist, mass, adduct, fullScan);
                    var transitionIsotopeDistInfo = new TransitionIsotopeDistInfo(isotopeDistInfo.GetRankI(0),
                        isotopeDistInfo.GetProportionI(0));
                    transitionQuantInfo = transitionQuantInfo.ChangeIsotopeDistInfo(transitionIsotopeDistInfo);
                }
            }

            return new TransitionDocNode(transition, annotations, null, mass, transitionQuantInfo, ionExplicitTransitionValues, null);
        }
    }

    public class SmallMoleculeTransitionListCSVReader : SmallMoleculeTransitionListReader
    {
        private readonly DsvFileReader _csvReader;

        public SmallMoleculeTransitionListCSVReader(IList<string> csvText, List<string> columnPositions = null, bool hasHeaders = true)
        {
            // Ask MassListInputs to figure out the column and decimal separators
            var inputs = new MassListInputs(csvText);
            _cultureInfo = inputs.FormatProvider;
            HasHeaders = hasHeaders;
            // CONSIDER: It is technically not necessary to dispose this DsvFileReader, because it uses a StringReader
            //           Though, it would not be much work to implement the IDisposable pattern on this class
            //           Or, the code could be adapted to save the headers instead of the reader.
            _csvReader = new DsvFileReader(new StringListReader(csvText), inputs.Separator, SmallMoleculeTransitionListColumnHeaders.KnownHeaderSynonyms, columnPositions, hasHeaders);
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

        public static bool IsPlausibleSmallMoleculeTransitionList(string csvText, SrmSettings settings, SrmDocument.DOCUMENT_TYPE defaultDocumentType = SrmDocument.DOCUMENT_TYPE.none)
        {
            return IsPlausibleSmallMoleculeTransitionList(MassListInputs.ReadLinesFromText(csvText), settings, defaultDocumentType);
        }

        public static bool IsPlausibleSmallMoleculeTransitionList(IList<string> csvText, SrmSettings settings, SrmDocument.DOCUMENT_TYPE defaultDocumentType = SrmDocument.DOCUMENT_TYPE.none)
        {
            // If it cannot be formatted as a mass list it cannot be a small molecule transition list
            var testLineCount = 100;
            var testText = TextUtil.LineSeparate(csvText.Take(testLineCount));
            if (!MassListInputs.TryInitFormat(testText, out var provider, out var sep))
            {
                return false;
            }

            // Use the first 100 lines and the document to create an importer
            var inputs = new MassListInputs(testText, provider, sep);
            var importer = new MassListImporter(settings, inputs, false);
            // See if creating a peptide row reader with the first 100 lines is possible
            if (importer.TryCreateRowReader(null, csvText.Take(testLineCount).ToList(), null, out _, out _))
            {
                // If the row reader is able to find a peptide column then it must be a protein transition list
                return false;
            }
            // If we cannot find the peptide column, then try reading it as a small molecule list
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
                // Check the first line for peptide header
                var header = csvText.First();
                // CONSIDER (henrys): Look for "peptide" in other languages as well
                if (header.ToLowerInvariant().Contains(@"peptide"))
                {
                    return false;
                }
                
                // Look for distinctive small molecule headers
                if (new[]
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
                    SmallMoleculeTransitionListColumnHeaders.idKEGG,
                }.Count(hint => SmallMoleculeTransitionListColumnHeaders.KnownHeaderSynonyms.Where(
                    p => string.Compare(p.Value, hint, StringComparison.OrdinalIgnoreCase) == 0).Any(kvp =>
                    header.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)) > 1)
                {
                    return true;
                }

                // If we still have not discerned the transition list type then decide based on the UI mode
                return defaultDocumentType == SrmDocument.DOCUMENT_TYPE.small_molecules;
            }
        }

        public override void UpdateCellBackingStore(int row, int col, object value)
        {
            // We don't have a backing store, unlike the dialog implementaion with its gridview 
        }

        public override void ShowTransitionError(PasteError error)
        {
            ErrorList.Add(error);
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
        public const string neutralLossProduct = "ProductNeutralLoss";
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
        public const string imPrecursor_invK0 = "PrecursorInvK0"; // Ion mobility with implied units
        public const string imHighEnergyOffset = "HighEnergyIMOffset";
        public const string imUnits = "IMUnits";
        public const string ccsPrecursor = "PrecursorCCS";
        public const string slens = "SLens";
        public const string coneVoltage = "ConeVoltage";
        public const string compensationVoltage = "CompensationVoltage";
        public const string declusteringPotential = "DeclusteringPotential";
        public const string transitionNote = "Note"; // "Note" is legacy name for "Transition Note", we didn't handle other levels before
        public const string precursorNote = "PrecursorNote";
        public const string moleculeNote = "MoleculeNote";
        public const string moleculeListNote = "MoleculeListNote";
        public const string labelType = "LabelType";
        public const string adductPrecursor = "PrecursorAdduct";
        public const string adductProduct = "ProductAdduct";
        public const string idCAS = "CAS";
        public const string idInChiKey = "InChiKey";
        public const string idInChi = "InChi";
        public const string idHMDB = "HMDB";
        public const string idSMILES = "SMILES";
        public const string idKEGG = "KEGG";
        public const string ignoreColumn = "IgnoreColumn"; // We want to be able to recognize these columns to avoid throwing an error and then we ignore them

        public const string iRT = "IRT"; // For assay library use
        public const string libraryIntensity = "LibraryIntensity"; // For assay library use

        public static readonly List<string> KnownHeaders;

        public static IReadOnlyDictionary<string, string> KnownHeaderSynonyms;

        static SmallMoleculeTransitionListColumnHeaders()
        {
            // The list of internal values, as used in serialization
            KnownHeaders = new List<string>(new[]
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
                transitionNote,
                precursorNote,
                moleculeNote,
                moleculeListNote,
                labelType,
                idInChiKey,
                idCAS,
                idHMDB,
                idInChi,
                idSMILES,
                idKEGG,
                neutralLossProduct,
                ignoreColumn, // Does not contain useful data, can be more than one in a list
                imPrecursor_invK0, // Ion mobility with implied units 1/K0
            });

            // A dictionary of terms that can be understood as column headers - this includes
            // the internal names, and the names presented in the UI (for all supported cultures)
            var currentCulture = Thread.CurrentThread.CurrentCulture;
            var currentUICulture = Thread.CurrentThread.CurrentUICulture;
            var knownColumnHeadersAllCultures = KnownHeaders.ToDictionary( hdr => hdr, hdr => hdr);

            void AddSynonym(string stdHeader, string synonym)
            {
                // Also try variants - Foo Bar, Foo_Bar, FooBar
                var noSpace = synonym.Replace(@" ", string.Empty); // Foo Bar => FooBar
                var noUnderbar = synonym.Replace(@"_", string.Empty); // Foo_Bar => FooBar
                var noCamelCase1 = string.Concat(synonym.Select((c, i) => i > 0 && char.IsUpper(c) ? @"_" + c.ToString() : c.ToString())); // FooBar => Foo_Bar
                var noCamelCase2 = string.Concat(synonym.Select((c, i) => i > 0 && char.IsUpper(c) ? @" " + c.ToString() : c.ToString())); // FooBar => Foo Bar
                var underbarForSpace = noCamelCase2.Replace(@" ", @"_"); // Foo Bar => Foo_Bar
                var spaceForUnderbar = noCamelCase1.Replace(@"_", @" "); // Foo_Bar => Foo Bar
                foreach (var syn in new[]{synonym, noSpace, noUnderbar, noCamelCase1, noCamelCase2, underbarForSpace, spaceForUnderbar})
                {
                    if (!knownColumnHeadersAllCultures.ContainsKey(syn))
                    {
                        knownColumnHeadersAllCultures.Add(syn, stdHeader);
                    }
                }

                // "Fragment" is a common synonym for "Product"
                if (synonym.IndexOf(@"product", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    AddSynonym(stdHeader, synonym.ToLowerInvariant().Replace(@"product", @"fragment"));
                }
            }

            foreach (var culture in CultureUtil.AvailableDisplayLanguages())
            {
                Thread.CurrentThread.CurrentUICulture =
                    Thread.CurrentThread.CurrentCulture = culture;
                foreach (var pair in new[] {
                    // ReSharper disable StringLiteralTypo
                    Tuple.Create(moleculeGroup, ModelResources.PasteDlg_UpdateMoleculeType_Molecule_List_Name),
                    Tuple.Create(moleculeGroup, Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name),
                    Tuple.Create(namePrecursor, Resources.PasteDlg_UpdateMoleculeType_Precursor_Name),
                    Tuple.Create(namePrecursor, Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name),
                    Tuple.Create(namePrecursor, ModelResources.SmallMoleculeTransitionListColumnHeaders_SmallMoleculeTransitionListColumnHeaders_Molecule),
                    Tuple.Create(namePrecursor, ModelResources.SmallMoleculeTransitionListColumnHeaders_SmallMoleculeTransitionListColumnHeaders_Compound),
                    Tuple.Create(nameProduct, Resources.PasteDlg_UpdateMoleculeType_Product_Name),
                    Tuple.Create(formulaPrecursor, ModelResources.PasteDlg_UpdateMoleculeType_Precursor_Formula),
                    Tuple.Create(formulaPrecursor, Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula),
                    Tuple.Create(formulaPrecursor,ModelResources.SmallMoleculeTransitionListColumnHeaders_SmallMoleculeTransitionListColumnHeaders_Chemical_Formula),
                    Tuple.Create(formulaPrecursor, ColumnCaptions.MoleculeFormula),
                    Tuple.Create(formulaProduct, Resources.PasteDlg_UpdateMoleculeType_Product_Formula),
                    Tuple.Create(mzPrecursor, ModelResources.PasteDlg_UpdateMoleculeType_Precursor_m_z),
                    Tuple.Create(mzPrecursor, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z),
                    Tuple.Create(mzProduct, ModelResources.PasteDlg_UpdateMoleculeType_Product_m_z),
                    Tuple.Create(mzProduct, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z),
                    Tuple.Create(chargePrecursor, ModelResources.PasteDlg_UpdateMoleculeType_Precursor_Charge),
                    Tuple.Create(chargePrecursor, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge),
                    Tuple.Create(chargeProduct, Resources.PasteDlg_UpdateMoleculeType_Product_Charge),
                    Tuple.Create(adductPrecursor, Resources.PasteDlg_UpdateMoleculeType_Precursor_Adduct),
                    Tuple.Create(adductProduct, Resources.PasteDlg_UpdateMoleculeType_Product_Adduct),
                    Tuple.Create(rtPrecursor, Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time),
                    Tuple.Create(rtPrecursor, Resources.PasteDlg_UpdateMoleculeType_Retention_Time),
                    Tuple.Create(rtPrecursor, ModelResources.SmallMoleculeTransitionListColumnHeaders_SmallMoleculeTransitionListColumnHeaders_RT__min_), // ""RT (min)"
                    Tuple.Create(rtPrecursor, @"explicitretentiontime"),
                    Tuple.Create(rtPrecursor, @"precursorrt"),
                    Tuple.Create(rtWindowPrecursor, Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time_Window),
                    Tuple.Create(rtWindowPrecursor, Resources.PasteDlg_UpdateMoleculeType_Retention_Time_Window),
                    Tuple.Create(rtWindowPrecursor, @"explicitretentiontimewindow"),
                    Tuple.Create(rtWindowPrecursor, @"precursorrtwindow"),
                    Tuple.Create(cePrecursor, @"ce"),
                    Tuple.Create(cePrecursor, Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Energy),
                    Tuple.Create(cePrecursor, ModelResources.PasteDlg_UpdateMoleculeType_Collision_Energy),
                    Tuple.Create(dtPrecursor, ModelResources.PasteDlg_UpdateMoleculeType_Explicit_Drift_Time__msec_),
                    Tuple.Create(dtHighEnergyOffset, ModelResources.PasteDlg_UpdateMoleculeType_Explicit_Drift_Time_High_Energy_Offset__msec_),
                    Tuple.Create(imPrecursor, Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility),
                    Tuple.Create(imPrecursor, Resources.PasteDlg_UpdateMoleculeType_Ion_Mobility),
                    Tuple.Create(imPrecursor, @"explicitionmobility"),
                    Tuple.Create(imPrecursor, @"ionmobility"),
                    Tuple.Create(imHighEnergyOffset, Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility_High_Energy_Offset),
                    Tuple.Create(imHighEnergyOffset, Resources.PasteDlg_UpdateMoleculeType_Ion_Mobility_High_Energy_Offset),
                    Tuple.Create(imHighEnergyOffset, @"explicitionmobilityhighenergyoffset"),
                    Tuple.Create(imHighEnergyOffset, @"ionmobilityhighenergyoffset"),
                    Tuple.Create(imUnits, Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility_Units),
                    Tuple.Create(imUnits, Resources.PasteDlg_UpdateMoleculeType_Ion_Mobility_Units),
                    Tuple.Create(imUnits, @"explicitionmobilityunits"),
                    Tuple.Create(imUnits, @"ionmobilityunits"),
                    Tuple.Create(dtPrecursor, Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility__msec_),
                    Tuple.Create(dtPrecursor, Resources.PasteDlg_UpdateMoleculeType_Ion_Mobility__msec_),
                    Tuple.Create(imPrecursor_invK0, Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility__1_K0_),
                    Tuple.Create(imPrecursor_invK0, Resources.PasteDlg_UpdateMoleculeType_Ion_Mobility__1_K0_),
                    Tuple.Create(imPrecursor_invK0, @"1/K0"),
                    Tuple.Create(imPrecursor_invK0, @"PrecursorIonMobility"), // As in Bruker PASER files
                    Tuple.Create(compensationVoltage, Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility + @" (CoV)"),
                    Tuple.Create(compensationVoltage, Resources.PasteDlg_UpdateMoleculeType_Ion_Mobility + @" (CoV)"),
                    Tuple.Create(compensationVoltage, @"CoV"),
                    Tuple.Create(ccsPrecursor, Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Cross_Section__sq_A_),
                    Tuple.Create(ccsPrecursor, Resources.PasteDlg_UpdateMoleculeType_Collision_Cross_Section__sq_A_),
                    Tuple.Create(ccsPrecursor, ModelResources.PasteDlg_UpdateMoleculeType_Collisional_Cross_Section__sq_A_),
                    Tuple.Create(ccsPrecursor, @"Collisional Cross Section"),
                    Tuple.Create(ccsPrecursor, @"Collision Cross Section"),
                    Tuple.Create(ccsPrecursor, @"CCS"),
                    Tuple.Create(ccsPrecursor, @"collisionalcrosssection"),
                    Tuple.Create(ccsPrecursor, @"collisionalcrosssection(sqa)"),
                    Tuple.Create(ccsPrecursor, @"collisionalcrosssectionsqa"),
                    Tuple.Create(slens, Resources.PasteDlg_UpdateMoleculeType_S_Lens),
                    Tuple.Create(slens, @"slens"),
                    Tuple.Create(slens, @"s-lens"),
                    Tuple.Create(coneVoltage, Resources.PasteDlg_UpdateMoleculeType_Cone_Voltage),
                    Tuple.Create(compensationVoltage, Resources.PasteDlg_UpdateMoleculeType_Explicit_Compensation_Voltage),
                    Tuple.Create(compensationVoltage, Resources.PasteDlg_UpdateMoleculeType_Compensation_Voltage),
                    Tuple.Create(declusteringPotential, ModelResources.PasteDlg_UpdateMoleculeType_Explicit_Declustering_Potential),
                    Tuple.Create(declusteringPotential, Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Explicit_Declustering_Potential),
                    Tuple.Create(declusteringPotential, ModelResources.ImportTransitionListColumnSelectDlg_ComboChanged_Declustering_Potential),
                    Tuple.Create(transitionNote, Resources.PasteDlg_UpdateMoleculeType_Note), // "Note" is legacy name for transition note
                    Tuple.Create(transitionNote, ColumnCaptions.TransitionNote),
                    Tuple.Create(precursorNote, Resources.PasteDlg_UpdateMoleculeType_PrecursorNote),
                    Tuple.Create(precursorNote, ColumnCaptions.PrecursorNote),
                    Tuple.Create(moleculeNote, Resources.PasteDlg_UpdateMoleculeType_MoleculeNote),
                    Tuple.Create(moleculeNote, ColumnCaptions.MoleculeNote),
                    Tuple.Create(moleculeListNote, Resources.PasteDlg_UpdateMoleculeType_MoleculeListNote),
                    Tuple.Create(moleculeListNote, ColumnCaptions.MoleculeListNote),
                    Tuple.Create(labelType, Resources.PasteDlg_UpdateMoleculeType_Label_Type),
                    Tuple.Create(labelType, ModelResources.SmallMoleculeTransitionListColumnHeaders_SmallMoleculeTransitionListColumnHeaders_Label),
                    Tuple.Create(idInChiKey, idInChiKey),
                    Tuple.Create(idCAS, idCAS),
                    Tuple.Create(idHMDB, idHMDB),
                    Tuple.Create(idInChi, idInChi),
                    Tuple.Create(idSMILES, idSMILES),
                    Tuple.Create(idKEGG, idKEGG),
                    Tuple.Create(neutralLossProduct, Resources.PasteDlg_UpdateMoleculeType_Product_Neutral_Loss),
                    Tuple.Create(libraryIntensity, ModelResources.PasteDlg_UpdateMoleculeType_Library_Intensity),
                    Tuple.Create(ignoreColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column),
                    // ReSharper restore StringLiteralTypo
                })
                {
                    AddSynonym(pair.Item1, pair.Item2);

                    AddSynonym(pair.Item1, pair.Item2.Replace(@"m/z", @"mz")); // Accept either m/z or mz

                    // Be willing to match "Ion Mobility" as well as "Explicit Ion Mobility"
                    var strExplicit = Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility.Replace(Resources.PeptideTipProvider_RenderTip_Ion_Mobility, string.Empty);
                    if (pair.Item2.Contains(strExplicit))
                    {
                        AddSynonym(pair.Item1, pair.Item2.Replace(strExplicit, String.Empty));
                    }

                    // Some values in common with peptide-oriented lists
                    foreach (var n in ColumnIndices.FragmentNameNames)
                    {
                        AddSynonym(nameProduct, n);
                    }
                    foreach (var n in ColumnIndices.IrtColumnNames)
                    {
                        AddSynonym(iRT, n);
                    }
                    foreach (var n in ColumnIndices.LibraryColumnNames)
                    {
                        AddSynonym(libraryIntensity, n);
                    }
                }
            }
            Thread.CurrentThread.CurrentCulture = currentCulture;
            Thread.CurrentThread.CurrentUICulture = currentUICulture;
            KnownHeaderSynonyms = knownColumnHeadersAllCultures;
        }
    }

    public class PasteError
    {
        public String Message { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public int Length { get; set; }
    }
}
