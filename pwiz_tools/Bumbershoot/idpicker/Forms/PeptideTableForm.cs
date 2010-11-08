//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using DigitalRune.Windows.Docking;
using NHibernate.Linq;
using IDPicker.DataModel;
using BrightIdeasSoftware;

namespace IDPicker.Forms
{
    public partial class PeptideTableForm : DockableForm
    {
        public TreeListView TreeListView { get { return treeListView; } }

        public class PeptideRow
        {
            public DataModel.Peptide Peptide { get; private set; }
            public long DistinctMatchesWithRoundedMass { get; private set; }
            public long Spectra { get; private set; }

            #region Constructor
            public PeptideRow (object[] queryRow)
            {
                Peptide = (DataModel.Peptide) queryRow[0];
                DistinctMatchesWithRoundedMass = (long) queryRow[1];
                Spectra = (long) queryRow[2];
            }
            #endregion
        }

        public class PeptideSpectrumMatchRow
        {
            public DataModel.PeptideSpectrumMatch PeptideSpectrumMatch { get; private set; }
            public long Spectra { get; private set; }
            public DistinctPeptideFormat DistinctPeptide { get; private set; }

            #region Constructor
            public PeptideSpectrumMatchRow (object[] queryRow)
            {
                PeptideSpectrumMatch = (DataModel.PeptideSpectrumMatch) queryRow[0];
                DistinctPeptide = new DistinctPeptideFormat("(psm.Peptide || ' ' || ROUND(psm.MonoisotopicMass))",
                                                            PeptideSpectrumMatch.ToModifiedString(),
                                                            (string) queryRow[1]);
                Spectra = (long) queryRow[2];
            }
            #endregion
        }

        public class PeptideInstanceRow
        {
            public DataModel.PeptideInstance PeptideInstance { get; private set; }

            #region Constructor
            public PeptideInstanceRow (object queryRow)
            {
                PeptideInstance = (DataModel.PeptideInstance) queryRow;
            }
            #endregion
        }

        Dictionary<OLVColumn, object[]> _columnSettings;
        bool _skipRebuild = false;

        public PeptideTableForm ()
        {
            InitializeComponent();

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                e.Cancel = true;
                DockState = DockState.DockBottomAutoHide;
            };

            Text = TabText = "Peptide View";

            #region Column aspect getters
            var allLayouts = new List<string>(Util.StringCollectionToStringArray(Properties.Settings.Default.PeptideTableFormSettings));
            _columnSettings = new Dictionary<OLVColumn, object[]>();

            if (allLayouts.Count > 1)
            {
                var retrievedList = allLayouts[1].Split(System.Environment.NewLine.ToCharArray()).ToList();
                if (retrievedList.Count == 11)
                {
                    SetPropertyFromUserSettings(ref _columnSettings, sequenceColumn, retrievedList);
                    SetPropertyFromUserSettings(ref _columnSettings, distinctMatchesColumn, retrievedList);
                    SetPropertyFromUserSettings(ref _columnSettings, filteredSpectraColumn, retrievedList);
                    SetPropertyFromUserSettings(ref _columnSettings, monoisotopicMassColumn, retrievedList);
                    SetPropertyFromUserSettings(ref _columnSettings, molecularWeightColumn, retrievedList);
                    SetPropertyFromUserSettings(ref _columnSettings, offsetColumn, retrievedList);
                    SetPropertyFromUserSettings(ref _columnSettings, terminalSpecificityColumn, retrievedList);
                    SetPropertyFromUserSettings(ref _columnSettings, missedCleavagesColumn, retrievedList);
                    SetPropertyFromUserSettings(ref _columnSettings, proteinsColumn, retrievedList);

                    treeListView.BackColor = Color.FromArgb(int.Parse(retrievedList[9].ToString()));
                    treeListView.ForeColor = Color.FromArgb(int.Parse(retrievedList[10].ToString()));
                }
                else
                    SetDefaults();
            }
            else
                SetDefaults();

            SetColumnAspectGetters();

            #endregion

