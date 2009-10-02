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
using System.Windows.Forms;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Proteome;

namespace pwiz.Skyline.EditUI
{
    public partial class PasteDlg : Form
    {
        private readonly StatementCompletionTextBox _statementCompletionEditBox;
        private bool _noErrors;

        public PasteDlg(IDocumentUIContainer documentUiContainer)
        {
            InitializeComponent();
            DocumentUiContainer = documentUiContainer;
            if (GetBackgroundProteome(documentUiContainer.DocumentUI).IsNone)
            {
                tabPageProteinList.Visible = false;
            }
            _statementCompletionEditBox = new StatementCompletionTextBox(documentUiContainer)
                                              {
                                                  MatchTypes = ProteinMatchType.name | ProteinMatchType.description
                                              };
            _statementCompletionEditBox.SelectionMade += statementCompletionEditBox_SelectionMade;
            gridViewProteins.DataGridViewKey += gridViewProteins_DataGridViewKey;
            gridViewPeptides.DataGridViewKey += gridViewPeptides_DataGridViewKey;
        }

        void gridViewPeptides_DataGridViewKey(object sender, KeyEventArgs e)
        {
            _statementCompletionEditBox.OnKeyPreview(sender, e);
        }

        void gridViewProteins_DataGridViewKey(object sender, KeyEventArgs e)
        {
            _statementCompletionEditBox.OnKeyPreview(sender, e);
        }

