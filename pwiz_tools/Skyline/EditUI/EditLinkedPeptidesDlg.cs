/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class EditLinkedPeptidesDlg : Form
    {
        private BindingList<PeptideRow> _peptideRows;
        private BindingList<CrosslinkRow> _crosslinkRows;
        private PeptideStructure _originalPeptideStructure;
        private IDictionary<string, StaticMod> _crosslinkers;
        private Tuple<DataGridView, int, DataGridViewColumn> _pendingFocus;
        private Dictionary<Point, string> _crosslinkerErrors;
        public EditLinkedPeptidesDlg(SrmSettings settings, PeptideStructure peptideStructure)
        {
            InitializeComponent();
            SrmSettings = settings;
            _peptideRows = new BindingList<PeptideRow>();
            _crosslinkRows = new BindingList<CrosslinkRow>();
            SetPeptide(peptideStructure);
            _peptideRows.ListChanged += PeptideRows_OnListChanged;
            dataGridViewLinkedPeptides.AutoGenerateColumns = false;
            dataGridViewLinkedPeptides.DataSource = _peptideRows;
            _crosslinkRows.ListChanged += CrosslinkRows_OnListChanged;
            dataGridViewCrosslinks.AutoGenerateColumns = false;
            dataGridViewCrosslinks.DataSource = _crosslinkRows;
            foreach (var column in new[] {colCrosslinker, colPeptide1, colAminoAcid1, colPeptide2, colAminoAcid2})
            {
                column.DisplayMember = @"Key";
                column.ValueMember = @"Value";
            }

            _crosslinkers = AvailableCrosslinkers(settings, peptideStructure).ToDictionary(mod => mod.Name);
            var availableCrosslinkers = _crosslinkers.Values
                .OrderBy(mod => mod.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(mod=>new KeyValuePair<string, string>(mod.Name, mod.Name));
            ReplaceDropdownItems(colCrosslinker, availableCrosslinkers);
            UpdateComboBoxes();
        }

        private void CrosslinkRows_OnListChanged(object sender, ListChangedEventArgs e)
        {
            UpdateComboBoxes();
        }

        private void PeptideRows_OnListChanged(object sender, ListChangedEventArgs e)
        {
            UpdateComboBoxes();
        }

        public void SetPeptide(PeptideStructure peptideStructure)
        {
            _originalPeptideStructure = peptideStructure;
            _peptideRows.Clear();
            _crosslinkRows.Clear();
            tbxPrimaryPeptide.Text = peptideStructure.Peptides[0].Sequence;
            for (int i = 1; i < peptideStructure.Peptides.Count; i++)
            {
                _peptideRows.Add(new PeptideRow()
                {
                    Sequence = peptideStructure.Peptides[i].Sequence,
                    ExplicitMods = peptideStructure.ExplicitModList[i]
                });
            }

            for (int i = 0; i < peptideStructure.Crosslinks.Count; i++)
            {
                var crosslink = peptideStructure.Crosslinks[i];
                if (crosslink.Sites.Count != 2)
                {
                    continue;
                }
                _crosslinkRows.Add(new CrosslinkRow
                {
                    Crosslinker = crosslink.Crosslinker.Name,
                    PeptideIndex1 = crosslink.Sites.Sites[0].PeptideIndex,
                    AaIndex1 = crosslink.Sites.Sites[0].AaIndex,
                    PeptideIndex2 = crosslink.Sites.Sites[1].PeptideIndex,
                    AaIndex2 = crosslink.Sites.Sites[1].AaIndex
                });
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (_pendingFocus != null)
            {
                SetGridFocus(_pendingFocus.Item1, _pendingFocus.Item2, _pendingFocus.Item3);
                _pendingFocus = null;
            }
        }

        public void SelectCrosslink(StaticMod crosslinker, int peptideIndex, int aaIndex)
        {
            for (int i = 0; i < _crosslinkRows.Count; i++)
            {
                var crosslinkRow = _crosslinkRows[i];
                if (crosslinkRow.PeptideIndex1 == peptideIndex && crosslinkRow.AaIndex1 == aaIndex)
                {
                    SetGridFocus(dataGridViewCrosslinks, i, colAminoAcid1);
                }
                else if (crosslinkRow.PeptideIndex2 == peptideIndex && crosslinkRow.AaIndex2 == aaIndex)
                {
                    SetGridFocus(dataGridViewCrosslinks, i, colAminoAcid2);
                }
                else
                {
                    continue;
                }

                if (crosslinker != null)
                {
                    crosslinkRow.Crosslinker = crosslinker.Name;
                }
                return;
            }
            var newCrosslinkRow = new CrosslinkRow
            {
                Crosslinker =  crosslinker?.Name,
                PeptideIndex1 = peptideIndex,
                AaIndex1 = aaIndex
            };
            if (IsSinglePeptide())
            {
                newCrosslinkRow.PeptideIndex1 = 0;
            }
            _crosslinkRows.Add(newCrosslinkRow);
            if (IsSinglePeptide())
            {
                SetGridFocus(dataGridViewLinkedPeptides, 0, colPeptideSequence);
            }
            else
            {
                SetGridFocus(dataGridViewCrosslinks, _crosslinkRows.Count - 1, colPeptide2);
            }
        }

        public SrmSettings SrmSettings { get; private set; }

        private void UpdateComboBoxes()
        {
            var peptideSequences = GetPeptideSequences();
            var longestPeptideSequence = peptideSequences.Max(sequence=>sequence?.Length ?? 0);
            
            var peptideChoices = Enumerable.Range(0, peptideSequences.Count).Select(i => new KeyValuePair<string, int>(
                (i + 1) + @":" + peptideSequences[i],
                i)).Prepend(new KeyValuePair<string, int>(string.Empty, -1)).ToList();
            ReplaceDropdownItems(colPeptide1, peptideChoices);
            ReplaceDropdownItems(colPeptide2, peptideChoices);
            colPeptide1.Visible = colPeptide2.Visible = peptideSequences.Count > 1;

            var aaChoices = Enumerable.Range(0, longestPeptideSequence)
                .Concat(_crosslinkRows.SelectMany(row => row.ListSites().Select(site => site.Value))).Append(-1)
                .Distinct().OrderBy(aaIndex => aaIndex)
                .Select(i=>MakeAaIndexOption(null, i)).ToList();
            ReplaceDropdownItems(colAminoAcid1, aaChoices);
            ReplaceDropdownItems(colAminoAcid2, aaChoices);
        }

        public IList<string> GetPeptideSequences()
        {
            var list = new List<string> {_originalPeptideStructure.Peptides[0].Sequence};
            list.AddRange(_peptideRows.Select(row => row.Sequence ?? string.Empty));
            while (string.IsNullOrEmpty(list[list.Count - 1]))
            {
                list.RemoveAt(list.Count - 1);
            }
            return list;
        }

        public bool IsSinglePeptide()
        {
            return _peptideRows.All(row => string.IsNullOrEmpty(row.Sequence));
        }

        private void ReplaceDropdownItems<TValue>(DataGridViewComboBoxColumn column,
            IEnumerable<KeyValuePair<string, TValue>> items)
        {
            var itemList = items.ToList();
            if (!itemList.Any(item => Equals(item.Value, default(TValue))))
            {
                itemList.Add(new KeyValuePair<string, TValue>(string.Empty, default(TValue)));
            }
            if (itemList.Cast<object>().SequenceEqual(column.Items.Cast<object>()))
            {
                return;
            }
            column.Items.Clear();
            column.Items.AddRange(itemList.Cast<object>().ToArray());
        }

        public class PeptideRow
        {
            public string Sequence { get; set; }
            public ExplicitMods ExplicitMods { get; set; }
        }

        public class CrosslinkRow
        {
            public CrosslinkRow()
            {
                PeptideIndex1 = -1;
                PeptideIndex2 = -1;
                AaIndex1 = -1;
                AaIndex2 = -1;
            }
            public string Crosslinker { get; set; }
            public int PeptideIndex1 { get; set; }
            public int AaIndex1 { get; set; }
            public int PeptideIndex2 { get; set; }
            public int AaIndex2 { get; set; }

            public IReadOnlyList<KeyValuePair<int, int>> ListSites()
            {
                return ImmutableList.ValueOf(new[]
                {
                    new KeyValuePair<int, int>(PeptideIndex1, AaIndex1),
                    new KeyValuePair<int, int>(PeptideIndex2, AaIndex2)
                });
            }
        }

        private void dataGridViewLinkedPeptides_CellErrorTextNeeded(object sender, DataGridViewCellErrorTextNeededEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _peptideRows.Count)
            {
                return;
            }

            var peptideRow = _peptideRows[e.RowIndex];
            if (e.ColumnIndex == colPeptideSequence.Index)
            {
                if (string.IsNullOrEmpty(peptideRow.Sequence) && e.RowIndex == _peptideRows.Count - 1)
                {
                    return;
                }
                try
                {
                    FastaSequence.ValidateSequence(peptideRow.Sequence);
                    e.ErrorText = null;
                }
                catch (Exception ex)
                {
                    e.ErrorText = ex.Message;
                }
            }
        }

        public static IEnumerable<StaticMod> AvailableCrosslinkers(SrmSettings settings, PeptideStructure peptideStructure)
        {
            var names = new HashSet<string>();
            var crosslinkers = settings.PeptideSettings.Modifications.StaticModifications
                .Concat(Settings.Default.StaticModList)
                .Where(mod => mod.IsCrosslinker);
            // Also, include any non-crosslinker modifications which are currently being used as a crosslinker in this peptide
            crosslinkers = crosslinkers.Concat(peptideStructure.Crosslinks.Select(link => link.Crosslinker));
            foreach (var mod in crosslinkers)
            {
                if (!names.Add(mod.Name))
                {
                    continue;
                }

                yield return mod;
            }
        }

        private void dataGridViewCrosslinks_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            var comboBoxControl = e.Control as ComboBox;
            if (comboBoxControl == null)
            {
                return;
            }

            var rowIndex = dataGridViewCrosslinks.CurrentCellAddress.Y;
            if (rowIndex < 0 || rowIndex >= _crosslinkRows.Count)
            {
                return;
            }

            var crosslinkRow = _crosslinkRows[rowIndex];
            StaticMod crosslinker = null;
            if (null != crosslinkRow.Crosslinker)
            {
                _crosslinkers.TryGetValue(crosslinkRow.Crosslinker, out crosslinker);
            }
            var columnIndex = dataGridViewCrosslinks.CurrentCellAddress.X;
            if (columnIndex == colAminoAcid1.Index || columnIndex == colAminoAcid2.Index)
            {
                int peptideIndex, chosenAaIndex;
                if (columnIndex == colAminoAcid1.Index)
                {
                    peptideIndex = crosslinkRow.PeptideIndex1;
                    chosenAaIndex = crosslinkRow.AaIndex1;
                }
                else
                {
                    peptideIndex = crosslinkRow.PeptideIndex2;
                    chosenAaIndex = crosslinkRow.AaIndex2;
                }

                var peptideList = GetPeptideSequences();
                string peptideSequence = null;
                if (peptideList.Count == 1)
                {
                    peptideSequence = peptideList[0];
                }
                else if (peptideIndex >= 0 && peptideIndex < peptideList.Count)
                {
                    peptideSequence = peptideList[peptideIndex];
                }

                var items = new List<KeyValuePair<string, int>>();
                if (peptideSequence == null || crosslinker == null)
                {
                    items.Add(MakeAaIndexOption(null, -1));
                }
                else
                {
                    for (int aaIndex = 0; aaIndex < peptideSequence.Length; aaIndex++)
                    {
                        char aa = peptideSequence[aaIndex];
                        if (!string.IsNullOrEmpty(crosslinker.AAs) && !crosslinker.AAs.Contains(aa))
                        {
                            continue;
                        }

                        items.Add(MakeAaIndexOption(peptideSequence, aaIndex));
                    }
                }


                int selectedIndex = IndexOfValue(items, chosenAaIndex);
                if (selectedIndex < 0)
                {
                    items.Insert(0, MakeAaIndexOption(peptideSequence, chosenAaIndex));
                    selectedIndex = 0;
                }
                comboBoxControl.Items.Clear();
                comboBoxControl.Items.AddRange(items.Cast<object>().ToArray());
                comboBoxControl.SelectedIndex = selectedIndex;
            }
        }



        private void dataGridViewCrosslinks_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            MessageDlg.ShowWithException(this, e.Exception.Message, e.Exception);
        }

        private int IndexOfValue<TValue>(IEnumerable enumerable, TValue value)
        {
            int index = 0;
            foreach (var item in enumerable)
            {
                if (item is KeyValuePair<string, TValue> kvp)
                {
                    if (Equals(kvp.Value, value))
                    {
                        return index;
                    }
                }
                index++;
            }

            return -1;
        }

        public static KeyValuePair<string, int> MakeAaIndexOption(string peptideSequence, int aaIndex)
        {
            string displayText;
            if (aaIndex == -1)
            {
                displayText = "";
            }
            else if (peptideSequence == null || peptideSequence.Length <= aaIndex)
            {
                displayText = @"<" + (aaIndex + 1) + @">";
            }
            else
            {
                displayText = (aaIndex + 1) + @":" + peptideSequence[aaIndex];
            }
            return new KeyValuePair<string, int>(displayText, aaIndex);
        }

        private void dataGridViewCrosslinks_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _crosslinkRows.Count)
            {
                return;
            }

            var crosslinkRow = _crosslinkRows[e.RowIndex];
            if (e.ColumnIndex == colAminoAcid1.Index || e.ColumnIndex == colAminoAcid2.Index)
            {
                int peptideIndex, aaIndex;
                var peptideSequences = GetPeptideSequences();
                if (e.ColumnIndex == colAminoAcid1.Index)
                {
                    peptideIndex = crosslinkRow.PeptideIndex1;
                    aaIndex = crosslinkRow.AaIndex1;
                }
                else
                {
                    peptideIndex = crosslinkRow.PeptideIndex2;
                    aaIndex = crosslinkRow.AaIndex2;
                }
                string peptideSequence = null;
                if (peptideSequences.Count == 1)
                {
                    peptideSequence = peptideSequences[0];
                }
                else if (peptideIndex >= 0 && peptideIndex < peptideSequences.Count)
                {
                    peptideSequence = peptideSequences[peptideIndex];
                }

                e.Value = MakeAaIndexOption(peptideSequence, aaIndex).Key;
                e.FormattingApplied = true;
            }
        }

        private void dataGridViewLinkedPeptides_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == colModificationsButton.Index)
            {
                EditLinkedModifications(e.RowIndex);
            }
        }

        public ExplicitMods ExplicitMods { get; private set; }
        public void OkDialog()
        {
            var linkedPeptides = new List<Peptide>();
            var linkedExplicitMods = new List<ExplicitMods>();
            for (int i = 0; i < _peptideRows.Count; i++)
            {
                var peptideRow = _peptideRows[i];
                if (string.IsNullOrEmpty(peptideRow.Sequence))
                {
                    if (i == _peptideRows.Count - 1)
                    {
                        // ignore the last row if it is blank
                        continue;
                    }
                    MessageDlg.Show(this, Resources.PasteDlg_ListPeptideSequences_The_peptide_sequence_cannot_be_blank);
                    SetGridFocus(dataGridViewLinkedPeptides, i, colPeptideSequence);
                    return;
                }
                Peptide peptide;
                try
                {
                    peptide = new Peptide(peptideRow.Sequence);
                }
                catch (Exception ex)
                {
                    MessageDlg.ShowWithException(this, ex.Message, ex);
                    SetGridFocus(dataGridViewLinkedPeptides, i, colPeptideSequence);
                    return;
                }

                linkedPeptides.Add(peptide);
                linkedExplicitMods.Add(peptideRow.ExplicitMods);
            }

            var crosslinks = new List<Crosslink>();
            var allSites = new HashSet<CrosslinkSite>();
            for (int i = 0; i < _crosslinkRows.Count; i++)
            {
                List<ColumnMessage> errors = new List<ColumnMessage>();
                var crosslink = MakeCrosslink(allSites, _crosslinkRows[i], errors);
                if (errors.Count > 0)
                {
                    MessageDlg.Show(this, errors[0].Message);
                    SetGridFocus(dataGridViewCrosslinks, i, errors[0].Column);
                    return;
                }

                if (crosslink != null)
                {
                    crosslinks.Add(crosslink);
                }
            }

            var aaIndexesByPeptideIndex = crosslinks.SelectMany(link => link.Sites).Distinct()
                .ToLookup(site => site.PeptideIndex, site => site.AaIndex);
            for (int i = 0; i < linkedExplicitMods.Count; i++)
            {
                if (null == linkedExplicitMods[i])
                {
                    linkedExplicitMods[i] =
                        GetDefaultExplicitMods(linkedPeptides[i], aaIndexesByPeptideIndex[i + 1].ToHashSet());
                }
            }
            var crosslinkStructure = new CrosslinkStructure(linkedPeptides, linkedExplicitMods, crosslinks);
            if (!crosslinkStructure.IsConnected())
            {
                MessageDlg.Show(this, Resources.EditLinkedPeptidesDlg_OkDialog_Some_crosslinked_peptides_are_not_connected_);
                return;
            }

            var mainPeptide = _originalPeptideStructure.Peptides[0];
            var mainExplicitMods = _originalPeptideStructure.ExplicitModList[0];
            if (mainExplicitMods == null)
            {
                mainExplicitMods = GetDefaultExplicitMods(mainPeptide, aaIndexesByPeptideIndex[0].ToHashSet());
            }

            ExplicitMods = RemoveModificationsOnCrosslinks(mainExplicitMods.ChangeCrosslinkStructure(crosslinkStructure));
            DialogResult = DialogResult.OK;
        }

        private ExplicitMods RemoveModificationsOnCrosslinks(ExplicitMods explicitMods)
        {
            var originalCrosslinkSites =
                _originalPeptideStructure.Crosslinks.SelectMany(link => link.Sites).ToHashSet();
            var newCrosslinkSites = explicitMods.CrosslinkStructure.Crosslinks.SelectMany(link => link.Sites)
                .Except(originalCrosslinkSites).ToHashSet();
            explicitMods = RemoveStaticModsFromSites(explicitMods, 0, newCrosslinkSites);
            var peptideStructure = explicitMods.GetPeptideStructure();
            var newLinkedExplicitMods = new List<ExplicitMods>();
            for (int peptideIndex = 1; peptideIndex < peptideStructure.Peptides.Count; peptideIndex++)
            {
                newLinkedExplicitMods.Add(RemoveStaticModsFromSites(peptideStructure.ExplicitModList[peptideIndex], peptideIndex, newCrosslinkSites));
            }

            if (!ArrayUtil.ReferencesEqual(explicitMods.CrosslinkStructure.LinkedExplicitMods, newLinkedExplicitMods))
            {
                explicitMods = explicitMods.ChangeCrosslinkStructure(new CrosslinkStructure(explicitMods.CrosslinkStructure.LinkedPeptides,
                    newLinkedExplicitMods, explicitMods.CrosslinkStructure.Crosslinks));
            }

            return explicitMods;
        }

        private static ExplicitMods RemoveStaticModsFromSites(ExplicitMods explicitMods, int peptideIndex,
            HashSet<CrosslinkSite> sites)
        {
            if (explicitMods?.StaticModifications == null)
            {
                return explicitMods;
            }

            var newStaticMods = explicitMods.StaticModifications.Where(mod => !sites.Contains(new CrosslinkSite(peptideIndex, mod.IndexAA))).ToList();
            if (newStaticMods.Count == explicitMods.StaticModifications.Count)
            {
                return explicitMods;
            }

            return explicitMods.ChangeStaticModifications(newStaticMods);
        }

        

        public void SetGridFocus(DataGridView dataGridView, int rowIndex, DataGridViewColumn column)
        {
            if (IsHandleCreated)
            {
                dataGridView.CurrentCell = dataGridView.Rows[rowIndex].Cells[column.Index];
                dataGridView.Focus();
            }
            else
            {
                _pendingFocus = Tuple.Create(dataGridView, rowIndex, column);
            }
        }

        public ExplicitMods GetDefaultExplicitMods(Peptide peptide, ICollection<int> crosslinkedAaIndexes)
        {
            var modifiedSequence = ModifiedSequence.GetModifiedSequence(SrmSettings, peptide.Sequence, null, IsotopeLabelType.light);
            var staticMods = modifiedSequence.ExplicitMods.Where(mod => !crosslinkedAaIndexes.Contains(mod.IndexAA))
                .Select(mod=>mod.ExplicitMod)
                .ToList();
            return new ExplicitMods(peptide, staticMods, null);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void EditLinkedModifications(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _peptideRows.Count)
            {
                return;
            }

            var peptideRow = _peptideRows[rowIndex];
            Peptide peptide;
            try
            {
                peptide = new Peptide(peptideRow.Sequence);
            }
            catch (Exception ex)
            {
                MessageDlg.ShowWithException(this, ex.Message, ex);
                dataGridViewLinkedPeptides.CurrentCell =
                    dataGridViewLinkedPeptides.Rows[rowIndex].Cells[colPeptideSequence.Index];
                return;
            }

            int peptideIndex = rowIndex + 1;
            var crosslinkedSites = new Dictionary<int, StaticMod>();
            foreach (var row in _crosslinkRows)
            {
                StaticMod crosslinker;
                if (string.IsNullOrEmpty(row.Crosslinker) || !_crosslinkers.TryGetValue(row.Crosslinker, out crosslinker))
                {
                    continue;
                }

                foreach (var siteKvp in row.ListSites().Where(kvp=>kvp.Key == peptideIndex))
                {
                    int aaIndex = siteKvp.Value;
                    if (crosslinkedSites.ContainsKey(aaIndex) || !crosslinker.IsApplicableCrosslink(peptide.Sequence, aaIndex))
                    {
                        continue;
                    }
                    crosslinkedSites.Add(aaIndex, crosslinker);
                }
            }

            var explicitMods = peptideRow.ExplicitMods;
            if (explicitMods == null)
            {
                explicitMods = GetDefaultExplicitMods(peptide, crosslinkedSites.Keys);
            }

            explicitMods = explicitMods.ChangeStaticModifications((explicitMods.StaticModifications ?? new ExplicitMod[0])
                .Where(mod => !crosslinkedSites.ContainsKey(mod.IndexAA)).ToList());
            explicitMods = explicitMods.ChangeCrosslinkStructure(new CrosslinkStructure(new Peptide[0],
                crosslinkedSites.Select(kvp =>
                    new Crosslink(kvp.Value, ImmutableList.Singleton(new CrosslinkSite(0, kvp.Key))))));


            var peptideDocNode = new PeptideDocNode(peptide, explicitMods);
            using (var editPepModsDlg = new EditPepModsDlg(SrmSettings, peptideDocNode, false))
            {
                if (editPepModsDlg.ShowDialog(this) == DialogResult.OK)
                {
                    peptideRow.ExplicitMods = editPepModsDlg.ExplicitMods?.ChangeCrosslinkStructure(null);
                }
            }
        }

        public DataGridView LinkedPeptidesGrid
        {
            get
            {
                return dataGridViewLinkedPeptides;
            }
        }

        public DataGridViewTextBoxColumn PeptideSequenceColumn
        {
            get { return colPeptideSequence; }
        }

        public DataGridView CrosslinksGrid
        {
            get
            {
                return dataGridViewCrosslinks;
            }
        }

        public DataGridViewComboBoxColumn CrosslinkerColumn
        {
            get
            {
                return colCrosslinker;
            }
        }

        public DataGridViewComboBoxColumn GetPeptideIndexColumn(int siteIndex)
        {
            switch (siteIndex)
            {
                case 0:
                    return colPeptide1;
                case 1:
                    return colPeptide2;
            }
            throw new ArgumentOutOfRangeException(nameof(siteIndex));
        }

        public DataGridViewComboBoxColumn GetAaIndexColumn(int siteIndex)
        {
            switch (siteIndex)
            {
                case 0:
                    return colAminoAcid1;
                case 1:
                    return colAminoAcid2;
            }
            throw new ArgumentOutOfRangeException(nameof(siteIndex));
        }

        private void dataGridViewCrosslinks_CellErrorTextNeeded(object sender, DataGridViewCellErrorTextNeededEventArgs e)
        {
            string errorMessage = null;
            _crosslinkerErrors?.TryGetValue(new Point(e.RowIndex, e.ColumnIndex), out errorMessage);
            e.ErrorText = errorMessage;
        }

        public Crosslink MakeCrosslink(HashSet<CrosslinkSite> allSites, CrosslinkRow crosslinkRow, List<ColumnMessage> errors)
        {
            StaticMod crosslinker = null;
            if (string.IsNullOrEmpty(crosslinkRow.Crosslinker))
            {
                return null;
            }
            _crosslinkers.TryGetValue(crosslinkRow.Crosslinker, out crosslinker);
            if (crosslinker == null)
            {
                errors.Add(new ColumnMessage(colCrosslinker, Resources.EditLinkedPeptidesDlg_MakeCrosslink_Invalid_crosslinker));
            }

            var siteTuples = new[]
            {
                Tuple.Create(new KeyValuePair<int, int>(crosslinkRow.PeptideIndex1, crosslinkRow.AaIndex1), colPeptide1, colAminoAcid1),
                Tuple.Create(new KeyValuePair<int, int>(crosslinkRow.PeptideIndex2, crosslinkRow.AaIndex2), colPeptide2, colAminoAcid2)
            };

            var peptideSequences = GetPeptideSequences();
            bool singlePeptide = peptideSequences.Count == 1;
            var sites = new List<CrosslinkSite>();
            foreach (var siteTuple in siteTuples)
            {
                int peptideIndex = singlePeptide ? 0 : siteTuple.Item1.Key;
                if (peptideIndex < 0 || peptideIndex >= peptideSequences.Count)
                {
                    errors.Add(new ColumnMessage(siteTuple.Item2, Resources.EditLinkedPeptidesDlg_MakeCrosslink_This_peptide_is_not_valid_));
                    continue;
                }

                var peptideSequence = peptideSequences[peptideIndex];
                int aaIndex = siteTuple.Item1.Value;
                if (aaIndex < 0)
                {
                    errors.Add(new ColumnMessage(siteTuple.Item3, Resources.EditLinkedPeptidesDlg_MakeCrosslink_This_amino_acid_position_cannot_be_blank_));
                    continue;
                }
                if (aaIndex >= peptideSequence.Length)
                {
                    errors.Add(new ColumnMessage(siteTuple.Item3, Resources.EditLinkedPeptidesDlg_MakeCrosslink_This_is_not_a_valid_amino_acid_position_in_this_peptide_));
                    continue;
                }

                if (crosslinker != null && !crosslinker.IsApplicableCrosslink(peptideSequence, aaIndex))
                {
                    errors.Add(new ColumnMessage(siteTuple.Item3, string.Format(Resources.EditLinkedPeptidesDlg_MakeCrosslink_The_crosslinker___0___cannot_attach_to_this_amino_acid_position_, crosslinker.Name)));
                }

                if (errors.Count == 0)
                {
                    var site = new CrosslinkSite(peptideIndex, aaIndex);
                    if (sites.Contains(site))
                    {
                        errors.Add(new ColumnMessage(siteTuple.Item3, Resources.EditLinkedPeptidesDlg_MakeCrosslink_Both_ends_of_this_crosslink_cannot_be_the_same_));
                    }
                    sites.Add(site);
                    if (!allSites.Add(site))
                    {
                        errors.Add(new ColumnMessage(siteTuple.Item3, Resources.EditLinkedPeptidesDlg_MakeCrosslink_This_amino_acid_position_in_this_peptide_is_already_being_used_by_another_crosslink_));
                    }
                }
            }

            if (errors.Count == 0 && crosslinker != null && sites.Count == 2)
            {
                return new Crosslink(crosslinker, sites);
            }

            return null;
        }

        public struct ColumnMessage
        {
            public ColumnMessage(DataGridViewColumn column, string message)
            {
                Column = column;
                Message = message;
            }

            public DataGridViewColumn Column { get; }
            public string Message { get; }
        }

        private void UpdateCrosslinkerErrors()
        {
            var newErrors = new Dictionary<Point, string>();
            var allSites = new HashSet<CrosslinkSite>();
            for (int iRow = 0; iRow < _crosslinkRows.Count; iRow++)
            {
                var errors = new List<ColumnMessage>();
                MakeCrosslink(allSites, _crosslinkRows[iRow], errors);
                foreach (var error in errors)
                {
                    Point cellAddress = new Point(iRow, error.Column.Index);
                    if (!newErrors.ContainsKey(cellAddress))
                    {
                        newErrors.Add(cellAddress, error.Message);
                    }
                }
            }

            if (_crosslinkerErrors != null)
            {
                if (_crosslinkerErrors.Count == newErrors.Count && !_crosslinkerErrors.Except(newErrors).Any())
                {
                    return;
                }
            }

            _crosslinkerErrors = newErrors;
            dataGridViewCrosslinks.Invalidate();
        }

        private void dataGridViewCrosslinks_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            UpdateCrosslinkerErrors();
        }

        private void dataGridViewLinkedPeptides_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            UpdateCrosslinkerErrors();
        }

        private void dataGridViewLinkedPeptides_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.ColumnIndex == colModificationsButton.Index)
            {
                e.ToolTipText = Resources.EditLinkedPeptidesDlg_dataGridViewLinkedPeptides_CellToolTipTextNeeded_Edit_Modifications;
            }
        }
    }
}
