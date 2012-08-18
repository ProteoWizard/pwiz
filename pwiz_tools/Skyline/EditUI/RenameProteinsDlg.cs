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
using System.Collections.Specialized;
using System.IO;
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
        private readonly SkylineWindow _parent;
        private readonly SrmDocument _document;
        private OrderedDictionary _seqToPeptideGroup;
        private readonly GridViewDriver _gridViewDriver;

        public RenameProteinsDlg(SkylineWindow parent)
        {
            InitializeComponent();
            Icon = Resources.Skyline;

            _parent = parent;
            _document = parent.Document;
            _gridViewDriver = new GridViewDriver(dataGridViewRename, renameProteinsWindowBindingSource,
                                                 new SortableBindingList<RenameProteins>());

            StorePeptideGroups();
        }

        internal class PeptideGroupWithName
        {
            private readonly PeptideGroupDocNode _peptideGroup;

            internal PeptideGroupDocNode PeptideGroup
            {
                get { return _peptideGroup; }
            }

            internal string NewName { get; set; }

            internal PeptideGroupWithName(PeptideGroupDocNode peptideGroup, string newName)
            {
                _peptideGroup = peptideGroup;
                NewName = newName;
            }
        }

        private void StorePeptideGroups()
        {
            _seqToPeptideGroup = new OrderedDictionary();
            foreach (var peptideGroup in _document.PeptideGroups)
            {
                _seqToPeptideGroup.Add(peptideGroup.PeptideGroup.Sequence,
                                       new PeptideGroupWithName(peptideGroup, string.Empty));
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            // Last row is empty (used to add more rows)
            for (int i = 0; i < dataGridViewRename.RowCount - 1; i++)
            {
                string currentName = dataGridViewRename.Rows[i].Cells[0].Value.ToString().Trim();
                string newName = dataGridViewRename.Rows[i].Cells[1].Value.ToString().Trim();

                // Make sure the given current name is an actual protein. 
                PeptideGroupDocNode node = FindProtein(currentName);
                if (node == null)
                {
                    helper.ShowTextBoxError(dataGridViewRename,
                                            Resources.RenameProteinsDlg_OkDialog__0__is_not_a_current_protein,
                                            currentName);
                    return;
                }

                // Only change the name if the new name isn't whitespace.
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    RenameProtein(node, newName);
                }
            }
            DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// Returns protein whose display name matches the given string.
        /// </summary>
        private PeptideGroupDocNode FindProtein(string protein)
        {
            foreach (var peptideGroup in _document.PeptideGroups)
            {
                if (peptideGroup.Name == protein)
                {
                    return peptideGroup;
                }
            }
            return null;
        }

        private void RenameProtein(PeptideGroupDocNode node, string newName)
        {
            _parent.ModifyDocument(string.Format(Resources.RenameProteinsDlg_OkDialog_Edit_name__0__, newName),
                                   doc => (SrmDocument)
                                          doc.ReplaceChild(
                                              node.ChangeName(newName)));
        }

        private void btnPopulate_Click(object sender, EventArgs e)
        {
            GetAllNames();
        }

        /// <summary>
        /// Populate grid with the display names of current proteins in document and new names if given.
        /// </summary>
        public void GetAllNames()
        {
            foreach (var obj in _seqToPeptideGroup.Values)
            {
                var peptideGroup = obj as PeptideGroupWithName;
                if (peptideGroup != null)
                {
                    _gridViewDriver.Items.Add(new RenameProteins
                                                  {
                                                      CurrentName = peptideGroup.PeptideGroup.Name,
                                                      NewName = peptideGroup.NewName
                                                  });
                }
            }
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
                using (var reader = new StreamReader(fastaFile))
                {
                    List<FastaData> fastaSeqs = FastaData.ParseFASTAFile(reader);
                    dataGridViewRename.Rows.Clear();
                    foreach (var seq in fastaSeqs)
                    {
                        // We only want to update sequences that are currently displayed in Skyline
                        if (_seqToPeptideGroup.Contains(seq.Sequence))
                        {
                            var peptideGroup = _seqToPeptideGroup[seq.Sequence] as PeptideGroupWithName;
                            if (peptideGroup != null)
                            {
                                if (peptideGroup.PeptideGroup.Name != seq.Name)
                                {
                                    peptideGroup.NewName = seq.Name;
                                }
                            }
                        }
                    }
                    GetAllNames();
                }
            }
            catch (Exception x)
            {
                MessageDlg.Show(this,
                                string.Format(Resources.RenameProteinsDlg_UseFastaFile_Failed_reading_the_file__0__1__,
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
                                                        NewName = values[1]
                                                    }));
                // Paste items
                Items.Clear();
                foreach (var name in renameList)
                    Items.Add(name);
            }

            private bool ValidateRow(object[] columns, int lineNumber)
            {
                // Should only have columns for current and new name
                if (columns.Length > 2)
                {
                    MessageDlg.Show(MessageParent, string.Format(Resources.GridViewDriver_ValidateRow_On_line__0__row_has_more_than_2_columns, lineNumber));
                    return false;
                }
                return true;
            }
        }

        public class RenameProteins
        {
            public string CurrentName { get; set; }
            public string NewName { get; set; }
        }
    }
}