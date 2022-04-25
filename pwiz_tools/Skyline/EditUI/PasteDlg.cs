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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Controls;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.EditUI
{
    // CONSIDER bspratt: Checkbox for hiding and showing new protein columns
    public partial class PasteDlg : FormEx, IMultipleViewProvider
    {
        private readonly StatementCompletionTextBox _statementCompletionEditBox;
        private bool _noErrors;
        private readonly AuditLogEntryCreatorList _entryCreators;

        public PasteDlg(IDocumentUIContainer documentUiContainer)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            DocumentUiContainer = documentUiContainer;
            _entryCreators = new AuditLogEntryCreatorList();

            _statementCompletionEditBox = new StatementCompletionTextBox(DocumentUiContainer)
                                              {
                                                  MatchTypes = ProteinMatchTypes.OfValues(ProteinMatchType.name, ProteinMatchType.description)
                                              };
            _statementCompletionEditBox.SelectionMade += statementCompletionEditBox_SelectionMade;
            gridViewProteins.DataGridViewKey += OnDataGridViewKey;
            gridViewPeptides.DataGridViewKey += OnDataGridViewKey;
        }

        void OnDataGridViewKey(object sender, KeyEventArgs e)
        {
            _statementCompletionEditBox.OnKeyPreview(sender, e);
        }

        void statementCompletionEditBox_SelectionMade(StatementCompletionItem statementCompletionItem)
        {
            if (tabControl1.SelectedTab == tabPageProteinList)
            {
                _statementCompletionEditBox.TextBox.Text = statementCompletionItem.ProteinInfo.Name;
                gridViewProteins.EndEdit();
            }
            else if (tabControl1.SelectedTab == tabPagePeptideList)
            {
                _statementCompletionEditBox.TextBox.Text = statementCompletionItem.Peptide;
                if (gridViewPeptides.CurrentRow != null)
                {
                    gridViewPeptides.CurrentRow.Cells[colPeptideProtein.Index].Value 
                        = statementCompletionItem.ProteinInfo.Name;
                }
                gridViewPeptides.EndEdit();    
            }
        }

        public IDocumentUIContainer DocumentUiContainer { get; private set; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            DocumentUiContainer.ListenUI(OnDocumentUIChanged);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            DocumentUiContainer.UnlistenUI(OnDocumentUIChanged);
        }

        private IdentityPath _selectedPath;
        public IdentityPath SelectedPath
        {
            get { return _selectedPath; }
            set
            {
                _selectedPath = value;

                // Handle insert node path
                if (_selectedPath != null &&
                    _selectedPath.Depth == (int)SrmDocument.Level.MoleculeGroups &&
                    ReferenceEquals(_selectedPath.GetIdentity((int)SrmDocument.Level.MoleculeGroups), SequenceTree.NODE_INSERT_ID))
                {
                    _selectedPath = null;
                }
            }
        }

        public string ErrorText
        {
            get { return panelError.Visible ? tbxError.Text : null; }
        }

        public int SelectedGridRow
        {
            get
            {
                var cell = ActiveGridView.CurrentCell;
                return cell != null ? cell.RowIndex : -1;
            }
        }

        public int SelectedGridColumn
        {
            get
            {
                var cell = ActiveGridView.CurrentCell;
                return cell != null ? cell.ColumnIndex : -1;
            }
        }

        private DataGridView ActiveGridView
        {
            get
            {
                return gridViewProteins.Visible
                   ? gridViewProteins
                   : gridViewPeptides;
            }
        }

        public void ShowError(PasteError pasteError)
        {
            _noErrors = false;
            panelError.Visible = true;
            if (pasteError == null)
            {
                tbxError.Text = string.Empty;
                tbxError.Visible = false;
                Text = Description;
                return;
            }
            tbxError.BackColor = Color.Red;
            tbxError.ForeColor = Color.White;
            tbxError.Text = pasteError.Message;
            // Useful for debugging if this hangs in a test - it appears in the timeout report  
            // ReSharper disable LocalizableElement
            Text = Description + " (" + pasteError.Message + ")";
            // ReSharper restore LocalizableElement
        }

        public void ShowNoErrors()
        {
            _noErrors = true;
            panelError.Visible = true;
            tbxError.Text = Resources.PasteDlg_ShowNoErrors_No_errors;
            tbxError.BackColor = Color.LightGreen;
            tbxError.ForeColor = Color.Black;
            Text = Description;  // Clear any error info
        }

        public void HideNoErrors()
        {
            if (!_noErrors)
            {
                return;
            }
            panelError.Visible = false;
            Text = Description;  // Clear any error info
        }

        private void btnValidate_Click(object sender, EventArgs e)
        {
            ValidateCells();
        }

        public void ValidateCells()
        {
            IdentityPath selectedPath = null;
            var document = GetNewDocument(DocumentUiContainer.Document, true, ref selectedPath);
            if (document != null)
                ShowNoErrors();
        }

        private SrmDocument GetNewDocument(SrmDocument document, bool validating, ref IdentityPath selectedPath)
        {
            List<PeptideGroupDocNode> newPeptideGroups;
            return GetNewDocument(document, validating, ref selectedPath, out newPeptideGroups);
        }

        private SrmDocument GetNewDocument(SrmDocument document, bool validating, ref IdentityPath selectedPath, out List<PeptideGroupDocNode> newPeptideGroups)
        {
            var fastaHelper = new ImportFastaHelper(tbxFasta, tbxError, panelError, toolTip1);
            if ((document = fastaHelper.AddFasta(document, null, null, ref selectedPath, out newPeptideGroups, out var error)) == null)
            {
                fastaHelper.ShowFastaError(error);
                tabControl1.SelectedTab = tabPageFasta;  // To show fasta errors
                return null;
            }
            if ((document = AddProteins(document, ref selectedPath)) == null)
            {
                return null;
            }
            if ((document = AddPeptides(document, validating, ref selectedPath)) == null)
            {
                return null;
            }
            return document;
        }

        private void SetCurrentCellForPasteError(DataGridView gridView, PasteError pasteError, int? columnIndex = null)
        {
            ShowError(pasteError);
            var errColumn = columnIndex ?? pasteError.Column;
            if (errColumn >= 0 && gridView.Rows[pasteError.Line].Cells[errColumn].Visible)
            {
                gridView.CurrentCell = gridView.Rows[pasteError.Line].Cells[errColumn];
            }
            else
            {
                // Set the row even if desired column isn't visible - just pick the first available column
                for (var firstVisibleColumn = 0; firstVisibleColumn < gridView.Rows[pasteError.Line].Cells.Count; firstVisibleColumn++)
                {
                    if (gridView.Rows[pasteError.Line].Cells[firstVisibleColumn].Visible)
                    {
                        gridView.CurrentCell = gridView.Rows[pasteError.Line].Cells[firstVisibleColumn];
                        break;
                    }
                }
            }
        }

        private void ShowProteinError(PasteError pasteError)
        {
            tabControl1.SelectedTab = tabPageProteinList;
            SetCurrentCellForPasteError(gridViewProteins, pasteError, colProteinName.Index);
        }

        private void ShowPeptideError(PasteError pasteError)
        {
            tabControl1.SelectedTab = tabPagePeptideList;
            SetCurrentCellForPasteError(gridViewPeptides, pasteError);
        }

        private SrmDocument AddPeptides(SrmDocument document, bool validating, ref IdentityPath selectedPath)
        {
            if (tabControl1.SelectedTab != tabPagePeptideList)
                return document;

            var matcher = new ModificationMatcher();
            var listPeptideSequences = ListPeptideSequences();
            if (listPeptideSequences == null)
                return null;
            try
            {
                matcher.CreateMatches(document.Settings, listPeptideSequences, Settings.Default.StaticModList,
                                      Settings.Default.HeavyModList);
            }
            catch (FormatException e)
            {
                MessageDlg.ShowException(this, e);
                ShowPeptideError(new PasteError
                                     {
                                         Column = colPeptideSequence.Index,
                                         Message = Resources.PasteDlg_AddPeptides_Unable_to_interpret_peptide_modifications
                                     });
                return null;
            }
            var strNameMatches = matcher.FoundMatches;
            if (!validating && !string.IsNullOrEmpty(strNameMatches))
            {
                string message = TextUtil.LineSeparate(Resources.PasteDlg_AddPeptides_Would_you_like_to_use_the_Unimod_definitions_for_the_following_modifications,
                                                        string.Empty, strNameMatches);
                if (MultiButtonMsgDlg.Show(this, message, Resources.PasteDlg_AddPeptides_OK) == DialogResult.Cancel)
                    return null;
            }
            var backgroundProteome = GetBackgroundProteome(document);
            // Insert last to first so that proteins get inserted on top of each other
            // in the order they are added. Peptide insertion into peptide lists needs
            // to be carefully tracked to insert them in the order they are listed in
            // the grid.
            int lastGroupGlobalIndex = 0, lastPeptideIndex = -1;
            for (int i = gridViewPeptides.Rows.Count - 1; i >= 0; i--)
            {
                PeptideGroupDocNode peptideGroupDocNode;
                var row = gridViewPeptides.Rows[i];
                var pepModSequence = Convert.ToString(row.Cells[colPeptideSequence.Index].Value);
                pepModSequence = FastaSequence.NormalizeNTerminalMod(pepModSequence);
                var proteinName = Convert.ToString(row.Cells[colPeptideProtein.Index].Value);
                if (string.IsNullOrEmpty(pepModSequence) && string.IsNullOrEmpty(proteinName))
                    continue;
                if (string.IsNullOrEmpty(proteinName))
                {
                    peptideGroupDocNode = GetSelectedPeptideGroupDocNode(document, selectedPath);
                    if (!IsPeptideListDocNode(peptideGroupDocNode))
                    {
                        peptideGroupDocNode = null;
                    }
                }
                else
                {
                    peptideGroupDocNode = FindPeptideGroupDocNode(document, proteinName);
                }
                if (peptideGroupDocNode == null)
                {
                    if (string.IsNullOrEmpty(proteinName))
                    {
                        peptideGroupDocNode = new PeptideGroupDocNode(new PeptideGroup(),
                                                                      document.GetPeptideGroupId(true), null,
                                                                      new PeptideDocNode[0]);
                    }
                    else
                    {
                        ProteinMetadata metadata = null;
                        PeptideGroup peptideGroup = backgroundProteome.IsNone ? new PeptideGroup()
                            : (backgroundProteome.GetFastaSequence(proteinName, out metadata) ??
                                                    new PeptideGroup());
                        if (metadata != null)
                            peptideGroupDocNode = new PeptideGroupDocNode(peptideGroup, metadata, new PeptideDocNode[0]);
                        else
                            peptideGroupDocNode = new PeptideGroupDocNode(peptideGroup, proteinName,
                                                                      peptideGroup.Description, new PeptideDocNode[0]);
                    }
                    // Add to the end, if no insert node
                    var to = selectedPath;
                    if (to == null || to.Depth < (int)SrmDocument.Level.MoleculeGroups)
                        document = (SrmDocument)document.Add(peptideGroupDocNode);
                    else
                    {
                        Identity toId = selectedPath.GetIdentity((int) SrmDocument.Level.MoleculeGroups);
                        document = (SrmDocument) document.Insert(toId, peptideGroupDocNode);
                    }
                    selectedPath = new IdentityPath(peptideGroupDocNode.Id);
                }
                var peptides = new List<PeptideDocNode>();
                foreach (PeptideDocNode peptideDocNode in peptideGroupDocNode.Children)
                {
                    peptides.Add(peptideDocNode);
                }

                var fastaSequence = peptideGroupDocNode.PeptideGroup as FastaSequence;
                PeptideDocNode nodePepNew;
                if (fastaSequence != null)
                {
                    // Attempt to create node for error checking.
                    nodePepNew = fastaSequence.CreateFullPeptideDocNode(document.Settings,
                                                                        new Target(FastaSequence.StripModifications(pepModSequence)));
                    if (nodePepNew == null)
                    {
                        ShowPeptideError(new PasteError
                                             {
                                                 Column = colPeptideSequence.Index,
                                                 Line = i,
                                                 Message = Resources.PasteDlg_AddPeptides_This_peptide_sequence_was_not_found_in_the_protein_sequence
                                             });
                        return null;
                    }
                }
                // Create node using ModificationMatcher.
                nodePepNew = matcher.GetModifiedNode(pepModSequence, fastaSequence).ChangeSettings(document.Settings,
                                                                                                  SrmSettingsDiff.ALL);
                // Avoid adding an existing peptide a second time.
                if (!peptides.Contains(nodePep => Equals(nodePep.Key, nodePepNew.Key)))
                {
                    if (nodePepNew.Peptide.FastaSequence != null)
                    {
                        peptides.Add(nodePepNew);
                        peptides.Sort(FastaSequence.ComparePeptides);
                    }
                    else
                    {
                        int groupGlobalIndex = peptideGroupDocNode.PeptideGroup.GlobalIndex;
                        if (groupGlobalIndex == lastGroupGlobalIndex && lastPeptideIndex != -1)
                        {
                            peptides.Insert(lastPeptideIndex, nodePepNew);
                        }
                        else
                        {
                            lastPeptideIndex = peptides.Count;
                            peptides.Add(nodePepNew);
                        }
                        lastGroupGlobalIndex = groupGlobalIndex;
                    }
                    var newPeptideGroupDocNode = new PeptideGroupDocNode(peptideGroupDocNode.PeptideGroup, peptideGroupDocNode.Annotations, peptideGroupDocNode.Name, peptideGroupDocNode.Description, peptides.ToArray(), false);
                    document = (SrmDocument)document.ReplaceChild(newPeptideGroupDocNode);
                }
            }
            if (!validating && listPeptideSequences.Count > 0)
            {
                var pepModsNew = matcher.GetDocModifications(document);
                document = document.ChangeSettings(document.Settings.ChangePeptideModifications(mods => pepModsNew));
                document.Settings.UpdateDefaultModifications(false);
            }
            return document;
        }

        private List<string> ListPeptideSequences()
        {
            List<string> listSequences = new List<string>();
            for (int i = gridViewPeptides.Rows.Count - 1; i >= 0; i--)
            {
                var row = gridViewPeptides.Rows[i];
                var peptideSequence = Convert.ToString(row.Cells[colPeptideSequence.Index].Value);
                var proteinName = Convert.ToString(row.Cells[colPeptideProtein.Index].Value);
                if (string.IsNullOrEmpty(peptideSequence) && string.IsNullOrEmpty(proteinName))
                {
                    continue;
                }
                if (string.IsNullOrEmpty(peptideSequence))
                {
                    ShowPeptideError(new PasteError
                    {
                        Column = colPeptideSequence.Index,
                        Line = i,
                        Message = Resources.PasteDlg_ListPeptideSequences_The_peptide_sequence_cannot_be_blank
                    });
                    return null;
                }

                CrosslinkLibraryKey crosslinkLibraryKey =
                    CrosslinkSequenceParser.TryParseCrosslinkLibraryKey(peptideSequence, 0);
                if (crosslinkLibraryKey == null)
                {
                    if (!FastaSequence.IsExSequence(peptideSequence))
                    {
                        ShowPeptideError(new PasteError
                        {
                            Column = colPeptideSequence.Index,
                            Line = i,
                            Message = Resources.PasteDlg_ListPeptideSequences_This_peptide_sequence_contains_invalid_characters
                        });
                        return null;
                    }
                    peptideSequence = FastaSequence.NormalizeNTerminalMod(peptideSequence);
                }
                else if (!crosslinkLibraryKey.IsSupportedBySkyline())
                {
                    ShowPeptideError(new PasteError
                    {
                        Column = colPeptideSequence.Index,
                        Line = i,
                        Message = Resources.PasteDlg_ListPeptideSequences_The_structure_of_this_crosslinked_peptide_is_not_supported_by_Skyline
                    });
                    return null;
                }

                listSequences.Add(peptideSequence);
            }
            return listSequences;
        }

        private static bool IsPeptideListDocNode(PeptideGroupDocNode peptideGroupDocNode)
        {
            return peptideGroupDocNode != null && peptideGroupDocNode.IsPeptideList;
        }

        private SrmDocument AddProteins(SrmDocument document, ref IdentityPath selectedPath)
        {
            if (tabControl1.SelectedTab != tabPageProteinList)
                return document;

            var backgroundProteome = GetBackgroundProteome(document);
            for (int i = gridViewProteins.Rows.Count - 1; i >= 0; i--)
            {
                var row = gridViewProteins.Rows[i];
                var proteinName = Convert.ToString(row.Cells[colProteinName.Index].Value);
                if (String.IsNullOrEmpty(proteinName))
                {
                    continue;
                }
                var pastedMetadata = new ProteinMetadata(proteinName,
                    Convert.ToString(row.Cells[colProteinDescription.Index].Value), 
                    SmallMoleculeTransitionListReader.NullForEmpty(Convert.ToString(row.Cells[colProteinPreferredName.Index].Value)), 
                    SmallMoleculeTransitionListReader.NullForEmpty(Convert.ToString(row.Cells[colProteinAccession.Index].Value)),
                    SmallMoleculeTransitionListReader.NullForEmpty(Convert.ToString(row.Cells[colProteinGene.Index].Value)), 
                    SmallMoleculeTransitionListReader.NullForEmpty(Convert.ToString(row.Cells[colProteinSpecies.Index].Value)));
                FastaSequence fastaSequence = null;
                if (!backgroundProteome.IsNone)
                {
                    ProteinMetadata protdbMetadata;
                    fastaSequence = backgroundProteome.GetFastaSequence(proteinName, out protdbMetadata);
                    // Fill in any gaps in pasted metadata with that in protdb
                    pastedMetadata = pastedMetadata.Merge(protdbMetadata);
                }
                // Strip any whitespace (tab, newline etc) In case it was copied out of a FASTA file
                var fastaSequenceString = new string(Convert.ToString(row.Cells[colProteinSequence.Index].Value).Where(c => !Char.IsWhiteSpace(c)).ToArray()); 
                if (!string.IsNullOrEmpty(fastaSequenceString))
                {
                        try
                        {
                            if (fastaSequence == null) // Didn't match anything in protdb
                            {
                                fastaSequence = new FastaSequence(pastedMetadata.Name, pastedMetadata.Description,
                                                                  new ProteinMetadata[0], fastaSequenceString);
                            }
                            else
                            {
                                if (fastaSequence.Sequence != fastaSequenceString)
                                {
                                    fastaSequence = new FastaSequence(pastedMetadata.Name, pastedMetadata.Description,
                                                                      fastaSequence.Alternatives, fastaSequenceString);
                                }
                            }
                        }
                        catch (Exception exception)
                        {
                            ShowProteinError(new PasteError
                                                 {
                                                     Line = i,
                                                     Column = colProteinDescription.Index,
                                                     Message = string.Format(Resources.PasteDlg_AddProteins_Invalid_protein_sequence__0__, exception.Message)
                                                 });
                            return null;
                        }
                }
                if (fastaSequence == null)
                {
                    ShowProteinError(
                        new PasteError
                        {
                                             Line = i,
                                Message = backgroundProteome.IsNone
                                        ? Resources.PasteDlg_AddProteins_Missing_protein_sequence
                                        : Resources.PasteDlg_AddProteins_This_protein_was_not_found_in_the_background_proteome_database
                        });
                    return null;
                }
                var description = pastedMetadata.Description;
                if (!string.IsNullOrEmpty(description) && description != fastaSequence.Description)
                {
                    fastaSequence = new FastaSequence(fastaSequence.Name, description, fastaSequence.Alternatives, fastaSequence.Sequence);
                }
                pastedMetadata = pastedMetadata.ChangeName(fastaSequence.Name).ChangeDescription(fastaSequence.Description);  // Make sure these agree
                var nodeGroupPep = new PeptideGroupDocNode(fastaSequence, pastedMetadata, new PeptideDocNode[0]);
                nodeGroupPep = nodeGroupPep.ChangeSettings(document.Settings, SrmSettingsDiff.ALL);
                var to = selectedPath;
                if (to == null || to.Depth < (int)SrmDocument.Level.MoleculeGroups)
                    document = (SrmDocument)document.Add(nodeGroupPep);
                else
                {
                    Identity toId = selectedPath.GetIdentity((int)SrmDocument.Level.MoleculeGroups);
                    document = (SrmDocument)document.Insert(toId, nodeGroupPep);
                }
                selectedPath = new IdentityPath(nodeGroupPep.Id);
            }
            return document;
        }

        private const char TRANSITION_LIST_SEPARATOR = TextUtil.SEPARATOR_TSV;
        private static readonly ColumnIndices TRANSITION_LIST_COL_INDICES = new ColumnIndices(
            0, 1, 2, 3);


        private static PeptideGroupDocNode FindPeptideGroupDocNode(SrmDocument document, PeptideGroupDocNode nodePepGroup)
        {
            if (!nodePepGroup.IsPeptideList)
                return (PeptideGroupDocNode) document.FindNode(nodePepGroup.PeptideGroup);

            // Find peptide lists by name
            return FindPeptideGroupDocNode(document, nodePepGroup.Name);
        }

        private static PeptideGroupDocNode FindPeptideGroupDocNode(SrmDocument document, String name)
        {
            return document.MoleculeGroups.FirstOrDefault(n => Equals(name, n.Name));
        }

        private PeptideGroupDocNode GetSelectedPeptideGroupDocNode(SrmDocument document, IdentityPath selectedPath)
        {
            var to = selectedPath;
            if (to != null && to.Depth >= (int)SrmDocument.Level.MoleculeGroups)
                return (PeptideGroupDocNode) document.FindNode(to.GetIdentity((int) SrmDocument.Level.MoleculeGroups));

            PeptideGroupDocNode lastPeptideGroupDocuNode = null;
            foreach (PeptideGroupDocNode peptideGroupDocNode in document.MoleculeGroups)
            {
                lastPeptideGroupDocuNode = peptideGroupDocNode;
            }
            return lastPeptideGroupDocuNode;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
            DialogResult = DialogResult.Cancel;
        }

        private static void OnDocumentUIChanged(object sender, DocumentChangedEventArgs e)
        {
            
        }

        public PasteFormat PasteFormat
        {
            get
            {
                return GetPasteFormat(tabControl1.SelectedTab);
            }
            set
            {
                var tab = GetTabPage(value);
                for (int i = tabControl1.Controls.Count - 1; i >= 0; i--)
                {
                    if (tabControl1.Controls[i] != tab)
                    {
                        tabControl1.Controls.RemoveAt(i);
                    }
                }
                if (tab.Parent == null)
                {
                    tabControl1.Controls.Add(tab);
                }
                tabControl1.SelectedTab = tab;
                AcceptButton = tabControl1.SelectedTab != tabPageFasta ? btnInsert : null;
            }
        }

        public string Description
        {
            get
            {
                switch (PasteFormat)
                {
                    case PasteFormat.fasta: return Resources.PasteDlg_Description_Insert_FASTA;
                    case PasteFormat.protein_list: return Resources.PasteDlg_Description_Insert_protein_list;
                    case PasteFormat.peptide_list: return Resources.PasteDlg_Description_Insert_peptide_list;
                }
                return Resources.PasteDlg_Description_Insert;
            }
        }

// ReSharper disable MemberCanBeMadeStatic.Local
        private void tabControl1_Selecting(object sender, TabControlCancelEventArgs e)
        {
            // Should no longer be possible to change tabs
        }
// ReSharper restore MemberCanBeMadeStatic.Local

        private PasteFormat GetPasteFormat(TabPage tabPage)
        {
            if (tabPage == tabPageFasta)
            {
                return PasteFormat.fasta;
            }
            if (tabPage == tabPageProteinList)
            {
                return PasteFormat.protein_list;
            }
            if (tabPage == tabPagePeptideList)
            {
                return PasteFormat.peptide_list;
            }
            return PasteFormat.none;
        }

        private TabPage GetTabPage(PasteFormat pasteFormat)
        {
            switch (pasteFormat)
            {
                case PasteFormat.fasta:
                    return tabPageFasta;
                case PasteFormat.protein_list:
                    return tabPageProteinList;
                case PasteFormat.peptide_list:
                    return tabPagePeptideList;
            }
            return null;
        }

        private static BackgroundProteome GetBackgroundProteome(SrmDocument srmDocument)
        {
            return srmDocument.Settings.PeptideSettings.BackgroundProteome;
        }

        private void gridViewProteins_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            HideNoErrors();
            if (e.ColumnIndex < 0 || e.RowIndex < 0)
            {
                return;
            }
            var column = gridViewProteins.Columns[e.ColumnIndex];
            if (column != colProteinName)
            {
                return;
            }
            var row = gridViewProteins.Rows[e.RowIndex];
            var proteinName = Convert.ToString(row.Cells[e.ColumnIndex].Value);
            if (string.IsNullOrEmpty(proteinName))
            {
                gridViewProteins.Rows.Remove(row);
            }

            ProteinMetadata metadata;
            FastaSequence fastaSequence = GetFastaSequence(row, proteinName, out metadata);
            if (fastaSequence == null)
            {
                row.Cells[colProteinDescription.Index].Value = null;
                row.Cells[colProteinSequence.Index].Value = null;
                row.Cells[colProteinPreferredName.Index].Value = null;
                row.Cells[colProteinAccession.Index].Value = null;
                row.Cells[colProteinGene.Index].Value = null;
                row.Cells[colProteinSpecies.Index].Value = null;
            }
            else
            {
                row.Cells[colProteinName.Index].Value = fastaSequence.Name; // Possibly the search was actually on accession, gene etc
                row.Cells[colProteinDescription.Index].Value = fastaSequence.Description;
                row.Cells[colProteinSequence.Index].Value = fastaSequence.Sequence;
                row.Cells[colProteinPreferredName.Index].Value = (metadata == null) ? null : metadata.PreferredName;
                row.Cells[colProteinAccession.Index].Value = (metadata == null) ? null : metadata.Accession;
                row.Cells[colProteinGene.Index].Value = (metadata == null) ? null : metadata.Gene;
                row.Cells[colProteinSpecies.Index].Value = (metadata == null) ? null : metadata.Species;
            }
        }
       private void gridViewPeptides_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }
            var row = gridViewPeptides.Rows[e.RowIndex];
            var column = gridViewPeptides.Columns[e.ColumnIndex];
            if (column != colPeptideProtein)
            {
                return;
            }
            var proteinName = Convert.ToString(row.Cells[colPeptideProtein.Index].Value);
            ProteinMetadata metadata;
            FastaSequence fastaSequence = GetFastaSequence(row, proteinName, out metadata);
            row.Cells[colPeptideProteinDescription.Index].Value = fastaSequence == null ? null : fastaSequence.Description;
        }

        /// <summary>
        /// Enumerates table entries for all proteins matching a pasted peptide.
        /// This can't be done on gridViewPeptides_CellValueChanged because we are creating new cells.
        /// </summary>
        private void EnumerateProteins(DataGridView dataGridView, int rowIndex, bool keepAllPeptides, 
            ref int numUnmatched, ref int numMultipleMatches, ref int numFiltered, HashSet<string> seenPepSeq, AssociateProteinsHelper associateHelper)
        {

            HideNoErrors();      
            var row = dataGridView.Rows[rowIndex];
            int sequenceIndex = Equals(dataGridView, gridViewPeptides)
                                ? colPeptideSequence.Index : -1;
            int proteinIndex = Equals(dataGridView, gridViewPeptides)
                                ? colPeptideProtein.Index : -1;
            
            var proteinName = Convert.ToString(row.Cells[proteinIndex].Value);
            var pepModSequence = Convert.ToString(row.Cells[sequenceIndex].Value);

            var associateAction = associateHelper.DetermineAssociateAction(proteinName, pepModSequence, seenPepSeq, keepAllPeptides);
            numUnmatched = associateHelper.numUnmatched;
            numMultipleMatches = associateHelper.numMultipleMatches;
            numFiltered = associateHelper.numFiltered;
            if (associateAction == AssociateProteinsHelper.AssociateAction.do_not_associate)
            {
                return;
            }
            else if (associateAction == AssociateProteinsHelper.AssociateAction.remove)
            {
                dataGridView.Rows.Remove(row);
                return;
            } else if (associateAction == AssociateProteinsHelper.AssociateAction.throw_exception)
            {
                dataGridView.CurrentCell = row.Cells[sequenceIndex];
                throw new InvalidDataException(Resources.PasteDlg_ListPeptideSequences_This_peptide_sequence_contains_invalid_characters);
            }

            var proteinNames = associateHelper.proteinNames;
            row.Cells[proteinIndex].Value = proteinNames[0];
            // Only using the first occurrence.
            if (associateAction == AssociateProteinsHelper.AssociateAction.first_occurrence)
            {
                return;
            }
            // Finally, enumerate all proteins for this peptide.
            for (int i = 1; i < proteinNames.Count; i ++)
            {
                var newRow = dataGridView.Rows[dataGridView.Rows.Add()];
                // Copy all cells, except for the protein name as well as any cells that are not null, 
                // meaning that they have already been filled out by CellValueChanged.
                for(int x = 0; x < row.Cells.Count; x++)
                {
                    if (newRow.Cells[proteinIndex].Value != null)
                        continue;
                    if (x == proteinIndex)
                        newRow.Cells[proteinIndex].Value = proteinNames[i];
                    else
                        newRow.Cells[x].Value = row.Cells[x].Value;
                }
            }
        }

        /// <summary>
        /// A helper class for associating peptides to proteins in the background proteome. Used across multiple forms.
        /// </summary>
        public class AssociateProteinsHelper
        {
            private SrmDocument _docCurrent;
            public List<string> proteinNames { get; private set;}
            public List<Protein> proteins { get; private set; }
            public int numUnmatched { get; private set;}
            public int numMultipleMatches { get; private set; }
            public int numFiltered{ get; private set; }

            public AssociateProteinsHelper(SrmDocument document)
            {
                proteinNames = new List<string>();
                _docCurrent = document;
                numUnmatched = 0;
                numFiltered = 0;
                numMultipleMatches = 0;
            }

            /// <summary>
            /// Determine the correct action to associate a peptide and update our count of filtered peptides,
            /// unmatched peptides, and peptides with multiple matches
            /// </summary>
            /// <param name="proteinName">The protein name listed in the data</param>
            /// <param name="pepModSequence">Modified sequence of the peptide to be associated</param>
            /// <param name="seenPepSeq">Peptide sequences we have already seen in the data</param>
            /// <param name="keepAllPeptides">Keep peptides that have multiple matches or no matches</param>
            /// <param name="dictSequenceProteins">Optional prefetched mapping of stripped peptides to proteins</param>
            /// <returns></returns>
            public AssociateAction DetermineAssociateAction(string proteinName, string pepModSequence, HashSet<string> seenPepSeq, 
                bool keepAllPeptides, IDictionary<string, List<Protein>> dictSequenceProteins = null)
            {

                // Only enumerate the proteins if the user has not specified a protein.
                if (!string.IsNullOrEmpty(proteinName))
                    return AssociateAction.do_not_associate;

                // If there is no peptide sequence and no protein, remove this entry.
                if (string.IsNullOrEmpty(pepModSequence))
                {
                    return AssociateAction.remove;
                }

                string peptideSequence = FastaSequence.StripModifications(pepModSequence);

                // Check to see if this is a new sequence because we don't want to count peptides more than once for
                // the FilterMatchedPeptidesDlg.
                bool newSequence = !seenPepSeq.Contains(peptideSequence);
                if (newSequence)
                {
                    // If we are not keeping filtered peptides, and this peptide does not match current filter
                    // settings, remove this peptide.
                    if (!FastaSequence.IsExSequence(peptideSequence))
                    {
                        return AssociateAction.throw_exception;
                    }
                    seenPepSeq.Add(peptideSequence);
                }

                if (dictSequenceProteins != null && dictSequenceProteins.TryGetValue(peptideSequence, out var proteinList))
                {
                    proteinNames = proteinList.ConvertAll(p => p.Name);
                }
                else
                {
                    proteinNames = GetProteinNamesForPeptideSequence(peptideSequence, _docCurrent, out proteinList);
                }
                proteins = proteinList;

                bool isUnmatched = proteinNames == null || proteinNames.Count == 0;
                bool hasMultipleMatches = proteinNames != null && proteinNames.Count > 1;
                bool isFiltered = !_docCurrent.Settings.Accept(peptideSequence);

                if (newSequence)
                {
                    numUnmatched += isUnmatched ? 1 : 0;
                    numMultipleMatches += hasMultipleMatches ? 1 : 0;
                    numFiltered += isFiltered ? 1 : 0;
                }

                // No protein matches found, so we do not need to enumerate this peptide. 
                if (isUnmatched)
                {
                    // If we are not keeping unmatched peptides, then remove this peptide.
                    if (!keepAllPeptides && !Settings.Default.LibraryPeptidesAddUnmatched)
                        return AssociateAction.remove;
                    // Even if we are keeping this peptide, it has no matches so we don't enumerate it.
                    return AssociateAction.do_not_associate;
                }

                // If there are multiple protein matches, and we are filtering such peptides, remove this peptide.
                if (!keepAllPeptides &&
                    (hasMultipleMatches && FilterMultipleProteinMatches == BackgroundProteome.DuplicateProteinsFilter.NoDuplicates)
                    || (isFiltered && !Settings.Default.LibraryPeptidesKeepFiltered))
                {
                    return AssociateAction.remove;
                }

                // Only using the first occurrence.
                if (!keepAllPeptides && FilterMultipleProteinMatches == BackgroundProteome.DuplicateProteinsFilter.FirstOccurence)
                    return AssociateAction.first_occurrence;
                // Finally, enumerate all proteins for this peptide.
                return AssociateAction.all_occurrences;
            }


            public enum AssociateAction
            {
                remove,
                do_not_associate,
                first_occurrence,
                all_occurrences,
                throw_exception
            }
        }


        private FastaSequence GetFastaSequence(DataGridViewRow row, string proteinName, out ProteinMetadata metadata)
        {
            metadata = null;
            var backgroundProteome = GetBackgroundProteome(DocumentUiContainer.DocumentUI);
            if (backgroundProteome.IsNone)
                return null;

            var fastaSequence = backgroundProteome.GetFastaSequence(proteinName, out metadata);
            if (fastaSequence == null)
            {
                // Sometimes the protein name in the background proteome will have an extra "|" on the end.
                // In that case, update the name of the protein to match the one in the database.
                fastaSequence = backgroundProteome.GetFastaSequence(proteinName + @"|");
                if (fastaSequence != null)
                {
                    row.Cells[colPeptideProtein.Index].Value = fastaSequence.Name;
                }
            }

            return fastaSequence;
        }

        private void OnEditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            _statementCompletionEditBox.Attach(((DataGridView) sender).EditingControl as TextBox);
        }

        private void btnInsert_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            bool error = false;
            IdentityPath newSelectedPath = SelectedPath;
            bool? keepEmptyProteins = null;
            List<PeptideGroupDocNode> newPeptideGroups = null;
            Program.MainWindow.ModifyDocument(
                Description,
                document =>
                {
                    newSelectedPath = SelectedPath;                 
                    var newDocument = GetNewDocument(document, false, ref newSelectedPath, out newPeptideGroups);

                    if (newDocument == null)
                    {
                        error = true;
                        return document;
                    }
                    if (!keepEmptyProteins.HasValue)
                    {
                        keepEmptyProteins = ImportFastaHelper.AskWhetherToKeepEmptyProteins(this,
                            newPeptideGroups.Count(pepGroup => pepGroup.PeptideCount == 0), _entryCreators);
                        if (!keepEmptyProteins.HasValue)
                        {
                            // Cancelled
                            error = true;
                            return document;
                        }
                    }

                    if (!keepEmptyProteins.Value)
                    {
                        newDocument = ImportPeptideSearch.RemoveProteinsByPeptideCount(newDocument, 1);
                    }
                    return newDocument;
                }, docPair =>
                {
                    if (error)
                        return null;

                    MessageType singular;
                    MessageType plural;
                    
                    string extraInfo = null;
                    DataGridViewEx grid = null;

                    IEnumerable<string> added = null;
                    DataGridViewColumn col = null;
                    var count = 0;

                    switch (PasteFormat)
                    {
                        case PasteFormat.fasta:
                        {
                            singular = MessageType.inserted_proteins_fasta; 
                            plural = MessageType.inserted_proteins_fasta;
                            extraInfo = tbxFasta.Text;
                            added = newPeptideGroups.Select(group => group.AuditLogText);
                            count = newPeptideGroups.Count;
                            break;
                        }
                        case PasteFormat.protein_list:
                            singular = MessageType.inserted_protein;
                            plural = MessageType.inserted_proteins;
                            grid = gridViewProteins;
                            col = colProteinName;
                            break;
                        case PasteFormat.peptide_list:
                            singular = MessageType.inserted_peptide;
                            plural = MessageType.inserted_peptides;
                            grid = gridViewPeptides;
                            col = colPeptideSequence;
                            break;
                        default:
                            return null;
                    }

                    if (grid != null)
                    {
                        extraInfo = grid.GetCopyText();

                        if (col != null)
                        {
                            added = grid.Rows.OfType<DataGridViewRow>().Select(row => row.Cells[col.Index].Value as string);
                            count = grid.RowCount - 1;
                        }        
                    }

                    return AuditLogEntry.CreateCountChangeEntry(singular, plural, docPair.NewDocumentType, added, count)
                        .ChangeExtraInfo(extraInfo)
                        .Merge(docPair, _entryCreators, false);
                });
            if (error)
            {
                return;
            }
            SelectedPath = newSelectedPath;
            DialogResult = DialogResult.OK;
        }

        public override void CancelDialog()
        {
            DialogResult = DialogResult.Cancel;
        }

        private void tbxFasta_TextChanged(object sender, EventArgs e)
        {
            HideNoErrors();
        }

        private void gridViewPeptides_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            _statementCompletionEditBox.MatchTypes = e.ColumnIndex == colPeptideSequence.Index
                ? ProteinMatchTypes.Singleton(ProteinMatchType.sequence) : ProteinMatchTypes.EMPTY;
        }

        private void gridViewProteins_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            _statementCompletionEditBox.MatchTypes = e.ColumnIndex == colProteinName.Index
                ? ProteinMatchTypes.ALL.Except(ProteinMatchType.sequence) : ProteinMatchTypes.EMPTY;  // name, description, accession, etc
        }
        
        private void gridViewProteins_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.V && e.Modifiers == Keys.Control)
            {
                if (!gridViewProteins.IsCurrentCellInEditMode)
                {
                    PasteProteins();
                    e.Handled = true;
                }
            }
        }

        public void PasteFasta()  // For functional test use
        {
            tbxFasta.Text = ClipboardHelper.GetClipboardText(this);
        }

        public void PasteProteins()
        {
            Paste(gridViewProteins, false);
        }
        
        private void gridViewPeptides_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.V && e.Modifiers == Keys.Control)
            {
                if (!gridViewPeptides.IsCurrentCellInEditMode)
                {
                    PastePeptides();
                    e.Handled = true;
                }
            }
        }

        public void PastePeptides()
        {       
            var document = DocumentUiContainer.Document;
            var backgroundProteome = document.Settings.PeptideSettings.BackgroundProteome;
            Paste(gridViewPeptides, !backgroundProteome.IsNone);
        }

        /// <summary>
        /// Removes the given number of last rows in the given DataGridView.
        /// </summary>
        private static void RemoveLastRows(DataGridView dataGridView, int numToRemove)
        {
            int rowCount = dataGridView.Rows.Count;
            for (int i = rowCount - numToRemove; i < rowCount; i++)
            {
                dataGridView.Rows.Remove(dataGridView.Rows[dataGridView.Rows.Count - 2]);
            }
        }    
              
        public static BackgroundProteome.DuplicateProteinsFilter FilterMultipleProteinMatches
        {
            get
            {
                return Helpers.ParseEnum(Settings.Default.LibraryPeptidesAddDuplicatesEnum,
                                         BackgroundProteome.DuplicateProteinsFilter.AddToAll);
            }
        }

        private void Paste(DataGridView dataGridView, bool enumerateProteins)
        {
            string text = ClipboardHelper.GetClipboardText(this);
            if (text == null)
            {
                return;
            }

            int numUnmatched;
            int numMultipleMatches;
            int numFiltered;
            int prevRowCount = dataGridView.RowCount;
            try
            {
                Paste(dataGridView, text, enumerateProteins, enumerateProteins, out numUnmatched, 
                    out numMultipleMatches, out numFiltered);
            }
            // User pasted invalid text.
            catch(InvalidDataException e)
            {
                dataGridView.Show();
                // Show the invalid text, then remove all newly added rows.
                MessageDlg.ShowException(this, e);
                RemoveLastRows(dataGridView, dataGridView.RowCount - prevRowCount);
                return;
            }
            // If we have no unmatched, no multiple matches, and no filtered, we do not need to show 
            // the FilterMatchedPeptidesDlg.
            if (numUnmatched + numMultipleMatches + numFiltered == 0)
                return;
            using (var filterPeptidesDlg =
                new FilterMatchedPeptidesDlg(numMultipleMatches, numUnmatched, numFiltered,
                                             dataGridView.RowCount - prevRowCount == 1, false))
            {
                var result = filterPeptidesDlg.ShowDialog(this);
                // If the user is keeping all peptide matches, we don't need to redo the paste.
                bool keepAllPeptides = ((FilterMultipleProteinMatches ==
                                         BackgroundProteome.DuplicateProteinsFilter.AddToAll || numMultipleMatches == 0)
                                        && (Settings.Default.LibraryPeptidesAddUnmatched || numUnmatched == 0)
                                        && (Settings.Default.LibraryPeptidesKeepFiltered || numFiltered == 0));
                // If the user is filtering some peptides, or if the user clicked cancel, remove all rows added as
                // a result of the paste.
                if (result == DialogResult.Cancel || !keepAllPeptides)
                    RemoveLastRows(dataGridView, dataGridView.RowCount - prevRowCount);
                // Redo the paste with the new filter settings.
                if (result != DialogResult.Cancel && !keepAllPeptides)
                {
                    Paste(dataGridView, text, enumerateProteins, !enumerateProteins, out numUnmatched,
                        out numMultipleMatches, out numFiltered);
                    _entryCreators.Add(filterPeptidesDlg.FormSettings.EntryCreator);
                }
            }
        }

        /// <summary>
        /// Paste the clipboard text into the specified DataGridView.
        /// The clipboard text is assumed to be tab separated values.
        /// The values are matched up to the columns in the order they are displayed.
        /// </summary>
        private void Paste(DataGridView dataGridView, string text, bool enumerateProteins, bool keepAllPeptides,
            out int numUnmatched, out int numMulitpleMatches, out int numFiltered)
        {
            numUnmatched = numMulitpleMatches = numFiltered = 0;
            var columns = new DataGridViewColumn[dataGridView.Columns.Count];
            dataGridView.Columns.CopyTo(columns, 0);
            Array.Sort(columns, (a,b)=>a.DisplayIndex - b.DisplayIndex);
            HashSet<string> listPepSeqs = new HashSet<string>();
            AssociateProteinsHelper associateHelper = null;
            if (enumerateProteins)
            {
                associateHelper = new AssociateProteinsHelper(DocumentUiContainer.Document);
            }
            foreach (var values in ParseColumnarData(text))
            {
                var row = dataGridView.Rows[dataGridView.Rows.Add()];
                using (var valueEnumerator = values.GetEnumerator())
                {
                    foreach (DataGridViewColumn column in columns)
                    {
                        if (column.ReadOnly || !column.Visible)
                        {
                            continue;
                        }
                        if (!valueEnumerator.MoveNext())
                        {
                            break;
                        }
                        row.Cells[column.Index].Value = valueEnumerator.Current;
                    }
                }
                if (enumerateProteins)
                {
                    EnumerateProteins(dataGridView, row.Index, keepAllPeptides, ref numUnmatched, ref numMulitpleMatches,
                        ref numFiltered, listPepSeqs, associateHelper);
                }
            }
        }

        static IEnumerable<IList<string>> ParseColumnarData(String text)
        {
            IFormatProvider formatProvider;
            char separator;
            Type[] types;

            if (!MassListImporter.IsColumnar(text, out formatProvider, out separator, out types))
            {
                string line;
                var reader = new StringReader(text);
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }
                    yield return new[] {line};
                }
            }
            else
            {
                string line;
                var reader = new StringReader(text);
                while ((line = reader.ReadLine()) != null)
                {
                    // Avoid trimming off tabs, which will shift columns
                    line = line.Trim('\r', '\n', TextUtil.SEPARATOR_SPACE);
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }
                    yield return line.ParseDsvFields(separator); // Properly handles quoted commas etc
                }
            }
        }


        public static List<String> GetProteinNamesForPeptideSequence(String peptideSequence, SrmDocument document, out List<Protein> proteinList)
        {
            proteinList = new List<Protein>();
            var backgroundProteome = document.Settings.PeptideSettings.BackgroundProteome;
            if (backgroundProteome.IsNone)
            {
                return null;
            }
            using (var proteomeDb = backgroundProteome.OpenProteomeDb())
            {
                var digestion = backgroundProteome.GetDigestion(proteomeDb, document.Settings.PeptideSettings);
                if (digestion == null)
                {
                    return null;
                }
                proteinList = digestion.GetProteinsWithSequence(peptideSequence);
                return proteinList.ConvertAll(protein => protein.Name);
            }
        }

        private void OnCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            _statementCompletionEditBox.HideStatementCompletionForm();
        }

        private void OnLoad(object sender, EventArgs e)
        {
            // If you set this in the Designer, DataGridView has a defect that causes it to throw an
            // exception if the the cursor is positioned over the record selector column during loading.
            gridViewPeptides.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridViewProteins.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        #region Testing

        public class FastaTab : IFormView {}
        public class ProteinListTab : IFormView { }
        public class PeptideListTab : IFormView { }

        private static readonly IFormView[] TAB_PAGES =
        {
            new FastaTab(), new ProteinListTab(), new PeptideListTab()
        };

        public IFormView ShowingFormView
        {
            get
            {
                int selectedIndex = 0;
                Invoke(new Action(() => selectedIndex = GetSelectedTabIndex()));
                return TAB_PAGES[selectedIndex];
            }
        }

        private int GetSelectedTabIndex()
        {
            if (tabControl1.SelectedTab == tabPageFasta)
                return 0;
            else if (tabControl1.SelectedTab == tabPageProteinList)
                return 1;
            else if (tabControl1.SelectedTab == tabPagePeptideList)
                return 2;
            return 3;
        }

        public int PeptideRowCount
        {
            get { return gridViewPeptides.RowCount; }
        }
        
        public bool PeptideRowsContainProtein(Predicate<string> found)
        {
            var peptideRows = new DataGridViewRow[gridViewPeptides.RowCount];
            gridViewPeptides.Rows.CopyTo(peptideRows, 0);
            return peptideRows.Take(gridViewPeptides.RowCount-1).Contains(row =>
            {
                var protein = row.Cells[colPeptideProtein.Index].Value;
                return found(protein != null ? protein.ToString() : null);
            });
        }

        public bool PeptideRowsContainPeptide(Predicate<string> found)
        {
            var peptideRows = new DataGridViewRow[gridViewPeptides.RowCount];
            gridViewPeptides.Rows.CopyTo(peptideRows, 0);
            return peptideRows.Take(gridViewPeptides.RowCount-1).Contains(row =>
            {
                var peptide = row.Cells[colPeptideSequence.Index].Value;
                return found(peptide != null ? peptide.ToString() : null);
            });
        }
        
        public void ClearRows()
        {
           if(PasteFormat == PasteFormat.peptide_list)
               gridViewPeptides.Rows.Clear();
        }

        #endregion
        
        private void PasteDlg_KeyDown(object sender, KeyEventArgs e)
        {
            // This keyboard handling is necessary to get Escape and Enter keys to work correctly in this form
            // They need to generally work, but not when grid controls are in edit mode and not Enter when the
            // FASTA text box has the focus. Especially to make grid editing work as expected, it seems to be
            // necessary to not have an Accept or Cancel button on the form.
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    // Somehow a grid in edit mode doesn't end up here, if there is no Cancel button on the form
                    CancelDialog();
                    e.Handled = true;
                    break;

                case Keys.Enter:
                    // Allow the FASTA text box to have enter keys
                    if (!tbxFasta.Focused)
                    {
                        // Otherwise, OK the dialog
                        OkDialog();
                        e.Handled = true;
                    }
                    break;
            }
        }
    }

    public enum PasteFormat
    {
        none,
        fasta,
        protein_list,
        peptide_list,
    }

    public class ImportFastaHelper
    {
        public ImportFastaHelper(TextBox tbxFasta, TextBox tbxError, Panel panelError, ToolTip helpTip)
        {
            _tbxFasta = tbxFasta;
            _tbxError = tbxError;
            _panelError = panelError;
            _helpTip = helpTip;
        }

        public IdentityPath SelectedPath { get; set; }

        private readonly TextBox _tbxFasta;
        private TextBox TbxFasta { get { return _tbxFasta; } }

        private readonly TextBox _tbxError;
        private TextBox TbxError { get { return _tbxError; } }

        private readonly Panel _panelError;
        private Panel PanelError { get { return _panelError; } }

        private readonly ToolTip _helpTip;
        private ToolTip HelpTip { get { return _helpTip; } }

        public SrmDocument AddFasta(SrmDocument document, IrtStandard irtStandard, IProgressMonitor monitor,
            ref IdentityPath selectedPath, out List<PeptideGroupDocNode> newPeptideGroups, out PasteError error)
        {
            newPeptideGroups = new List<PeptideGroupDocNode>();
            var text = TbxFasta.Text;
            if (text.Length == 0)
            {
                error = null;
                return document;
            }
            if (!text.StartsWith(@">"))
            {
                error = new PasteError
                {
                    Message = Resources.ImportFastaHelper_AddFasta_This_must_start_with____,
                    Column = 0,
                    Length = 1,
                    Line = 0,
                };
                return null;
            }
            string[] lines = text.Split('\n');
            int lastNameLine = -1;
            int aa = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith(@">"))
                {
                    if (line.Trim().Length == 1)
                    {
                        error = new PasteError
                        {
                            Message = Resources.ImportFastaHelper_AddFasta_There_is_no_name_for_this_protein,
                            Column = 0,
                            Line = i,
                            Length = 1
                        };
                        return null;
                    }
                    if ((error = CheckSequence(aa, lastNameLine, lines)) != null)
                        return null;
                    lastNameLine = i;
                    aa = 0;
                    continue;
                }

                for (int column = 0; column < line.Length; column++)
                {
                    char c = line[column];
                    if (AminoAcid.IsExAA(c))
                        aa++;
                    else if (!char.IsWhiteSpace(c) && c != '*')
                    {
                        error = new PasteError
                        {
                            Message =
                                string.Format(Resources.ImportFastaHelper_AddFasta___0___is_not_a_capital_letter_that_corresponds_to_an_amino_acid_, c),
                            Column = column,
                            Line = i,
                            Length = 1,
                        };
                        return null;
                    }
                }
            }

            if ((error = CheckSequence(aa, lastNameLine, lines)) != null)
                return null;

            var importer = new FastaImporter(document, irtStandard);
            try
            {
                var reader = new StringReader(TbxFasta.Text);
                IdentityPath to = selectedPath;
                IdentityPath firstAdded, nextAdd;
                newPeptideGroups = importer.Import(reader, monitor, -1).ToList();
                document = document.AddPeptideGroups(newPeptideGroups, false, to, out firstAdded, out nextAdd);
                selectedPath = firstAdded;
            }
            catch (Exception exception)
            {
                error = new PasteError
                {
                    Message = Resources.ImportFastaHelper_AddFasta_An_unexpected_error_occurred__ + exception.Message + @" (" + exception.GetType() + @")"
                };
                return null;
            }
            return document;
        }

        public void ShowFastaError(PasteError pasteError)
        {
            PanelError.Visible = true;
            if (pasteError == null)
            {
                ClearFastaError();
                return;
            }

            ShowFastaError(pasteError.Message);

            TbxFasta.Height -= TbxFasta.Bottom - PanelError.Top + 8;
            TbxFasta.SelectionStart = Math.Max(0, TbxFasta.GetFirstCharIndexFromLine(pasteError.Line) + pasteError.Column);
            TbxFasta.SelectionLength = Math.Min(pasteError.Length, TbxFasta.Text.Length - TbxFasta.SelectionStart);
            TbxFasta.Focus();
        }

        public void ShowFastaError(string errorMsg)
        {
            PanelError.Visible = true;
            if (string.IsNullOrEmpty(errorMsg))
            {
                ClearFastaError();
                return;
            }
            TbxError.BackColor = Color.Red;
            TbxError.ForeColor = Color.White;
            TbxError.Text = errorMsg;
            TbxError.Visible = true;
            if (HelpTip != null)
            {
                // In case message is long, make it possible to see in a tip
                HelpTip.SetToolTip(TbxError, errorMsg);
            }
        }

        public void ClearFastaError()
        {
            TbxFasta.Height = PanelError.Bottom - TbxFasta.Top;
            TbxError.Text = string.Empty;
            TbxError.Visible = false;
            PanelError.Visible = false;
        }

        private static PasteError CheckSequence(int aa, int lastNameLine, string[] lines)
        {
            if (aa == 0 && lastNameLine >= 0)
            {
                return new PasteError
                {
                    Message = Resources.ImportFastaHelper_CheckSequence_There_is_no_sequence_for_this_protein,
                    Column = 0,
                    Line = lastNameLine,
                    Length = lines[lastNameLine].Length
                };
            }
            return null;
        }

        public static SrmDocument HandleEmptyPeptideGroups(IWin32Window parent, int emptyPeptideGroups, SrmDocument docCurrent, AuditLogEntryCreatorList entryCreatorList = null)
        {
            switch (AskWhetherToKeepEmptyProteins(parent, emptyPeptideGroups, entryCreatorList))
            {
                case true:
                    return docCurrent;
                case false:
                    return ImportPeptideSearch.RemoveProteinsByPeptideCount(docCurrent, 1);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Display the dialog that says "This operation has added X new proteins with no peptides meeting your filter criteria".
        /// </summary>
        /// <returns>
        /// null if the user cancels, true/false for whether the user says whether they want to keep empty proteins.
        /// Also returns true if there were so many empty peptide groups that they have already been removed.
        /// </returns>
        public static bool? AskWhetherToKeepEmptyProteins(IWin32Window parent, int numberOfEmptyPeptideGroups, AuditLogEntryCreatorList entryCreatorList = null)
        {
            if (numberOfEmptyPeptideGroups > FastaImporter.MaxEmptyPeptideGroupCount)
            {
                MessageDlg.Show(parent, string.Format(Resources.SkylineWindow_ImportFasta_This_operation_discarded__0__proteins_with_no_peptides_matching_the_current_filter_settings_, numberOfEmptyPeptideGroups));
                return true;
            }
            else if (numberOfEmptyPeptideGroups > 0)
            {
                using (var dlg = new EmptyProteinsDlg(numberOfEmptyPeptideGroups))
                {
                    if (dlg.ShowDialog(parent) == DialogResult.Cancel)
                        return null;
                    if(entryCreatorList != null)
                        entryCreatorList.Add(dlg.FormSettings.EntryCreator);
                    // Remove all empty proteins, if requested by the user.
                    return dlg.IsKeepEmptyProteins;
                }
            }
            return true;
        }
    }
}
