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

namespace pwiz.Skyline.Controls
{

    /// <summary>
    /// Handles updating small molecule detail columns when target is set
    /// </summary>
    public class SmallMoleculeColumnsManager
    {
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
            var smallMoleculeLibraryAttributes = new List<SmallMoleculeLibraryAttributes>();

            var targets = targetResolver.AvailableTargets;

            if (targets != null)
            {
                smallMoleculeLibraryAttributes = targets.Where(t => !t.IsProteomic)
                    .Select(t => t.Molecule.GetSmallMoleculeLibraryAttributes()).ToList();
            }
            if (!smallMoleculeLibraryAttributes.Any() && modeUI != SrmDocument.DOCUMENT_TYPE.proteomic)
            {
                // No targets to inspect, assume we want all the detail columns
                var dummy = @"dummy";
                var otherKeys = MoleculeAccessionNumbers.PREFERRED_DISPLAY_ORDER.ToDictionary(tag => tag, tag => dummy);
                smallMoleculeLibraryAttributes.Add(SmallMoleculeLibraryAttributes.Create(dummy, @"CH", dummy, otherKeys));
            }

            if (!smallMoleculeLibraryAttributes.Any())
            {
                return;
            }

            var formulaHeader = Resources.SmallMoleculeLibraryAttributes_KeyValuePairs_Formula;
            if (smallMoleculeLibraryAttributes.Any(a => !string.IsNullOrEmpty(a.ChemicalFormula)) &&
                !gridView.Columns.Contains(formulaHeader))
            {
                gridView.Columns.Insert(InsertColumnsAt, new DataGridViewTextBoxColumn());
                FormulaColumnIndex = InsertColumnsAt;
                var dataGridViewColumn = gridView.Columns[InsertColumnsAt++];
                dataGridViewColumn.Name = dataGridViewColumn.HeaderText = formulaHeader;
                dataGridViewColumn.Visible = true;
                dataGridViewColumn.ReadOnly = ReadOnly;
            }
            AccessionColumnIndexes = new Dictionary<string, int>();
            foreach (var tag in MoleculeAccessionNumbers.PREFERRED_DISPLAY_ORDER)
            {
                if (!gridView.Columns.Contains(tag) && 
                    ((Equals(tag, MoleculeAccessionNumbers.TagInChiKey) && smallMoleculeLibraryAttributes.Any(a => !string.IsNullOrEmpty(a.InChiKey)))
                     || smallMoleculeLibraryAttributes.Any(a => a.CreateMoleculeID().AccessionNumbers.Keys.Contains(tag))))
                {
                    gridView.Columns.Insert(InsertColumnsAt, new DataGridViewTextBoxColumn());
                    AccessionColumnIndexes.Add(tag, InsertColumnsAt);
                    var dataGridViewColumn = gridView.Columns[InsertColumnsAt++];
                    dataGridViewColumn.Name = dataGridViewColumn.HeaderText = tag;
                    dataGridViewColumn.Visible = true;
                    dataGridViewColumn.ReadOnly = readOnly;
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

                if (DataGridView.Rows.Count > 0)
                {
                    var row = DataGridView.Rows[0];
                    var nVisibleLeftOfFormula = 0;
                    for (var i = 0; i < row.Cells.Count; i++)
                    {
                        if (Equals(row.Cells[i].OwningColumn.HeaderText,
                            Resources.SmallMoleculeLibraryAttributes_KeyValuePairs_Formula))
                        {
                            return FormulaColumnIndex.Value - nVisibleLeftOfFormula;
                        }
                        if (row.Cells[i].Displayed || row.Cells[i].Value != null)
                        {
                            nVisibleLeftOfFormula++;
                        }
                    }
                }
                return 0;
            }
        }

        /// <summary>
        /// Helps deal with hidden columns and their effect on column indexing
        /// </summary>
        public string[] PadHiddenColumns(string[] columns)
        {
            var nHidden = FormulaColumnShift;
            if (nHidden != 0)
            {
                var columnCount = columns.Length;
                if (columnCount + nHidden > FormulaColumnIndex)
                {
                    // Shift any pasted small mol details into the proper column
                    var expanded = columns.ToList();
                    for (var i = 0; i < nHidden; i++)
                    {
                        expanded.Add(null);
                    }
                    var expandedCount = expanded.Count;
                    int index;
                    for (index = expandedCount; index-- > FormulaColumnIndex;)
                    {
                        expanded[index] = expanded[index-1];
                    }
                    for (var i = 0; i < nHidden; i++)
                    {
                        expanded[index--] = null;
                    }
                    return expanded.ToArray();
                }
            }
            return columns;
        }

        private DataGridView DataGridView { get; }
        private int? FormulaColumnIndex { get; } // Always leftmost of inserted detail columns
        public TargetResolver TargetResolver { get; private set; }
        private int InsertColumnsAt;
        private bool ReadOnly;
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
                row.Cells[FormulaColumnIndex.Value].Value = mol.Formula;
            }

            var accessionNumbers = mol.AccessionNumbers.AccessionNumbers;
            foreach (var col in AccessionColumnIndexes)
            {
                if (accessionNumbers.TryGetValue(col.Key, out var value))
                {
                    row.Cells[col.Value].Value = value;
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
        public Target TryGetSmallMoleculeTargetFromDetails(string name, IEnumerable<string> values, int rowIndex,
            out string errorMessage)
        {
            return TryGetSmallMoleculeTargetFromDetails(name, values.ToArray(), FormulaColumnShift, rowIndex, out errorMessage);
        }

        private Target TryGetSmallMoleculeTargetFromDetails(string name, string[] strings, int nHiddenColumns, int rowIndex, out string errorMessage)
        {
            errorMessage = null;
            var formula = FormulaColumnIndex.HasValue && strings.Length > FormulaColumnIndex.Value - nHiddenColumns ?  strings[FormulaColumnIndex.Value - nHiddenColumns] : null;
            var accessionNumbers = new Dictionary<string, string>();
            foreach (var pair in AccessionColumnIndexes)
            {
                if (pair.Value > -1 && strings.Length > pair.Value - nHiddenColumns && !string.IsNullOrEmpty(strings[pair.Value - nHiddenColumns]))
                {
                    accessionNumbers.Add(pair.Key, strings[pair.Value - nHiddenColumns]);
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