            treeListView.UseCellFormatEvents = true;
            treeListView.FormatCell += delegate(object sender, FormatCellEventArgs currentCell)
            {
                if (currentCell.Item.RowObject is PeptideRow &&
                    (viewFilter.Peptide != null && viewFilter.Peptide.Id != (currentCell.Item.RowObject as PeptideRow).Peptide.Id ||
                     viewFilter.DistinctPeptideKey != null))
                    currentCell.SubItem.ForeColor = SystemColors.GrayText;
                else if (currentCell.Item.RowObject is PeptideSpectrumMatchRow &&
                         viewFilter.DistinctPeptideKey != null && viewFilter.DistinctPeptideKey.Sequence != (currentCell.Item.RowObject as PeptideSpectrumMatchRow).DistinctPeptide.Sequence)
                    currentCell.SubItem.ForeColor = SystemColors.GrayText;

                currentCell.SubItem.BackColor = (Color)_columnSettings[currentCell.Column][2];
            };

            treeListView.CellClick += new EventHandler<CellClickEventArgs>(treeListView_CellClick);

            radioButton1.CheckedChanged += new EventHandler(radioButton1_CheckedChanged);
        }

        private void SetDefaults()
        {
            _columnSettings.Add(sequenceColumn, new object[5] { "Key", -99, treeListView.BackColor, false, false });
            _columnSettings.Add(distinctMatchesColumn, new object[5] { "Integer", -99, treeListView.BackColor, false, false });
            _columnSettings.Add(filteredSpectraColumn, new object[5] { "Integer", -99, treeListView.BackColor, false, false });
            _columnSettings.Add(monoisotopicMassColumn, new object[5] { "Float", -1, treeListView.BackColor, false, false });
            _columnSettings.Add(molecularWeightColumn, new object[5] { "Float", -1, treeListView.BackColor, false, false });
            _columnSettings.Add(offsetColumn, new object[5] { "Integer", -99, treeListView.BackColor, false, false });
            _columnSettings.Add(terminalSpecificityColumn, new object[5] { "String", -99, treeListView.BackColor, false, false });
            _columnSettings.Add(missedCleavagesColumn, new object[5] { "Integer", -99, treeListView.BackColor, false, false });
            _columnSettings.Add(proteinsColumn, new object[5] { "String", -99, treeListView.BackColor, false, false });
        }

