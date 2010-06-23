using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using IdPickerGui.BLL;
using IDPicker;

namespace Forms.Controls
{
    public partial class DeltaMassTable : UserControl
    {
        public PTMDataInTreeLists PTMData;
        public Workspace workspace;
        public TabControl tabControl;
        public Map<UniqueSpectrumID, VariantInfo> secondaryResults;
        public string tabSuffix;
        public int imageIndex;

        public DeltaMassTable(string suffix, int imgIndex)
        {
            InitializeComponent();
            PTMData = new PTMDataInTreeLists();
            tabSuffix = suffix;
            imageIndex = imgIndex;
        }

        public void loadFromWorkspace(ref Workspace space, ref Map<UniqueSpectrumID,VariantInfo> primResults)
        {
            workspace = space;
            PTMData.loadFromWorkspace(workspace, ref primResults);
            PTMData.makeDeltaMassTable();
            if (workspace.modificationAnnotations != null && workspace.modificationAnnotations.Count > 0)
                PTMData.makeAnnotationsTable(workspace);
            setAndFormatDataTable();
        }
    
        public void loadFromWorkspace(ref Workspace space, 
                                      ref Map<UniqueSpectrumID, PTMDigger.AttestationStatus> results,
                                      PTMDigger.AttestationStatus[] status)
        {
            workspace = space;
            PTMData.loadFromWorkspace( workspace, ref results, status);
            PTMData.makeDeltaMassTable();
            if (workspace.modificationAnnotations != null && workspace.modificationAnnotations.Count > 0)
                PTMData.makeAnnotationsTable(workspace);
            setAndFormatDataTable();
        }
   
        public void setData(ref PTMDataInTreeLists data, ref Workspace space)
        {
            PTMData = data;
            workspace = space;
        }

        public void setControls(ref TabControl control)
        {
            tabControl = control;
        }

        public void setSecondaryResults(ref Map<UniqueSpectrumID, VariantInfo> results)
        {
            secondaryResults = results;
        }

        /// <summary>
        /// These set of functions are delegates for the datagridview
        /// </summary>
        /// <param name="table"></param>
        public void setAndFormatDataTable()
        {
            deltaMassMatrix.DataSource = PTMData.deltaMassTable.DefaultView;
            deltaMassMatrix.Dock = DockStyle.Fill;
            deltaMassMatrix.BackgroundColor = SystemColors.Control;
            // Set the order and size of columns 
            deltaMassMatrix.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            deltaMassMatrix.Columns["DeltaMass"].DisplayIndex = 0;
            deltaMassMatrix.Columns["DeltaMass"].DefaultCellStyle = deltaMassMatrix.ColumnHeadersDefaultCellStyle;
            deltaMassMatrix.Columns["Total"].Visible = false;
            int displayIndex = 1;
            for (int ind = 0; ind < deltaMassMatrix.Columns.Count; ++ind)
            {
                if (deltaMassMatrix.Columns[ind].Name.Length < 2)
                {
                    deltaMassMatrix.Columns[ind].DisplayIndex = displayIndex;
                    deltaMassMatrix.Columns[ind].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                    ++displayIndex;
                }
                // Autosize the table columns
                deltaMassMatrix.Columns[ind].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }
            if (deltaMassMatrix.Columns.Contains("N-term"))
            {
                deltaMassMatrix.Columns["N-term"].DisplayIndex = displayIndex;
                ++displayIndex;
            }
            if (deltaMassMatrix.Columns.Contains("C-term"))
            {
                deltaMassMatrix.Columns["C-term"].DisplayIndex = displayIndex;
                ++displayIndex;
            }
            deltaMassMatrix.Columns["Total"].DisplayIndex = displayIndex;
            // Set the cell display and hide the row headers
            deltaMassMatrix.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            deltaMassMatrix.RowHeadersVisible = false;
            // Set cell event handlers
            deltaMassMatrix.CellFormatting += new DataGridViewCellFormattingEventHandler(customCellFormatting);
            deltaMassMatrix.CellDoubleClick += new DataGridViewCellEventHandler(deltaMassMatrix_CellDoubleClick);
            deltaMassMatrix.CellClick += new DataGridViewCellEventHandler(deltaMassMatrix_CellClick);
            deltaMassMatrix.Visible = true;
            deltaMassMatrix.ReadOnly = true;
        }

