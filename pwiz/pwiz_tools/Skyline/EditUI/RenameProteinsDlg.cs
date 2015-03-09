/*
 * Original author: Shannon Joyner <saj9191 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class RenameProteinsDlg : FormEx
    {
        private readonly SrmDocument _document;
        private readonly GridViewDriver _gridViewDriver;

        public RenameProteinsDlg(SrmDocument document)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            _document = document;
            _gridViewDriver = new GridViewDriver(dataGridViewRename, renameProteinsWindowBindingSource,
                                                 new SortableBindingList<RenameProteins>());
        }

        public IDictionary<string, string> DictNameToName { get; private set; }

        public int NameCount
        {
            get { return _gridViewDriver.RowCount - 1; } // Extra row is for adding more proteins
        }

        private void OnLoad(object sender, EventArgs e)
        {
            // If you set this in the Designer, DataGridView has a defect that causes it to throw an
            // exception if the the cursor is positioned over the record selector column during loading.
            dataGridViewRename.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            // Last row is empty (used to add more rows)
            var dictNameToName = new Dictionary<string, string>();
            var setNamesExisting = new HashSet<string>();
            foreach (var nodePepGroup in _document.MoleculeGroups)
                setNamesExisting.Add(nodePepGroup.Name);
            foreach (var proteinNames in _gridViewDriver.Items)
            {
                string currentName = proteinNames.CurrentName;
                string newName = proteinNames.NewName;
                if (string.IsNullOrWhiteSpace(newName))
                    continue;

                string existingName;
                if (dictNameToName.TryGetValue(currentName, out existingName))
                {
                    // If there are two rows for exactly the same rename, just ignore the repetition
                    if (Equals(existingName, newName))
                        continue;
                    MessageDlg.Show(this, string.Format(Resources.RenameProteinsDlg_OkDialog_Cannot_rename__0__more_than_once__Please_remove_either__1__or__2__, currentName, existingName, newName));
                    return;
                }
                if (!setNamesExisting.Contains(currentName))
                {
                    MessageDlg.Show(this, string.Format(Resources.RenameProteinsDlg_OkDialog__0__is_not_a_current_protein,
                                                        currentName));
                    return;
                }

                dictNameToName.Add(currentName, newName);
            }
            DictNameToName = dictNameToName;
            DialogResult = DialogResult.OK;
        }

        private void btnPopulate_Click(object sender, EventArgs e)
        {
            PopulateGrid();
        }

        /// <summary>
        /// Populate grid with the display names of current proteins in document and new names if given.
        /// </summary>
        public void PopulateGrid()
        {
            _gridViewDriver.Populate(
                _document.MoleculeGroups.Select(nodePepGroup => nodePepGroup.Name)
                                       .Distinct()
                                       .Select(name => new RenameProteins{ CurrentName = name, NewName = null}));
        }

        private void btnFASTA_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog
                                            {
                                                Title = Resources.RenameProteinsDlg_btnFASTA_Click_Add_FASTA_File,
                                                InitialDirectory = Settings.Default.FastaDirectory,
                                                CheckPathExists = true
                                                // FASTA files often have no extension as well as .fasta and others
                                            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.FastaDirectory = Path.GetDirectoryName(dlg.FileName);
                    UseFastaFile(dlg.FileName);
                }
            }
        }

        /// <summary>
        /// Obtain new names from a FASTA file.
        /// </summary>
        public void UseFastaFile(string fastaFile)
        {
            try
            {
                var dictExistToNewName = new Dictionary<string, string>();
                using (var reader = new StreamReader(fastaFile))
                {
                    var dictSeqToNames = new Dictionary<string, List<string>>();
                    foreach (var nodePepGroup in _document.MoleculeGroups)
                    {
                        string sequence = nodePepGroup.PeptideGroup.Sequence;
                        if (string.IsNullOrEmpty(sequence))
                            continue;

                        List<string> names;
                        if (!dictSeqToNames.TryGetValue(sequence, out names))
                        {
                            names = new List<string>();
                            dictSeqToNames.Add(sequence, names);
                        }
                        if (!names.Contains(nodePepGroup.Name))
                            names.Add(nodePepGroup.Name);
                    }

                    foreach (var seq in FastaData.ParseFastaFile(reader))
                    {
                        List<string> names;
                        if (dictSeqToNames.TryGetValue(seq.Sequence, out names))
                        {
                            // Ignore multiple occurrances of the same sequence in the FASTA file
                            dictSeqToNames.Remove(seq.Sequence);

                            foreach (var name in names)
                            {
                                if (Equals(name, seq.Name))
                                    continue;                                
                                if (dictExistToNewName.ContainsKey(name))
                                    throw new IOException(string.Format(Resources.RenameProteinsDlg_UseFastaFile_The_document_contains_a_naming_conflict_The_name__0__is_currently_used_by_multiple_protein_sequences, name));
                                dictExistToNewName.Add(name, seq.Name);
                            }
                        }
                    }
                }

                _gridViewDriver.Populate(_document.MoleculeGroups
                    .Where(nodePepGroup => dictExistToNewName.ContainsKey(nodePepGroup.Name))
                    .Select(nodePepGroup => new RenameProteins
                    {
                        CurrentName = nodePepGroup.Name,
                        NewName = dictExistToNewName[nodePepGroup.Name]
                    }));
                if (NameCount == 0)
                {
                    MessageDlg.Show(this, string.Format(Resources.RenameProteinsDlg_UseFastaFile_No_protein_sequence_matches_found_between_the_current_document_and_the_FASTA_file__0_,
                                                        fastaFile));
                }
            }
            catch (IOException x)
            {
                MessageDlg.Show(this, string.Format(Resources.RenameProteinsDlg_UseFastaFile_Failed_reading_the_file__0__1__,
                                                    fastaFile, x.Message));
            }
        }

        private class GridViewDriver : SimpleGridViewDriver<RenameProteins>
        {
            public GridViewDriver(DataGridViewEx gridView, BindingSource bindingSource,
                                   SortableBindingList<RenameProteins> items)
                : base(gridView, bindingSource, items)
            {
                
            }

            protected override void DoPaste()
            {
                var renameList = new List<RenameProteins>();

                GridView.DoPaste(MessageParent, ValidateRow,
                                 values =>
                                 renameList.Add(new RenameProteins
                                                    {
                                                        CurrentName = values[0],
                                                        NewName = values.Length == 2 ? values[1] : string.Empty
                                                    }));
                // Paste items
                Items.Clear();
                foreach (var name in renameList)
                    Items.Add(name);
            }

            private bool ValidateRow(object[] columns, IWin32Window parent, int lineNumber)
            {
                // Should only have columns for current and new name
                if (columns.Length > 2)
                {
                    MessageDlg.Show(parent, string.Format(Resources.GridViewDriver_ValidateRow_On_line__0__row_has_more_than_2_columns, lineNumber));
                    return false;
                }
                return true;
            }

            public void Populate(IEnumerable<RenameProteins> values)
            {
                Items.RaiseListChangedEvents = false;
                try
                {
                    Items.Clear();
                    foreach (var value in values)
                    {
                        Items.Add(value);
                    }
                }
                finally
                {
                    Items.RaiseListChangedEvents = true;
                }
                Items.ResetBindings();
            }
        }

        public class RenameProteins
        {
            public string CurrentName { get; set; }
            public string NewName { get; set; }
        }

        // CONSIDER bspratt - wouldn't a button for restoring the original name be useful too?

        private void UseAccessionOrPreferredNameorGene(ProteinDisplayMode mode)
        {
            var dictNodeToNewName = new Dictionary<string, string>();
            foreach (var nodePepGroup in _document.MoleculeGroups)
            {
                string newname;
                if (!dictNodeToNewName.TryGetValue(nodePepGroup.Name, out newname))
                {
                    string text = null;
                    switch (mode)
                    {
                        case ProteinDisplayMode.ByAccession:
                            text = nodePepGroup.ProteinMetadata.Accession;
                            break;
                        case ProteinDisplayMode.ByPreferredName:
                            text = nodePepGroup.ProteinMetadata.PreferredName;
                            break;
                        case ProteinDisplayMode.ByGene:
                            text = nodePepGroup.ProteinMetadata.Gene;
                            break;
                    }
                    text = text ?? nodePepGroup.Name;
                    dictNodeToNewName.Add(nodePepGroup.Name, text);
                }
            }


            _gridViewDriver.Populate(_document.MoleculeGroups
                .Where(nodePepGroup => dictNodeToNewName.ContainsKey(nodePepGroup.Name))
                .Select(nodePepGroup => new RenameProteins
                {
                    CurrentName = nodePepGroup.Name,
                    NewName = dictNodeToNewName[nodePepGroup.Name]
                }));
            if (NameCount == 0)
            {
                MessageDlg.Show(this, string.Format(Resources.RenameProteinsDlg_UseAccessionOrPreferredNameorGene_No_protein_metadata_available));
            }
            
        }

        private void Accession_Click(object sender, EventArgs e)
        {
            UseAccessionOrPreferredNameorGene(ProteinDisplayMode.ByAccession);
        }

        private void PreferredName_Click(object sender, EventArgs e)
        {
            UseAccessionOrPreferredNameorGene(ProteinDisplayMode.ByPreferredName);
        }

        private void Gene_Click(object sender, EventArgs e)
        {
            UseAccessionOrPreferredNameorGene(ProteinDisplayMode.ByGene);
        }


        #region Functional test support

        public void Clear()
        {
            _gridViewDriver.Items.Clear();
        }

        public void Paste()
        {
            _gridViewDriver.OnPaste();
        }

        #endregion

    }
}