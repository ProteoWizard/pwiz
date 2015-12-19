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
using System.Drawing;
using System.Linq;
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
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.EditUI
{
    /// <summary>
    /// Dialog box which shows the user which of their peptides match more than one protein in the database,
    /// and allows them to selectively remove peptides from the document.
    /// </summary>
    public partial class UniquePeptidesDlg : FormEx
    {
        private readonly CheckBox _checkBoxPeptideIncludedColumnHeader = new CheckBox
        {
            Name = "checkBoxPeptideIncludedColumnHeader", // Not L10N
            Size = new Size(18, 18),
            AutoCheck = false
        };
        private List<ProteinColumn> _proteinColumns;
        private List<PeptideDocNode> _peptideDocNodes;
        private List<HashSet<Protein>> _peptideProteins;
        private bool _targetIsInBackgroundProteome;

        public UniquePeptidesDlg(IDocumentUIContainer documentUiContainer)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            DocumentUIContainer = documentUiContainer;
            _targetIsInBackgroundProteome = false;
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
            ProteinMetadata metadata;
            if (proteinColumn == null)
            {
                metadata = PeptideGroupDocNode.ProteinMetadata;
                proteinSequence = PeptideGroupDocNode.PeptideGroup.Sequence;
            }
            else
            {
                metadata = proteinColumn.Protein.ProteinMetadata;
                proteinSequence = proteinColumn.Protein.Sequence;
            }
            tbxProteinName.Text = metadata.Name;
            tbxProteinDescription.Text = metadata.Description;
            tbxProteinDetails.Text = metadata.DisplayTextWithoutNameOrDescription(); // Don't show name or description

            if (!string.IsNullOrEmpty(proteinSequence))
            {
                var regex = new Regex(peptideSequence);
                StringBuilder formattedText = new StringBuilder("{\\rtf1\\ansi{\\fonttbl\\f0\\fswiss Helvetica;}{\\colortbl ;\\red0\\green0\\blue255;}\\f0\\pard \\fs16"); // Not L10N
                int lastIndex = 0;
                for (Match match = regex.Match(proteinSequence, 0); match.Success; lastIndex = match.Index + match.Length, match = match.NextMatch())
                {
                    formattedText.Append("\\cf0\\b0 " + proteinSequence.Substring(lastIndex, match.Index - lastIndex)); // Not L10N
                    formattedText.Append("\\cf1\\b " + proteinSequence.Substring(match.Index, match.Length)); // Not L10N
                }
                formattedText.Append("\\cf0\\b0 " + proteinSequence.Substring(lastIndex, proteinSequence.Length - lastIndex)); // Not L10N
                formattedText.Append("\\par }"); // Not L10N
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
                if (nodePep != null && nodePep.IsProteomic)
                {
                    _peptideDocNodes.Add(nodePep);
                }
            }
            _peptideProteins = null;
            var peptideSettings = DocumentUIContainer.DocumentUI.Settings.PeptideSettings;
            if (!BackgroundProteome.HasDigestion(peptideSettings))
            {
                MessageDlg.Show(this, string.Format(Resources.UniquePeptidesDlg_OnShown_The_background_proteome__0__has_not_yet_finished_being_digested_with__1__,
                                                    BackgroundProteome.Name, peptideSettings.Enzyme.Name));
                Close();
                return;
            }
            LaunchPeptideProteinsQuery();
        }

        private void LaunchPeptideProteinsQuery()
        {
            HashSet<Protein> proteinSet = new HashSet<Protein>();
            using (var longWaitDlg = new LongWaitDlg
                {
                    Text = Resources.UniquePeptidesDlg_LaunchPeptideProteinsQuery_Querying_Background_Proteome_Database,
                    Message = Resources.UniquePeptidesDlg_LaunchPeptideProteinsQuery_Looking_for_proteins_with_matching_peptide_sequences
                })
            {
                try
                {
                    longWaitDlg.PerformWork(this, 1000, QueryPeptideProteins);
                }
                catch (Exception x)
                {
                    var message = TextUtil.LineSeparate(string.Format(Resources.UniquePeptidesDlg_LaunchPeptideProteinsQuery_Failed_querying_background_proteome__0__,
                                                BackgroundProteome.Name), x.Message);
                    MessageDlg.ShowWithException(this, message, x);
                }
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
                    HeaderText = ((protein.Gene != null) ? String.Format(Resources.UniquePeptidesDlg_LaunchPeptideProteinsQuery_, protein.Name, protein.Gene) : protein.Name), // Not L10N
                    ReadOnly = true,
                    ToolTipText = protein.ProteinMetadata.DisplayTextWithoutName(), 
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

            DrawCheckBoxOnPeptideIncludedColumnHeader();
        }

        private void DrawCheckBoxOnPeptideIncludedColumnHeader()
        {
            Rectangle headerRectangle = PeptideIncludedColumn.HeaderCell.ContentBounds;
            headerRectangle.X = headerRectangle.Location.X;

            _checkBoxPeptideIncludedColumnHeader.Location = headerRectangle.Location;
            _checkBoxPeptideIncludedColumnHeader.Click += CheckboxPeptideIncludedColumnHeaderOnClick;

            dataGridView1.Controls.Add(_checkBoxPeptideIncludedColumnHeader);

            PeptideIncludedColumn.HeaderCell.Style.Padding =
                new Padding(PeptideIncludedColumn.HeaderCell.Style.Padding.Left + 18,
                    PeptideIncludedColumn.HeaderCell.Style.Padding.Top,
                    PeptideIncludedColumn.HeaderCell.Style.Padding.Right,
                    PeptideIncludedColumn.HeaderCell.Style.Padding.Bottom);

            SetCheckBoxPeptideIncludedHeaderState();
        }

        private void QueryPeptideProteins(ILongWaitBroker longWaitBroker)
        {
            List<HashSet<Protein>> peptideProteins = new List<HashSet<Protein>>();
            if (BackgroundProteome != null)
            {
                using (var proteomeDb = BackgroundProteome.OpenProteomeDb())
                {
                    Digestion digestion = BackgroundProteome.GetDigestion(proteomeDb, SrmDocument.Settings.PeptideSettings);
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
                                    _targetIsInBackgroundProteome = true;
                                    continue;
                                }
                                proteins.Add(protein);
                            }
                        }
                        peptideProteins.Add(proteins);
                    }
                }
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
            public String Name { get { return "protein" + Index; } } // Not L10N
            public int Index { get; set; }
            public Protein Protein { get; set; }
        }
       
        private void CheckboxPeptideIncludedColumnHeaderOnClick(object sender, EventArgs eventArgs)
        {
            contextMenuStrip1.Show(
                PointToScreen(new Point(PeptideIncludedColumn.HeaderCell.ContentBounds.X,
                    PeptideIncludedColumn.HeaderCell.ContentBounds.Y)));
        }

        private void includeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetSelectedRowsIncluded(true);
        }

        private void excludeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetSelectedRowsIncluded(false);
        }
        
        private void uniqueOnlyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelectUnique();
        }

        private void excludeBackgroundProteomeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExcludeBackgroundProteome();
        }

        private void includeAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetAllRowsIncluded(true);
        }

        private void excludeAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetAllRowsIncluded(false);
        }

        private void SetAllRowsIncluded(bool included)
        {
            dataGridView1.EndEdit();
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                var row = dataGridView1.Rows[i];
                row.Cells[PeptideIncludedColumn.Name].Value = included;
            }
            SetCheckBoxPeptideIncludedHeaderState();
        }

        private void SelectPeptidesWithNumberOfMatchesAtOrBelowThreshold(int threshold)
        {
            dataGridView1.EndEdit();
            for (int rowIndex = 0; rowIndex < dataGridView1.Rows.Count; rowIndex++)
            {
                int matchCount = _targetIsInBackgroundProteome ? 1 : 0;
                var row = dataGridView1.Rows[rowIndex];
                for (int col = 0; col < dataGridView1.ColumnCount; col++)
                {
                    if (col == PeptideIncludedColumn.Index || col == PeptideColumn.Index)
                        continue;

                    if (row.Cells[col].Value is bool && ((bool) row.Cells[col].Value))
                    {
                        matchCount++;
                    }
                    if (matchCount > threshold)
                    {
                        break;
                    }
                }
                row.Cells[PeptideIncludedColumn.Name].Value = (matchCount <= threshold);
            }
            SetCheckBoxPeptideIncludedHeaderState();
        }

        private void SetSelectedRowsIncluded(bool included)
        {
            dataGridView1.EndEdit();

            IEnumerable<DataGridViewRow> selectedRows = dataGridView1.SelectedCells.Cast<DataGridViewCell>()
                                                                     .Select(cell => cell.OwningRow)
                                                                     .Distinct();
            foreach (DataGridViewRow row in selectedRows)
            {
                row.Cells[PeptideIncludedColumn.Name].Value = included;
            }

            SetCheckBoxPeptideIncludedHeaderState();
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
            Program.MainWindow.ModifyDocument(Resources.UniquePeptidesDlg_OkDialog_Exclude_peptides, ExcludePeptidesFromDocument);
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
                peptideGroupDocNode.ProteinMetadata, 
                children.ToArray(),
                false);
        }

        private void dataGridView1_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            dataGridView1.EndEdit();
            SetCheckBoxPeptideIncludedHeaderState();
        }

        private void SetCheckBoxPeptideIncludedHeaderState()
        {
            bool atLeastOneChecked = false;
            bool atLeastOneUnchecked = false;
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                var row = dataGridView1.Rows[i];
                if (((bool)row.Cells[PeptideIncludedColumn.Name].Value))
                {
                    atLeastOneChecked = true;
                }
                if (((bool)row.Cells[PeptideIncludedColumn.Name].Value) == false)
                {
                    atLeastOneUnchecked = true;
                }
                if (atLeastOneChecked && atLeastOneUnchecked)
                {
                    break;
                }
            }

            if (atLeastOneChecked && atLeastOneUnchecked)
                _checkBoxPeptideIncludedColumnHeader.CheckState = CheckState.Indeterminate;
            else if (atLeastOneChecked)
                _checkBoxPeptideIncludedColumnHeader.CheckState = CheckState.Checked;
            else
                _checkBoxPeptideIncludedColumnHeader.CheckState = CheckState.Unchecked;
        }

        #region for testing

        public DataGridView GetDataGridView()
        {
            return dataGridView1;
        }

        public void SelectUnique()
        {
            SelectPeptidesWithNumberOfMatchesAtOrBelowThreshold(1);
        }

        public void ExcludeBackgroundProteome()
        {
            SelectPeptidesWithNumberOfMatchesAtOrBelowThreshold(0);
        }

        #endregion

    }
}
