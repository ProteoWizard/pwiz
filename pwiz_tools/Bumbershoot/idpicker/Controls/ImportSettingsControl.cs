//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.IO;
using Iesi.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using IDPicker.DataModel;

namespace IDPicker.Controls
{
    /// <summary>
    /// Represents the method called when a user wants to view or manipulate their QonverterSettings presets.
    /// </summary>
    public delegate void QonverterSettingsManagerNeeded();

    /// <summary>
    /// Allows a user to assign QonverterSettings presets to Analysis instances.
    /// </summary>
    public partial class ImportSettingsControl : UserControl
    {
        QonverterSettingsManagerNeeded qonverterSettingsManagerNeeded;
        IDictionary<string, QonverterSettings> qonverterSettingsByName;

        private class ImportSettingsRow
        {
            public Parser.Analysis Analysis { get; set; }
            public Parser.Analysis.ImportSettings ImportSettings { get { return Analysis.importSettings; } }

            public string OriginalAnalysisName { get; set; }
            public string DecoyPrefix { get; set; }
            public string QonverterSettingsPreset { get; set; }
        }

        IList<ImportSettingsRow> rows;


        public ImportSettingsControl (IEnumerable<Parser.Analysis> distinctAnalyses,
                                      QonverterSettingsManagerNeeded qonverterSettingsManagerNeeded)
        {
            InitializeComponent();

            databaseColumn.OpenFileDialog = new OpenFileDialog
            {
                CheckFileExists = true,
                Filter = "FASTA files|*.fasta|All files|*.*"
            };

            if (qonverterSettingsManagerNeeded == null)
                throw new NullReferenceException();

            this.qonverterSettingsManagerNeeded = qonverterSettingsManagerNeeded;

            qonverterSettingsByName = QonverterSettings.LoadQonverterSettings();

            qonverterSettingsByName.Keys.ToList().ForEach(o => qonverterSettingsColumn.Items.Add(o));
            qonverterSettingsColumn.Items.Add("Edit...");

            rows = new List<ImportSettingsRow>();

            foreach (var a in distinctAnalyses)
            {
                var row = new ImportSettingsRow {Analysis = a, OriginalAnalysisName = a.name};

                // try to find valid protein database location
                if (!File.Exists(a.importSettings.proteinDatabaseFilepath))
                {
                    var databaseName = Path.GetFileName(a.importSettings.proteinDatabaseFilepath);
                    if (databaseName != null)
                    {
                        foreach (var item in a.filepaths)
                        {
                            var possibleLocation = Path.Combine(Path.GetDirectoryName(item) ?? string.Empty,
                                                                databaseName);
                            if (File.Exists(possibleLocation))
                            {
                                a.importSettings.proteinDatabaseFilepath = possibleLocation;
                                break;
                            }

                            try
                            {
                                a.importSettings.proteinDatabaseFilepath = Util.FindDatabaseInSearchPath(databaseName, Path.GetDirectoryName(item) ?? ".");
                                break;
                            }
                            catch (ArgumentException)
                            {
                                // ignore exception, but if protein database still isn't found, reduce it to the filename
                                if (Path.IsPathRooted(a.importSettings.proteinDatabaseFilepath))
                                    a.importSettings.proteinDatabaseFilepath = databaseName;
                            }
                        }
                    }
                }

                row.DecoyPrefix = Properties.Settings.Default.DefaultDecoyPrefix;
                row.ImportSettings.maxResultRank = Properties.Settings.Default.DefaultMaxRank;
                row.ImportSettings.maxQValue = Properties.Settings.Default.DefaultMaxImportFDR;
                row.ImportSettings.ignoreUnmappedPeptides = Properties.Settings.Default.DefaultIgnoreUnmappedPeptides;

                // select a default qonverter settings preset
                var firstSoftwarePreset = qonverterSettingsByName.Keys.FirstOrDefault(o => o.ToLowerInvariant().Contains(a.softwareName.ToLowerInvariant()) ||
                                                                                           a.softwareName.ToLowerInvariant().Contains(o.ToLowerInvariant()));
                row.QonverterSettingsPreset = firstSoftwarePreset ?? qonverterSettingsByName.Keys.FirstOrDefault() ?? String.Empty;
                row.ImportSettings.qonverterSettings = qonverterSettingsByName[row.QonverterSettingsPreset].ToQonverterSettings();

                if (a.parameters.ContainsKey("Config: DecoyPrefix"))
                    row.DecoyPrefix = a.parameters["Config: DecoyPrefix"];
                else if (a.parameters.ContainsKey("DecoyPrefix"))
                    row.DecoyPrefix = a.parameters["DecoyPrefix"];

                row.ImportSettings.qonverterSettings.DecoyPrefix = row.DecoyPrefix;
                //row.ImportSettings.logQonversionDetails = true;

                rows.Add(row);
            }

            dataGridView.CellBeginEdit += dataGridView_CellBeginEdit;
            dataGridView.CellEndEdit += dataGridView_CellEndEdit;
            dataGridView.CurrentCellDirtyStateChanged += dataGridView_CurrentCellDirtyStateChanged;
            dataGridView.CellValueNeeded += dataGridView_CellValueNeeded;
            dataGridView.CellValuePushed += dataGridView_CellValuePushed;
            dataGridView.CellPainting += dataGridView_CellPainting;
            dataGridView.CellFormatting += dataGridView_CellFormatting;

            dataGridView.RootRowCount = rows.Count;
        }

