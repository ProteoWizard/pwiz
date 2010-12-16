using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using BrightIdeasSoftware;
using IDPicker;
using IdPickerGui.BLL;
using System.IO;

namespace Forms
{
    public partial class ProteinValidationPane : UserControl
    {
        // Map loci to protein data
        Map<string, Protein> proteins;
        
        // Current protein and interpetation 
        //that are being displayed by the control
        string currentProtein;
        string currentInterpretation;
        
        // Current spectrum being dispayed
        DataGridViewRow currentSpectraGridSelection;
        
        // A map of secondary matches to the spectra in this dataset
        Map<UniqueSpectrumID,VariantInfo> alternativeInterpretations;

        public ProteinValidationPane()
        {
            InitializeComponent();
            currentProtein = null;
            currentInterpretation = null;
            currentSpectraGridSelection = null;
            alternativeInterpretations = null;
        }

        public ProteinValidationPane(Map<string, Protein> prots, ref Map<UniqueSpectrumID, VariantInfo> alts)
        {
            InitializeComponent();
            proteins = prots;
            setUpProteinGrid();
            peptideTree.Dock = DockStyle.Fill;
            currentSpectraGridSelection = null;
            alternativeInterpretations = alts;
        }

        /// <summary>
        /// This function sets up the protein grid, which shows the list of proteins
        /// that contain a particualr modification
        /// </summary>
        public void setUpProteinGrid()
        {
            // Get the table, add columns
            DataTable tbl = new DataTable();
            tbl.Columns.Add("CID", typeof(string));
            tbl.Columns.Add("Locus", typeof(string));
            tbl.Columns.Add("#Peps", typeof(int));
            tbl.Columns.Add("#Vars", typeof(int));
            tbl.Columns.Add("#Spec", typeof(int));
            // Populate the rows
            foreach (var protein in proteins.Values)
            {
                DataRow newRow = tbl.NewRow();
                newRow["CID"] = protein.clusterID;
                newRow["Locus"] = protein.proteinID;
                newRow["#Peps"] = protein.peptides.Count;
                int varCount = 0;
                foreach (var pepInfo in protein.interpretations)
                    varCount += pepInfo.Value.Count;
                newRow["#Vars"] = varCount;
                int specCount = 0;
                foreach (var specs in protein.spectra)
                    specCount += specs.Value.Count;
                newRow["#Spec"] = specCount;
                tbl.Rows.Add(newRow);
            }

            Font newFont = new Font(proteinGrid.Font, FontStyle.Bold);
            proteinGrid.ReadOnly = true;
            proteinGrid.DataSource = tbl.DefaultView;
            // Format the columns and cells
            proteinGrid.ColumnHeadersDefaultCellStyle.Font = newFont;
            proteinGrid.Columns["CID"].Width = 30;
            proteinGrid.Columns["Locus"].Width = proteinGrid.Columns["Locus"].Width + 30;
            proteinGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            proteinGrid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            proteinGrid.Columns["Locus"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            proteinGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            proteinGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            // Hide the row headers, presort the table, and set the selection mode to "entire row" only
            proteinGrid.RowHeadersVisible = false;
            proteinGrid.Sort(proteinGrid.Columns["#Spec"], ListSortDirection.Descending);
            proteinGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            proteinGrid.MultiSelect = false;
            proteinGrid.AllowUserToResizeColumns = true;
            proteinGrid.AllowUserToOrderColumns = false;
            // Set the row selection event handler
            proteinGrid.SelectionChanged += new EventHandler(proteinGrid_SelectedRowsClick);
            // Set up the export options as row selection context menu items
            setupContextMenu();
        }

        /// <summary>
        /// This function sets up some export options as row selection context menu items
        /// </summary>
        public void setupContextMenu()
        {
            // Set the protein centric export. This lists all proteins and 
            // peptides seen in this view. Shared peptides are represented 
            // twice.
            ToolStripMenuItem exportSelection = new ToolStripMenuItem();
            exportSelection.Text = "Export Selection - Protein centric";
            exportSelection.Click += new EventHandler(export_click);
            // Same as above, except it exports all protein in the view
            ToolStripMenuItem exportAll = new ToolStripMenuItem();
            exportAll.Text = "Export All - Protein centric";
            exportAll.Click += new EventHandler(exportAll_click);
            // Set the peptide centric export. This lists all peptides seen 
            // in this view. Shared peptides are represented only once.
            ToolStripMenuItem exportSelectedPeptides = new ToolStripMenuItem();
            exportSelectedPeptides.Text = "Export Selection - Peptide centric";
            exportSelectedPeptides.Click += new EventHandler(exportSelectPeptide_click);
            // Same as above, except it exports all peptides in the view
            ToolStripMenuItem exportAllPeptides = new ToolStripMenuItem();
            exportAllPeptides.Text = "Export All - Peptide centric";
            exportAllPeptides.Click += new EventHandler(exportAllPeptide_click);
            
            // Add the menu items
            ContextMenuStrip strip = new ContextMenuStrip();
            foreach (DataGridViewColumn column in proteinGrid.Columns)
            {
                column.ContextMenuStrip = strip;
                column.ContextMenuStrip.Items.Add(exportSelection);
                column.ContextMenuStrip.Items.Add(exportSelectedPeptides);
                column.ContextMenuStrip.Items.Add(exportAll);
                column.ContextMenuStrip.Items.Add(exportAllPeptides);
            }
        }

        /// <summary>
        /// This function gets the first selected row in the protein grid and prepares
        /// the peptide tree display
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void proteinGrid_SelectedRowsClick(object sender, EventArgs e)
        {
            int selectedRowCount = proteinGrid.Rows.GetRowCount(DataGridViewElementStates.Selected);

            if (selectedRowCount > 0)
            {
                // Find the protein locus
                int rowIndex = Int32.Parse(proteinGrid.CurrentRow.Index.ToString());
                string locus = proteinGrid["Locus", rowIndex].Value.ToString();

                if (locus != null && locus.Length > 0 )
                {
                    // Set the peptide, spectra, and annotations panels invisible.
                    currentProtein = locus;
                    currentInterpretation = null;
                    spectraGrid.Visible = false;
                    peptideTree.Visible = false;
                    splitContainer1.Panel2.Hide();
                    splitContainer4.Panel2.Hide();
                    currentSpectraGridSelection = null;
                    Application.DoEvents();
                    // Setup the peptide display
                    setupPeptideGrid();
                }
            }
        }

        #region ReportExport
        
        public enum ReportTypes { EXPORT_SELECTED_PROTEIN, EXPORT_ALL_PROTEINS, EXPORT_SELECTED_PEPTIDE, EXPORT_ALL_PEPTIDES };

        public void exportAll_click(object sender, EventArgs e)
        {
            createReport(ReportTypes.EXPORT_ALL_PROTEINS);
        }

        public void export_click(object sender, EventArgs e)
        {
            createReport(ReportTypes.EXPORT_SELECTED_PROTEIN);
        }

        public void exportAllPeptide_click(object sender, EventArgs e)
        {
            createReport(ReportTypes.EXPORT_ALL_PEPTIDES);
        }

        public void exportSelectPeptide_click(object sender, EventArgs e)
        {
            createReport(ReportTypes.EXPORT_SELECTED_PEPTIDE);
        }
        
        public void createReport(ReportTypes type)
        {
            Set<string> loci = new Set<string>();
            if (type == ReportTypes.EXPORT_ALL_PEPTIDES || type == ReportTypes.EXPORT_ALL_PROTEINS)
            {
                foreach (DataGridViewRow row in proteinGrid.Rows)
                {
                    string locus = proteinGrid["Locus", row.Index].Value.ToString();
                    if (locus != null && locus.Length > 0)
                        loci.Add(locus);
                }
            }
            else if (type == ReportTypes.EXPORT_SELECTED_PEPTIDE || type == ReportTypes.EXPORT_SELECTED_PROTEIN)
            {
                int selectedRowCount = proteinGrid.Rows.GetRowCount(DataGridViewElementStates.Selected);
                if (selectedRowCount > 0)
                {
                    // Find the protein locus
                    int rowIndex = Int32.Parse(proteinGrid.CurrentRow.Index.ToString());
                    string locus = proteinGrid["Locus", rowIndex].Value.ToString();
                    if (locus != null && locus.Length > 0)
                        loci.Add(locus);
                }
            }
            if (loci.Count > 0)
            {
                // Get the file to save
                SaveFileDialog outputFile = new SaveFileDialog();
                outputFile.Filter = "CSV|*.csv";
                outputFile.Title = "Export the results...";
                outputFile.ShowDialog();
                if (outputFile.FileName.Length == 0)
                    return;
                StreamWriter writer = new StreamWriter(outputFile.OpenFile());
                if (type == ReportTypes.EXPORT_ALL_PROTEINS || type == ReportTypes.EXPORT_SELECTED_PROTEIN)
                    exportProteinReport(loci, writer);
                else if (type == ReportTypes.EXPORT_ALL_PEPTIDES || type == ReportTypes.EXPORT_SELECTED_PEPTIDE)
                    exportPeptideReport(loci, writer);
                writer.Close();
            }
        }
        /// <summary>
        /// This function exports a detailed protein modification report.
        /// </summary>
        public void exportProteinReport(Set<string> loci, StreamWriter writer)
        {
            
            if(loci.Count == 0)
                return;
            Map<string, Map<string, int>> table = new Map<string, Map<string, int>>();
            Set<string> allSources = new Set<string>();
            foreach (var locus in loci)
                foreach (var peptide in proteins[locus].peptides)
                    foreach (var interp in proteins[locus].interpretations[peptide])
                        foreach (var variant in proteins[locus].variants[interp])
                        {
                            string key = locus + "," + interp;
                            foreach (var spectrum in proteins[locus].spectra[variant])
                            {
                                string sourceName = spectrum.id.source.group.name;
                                allSources.Add(sourceName);
                                ++table[key][sourceName];
                                SourceGroupList parentGroups = new SourceGroupList();
                                foreach (var parent in spectrum.id.source.group.getAllParentGroupPaths())
                                {
                                    allSources.Add(parent);
                                    ++table[key][parent];
                                }

                            }
                        }
            writer.Write("Protein,peptide");
            foreach(var source in allSources)
                writer.Write(","+source);
            writer.WriteLine();
            foreach(var keys in table) 
            {
                StringBuilder line = new StringBuilder();
                line.Append(keys.Key);
                foreach(var source in allSources)
                {
                    line.Append("," + table[keys.Key][source]);
                }
                writer.WriteLine(line.ToString());
            }
            writer.Flush();
        }

        public void exportPeptideReport(Set<string> loci, StreamWriter writer)
        {
            if(loci.Count == 0)
                return;
            // Interpretation to variant map
            Map<string,Set<VariantInfo>> uniqueVariants = new Map<string,Set<VariantInfo>>();
            // Interpretation to protein ID map
            Map<string,Set<ProteinInstanceInfo>> varToProteinMap = new Map<string,Set<ProteinInstanceInfo>>();
            // Variant to spectrum map
            Map<VariantInfo, Set<SpectrumInfo>> varToSpecMap = new Map<VariantInfo,Set<SpectrumInfo>>();
            foreach(var locus in loci)
                foreach (var peptide in proteins[locus].peptides)
                    foreach (var interp in proteins[locus].interpretations[peptide])
                        foreach (var variant in proteins[locus].variants[interp])
                        {
                            uniqueVariants[variant.ToInsPecTStyle()].Add(variant);
                            foreach (var spectrum in proteins[locus].spectra[variant])
                                varToSpecMap[variant].Add(spectrum);
                            foreach (var protein in variant.peptide.proteins.Values)
                                varToProteinMap[interp].Add(protein);
                        }

            Map<string, Map<string, int>> table = new Map<string, Map<string, int>>();
            Set<string> allSources = new Set<string>();
            foreach(var interp in uniqueVariants.Keys) 
                foreach(var variant in uniqueVariants[interp]) 
                    foreach(var spectrum in varToSpecMap[variant])
                    {
                        string sourceName = spectrum.id.source.group.name;
                        allSources.Add(sourceName);
                        ++table[interp][sourceName];
                        SourceGroupList parentGroups = new SourceGroupList();
                        foreach (var parent in spectrum.id.source.group.getAllParentGroupPaths())
                        {
                            allSources.Add(parent);
                            ++table[interp][parent];
                        }
                    }

            writer.Write("Peptide,Protein,Alternatives");
            foreach (var source in allSources)
                writer.Write("," + source);
            writer.WriteLine();
            foreach ( var keys in table)
            {
                StringBuilder line = new StringBuilder();
                line.Append(keys.Key);
                Set<ProteinInstanceInfo>.Enumerator proIter = varToProteinMap[keys.Key].GetEnumerator();
                proIter.MoveNext();
                ProteinInstanceInfo first = proIter.Current;
                line.Append(","+first.protein.locus);
                StringBuilder alts = new StringBuilder();
                while(proIter.MoveNext())
                    alts.Append(proIter.Current.protein.locus+";");
                line.Append(","+alts.ToString());
                foreach (var source in allSources)
                {
                    line.Append("," + table[keys.Key][source]);
                }
                writer.WriteLine(line.ToString());
            }
            writer.Flush();
        }
        #endregion ReportExport

        /// <summary>
        /// This function takes locus of a protein and prepares the peptide grid display
        /// </summary>
        /// <param name="locus"></param>
        public void setupPeptideGrid()
        {
            if (currentProtein == null)
                return;
            currentInterpretation = null;
            // Clear the tree, make it invisible, and
            // freeze any repaints to the tree while we prep it.
            if (peptideTree.Columns.Count > 0)
            {
                peptideTree.ClearObjects();
                peptideTree.ClearCachedInfo();
            }
            peptideTree.Clear();
            peptideTree.CheckBoxes = false;
            peptideTree.Visible = false;
            peptideTree.BeginUpdate();

            if (proteins[currentProtein].peptides.Count > 0)
            {
                // Add columns
                OLVColumn peptide = new OLVColumn();
                peptide.AspectName = "Peptide";
                peptide.DisplayIndex = 0;
                peptide.Text = "Peptide";
                peptide.TextAlign = HorizontalAlignment.Center;
                peptide.FillsFreeSpace = true;
                peptide.FreeSpaceProportion = 65;
                OLVColumn mass = new OLVColumn();
                mass.AspectName = "Mass";
                mass.DisplayIndex = 1;
                mass.Text = "Mass";
                mass.TextAlign = HorizontalAlignment.Center;
                mass.FillsFreeSpace = true;
                mass.FreeSpaceProportion = 15;
                OLVColumn ntt = new OLVColumn();
                ntt.AspectName = "NTT";
                ntt.DisplayIndex = 2;
                ntt.Text = "NTT";
                ntt.TextAlign = HorizontalAlignment.Center;
                OLVColumn numVars = new OLVColumn();
                numVars.AspectName = "#Vars";
                numVars.DisplayIndex = 3;
                numVars.Text = "#Vars";
                numVars.TextAlign = HorizontalAlignment.Center;
                numVars.FillsFreeSpace = true;
                numVars.FreeSpaceProportion = 10;
                OLVColumn numSpec = new OLVColumn();
                numSpec.AspectName = "#Spec";
                numSpec.DisplayIndex = 4;
                numSpec.Text = "#Spec";
                numSpec.TextAlign = HorizontalAlignment.Center;
                numSpec.FillsFreeSpace = true;
                numSpec.FreeSpaceProportion = 10;

                peptideTree.Columns.AddRange(new ColumnHeader[] { peptide, mass, ntt, numVars, numSpec });
                peptideTree.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                                        | System.Windows.Forms.AnchorStyles.Left)
                                        | System.Windows.Forms.AnchorStyles.Right)));
                // Add the functions that get values for each column
                peptide.AspectGetter = delegate(object x)
                {
                    if (x is PeptideInfo)
                        return ((PeptideInfo)x).sequence;
                    //return ((VariantInfo)x).ToInsPecTStyle();
                    return x as string;
                };
                mass.AspectGetter = delegate(object x)
                {
                    if (x is PeptideInfo)
                        return ((PeptideInfo)x).mass;
                    //return ((VariantInfo)x).Mass;
                    return proteins[currentProtein].variants[x as string].ElementAt(0).Mass;
                };
                ntt.AspectGetter = delegate(object x)
                {
                    PeptideInfo pep;
                    if (x is PeptideInfo)
                        pep = ((PeptideInfo)x);
                    else
                        //pep = ((VariantInfo)x).peptide;
                        pep = proteins[currentProtein].variants[x as string].ElementAt(0).peptide;
                    int NTT = 0;
                    if (pep.NTerminusIsSpecific)
                        ++NTT;
                    if (pep.CTerminusIsSpecific)
                        ++NTT;
                    return NTT;
                };
                numVars.AspectGetter = delegate(object x)
                {
                    if (x is PeptideInfo && currentProtein != null)
                        return proteins[currentProtein].interpretations[((PeptideInfo)x)].Count;
                    return 1;
                };
                numSpec.AspectGetter = delegate(object x)
                {
                    if (currentProtein == null)
                        return -1;
                    int count = 0;
                    if (x is PeptideInfo)
                    {
                        foreach (var interp in proteins[currentProtein].interpretations[x as PeptideInfo])
                            foreach(var var in proteins[currentProtein].variants[interp])
                                count += proteins[currentProtein].spectra[var].Count;
                        return count;
                    } else if(x is string)
                    {
                        foreach(var var in proteins[currentProtein].variants[x as string])
                            count += proteins[currentProtein].spectra[var].Count;
                    }
                    return count;
                    //return proteins[currentProtein].spectra[(VariantInfo)x].Count;
                };
                // Add the function that tells whether the tree can be 
                // expanded at a specified node
                peptideTree.CanExpandGetter = delegate(object x)
                {
                    if (x is PeptideInfo)
                        return true;
                    return false;
                };

