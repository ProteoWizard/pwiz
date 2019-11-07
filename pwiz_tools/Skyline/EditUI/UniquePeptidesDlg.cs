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
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
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
    public partial class UniquePeptidesDlg : ModeUIInvariantFormEx,  // This dialog is inherently proteomic, never wants the "peptide"->"molecule" translation
           IAuditLogModifier<UniquePeptidesDlg.UniquePeptideSettings>
    {
        private readonly CheckBox _checkBoxPeptideIncludedColumnHeader = new CheckBox
        {
            Name = @"checkBoxPeptideIncludedColumnHeader",
            Size = new Size(18, 18),
            AutoCheck = false
        };
        private List<ProteinColumn> _proteinColumns;
        private List<Tuple<IdentityPath, PeptideDocNode>> _peptideDocNodes;
        private List<HashSet<Protein>> _peptideProteins;
        private readonly HashSet<IdentityPath> _peptidesInBackgroundProteome;

        // Support multiple selection (though using peptide settings is more efficient way to do this filtering)
        public static List<PeptideGroupTreeNode> PeptideSelection(SequenceTree sequenceTree)
        {
            HashSet<PeptideGroupTreeNode> treeNodeSet = new HashSet<PeptideGroupTreeNode>();
            var peptideGroupTreeNodes = new List<PeptideGroupTreeNode>();
            foreach (var node in sequenceTree.SelectedNodes)
            {
                PeptideGroupTreeNode peptideGroupTreeNode = null;
                var treeNode = node as SrmTreeNode;
                if (treeNode != null)
                    peptideGroupTreeNode = treeNode.GetNodeOfType<PeptideGroupTreeNode>();
                if (peptideGroupTreeNode == null || !treeNodeSet.Add(peptideGroupTreeNode))
                {
                    continue;
                }
                if (peptideGroupTreeNode.DocNode.Peptides.Any())
                {
                    peptideGroupTreeNodes.Add(peptideGroupTreeNode);
                }
            }
            return peptideGroupTreeNodes;
        }

        public UniquePeptidesDlg(IDocumentUIContainer documentUiContainer)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            DocumentUIContainer = documentUiContainer;
            _peptidesInBackgroundProteome = new HashSet<IdentityPath>();
            dataGridView1.CurrentCellChanged += dataGridView1_CurrentCellChanged; 
        }

        public enum UniquenessType
        {
            protein, // Reject any peptide found in more than one protein  in background proteome
            gene, // Reject any peptide associated with more than one gene in background proteome
            species // Reject any peptide associated with more than one species in background proteome
        };

        public IDocumentUIContainer DocumentUIContainer { get; private set; }

        void dataGridView1_CurrentCellChanged(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentCell == null || dataGridView1.CurrentRow == null)
            {
                return;
            }
            var rowTag = (Tuple<IdentityPath, PeptideDocNode>) dataGridView1.CurrentRow.Tag;
            if (rowTag == null)
            {
                return;
            }
            PeptideDocNode peptideDocNode = rowTag.Item2;
            // Expecting to find this peptide
            var peptideGroupDocNode = PeptideGroupDocNodes.FirstOrDefault(g => null != g.FindNode(peptideDocNode.Peptide));
            if (peptideGroupDocNode == null)
            {
                return;
            }
            String peptideSequence = peptideDocNode.Peptide.Target.Sequence;
            String proteinSequence;
            var proteinColumn = dataGridView1.Columns[dataGridView1.CurrentCell.ColumnIndex].Tag as ProteinColumn;
            ProteinMetadata metadata;
            if (proteinColumn == null)
            {
                metadata = peptideGroupDocNode.ProteinMetadata;
                proteinSequence = peptideGroupDocNode.PeptideGroup.Sequence;
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
                // ReSharper disable LocalizableElement
                StringBuilder formattedText = new StringBuilder("{\\rtf1\\ansi{\\fonttbl\\f0\\fswiss Helvetica;}{\\colortbl ;\\red0\\green0\\blue255;}\\f0\\pard \\fs16");
                // ReSharper restore LocalizableElement
                int lastIndex = 0;
                for (Match match = regex.Match(proteinSequence, 0); match.Success; lastIndex = match.Index + match.Length, match = match.NextMatch())
                {
                    // ReSharper disable LocalizableElement
                    formattedText.Append("\\cf0\\b0 " + proteinSequence.Substring(lastIndex, match.Index - lastIndex));
                    formattedText.Append("\\cf1\\b " + proteinSequence.Substring(match.Index, match.Length));
                    // ReSharper restore LocalizableElement
                }
                // ReSharper disable LocalizableElement
                formattedText.Append("\\cf0\\b0 " + proteinSequence.Substring(lastIndex, proteinSequence.Length - lastIndex));
                formattedText.Append("\\par }");
                // ReSharper restore LocalizableElement
                richTextBoxSequence.Rtf = formattedText.ToString();
            }
        }

        public List<PeptideGroupTreeNode> PeptideGroupTreeNodes { get; set;}
        public IEnumerable<PeptideGroupDocNode> PeptideGroupDocNodes
        {
            get { return PeptideGroupTreeNodes.Select(n => (PeptideGroupDocNode)(n.Model)); }
        }
        public SrmDocument SrmDocument { get { return PeptideGroupTreeNodes.First().Document; } }
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
                    dataGridView1.Columns.Remove(dataGridView1.Columns[proteinColumn.Index]);
                }
            }
            _proteinColumns = new List<ProteinColumn>();
            _peptideDocNodes = new List<Tuple<IdentityPath, PeptideDocNode>>();
            foreach (var peptideGroupDocNode in PeptideGroupDocNodes)
            {
                foreach (PeptideDocNode nodePep in peptideGroupDocNode.Children)
                {
                    if (nodePep.IsProteomic)
                    {
                        _peptideDocNodes.Add(Tuple.Create(new IdentityPath(peptideGroupDocNode.Id, nodePep.Id), nodePep));
                    }
                }
            }
            _peptideProteins = null;
            LaunchPeptideProteinsQuery();
        }

        private void LaunchPeptideProteinsQuery()
        {
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

            var longOperationRunner = new LongOperationRunner
            {
                ParentControl = this
            };
            bool success = longOperationRunner.CallFunction(AddProteinRowsToGrid);
            if (!success)
            {
                Close();
            }
        }

        private bool AddProteinRowsToGrid(ILongWaitBroker longWaitBroker)
        {
            longWaitBroker.Message = Resources.UniquePeptidesDlg_AddProteinRowsToGrid_Adding_rows_to_grid_;
            HashSet<Protein> proteinSet = new HashSet<Protein>();
            foreach (var proteins in _peptideProteins)
            {
                proteinSet.UnionWith(proteins);
            }
            List<Protein> proteinList = new List<Protein>();
            proteinList.AddRange(proteinSet);
            proteinList.Sort();
            var proteinsByPreferredNameCounts = proteinList
                .Where(p => !string.IsNullOrEmpty(p.PreferredName))
                .ToLookup(p => p.PreferredName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(grouping => grouping.Key, grouping => grouping.Count(), StringComparer.OrdinalIgnoreCase);

            var newColumns = new List<DataGridViewColumn>();
            foreach (var protein in proteinList)
            {
                ProteinColumn proteinColumn = new ProteinColumn(_proteinColumns.Count + dataGridView1.ColumnCount, protein);
                _proteinColumns.Add(proteinColumn);
                // ReSharper disable LocalizableElement
                var accession = string.IsNullOrEmpty(protein.Accession) ? string.Empty : protein.Accession + "\n";
                // ReSharper restore LocalizableElement
                var proteinName = protein.Name;
                // Isoforms may all get the same preferredname, which is confusing to look at
                if (!string.IsNullOrEmpty(protein.PreferredName))
                {
                    int countProteinsWithSameName;
                    if (proteinsByPreferredNameCounts.TryGetValue(protein.PreferredName, out countProteinsWithSameName) && countProteinsWithSameName == 1)
                    {
                        proteinName = protein.PreferredName;
                    }
                }
                // ReSharper disable LocalizableElement
                var gene = string.IsNullOrEmpty(protein.Gene) ? string.Empty : "\n" + protein.Gene;
                // ReSharper restore LocalizableElement
                DataGridViewCheckBoxColumn column = new DataGridViewCheckBoxColumn
                {
                    Name = proteinColumn.Name,
                    HeaderText = accession + proteinName + gene,
                    ReadOnly = true,
                    ToolTipText = protein.ProteinMetadata.DisplayTextWithoutName(),
                    SortMode = DataGridViewColumnSortMode.Automatic,
                    FillWeight = 1f,
                    Tag = proteinColumn,
                };
                if (longWaitBroker.IsCanceled)
                {
                    return false;
                }
                newColumns.Add(column);
            }
            int actualProteinCount = dataGridView1.AddColumns(newColumns);
            if (actualProteinCount < _proteinColumns.Count)
            {
                _proteinColumns.RemoveRange(actualProteinCount, _proteinColumns.Count - actualProteinCount);
            }

            for (int i = 0; i < _peptideDocNodes.Count; i++)
            {
                if (longWaitBroker.IsCanceled)
                {
                    return false;
                }
                longWaitBroker.ProgressValue = 100 * i / _peptideDocNodes.Count;
                var peptideTag = _peptideDocNodes[i];
                var proteins = _peptideProteins[i];
                var row = dataGridView1.Rows[dataGridView1.Rows.Add()];
                row.Tag = peptideTag;
                row.Cells[PeptideIncludedColumn.Index].Value = true;
                row.Cells[PeptideColumn.Index].Value = peptideTag.Item2.Peptide.Target;
                foreach (var proteinColumn in _proteinColumns)
                {
                    row.Cells[proteinColumn.Index].Value = proteins.Contains(proteinColumn.Protein);
                }
            }
            dataGridView1.EndEdit();
            if (dataGridView1.RowCount > 0)
            {
                // Select the first peptide to populate the other controls in the dialog.
                dataGridView1.CurrentCell = dataGridView1.Rows[0].Cells[1];
            }

            DrawCheckBoxOnPeptideIncludedColumnHeader();
            return true;
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
                using (var proteomeDb = BackgroundProteome.OpenProteomeDb(longWaitBroker.CancellationToken))
                {
                    Digestion digestion = proteomeDb.GetDigestion();
                    if (digestion != null)
                    {
                        var peptidesOfInterest = _peptideDocNodes.Select(node => node.Item2.Peptide.Target.Sequence);
                        var sequenceProteinsDict = digestion.GetProteinsWithSequences(peptidesOfInterest);
                        if (longWaitBroker.IsCanceled)
                        {
                            return;
                        }
                        foreach (var tuple in _peptideDocNodes)
                        {
                            if (longWaitBroker.IsCanceled)
                            {
                                return;
                            }
                            var peptideGroup = (PeptideGroup) tuple.Item1.GetIdentity(0);
                            var peptideDocNode = tuple.Item2;
                            HashSet<Protein> proteins = new HashSet<Protein>();
                            var peptideGroupDocNode = PeptideGroupDocNodes.First(g => ReferenceEquals(g.PeptideGroup, peptideGroup));
                            List<Protein> proteinsForSequence;
                            if (sequenceProteinsDict.TryGetValue(peptideDocNode.Peptide.Target.Sequence, out proteinsForSequence))
                            {
                                if (peptideGroupDocNode != null)
                                {
                                    foreach (var protein in proteinsForSequence)
                                    {
                                        if (protein.Sequence == peptideGroupDocNode.PeptideGroup.Sequence)
                                        {
                                            _peptidesInBackgroundProteome.Add(tuple.Item1);
                                            continue;
                                        }
                                        proteins.Add(protein);
                                    }
                                }
                            }
                            peptideProteins.Add(proteins);
                        }
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
            public String Name { get { return @"protein" + Index; } }
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

        private void uniqueProteinsOnlyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelectUnique(UniquenessType.protein);
       }

        private void uniqueGenesOnlyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelectPeptidesWithNumberOfMatchesAtOrBelowThreshold(1, UniquenessType.gene);
        }

        private void uniqueSpeciesOnlyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelectPeptidesWithNumberOfMatchesAtOrBelowThreshold(1, UniquenessType.species);
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

        private void SelectPeptidesWithNumberOfMatchesAtOrBelowThreshold(int threshold, UniquenessType uniqueBy)
        {
            dataGridView1.EndEdit();
            var dubious = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int rowIndex = 0; rowIndex < dataGridView1.Rows.Count; rowIndex++)
            {
                var row = dataGridView1.Rows[rowIndex];
                var rowTag = (Tuple<IdentityPath, PeptideDocNode>) row.Tag;
                int matchCount = _peptidesInBackgroundProteome.Contains(rowTag.Item1) ? 1 : 0;
                for (int col = 0; col < dataGridView1.ColumnCount; col++)
                {
                    if (col == PeptideIncludedColumn.Index || col == PeptideColumn.Index)
                        continue;

                    if (row.Cells[col].Value is bool && ((bool) row.Cells[col].Value))
                    {
                        if (uniqueBy == UniquenessType.protein)
                        {
                            matchCount++;
                        }
                        else
                        {
                            var peptide = rowTag.Item2;
                            var parent = PeptideGroupDocNodes.First(p => p.Children.Contains(peptide));
                            string testValA;
                            string testValB;
                            // ATP5B and atp5b are the same thing, as are "mus musculus" and "MUS MUSCULUS"
                            if (uniqueBy == UniquenessType.gene)
                            {
                                // ReSharper disable once PossibleNullReferenceException
                                testValA = parent.ProteinMetadata.Gene;
                                testValB = ((ProteinColumn) dataGridView1.Columns[col].Tag).Protein.Gene;
                            }
                            else
                            {
                                // ReSharper disable once PossibleNullReferenceException
                                testValA = parent.ProteinMetadata.Species;
                                testValB = ((ProteinColumn) dataGridView1.Columns[col].Tag).Protein.Species;
                            }
                            // Can't filter on something that isn't there - require nonempty values
                            if (!string.IsNullOrEmpty(testValA) && !string.IsNullOrEmpty(testValB) && 
                                string.Compare(testValA, testValB, StringComparison.OrdinalIgnoreCase) != 0)
                                matchCount++;
                            if (string.IsNullOrEmpty(testValA))
                            {
                                dubious.Add(parent.Name);
                            }
                            if (string.IsNullOrEmpty(testValB))
                            {
                                dubious.Add(((ProteinColumn)dataGridView1.Columns[col].Tag).Protein.Name);
                            }
                        }
                    }
                    if (matchCount > threshold)
                    {
                        break;
                    }
                }
                row.Cells[PeptideIncludedColumn.Name].Value = (matchCount <= threshold);
            }
            SetCheckBoxPeptideIncludedHeaderState();
            if (dubious.Any())
            {
                var dubiousValues = TextUtil.LineSeparate(uniqueBy == UniquenessType.gene ? 
                    Resources.UniquePeptidesDlg_SelectPeptidesWithNumberOfMatchesAtOrBelowThreshold_Some_background_proteome_proteins_did_not_have_gene_information__this_selection_may_be_suspect_ :
                    Resources.UniquePeptidesDlg_SelectPeptidesWithNumberOfMatchesAtOrBelowThreshold_Some_background_proteome_proteins_did_not_have_species_information__this_selection_may_be_suspect_,
                    Resources.UniquePeptidesDlg_SelectPeptidesWithNumberOfMatchesAtOrBelowThreshold_These_proteins_include_,
                    TextUtil.LineSeparate(dubious));
                MessageDlg.Show(this, dubiousValues);
            }
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
            Program.MainWindow.ModifyDocument(Resources.UniquePeptidesDlg_OkDialog_Exclude_peptides, ExcludePeptidesFromDocument, FormSettings.EntryCreator.Create);
            Close();
        }

        private SrmDocument ExcludePeptidesFromDocument(SrmDocument srmDocument)
        {
            List<DocNode> children = new List<DocNode>();
            foreach (var docNode in srmDocument.Children)
            {
                children.Add(!PeptideGroupDocNodes.Contains(docNode)
                                 ? docNode
                                 : ExcludePeptides((PeptideGroupDocNode) docNode));
            }
            return (SrmDocument) srmDocument.ChangeChildrenChecked(children);
        }

        public class ProteinPeptideSelection : IAuditLogObject
        {
            public ProteinPeptideSelection(string proteinName, List<string> peptides)
            {
                ProteinName = proteinName;
                Peptides = peptides;
            }

            protected bool Equals(ProteinPeptideSelection other)
            {
                return string.Equals(ProteinName, other.ProteinName);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((ProteinPeptideSelection) obj);
            }

            public override int GetHashCode()
            {
                return (ProteinName != null ? ProteinName.GetHashCode() : 0);
            }

            public string ProteinName { get; private set; }
            [Track]
            public List<string> Peptides { get; private set; }

            public string AuditLogText { get { return ProteinName; } }
            public bool IsName { get { return true; } }
        }

        public UniquePeptideSettings FormSettings
        {
            get { return new UniquePeptideSettings(this); }
        }

        public class UniquePeptideSettings : AuditLogOperationSettings<UniquePeptideSettings>
        {
            private readonly int _excludedCount;

            public override MessageInfo MessageInfo
            {
                get { return new MessageInfo(_excludedCount == 1 ? MessageType.excluded_peptide : MessageType.excluded_peptides, SrmDocument.DOCUMENT_TYPE.proteomic, _excludedCount); }
            }

            public UniquePeptideSettings(UniquePeptidesDlg dlg)
            {
                ProteinPeptideSelections = new Dictionary<int, ProteinPeptideSelection>();
                for (var i = 0; i < dlg.dataGridView1.Rows.Count; ++i)
                {
                    var row = dlg.dataGridView1.Rows[i];
                    var rowTag = (Tuple<IdentityPath, PeptideDocNode>)row.Tag;
                    if (!(bool)row.Cells[dlg.PeptideIncludedColumn.Name].Value)
                    {
                        var id = rowTag.Item1.GetIdentity(0);
                        if (!ProteinPeptideSelections.ContainsKey(id.GlobalIndex))
                        {
                            var node = (PeptideGroupDocNode)dlg.SrmDocument.FindNode(id);
                            ProteinPeptideSelections.Add(id.GlobalIndex, new ProteinPeptideSelection(node.ProteinMetadata.Name, new List<string>()));
                        }

                        var item = ProteinPeptideSelections[id.GlobalIndex];
                        item.Peptides.Add(PeptideTreeNode.GetLabel(rowTag.Item2, string.Empty));
                        ++_excludedCount;
                    }
                }
            }

            [TrackChildren]
            public Dictionary<int, ProteinPeptideSelection> ProteinPeptideSelections { get; private set; }
        }

        private PeptideGroupDocNode ExcludePeptides(PeptideGroupDocNode peptideGroupDocNode)
        {
            var excludedPeptides = new HashSet<IdentityPath>();
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                var row = dataGridView1.Rows[i];
                var rowTag = (Tuple<IdentityPath, PeptideDocNode>) row.Tag;
                if (!(bool) row.Cells[PeptideIncludedColumn.Name].Value && ReferenceEquals(rowTag.Item1.GetIdentity(0), peptideGroupDocNode.Id))
                {
                    excludedPeptides.Add(rowTag.Item1);
                }
            }

            var nodeGroupNew = peptideGroupDocNode.ChangeChildrenChecked(peptideGroupDocNode.Molecules.Where(pep =>
                !excludedPeptides.Contains(new IdentityPath(peptideGroupDocNode.PeptideGroup, pep.Id))).ToArray());
            if (!ReferenceEquals(nodeGroupNew, peptideGroupDocNode))
                nodeGroupNew = nodeGroupNew.ChangeAutoManageChildren(false);
            return (PeptideGroupDocNode) nodeGroupNew;
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

        public void SelectUnique(UniquenessType uniquenessType)
        {
            SelectPeptidesWithNumberOfMatchesAtOrBelowThreshold(1, uniquenessType);
        }

        public void ExcludeBackgroundProteome()
        {
            SelectPeptidesWithNumberOfMatchesAtOrBelowThreshold(0, UniquenessType.protein);
        }

        #endregion
    }
}