        public void updateUnimodStatus()
        {
            if (workspace.modificationAnnotations != null && workspace.modificationAnnotations.Count > 0)
                PTMData.makeAnnotationsTable(workspace);
        }

        public void updateRawDataPathStatus()
        {
            this.rawDataPath.Text = Properties.Settings.Default.SourcePath;
        }

        public void deltaMassMatrix_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            // Make sure we have an event
            if (e != null)
            {
                // Get the mass, amino acid, and the spectral count of the cell
                string aminoAcid = null;
                int mass = Int32.MinValue;
                int cellValue = Int32.MinValue;
                aminoAcid = deltaMassMatrix.Columns[e.ColumnIndex].Name;
                try { cellValue = Int32.Parse(deltaMassMatrix[e.ColumnIndex, e.RowIndex].Value.ToString()); }
                catch (Exception) { }
                try { mass = Int32.Parse(deltaMassMatrix[0, e.RowIndex].Value.ToString()); }
                catch (Exception) { }
                unimodAnnotation.Text = "";
                if (aminoAcid != null && mass != Int32.MinValue && cellValue != Int32.MinValue && cellValue > 0)
                {
                    if (workspace.modificationAnnotations != null && workspace.modificationAnnotations.Count > 0)
                    {
                        StringBuilder str = new StringBuilder();
                        foreach (var ann in PTMData.annotations[mass][aminoAcid])
                            str.Append(ann + Environment.NewLine);
                        unimodAnnotation.Text = str.ToString();
                    }
                }
            }
        }

        /// <summary>
        /// This event handler opens up a new PTMValidation page when a certain
        /// cell in delta mass table is double clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void deltaMassMatrix_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            // Make sure we have an event
            if (e != null)
            {
                // Get the mass, amino acid, and the spectral count of the cell
                string aminoAcid = null;
                int mass = Int32.MinValue;
                int cellValue = Int32.MinValue;
                aminoAcid = deltaMassMatrix.Columns[e.ColumnIndex].Name;
                try { cellValue = Int32.Parse(deltaMassMatrix[e.ColumnIndex, e.RowIndex].Value.ToString()); }
                catch (Exception) { }
                try { mass = Int32.Parse(deltaMassMatrix[0, e.RowIndex].Value.ToString()); }
                catch (Exception) { }
                if (aminoAcid != null && mass != Int32.MinValue && cellValue != Int32.MinValue && cellValue > 0)
                {
                    // Make the tab page name
                    string tabPageText;
                    if (mass > 0)
                        tabPageText = aminoAcid + "+" + mass;
                    else
                        tabPageText = aminoAcid + mass;

                    string tabPageName = tabPageText + " (" + tabSuffix + ")";

                    // Check to see if already exists, create a new one
                    // iff there isn't a page with this name already
                    if (tabControl.TabPages[tabPageName] != null)
                        tabControl.SelectTab(tabPageName);
                    else
                    {
                        Map<string, Protein> prots = PTMData.dataTrees[mass][aminoAcid];
                        ProteinValidationPane newPane = new ProteinValidationPane(prots, ref secondaryResults);
                        newPane.Dock = DockStyle.Fill;
                        TabPage newPage = new TabPage(tabPageName);
                        newPage.Name = tabPageName;
                        newPage.Text = tabPageText;
                        newPage.ImageIndex = imageIndex;
                        newPage.Controls.Add(newPane);
                        tabControl.TabPages.Add(newPage);
                        tabControl.SelectTab(newPage);
                    }
                }
            }
        }

        /// <summary>
        /// This function highlights the cells based on their values. 
        /// TODO: User-configurable.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void customCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.Value != null && e.ColumnIndex > 0 && e.Value.ToString().Length > 0)
            {
                int val = Int32.Parse(e.Value.ToString());
                if (val > 10 && val < 50)
                    e.CellStyle.BackColor = Color.PaleGreen;
                else if (val >= 50 && val < 100)
                    e.CellStyle.BackColor = Color.DeepSkyBlue;
                else if (val >= 100)
                    e.CellStyle.BackColor = Color.OrangeRed;
            }
        }
    }
}
