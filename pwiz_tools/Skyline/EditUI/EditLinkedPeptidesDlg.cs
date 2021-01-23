using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.EditUI
{
    public partial class EditLinkedPeptidesDlg : Form
    {
        private BindingList<PeptideRow> _peptideRows;
        private BindingList<CrosslinkRow> _crosslinkRows;
        private PeptideDocNode _originalPeptideDocNode;
        private IDictionary<string, StaticMod> _crosslinkers;
        public EditLinkedPeptidesDlg(SrmSettings settings, PeptideDocNode peptide)
        {
            InitializeComponent();
            SrmSettings = settings;
            _peptideRows = new BindingList<PeptideRow>();
            _crosslinkRows = new BindingList<CrosslinkRow>();
            SetPeptide(peptide);
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

        public void SetPeptide(PeptideDocNode peptideDocNode)
        {
            _originalPeptideDocNode = peptideDocNode;
            _peptideRows.Clear();
            _crosslinkRows.Clear();
            var peptideStructure = peptideDocNode.GetPeptideStructure();
            for (int i = 0; i < peptideStructure.Peptides.Count; i++)
            {
                _peptideRows.Add(new PeptideRow()
                {
                    Sequence =  peptideStructure.Peptides[i].Sequence,
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

        public SrmSettings SrmSettings { get; private set; }

        private void UpdateComboBoxes()
        {
            var longestSequence = _peptideRows.Max(row => row.Sequence?.Length ?? 0);
            var peptideChoices = Enumerable.Range(0, _peptideRows.Count).Select(i => new KeyValuePair<string, int>(
                (i + 1) + @":" + _peptideRows[i].Sequence,
                i)).ToList();
            ReplaceDropdownItems(colPeptide1, peptideChoices);
            ReplaceDropdownItems(colPeptide2, peptideChoices);

            var aaChoices= Enumerable.Range(0, longestSequence)
                .Select(i => MakeAaIndexOption(null, i)).ToList();
            ReplaceDropdownItems(colAminoAcid1, aaChoices);
            ReplaceDropdownItems(colAminoAcid2, aaChoices);
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
                string peptideSequence = null;
                if (peptideIndex >= 0 && peptideIndex < _peptideRows.Count)
                {
                    peptideSequence = _peptideRows[peptideIndex].Sequence;
                }

                if (peptideSequence != null)
                {
                    var items = new List<KeyValuePair<string, int>>();
                    for (int aaIndex = 0; aaIndex < peptideSequence.Length; aaIndex++)
                    {
                        char aa = peptideSequence[aaIndex];
                        if (!string.IsNullOrEmpty(crosslinker?.AAs) && !crosslinker.AAs.Contains(aa))
                        {
                            continue;
                        }

                        items.Add(MakeAaIndexOption(peptideSequence, aaIndex));
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
            if (peptideSequence == null || peptideSequence.Length <= aaIndex)
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
                if (peptideIndex >= 0 && peptideIndex < _peptideRows.Count)
                {
                    peptideSequence = _peptideRows[peptideIndex].Sequence;
                }

                e.Value = MakeAaIndexOption(peptideSequence, aaIndex).Key;
                e.FormattingApplied = true;
            }
        }

        private void dataGridViewLinkedPeptides_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            var rowIndex = e.RowIndex;
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
                    dataGridViewLinkedPeptides.Rows[e.RowIndex].Cells[colPeptideSequence.Index];
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
    }
}