        void statementCompletionEditBox_SelectionMade(StatementCompletionItem statementCompletionItem)
        {
            if (tabControl1.SelectedTab == tabPageProteinList)
            {
                _statementCompletionEditBox.TextBox.Text = statementCompletionItem.ProteinName;
                gridViewProteins.EndEdit();
            }
            else if (tabControl1.SelectedTab == tabPagePeptideList)
            {
                _statementCompletionEditBox.TextBox.Text = statementCompletionItem.Peptide;
                if (gridViewPeptides.CurrentRow != null)
                {
                    gridViewPeptides.CurrentRow.Cells[colPeptideProtein.Index].Value = statementCompletionItem.ProteinName;
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

        public SrmTreeNode SelectedTreeNode { get; set; }

        public void ShowError(PasteError pasteError)
        {
            _noErrors = false;
            panelError.Visible = true;
            if (pasteError == null)
            {
                tbxError.Text = "";
                tbxError.Visible = false;
                return;
            }
            tbxError.BackColor = Color.Red;
            tbxError.Text = pasteError.Message;
        }
        public void ShowNoErrors()
        {
            _noErrors = true;
            panelError.Visible = true;
            tbxError.Text = "No errors";
            tbxError.BackColor = Color.LightGreen;
        }

        public void HideNoErrors()
        {
            if (!_noErrors)
            {
                return;
            }
            panelError.Visible = false;
        }

        private void btnValidate_Click(object sender, EventArgs e)
        {
            var document = GetNewDocument(DocumentUiContainer.Document);
            if (document != null)
            {
                ShowNoErrors();
            }
        }

        private SrmDocument GetNewDocument(SrmDocument document)
        {
            if ((document = AddFasta(document)) == null)
            {
                return null;
            }
            if ((document = AddProteins(document)) == null)
            {
                return null;
            }
            if ((document = AddPeptides(document)) == null)
            {
                return null;
            }
            return document;
        }

        private void ShowProteinError(PasteError pasteError)
        {
            tabControl1.SelectedTab = tabPageProteinList;
            ShowError(pasteError);
            gridViewProteins.CurrentCell = gridViewProteins.Rows[pasteError.Line].Cells[colProteinName.Index];
        }

        private void ShowPeptideError(PasteError pasteError)
        {
            tabControl1.SelectedTab = tabPagePeptideList;
            ShowError(pasteError);
            gridViewPeptides.CurrentCell = gridViewPeptides.Rows[pasteError.Line].Cells[pasteError.Column];
        }

        private SrmDocument AddPeptides(SrmDocument document)
        {
            bool missingProteinName = false;
            bool anyProteinName = false;
            var backgroundProteome = GetBackgroundProteome(document);
            for (int i = 0; i < gridViewPeptides.Rows.Count; i++)
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
                    ShowPeptideError(new PasteError { Column = colPeptideSequence.Index, Line = i, Message = "The peptide sequence cannot be blank."});
                    return null;
                }
                if (!FastaSequence.IsExSequence(peptideSequence))
                {
                    ShowPeptideError(new PasteError { Column = colPeptideSequence.Index, Line = i, Message = "This peptide sequence contains invalid characters." });
                    return null;
                }
                PeptideGroupDocNode peptideGroupDocNode;
                if (string.IsNullOrEmpty(proteinName))
                {
                    if (anyProteinName)
                    {
                        ShowPeptideError(new PasteError
                                             {
                                                 Column = colPeptideProtein.Index, Line = i, Message = "This protein name is missing"
                                             });
                        return null;
                    }
                    missingProteinName = true;
                    peptideGroupDocNode = GetLastPeptideGroupDocNode(document);
                    if (!IsPeptideListDocNode(peptideGroupDocNode))
                    {
                        peptideGroupDocNode = null;
                    }
                }
                else
                {
                    if (missingProteinName)
                    {
                        ShowPeptideError(new PasteError
                                             {
                                                 Column = colPeptideProtein.Index,
                                                 Line = i,
                                                 Message = "Earlier rows did not specify a protein name, but this row has a protein name."
                                             });
                        return null;
                    }
                    anyProteinName = true;
                    peptideGroupDocNode = FindPeptideGroupDocNode(document, proteinName);
                }
                if (peptideGroupDocNode == null)
                {
                    if (proteinName == null)
                    {
                        peptideGroupDocNode = new PeptideGroupDocNode(new PeptideGroup(), SkylineWindow.GetPeptideGroupId(document, true), null, new PeptideDocNode[0]);
                    }
                    else
                    {
                        PeptideGroup peptideGroup = backgroundProteome.GetFastaSequence(proteinName);
                        if (peptideGroup == null)
                        {
                            peptideGroup = new PeptideGroup();
                        }
                        peptideGroupDocNode = new PeptideGroupDocNode(peptideGroup, proteinName, peptideGroup.Description, new PeptideDocNode[0]);
                    }
                    document = (SrmDocument) document.Add(peptideGroupDocNode);
                }
                var children = new List<PeptideDocNode>();
                foreach (PeptideDocNode peptideDocNode in peptideGroupDocNode.Children)
                {
                    children.Add(peptideDocNode);
                }

                var fastaSequence = peptideGroupDocNode.PeptideGroup as FastaSequence;
                int missedCleavages = document.Settings.PeptideSettings.Enzyme.CountCleavagePoints(peptideSequence);
                if (fastaSequence != null)
                {
                    var peptideDocNode = fastaSequence.CreatePeptideDocNode(document.Settings, peptideSequence);
                    if (peptideDocNode == null)
                    {
                        ShowPeptideError(new PasteError
                                             {
                                                 Column = colPeptideSequence.Index,
                                                 Line = i,
                                                 Message = "This peptide sequence was not found in the protein sequence"
                                             });
                        return null;
                    }
                    children.Add(peptideDocNode);
                }
                else
                {
                    var newPeptide = new Peptide(null, peptideSequence, null, null, missedCleavages);
                    children.Add(new PeptideDocNode(newPeptide, new TransitionGroupDocNode[0]).ChangeSettings(document.Settings, SrmSettingsDiff.ALL));
                }
                var newPeptideGroupDocNode = new PeptideGroupDocNode(peptideGroupDocNode.PeptideGroup, peptideGroupDocNode.Note, peptideGroupDocNode.Name, peptideGroupDocNode.Description, children.ToArray(), true);
                document = (SrmDocument) document.ReplaceChild(newPeptideGroupDocNode);
            }
            return document;
        }

        private static bool IsPeptideListDocNode(PeptideGroupDocNode peptideGroupDocNode)
        {
            return peptideGroupDocNode != null && peptideGroupDocNode.IsPeptideList;
        }

        private SrmDocument AddProteins(SrmDocument document)
        {
            var backgroundProteome = GetBackgroundProteome(document);
            for (int i = 0; i < gridViewProteins.Rows.Count; i++)
            {
                var row = gridViewProteins.Rows[i];
                var proteinName = Convert.ToString(row.Cells[colProteinName.Index].Value);
                if (String.IsNullOrEmpty(proteinName))
                {
                    continue;
                }
                var fastaSequence = backgroundProteome.GetFastaSequence(proteinName);
                if (fastaSequence == null)
                {
                    ShowProteinError(
                        new PasteError
                        {
                                             Line = i,
                                             Message = "This protein was not found in the background proteome database."
                        });
                    return null;
                }
                document = (SrmDocument) document.Add(
                    new PeptideGroupDocNode(fastaSequence, fastaSequence.Name, fastaSequence.Description, 
                    fastaSequence.CreatePeptideDocNodes(document.Settings, true).ToArray()));
            }
            return document;
        }

        private SrmDocument AddFasta(SrmDocument document)
        {
            var text = tbxFasta.Text;
            if (text.Length == 0)
            {
                return document;
            }
            if (!text.StartsWith(">"))
            {
                ShowFastaError(new PasteError
                {
                    Message = "This must start with '>'",
                    Column = 0,
                    Length = 1,
                    Line = 0,
                });
                return null;
            }
            string[] lines = text.Split('\n');
            int aa = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith(">"))
                {
                    int lineWithMissingProteinSequence = -1;
                    if (i == lines.Length - 1)
                    {
                        lineWithMissingProteinSequence = i;
                    }
                    else if (aa == 0)
                    {
                        lineWithMissingProteinSequence = i - 1;
                    }
                    if (lineWithMissingProteinSequence >= 0)
                    {
                        ShowFastaError(new PasteError
                        {
                            Message = "There is no sequence for this protein",
                            Column = 0,
                            Line = lineWithMissingProteinSequence,
                            Length = lines[lineWithMissingProteinSequence].Length
                        });
                        return null;
                    }
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
                        ShowFastaError(new PasteError
                        {
                            Message =
                                string.Format("'{0}' is not a capital letter that corresponds to an amino acid.", c),
                            Column = column,
                            Line = i,
                            Length = 1,
                        });
                        return null;
                    }
                }
            }
            var importer = new FastaImporter(DocumentUiContainer.DocumentUI, false);
            try
            {
                foreach (var peptideGroupDocNode in importer.Import(new StringReader(tbxFasta.Text)))
                {
                    document = (SrmDocument) document.Add(peptideGroupDocNode);
                }
            }
            catch (Exception exception)
            {
                Console.Out.WriteLine(exception);
                ShowFastaError(new PasteError
                              {
                                  Message =
                                      "An unexpected error occurred: " + exception.Message + " (" + exception.GetType() +
                                      ")"
                              });
                return null;
            }
            return document;
        }

        private static PeptideGroupDocNode FindPeptideGroupDocNode(SrmDocument document, String name)
        {
            foreach (PeptideGroupDocNode peptideGroupDocNode in document.PeptideGroups)
            {
                if (peptideGroupDocNode.Name == name)
                {
                    return peptideGroupDocNode;
                }
            }
            return null;
        }

        private static PeptideGroupDocNode GetLastPeptideGroupDocNode(SrmDocument document)
        {
            PeptideGroupDocNode lastPeptideGroupDocuNode = null;
            foreach (PeptideGroupDocNode peptideGroupDocNode in document.PeptideGroups)
            {
                lastPeptideGroupDocuNode = peptideGroupDocNode;
            }
            return lastPeptideGroupDocuNode;
        }

        private void ShowFastaError(PasteError pasteError)
        {
            ShowError(pasteError);
            tabControl1.SelectedTab = tabPageFasta;
            tbxFasta.SelectionStart = tbxFasta.GetFirstCharIndexFromLine(pasteError.Line) + pasteError.Column;
            tbxFasta.SelectionLength = pasteError.Length;
            tbxFasta.Focus();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
            DialogResult = DialogResult.Cancel;
        }

        private static void OnDocumentUIChanged(object sender, DocumentChangedEventArgs e)
        {
            
        }

        public PasteFormat PasteFormat {
            get
            {
                return GetPasteFormat(tabControl1.SelectedTab);
            }
            set
            {
                tabControl1.SelectedTab = GetTabPage(value);
            }
        }

        private void tabControl1_Selecting(object sender, TabControlCancelEventArgs e)
        {
            // TODO: prompt user if they will lose data on current tab
        }

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
            var fastaSequence = GetBackgroundProteome(DocumentUiContainer.DocumentUI).GetFastaSequence(proteinName);
            if (fastaSequence == null)
            {
                row.Cells[colProteinDescription.Index].Value = null;
                row.Cells[colProteinSequence.Index].Value = null;
            }
            else
            {
                row.Cells[colProteinDescription.Index].Value = fastaSequence.Description;
                row.Cells[colProteinSequence.Index].Value = fastaSequence.Sequence;
            }
        }

        private void gridViewPeptides_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            HideNoErrors();
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }
            var row = gridViewPeptides.Rows[e.RowIndex];
            var proteinName = Convert.ToString(row.Cells[colPeptideProtein.Index].Value);
            var peptideSequence = Convert.ToString(row.Cells[colPeptideSequence.Index].Value);
            if (string.IsNullOrEmpty(peptideSequence) && string.IsNullOrEmpty(proteinName))
            {
                gridViewPeptides.Rows.Remove(row);
                return;
            }
            var column = gridViewPeptides.Columns[e.ColumnIndex];
            if (column == colPeptideSequence)
            {
                if (String.IsNullOrEmpty(proteinName) && !String.IsNullOrEmpty(peptideSequence))
                {
                    row.Cells[colPeptideProtein.Index].Value = GetProteinNameForPeptideSequence(peptideSequence);
                }
                return;
            }
            if (column != colPeptideProtein)
            {
                return;
            }
            var fastaSequence = GetBackgroundProteome(DocumentUiContainer.Document).GetFastaSequence(proteinName);
            if (fastaSequence == null)
            {
                // Sometimes the protein name in the background proteome will have an extra "|" on the end.
                // In that case, update the name of the protein to match the one in the database.
                fastaSequence = GetBackgroundProteome(DocumentUiContainer.Document).GetFastaSequence(proteinName + "|");
                if (fastaSequence != null)
                {
                    row.Cells[colPeptideProtein.Index].Value = fastaSequence.Name;
                }
            }
            if (fastaSequence == null)
            {
                row.Cells[colPeptideProteinDescription.Index].Value = null;
            }
            else
            {
                row.Cells[colPeptideProteinDescription.Index].Value = fastaSequence.Description;
            }
        }

        private void gridViewProteins_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            _statementCompletionEditBox.Attach(gridViewProteins.EditingControl as TextBox);
        }

        private void gridViewPeptides_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            _statementCompletionEditBox.Attach(gridViewPeptides.EditingControl as TextBox);
        }

        private void btnInsert_Click(object sender, EventArgs e)
        {
            bool error = false;
            Program.MainWindow.ModifyDocument("Insert", 
                document =>
                {
                    var newDocument = GetNewDocument(document);
                    if (newDocument == null)
                    {
                        error = true;
                        return document;
                    }
                    return newDocument;
                });
            if (error)
            {
                return;
            }
            Close();
        }

        private void tbxFasta_TextChanged(object sender, EventArgs e)
        {
            HideNoErrors();
        }

        private void gridViewPeptides_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.ColumnIndex == colPeptideSequence.Index)
            {
                _statementCompletionEditBox.MatchTypes = ProteinMatchType.sequence;
            }
            else
            {
                _statementCompletionEditBox.MatchTypes = 0;
            }
        }

        private void gridViewProteins_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.ColumnIndex == colProteinName.Index)
            {
                _statementCompletionEditBox.MatchTypes = ProteinMatchType.name | ProteinMatchType.description;
            }
            else
            {
                _statementCompletionEditBox.MatchTypes = 0;
            }
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

        private void PasteProteins()
        {
            TextReader reader = new StringReader(Clipboard.GetText());
            String line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (String.IsNullOrEmpty(line))
                {
                    continue;
                }
                var row = gridViewProteins.Rows[gridViewProteins.Rows.Add()];
                row.Cells[colProteinName.Index].Value = line;
            }
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

        private void PastePeptides()
        {
            TextReader reader = new StringReader(Clipboard.GetText());
            String line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }
                String peptide, protein;
                ParsePeptideProtein(line, out peptide, out protein);
                var row = gridViewPeptides.Rows[gridViewPeptides.Rows.Add()];
                row.Cells[colPeptideProtein.Index].Value = protein;
                row.Cells[colPeptideSequence.Index].Value = peptide;
            }
        }

        private void ParsePeptideProtein(String line, out string peptide, out string protein)
        {
            String[] parts = line.Split(new[] {' ', '\t'}, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                peptide = parts[0];
                protein = null;
                return;
            }
            if (parts.Length > 2)
            {
                try
                {
                    // If the first column successfully parses as a number, then skip over it
                    int.Parse(parts[0]);
                    parts = new[] {parts[1], parts[2]};
                }
                catch
                {
                    // ignore exception
                }
            }
            bool peptideFirst = colPeptideSequence.DisplayIndex < colPeptideProtein.DisplayIndex;
            peptide = peptideFirst ? parts[0] : parts[1];
            protein = peptideFirst ? parts[1] : parts[0];
        }

        private String GetProteinNameForPeptideSequence(String peptideSequence)
        {
            var document = DocumentUiContainer.Document;
            var backgroundProteome = document.Settings.PeptideSettings.BackgroundProteome;
            if (backgroundProteome.IsNone)
            {
                return null;
            }
            var digestion = backgroundProteome.GetDigestion(document.Settings.PeptideSettings);
            if (digestion == null)
            {
                return null;
            }
            var proteins = digestion.GetProteinsWithSequence(peptideSequence);
            if (proteins.Count != 1)
            {
                return null;
            }
            return proteins[0].Name;
        }

        private void gridViewPeptides_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            _statementCompletionEditBox.HideStatementCompletionForm();
        }

        private void gridViewProteins_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            _statementCompletionEditBox.HideStatementCompletionForm();
        }
    }

    public enum PasteFormat
    {
        none,
        fasta,
        protein_list,
        peptide_list,
    }
}
