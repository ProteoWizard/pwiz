using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.EditUI
{
    public partial class EditLinkedPeptidesDlg : Form
    {
        private BindingList<PeptideRow> _peptideRows;
        private BindingList<CrosslinkRow> _crosslinkRows;
        private PeptideStructure _originalPeptideStructure;
        private IDictionary<string, StaticMod> _crosslinkers;
        private Tuple<DataGridView, int, DataGridViewColumn> _pendingFocus;
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

            _crosslinkers = AvailableCrosslinkers(settings).ToDictionary(mod => mod.Name);
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
            SetGridFocus(dataGridViewCrosslinks, _crosslinkRows.Count - 1,
                IsSinglePeptide() ? colAminoAcid2 : colPeptide2);
        }

        public SrmSettings SrmSettings { get; private set; }

        private void UpdateComboBoxes()
        {
            var peptideSequences = GetPeptideSequences();
            var longestSequence = peptideSequences .Max(sequence => sequence?.Length ?? 0);
            var peptideChoices = Enumerable.Range(0, peptideSequences.Count).Select(i => new KeyValuePair<string, int>(
                (i + 1) + @":" + peptideSequences[i],
                i)).Prepend(new KeyValuePair<string, int>(string.Empty, -1)).ToList();
            ReplaceDropdownItems(colPeptide1, peptideChoices);
            ReplaceDropdownItems(colPeptide2, peptideChoices);
            colPeptide1.Visible = colPeptide2.Visible = peptideSequences.Count > 1;

            var aaChoices= Enumerable.Range(-1, longestSequence)
                .Select(i => MakeAaIndexOption(null, i)).ToList();
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

        public static IEnumerable<StaticMod> AvailableCrosslinkers(SrmSettings settings)
        {
            var names = new HashSet<string>();
            foreach (var mod in settings.PeptideSettings.Modifications.StaticModifications.Concat(Settings.Default
                .StaticModList))
            {
                if (!names.Add(mod.Name))
                {
                    continue;
                }

                if (null == mod.CrosslinkerSettings)
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
                if (peptideSequence == null)
                {
                    items.Add(MakeAaIndexOption(null, -1));
                }
                else
                {
                    for (int aaIndex = 0; aaIndex < peptideSequence.Length; aaIndex++)
                    {
                        char aa = peptideSequence[aaIndex];
                        if (!string.IsNullOrEmpty(crosslinker?.AAs) && !crosslinker.AAs.Contains(aa))
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

        private void ReplaceItems<TValue>(ComboBox comboBox, IEnumerable<KeyValuePair<string, TValue>> newItems)
        {
            var oldSelectedItem = comboBox.SelectedItem as KeyValuePair<string, TValue>?;
            comboBox.Items.Clear();
            comboBox.Items.AddRange(newItems.Cast<object>().ToArray());
            if (oldSelectedItem != null)
            {
                var newSelectedIndex = IndexOfValue(comboBox.Items, oldSelectedItem.Value.Value);
                if (newSelectedIndex >= 0)
                {
                    comboBox.SelectedIndex = newSelectedIndex;
                }
                else
                {
                    comboBox.Items.Insert(0, oldSelectedItem);
                    comboBox.SelectedIndex = 0;
                }
            }
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
            EditLinkedModifications(e.RowIndex);
        }

        public ExplicitMods ExplicitMods { get; private set; }
        public void OkDialog()
        {
            var peptideSequences = GetPeptideSequences();
            var linkedPeptides = new List<Peptide>();
            var linkedExplicitMods = new List<ExplicitMods>();
            for (int i = 0; i < _peptideRows.Count; i++)
            {
                var peptideRow = _peptideRows[i];
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

            bool singlePeptide = linkedPeptides.Count == 0;
            var crosslinks = new List<Crosslink>();
            for (int i = 0; i < _crosslinkRows.Count; i++)
            {
                var crosslinkRow = _crosslinkRows[i];
                if (crosslinkRow.Crosslinker == null)
                {
                    continue;
                }

                StaticMod crosslinker;
                _crosslinkers.TryGetValue(crosslinkRow.Crosslinker, out crosslinker);
                if (crosslinker == null)
                {
                    MessageDlg.Show(this, string.Format(Resources.MessageBoxHelper_ValidateNameTextBox__0__cannot_be_empty, colCrosslinker.HeaderText));
                    SetGridFocus(dataGridViewCrosslinks, i, colCrosslinker);
                    return;
                }

                var siteTuples = new[]
                {
                    Tuple.Create(new KeyValuePair<int, int>(crosslinkRow.PeptideIndex1, crosslinkRow.AaIndex1), colPeptide1, colAminoAcid1),
                    Tuple.Create(new KeyValuePair<int, int>(crosslinkRow.PeptideIndex2, crosslinkRow.AaIndex2), colPeptide2, colAminoAcid2)
                };

                var sites = new List<CrosslinkSite>();
                foreach (var siteTuple in siteTuples)
                {
                    int peptideIndex = singlePeptide ? 0 : siteTuple.Item1.Key;
                    if (peptideIndex < 0 || peptideIndex >= peptideSequences.Count)
                    {
                        MessageDlg.Show(this, "This peptide is not valid.");
                        SetGridFocus(dataGridViewCrosslinks, i, siteTuple.Item2);
                        return;
                    }

                    var peptideSequence = peptideSequences[peptideIndex];
                    int aaIndex = siteTuple.Item1.Value;
                    if (aaIndex < 0 || aaIndex >= peptideSequence.Length)
                    {
                        MessageDlg.Show(this, "This is not a valid amino acid position in this peptide.");
                        SetGridFocus(dataGridViewCrosslinks, i, siteTuple.Item3);
                        return;
                    }
                    var site = new CrosslinkSite(peptideIndex, aaIndex);
                    if (sites.Contains(site))
                    {
                        MessageDlg.Show(this, "Both ends of this crosslink cannot be the same.");
                        SetGridFocus(dataGridViewCrosslinks, i, siteTuple.Item3);
                        return;
                    }
                    sites.Add(site);
                }
                crosslinks.Add(new Crosslink(crosslinker, sites));
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
                MessageDlg.Show(this, "Some peptides are not connected.");
                return;
            }

            var mainPeptide = _originalPeptideStructure.Peptides[0];
            var mainExplicitMods = _originalPeptideStructure.ExplicitModList[0];
            if (mainExplicitMods == null)
            {
                mainExplicitMods = GetDefaultExplicitMods(mainPeptide, aaIndexesByPeptideIndex[0].ToHashSet());
            }

            ExplicitMods = mainExplicitMods.ChangeCrosslinks(crosslinkStructure);
            DialogResult = DialogResult.OK;
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
            var peptideDocNode = new PeptideDocNode(peptide, peptideRow.ExplicitMods);
            using (var editPepModsDlg = new EditPepModsDlg(SrmSettings, peptideDocNode, false))
            {
                if (editPepModsDlg.ShowDialog(this) == DialogResult.OK)
                {
                    peptideRow.ExplicitMods = editPepModsDlg.ExplicitMods;
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
    }
}
