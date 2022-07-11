/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Menus
{
    public partial class EditMenu : SkylineControl
    {
        public EditMenu(SkylineWindow skylineWindow) : base(skylineWindow)
        {
            InitializeComponent();
            DropDownItems = ImmutableList.ValueOf(editToolStripMenuItem.DropDownItems.Cast<ToolStripItem>());
        }

        public IEnumerable<ToolStripItem> DropDownItems { get; }

        public ToolStripMenuItem UndoMenuItem => undoMenuItem;

        public ToolStripMenuItem RedoMenuItem => redoMenuItem;

        #region Clipboard Operations
        public ToolStripMenuItem CutMenuItem => cutMenuItem;
        public ToolStripMenuItem CopyMenuItem => copyMenuItem;
        public ToolStripMenuItem PasteMenuItem => pasteMenuItem;
        public ToolStripItem DeleteMenuItem => deleteMenuItem;
        public ToolStripMenuItem SelectAllMenuItem => selectAllMenuItem;

        private void cutMenuItem_Click(object sender, EventArgs e) { Cut(); }
        public void Cut()
        {
            if (SkylineWindow.StatementCompletionAction(textBox => textBox.Cut()))
                return;

            Copy();
            EditDelete();
        }
        private bool StatementCompletionAction(Action<TextBox> act)
        {
            return SkylineWindow.StatementCompletionAction(act);
        }
        private void copyMenuItem_Click(object sender, EventArgs e) { Copy(); }

        public void Copy()
        {
            if (StatementCompletionAction(textBox => textBox.Copy()) || SequenceTree.SelectedNodes.Count < 0)
                return;

            List<TreeNode> sortedNodes = new List<TreeNode>();
            int shallowestLevel = int.MaxValue;
            foreach (TreeNodeMS node in SequenceTree.SelectedNodes)
            {
                shallowestLevel = Math.Min(shallowestLevel, node.Level);
                sortedNodes.Add(node);
            }
            sortedNodes.Sort(CompareNodeBounds);

            StringBuilder htmlSb = new StringBuilder();
            StringBuilder textSb = new StringBuilder();

            TreeNodeMS prev = null;
            foreach (TreeNodeMS node in sortedNodes)
            {
                IClipboardDataProvider provider = node as IClipboardDataProvider;
                if (provider == null)
                    continue;

                DataObject data = provider.ProvideData();
                int levels = node.Level - shallowestLevel;
                int lineBreaks = GetLineBreakCount(node, prev);
                string providerHtml = (string)data.GetData(DataFormats.Html);
                if (providerHtml != null)
                    AppendClipboardText(htmlSb, new HtmlFragment(providerHtml).Fragment,
                        // ReSharper disable LocalizableElement
                        "<br>\r\n", "&nbsp;&nbsp;&nbsp;&nbsp;", levels, lineBreaks);
                // ReSharper restore LocalizableElement
                string providerText = (string)data.GetData(@"Text");
                if (providerText != null)
                    // ReSharper disable LocalizableElement
                    AppendClipboardText(textSb, providerText, "\r\n", "    ", levels, lineBreaks);
                // ReSharper restore LocalizableElement

                prev = node;
            }
            DataObject dataObj = new DataObject();
            if (htmlSb.Length > 0)
                dataObj.SetData(DataFormats.Html, HtmlFragment.ClipBoardText(htmlSb.AppendLine().ToString()));
            if (textSb.Length > 0)
                dataObj.SetData(DataFormats.Text, textSb.AppendLine().ToString());

            bool selectionContainsProteins = SequenceTree.SelectedDocNodes.Contains(node =>
                node is PeptideGroupDocNode);

            var docCopy = DocumentUI.RemoveAllBut(SequenceTree.SelectedDocNodes);
            docCopy = docCopy.ChangeMeasuredResults(null);
            var stringWriter = new XmlStringWriter();
            using (var writer = new XmlTextWriter(stringWriter) { Formatting = Formatting.Indented })
            {
                XmlSerializer ser = new XmlSerializer(typeof(SrmDocument));
                ser.Serialize(writer, docCopy);
            }

            var sbData = new StringBuilder();
            sbData.Append(@"proteins-selected=").Append(selectionContainsProteins).AppendLine();
            sbData.AppendLine();
            sbData.Append(stringWriter);
            dataObj.SetData(ClipboardEx.SKYLINE_FORMAT, sbData.ToString());

            ClipboardHelper.SetClipboardData(SkylineWindow, dataObj, false);
        }
        private void deleteMenuItem_Click(object sender, EventArgs e) { EditDelete(); }
        public void EditDelete()
        {
            string undoText = Resources.SkylineWindow_EditDelete_items;
            if (SequenceTree.SelectedNodes.Count == 1)
            {
                SrmTreeNode node = SequenceTree.SelectedNode as SrmTreeNode;
                if (node != null)
                    undoText = node.Text;
            }

            var removedNodes = new List<DocNode>();
            ModifyDocument(string.Format(Resources.SkylineWindow_EditDelete_Delete__0__, undoText), doc =>
            {
                bool canSynchSiblings = !doc.Settings.PeptideSettings.Quantification.SimpleRatios;
                var listRemoveParams = new List<RemoveParams>();    // Keep removals in order
                var dictRemove = new Dictionary<int, RemoveParams>();   // Minimize removal operations
                var setRemove = new HashSet<int>(); // Keep track of what is being removed
                foreach (TreeNodeMS nodeTree in SequenceTree.SelectedNodes)
                {
                    var node = nodeTree as SrmTreeNode;
                    if (node == null)
                        continue;

                    int indexRemove = node.Model.Id.GlobalIndex;
                    setRemove.Add(indexRemove);

                    IdentityPath path = node.Path;

                    IdentityPath parentPath = path.Parent;
                    int indexParent = parentPath.IsRoot
                        ? -1
                        : parentPath.GetIdentity(parentPath.Length - 1).GlobalIndex;

                    // If parent is being removed, ignore this element
                    if (setRemove.Contains(indexParent))
                        continue;
                    removedNodes.Add(node.Model);
                    RemoveParams removeParams;
                    if (!dictRemove.TryGetValue(indexParent, out removeParams))
                    {
                        removeParams = new RemoveParams(parentPath);
                        listRemoveParams.Add(removeParams);
                        dictRemove.Add(indexParent, removeParams);
                    }
                    removeParams.RemoveIds.Add(indexRemove);
                }

                foreach (var removeParams in listRemoveParams)
                {
                    var nodeParent = doc.FindNode(removeParams.ParentPath);
                    if (canSynchSiblings && nodeParent is TransitionGroupDocNode)
                        doc = (SrmDocument)doc.RemoveAllSynched(removeParams.ParentPath.Parent, removeParams.ParentPath.Child, removeParams.RemoveIds);
                    else if (nodeParent != null)
                        doc = (SrmDocument)doc.RemoveAll(removeParams.ParentPath, removeParams.RemoveIds);
                }
                return doc;
            }, docPair => SkylineWindow.CreateDeleteNodesEntry(docPair, removedNodes.Select(node => AuditLogEntry.GetNodeName(docPair.OldDoc, node).ToString()), removedNodes.Count));
        }

        private class RemoveParams
        {
            public RemoveParams(IdentityPath parentPath)
            {
                ParentPath = parentPath;
                RemoveIds = new HashSet<int>();
            }

            public IdentityPath ParentPath { get; private set; }
            public ICollection<int> RemoveIds { get; private set; }
        }

        private static int CompareNodeBounds(TreeNode x, TreeNode y)
        {
            return Comparer<int>.Default.Compare(x.Bounds.Top, y.Bounds.Top);
        }

        private int GetLineBreakCount(TreeNodeMS curNode, TreeNodeMS prevNode)
        {
            int count = 0;
            if (prevNode != null)
                count++;
            if (curNode == null || prevNode == null)
                return count;
            TreeNodeMS nodeParent = curNode;
            while (nodeParent != null)
            {
                if (nodeParent == prevNode)
                    return count;
                nodeParent = (TreeNodeMS)nodeParent.Parent;
            }
            TreeNodeMS nodeVisible = curNode;
            while (nodeVisible != prevNode)
            {
                if (!SequenceTree.SelectedNodes.Contains(nodeVisible) && nodeVisible.Level < curNode.Level)
                    return count + 1;
                nodeVisible = (TreeNodeMS)nodeVisible.PrevVisibleNode;
                if (nodeVisible == null)
                    return count;
            }
            return count;
        }

        private static void AppendClipboardText(StringBuilder sb, string text, string lineSep, string indent, int levels, int lineBreaks)
        {
            for (int i = 0; i < lineBreaks; i++)
                sb.Append(lineSep);
            for (int i = 0; i < levels; i++)
                sb.Append(indent);
            sb.Append(text);
        }

        private void pasteMenuItem_Click(object sender, EventArgs e) { Paste(); }
        public void Paste()
        {
            if (StatementCompletionAction(textBox => textBox.Paste()))
                return;

            string dataObjectSkyline;

            try
            {
                dataObjectSkyline = (string)ClipboardEx.GetData(ClipboardEx.SKYLINE_FORMAT);
            }
            catch (ExternalException)
            {
                MessageDlg.Show(SkylineWindow, ClipboardHelper.GetPasteErrorMessage());
                return;
            }

            if (dataObjectSkyline != null)
            {
                SrmTreeNode nodePaste = SequenceTree.SelectedNode as SrmTreeNode;

                bool pasteToPeptideList = false;

                if (dataObjectSkyline.Substring(0, dataObjectSkyline.IndexOf('\r')).Equals(@"proteins-selected=False"))
                {
                    if (nodePaste != null)
                        pasteToPeptideList = !(nodePaste.Path.GetIdentity((int)SrmDocument.Level.MoleculeGroups) is FastaSequence);
                }

                IdentityPath selectPath = null;

                try
                {
                    IdentityPath nextAdd;
                    ModifyDocument(string.Format(Resources.SkylineWindow_Paste_Paste__0__, (pasteToPeptideList ? Resources.SkylineWindow_Paste_peptides : Resources.SkylineWindow_Paste_proteins)), doc =>
                        doc.ImportDocumentXml(new StringReader(dataObjectSkyline.Substring(dataObjectSkyline.IndexOf('<'))),
                            null,
                            MeasuredResults.MergeAction.remove,
                            false,
                            SkylineWindow.FindSpectralLibrary,
                            Settings.Default.StaticModList,
                            Settings.Default.HeavyModList,
                            nodePaste != null ? nodePaste.Path : null,
                            out selectPath,
                            out nextAdd,
                            pasteToPeptideList), docPair => AuditLogEntry.DiffDocNodes(MessageType.pasted_targets, docPair));
                }
                catch (Exception)
                {
                    MessageDlg.Show(SkylineWindow, Resources.SkylineWindow_Paste_Failed_reading_Skyline_document_from_the_clipboard_);
                    return;
                }

                if (selectPath != null)
                    SequenceTree.SelectedPath = selectPath;
            }
            else
            {
                string text;
                try
                {
                    text = ClipboardEx.GetText(TextDataFormat.CommaSeparatedValue);
                    if (string.IsNullOrEmpty(text))
                    {
                        text = ClipboardEx.GetText().Trim();
                    }
                }
                catch (Exception)
                {
                    MessageDlg.Show(SkylineWindow, ClipboardHelper.GetPasteErrorMessage());
                    return;
                }
                try
                {
                    Paste(text);
                }
                catch (Exception x)
                {
                    MessageDlg.ShowException(SkylineWindow, x);
                }
            }
        }
        public void Paste(string text)
        {
            bool peptideList = false;
            Type[] columnTypes;
            IFormatProvider formatProvider;
            char separator;

            // Check for a FASTA header
            if (text.StartsWith(@">"))
            {
                // Make sure there is sequence information
                string[] lines = text.Split('\n');
                int aa = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.StartsWith(@">"))
                    {
                        if (i > 0 && aa == 0)
                        {
                            throw new InvalidDataException(
                                string.Format(Resources.SkylineWindow_Paste_Empty_sequence_found_at_line__0__, i + 1));
                        }
                        aa = 0;
                        continue;
                    }

                    foreach (char c in line)
                    {
                        if (AminoAcid.IsExAA(c))
                            aa++;
                        else if (!char.IsWhiteSpace(c) && c != '*')
                        {
                            throw new InvalidDataException(
                                string.Format(Resources.SkylineWindow_Unexpected_character__0__found_on_line__1__, c,
                                    i + 1));
                        }
                    }
                }
            }
            else
            {
                var inputType =
                    SmallMoleculeTransitionListCSVReader.IsPlausibleSmallMoleculeTransitionList(text, Document.Settings, Program.ModeUI )
                        ? SrmDocument.DOCUMENT_TYPE.small_molecules : SrmDocument.DOCUMENT_TYPE.proteomic;
                
                // Perhaps it's a small molecule list with headers
                if (inputType == SrmDocument.DOCUMENT_TYPE.small_molecules && 
                    MassListImporter.IsColumnar(text, out formatProvider, out separator, out columnTypes))
                {
                    SkylineWindow.ImportMassList(new MassListInputs(text, formatProvider, separator),
                        Resources.SkylineWindow_Paste_Paste_transition_list, false, inputType);
                }
                // If the text contains numbers, see if it can be imported as a mass list.
                // It is definitely not a sequence, if it has numbers.  Whereas, sequences do
                // allow internal white space including tabs.
                else if (MassListImporter.IsColumnar(
                    Transition.StripChargeIndicators(text, TransitionGroup.MIN_PRECURSOR_CHARGE,
                        TransitionGroup.MAX_PRECURSOR_CHARGE),
                    out formatProvider, out separator, out columnTypes))
                {
                    // If no numeric type is found, try the second line.  The first may be
                    // a header row.
                    if (!MassListImporter.HasNumericColumn(columnTypes))
                    {
                        int endLine = text.IndexOf('\n');
                        if (endLine != -1)
                        {
                            MassListImporter.IsColumnar(text.Substring(endLine + 1),
                                out formatProvider, out separator, out columnTypes);
                        }
                    }

                    if (MassListImporter.HasNumericColumn(columnTypes))
                    {
                        SkylineWindow.ImportMassList(new MassListInputs(text, formatProvider, separator),
                            Resources.SkylineWindow_Paste_Paste_transition_list, false, inputType);
                    }
                    // Handle unusual corner case where data is found to be columnar and contains numbers, 
                    // but first line is missing
                    else if (columnTypes.Length == 0)
                    {
                        throw new InvalidDataException(Resources
                            .CopyPasteTest_DoTest_Could_not_read_the_pasted_transition_list___Transition_list_must_be_in_separated_columns_and_cannot_contain_blank_lines_);
                    }
                    else if (columnTypes.Length <= 3 && columnTypes[columnTypes.Length - 1] != typeof(FastaSequence)
                    ) // Name, Description, Sequence
                    {
                        var message = TextUtil.LineSeparate(Resources.SkylineWindow_Paste_Protein_sequence_not_found,
                            Resources.SkylineWindow_Paste_The_protein_sequence_must_be_the_last_value_in_each_line);
                        throw new InvalidDataException(message);
                    }
                    else
                    {
                        string textFasta;
                        try
                        {
                            textFasta = FastaImporter.ToFasta(text, separator);
                        }
                        catch (LineColNumberedIoException x)
                        {
                            throw new InvalidDataException(x.Message, x);
                        }

                        SkylineWindow.ImportFasta(new StringReader(textFasta), Helpers.CountLinesInString(textFasta),
                            false, Resources.SkylineWindow_Paste_Paste_proteins,
                            new SkylineWindow.ImportFastaInfo(false, textFasta));
                    }

                    return;
                }
                // Otherwise, look for a list of peptides, or a bare sequence
                else
                {
                    // First make sure it looks like a sequence.
                    List<double> lineLengths = new List<double>();
                    int lineLen = 0;
                var textNoMods = FastaSequence.StripModifications(Transition.StripChargeIndicators(text, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE));
                    foreach (char c in textNoMods)
                    {
                        if (!AminoAcid.IsExAA(c) && !char.IsWhiteSpace(c) && c != '*' && c != '.')
                        {
                        MessageDlg.Show(SkylineWindow, string.Format(Resources.SkylineWindow_Unexpected_character__0__found_on_line__1__, c, lineLengths.Count + 1));
                            return;
                        }
                        if (c == '\n')
                        {
                            lineLengths.Add(lineLen);
                            lineLen = 0;
                        }
                        else if (!char.IsWhiteSpace(c))
                        {
                            lineLen++;
                        }
                    }
                    lineLengths.Add(lineLen);

                    // Check to see if the pasted text looks like a peptide list.
                    PeptideFilter filter = DocumentUI.Settings.PeptideSettings.Filter;
                    if (lineLengths.Count == 1 && lineLen < filter.MaxPeptideLength)
                        peptideList = true;
                    else
                    {
                        Statistics stats = new Statistics(lineLengths);
                        // All lines smaller than the peptide filter
                        if (stats.Max() <= filter.MaxPeptideLength ||
                            // 3 out of 4 are peptide length
                            (lineLengths.Count > 3 && stats.Percentile(0.75) <= filter.MaxPeptideLength))
                            peptideList = true;
                        // Probably a FASTA sequence, but ask if average line length is less than 40
                        else if (stats.Mean() < 40)
                        {
                            using (PasteTypeDlg dlg = new PasteTypeDlg())
                            {
                                if (dlg.ShowDialog(SkylineWindow) == DialogResult.Cancel)
                                    return;
                                peptideList = dlg.PeptideList;
                            }
                        }
                    }

                    if (peptideList)
                    {
                        text = FilterPeptideList(text);
                        if (text == null)
                            return; // Canceled
                    }
                    else if (text.Contains(@"."))
                    {
                        MessageDlg.Show(SkylineWindow, Resources.SkylineWindow_Paste_Unexpected_character_period_found);
                        return;
                    }

                    // Choose an unused ID
                    string seqId = Document.GetPeptideGroupId(peptideList);

                    // Construct valid FASTA format (with >> to indicate custom name)
                    text = @">>" + TextUtil.LineSeparate(seqId, text);
                }
            }

            string description = (peptideList ? Resources.SkylineWindow_Paste_Paste_peptide_list : Resources.SkylineWindow_Paste_Paste_FASTA);
            SkylineWindow.ImportFasta(new StringReader(text), Helpers.CountLinesInString(text),
                peptideList, description, new SkylineWindow.ImportFastaInfo(false, text));
        }
        private string FilterPeptideList(string text)
        {
            SrmSettings settings = DocumentUI.Settings;
            //            Enzyme enzyme = settings.PeptideSettings.Enzyme;

            // Check to see if any of the peptides would be filtered
            // by the current settings.
            string[] pepSequences = text.Split('\n');
            // ReSharper disable once CollectionNeverQueried.Local
            var setAdded = new HashSet<string>();
            var listAllPeptides = new List<string>();
            var listAcceptPeptides = new List<string>();
            var listFilterPeptides = new List<string>();
            for (int i = 0; i < pepSequences.Length; i++)
            {
                var charge = Transition.GetChargeFromIndicator(pepSequences[i].Trim(), TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE);
                string pepSeqMod = CleanPeptideSequence(Transition.StripChargeIndicators(pepSequences[i], TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE));
                string pepSeqClean = FastaSequence.StripModifications(pepSeqMod);
                if (!charge.IsEmpty)
                    pepSeqMod += Transition.GetChargeIndicator(charge);
                if (string.IsNullOrEmpty(pepSeqMod))
                    continue;
                if (pepSeqClean.Contains(@"."))
                {
                    MessageDlg.Show(SkylineWindow, string.Format(Resources.SkylineWindow_Unexpected_character__0__found_on_line__1__, @".", i + 1));
                    return null;
                }

                // Make sure no duplicates are added during a paste
                // With explicit modifications, there is now reason to add duplicates,
                // when multiple modified forms are desired.
                // if (setAdded.Contains(pepSeqClean))
                //    continue;
                setAdded.Add(pepSeqMod);
                listAllPeptides.Add(pepSeqMod);

                if (settings.Accept(pepSeqClean))
                    listAcceptPeptides.Add(pepSeqMod);
                else
                    listFilterPeptides.Add(pepSeqMod);
            }

            // If filtered peptides, ask the user whether to filter or keep.
            if (listFilterPeptides.Count > 0)
            {
                using (var dlg = new PasteFilteredPeptidesDlg { Peptides = listFilterPeptides })
                {
                    switch (dlg.ShowDialog(SkylineWindow))
                    {
                        case DialogResult.Cancel:
                            return null;
                        case DialogResult.Yes:
                            if (listAcceptPeptides.Count == 0)
                                return null;
                            return TextUtil.LineSeparate(listAcceptPeptides);
                    }
                }
            }
            return TextUtil.LineSeparate(listAllPeptides);
        }

        // CONSIDER: Probably should go someplace else
        private static string CleanPeptideSequence(string s)
        {
            s = s.Trim();
            if (s.IndexOfAny(new[] { '\n', '\r', '\t', ' ', '.' }) == -1)
                return s;
            // Internal whitespace
            var sb = new StringBuilder();
            bool inParen = false;
            foreach (char c in s)
            {
                if (c == '[' || c == '{')
                    inParen = true;
                if (c == ']' || c == '}')
                    inParen = false;
                // Preserve spaces inside brackets - modification names can have spaces.
                if (inParen || !char.IsWhiteSpace(c))
                    sb.Append(c);
            }
            // If the peptide is in the format K.PEPTIDER.C, then remove the periods
            // and the preceding and trailing amino acids.
            if (sb.Length > 4 && sb[1] == '.' && sb[sb.Length - 2] == '.')
            {
                sb.Remove(0, 2);
                sb.Remove(sb.Length - 2, 2);
            }
            return sb.ToString();
        }
        private void selectAllMenuItem_Click(object sender, EventArgs e) { SelectAll(); }
        public void SelectAll()
        {
            TreeNode node = SequenceTree.Nodes[0];
            SequenceTree.SelectedNode = node;
            while (node.NextVisibleNode != null)
            {
                node = node.NextVisibleNode;
            }
            bool usingKeysOverride = SequenceTree.UseKeysOverride;
            SequenceTree.UseKeysOverride = true;
            SequenceTree.KeysOverride = Keys.Shift;
            SequenceTree.SelectedNode = node;
            SequenceTree.KeysOverride = Keys.None;
            SequenceTree.UseKeysOverride = usingKeysOverride;
        }

        #endregion

        #region Find
        private void findMenuItem_Click(object sender, EventArgs e)
        {
            var index = SkylineWindow.OwnedForms.IndexOf(form => form is FindNodeDlg);
            if (index != -1)
                SkylineWindow.OwnedForms[index].Activate();
            else
                ShowFindNodeDlg();
        }

        private void findNextMenuItem_Click(object sender, EventArgs e)
        {
            FindNext(false);
        }

        public void ShowFindNodeDlg()
        {
            var dlg = new FindNodeDlg
            {
                FindOptions = FindOptions.ReadFromSettings(Settings.Default)
            };
            dlg.Show(SkylineWindow);
        }

        public void FindNext(bool reverse)
        {
            SkylineWindow.FindNext(reverse);
        }
        #endregion
        private void editNoteMenuItem_Click(object sender, EventArgs e) { EditNote(); }
        public void EditNote()
        {
            IList<IdentityPath> selPaths = SequenceTree.SelectedPaths;
            var selectedSrmTreeNode = SequenceTree.SelectedNode as SrmTreeNode;
            // Should not be possible to get here through the UI without a SrmTreeNode selected,
            // but a stack trace for it seems to have been posted to the unexpected error page
            if (selectedSrmTreeNode == null)
                return;

            using (EditNoteDlg dlg = new EditNoteDlg
            {
                Text = selPaths.Count > 1
                    ? Resources.SkylineWindow_EditNote_Edit_Note
                    : TextUtil.SpaceSeparate(Resources.SkylineWindow_EditNote_Edit_Note, selectedSrmTreeNode.Heading,
                        SequenceTree.SelectedNode.Text)
            })
            {
                dlg.Init(selectedSrmTreeNode.Document, selPaths);
                if (dlg.ShowDialog(SkylineWindow) == DialogResult.OK)
                {
                    bool clearAll = dlg.ClearAll;
                    var resultAnnotations = dlg.GetChangedAnnotations();
                    var resultColorIndex = dlg.ColorIndex;
                    string resultText = dlg.GetText();
                    if (resultColorIndex != -1)
                        Settings.Default.AnnotationColor = dlg.ColorIndex;

                    var count = selPaths.Count;
                    var changedTargets = count == 1
                        ? SelectedNode.Text
                        : string.Format(AuditLogStrings.SkylineWindow_EditNote__0__targets, count);

                    ModifyDocument(Resources.SkylineWindow_EditNote_Edit_Note, doc =>
                    {
                        foreach (IdentityPath nodePath in selPaths)
                        {
                            if (Equals(nodePath.Child, SequenceTree.NODE_INSERT_ID))
                                continue;
                            var nodeInDoc = doc.FindNode(nodePath);
                            var newAnnotations = clearAll
                                ? Annotations.EMPTY
                                : nodeInDoc.Annotations.MergeNewAnnotations(
                                    resultText,
                                    resultColorIndex,
                                    resultAnnotations);
                            if (Equals(newAnnotations, nodeInDoc.Annotations))
                                continue;

                            doc = (SrmDocument) doc.ReplaceChild(nodePath.Parent,
                                nodeInDoc.ChangeAnnotations(
                                    newAnnotations));
                        }

                        return doc;
                    }, docPair => AuditLogEntry.DiffDocNodes(MessageType.edited_note, docPair, changedTargets));
                }
            }
        }

        public void EditToolStripMenuItemDropDownOpening()
        {
            var synchronizedIntegration = DocumentUI.GetSynchronizeIntegrationChromatogramSets().Any();
            CanApplyOrRemovePeak(null, null, out var canApply, out var canRemove);
            if (!canApply && !canRemove)
            {
                integrationToolStripMenuItem.Enabled = false;
            }
            else
            {

                applyPeakAllToolStripMenuItem.Enabled = canApply;
                applyPeakSubsequentToolStripMenuItem.Enabled = canApply && !synchronizedIntegration;
                applyPeakGroupToolStripMenuItem.Text = Resources.SkylineWindow_editToolStripMenuItem_DropDownOpening_Apply_Peak_to_Group;
                groupApplyToByToolStripMenuItem.DropDownItems.Clear();
                applyPeakGroupToolStripMenuItem.Enabled = groupApplyToByToolStripMenuItem.Enabled = false;
                if (ReplicateValue.GetGroupableReplicateValues(DocumentUI).Any())
                {
                    groupApplyToByToolStripMenuItem.Enabled = !synchronizedIntegration;
                    var selectedAnnotation = GetGroupApplyToDescription();
                    if (selectedAnnotation != null)
                    {
                        applyPeakGroupToolStripMenuItem.Text = Resources.SkylineWindow_BuildChromatogramMenu_Apply_Peak_to_ + selectedAnnotation;
                        applyPeakGroupToolStripMenuItem.Enabled = !synchronizedIntegration;
                    }
                    var i = 0;
                    AddGroupByMenuItems(null, groupApplyToByToolStripMenuItem, replicateValue => Settings.Default.GroupApplyToBy = replicateValue?.ToPersistedString(), false, Settings.Default.GroupApplyToBy, ref i);
                }
                removePeakToolStripMenuItem.Enabled = canRemove;
                integrationToolStripMenuItem.Enabled = true;
            }
            synchronizedIntegrationToolStripMenuItem.Checked = synchronizedIntegration;
        }

        #region Peaks
        private void applyPeakAllMenuItem_Click(object sender, EventArgs e)
        {
            ApplyPeak(false, false);
        }

        private void applyPeakSubsequentMenuItem_Click(object sender, EventArgs e)
        {
            ApplyPeak(true, false);
        }

        private void applyPeakGroupGraphMenuItem_Click(object sender, EventArgs e)
        {
            ApplyPeak(false, true);
        }

        public void ApplyPeak(bool subsequent, bool group)
        {
            CanApplyOrRemovePeak(null, null, out var canApply, out _);
            if (!canApply)
                return;

            var resultsIndex = SelectedResultsIndex;
            var nodePepTree = SequenceTree.GetNodeOfType<PeptideTreeNode>();
            var nodeTranGroup = SequenceTree.GetNodeOfType<TransitionGroupTreeNode>()?.DocNode ?? PeakMatcher.PickTransitionGroup(DocumentUI, nodePepTree, resultsIndex);

            if (DocumentUI.GetSynchronizeIntegrationChromatogramSets().Any())
            {
                // Apply peak with synchronized integration
                var chromSet = DocumentUI.MeasuredResults.Chromatograms[resultsIndex];
                var filePath = SkylineWindow.GetGraphChrom(chromSet.Name).FilePath;
                var chromInfo = SkylineWindow.FindChromInfo(DocumentUI, nodeTranGroup, chromSet.Name, filePath);

                nodePepTree.EnsureChildren(); // in case peptide node is not expanded
                var nodeTranGroupPath = nodePepTree.Nodes.Cast<TransitionGroupTreeNode>().First(node => ReferenceEquals(node.DocNode, nodeTranGroup)).Path;

                var change = new ChangedPeakBoundsEventArgs(
                    nodeTranGroupPath, null, chromSet.Name, filePath,
                    new ScaledRetentionTime(chromInfo.StartRetentionTime.GetValueOrDefault()),
                    new ScaledRetentionTime(chromInfo.EndRetentionTime.GetValueOrDefault()),
                    null, PeakBoundsChangeType.both);

                var path = PropertyName.ROOT
                    .SubProperty(((PeptideGroupTreeNode) nodePepTree.SrmParent).DocNode.AuditLogText)
                    .SubProperty(nodePepTree.DocNode.AuditLogText)
                    .SubProperty(nodeTranGroup.AuditLogText);

                ModifyDocument(Resources.SkylineWindow_PickPeakInChromatograms_Apply_picked_peak,
                    doc => SkylineWindow.ChangePeakBounds(doc, SkylineWindow.GetSynchronizedPeakBoundChanges(doc, change, false)),
                    docPair => AuditLogEntry.CreateSimpleEntry(MessageType.applied_peak_all, docPair.NewDocumentType,
                        path.ToString()));
                return;
            }

            using (var longWait = new LongWaitDlg(SkylineWindow) { Text = Resources.SkylineWindow_ApplyPeak_Applying_Peak })
            {
                SrmDocument doc = null;
                try
                {
                    var chromatogramSet = Document.MeasuredResults.Chromatograms[resultsIndex];
                    var resultsFile = SkylineWindow.GetGraphChrom(chromatogramSet.Name).GetChromFileInfoId();
                    var groupBy = group ? ReplicateValue.FromPersistedString(Document.Settings, Settings.Default.GroupApplyToBy) : null;
                    var groupByValue = groupBy?.GetValue(new AnnotationCalculator(Document), chromatogramSet);
                    longWait.PerformWork(SkylineWindow, 800, monitor =>
                        doc = PeakMatcher.ApplyPeak(Document, nodePepTree, nodeTranGroup, resultsIndex, resultsFile, subsequent, groupBy, groupByValue, monitor));
                }
                catch (Exception x)
                {
                    MessageDlg.ShowWithException(SkylineWindow, TextUtil.LineSeparate(Resources.SkylineWindow_ApplyPeak_Failed_to_apply_peak_, x.Message), x);
                }

                if (!longWait.IsCanceled && doc != null && !ReferenceEquals(doc, Document))
                {
                    // ReSharper disable once PossibleNullReferenceException
                    var path = PropertyName.ROOT
                        .SubProperty(((PeptideGroupTreeNode)nodePepTree.SrmParent).DocNode.AuditLogText)
                        .SubProperty(nodePepTree.DocNode.AuditLogText)
                        .SubProperty(nodeTranGroup.AuditLogText);

                    var msg = subsequent ? MessageType.applied_peak_subsequent : MessageType.applied_peak_all;

                    ModifyDocument(Resources.SkylineWindow_PickPeakInChromatograms_Apply_picked_peak, document => doc,
                        docPair => AuditLogEntry.CreateSimpleEntry(msg, docPair.NewDocumentType, path.ToString()));
                }
            }
        }

        private void removePeakMenuItem_Click(object sender, EventArgs e)
        {
            var menu = sender as ToolStripMenuItem;
            if (menu == null || menu.DropDownItems.OfType<object>().Any())
                return;
            RemovePeak(false);
        }

        public void RemovePeak(bool removePeakBySequenceTreeContextMenu)
        {
            var chromFileInfoId = SkylineWindow.GetSelectedChromFileId();
            CanApplyOrRemovePeak(null, null, out _, out var canRemove);
            if (!canRemove)
                return;

            var nodeGroupTree = SequenceTree.GetNodeOfType<TransitionGroupTreeNode>();
            var nodeGroups = new List<Tuple<TransitionGroupDocNode, IdentityPath>>();
            var nodePepTree = SelectedNode as PeptideTreeNode;
            if (nodeGroupTree != null)
            {
                nodeGroups.Add(new Tuple<TransitionGroupDocNode, IdentityPath>(nodeGroupTree.DocNode, nodeGroupTree.Path));
            }
            else if (nodePepTree != null && nodePepTree.Nodes.OfType<object>().Any())
            {
                nodeGroups.AddRange(
                    from TransitionGroupDocNode tranGroup in nodePepTree.DocNode.Children
                    select Tuple.Create(tranGroup, new IdentityPath(nodePepTree.Path, tranGroup.Id)));
            }
            else
            {
                return;
            }

            TransitionDocNode nodeTran = null;
            if (removePeakBySequenceTreeContextMenu)
            {
                var nodeTranTree = SelectedNode as TransitionTreeNode;
                if (nodeTranTree != null)
                {
                    nodeTran = nodeTranTree.DocNode;
                }
            }

            if (nodeGroups.Count == 1)
            {
                var nodeGroup = nodeGroups.First();
                RemovePeak(nodeGroup.Item2, nodeGroup.Item1, nodeTran);
            }
            else
            {
                // ReSharper disable once PossibleNullReferenceException
                ModifyDocument(
                    string.Format(Resources.SkylineWindow_removePeakContextMenuItem_Click_Remove_all_peaks_from__0_,
                        nodePepTree.DocNode.ModifiedSequenceDisplay),
                    document => nodeGroups.Aggregate(Document,
                        (doc, nodeGroup) => RemovePeakInternal(doc, SelectedResultsIndex, chromFileInfoId,
                            nodeGroup.Item2, nodeGroup.Item1, nodeTran)),
                    docPair =>
                    {
                        var peptideGroup = ((PeptideGroupTreeNode) nodePepTree.SrmParent).DocNode;
                        var name = PropertyName.ROOT.SubProperty(peptideGroup.AuditLogText)
                            .SubProperty(nodePepTree.DocNode.AuditLogText);
                        return AuditLogEntry.CreateSimpleEntry(MessageType.removed_all_peaks_from,
                            docPair.OldDocumentType, name,
                            docPair.OldDoc.MeasuredResults.Chromatograms[SelectedResultsIndex].Name);
                    });
            }
        }

        public void RemovePeak(IdentityPath groupPath, TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
        {
            string message = nodeTran == null
                ? string.Format(Resources.SkylineWindow_RemovePeak_Remove_all_peaks_from__0__, ChromGraphItem.GetTitle(nodeGroup))
                : string.Format(Resources.SkylineWindow_RemovePeak_Remove_peak_from__0__, ChromGraphItem.GetTitle(nodeTran));
            var chromFileInfoId = SkylineWindow.GetSelectedChromFileId();
            ModifyDocument(message, doc => RemovePeakInternal(doc, SelectedResultsIndex, chromFileInfoId, groupPath, nodeGroup, nodeTran),
                docPair =>
                {
                    var msg = nodeTran == null ? MessageType.removed_all_peaks_from : MessageType.removed_peak_from;

                    var peptide = (PeptideDocNode)docPair.OldDoc.FindNode(groupPath.Parent);
                    var peptideGroup = (PeptideGroupDocNode)docPair.OldDoc.FindNode(groupPath.Parent.Parent);

                    var name = PropertyName.ROOT.SubProperty(peptideGroup.AuditLogText)
                        .SubProperty(peptide.AuditLogText).SubProperty(nodeGroup.AuditLogText);
                    if (nodeTran != null)
                        name = name.SubProperty(nodeTran.AuditLogText);

                    return AuditLogEntry.CreateSimpleEntry(msg, docPair.OldDocumentType, name,
                        docPair.OldDoc.MeasuredResults.Chromatograms[SelectedResultsIndex].Name);
                });
        }

        private SrmDocument RemovePeakInternal(SrmDocument document, int resultsIndex, ChromFileInfoId chromFileInfoId, IdentityPath groupPath,
            TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran, bool syncRecurse = true)
        {
            ChromInfo chromInfo;
            Transition transition;

            if (nodeTran == null)
            {
                chromInfo = nodeGroup.GetChromInfo(resultsIndex, chromFileInfoId);
                transition = null;
            }
            else
            {
                chromInfo = nodeTran.GetChromInfo(resultsIndex, chromFileInfoId);
                transition = nodeTran.Transition;
            }
            if (chromInfo == null)
                return document;

            string name = SkylineWindow.GetGraphChromStrings(resultsIndex, chromInfo.FileId, out var filePath);
            document = name == null
                ? document
                : document.ChangePeak(groupPath, name, filePath, transition, 0, 0, UserSet.TRUE, PeakIdentification.FALSE, false);

            if (syncRecurse)
            {
                var syncTargets = document.GetSynchronizeIntegrationChromatogramSets().ToHashSet();
                for (var i = 0; i < document.MeasuredResults.Chromatograms.Count; i++)
                {
                    var chromSet = document.MeasuredResults.Chromatograms[i];
                    if (syncTargets.Contains(chromSet))
                    {
                        foreach (var info in chromSet.MSDataFileInfos.Where(info => !(resultsIndex == i && Equals(chromInfo.FileId, info.FileId))))
                            document = RemovePeakInternal(document, i, info.FileId, groupPath, nodeGroup, nodeTran, false);
                    }
                }
            }

            return document;
        }
        public void CanApplyOrRemovePeak(ToolStripItemCollection removePeakItems, IsotopeLabelType labelType, out bool canApply, out bool canRemove)
        {
            canApply = canRemove = false;

            if (!DocumentUI.Settings.HasResults)
                return;

            var selectedTreeNode = SelectedNode as SrmTreeNode;
            var displayType = GraphChromatogram.GetDisplayType(DocumentUI, selectedTreeNode);
            if (displayType == DisplayTypeChrom.base_peak || displayType == DisplayTypeChrom.tic || displayType == DisplayTypeChrom.qc)
                return;
            var chromFileInfoId = GetSelectedChromFileId();

            var node = selectedTreeNode as TransitionTreeNode;
            if (node != null && GraphChromatogram.IsSingleTransitionDisplay)
            {
                if (HasPeak(SelectedResultsIndex, chromFileInfoId, node.DocNode))
                {
                    if (removePeakItems != null)
                        removePeakItems.Add(new ToolStripMenuItem());
                    canApply = canRemove = true;
                }
            }
            else if (selectedTreeNode is TransitionTreeNode && displayType == DisplayTypeChrom.all ||
                     selectedTreeNode is TransitionGroupTreeNode ||
                     selectedTreeNode is PeptideTreeNode treeNode && treeNode.DocNode.Children.Any())
            {
                canApply = true;

                var nodeGroupTree = SequenceTree.GetNodeOfType<TransitionGroupTreeNode>();
                var hasPeak = nodeGroupTree != null
                    ? HasPeak(SelectedResultsIndex, chromFileInfoId, nodeGroupTree.DocNode)
                    : SequenceTree.GetNodeOfType<PeptideTreeNode>().DocNode.TransitionGroups.Any(tranGroup => HasPeak(SelectedResultsIndex, chromFileInfoId, tranGroup));

                if (hasPeak)
                {
                    removePeakItems?.Clear();
                    canRemove = true;

                    // Remove [IsotopeLabelType]
                    if (removePeakItems != null && labelType != null)
                    {
                        // only if multiple isotope label types
                        if (selectedTreeNode is PeptideTreeNode peptideTreeNode &&
                            peptideTreeNode.DocNode.TransitionGroups.Select(nodeTranGroup => nodeTranGroup.TransitionGroup.LabelType).Distinct().Count() > 1)
                        {
                            removePeakItems.Add(new ToolStripMenuItem { Tag = labelType });
                        }
                    }
                }
            }
        }

        // ReSharper disable SuggestBaseTypeForParameter
        private static bool HasPeak(int iResult, ChromFileInfoId chromFileInfoId, TransitionGroupDocNode nodeGroup)
            // ReSharper restore SuggestBaseTypeForParameter
        {
            foreach (TransitionDocNode nodeTran in nodeGroup.Children)
            {
                if (HasPeak(iResult, chromFileInfoId, nodeTran))
                    return true;
            }
            return false;
        }

        private static bool HasPeak(int iResults, ChromFileInfoId chromFileInfoId, TransitionDocNode nodeTran)
        {
            var chromInfo = nodeTran.GetChromInfo(iResults, chromFileInfoId);
            return (chromInfo != null && !chromInfo.IsEmpty);
        }

        private void synchronizedIntegrationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowSynchronizedIntegrationDialog();
        }

        public void ShowSynchronizedIntegrationDialog()
        {
            using (var dlg = new SynchronizedIntegrationDlg(SkylineWindow))
            {
                if (dlg.ShowDialog(SkylineWindow) == DialogResult.OK)
                {
                    var groupBy = dlg.GroupByPersistedString;
                    var all = dlg.IsAll;
                    var targets = dlg.TargetsInvariant.ToArray();

                    var existing = Document.Settings.TransitionSettings.Integration;
                    if (groupBy != existing.SynchronizedIntegrationGroupBy ||
                        all != existing.SynchronizedIntegrationAll ||
                        !ArrayUtil.EqualsDeep(existing.SynchronizedIntegrationTargets, targets))
                    {
                        ModifyDocument(
                            string.Format(Resources.EditMenu_SetSynchronizedIntegration_Change_synchronized_integration_to__0_,
                                (string.IsNullOrEmpty(groupBy) ? Resources.GroupByItem_ToString_Replicates : groupBy) + @":" + string.Join(@",", targets)),
                            doc => doc.ChangeSettings(doc.Settings.ChangeTransitionIntegration(i =>
                                i.ChangeSynchronizedIntegration(groupBy, all, targets))), AuditLogEntry.SettingsLogFunction);
                    }
                }
            }
        }

        #endregion

        #region Insert

        private void insertFASTAToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowPasteFastaDlg();
        }

        public void ShowPasteFastaDlg()  // Expose for test access
        {
            using (var pasteDlg = new PasteDlg(SkylineWindow)
            {
                SelectedPath = SelectedPath,
                PasteFormat = PasteFormat.fasta
            })
            {
                if (pasteDlg.ShowDialog(SkylineWindow) == DialogResult.OK)
                    SelectedPath = pasteDlg.SelectedPath;
            }
        }

        private void insertPeptidesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowPastePeptidesDlg();
        }

        public void ShowPastePeptidesDlg()
        {
            using (var pasteDlg = new PasteDlg(SkylineWindow)
            {
                SelectedPath = SelectedPath,
                PasteFormat = PasteFormat.peptide_list
            })
            {
                if (pasteDlg.ShowDialog(SkylineWindow) == DialogResult.OK)
                    SelectedPath = pasteDlg.SelectedPath;
            }
        }

        private void insertProteinsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowPasteProteinsDlg();
        }

        public void ShowPasteProteinsDlg()
        {
            using (var pasteDlg = new PasteDlg(SkylineWindow)
            {
                SelectedPath = SelectedPath,
                PasteFormat = PasteFormat.protein_list
            })
            {
                if (pasteDlg.ShowDialog(SkylineWindow) == DialogResult.OK)
                    SelectedPath = pasteDlg.SelectedPath;
            }
        }

        private void insertTransitionListMenuItem_Click(object sender, EventArgs e)
        {
            ShowInsertTransitionListDlg();
        }

        public void ShowInsertTransitionListDlg()
        {
            using (var transitionDlg = new InsertTransitionListDlg())
            {
                if (transitionDlg.ShowDialog(SkylineWindow) != DialogResult.OK)
                    return;

                IFormatProvider formatProvider;
                char separator;
                Type[] columnTypes;
                var text = transitionDlg.TransitionListText;
                // As long as it has columns we want to parse the input as a transition list
                if (MassListImporter.IsColumnar(text, out formatProvider, out separator, out columnTypes))
                {
                    try
                    {
                        SkylineWindow.ImportMassList(new MassListInputs(text, formatProvider, separator),
                            Resources.SkylineWindow_Paste_Paste_transition_list, false, SrmDocument.DOCUMENT_TYPE.none, true);
                    }
                    catch (Exception exception)
                    {
                        if (ExceptionUtil.IsProgrammingDefect(exception))
                        {
                            throw;
                        }
                        MessageDlg.ShowWithException(this, exception.Message, exception, true);
                    }
                }
                else
                {
                    // Alert the user that their list is not columnar
                    MessageDlg.Show(this, Resources.SkylineWindow_importMassListMenuItem_Click_Data_columns_not_found_in_first_line);
                }
            }
        }
        #endregion

        #region Expand/Collapse
        private void expandProteinsMenuItem_Click(object sender, EventArgs e) { ExpandProteins(); }
        public void ExpandProteins()
        {
            if (Document.MoleculeCount > SequenceTree.MAX_PEPTIDE_EXPANSION)
            {
                MessageDlg.Show(SkylineWindow, Resources.SkylineWindow_ExpandProteins_The_number_of_targets_exceeds_the_limit_for_this_operation_);
                return;
            }
            BulkUpdateTreeNodes<TreeNode>(() =>
            {
                foreach (PeptideGroupTreeNode node in SequenceTree.GetSequenceNodes())
                    node.Expand();
            });
            Settings.Default.SequenceTreeExpandProteins = true;
        }

        private void expandPeptidesMenuItem_Click(object sender, EventArgs e) { ExpandPeptides(); }
        public void ExpandPeptides()
        {
            if (Document.MoleculeCount > SequenceTree.MAX_PEPTIDE_EXPANSION)
            {
                MessageDlg.Show(SkylineWindow, Resources.SkylineWindow_ExpandProteins_The_number_of_targets_exceeds_the_limit_for_this_operation_);
                return;
            }
            BulkUpdateTreeNodes<TreeNode>(() =>
            {
                foreach (PeptideGroupTreeNode node in SequenceTree.GetSequenceNodes())
                {
                    node.Expand();
                    foreach (TreeNode nodeChild in node.Nodes)
                        nodeChild.Expand();
                }
            });
            Settings.Default.SequenceTreeExpandPeptides =
                Settings.Default.SequenceTreeExpandProteins = true;
        }

        private void expandPrecursorsMenuItem_Click(object sender, EventArgs e) { ExpandPrecursors(); }
        public void ExpandPrecursors()
        {
            if (Document.MoleculeTransitionCount > SequenceTree.MAX_TRANSITION_EXPANSTION)
            {
                MessageDlg.Show(SkylineWindow, Resources.SkylineWindow_ExpandProteins_The_number_of_targets_exceeds_the_limit_for_this_operation_);
                return;
            }
            BulkUpdateTreeNodes<TreeNode>(() =>
            {
                foreach (TreeNode node in SequenceTree.Nodes)
                    node.ExpandAll();
            });
            Settings.Default.SequenceTreeExpandPrecursors =
                Settings.Default.SequenceTreeExpandPeptides =
                    Settings.Default.SequenceTreeExpandProteins = true;
        }

        private void collapseProteinsMenuItem_Click(object sender, EventArgs e) { CollapseProteins(); }
        public void CollapseProteins()
        {
            BulkUpdateTreeNodes<PeptideGroupTreeNode>(() =>
            {
                foreach (PeptideGroupTreeNode node in SequenceTree.GetSequenceNodes())
                    node.CollapseAndClear();
            });
            Settings.Default.SequenceTreeExpandProteins =
                Settings.Default.SequenceTreeExpandPeptides =
                    Settings.Default.SequenceTreeExpandPrecursors = false;
        }

        private void collapsePeptidesMenuItem_Click(object sender, EventArgs e) { CollapsePeptides(); }
        public void CollapsePeptides()
        {
            BulkUpdateTreeNodes<PeptideTreeNode>(() =>
            {
                foreach (PeptideGroupTreeNode node in SequenceTree.GetSequenceNodes())
                foreach (PeptideTreeNode child in node.Nodes.OfType<PeptideTreeNode>())
                    child.CollapseAndClear();
            });
            Settings.Default.SequenceTreeExpandPeptides =
                Settings.Default.SequenceTreeExpandPrecursors = false;
        }

        private void collapsePrecursorsMenuItem_Click(object sender, EventArgs e) { CollapsePrecursors(); }
        public void CollapsePrecursors()
        {
            BulkUpdateTreeNodes<PeptideTreeNode>(() =>
            {
                foreach (PeptideGroupTreeNode node in SequenceTree.GetSequenceNodes())
                foreach (PeptideTreeNode child in node.Nodes.OfType<PeptideTreeNode>())
                foreach (TransitionGroupTreeNode grandChild in child.Nodes.OfType<TransitionGroupTreeNode>())
                    grandChild.CollapseAndClear();
            });
            Settings.Default.SequenceTreeExpandPrecursors = false;
        }

        private void BulkUpdateTreeNodes<TNode>(Action update)
            where TNode : TreeNode
        {
            TreeNode nodeTop = SequenceTree.GetNodeOfType<TNode>(SequenceTree.TopNode) ??
                               SequenceTree.TopNode;

            using (SequenceTree.BeginLargeUpdate())
            {
                update();
            }
            if (nodeTop != null)
                SequenceTree.TopNode = nodeTop;
        }
        #endregion

        #region Set Standard Type
        private void setStandardTypeMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            UpdateStandardTypeMenu();
        }

        public void UpdateStandardTypeMenu()
        {
            var selectedPeptides = SequenceTree.SelectedDocNodes
                .OfType<PeptideDocNode>().ToArray();
            var selectedStandardTypes = selectedPeptides.Select(peptide => peptide.GlobalStandardType)
                .Distinct().ToArray();
            foreach (var menuItemStandardType in GetStandardTypeMenuItems())
            {
                var toolStripMenuItem = menuItemStandardType.Key;
                var standardType = menuItemStandardType.Value;
                if (standardType == StandardType.IRT)
                {
                    // Only show iRT menu item when there is an iRT calculator
                    var rtRegression = Document.Settings.PeptideSettings.Prediction.RetentionTime;
                    toolStripMenuItem.Visible = rtRegression == null || !(rtRegression.Calculator is RCalcIrt);
                    toolStripMenuItem.Enabled = selectedStandardTypes.Contains(StandardType.IRT);
                }
                else
                {
                    toolStripMenuItem.Enabled = selectedPeptides.Length >= 1 &&
                                                !selectedStandardTypes.Contains(StandardType.IRT);
                }
                toolStripMenuItem.Checked = selectedStandardTypes.Length == 1 &&
                                            selectedStandardTypes[0] == standardType;
            }
        }

        public IDictionary<ToolStripMenuItem, StandardType> GetStandardTypeMenuItems()
        {
            return new Dictionary<ToolStripMenuItem, StandardType>
            {
                {noStandardMenuItem, null},
                {normStandardMenuItem, StandardType.GLOBAL_STANDARD},
                {surrogateStandardMenuItem, StandardType.SURROGATE_STANDARD},
                {qcStandardMenuItem, StandardType.QC},
                {irtStandardMenuItem, StandardType.IRT},
            };
        }

        private void noStandardMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetStandardType(null);
        }

        private void qcStandardMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetStandardType(PeptideDocNode.STANDARD_TYPE_QC);
        }

        private void normStandardMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetStandardType(StandardType.GLOBAL_STANDARD);
        }

        private void surrogateStandardMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.SetStandardType(StandardType.SURROGATE_STANDARD);
        }
        #endregion

        #region Modify Peptide/Molecule
        private void modifyPeptideMenuItem_Click(object sender, EventArgs e)
        {
            var nodeTranGroupTree = SequenceTree.GetNodeOfType<TransitionGroupTreeNode>();
            var nodeTranTree = SequenceTree.GetNodeOfType<TransitionTreeNode>();
            if (nodeTranTree == null && nodeTranGroupTree != null && nodeTranGroupTree.DocNode.TransitionGroup.IsCustomIon)
            {
                ModifySmallMoleculeTransitionGroup();
            }
            else if (nodeTranTree != null && nodeTranTree.DocNode.Transition.IsNonPrecursorNonReporterCustomIon())
            {
                ModifyTransition(nodeTranTree);
            }
            else
            {
                ModifyPeptide();
            }
        }

        public void ModifySmallMoleculeTransitionGroup()
        {
            var nodeTransitionGroupTree = SequenceTree.GetNodeOfType<TransitionGroupTreeNode>();

            if (nodeTransitionGroupTree == null)
                return;

            var nodeTransGroup = nodeTransitionGroupTree.DocNode;
            if (!nodeTransGroup.Peptide.IsCustomMolecule)
                return;

            var existingPrecursors = nodeTransitionGroupTree.PepNode.TransitionGroups.Select(child => child.TransitionGroup).Where(c => c.IsCustomIon).ToArray();
            using (var dlg = new EditCustomMoleculeDlg(SkylineWindow,
                EditCustomMoleculeDlg.UsageMode.precursor,
                Resources.SkylineWindow_ModifySmallMoleculeTransitionGroup_Modify_Custom_Ion_Precursor,
                nodeTransGroup.TransitionGroup, // Self
                existingPrecursors, // Self and siblings
                TransitionGroup.MIN_PRECURSOR_CHARGE,
                TransitionGroup.MAX_PRECURSOR_CHARGE,
                Document.Settings,
                nodeTransGroup.TransitionGroup.CustomMolecule,
                nodeTransGroup.TransitionGroup.PrecursorAdduct,
                nodeTransGroup.ExplicitValues,
                null,
                null,
                nodeTransGroup.TransitionGroup.LabelType))
            {
                dlg.SetResult(nodeTransGroup.CustomMolecule,
                    nodeTransGroup.PrecursorAdduct); // Set initial value change check
                if (dlg.ShowDialog(SkylineWindow) == DialogResult.OK)
                {
                    Assume.IsTrue(Equals(dlg.ResultCustomMolecule,
                        nodeTransGroup.Peptide.CustomMolecule)); // Not expecting any change to neutral molecule
                    var newTransGroup = new TransitionGroup(nodeTransGroup.Peptide, dlg.Adduct, dlg.IsotopeLabelType);
                    ModifyDocument(string.Format(Resources.SkylineWindow_ModifyPeptide_Modify__0__,
                            nodeTransitionGroupTree.Text),
                        doc =>
                        {
                            if (!Equals(newTransGroup, nodeTransGroup.Id))
                            {
                                // ID Changes impact all children, because IDs have back pointers to their parents.
                                // User altered some identity item so we have to insert a copy of the tree from here down, and delete  
                                var newNode = nodeTransGroup.UpdateSmallMoleculeTransitionGroup(nodeTransGroup.Peptide,
                                    newTransGroup, Document.Settings);
                                var newdoc = (SrmDocument) doc.Insert(nodeTransitionGroupTree.Path, newNode);
                                return (SrmDocument) newdoc.RemoveChild(nodeTransitionGroupTree.Path.Parent,
                                    nodeTransGroup);
                            }
                            else
                            {
                                var newNode =
                                    new TransitionGroupDocNode(nodeTransGroup.TransitionGroup,
                                        nodeTransGroup.Annotations, Document.Settings, null, null,
                                        dlg.ResultExplicitTransitionGroupValues, nodeTransGroup.Results,
                                        nodeTransGroup.Children.Cast<TransitionDocNode>().ToArray(),
                                        nodeTransGroup.AutoManageChildren);
                                if (Equals(newNode, nodeTransGroup))
                                    return doc;

                                return (SrmDocument) doc.ReplaceChild(nodeTransitionGroupTree.Path.Parent, newNode);
                            }
                        },
                        docPair => AuditLogEntry.DiffDocNodes(MessageType.modified, docPair,
                            AuditLogEntry.GetNodeName(docPair.OldDoc, nodeTransGroup)));
                }
            }
        }

        public void ModifyPeptide()
        {
            var nodePepTree = SequenceTree.GetNodeOfType<PeptideTreeNode>();
            if (nodePepTree != null)
            {
                var nodePep = nodePepTree.DocNode;
                if (nodePep.Peptide.IsCustomMolecule)
                {
                    // End up here when modifying the small molecule equivalent of a peptide
                    // We want to be able to adjust explicit retention time in particular
                    using (
                        var dlg = new EditCustomMoleculeDlg(SkylineWindow, Resources.SkylineWindow_ModifyPeptide_Modify_Small_Molecule,
                            Document.Settings,
                            nodePep.Peptide.CustomMolecule,
                            nodePep.ExplicitRetentionTime ?? ExplicitRetentionTimeInfo.EMPTY))
                    {
                        dlg.SetResult(nodePep.Peptide.CustomMolecule, Adduct.EMPTY);
                        if (dlg.ShowDialog(SkylineWindow) == DialogResult.OK)
                        {
                            ModifyDocument(
                                string.Format(Resources.SkylineWindow_ModifyPeptide_Modify__0__, nodePepTree.Text),
                                doc =>
                                {
                                    // If we didn't change the custom molecule's formula and/or mass, the ID should be unchanged and we can just change any needed transition info.
                                    // But if custom molecule has changed then we can't "Replace", since newNode has a different identity and isn't in the document.  We have to 
                                    // insert and delete instead.  
                                    var newNode = nodePep.ChangeCustomIonValues(doc.Settings, dlg.ResultCustomMolecule, dlg.ExplicitRetentionTimeInfo);
                                    // Nothing to do if the node is not changing
                                    if (Equals(nodePep, newNode))
                                        return doc;

                                    if (!nodePep.Peptide.Equals(newNode.Peptide)) // Did that change the Id object?
                                    {
                                        // We don't want the tree selection to change, so note this as a replacement.
                                        var newdoc = (SrmDocument)doc.Insert(nodePepTree.Path, newNode.ChangeReplacedId(nodePep.Id));
                                        return (SrmDocument)newdoc.RemoveChild(nodePepTree.Path.Parent, nodePep);
                                    }
                                    else
                                    {
                                        return (SrmDocument)doc.ReplaceChild(nodePepTree.Path.Parent, newNode);
                                    }
                                }, docPair => AuditLogEntry.DiffDocNodes(MessageType.modified, docPair,
                                    AuditLogEntry.GetNodeName(docPair.OldDoc, nodePep)));
                        }
                    }
                }
                else
                {
                    using (var dlg = new EditPepModsDlg(DocumentUI.Settings, nodePep, true))
                    {
                        dlg.Height = Math.Min(dlg.Height, Screen.FromControl(SkylineWindow).WorkingArea.Height);
                        if (dlg.ShowDialog(SkylineWindow) == DialogResult.OK)
                        {
                            var listStaticMods = Settings.Default.StaticModList;
                            var listHeavyMods = Settings.Default.HeavyModList;
                            ModifyDocument(
                                string.Format(Resources.SkylineWindow_ModifyPeptide_Modify__0__, nodePepTree.Text),
                                doc =>
                                    doc.ChangePeptideMods(nodePepTree.Path,
                                        dlg.ExplicitMods,
                                        dlg.IsCreateCopy,
                                        listStaticMods,
                                        listHeavyMods), docPair => AuditLogEntry.DiffDocNodes(MessageType.modified,
                                    docPair,
                                    AuditLogEntry.GetNodeName(docPair.OldDoc, nodePep)));
                        }
                    }
                }
            }
        }

        public void ModifyTransition(TransitionTreeNode nodeTranTree)
        {
            var nodeTran = nodeTranTree.DocNode;
            if (nodeTran.Transition.IsNonPrecursorNonReporterCustomIon())
            {
                var nodeGroupTree = SequenceTree.GetNodeOfType<TransitionGroupTreeNode>();
                // Existing transitions to avoid duplication
                var existingMolecules = nodeGroupTree.DocNode.Transitions
                    .Select(nodeT => nodeT.Transition)
                    .Where(t => t.IsNonReporterCustomIon()).ToArray();

                using (var dlg = new EditCustomMoleculeDlg(SkylineWindow,
                    EditCustomMoleculeDlg.UsageMode.fragment,
                    Resources.SkylineWindow_ModifyTransition_Modify_Transition,
                    nodeTran.Transition, existingMolecules, // Self and siblings
                    Transition.MIN_PRODUCT_CHARGE,
                    Transition.MAX_PRODUCT_CHARGE,
                    Document.Settings,
                    nodeTran.Transition.CustomIon,
                    nodeTran.Transition.Adduct,
                    null,
                    nodeTran.ExplicitValues,
                    null, null))
                {
                    dlg.SetResult(nodeTran.Transition.CustomIon, nodeTran.Transition.Adduct);
                    if (dlg.ShowDialog(SkylineWindow) == DialogResult.OK)
                    {
                        ModifyDocument(string.Format(Resources.SkylineWindow_ModifyTransition_Modify_product_ion__0_,
                                nodeTranTree.Text),
                            doc =>
                            {
                                Assume.IsTrue(ReferenceEquals(nodeGroupTree.DocNode.TransitionGroup, nodeTran.Transition.Group));
                                var mass = dlg.ResultCustomMolecule.GetMass(
                                    doc.Settings.TransitionSettings.Prediction.FragmentMassType);
                                var transition = new Transition(nodeTran.Transition.Group, dlg.Adduct, null,
                                    dlg.ResultCustomMolecule);
                                var noTransitionChange = transition.Equals(nodeTran.Transition);
                                var newDocNode = new TransitionDocNode(noTransitionChange ? nodeTran.Transition : transition,
                                    nodeTran.Annotations,
                                    nodeTran.Losses,
                                    mass,
                                    nodeTran.QuantInfo,
                                    dlg.ResultExplicitTransitionValues,
                                    null);
                                // Nothing to do, if the transition is not changing
                                if (Equals(newDocNode, nodeTran))
                                    return doc;

                                if (noTransitionChange)
                                {
                                    // If we are not changing anything in the transition ID, we can be more gentle in replacement
                                    return (SrmDocument)doc.ReplaceChild(nodeTranTree.Path.Parent, newDocNode);
                                }
                                else
                                {
                                    // Note we can't just Replace the node, as it has a new Id that's not in the doc.
                                    // But neither do we want the tree selection to change, so note this as a replacement.
                                    var newDoc = doc.Insert(nodeTranTree.Path, newDocNode.ChangeReplacedId(nodeTran.Id));
                                    return (SrmDocument)newDoc.RemoveChild(nodeTranTree.Path.Parent, nodeTran);
                                }
                            }, docPair => AuditLogEntry.DiffDocNodes(MessageType.modified, docPair,
                                AuditLogEntry.GetNodeName(docPair.OldDoc, nodeTran)));
                    }
                }
            }
        }
        #endregion

        private void manageUniquePeptidesMenuItem_Click(object sender, EventArgs e)
        {
            ShowUniquePeptidesDlg();
        }

        public void ShowUniquePeptidesDlg()
        {
            if (DocumentUI.Settings.PeptideSettings.BackgroundProteome.IsNone)
            {
                MessageDlg.Show(SkylineWindow,
                    TextUtil.LineSeparate(Resources.SkylineWindow_ShowUniquePeptidesDlg_Inspecting_peptide_uniqueness_requires_a_background_proteome,
                        Resources.SkylineWindow_ShowUniquePeptidesDlg_Choose_a_background_proteome_in_the_Digestions_tab_of_the_Peptide_Settings));
                return;
            }

            // Need at least one node selected in tree
            var peptideGroupTreeNodes = UniquePeptidesDlg.PeptideSelection(SequenceTree);
            if (!peptideGroupTreeNodes.Any())
            {
                return;  // No selection
            }
            if (!ViewLibraryDlg.EnsureDigested(SkylineWindow, DocumentUI.Settings.PeptideSettings.BackgroundProteome, null))
            if (!ViewLibraryDlg.EnsureDigested(SkylineWindow, DocumentUI.Settings.PeptideSettings.BackgroundProteome, null))
            {
                return;
            }
            using (UniquePeptidesDlg uniquePeptidesDlg = new UniquePeptidesDlg(SkylineWindow)
            {
                PeptideGroupTreeNodes = peptideGroupTreeNodes
            })
            {
                uniquePeptidesDlg.ShowDialog(SkylineWindow);
            }
        }
        private void manageResultsMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ManageResults();
        }

        public void SequenceTreeAfterSelect()
        {
            SrmTreeNode nodeTree = SequenceTree.SelectedNode as SrmTreeNode;
            var enabled = nodeTree != null;
            editNoteToolStripMenuItem.Enabled = enabled;
            manageUniquePeptidesMenuItem.Enabled = UniquePeptidesDlg.PeptideSelection(SequenceTree).Any(); // Only works for peptide molecules, and only if selected
            var nodePepTree = SequenceTree.GetNodeOfType<PeptideTreeNode>();
            modifyPeptideMenuItem.Enabled = nodePepTree != null;
            setStandardTypeMenuItem.Enabled = SkylineWindow.HasSelectedTargetPeptides();
        }

        public string GetGroupApplyToDescription()
        {
            var document = Document;
            var groupBy = ReplicateValue.FromPersistedString(document.Settings, Settings.Default.GroupApplyToBy);
            if (groupBy == null)
            {
                return null;
            }

            object value = null;
            if (document.Settings.HasResults)
            {
                int replicateIndex = SelectedResultsIndex;
                if (replicateIndex >= 0 && replicateIndex < document.Settings.MeasuredResults.Chromatograms.Count)
                {
                    var chromatogramSet = document.Settings.MeasuredResults.Chromatograms[replicateIndex];
                    value = groupBy.GetValue(new AnnotationCalculator(document), chromatogramSet);
                }
            }

            return groupBy.Title + ':' + value;
        }
        public void AddGroupByMenuItems(ToolStrip menuStrip, ToolStripDropDownItem item, Action<ReplicateValue> clickHandler, bool includeAll, string checkedValue, ref int iInsert)
        {
            var replicateValues = ReplicateValue.GetGroupableReplicateValues(Document).ToArray();
            if (!replicateValues.Any())
            {
                return;
            }

            item.DropDownItems.Clear();

            if (includeAll)
            {
                item.DropDownItems.Add(new ToolStripMenuItem(Resources.SkylineWindow_AddGroupByMenuItems_All_Replicates,
                        null, (sender, args) => clickHandler(null))
                    { Checked = string.IsNullOrEmpty(checkedValue) });
            }

            foreach (var g in replicateValues)
            {
                var subItem = new ToolStripMenuItem(g.Title, null, (sender, args) => clickHandler(g))
                {
                    Checked = checkedValue == g.ToPersistedString(),
                };

                item.DropDownItems.Add(subItem);
            }

            menuStrip?.Items.Insert(iInsert++, item);
        }

    }
}
