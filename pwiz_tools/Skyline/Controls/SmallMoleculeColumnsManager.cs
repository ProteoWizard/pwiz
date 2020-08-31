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

using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model;
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
        public const string DATA_PROPERTY_NAME_FORMULA = @"Formula"; // This must agree with the member name in DbAbstractPeptide
        public const string DATA_PROPERTY_NAME_MONOISOTOPICMASS = @"MonoisotopicMass"; // This must agree with the member name in DbAbstractPeptide
        public const string DATA_PROPERTY_NAME_AVERAGEMASS = @"AverageMass"; // This must agree with the member name in DbAbstractPeptide

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
            if (!hasFormulaColumn && smallMoleculeLibraryAttributes.Any(a => !string.IsNullOrEmpty(a.Value.ChemicalFormula)))
            {
                // Add a column for chemical formula
                gridView.Columns.Insert(InsertColumnsAt, new DataGridViewTextBoxColumn());
                FormulaColumnIndex = InsertColumnsAt;
                var dataGridViewColumn = gridView.Columns[InsertColumnsAt++];
                dataGridViewColumn.Name = dataGridViewColumn.HeaderText = formulaHeader;
                dataGridViewColumn.DataPropertyName = DATA_PROPERTY_NAME_FORMULA;
                dataGridViewColumn.Visible = true;
                dataGridViewColumn.ReadOnly = readOnly;
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
                        gridView.Columns.Insert(InsertColumnsAt, new DataGridViewTextBoxColumn());
                        var dataGridViewColumn = gridView.Columns[InsertColumnsAt];
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
                    }
                    else if (hasColumn)
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
                    gridView.Columns.Insert(InsertColumnsAt, new DataGridViewTextBoxColumn());
                    AccessionColumnIndexes.Add(tag, InsertColumnsAt);
                    var dataGridViewColumn = gridView.Columns[InsertColumnsAt++];
                    dataGridViewColumn.Name = dataGridViewColumn.HeaderText = tag;
                    dataGridViewColumn.DataPropertyName = tag;
                    dataGridViewColumn.Visible = true;
                    dataGridViewColumn.ReadOnly = readOnly;
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

            // Let system handle width setting for these newly added columns
            DataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells; 
            for (var c = 0; c <  DataGridView.ColumnCount; c++)
            {
                DataGridView.Columns[c].AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells;
            }
        }

        public SmallMoleculeColumnsManager ChangeTargetResolver(TargetResolver targetResolver)
        {
            return new SmallMoleculeColumnsManager(DataGridView, targetResolver, ModeUI, ReadOnly, InsertColumnsAt);
        }

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
        private int? FormulaColumnIndex { get; } // Always leftmost of inserted detail columns (when formula is available)
        private int? MonoisotopicMassColumnIndex { get; } // Always leftmost of inserted detail columns (when formula is unavailable)
        private int? AverageMassColumnIndex { get; }  // Always next-to-leftmost of inserted detail columns (when formula is unavailable)
        public TargetResolver TargetResolver { get; private set; }
        private int InsertColumnsAt;
        private bool ReadOnly; // When true, small molecule details are display only (i.e. not for pasting in new lists)
        private SrmDocument.DOCUMENT_TYPE ModeUI;
        private Dictionary<string, int> AccessionColumnIndexes { get; } // Inserted in order of MoleculeAccessionNumbers.PREFERRED_ACCESSION_TYPE_ORDER

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
        public void UpdateSmallMoleculeDetails(Target target, DataGridViewRow row)
        {
            if (target == null || target.IsProteomic)
            {
                // Clear out small molecule detail columns, if any
                if (FormulaColumnIndex.HasValue)
                {
                    row.Cells[FormulaColumnIndex.Value].Value = string.Empty;
                }

                if (MonoisotopicMassColumnIndex.HasValue)
                {
                    row.Cells[MonoisotopicMassColumnIndex.Value].Value = string.Empty;
                }

                if (AverageMassColumnIndex.HasValue)
                {
                    row.Cells[AverageMassColumnIndex.Value].Value = string.Empty;
                }

                if (AccessionColumnIndexes != null)
                {
                    foreach (var col in AccessionColumnIndexes)
                    {
                        row.Cells[col.Value].Value = string.Empty;
                    }
                }

                return;
            }

            var mol = target.Molecule;
            if (FormulaColumnIndex.HasValue)
            {
                row.Cells[FormulaColumnIndex.Value].Value = mol.Formula ?? string.Empty;
            }

            if (MonoisotopicMassColumnIndex.HasValue)
            {
                row.Cells[MonoisotopicMassColumnIndex.Value].Value = string.IsNullOrEmpty(mol.Formula) ? mol.MonoisotopicMass.ToString() : string.Empty;
            }

            if (AverageMassColumnIndex.HasValue)
            {
                row.Cells[AverageMassColumnIndex.Value].Value = string.IsNullOrEmpty(mol.Formula) ? mol.AverageMass.ToString() : string.Empty;
            }

            var accessionNumbers = mol.AccessionNumbers.AccessionNumbers;
            foreach (var col in AccessionColumnIndexes)
            {
                if (accessionNumbers.TryGetValue(col.Key, out var value))
                {
                    row.Cells[col.Value].Value = value ?? string.Empty;
                }
                else
                {
                    row.Cells[col.Value].Value = string.Empty;
                }
            }
        }

        // Get details from an existing line in the grid
        public Target TryGetSmallMoleculeTargetFromDetails(string name, DataGridViewCellCollection cells, int rowIndex, out string errorMessage)
        {
            var values = Enumerable.Range(0, cells.Count).Select(i => cells[i]).Select(cell => cell.Value?.ToString()).ToArray();
            return TryGetSmallMoleculeTargetFromDetails(name, values, 0, rowIndex, out errorMessage);
        }

        // Get details from a set of values not yet pasted into the grid, taking hidden columns into account 
        public Target TryGetSmallMoleculeTargetFromDetails(string name, IEnumerable<object> values, int rowIndex,
            out string errorMessage)
        {
            return TryGetSmallMoleculeTargetFromDetails(name, values, FormulaColumnShift, rowIndex, out errorMessage);
        }

        private Target TryGetSmallMoleculeTargetFromDetails(string name, IEnumerable<object> values, int nHiddenColumns, int rowIndex, out string errorMessage)
        {
            errorMessage = null;
            var strings = values.Select(v => v.ToString()).ToArray();
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

            if (!string.IsNullOrEmpty(formula))
            {
                if (string.IsNullOrEmpty(name))
                {
                    errorMessage = string.Format(Resources.SmallMoleculeColumnsManager_TryGetSmallMoleculeTargetFromDetails_Molecule_description_on_line__0__requires_at_least_a_name_and_chemical_formula, rowIndex);
                    return null;
                }
                var molecule = new CustomMolecule(formula, name, new MoleculeAccessionNumbers(accessionNumbers));
                return new Target(molecule);
            }

            if (accessionNumbers.Any())
            {
                errorMessage = string.Format(Resources.SmallMoleculeColumnsManager_TryGetSmallMoleculeTargetFromDetails_Molecule_description_on_line__0__requires_at_least_a_name_and_chemical_formula, rowIndex);
            }

            return null;
        }
    }
}