        private void SetColumnAspectGetters()
        {
            sequenceColumn.AspectGetter = null;
            distinctMatchesColumn.AspectGetter = null;
            filteredSpectraColumn.AspectGetter = null;
            monoisotopicMassColumn.AspectGetter = null;
            molecularWeightColumn.AspectGetter = null;
            offsetColumn.AspectGetter = null;
            terminalSpecificityColumn.AspectGetter = null;
            missedCleavagesColumn.AspectGetter = null;

            sequenceColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideRow)
                    return (x as PeptideRow).Peptide.Sequence;
                else if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).DistinctPeptide.Sequence;
                else if (x is PeptideInstanceRow)
                    return (x as PeptideInstanceRow).PeptideInstance.Protein.Accession;
                return null;
            };

            distinctMatchesColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideRow)
                    return (x as PeptideRow).DistinctMatchesWithRoundedMass;
                return null;
            };

            filteredSpectraColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideRow)
                    return (x as PeptideRow).Spectra;
                else if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).Spectra;
                return null;
            };

            if ((int)_columnSettings[monoisotopicMassColumn][1] == -1)
            {
                monoisotopicMassColumn.AspectGetter += delegate(object x)
                {
                    if (x is PeptideRow)
                        return (x as PeptideRow).Peptide.MonoisotopicMass;
                    else if (x is PeptideSpectrumMatchRow)
                        return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MonoisotopicMass;
                    return null;
                };
            }
            else
            {
                monoisotopicMassColumn.AspectGetter += delegate(object x)
                {
                    if (x is PeptideRow)
                        return Math.Round((x as PeptideRow).Peptide.MonoisotopicMass,(int)_columnSettings[monoisotopicMassColumn][1]);
                    else if (x is PeptideSpectrumMatchRow)
                        return Math.Round((x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MonoisotopicMass, (int)_columnSettings[monoisotopicMassColumn][1]);
                    return null;
                };
            }

            if ((int)_columnSettings[molecularWeightColumn][1] == -1)
            {
                molecularWeightColumn.AspectGetter += delegate(object x)
                {
                    if (x is PeptideRow)
                        return (x as PeptideRow).Peptide.MolecularWeight;
                    else if (x is PeptideSpectrumMatchRow)
                        return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MolecularWeight;
                    return null;
                };
            }
            else
            {
                molecularWeightColumn.AspectGetter += delegate(object x)
                {
                    if (x is PeptideRow)
                        return Math.Round((x as PeptideRow).Peptide.MolecularWeight,(int)_columnSettings[molecularWeightColumn][1]);
                    else if (x is PeptideSpectrumMatchRow)
                        return Math.Round((x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MolecularWeight,(int)_columnSettings[molecularWeightColumn][1]);
                    return null;
                };
            }


            offsetColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideInstanceRow)
                    return (x as PeptideInstanceRow).PeptideInstance.Offset;
                return null;
            };

            terminalSpecificityColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideInstanceRow)
                {
                    var specificTermini = new List<string>();
                    if ((x as PeptideInstanceRow).PeptideInstance.NTerminusIsSpecific)
                        specificTermini.Add("N");
                    if ((x as PeptideInstanceRow).PeptideInstance.CTerminusIsSpecific)
                        specificTermini.Add("C");
                    if (specificTermini.Count == 0)
                        specificTermini.Add("None");
                    return String.Join(",", specificTermini.ToArray());
                }
                return null;
            };

            missedCleavagesColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideInstanceRow)
                    return (x as PeptideInstanceRow).PeptideInstance.MissedCleavages;
                return null;
            };

            proteinsColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideRow)
                    return String.Join(";", (x as PeptideRow).Peptide.Instances.Select(o => o.Protein.Accession).Distinct().ToArray());
                return null;
            };

            treeListView.CanExpandGetter += delegate(object x) { return x is PeptideRow; };
            treeListView.ChildrenGetter += delegate(object x)
            {
                var childFilter = new DataFilter(dataFilter) { Peptide = (x as PeptideRow).Peptide };

                object result;
                if (radioButton1.Checked)
                    result = session.CreateQuery("SELECT psm, (psm.Peptide || ' ' || ROUND(psm.MonoisotopicMass)), COUNT(DISTINCT psm.Spectrum) " +
                                                 childFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                                 "GROUP BY (psm.Peptide || ' ' || ROUND(psm.MonoisotopicMass))")
                                    .List<object[]>().Select(o => new PeptideSpectrumMatchRow(o));
                else
                    return session.CreateQuery("SELECT DISTINCT psm.Peptide.Instances " +
                                               childFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch)/* +
                                               " GROUP BY psm.Peptide"*/
                                                                        )
                                  .List<object>().Select(o => new PeptideInstanceRow(o));

                return result as System.Collections.IEnumerable;
            };

            treeListView.CellClick += new EventHandler<CellClickEventArgs>(treeListView_CellClick);

            radioButton1.CheckedChanged += new EventHandler(radioButton1_CheckedChanged);
        }

        void radioButton1_CheckedChanged (object sender, EventArgs e)
        {
            if (!(bool)_columnSettings[filteredSpectraColumn][4])
                filteredSpectraColumn.IsVisible = radioButton1.Checked;
            if (!(bool)_columnSettings[distinctMatchesColumn][4])
                distinctMatchesColumn.IsVisible = radioButton1.Checked;
            if (!(bool)_columnSettings[monoisotopicMassColumn][4])
                monoisotopicMassColumn.IsVisible = radioButton1.Checked;
            if (!(bool)_columnSettings[molecularWeightColumn][4])
                molecularWeightColumn.IsVisible = radioButton1.Checked;
            if (!(bool)_columnSettings[offsetColumn][4])
                offsetColumn.IsVisible = radioButton2.Checked;
            if (!(bool)_columnSettings[terminalSpecificityColumn][4])
                terminalSpecificityColumn.IsVisible = radioButton2.Checked;
            if (!(bool)_columnSettings[missedCleavagesColumn][4])
                missedCleavagesColumn.IsVisible = radioButton2.Checked;

            treeListView.RebuildColumns();
            _skipRebuild = true;
            if (session != null)
                SetData(session, dataFilter);
            _skipRebuild = false;
        }

        void treeListView_CellClick (object sender, CellClickEventArgs e)
        {
            if (e.ClickCount < 2 || e.Item == null || e.Item.RowObject == null)
                return;

            var newDataFilter = new DataFilter() { FilterSource = this };

            if (e.Item.RowObject is PeptideRow)
                newDataFilter.Peptide = (e.Item.RowObject as PeptideRow).Peptide;
            else if (e.Item.RowObject is PeptideSpectrumMatchRow)
                newDataFilter.DistinctPeptideKey = (e.Item.RowObject as PeptideSpectrumMatchRow).DistinctPeptide;

            if (PeptideViewFilter != null)
                PeptideViewFilter(this, newDataFilter);
        }

        public event PeptideViewFilterEventHandler PeptideViewFilter;

        private NHibernate.ISession session;

        private DataFilter viewFilter; // what the user has filtered on
        private DataFilter dataFilter; // how this view is filtered (i.e. never on its own rows)
        private DataFilter basicDataFilter; // the basic filter without the user filtering on rows

        private IList<PeptideRow> rowsByPeptide, basicRowsByPeptide;

        // TODO: support multiple selected objects
        string[] oldSelectionPath = new string[] { };

        public void SetData (NHibernate.ISession session, DataFilter dataFilter)
        {
            this.session = session;
            viewFilter = dataFilter;
            this.dataFilter = new DataFilter(dataFilter) {Peptide = null, DistinctPeptideKey = null};

            if (treeListView.SelectedObject is PeptideRow)
                oldSelectionPath = new string[] { treeListView.SelectedItem.Text };
            else if (treeListView.SelectedObject is PeptideSpectrumMatchRow)
                oldSelectionPath = new string[] { (treeListView.SelectedObject as PeptideSpectrumMatchRow).PeptideSpectrumMatch.Peptide.Sequence, treeListView.SelectedItem.Text };

            ClearData();

            Text = TabText = "Loading peptide view...";

            var workerThread = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            workerThread.DoWork += new DoWorkEventHandler(setData);
            workerThread.RunWorkerCompleted += new RunWorkerCompletedEventHandler(renderData);
            workerThread.RunWorkerAsync();
        }

        internal void LoadLayout(IList<ColumnProperty> listOfSettings)
        {
            if (listOfSettings.Count > 0 && !_skipRebuild)
            {
                _columnSettings = new Dictionary<OLVColumn, object[]>();

                SetPropertyFromDatabase(ref _columnSettings, sequenceColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, distinctMatchesColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, filteredSpectraColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, monoisotopicMassColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, molecularWeightColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, offsetColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, terminalSpecificityColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, missedCleavagesColumn, listOfSettings);

                SetColumnAspectGetters();
                var backColor = listOfSettings.Where(x => x.Name == "BackColor").SingleOrDefault();
                var textColor = listOfSettings.Where(x => x.Name == "TextColor").SingleOrDefault();
                treeListView.BackColor = Color.FromArgb(backColor.ColorCode);
                treeListView.ForeColor = Color.FromArgb(textColor.ColorCode);

                foreach (var kvp in _columnSettings)
                    kvp.Key.IsVisible = (bool)kvp.Value[3];
                treeListView.RebuildColumns();
            }
        }

        public void ClearData ()
        {
            Text = TabText = "Peptide View";

            treeListView.DiscardAllState();
            treeListView.Roots = null;
            treeListView.Refresh();
            Refresh();
        }

        void setData(object sender, DoWorkEventArgs e)
        {
            lock (session)
            try
            {
                var peptideQuery = session.CreateQuery("SELECT psm.Peptide, " +
                                                       "       COUNT(DISTINCT psm.SequenceAndMassDistinctKey), " +
                                                       "       COUNT(DISTINCT psm.Spectrum) " +
                                                       dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                                       "GROUP BY psm.Peptide " +
                                                       "ORDER BY COUNT(DISTINCT psm.SequenceAndMassDistinctKey) DESC, COUNT(DISTINCT psm.Spectrum) DESC");

                peptideQuery.SetReadOnly(true);

                if (dataFilter.IsBasicFilter || viewFilter.Peptide != null || viewFilter.DistinctPeptideKey != null)
                {
                    // refresh basic data when basicDataFilter is unset or when the basic filter values have changed
                    if (basicDataFilter == null || (dataFilter.IsBasicFilter && dataFilter != basicDataFilter))
                    {
                        basicDataFilter = new DataFilter(dataFilter);
                        basicRowsByPeptide = peptideQuery.List<object[]>().Select(o => new PeptideRow(o)).ToList();
                    }

                    rowsByPeptide = basicRowsByPeptide;
                }
                else
                    rowsByPeptide = peptideQuery.List<object[]>().Select(o => new PeptideRow(o)).ToList();
            }
            catch (Exception)
            {
                throw;
            }
        }

        void renderData (object sender, RunWorkerCompletedEventArgs e)
        {
            long totalDistinctMatches = rowsByPeptide.Sum(o => o.DistinctMatchesWithRoundedMass);

            // show total counts in the form title
            Text = TabText = String.Format("Peptide View: {0} distinct peptides, {1} distinct matches", rowsByPeptide.Count, totalDistinctMatches);

            treeListView.Roots = rowsByPeptide;

            // try to (re)set selected item
            expandSelectionPath(oldSelectionPath);

            treeListView.Refresh();
        }

        private void expandSelectionPath (IEnumerable<string> selectionPath)
        {
            OLVListItem selectedItem = null;
            foreach (string branch in selectionPath)
            {
                int index = 0;
                if (selectedItem != null)
                {
                    treeListView.Expand(selectedItem.RowObject);
                    index = selectedItem.Index;
                }

                index = treeListView.FindMatchingRow(branch, index, SearchDirectionHint.Down);
                if (index < 0)
                    break;
                selectedItem = treeListView.Items[index] as OLVListItem;
            }

            if (selectedItem != null)
            {
                treeListView.SelectedItem = selectedItem;
                selectedItem.EnsureVisible();
            }
        }

        private void clipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var table = getFormTable();

            TableExporter.CopyToClipboard(table);
        }

        private void fileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var table = getFormTable();

            TableExporter.ExportToFile(table);
        }

        private void exportButton_Click(object sender, EventArgs e)
        {
            if (treeListView.SelectedIndices.Count > 1)
            {
                exportMenu.Items[0].Text = "Copy Selected to Clipboard";
                exportMenu.Items[1].Text = "Export Selected to File";
                exportMenu.Items[2].Text = "Show Selected in Excel";
            }
            else
            {
                exportMenu.Items[0].Text = "Copy to Clipboard";
                exportMenu.Items[1].Text = "Export to File";
                exportMenu.Items[2].Text = "Show in Excel";
            }

            exportMenu.Show(Cursor.Position);
        }

        private List<List<string>> getFormTable()
        {
            var table = new List<List<string>>();
            var row = new List<string>();
            int numColumns;

            //get column names
            foreach (var column in treeListView.ColumnsInDisplayOrder)
                row.Add(column.Text);

            table.Add(row);
            numColumns = row.Count;
            row = new List<string>();

            //Retrieve all items
            if (treeListView.SelectedIndices.Count > 1)
            {
                foreach (int tableRow in treeListView.SelectedIndices)
                {
                    string indention = string.Empty;
                    for (int tabs = 0; tabs < treeListView.Items[tableRow].IndentCount; tabs++)
                        indention += "     ";

                    row.Add(indention + treeListView.Items[tableRow].SubItems[0].Text);

                    for (int x = 1; x < numColumns; x++)
                    {
                        row.Add(treeListView.Items[tableRow].SubItems[x].Text);
                    }
                    table.Add(row);
                    row = new List<string>();
                }
            }
            else
            {
                for (int tableRow = 0; tableRow < treeListView.Items.Count; tableRow++)
                {
                    string indention = string.Empty;
                    for (int tabs = 0; tabs < treeListView.Items[tableRow].IndentCount; tabs++)
                        indention += "     ";

                    row.Add(indention + treeListView.Items[tableRow].SubItems[0].Text);

                    for (int x = 1; x < numColumns; x++)
                    {
                        row.Add(treeListView.Items[tableRow].SubItems[x].Text);
                    }
                    table.Add(row);
                    row = new List<string>();
                }
            }

            return table;
        }

        private void showInExcelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var table = getFormTable();

            TableExporter.ShowInExcel(table);
        }

        private void displayOptionsButton_Click(object sender, EventArgs e)
        {
            Color[] currentColors = { treeListView.BackColor, treeListView.ForeColor };

            foreach (var kvp in _columnSettings)
                kvp.Value[3] = kvp.Key.IsVisible;

            var ccf = new ColumnControlForm(_columnSettings, currentColors);

            if (ccf.ShowDialog() == DialogResult.OK)
            {
                _columnSettings = ccf._savedSettings;

                foreach (var kvp in _columnSettings)
                    kvp.Key.IsVisible = (bool)kvp.Value[3];

                treeListView.BackColor = ccf.WindowBackColorBox.BackColor;
                treeListView.ForeColor = ccf.WindowTextColorBox.BackColor;

                SetColumnAspectGetters();
                treeListView.RebuildColumns();
            }
        }

        private void SetPropertyFromUserSettings(ref Dictionary<OLVColumn, object[]> testDictionary, OLVColumn targetColumn, List<string> columnProperties)
        {
            for (int x = 0; x < columnProperties.Count - 1; x++)
            {
                var splitSetting = columnProperties[x].Split('|');

                if (splitSetting[0] == targetColumn.Text)
                {
                    testDictionary.Add(targetColumn,
                        new object[5]{splitSetting[1], int.Parse(splitSetting[2]),
                            Color.FromArgb(int.Parse(splitSetting[3])),
                            bool.Parse(splitSetting[4]), bool.Parse(splitSetting[5])});
                    break;
                }
            }
        }

        private void SetPropertyFromDatabase(ref Dictionary<OLVColumn, object[]> testDictionary, OLVColumn targetColumn, IList<ColumnProperty> FormSettings)
        {
            var rowSettings = FormSettings.Where(x => x.Name == targetColumn.Text).SingleOrDefault();

            testDictionary.Add(targetColumn,
                new object[5]{rowSettings.Type, rowSettings.DecimalPlaces,
                    Color.FromArgb(rowSettings.ColorCode),
                    rowSettings.Visible, rowSettings.Locked});

        }

        internal List<ColumnProperty> GetCurrentProperties()
        {
            foreach (var kvp in _columnSettings)
                kvp.Value[3] = false;
            foreach (var column in treeListView.ColumnsInDisplayOrder)
                _columnSettings[column][3] = true;
            var currentList = new List<ColumnProperty>();

            foreach (var kvp in _columnSettings)
            {
                currentList.Add(new ColumnProperty
                {
                    Scope = "PeptideTableForm",
                    Name = kvp.Key.Text,
                    Type = kvp.Value[0].ToString(),
                    DecimalPlaces = (int)kvp.Value[1],
                    ColorCode = ((Color)kvp.Value[2]).ToArgb(),
                    Visible = (bool)kvp.Value[3],
                    Locked = (bool)kvp.Value[4]
                });
            }

            currentList.Add(new ColumnProperty
            {
                Scope = "PeptideTableForm",
                Name = "BackColor",
                Type = "GlobalSetting",
                DecimalPlaces = -1,
                ColorCode = treeListView.BackColor.ToArgb(),
                Visible = false,
                Locked = false
            });
            currentList.Add(new ColumnProperty
            {
                Scope = "PeptideTableForm",
                Name = "TextColor",
                Type = "GlobalSetting",
                DecimalPlaces = -1,
                ColorCode = treeListView.ForeColor.ToArgb(),
                Visible = false,
                Locked = false
            });

            return currentList;
        }
    }

    public delegate void PeptideViewFilterEventHandler (PeptideTableForm sender, DataFilter peptideViewFilter);
}