                // Add the function that gets children of an 
                // expanded node.
                peptideTree.ChildrenGetter = delegate(object x)
                {
                    if (x is PeptideInfo && currentProtein != null)
                    {
                        return proteins[currentProtein].interpretations[(PeptideInfo)x];
                    }
                    return new ArrayList();
                };

                // Add the function to format rows differently
                // depending on the object displayed in the row
                peptideTree.RowFormatter = delegate(OLVListItem item)
                {
                    if (item.RowObject is PeptideInfo)
                        item.BackColor = Color.FromArgb(192, 192, 255);
                    else if (item.RowObject is VariantInfo)
                        item.BackColor = Color.LightSkyBlue;
                };

                // Add an event trigger that listens for a selection on the
                // variants and prepares the spectra display.
                peptideTree.SelectionChanged += delegate(object sender, EventArgs e)
                {
                    ObjectListView obj = (ObjectListView)sender;
                    if (obj.GetSelectedObject() is string &&
                        currentInterpretation != ((string)obj.GetSelectedObject()) &&
                        currentProtein != null)
                    {
                        //MessageBox.Show(((VariantInfo)obj.GetSelectedObject()).ToInsPecTStyle() + ":" + currentProtein);
                        currentInterpretation = obj.GetSelectedObject() as string;
                        prepareSpectraGrid();
                    }
                    else if (obj.GetSelectedObject() is PeptideInfo)
                    {
                        currentInterpretation = null;
                        spectraGrid.Visible = false;
                        if(currentSpectrumViewer!=null)
                        {
                            currentSpectrumViewer.spectrumPanel.Hide();
                            currentSpectrumViewer.fragmentationPanel.Hide();
                            currentSpectrumViewer.annotationPanel.Hide();
                        }
                    }
                };
                // Set the tree style and make it visible.
                peptideTree.VirtualMode = true;
                peptideTree.Roots = proteins[currentProtein].peptides;
                peptideTree.Dock = DockStyle.Fill;
                peptideTree.Visible = true;
                peptideTree.EndUpdate();
            }
        }
        
        SpectrumViewer currentSpectrumViewer;

        /// <summary>
        /// This function trigges when an interpretation is double clicked. 
        /// It prepares the annotation and spectrum viewer panels.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void triggerSpectrumViewer(object sender, EventArgs e)
        {
            // Get the current row
            DataGridViewRow row = spectraGrid.CurrentRow;
            if(row != null) 
            {
                // Get the result and variant objects associated with this row
                Object[] tags = row.Tag as Object[];
                SpectrumInfo spectrum = tags[0] as SpectrumInfo;
                VariantInfo variant = tags[1] as VariantInfo;
                if (spectrum != null && currentSpectraGridSelection != row) 
                {
                    try {
                        // Resolve the spectrum source
                        var source = Path.GetFileNameWithoutExtension(spectrum.id.source.name);
                        var paths = Properties.Settings.Default.SourcePath.Split(";".ToCharArray());
                        var matches = Util.FindFileInSearchPath(source, new string[] { "mzXML", "mzML", "mgf", "RAW" }, paths.ToArray(), true);
                        if(matches.Length == 0)
                            MessageBox.Show("Can't find source. Set the source folder path.");
                        else 
                        {
                            // Convert the interpretation for seems compatibility
                            var interpetation = variant.ToSimpleString();
                            interpetation = interpetation.Replace('(', '[');
                            interpetation = interpetation.Replace(')', ']');
                            splitContainer4.Panel2.Hide();
                            splitContainer1.Panel2.Hide();
                            splitContainer5.Panel1.Hide();
                            splitContainer5.Panel2.Hide();
                            Application.DoEvents();
                            UniqueSpectrumID uniqueSpectrumID;
                            // Create a spectrum viewer and add its components
                            if(spectrum.nativeID!=null && spectrum.nativeID.Length>0) 
                            {
                                currentSpectrumViewer = new SpectrumViewer(matches[0],spectrum.nativeID,interpetation);
                                uniqueSpectrumID = new UniqueSpectrumID(source, spectrum.nativeID, spectrum.id.charge);
                            }
                            else
                            {
                                currentSpectrumViewer = new SpectrumViewer(matches[0], spectrum.id.index, interpetation);
                                uniqueSpectrumID = new UniqueSpectrumID(source, spectrum.id.index, spectrum.id.charge);
                            }
                            splitContainer4.Panel2.Controls.Clear();
                            splitContainer4.Panel2.Controls.Add(currentSpectrumViewer.annotationPanel);
                            splitContainer5.Panel1.Controls.Clear();
                            splitContainer5.Panel1.Controls.Add(currentSpectrumViewer.spectrumPanel);
                            splitContainer5.Panel2.Controls.Clear();
                            splitContainer5.Panel2.Controls.Add(currentSpectrumViewer.fragmentationPanel);
                            splitContainer5.Panel2.AutoScroll = true;
                            splitContainer1.Panel2.Controls.Add(splitContainer5);
                            splitContainer1.Panel2.Show();
                            splitContainer4.Panel2.Show();
                            splitContainer5.Panel1.Show();
                            splitContainer5.Panel2.Show();
                            Application.DoEvents();
                            // If we have a seconday result associated with this spectrum, 
                            // set the mechanism in spectrum viewer to see it.
                            if(alternativeInterpretations != null)
                            {
                                if (alternativeInterpretations.Contains(uniqueSpectrumID))
                                {
                                    VariantInfo alt = alternativeInterpretations[uniqueSpectrumID];
                                    interpetation = alt.ToSimpleString();
                                    interpetation = interpetation.Replace('(', '[');
                                    interpetation = interpetation.Replace(')', ']');
                                    currentSpectrumViewer.setSecondarySequence(interpetation);
                                }
                            }
                            currentSpectraGridSelection = row;
                        }
                    } catch(Exception exp) { 
                        MessageBox.Show(exp.StackTrace);
                        MessageBox.Show("Failed to show spectrum. Check raw data path.");
                    }
                }
            }
        }

        /// <summary>
        /// This function prepares the spectrum table for user display
        /// </summary>
        public void prepareSpectraGrid()
        {
            if (currentProtein != null && currentInterpretation != null)
            {
                // Set the grid invisible while it's being prepped.
                spectraGrid.Visible = false;
                spectraGrid.Rows.Clear();
                spectraGrid.Columns.Clear();
                currentSpectraGridSelection = null;
                splitContainer1.Panel2.Controls.Clear();
                splitContainer4.Panel2.Controls.Clear();
                this.Refresh();

                // Add columns
                spectraGrid.Columns.Add("Source", "Source");
                spectraGrid.Columns["Source"].ValueType = typeof(string);
                spectraGrid.Columns.Add("NativeID", "NativeID");
                spectraGrid.Columns["NativeID"].ValueType = typeof(string);
                spectraGrid.Columns.Add("Mass", "Mass");
                spectraGrid.Columns["Mass"].ValueType = typeof(double);
                spectraGrid.Columns.Add("Z", "Z");
                spectraGrid.Columns["Z"].ValueType = typeof(int);
                spectraGrid.Columns.Add("MassErr(Da.)", "MassErr(Da.)");
                spectraGrid.Columns["MassErr(Da.)"].ValueType = typeof(double);

                Application.DoEvents();
                Protein prot = proteins[currentProtein];
                // Add rows
                foreach(var var in prot.variants[currentInterpretation])
                    foreach (var spectrum in prot.spectra[var])
                    {
                        DataGridViewRow row = new DataGridViewRow();
                        int index = spectraGrid.Rows.Add(row);
                        spectraGrid.Rows[index].Cells["Source"].Value = spectrum.id.source;
                        spectraGrid.Rows[index].Cells["NativeID"].Value = spectrum.nativeID;
                        spectraGrid.Rows[index].Cells["Mass"].Value = Math.Round(spectrum.precursorMass, 3);
                        spectraGrid.Rows[index].Cells["Z"].Value = spectrum.id.charge;
                        spectraGrid.Rows[index].Cells["MassErr(Da.)"].Value = Math.Round(spectrum.precursorMass - var.Mass, 3);
                        // ResultInstance and VariantInfo objects associated with this row.
                        spectraGrid.Rows[index].Tag = new Object[]{spectrum, var};
                    }

                // Format the columns and cells
                spectraGrid.Dock = DockStyle.Fill;
                spectraGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                spectraGrid.RowHeadersVisible = false;
                spectraGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                spectraGrid.MultiSelect = false;
                spectraGrid.Columns["Z"].Width = 20;
                spectraGrid.Columns["Mass"].Width = 30;
                spectraGrid.Columns["MassErr(Da.)"].Width = 30;
                spectraGrid.Columns["Source"].Width = spectraGrid.Columns["Source"].Width + 40;
                spectraGrid.Columns["NativeID"].Width = spectraGrid.Columns["NativeID"].Width + 40;
                spectraGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                Font newFont = new Font(spectraGrid.Font, FontStyle.Bold);
                spectraGrid.ReadOnly = true;
                spectraGrid.ColumnHeadersDefaultCellStyle.Font = newFont;
                spectraGrid.AllowUserToResizeColumns = true;
                spectraGrid.AllowUserToOrderColumns = false;
                // Set the event handler for spectrum viewer
                spectraGrid.DoubleClick += new EventHandler(triggerSpectrumViewer);
                spectraGrid.Visible = true;
                Application.DoEvents();
            }
        }
    }
}