        void dataGridView_CellFormatting(object sender, TreeDataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndexHierarchy.Count == 1 && e.ColumnIndex == databaseColumn.Index)
                e.CellStyle.BackColor = File.Exists((string)e.Value) ? SystemColors.Window : Color.LightSalmon;
            else if (e.RowIndexHierarchy.Count > 1)
            {
                // if the current analysis parameter is one of the differing paremeters (as stored in the original analysis name);
                // make the text red
                var analysisRow = rows[e.RowIndexHierarchy[0]];
                var currentParameter = analysisRow.Analysis.parameters.ElementAt(e.RowIndexHierarchy[1]);
                if (analysisRow.OriginalAnalysisName.Contains(currentParameter.Key.Replace("Config: ", "") + "="))
                    e.CellStyle.ForeColor = Color.Red;
            }
        }

        /// <summary>Prevents painting the editing controls for analysis parameter rows.</summary>
        void dataGridView_CellPainting(object sender, TreeDataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndexHierarchy.Count > 1 && e.ColumnIndex > 1)
            {
                e.PaintBackground(e.ClipBounds, e.State.HasFlag(DataGridViewElementStates.Selected));
                e.Handled = true;
            }
            else
                e.Handled = false;
        }

        void dataGridView_CellValuePushed(object sender, TreeDataGridViewCellValueEventArgs e)
        {
            double temp;
            var importSettingsRow = rows[e.RowIndexHierarchy[0]];

            if (e.RowIndexHierarchy.Count == 1)
            {
                if (e.ColumnIndex == analysisNameColumn.Index) importSettingsRow.Analysis.name = (string) e.Value;
                else if (e.ColumnIndex == databaseColumn.Index) importSettingsRow.ImportSettings.proteinDatabaseFilepath = (string) e.Value;
                else if (e.ColumnIndex == decoyPrefixColumn.Index) importSettingsRow.DecoyPrefix = (string) e.Value;
                else if (e.ColumnIndex == maxFDRColumn.Index && double.TryParse(e.Value.ToString(), out temp)) importSettingsRow.ImportSettings.maxQValue = Convert.ToDouble(e.Value);
                else if (e.ColumnIndex == maxRankColumn.Index) importSettingsRow.ImportSettings.maxResultRank = Convert.ToInt32(e.Value);
                else if (e.ColumnIndex == ignoreUnmappedPeptidesColumn.Index) importSettingsRow.ImportSettings.ignoreUnmappedPeptides = (bool) e.Value;
                else if (e.ColumnIndex == qonverterSettingsColumn.Index) importSettingsRow.QonverterSettingsPreset = (string) e.Value;
            }
        }

        void dataGridView_CellValueNeeded(object sender, TreeDataGridViewCellValueEventArgs e)
        {
            var importSettingsRow = rows[e.RowIndexHierarchy[0]];

            if (e.RowIndexHierarchy.Count == 1)
            {
                e.ChildRowCount = importSettingsRow.Analysis.parameters.Count;

                if (e.ColumnIndex == analysisNameColumn.Index) e.Value = importSettingsRow.Analysis.name;
                else if (e.ColumnIndex == databaseColumn.Index) e.Value = importSettingsRow.ImportSettings.proteinDatabaseFilepath;
                else if (e.ColumnIndex == decoyPrefixColumn.Index) e.Value = importSettingsRow.DecoyPrefix;
                else if (e.ColumnIndex == maxFDRColumn.Index) e.Value = importSettingsRow.ImportSettings.maxQValue;
                else if (e.ColumnIndex == maxRankColumn.Index) e.Value = importSettingsRow.ImportSettings.maxResultRank;
                else if (e.ColumnIndex == ignoreUnmappedPeptidesColumn.Index) e.Value = importSettingsRow.ImportSettings.ignoreUnmappedPeptides;
                else if (e.ColumnIndex == qonverterSettingsColumn.Index) e.Value = importSettingsRow.QonverterSettingsPreset;
            }
            else if (e.ColumnIndex == 0) e.Value = importSettingsRow.Analysis.parameters.ElementAt(e.RowIndexHierarchy[1]).Key.Replace("Config: ", "");
            else if (e.ColumnIndex == 1) e.Value = importSettingsRow.Analysis.parameters.ElementAt(e.RowIndexHierarchy[1]).Value;
        }

        string uneditedValue = null;
        void dataGridView_CellBeginEdit (object sender, TreeDataGridViewCellCancelEventArgs e)
        {
            // child rows (analysis parameters) can't be edited
            if (e.RowIndexHierarchy.Count > 1)
            {
                e.Cancel = true;
                return;
            }

            uneditedValue = (dataGridView[e.ColumnIndex, e.RowIndexHierarchy].Value ?? String.Empty).ToString();

            if (e.ColumnIndex == databaseColumn.Index)
            {
                string filename = Path.GetFileName(uneditedValue);
                databaseColumn.OpenFileDialog.Title = "Find the FASTA database (" + filename + ")";
                databaseColumn.OpenFileDialog.FileName = filename;
            }
        }

        void dataGridView_CellEndEdit (object sender, TreeDataGridViewCellEventArgs e)
        {
            var row = dataGridView[e.ColumnIndex, e.RowIndexHierarchy].OwningRow;
            var importSettingsRow = rows[e.RowIndexHierarchy[0]];
            var analysis = importSettingsRow.Analysis;

            if (e.ColumnIndex == analysisNameColumn.Index)
            {
                if (String.IsNullOrEmpty((string) row.Cells[analysisNameColumn.Index].Value))
                    row.Cells[analysisNameColumn.Index].Value = analysis.name = importSettingsRow.OriginalAnalysisName;
            }
            else if (e.ColumnIndex == databaseColumn.Index)
            {
                analysis.importSettings.proteinDatabaseFilepath = (string) row.Cells[databaseColumn.Index].Value;
                row.Cells[databaseColumn.Index].Style.BackColor = File.Exists((string) row.Cells[databaseColumn.Index].Value) ? SystemColors.Window : Color.LightSalmon;

                // also set all databases equal to the uneditedValue
                string originalFilename = Path.GetFileName(uneditedValue) ?? uneditedValue;
                foreach (var importSettingsRow2 in rows)
                {
                    if (importSettingsRow == importSettingsRow2) continue;
                    string rowFilename = importSettingsRow2.ImportSettings.proteinDatabaseFilepath;
                    rowFilename = Path.GetFileName(rowFilename) ?? rowFilename;
                    if (rowFilename == originalFilename)
                        importSettingsRow2.ImportSettings.proteinDatabaseFilepath = importSettingsRow.ImportSettings.proteinDatabaseFilepath;
                }
            }
            else if (e.ColumnIndex == decoyPrefixColumn.Index)
            {
                analysis.importSettings.qonverterSettings.DecoyPrefix = (string) row.Cells[decoyPrefixColumn.Index].Value;
                importSettingsRow.DecoyPrefix = (string)row.Cells[decoyPrefixColumn.Index].Value;
            }
            else if (e.ColumnIndex == maxRankColumn.Index &&
                     !Int32.TryParse(row.Cells[maxRankColumn.Index].Value.ToString(), out analysis.importSettings.maxResultRank) ||
                     analysis.importSettings.maxResultRank < 1)
                row.Cells[maxRankColumn.Index].Value = analysis.importSettings.maxResultRank = Convert.ToInt32(uneditedValue);
            else if (e.ColumnIndex == maxFDRColumn.Index &&
                     !Double.TryParse(row.Cells[maxFDRColumn.Index].Value.ToString(), out analysis.importSettings.maxQValue) ||
                     analysis.importSettings.maxQValue < 0 || analysis.importSettings.maxQValue > 1)
                row.Cells[maxFDRColumn.Index].Value = analysis.importSettings.maxQValue = Convert.ToDouble(uneditedValue);
            else if (e.ColumnIndex == qonverterSettingsColumn.Index)
            {
                analysis.importSettings.qonverterSettings = qonverterSettingsByName[(string) row.Cells[qonverterSettingsColumn.Index].Value].ToQonverterSettings();
                analysis.importSettings.qonverterSettings.DecoyPrefix = (string) row.Cells[decoyPrefixColumn.Index].Value;
                importSettingsRow.DecoyPrefix = (string)row.Cells[decoyPrefixColumn.Index].Value;
            }
            else if (e.ColumnIndex == ignoreUnmappedPeptidesColumn.Index)
                analysis.importSettings.ignoreUnmappedPeptides = (bool) row.Cells[ignoreUnmappedPeptidesColumn.Index].Value;
        }

        void dataGridView_CurrentCellDirtyStateChanged (object sender, EventArgs e)
        {
            var cell = dataGridView.CurrentCell;
            var row = cell.OwningRow;

            dataGridView.EndEdit();
            dataGridView.NotifyCurrentCellDirty(false);

            if (cell.ColumnIndex != qonverterSettingsColumn.Index)
                return;

            if ((string) cell.EditedFormattedValue == "Edit..." || (string) cell.Value == "Edit...")
            {
                qonverterSettingsManagerNeeded();

                qonverterSettingsByName = QonverterSettings.LoadQonverterSettings();
                qonverterSettingsColumn.Items.Clear();
                qonverterSettingsByName.Keys.ToList().ForEach(o => qonverterSettingsColumn.Items.Add(o));
                qonverterSettingsColumn.Items.Add("Edit...");

                cell.Value = uneditedValue;
                dataGridView.RefreshEdit();
            }
        }
    }
}