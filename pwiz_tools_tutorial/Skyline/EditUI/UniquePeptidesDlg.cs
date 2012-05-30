/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    /// <summary>
    /// Dialog box which shows the user which of their peptides match more than one protein in the database,
    /// and allows them to selectively remove peptides from the document.
    /// </summary>
    public partial class UniquePeptidesDlg : FormEx
    {
        private List<ProteinColumn> _proteinColumns;
        private List<PeptideDocNode> _peptideDocNodes;
        private List<HashSet<Protein>> _peptideProteins;
        public UniquePeptidesDlg(IDocumentUIContainer documentUiContainer)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            DocumentUIContainer = documentUiContainer;
            dataGridView1.CurrentCellChanged += dataGridView1_CurrentCellChanged;
        }

        public IDocumentUIContainer DocumentUIContainer { get; private set; }

        void dataGridView1_CurrentCellChanged(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentCell == null || dataGridView1.CurrentRow == null)
            {
                return;
            }
            PeptideDocNode peptideDocNode = (PeptideDocNode) dataGridView1.CurrentRow.Tag;
            if (peptideDocNode == null)
            {
                return;
            }
            String peptideSequence = peptideDocNode.Peptide.Sequence;
            String proteinSequence;
            var proteinColumn = dataGridView1.Columns[dataGridView1.CurrentCell.ColumnIndex].Tag as ProteinColumn;
            if (proteinColumn == null)
            {
                tbxProteinName.Text = PeptideGroupDocNode.Name;
                tbxProteinDescription.Text = PeptideGroupDocNode.Description;
                proteinSequence = PeptideGroupDocNode.PeptideGroup.Sequence;
            }
            else
            {
                tbxProteinName.Text = proteinColumn.Protein.Name;
                tbxProteinDescription.Text = proteinColumn.Protein.Description;
                proteinSequence = proteinColumn.Protein.Sequence;
            }
            if (!string.IsNullOrEmpty(proteinSequence))
            {
                var regex = new Regex(peptideSequence);
                StringBuilder formattedText = new StringBuilder("{\\rtf1\\ansi{\\fonttbl\\f0\\fswiss Helvetica;}{\\colortbl ;\\red0\\green0\\blue255;}\\f0\\pard \\fs16");
                int lastIndex = 0;
                for (Match match = regex.Match(proteinSequence, 0); match.Success; lastIndex = match.Index + match.Length, match = match.NextMatch())
                {
                    formattedText.Append("\\cf0\\b0 " + proteinSequence.Substring(lastIndex, match.Index - lastIndex));
                    formattedText.Append("\\cf1\\b " + proteinSequence.Substring(match.Index, match.Length));
                }
                formattedText.Append("\\cf0\\b0 " + proteinSequence.Substring(lastIndex, proteinSequence.Length - lastIndex));
                formattedText.Append("\\par }");
                richTextBoxSequence.Rtf = formattedText.ToString();
            }
        }

        public PeptideGroupTreeNode PeptideGroupTreeNode { get; set;}
        public PeptideGroupDocNode PeptideGroupDocNode
        {
            get { return (PeptideGroupDocNode) PeptideGroupTreeNode.Model;}
        }
        public SrmDocument SrmDocument { get { return PeptideGroupTreeNode.Document; } }
        public BackgroundProteome BackgroundProteome
        {
            get
            {
                return SrmDocument.Settings.PeptideSettings.BackgroundProteome;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            if (_proteinColumns != null)
            {
                foreach (ProteinColumn proteinColumn in _proteinColumns)
                {
                    dataGridView1.Columns.Remove(proteinColumn.Name);
                }
            }
            _proteinColumns = new List<ProteinColumn>();
            _peptideDocNodes = new List<PeptideDocNode>();
            foreach (var child in PeptideGroupDocNode.Children)
            {
                var nodePep = child as PeptideDocNode;
                if (nodePep != null)
                {
                    _peptideDocNodes.Add(nodePep);
                }
            }
            _peptideProteins = null;
            var peptideSettings = DocumentUIContainer.DocumentUI.Settings.PeptideSettings;
            var digestion = BackgroundProteome.GetDigestion(peptideSettings);
            if (digestion == null)
            {
                MessageDlg.Show(this, string.Format("The background proteome {0} has not yet finished being digested with {1}.", BackgroundProteome.Name, peptideSettings.Enzyme.Name));
                Close();
                return;
            }
            LaunchPeptideProteinsQuery();
        }

        private void LaunchPeptideProteinsQuery()
        {
            HashSet<Protein> proteinSet = new HashSet<Protein>();
            LongWaitDlg longWaitDlg = new LongWaitDlg
            {
                Text = "Querying Background Proteome Database",
                Message =
                    "Looking for proteins with matching peptide sequences"
            };
            try
            {
                longWaitDlg.PerformWork(this, 1000, QueryPeptideProteins);
            }
            catch (Exception x)
            {
                MessageDlg.Show(this, string.Format("Failed querying backgroung proteome {0}.\n{1}", BackgroundProteome.Name, x.Message));
            }
            if (_peptideProteins == null)
            {
                Close();
                return;
            }
            foreach (var proteins in _peptideProteins)
            {
                proteinSet.UnionWith(proteins);
            }
            List<Protein> proteinList = new List<Protein>();
            proteinList.AddRange(proteinSet);
            proteinList.Sort();
            foreach (var protein in proteinList)
            {
                ProteinColumn proteinColumn = new ProteinColumn(_proteinColumns.Count, protein);
                _proteinColumns.Add(proteinColumn);
                DataGridViewCheckBoxColumn column = new DataGridViewCheckBoxColumn
                {
                    Name = proteinColumn.Name,
                    HeaderText = protein.Name,
                    ReadOnly = true,
                    ToolTipText = protein.Description,
                    SortMode = DataGridViewColumnSortMode.Automatic,
                    Tag = proteinColumn,
                };
                dataGridView1.Columns.Add(column);
            }
            for (int i = 0; i < _peptideDocNodes.Count; i++)
            {
                var peptide = _peptideDocNodes[i];
                var proteins = _peptideProteins[i];
                var row = dataGridView1.Rows[dataGridView1.Rows.Add()];
                row.Tag = peptide;
                row.Cells[PeptideIncludedColumn.Name].Value = true;
                row.Cells[PeptideColumn.Name].Value = peptide.Peptide.Sequence;
                foreach (var proteinColumn in _proteinColumns)
                {
                    row.Cells[proteinColumn.Name].Value = proteins.Contains(proteinColumn.Protein);
                }
            }
            dataGridView1.EndEdit();
            if (dataGridView1.RowCount > 0)
            {
                // Select the first peptide to populate the other controls in the dialog.
                dataGridView1.CurrentCell = dataGridView1.Rows[0].Cells[1];
            }
        }

        private void QueryPeptideProteins(ILongWaitBroker longWaitBroker)
        {
            List<HashSet<Protein>> peptideProteins = new List<HashSet<Protein>>();
            Digestion digestion = null;
            if (BackgroundProteome != null)
            {
                digestion = BackgroundProteome.GetDigestion(SrmDocument.Settings.PeptideSettings.Enzyme,
                                                            SrmDocument.Settings.PeptideSettings.DigestSettings);
            }
            foreach (var peptideDocNode in _peptideDocNodes)
            {
                HashSet<Protein> proteins = new HashSet<Protein>();
                if (digestion != null)
                {
                    if (longWaitBroker.IsCanceled)
                    {
                        return;
                    }
                    foreach (Protein protein in digestion.GetProteinsWithSequence(peptideDocNode.Peptide.Sequence))
                    {
                        if (protein.Sequence == PeptideGroupDocNode.PeptideGroup.Sequence)
                        {
                            continue;
                        }
                        proteins.Add(protein);
                    }
                }
                peptideProteins.Add(proteins);
            }
            _peptideProteins = peptideProteins;
        }

        public class ProteinColumn
        {
            public ProteinColumn(int index, Protein protein)
            {
                Index = index;
                Protein = protein;
            }
            public String Name { get { return "protein" + Index; } }
            public int Index { get; set; }
            public Protein Protein { get; set; }
        }

        private void includeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetSelectedRowsIncluded(true);
        }

        private void excludeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetSelectedRowsIncluded(false);
        }

        private void SetSelectedRowsIncluded(bool included)
        {
            dataGridView1.EndEdit();
            for (int i = 0; i < dataGridView1.SelectedRows.Count; i++)
            {
                var row = dataGridView1.SelectedRows[i];
                row.Cells[PeptideIncludedColumn.Name].Value = included;
            }
            if (dataGridView1.CurrentRow != null)
            {
                dataGridView1.CurrentRow.Cells[PeptideIncludedColumn.Name].Value = included;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            Program.MainWindow.ModifyDocument("Exclude peptides", ExcludePeptidesFromDocument);
            Close();
        }

        private SrmDocument ExcludePeptidesFromDocument(SrmDocument srmDocument)
        {
            List<DocNode> children = new List<DocNode>();
            PeptideGroupDocNode peptideGroupDocNode = PeptideGroupDocNode;
            foreach (var docNode in srmDocument.Children)
            {
                children.Add(!Equals(docNode, peptideGroupDocNode)
                                 ? docNode
                                 : ExcludePeptides((PeptideGroupDocNode) docNode));
            }
            return new SrmDocument(srmDocument, srmDocument.Settings, children);
        }
        private PeptideGroupDocNode ExcludePeptides(PeptideGroupDocNode peptideGroupDocNode)
        {
            HashSet<PeptideDocNode> excludedPeptides = new HashSet<PeptideDocNode>();
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                var row = dataGridView1.Rows[i];
                if (!(bool) row.Cells[PeptideIncludedColumn.Name].Value)
                {
                    excludedPeptides.Add((PeptideDocNode) row.Tag);
                }
            }
            List<PeptideDocNode> children = new List<PeptideDocNode>();
            foreach (PeptideDocNode child in peptideGroupDocNode.Children)
            {
                if (excludedPeptides.Contains(child))
                {
                    continue;
                }
                children.Add(child);
            }
            return new PeptideGroupDocNode(
                peptideGroupDocNode.PeptideGroup, 
                peptideGroupDocNode.Annotations, 
                peptideGroupDocNode.Name, 
                peptideGroupDocNode.Description, 
                children.ToArray(),
                false);
        }

        public DataGridView GetDataGridView()
        {
            return dataGridView1;
        }
    }
}
