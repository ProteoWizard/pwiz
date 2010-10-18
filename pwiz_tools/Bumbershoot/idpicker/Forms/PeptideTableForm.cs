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

            HideOnClose = true;

            Text = TabText = "Peptide View";

            #region Column aspect getters
            var retrievedList = new List<string>(Util.StringCollectionToStringArray(Properties.Settings.Default.PeptideTableFormSettings));
            _columnSettings = new Dictionary<OLVColumn, object[]>();

            if (retrievedList.Count == 56)
            {
                SetPropertyFromUserSettings(ref _columnSettings, sequenceColumn, ref  retrievedList);
                SetPropertyFromUserSettings(ref _columnSettings, filteredVariantsColumn, ref  retrievedList);
                SetPropertyFromUserSettings(ref _columnSettings, filteredSpectraColumn, ref  retrievedList);
                SetPropertyFromUserSettings(ref _columnSettings, monoisotopicMassColumn, ref  retrievedList);
                SetPropertyFromUserSettings(ref _columnSettings, molecularWeightColumn, ref  retrievedList);
                SetPropertyFromUserSettings(ref _columnSettings, offsetColumn, ref  retrievedList);
                SetPropertyFromUserSettings(ref _columnSettings, terminalSpecificityColumn, ref  retrievedList);
                SetPropertyFromUserSettings(ref _columnSettings, missedCleavagesColumn, ref  retrievedList);
                SetPropertyFromUserSettings(ref _columnSettings, proteinsColumn, ref  retrievedList);

                treeListView.BackColor = Color.FromArgb(int.Parse(retrievedList[0].ToString()));
                treeListView.ForeColor = Color.FromArgb(int.Parse(retrievedList[1].ToString()));

                foreach (var kvp in _columnSettings)
                    kvp.Key.IsVisible = (bool)kvp.Value[3];
                treeListView.RebuildColumns();
            }
            else
            {
                _columnSettings.Add(sequenceColumn, new object[4] { "Key", -99, treeListView.BackColor, false });
                _columnSettings.Add(filteredVariantsColumn, new object[4] { "Integer", -99, treeListView.BackColor, false });
                _columnSettings.Add(filteredSpectraColumn, new object[4] { "Integer", -99, treeListView.BackColor, false });
                _columnSettings.Add(monoisotopicMassColumn, new object[4] { "Float", -1, treeListView.BackColor, false });
                _columnSettings.Add(molecularWeightColumn, new object[4] { "Float", -1, treeListView.BackColor, false });
                _columnSettings.Add(offsetColumn, new object[4] { "Integer", -99, treeListView.BackColor, false });
                _columnSettings.Add(terminalSpecificityColumn, new object[4] { "String", -99, treeListView.BackColor, false });
                _columnSettings.Add(missedCleavagesColumn, new object[4] { "Integer", -99, treeListView.BackColor, false });
                _columnSettings.Add(proteinsColumn, new object[4] { "String", -99, treeListView.BackColor, false });
            }

            SetColumnAspectGetters();

            #endregion

            treeListView.UseCellFormatEvents = true;
            treeListView.FormatCell += delegate(object sender, FormatCellEventArgs currentCell)
            {
                currentCell.SubItem.BackColor = (Color)_columnSettings[currentCell.Column][2];
            };

            treeListView.CanExpandGetter += delegate(object x) { return x is PeptideRow; };
            treeListView.ChildrenGetter += delegate(object x)
            {
                dataFilter.Peptide = (x as PeptideRow).Peptide;
                object result;
                if (radioButton1.Checked)
                    result = session.CreateQuery("SELECT psm, (psm.Peptide || ' ' || ROUND(psm.MonoisotopicMass)), COUNT(DISTINCT psm.Spectrum) " +
                                                 dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                                 "GROUP BY (psm.Peptide || ' ' || ROUND(psm.MonoisotopicMass))")
                                    .List<object[]>().Select(o => new PeptideSpectrumMatchRow(o));
                else
                    return session.CreateQuery("SELECT DISTINCT psm.Peptide.Instances " +
                                               dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch)/* +
                                               " GROUP BY psm.Peptide"*/
                                                                        )
                                  .List<object>().Select(o => new PeptideInstanceRow(o));

                dataFilter.Peptide = null;
                return result as System.Collections.IEnumerable;
            };

            treeListView.CellClick += new EventHandler<CellClickEventArgs>(treeListView_CellClick);

            radioButton1.CheckedChanged += new EventHandler(radioButton1_CheckedChanged);
        }

        private void SetColumnAspectGetters()
        {
            sequenceColumn.AspectGetter = null;
            filteredVariantsColumn.AspectGetter = null;
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

            filteredVariantsColumn.AspectGetter += delegate(object x)
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
                dataFilter.Peptide = (x as PeptideRow).Peptide;
                object result;
                if (radioButton1.Checked)
                    result = session.CreateQuery("SELECT psm, (psm.Peptide || ' ' || ROUND(psm.MonoisotopicMass)), COUNT(DISTINCT psm.Spectrum) " +
                                                 dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                                 "GROUP BY (psm.Peptide || ' ' || ROUND(psm.MonoisotopicMass))")
                                    .List<object[]>().Select(o => new PeptideSpectrumMatchRow(o));
                else
                    return session.CreateQuery("SELECT DISTINCT psm.Peptide.Instances " +
                                               dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch)/* +
                                               " GROUP BY psm.Peptide"*/
                                                                        )
                                  .List<object>().Select(o => new PeptideInstanceRow(o));

                dataFilter.Peptide = null;
                return result as System.Collections.IEnumerable;
            };

            treeListView.CellClick += new EventHandler<CellClickEventArgs>(treeListView_CellClick);

            radioButton1.CheckedChanged += new EventHandler(radioButton1_CheckedChanged);
        }

        void radioButton1_CheckedChanged (object sender, EventArgs e)
        {
            if (!(bool)_columnSettings[filteredSpectraColumn][4])
                filteredSpectraColumn.IsVisible = radioButton1.Checked;
            if (!(bool)_columnSettings[filteredVariantsColumn][4])
                filteredVariantsColumn.IsVisible = radioButton1.Checked;
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

            var newDataFilter = new DataFilter()
            {
                MaximumQValue = dataFilter.MaximumQValue,
                FilterSource = this
            };

            if (e.Item.RowObject is PeptideRow)
                newDataFilter.Peptide = (e.Item.RowObject as PeptideRow).Peptide;
            else if (e.Item.RowObject is PeptideSpectrumMatchRow)
                newDataFilter.DistinctPeptideKey = (e.Item.RowObject as PeptideSpectrumMatchRow).DistinctPeptide;

            if (PeptideViewFilter != null)
                PeptideViewFilter(this, newDataFilter);
        }

        public event PeptideViewFilterEventHandler PeptideViewFilter;

        private NHibernate.ISession session;
        private DataFilter dataFilter, basicDataFilter;
        private IList<PeptideRow> rowsByPeptide, basicRowsByPeptide;

        // TODO: support multiple selected objects
        string[] oldSelectionPath = new string[] { };

        public void SetData (NHibernate.ISession session, DataFilter dataFilter)
        {
            this.session = session;
            this.dataFilter = new DataFilter(dataFilter) {Peptide = null};

            if (treeListView.SelectedObject is PeptideRow)
                oldSelectionPath = new string[] { treeListView.SelectedItem.Text };
            else if (treeListView.SelectedObject is PeptideSpectrumMatchRow)
                oldSelectionPath = new string[] { (treeListView.SelectedObject as PeptideSpectrumMatchRow).PeptideSpectrumMatch.Peptide.Sequence, treeListView.SelectedItem.Text };

            ClearData();

            Text = TabText = "Loading peptide view...";

            //set column settings if they are set for the database
            IList<idpDBSettings> listOfSettings = new List<idpDBSettings>();
            try
            {
                listOfSettings = session.QueryOver<idpDBSettings>().Where(x => x.FormName == "PeptideTableForm").List<idpDBSettings>();
            }
            catch
            {
                //sometimes throws "There was a problem converting an IDataReader to NDataReader" on filter attempt
                //does not appear to have any effect. Swallow error for now unless it turns out to be part of bigger problem;
            }

            if (listOfSettings.Count > 0 && !_skipRebuild)
            {
                _columnSettings = new Dictionary<OLVColumn, object[]>();

                SetPropertyFromDatabase(ref _columnSettings, sequenceColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, filteredVariantsColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, filteredSpectraColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, monoisotopicMassColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, molecularWeightColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, offsetColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, terminalSpecificityColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, missedCleavagesColumn, listOfSettings);

                var backColor = listOfSettings.Where<idpDBSettings>(x => x.ColumnName == "__BackColor").SingleOrDefault();
                var textColor = listOfSettings.Where<idpDBSettings>(x => x.ColumnName == "__TextColor").SingleOrDefault();
                treeListView.BackColor = Color.FromArgb(backColor.ColorCode);
                treeListView.ForeColor = Color.FromArgb(textColor.ColorCode);

                foreach (var kvp in _columnSettings)
                    kvp.Key.IsVisible = (bool)kvp.Value[3];
                treeListView.RebuildColumns();
            }

            var workerThread = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            workerThread.DoWork += new DoWorkEventHandler(setData);
            workerThread.RunWorkerCompleted += new RunWorkerCompletedEventHandler(renderData);
            workerThread.RunWorkerAsync();
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
                                                       this.dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                                       "GROUP BY psm.Peptide " +
                                                       "ORDER BY COUNT(DISTINCT psm.SequenceAndMassDistinctKey) DESC, COUNT(DISTINCT psm.Spectrum) DESC");

                peptideQuery.SetReadOnly(true);

                if (dataFilter.IsBasicFilter || dataFilter.Peptide != null || dataFilter.DistinctPeptideKey != null)
                {
                    if (basicDataFilter == null || (dataFilter.IsBasicFilter && dataFilter != basicDataFilter))
                    {
                        basicDataFilter = new DataFilter(this.dataFilter);
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
            long totalSpectra = rowsByPeptide.Sum(o => o.Spectra);

            // show total counts in the form title
            Text = TabText = String.Format("Peptide View: {0} peptides, {1} distinct matches, {2} spectra", rowsByPeptide.Count, totalDistinctMatches, totalSpectra);

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

        private void SetPropertyFromUserSettings(ref Dictionary<OLVColumn, object[]> testDictionary, OLVColumn targetColumn, ref List<string> columnProperties)
        {
            for (int x = 0; x < columnProperties.Count - 6; x += 6)
            {
                if (columnProperties[x] == targetColumn.Text)
                {
                    testDictionary.Add(targetColumn,
                        new object[5]{columnProperties[x+1], int.Parse(columnProperties[x+2]),
                            Color.FromArgb(int.Parse(columnProperties[x+3])),
                            bool.Parse(columnProperties[x+4]), bool.Parse(columnProperties[x+5])});

                    columnProperties.RemoveRange(x, 6);
                    break;
                }
            }
        }

        private void SetPropertyFromDatabase(ref Dictionary<OLVColumn, object[]> testDictionary, OLVColumn targetColumn, IList<idpDBSettings> FormSettings)
        {
            idpDBSettings rowSettings = FormSettings.Where<idpDBSettings>(x => x.ColumnName == targetColumn.Text).SingleOrDefault();

            testDictionary.Add(targetColumn,
                new object[5]{rowSettings.Type, rowSettings.DecimalPlaces,
                    Color.FromArgb(rowSettings.ColorCode),
                    rowSettings.Visible, rowSettings.Locked});

        }

        public void SaveSettings()
        {
            //save current column settings (In case user has changed columns using right-click menu
            foreach (var kvp in _columnSettings)
                kvp.Value[3] = false;
            foreach (OLVColumn column in treeListView.ColumnsInDisplayOrder)
                _columnSettings[column][3] = true;

            if (session == null)
            {
                //Store settings dictionary as string list
                //{Text of key, (string)Value[0], (string?)Value[1], Name of Color Value[2], (string)Value[3]}
                //Therefore the list will be translated back in sets of 5

                var storedList = new System.Collections.Specialized.StringCollection();

                foreach (var kvp in _columnSettings)
                {
                    storedList.Add(kvp.Key.Text);
                    storedList.Add(kvp.Value[0].ToString());
                    storedList.Add(kvp.Value[1].ToString());
                    storedList.Add(((Color)kvp.Value[2]).ToArgb().ToString());
                    storedList.Add(kvp.Value[3].ToString());
                    storedList.Add("false");
                }

                storedList.Add(treeListView.BackColor.ToArgb().ToString());
                storedList.Add(treeListView.ForeColor.ToArgb().ToString());

                Properties.Settings.Default.PeptideTableFormSettings = storedList;
                Properties.Settings.Default.Save();


            }
            else
            {
                var listOfSettings = session.QueryOver<idpDBSettings>().Where(x => x.FormName == "PeptideTableForm").List();

                if (listOfSettings.Count > 0)
                {
                    foreach (var kvp in _columnSettings)
                    {
                        var settingRow = listOfSettings.Where<idpDBSettings>(x => x.ColumnName == kvp.Key.Text).SingleOrDefault<idpDBSettings>();

                        settingRow.DecimalPlaces = (int)kvp.Value[1];
                        settingRow.ColorCode = ((Color)kvp.Value[2]).ToArgb();
                        settingRow.Visible = (bool)kvp.Value[3];
                        settingRow.Locked = false;
                        session.Save(settingRow);
                    }

                    var formBackColorRow = listOfSettings.Where<idpDBSettings>(x => x.ColumnName == "__BackColor").SingleOrDefault<idpDBSettings>();
                    var formTextColorRow = listOfSettings.Where<idpDBSettings>(x => x.ColumnName == "__TextColor").SingleOrDefault<idpDBSettings>();

                    formBackColorRow.ColorCode = treeListView.BackColor.ToArgb();
                    formTextColorRow.ColorCode = treeListView.ForeColor.ToArgb();
                    session.Save(formBackColorRow);
                    session.Save(formTextColorRow);
                }
                else
                {
                    foreach (var kvp in _columnSettings)
                    {
                        session.Save(new idpDBSettings()
                        {
                            FormName = "PeptideTableForm",
                            ColumnName = kvp.Key.Text,
                            Type = kvp.Value[0].ToString(),
                            DecimalPlaces = (int)kvp.Value[1],
                            ColorCode = ((Color)kvp.Value[2]).ToArgb(),
                            Visible = (bool)kvp.Value[3],
                            Locked = (bool)kvp.Value[4]
                        });
                    }

                    session.Save(new idpDBSettings()
                    {
                        FormName = "PeptideTableForm",
                        ColumnName = "__BackColor",
                        Type = "GlobalSetting",
                        DecimalPlaces = -1,
                        ColorCode = treeListView.BackColor.ToArgb(),
                        Visible = false,
                        Locked = false
                    });
                    session.Save(new idpDBSettings()
                    {
                        FormName = "PeptideTableForm",
                        ColumnName = "__TextColor",
                        Type = "GlobalSetting",
                        DecimalPlaces = -1,
                        ColorCode = treeListView.ForeColor.ToArgb(),
                        Visible = false,
                        Locked = false
                    });
                }

                session.Flush();
            }
        }
    }

    public delegate void PeptideViewFilterEventHandler (PeptideTableForm sender, DataFilter peptideViewFilter);
}
