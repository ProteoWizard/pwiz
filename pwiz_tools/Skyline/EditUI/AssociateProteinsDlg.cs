/*
 * Original author: Yuval Boss <yuval .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class AssociateProteinsDlg : ModeUIInvariantFormEx,  // This dialog has nothing to do with small molecules, always display as proteomic even in mixed mode
                  IAuditLogModifier<AssociateProteinsDlg.AssociateProteinsSettings>
    {
        private readonly SkylineWindow _parent;
        private IList<KeyValuePair<FastaSequence, List<IdentityPath>>> _associatedProteins;
        private bool _isFasta;
        private string _fileName;

        public AssociateProteinsDlg(SkylineWindow parent)
        {
            InitializeComponent();
            _parent = parent;
            _associatedProteins = new List<KeyValuePair<FastaSequence, List<IdentityPath>>>();
        }

        private List<IdentityPath> ListPeptidesForMatching()
        {
            var peptidesForMatching = new List<IdentityPath>();

            var doc = _parent.Document;
            foreach (var nodePepGroup in doc.PeptideGroups)
            {
                // if is already a FastaSequence we don't want to mess with it
                if (nodePepGroup.PeptideGroup is FastaSequence)
                {
                    continue;
                }

                peptidesForMatching.AddRange(nodePepGroup.Peptides.Select(pep =>
                    new IdentityPath(nodePepGroup.PeptideGroup, pep.Peptide)));
            }
            return peptidesForMatching;
        }

        private void btnBackgroundProteomeClick(object sender, EventArgs e)
        {
            UseBackgroundProteome();
        }

        // find matches using the background proteome
        public void UseBackgroundProteome()
        {
            if (_parent.Document.Settings.PeptideSettings.BackgroundProteome.Equals(BackgroundProteome.NONE))
            {
                MessageDlg.Show(this, Resources.AssociateProteinsDlg_UseBackgroundProteome_No_background_proteome_defined);
                return;
            }
            _isFasta = false;
            _fileName = _parent.Document.Settings.PeptideSettings.BackgroundProteome.DatabasePath;
            checkBoxListMatches.Items.Clear();
            BackgroundProteome proteome = _parent.Document.Settings.PeptideSettings.BackgroundProteome;
            var proteinAssociations = new List<KeyValuePair<FastaSequence, List<IdentityPath>>>();
            var peptidesForMatching = ListPeptidesForMatching();

            IDictionary<String, List<Protein>> proteinsWithSequences = null;
            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.PerformWork(this, 1000, longWaitBroker =>
                {
                    using (var proteomeDb = proteome.OpenProteomeDb(longWaitBroker.CancellationToken))
                    {
                        proteinsWithSequences = proteomeDb.GetDigestion()
                            .GetProteinsWithSequences(peptidesForMatching.Select(identityPath => ((Peptide) identityPath.Child).Target.Sequence), longWaitBroker.CancellationToken);
                        if (longWaitBroker.IsCanceled)
                        {
                            proteinsWithSequences = null;
                        }
                    }
                });
            }
            if (proteinsWithSequences == null)
            {
                return;
            }
            HashSet<string> processedProteinSequence = new HashSet<string>();
            foreach (var entry in proteinsWithSequences)
            {
                foreach (var protein in entry.Value)
                {
                    if (!processedProteinSequence.Add(protein.Sequence))
                    {
                        continue;
                    }
                    var matches = peptidesForMatching.Where(pep => protein.Sequence.Contains(GetPeptideSequence(pep))).ToList();
                    if (matches.Count == 0)
                    {
                        continue;
                    }
                    FastaSequence fastaSequence = proteome.MakeFastaSequence(protein);
                    if (fastaSequence != null)
                    {
                        proteinAssociations.Add(new KeyValuePair<FastaSequence, List<IdentityPath>>(fastaSequence, matches));
                    }
                }
            }
            SetCheckBoxListItems(proteinAssociations, 
                Resources.AssociateProteinsDlg_UseBackgroundProteome_No_matches_were_found_using_the_background_proteome_);
            
        }

        private void btnUseFasta_Click(object sender, EventArgs e)
        {
            ImportFasta();
        }

        private string GetPeptideSequence(IdentityPath identityPath)
        {
            return ((Peptide) identityPath.Child).Target.Sequence;
        }

        // prompts user to select a fasta file to use for matching proteins
        public void ImportFasta()
        {
            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Title = Resources.SkylineWindow_ImportFastaFile_Import_FASTA,
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

        // find matches using a FASTA file
        // needed for Testing purposes so we can skip ImportFasta() because of the OpenFileDialog
        public void UseFastaFile(string file)
        {
            _isFasta = true;
            _fileName = file;

            checkBoxListMatches.Items.Clear();
            try
            {
                using (var stream = File.Open(file, FileMode.Open, FileAccess.Read))
                {
                    var proteinAssociations = FindProteinMatchesWithFasta(stream);
                    if (proteinAssociations != null)
                    {
                        SetCheckBoxListItems(proteinAssociations,
                            Resources.AssociateProteinsDlg_FindProteinMatchesWithFasta_No_matches_were_found_using_the_imported_fasta_file_);

                    }
                }
            }
            catch (Exception e)
            {
                MessageDlg.ShowWithException(this, Resources.AssociateProteinsDlg_UseFastaFile_There_was_an_error_reading_from_the_file_, e);
            }
        }

        // creates dictionary of a protein to a list of peptides that match
        private List<KeyValuePair<FastaSequence, List<IdentityPath>>> FindProteinMatchesWithFasta(Stream fastaFile)
        {
            var peptidesForMatching = ListPeptidesForMatching();
            if (peptidesForMatching.Count == 0)
            {
                return new List<KeyValuePair<FastaSequence, List<IdentityPath>>>();
            }

            int maxLength = peptidesForMatching.Max(peptide => GetPeptideSequence(peptide).Length);
            long streamLength = fastaFile.Length;
            var proteinAssociations = new List<Tuple<FastaRecord, List<IdentityPath>>>();
            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.Message = Resources.AssociateProteinsDlg_FindProteinMatchesWithFasta_Finding_peptides_in_FASTA_file;
                longWaitDlg.PerformWork(this, 1000, broker =>
                {
                    var fastaRecords = ParseFastaWithFilePositions(fastaFile);
                    ParallelEx.ForEach(fastaRecords, fastaRecord =>
                    {
                        int progressValue = (int) (fastaRecord.FilePosition * 100 / streamLength);
                        var fasta = fastaRecord.FastaSequence;
                        var substringFinder = new SubstringFinder(fasta.Sequence, maxLength);
                        var matches = new List<IdentityPath>();
                        foreach (var peptide in peptidesForMatching)
                        {
                            // TODO(yuval): does digest matter?
                            if (!substringFinder.ContainsSubstring(GetPeptideSequence(peptide)))
                            {
                                continue;
                            }

                            matches.Add(peptide);
                        }

                        lock (proteinAssociations)
                        {
                            broker.CancellationToken.ThrowIfCancellationRequested();
                            if (progressValue > broker.ProgressValue && progressValue <= 100)
                            {
                                broker.ProgressValue = progressValue;
                            }
                            if (matches.Count > 0)
                            {
                                proteinAssociations.Add(Tuple.Create(fastaRecord, matches));
                            }
                        }
                    });
                });
                if (longWaitDlg.IsCanceled)
                {
                    return null;
                }
            }

            // Protein associations may be out of order because of multi-threading, so put them back in order.
            return proteinAssociations.OrderBy(tuple => tuple.Item1.RecordIndex).Select(tuple =>
                new KeyValuePair<FastaSequence, List<IdentityPath>>(tuple.Item1.FastaSequence, tuple.Item2)).ToList();
        }

        /// <summary>
        /// Returns tuples of FastaData along with the position in the Stream that the fasta record was found.
        /// This method returns an IEnumerable because it is intended to be passed to <see cref="ParallelEx.ForEach{TSource}" />
        /// which starts processing in parallel before the last record has been fetched.
        /// </summary>
        private IEnumerable<FastaRecord> ParseFastaWithFilePositions(Stream stream)
        {
            int index = 0;
            foreach (var fastaData in FastaData.ParseFastaFile(new StreamReader(stream)))
            {
                yield return new FastaRecord(index, stream.Position, new FastaSequence(fastaData.Name, null, null, fastaData.Sequence));
                index++;
            }
        }

        // once proteinAssociation(matches) have been found will populate checkBoxListMatches
        private void SetCheckBoxListItems(IList<KeyValuePair<FastaSequence, List<IdentityPath>>> proteinAssociations, String noMatchFoundMessage)
        {
            if (proteinAssociations.Count > 0) {
                btnApplyChanges.Enabled = true;
            }
            else {
                MessageDlg.Show(this, noMatchFoundMessage);
                btnApplyChanges.Enabled = false;
            }

            checkBoxListMatches.Items.AddRange(proteinAssociations.Select(assoc => assoc.Key.DisplayName).ToArray());
            for (int i = 0; i < checkBoxListMatches.Items.Count; i++)
            {
                checkBoxListMatches.SetItemCheckState(i, CheckState.Checked);
            }
            _associatedProteins = proteinAssociations;
        }

        // Given the current SRMDocument and a dictionary with associated proteins this method will run the the document tree and
        // build a new document.  The new document will contain all pre-existing FastaSequence nodes and will add the newly matches 
        // FastaSequence nodes.  The peptides that were matched to a FastaSequence are removed from their old group.
        private SrmDocument CreateDocTree(SrmDocument current, List<KeyValuePair<FastaSequence, List<IdentityPath>>> proteinAssociations)
        {
            var newPeptideGroups = new List<PeptideGroupDocNode>(); // all groups that will be added in the new document
            var assignedPeptides = proteinAssociations.SelectMany(kvp => kvp.Value).ToHashSet();
            // Modifies and adds old groups that still contain unmatched peptides to newPeptideGroups
            foreach (var nodePepGroup in current.MoleculeGroups) 
            {
                // Adds all pre-existing proteins to list of groups that will be added in the new document
                if (nodePepGroup.PeptideGroup is FastaSequence) 
                {
                    newPeptideGroups.Add(nodePepGroup);
                    continue;
                }

                // Not a protein
                var newNodePepGroup = new List<PeptideDocNode>();

                foreach (PeptideDocNode nodePep in nodePepGroup.Children)
                {
                    var identityPath = new IdentityPath(nodePepGroup.PeptideGroup, nodePep.Peptide);
                    // If any matches contain the PeptideDocNode it no longer needs to be in the group
                    if (!assignedPeptides.Contains(identityPath))
                    {
                        // If PeptideDocNode wasn't matched it will stay in the original group
                        newNodePepGroup.Add(nodePep);
                    }
                }
                // If the count of items in the group has not changed then it can be assumed that the group is the same
                // otherwise if there is a different count and it is not 0 then we want to add the modified group to the
                // set of new groups that will be added to the tree
                if (newNodePepGroup.Count == nodePepGroup.Children.Count)
                {
                    newPeptideGroups.Add(nodePepGroup);  // No change
                }
                else if (newNodePepGroup.Any())
                {
                    newPeptideGroups.Add((PeptideGroupDocNode) nodePepGroup.ChangeChildren(newNodePepGroup.ToArray()));
                }
            }
            
            // Adds all new groups/proteins to newPeptideGroups
            foreach (var keyValuePair in proteinAssociations)
            {
                var protein = keyValuePair.Key;
                var children = new List<PeptideDocNode>();
                foreach (var oldChildPath in keyValuePair.Value)
                {
                    var oldChild = (PeptideDocNode) current.FindNode(oldChildPath);
                    children.Add(oldChild.ChangeFastaSequence(protein));
                }
                var peptideGroupDocNode = new PeptideGroupDocNode(protein, protein.Name, protein.Description, children.ToArray());
                newPeptideGroups.Add(peptideGroupDocNode);
            }

            return (SrmDocument) current.ChangeChildrenChecked(newPeptideGroups.ToArray());
        }

        private void btnApplyChanges_Click(object sender, EventArgs e)
        {
            ApplyChanges();
        }

        public AssociateProteinsSettings FormSettings
        {
            get
            {
                var proteins = checkBoxListMatches.CheckedIndices.OfType<int>()
                    .Select(i => _associatedProteins[i].Key.DisplayName)
                    .ToList();

                var fileName = Path.GetFileName(_fileName);
                return new AssociateProteinsSettings(proteins, _isFasta ? fileName : null, _isFasta ? null : fileName);
            }
        }

        public class AssociateProteinsSettings : AuditLogOperationSettings<AssociateProteinsSettings>, IAuditLogComparable
        {
            public AssociateProteinsSettings(List<string> proteins, string fasta, string backgroundProteome)
            {
                FASTA = fasta;
                BackgroundProteome = backgroundProteome;
                Proteins = proteins;
            }

            protected override AuditLogEntry CreateEntry(SrmDocumentPair docPair)
            {
                var entry = AuditLogEntry.CreateCountChangeEntry(
                    MessageType.associated_peptides_with_protein,
                    MessageType.associated_peptides_with_proteins, docPair.NewDocumentType, Proteins);

                return entry.Merge(base.CreateEntry(docPair));
            }

            [Track]
            public List<string> Proteins { get; private set; }

            [Track]
            public string FASTA { get; private set; }

            [Track]
            public string BackgroundProteome { get; private set; }

            public object GetDefaultObject(ObjectInfo<object> info)
            {
                return new AssociateProteinsSettings(null, null, null);
            }
        }

        // Will only be called if there are changes to apply
        // user cannot click apply button(disabled) without checking any items
        public void ApplyChanges()
        {
            var selectedProteins =
                checkBoxListMatches.CheckedIndices.OfType<int>().Select(i => _associatedProteins[i]).ToList();

            _parent.ModifyDocument(Resources.AssociateProteinsDlg_ApplyChanges_Associated_proteins,
                doc => CreateDocTree(doc, selectedProteins), FormSettings.EntryCreator.Create);

            DialogResult = DialogResult.OK;
        }

        // if no items are checked to match will disable the apply button
        private void checkBoxListMatches_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            // checkBoxListMatches.CheckedItems gets updates after this event is called which
            // is why we have to check the e.NewValue check state
            List<string> checkedItems = new List<string>();
            foreach (var item in checkBoxListMatches.CheckedItems)
                checkedItems.Add(item.ToString());

            if (e.NewValue == CheckState.Unchecked)
                checkedItems.RemoveAt(0);
            if (e.NewValue == CheckState.Checked)
                checkedItems.Add(string.Empty);

            if (checkedItems.Count == 0)
                btnApplyChanges.Enabled = false;
            else
                btnApplyChanges.Enabled = true;
        }


        // required for testing
        public Button ApplyButton { get { return btnApplyChanges; } }
        public CheckedListBox CheckboxListMatches { get { return checkBoxListMatches; } }

        /// <summary>
        /// Contains the fasta sequences read from a fasta file, along with properties indicating
        /// the order that the records were found in the file, and the byte offset where the records
        /// were found. (In theory, "RecordIndex" is redundant, and "FilePosition" could be used to order these
        /// things, but, it's conceivable the fasta parsing code might not be careful about where the stream position
        /// is as each record is returned).
        /// </summary>
        private class FastaRecord
        {
            public FastaRecord(int recordIndex, long filePosition, FastaSequence fastaSequence)
            {
                RecordIndex = recordIndex;
                FilePosition = filePosition;
                FastaSequence = fastaSequence;
            }

            public int RecordIndex { get; }
            public long FilePosition { get; }
            public FastaSequence FastaSequence { get; }
        }
    }
}
