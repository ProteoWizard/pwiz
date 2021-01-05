/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls
{

    /// <summary>
    /// Handles updating small molecule detail columns when target is set
    /// </summary>
    public class SmallMoleculeColumnsManager
    {
        public const string DATA_PROPERTY_NAME_FORMULA = nameof(DbAbstractPeptide.Formula);
        public const string DATA_PROPERTY_NAME_MONOISOTOPICMASS = nameof(DbAbstractPeptide.MonoisotopicMass);
        public const string DATA_PROPERTY_NAME_AVERAGEMASS = nameof(DbAbstractPeptide.AverageMass);

        /// <summary>
        /// Examines all targets in the targetResolver's document to see what kind of detail columns are needed
        /// </summary>
        /// <param name="gridView">The data grid to be given additional small molecule detail columns</param>
        /// <param name="targetResolver">Used as a source of targets to examine to see which details (KEGG, InChI etc) are needed. If none, assume all details are needed.</param>
        /// <param name="modeUI">Current UI display type, helps decide what to do when no targets are provided</param>
        /// <param name="readOnly">When true, small molecule detail columns are not editable</param>
        /// <param name="insertColumnsAt">Where to insert the new columns. Default is to the right of existing columns.</param>
        public SmallMoleculeColumnsManager(DataGridView gridView, TargetResolver targetResolver, SrmDocument.DOCUMENT_TYPE modeUI, bool readOnly, int insertColumnsAt = -1)
        {
            DataGridView = gridView;
            for (var targetColumnIndex = 0; targetColumnIndex < gridView.ColumnCount; targetColumnIndex++)
            {
                if (gridView.Columns[targetColumnIndex] is TargetColumn)
                {
                    TargetColumnIndex = targetColumnIndex;
                    break;
                }
            }
            TargetResolver = targetResolver;
            ModeUI = modeUI;
            InsertColumnsAt = insertColumnsAt;
            ReadOnly = readOnly;
            if (InsertColumnsAt == -1)
            {
                InsertColumnsAt = gridView.Columns.Count; // Just add detail columns to the right of everything else
            }
            var smallMoleculeLibraryAttributes = new List<KeyValuePair<Target, SmallMoleculeLibraryAttributes>>();

            var targets = targetResolver.AvailableTargets;

            if (targets != null)
            {
                smallMoleculeLibraryAttributes = targets.Where(t => !t.IsProteomic)
                    .Select(t => new KeyValuePair<Target, SmallMoleculeLibraryAttributes>(t, t.Molecule.GetSmallMoleculeLibraryAttributes())).ToList();
            }
            if (!smallMoleculeLibraryAttributes.Any() && modeUI != SrmDocument.DOCUMENT_TYPE.proteomic && !ReadOnly)
            {
                // No targets to inspect, assume we want all the detail columns so we can paste in a list
                var dummy = @"dummy";
                var otherKeys = MoleculeAccessionNumbers.PREFERRED_DISPLAY_ORDER.ToDictionary(tag => tag, tag => dummy);
                smallMoleculeLibraryAttributes.Add(new KeyValuePair<Target, SmallMoleculeLibraryAttributes>(null, 
                    SmallMoleculeLibraryAttributes.Create(dummy, @"CH", dummy, otherKeys)));
            }

            if (!smallMoleculeLibraryAttributes.Any())
            {
                return;
            }

            var formulaHeader = Resources.SmallMoleculeLibraryAttributes_KeyValuePairs_Formula;
            var hasFormulaColumn = gridView.Columns.Contains(formulaHeader);
            var addedHeaders = new List<string>();
            var targetColumn = gridView.Columns[TargetColumnIndex] as TargetColumn;

            if (!hasFormulaColumn && smallMoleculeLibraryAttributes.Any(a => !string.IsNullOrEmpty(a.Value.ChemicalFormula)))
            {
                // Add a column for chemical formula
                gridView.Columns.Insert(InsertColumnsAt, new TargetDetailColumn(targetColumn));
                FormulaColumnIndex = InsertColumnsAt;
                var dataGridViewColumn = gridView.Columns[InsertColumnsAt++];
                dataGridViewColumn.Name = dataGridViewColumn.HeaderText = formulaHeader;
                dataGridViewColumn.DataPropertyName = DATA_PROPERTY_NAME_FORMULA;
                dataGridViewColumn.Visible = true;
                dataGridViewColumn.ReadOnly = readOnly;
                addedHeaders.Add(dataGridViewColumn.HeaderText);
            }
            else if (hasFormulaColumn)
            {
                // Already has a formula column - note which one
                for (var i = 0; i < gridView.ColumnCount; i++)
                {
                    if (Equals(formulaHeader, gridView.Columns[i].HeaderText))
                    {
                        FormulaColumnIndex = i;
                        break;
                    }
                }
            }

            // Deal with mass-only descriptions
            if (smallMoleculeLibraryAttributes.Any(a => string.IsNullOrEmpty(a.Value.ChemicalFormula)))
            {
                foreach (var massType in new[]{MassType.Monoisotopic, MassType.Average})
                {
                    var header = massType == MassType.Monoisotopic ? 
                        Resources.SmallMoleculeLibraryAttributes_KeyValuePairs_Monoisotopic_mass : 
                        Resources.SmallMoleculeLibraryAttributes_KeyValuePairs_Average_mass;
                    var hasColumn = gridView.Columns.Contains(header);
                    if (!hasColumn)
                    {
                        // Add a column for mass
                        gridView.Columns.Insert(InsertColumnsAt, new TargetDetailColumn(targetColumn));
                        var dataGridViewColumn = gridView.Columns[InsertColumnsAt];
                        var dataGridViewCellStyle = new DataGridViewCellStyle();
                        var format = @"0." + new string('0', CustomMolecule.DEFAULT_ION_MASS_PRECISION); // N.B. the "N" format provides a thousands separator which we don't want
                        dataGridViewCellStyle.Format = format;
                        dataGridViewCellStyle.NullValue = null;
                        dataGridViewColumn.DefaultCellStyle = dataGridViewCellStyle;
                        if (massType == MassType.Monoisotopic)
                        {
                            MonoisotopicMassColumnIndex = InsertColumnsAt++;
                            dataGridViewColumn.DataPropertyName = DATA_PROPERTY_NAME_MONOISOTOPICMASS;
                        }
                        else
                        {
                            AverageMassColumnIndex = InsertColumnsAt++;
                            dataGridViewColumn.DataPropertyName = DATA_PROPERTY_NAME_AVERAGEMASS;
                        }
                        dataGridViewColumn.Name = dataGridViewColumn.HeaderText = header;
                        dataGridViewColumn.Visible = true;
                        dataGridViewColumn.ReadOnly = readOnly;
                        addedHeaders.Add(dataGridViewColumn.HeaderText);
                    }
                    else
                    {
                        // Already has a mass column - note which one
                        for (var i = 0; i < gridView.ColumnCount; i++)
                        {
                            if (Equals(header, gridView.Columns[i].HeaderText))
                            {
                                if (massType == MassType.Monoisotopic)
                                {
                                    MonoisotopicMassColumnIndex = i;
                                }
                                else
                                {
                                    AverageMassColumnIndex = i;
                                }
                                break;
                            }
                        }
                    }
                }
            }

            AccessionColumnIndexes = new Dictionary<string, int>();
            foreach (var tag in MoleculeAccessionNumbers.PREFERRED_DISPLAY_ORDER)
            {
                var hasAccessionColumnForTag = gridView.Columns.Contains(tag);
                var hasValueForAccessionTag = Equals(tag, MoleculeAccessionNumbers.TagInChiKey) ?
                    smallMoleculeLibraryAttributes.Any(a => !string.IsNullOrEmpty(a.Value.InChiKey)) :
                    smallMoleculeLibraryAttributes.Any(a => a.Value.CreateMoleculeID().AccessionNumbers.Keys.Contains(tag));
                if (!hasAccessionColumnForTag && hasValueForAccessionTag)
                {
                    gridView.Columns.Insert(InsertColumnsAt, new TargetDetailColumn(targetColumn));
                    AccessionColumnIndexes.Add(tag, InsertColumnsAt);
                    var dataGridViewColumn = gridView.Columns[InsertColumnsAt++];
                    dataGridViewColumn.Name = dataGridViewColumn.HeaderText = tag;
                    dataGridViewColumn.DataPropertyName = tag;
                    dataGridViewColumn.Visible = true;
                    dataGridViewColumn.ReadOnly = readOnly;
                    addedHeaders.Add(dataGridViewColumn.HeaderText);
                }
                else if (hasAccessionColumnForTag && !AccessionColumnIndexes.TryGetValue(tag, out _))
                {
                    // Already has a column for this accession detail - note which one
                    for (var i = 0; i < gridView.ColumnCount; i++)
                    {
                        if (Equals(tag, gridView.Columns[i].HeaderText))
                        {
                            AccessionColumnIndexes.Add(tag, i);
                            break;
                        }
                    }
                }
            }

            HeadersAdded = addedHeaders.ToArray(); // Useful for adjusting parent control width
        }

        public SmallMoleculeColumnsManager ChangeTargetResolver(TargetResolver targetResolver)
        {
            return new SmallMoleculeColumnsManager(DataGridView, targetResolver, ModeUI, ReadOnly, InsertColumnsAt);
        }

        /// <summary>
        /// List of header names that were automatically added (useful for adjusting parent control width)
        /// </summary>
        public string[] HeadersAdded { get; private set; }

        /// <summary>
        /// Helps deal with hidden columns and their effect on column indexing
        /// </summary>
        public int FormulaColumnShift
        {
            get
            {
                if (!FormulaColumnIndex.HasValue)
                {
                    return 0;
                }

                var nVisibleLeftOfFormula = 0;
                for (var i = 0; i < DataGridView.Columns.Count; i++)
                {
                    if (Equals(DataGridView.Columns[i].HeaderText,
                        Resources.SmallMoleculeLibraryAttributes_KeyValuePairs_Formula))
                    {
                        return FormulaColumnIndex.Value - nVisibleLeftOfFormula;
                    }
                    if (DataGridView.Columns[i].Displayed)
                    {
                        nVisibleLeftOfFormula++;
                    }
                }

                return 0;
            }
        }

        private DataGridView DataGridView { get; }
        public int TargetColumnIndex { get; } // Index of column that shows peptide sequence or molecule name
        private int? FormulaColumnIndex { get; } // Always leftmost of inserted detail columns (when formula is available)
        private int? MonoisotopicMassColumnIndex { get; } // Always leftmost of inserted detail columns (when formula is unavailable)
        private int? AverageMassColumnIndex { get; }  // Always next-to-leftmost of inserted detail columns (when formula is unavailable)
        public TargetResolver TargetResolver { get; private set; }
        private int InsertColumnsAt;
        private bool ReadOnly; // When true, small molecule details are display only (i.e. not for pasting in new lists)
        private SrmDocument.DOCUMENT_TYPE ModeUI;
        private Dictionary<string, int> AccessionColumnIndexes { get; } // Inserted in order of MoleculeAccessionNumbers.PREFERRED_ACCESSION_TYPE_ORDER

        public bool HasSmallMoleculeColumns
        {
            get { return FormulaColumnIndex.HasValue || MonoisotopicMassColumnIndex.HasValue || AccessionColumnIndexes != null; }
        }

        /// <summary>
        /// Populate the molecule detail cells for this target in this row
        /// </summary>
        public void UpdateSmallMoleculeDetails(Target target, int rowIndex)
        {
            if (rowIndex < DataGridView.Rows.Count) // Can only update existing rows
            {
                UpdateSmallMoleculeDetails(target, DataGridView.Rows[rowIndex]);
            }
        }

        public string FormatMass(double mass)
        {
            var massFormat = @"{0:F0" + CustomMolecule.DEFAULT_ION_MASS_PRECISION + @"}";
            return string.Format(massFormat, mass);
        }

        public void UpdateSmallMoleculeDetails(Target target, DataGridViewRow row)
        {
            void Wipe(int? columnIndex)
            {
                if (columnIndex.HasValue && !row.Cells[columnIndex.Value].IsInEditMode)
                {
                    row.Cells[columnIndex.Value].Value = string.Empty;
                }
            }

            void SetSmallMolDetailText(int? columnIndex, string value)
            {
                if (columnIndex.HasValue)
                {
                    row.Cells[columnIndex.Value].Value = value ?? string.Empty;
                }
            }

            if (target == null || target.IsProteomic)
            {
                // Clear out small molecule detail columns, if any
                Wipe(FormulaColumnIndex);
                Wipe(MonoisotopicMassColumnIndex);
                Wipe(AverageMassColumnIndex);

                if (AccessionColumnIndexes != null)
                {
                    foreach (var col in AccessionColumnIndexes.Values)
                    {
                        Wipe(col);
                    }
                }

                return;
            }

            var mol = target.Molecule;
            SetSmallMolDetailText(FormulaColumnIndex, mol.Formula);
            SetSmallMolDetailText(MonoisotopicMassColumnIndex, string.IsNullOrEmpty(mol.Formula) ? FormatMass(mol.MonoisotopicMass) : string.Empty);
            SetSmallMolDetailText(AverageMassColumnIndex, string.IsNullOrEmpty(mol.Formula) ? FormatMass(mol.AverageMass) : string.Empty);

            var accessionNumbers = mol.AccessionNumbers.AccessionNumbers;
            foreach (var col in AccessionColumnIndexes)
            {
                if (accessionNumbers.TryGetValue(col.Key, out var value))
                {
                    SetSmallMolDetailText(col.Value, value);
                }
                else
                {
                    SetSmallMolDetailText(col.Value, string.Empty);
                }
            }

            var newTarget = new Target(mol);
            if (!Equals(newTarget, row.Cells[TargetColumnIndex].Value))
            {
                if (row.Cells[TargetColumnIndex].Value is string)
                {
                    row.Cells[TargetColumnIndex].Value = newTarget.ToSerializableString();
                }
                else
                {
                    row.Cells[TargetColumnIndex].Value = newTarget;
                }
            }
        }

        // Get details from an existing line in the grid
        public Target TryGetSmallMoleculeTargetFromDetails(
            string name, DataGridViewCellCollection cells, int rowIndex, out string errorMessage,
            bool strict = true) // When true, all necessary fields must be present and correct
        {
            var values = Enumerable.Range(0, cells.Count).Select(i => cells[i]).Select(cell => cell.Value?.ToString()).ToArray();
            return TryGetSmallMoleculeTargetFromDetails(name, values, 0, rowIndex, out errorMessage, strict);
        }

        // Get details from a set of values not yet pasted into the grid, taking hidden columns into account 
        public Target TryGetSmallMoleculeTargetFromDetails(string name, IEnumerable<object> values, int rowIndex,
            out string errorMessage, bool strict = true)
        {
            return TryGetSmallMoleculeTargetFromDetails(name, values, FormulaColumnShift, rowIndex, out errorMessage, strict);
        }

        private Target TryGetSmallMoleculeTargetFromDetails(string name, IEnumerable<object> values, int nHiddenColumns, int rowIndex, out string errorMessage,
            bool strict = true) // When true, fail if we don't have minimum info to describe a molecule
        {
            errorMessage = null;
            var strings = values.Select(v => v?.ToString()).ToArray();
            var formula = FormulaColumnIndex.HasValue && strings.Length > FormulaColumnIndex.Value - nHiddenColumns ?  strings[FormulaColumnIndex.Value - nHiddenColumns] : null;
            var accessionNumbers = new Dictionary<string, string>();
            if (AccessionColumnIndexes != null)
            {
                foreach (var pair in AccessionColumnIndexes)
                {
                    if (pair.Value > -1 && strings.Length > pair.Value - nHiddenColumns && !string.IsNullOrEmpty(strings[pair.Value - nHiddenColumns]))
                    {
                        accessionNumbers.Add(pair.Key, strings[pair.Value - nHiddenColumns]);
                    }
                }
            }

            var moleculeAccessionNumbers = new MoleculeAccessionNumbers(accessionNumbers);
            if (!string.IsNullOrEmpty(formula))
            {
                if (strict && string.IsNullOrEmpty(name))
                {
                    errorMessage = string.Format(Resources.SmallMoleculeColumnsManager_TryGetSmallMoleculeTargetFromDetails_Molecule_description_on_line__0__requires_at_least_a_name_and_chemical_formula, rowIndex);
                    return null;
                }
                try
                {
                    return new Target(new CustomMolecule(formula, name, moleculeAccessionNumbers));
                }
                catch (Exception e)
                {
                    if (strict)
                    {
                        errorMessage = string.Format(Resources.SmallMoleculeColumnsManager_TryGetSmallMoleculeTargetFromDetails_Error_in_molecule_description_on_line__0_____1__, rowIndex, e.Message);
                        return null;
                    }
                    return new Target(new IncompleteCustomMolecule(formula, name, moleculeAccessionNumbers));
                }
            }

            double? averageMass = null;
            double? monoisotopicMass = null;
            if (AverageMassColumnIndex.HasValue)
            {
                if (double.TryParse(strings[AverageMassColumnIndex.Value], out var mass))
                {
                    averageMass = mass;
                }
            }
            if (MonoisotopicMassColumnIndex.HasValue)
            {
                if (double.TryParse(strings[MonoisotopicMassColumnIndex.Value], out var mass))
                {
                    monoisotopicMass = mass;
                }
            }

            if (monoisotopicMass.HasValue && averageMass.HasValue)
            {
                try
                {
                    return new Target(new CustomMolecule(
                        new TypedMass(monoisotopicMass.Value, MassType.Monoisotopic),
                        new TypedMass(averageMass.Value, MassType.Average),
                        name, moleculeAccessionNumbers));
                }
                catch(Exception e)
                {
                    if (strict)
                    {
                        errorMessage = string.Format(Resources.SmallMoleculeColumnsManager_TryGetSmallMoleculeTargetFromDetails_Error_in_molecule_description_on_line__0_____1__, rowIndex, e.Message);
                        return null;
                    }
                    return new Target(new IncompleteCustomMolecule(null, monoisotopicMass, averageMass, name, moleculeAccessionNumbers));
                }
            }

            if (accessionNumbers.Any())
            {
                errorMessage = string.Format(Resources.SmallMoleculeColumnsManager_TryGetSmallMoleculeTargetFromDetails_Molecule_description_on_line__0__requires_at_least_a_name_and_chemical_formula, rowIndex);
            }

            if (!strict)
            {
                return new Target(new IncompleteCustomMolecule(formula, monoisotopicMass, averageMass, name, moleculeAccessionNumbers));
            }

            return null;
        }
    }
}