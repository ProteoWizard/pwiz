using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using IDPicker;
using System.IO;
using Forms.Controls;
using IdPickerGui.BLL;
using pwiz.CLI.proteome;

namespace Forms
{
    
    public partial class PTMDigger : Form
    {

        Map<string, int> tabPageIndex;
        DeltaMassTable originalDeltaMassTable;
        DeltaMassTable attestedDeltaMassTable;
        DeltaMassTable unattestedDeltaMassTable;

        /// <summary>
        /// This function filters rows in the data view based on total spectral counts
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RowTotalFilterThresholdComboBox_TextChanged(object sender, EventArgs e)
        {
            int threshold = 1;
            try { threshold = Int32.Parse(RowTotalFilterThresholdComboBox.Text); }
            catch (Exception) { }
            if(originalDeltaMassTable != null)
                originalDeltaMassTable.PTMData.deltaMassTable.DefaultView.RowFilter = "Total >= " + threshold;
            if(attestedDeltaMassTable != null)
                attestedDeltaMassTable.PTMData.deltaMassTable.DefaultView.RowFilter = "Total >= " + threshold;
            if(unattestedDeltaMassTable != null)
                unattestedDeltaMassTable.PTMData.deltaMassTable.DefaultView.RowFilter = "Total >= " + threshold;
        }

        /// <summary>
        /// This event handler removes the tab page when double-clicked. It spares
        /// the main "Delta Masses" tab page.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void tabPageControl_DoubleClick(object sender, EventArgs e)
        {
            if (!tabControl.SelectedTab.Name.Contains("Delta Masses"))
            {
                TabPage selectedTab = tabControl.SelectedTab;
                tabControl.TabPages.Remove(tabControl.SelectedTab);
                if(selectedTab.Name.Contains("Att."))
                    tabControl.SelectTab("Attested Delta Masses");
                else if(selectedTab.Name.Contains("Unatt."))
                    tabControl.SelectTab("Unattested Delta Masses");
                else
                    tabControl.SelectTab("Delta Masses");
            }
        }

        public delegate void UpdateTabPage(ref DeltaMassTable table, string name, string text, int imageIndex);
        public void updateTabPage(ref DeltaMassTable table, string name, string text, int imageIndex)
        {
            table.Visible = false;
            // Check to see if already exists, create a new one
            // iff there isn't a page with this name already
            if (tabControl.TabPages[name] != null)
                tabControl.SelectTab(name);
            else 
            {
                TabPage newPage = new TabPage(name);
                newPage.Name = name;
                newPage.Text = text;
                newPage.ImageIndex = imageIndex;
                newPage.Controls.Add(table);
                tabControl.TabPages.Add(newPage);
                tabControl.SelectTab(newPage);
            }
            table.Visible = true;
        }

        #region attestation variables
        public enum AttestationStatus { PRIMARY_RESULT_IS_BETTER, SECONDARY_RESULT_IS_BETTER, AMBIGUOUS, UNKNOWN, IGNORE };
        // A dialog for choosing parameters to attest the primary PSMs
        AttestationDialog dialog;
        /// <summary>
        /// These two maps hold the two independent interpretations 
        /// for each spectrum
        /// </summary>
        public Map<UniqueSpectrumID, VariantInfo> primaryResults;
        public Map<UniqueSpectrumID, VariantInfo> secondaryResults;
        public Map<UniqueSpectrumID, AttestationStatus> attestationResults;
        #endregion attestation variables

        public PTMDigger()
        {
            InitializeComponent();
            tabPageIndex = new Map<string, int>();
            tabControl.DoubleClick += new EventHandler(tabPageControl_DoubleClick);
        }

        /// <summary>
        /// This function reads the final assembly file into a give workspace
        /// </summary>
        /// <param name="workspace">Pass a workspace by reference</param>
        /// <param name="progress">Progress bar for notification</param>
        /// <param name="assemblyFile">idpXML file containing the final results</param>
        public void loadWorkspace(ref Workspace workspace, ref GhettoProgressControl progress, string assemblyFile)
        {
            long start;
            if (workspace == null)
            {
                return;
            }

            try
            {
                progress.showProgress("Reading and parsing IDPicker XML from filepath: " + assemblyFile);
                start = DateTime.Now.Ticks;
                workspace.readPeptidesXml(new StreamReader(assemblyFile), "", 1.0f, 1);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.StackTrace.ToString());
                Console.Error.WriteLine("Error reading input filepath \"" + assemblyFile + "\": " + e.Message);
                Environment.Exit(1);
            }

            start = DateTime.Now.Ticks;
            progress.showProgress("Assembling protein groups...");
            workspace.assembleProteinGroups();
            // Create peptide groups by grouping peptide identifications
            // that mapped to same proteins into a single group.
            start = DateTime.Now.Ticks;
            progress.showProgress("Assembling peptide groups...");
            workspace.assemblePeptideGroups();
            // Recursively generate clusters containing equivalent and subset proteins.
            // Also connect the corresponding proteins and peptides in a cluster.
            start = DateTime.Now.Ticks;
            progress.showProgress("Assembling clusters...");
            workspace.assembleClusters();
            start = DateTime.Now.Ticks;
            // Determine the minimum number of protein clusters needed
            // to explain all the peptides.
            progress.updateMax(progress.getMax() + workspace.clusters.Count + 1);
            progress.showProgress("Assembling minimum covering set for clusters...");
            int clusterCount = 0;
            foreach (ClusterInfo c in workspace.clusters)
            {
                ++clusterCount;
                progress.showProgress("Assembling minimum covering set for cluster " + clusterCount + " of " + workspace.clusters.Count + " (" + c.proteinGroups.Count + " protein groups, " + c.results.Count + " results)          \r");
                workspace.assembleMinimumCoveringSet(c);
            }
        }

        /// <summary>
        /// This function reads in the secondary results for each spectrum.
        /// It also reconciles the primary and secondary results for each spectrum.
        /// </summary>
        public void loadSecondaryResults()
        {
            try
            {
                // IDP ghetto progress bar
                GhettoProgressControl progress = new GhettoProgressControl(4);
                // Intitialize a workspace for secondary results
                if (secondaryMatchesWorkspace == null)
                {
                    secondaryMatchesWorkspace = new Workspace();
                    rtConfig = new Workspace.RunTimeConfig();
                }
                loadWorkspace(ref secondaryMatchesWorkspace, ref progress, secondaryResultsAssembly);
                // Extract the PSMs and store them
                secondaryResults = extractSpectrumIDs(ref secondaryMatchesWorkspace);
                if(originalDeltaMassTable!=null)
                    originalDeltaMassTable.setSecondaryResults(ref secondaryResults);
                if(attestedDeltaMassTable!=null)
                    attestedDeltaMassTable.setSecondaryResults(ref secondaryResults);
                if(unattestedDeltaMassTable!=null)
                    unattestedDeltaMassTable.setSecondaryResults(ref secondaryResults);
                // If user wants us to reconcile primary PSMs with secondary PSMs
                if (dialog.autoAttestResultsCheckBox.Checked)
                {
                    progress.updateMax(progress.getMax() + primaryResults.Count + 1);
                    progress.showProgress("Auto validating PSMs...");
                    int psmCount = 0;
                    int numAmbResults = 0;
                    int numPrimResultsAreBetter = 0;
                    int numSecondaryResultsAreBetter = 0;
                    double deltaTICThresholdSetByUser = -1;
                    double deltaXCorrThreshldSetByUser = -1;
                    attestationResults = new Map<UniqueSpectrumID,AttestationStatus>();
                    try
                    {
                        deltaTICThresholdSetByUser = double.Parse(dialog.deltaTICTextBox.Text);
                        deltaTICThresholdSetByUser /= 100.0;
                    }
                    catch (Exception) { }
                    try
                    {
                        deltaXCorrThreshldSetByUser = double.Parse(dialog.deltaXCorrThresholdTextBox.Text);
                        deltaXCorrThreshldSetByUser /= 100.0;
                    }
                    catch (Exception) { }

                    foreach (var primaryPSM in primaryResults)
                    {
                        progress.showProgress("Auto attesting PSM " + (++psmCount) + " of " + primaryResults.Count + "...");
                        // Get the unique spectrum key, primary peptide, and secondary peptide
                        UniqueSpectrumID primarySpectrum = primaryPSM.Key;
                        VariantInfo primaryResult = primaryPSM.Value;
                        string primaryInterpretation = primaryResult.ToSimpleString();
                        primaryInterpretation = primaryInterpretation.Replace('(', '[');
                        primaryInterpretation = primaryInterpretation.Replace(')', ']');
                        Peptide primaryPeptide;
                        int chargeState = primarySpectrum.charge;
                        try
                        {
                            primaryPeptide = new Peptide(primaryInterpretation,
                                pwiz.CLI.proteome.ModificationParsing.ModificationParsing_Auto,
                                pwiz.CLI.proteome.ModificationDelimiter.ModificationDelimiter_Brackets);
                        }
                        catch (Exception exp) { MessageBox.Show(exp.StackTrace); return; }
                        AttestationStatus status = AttestationStatus.UNKNOWN;
                        if (secondaryResults.Contains(primarySpectrum))
                        {
                            VariantInfo secondaryResult = secondaryResults[primarySpectrum];
                            try
                            {
                                // Resolve the spectrum source
                                var source = Path.GetFileNameWithoutExtension(primarySpectrum.spectrumSourceName);
                                var paths = Properties.Settings.Default.SourcePath.Split(";".ToCharArray());
                                var matches = Util.FindFileInSearchPath(source, new string[] { "mzXML", "mzML", "mgf", "RAW" }, paths.ToArray(), true);
                                if (matches.Length > 0)
                                {
                                    object index = primarySpectrum.idOrIndex;
                                    MassSpectrum spectrum = SpectrumCache.GetMassSpectrum(matches[0], index);
                                    // Work around to have a sort function in List.
                                    ZedGraph.IPointList peaks = spectrum.Points;
                                    List<ScoringUtils.Peak> rawPeaks = new List<ScoringUtils.Peak>();
                                    for (int i = 0; i < peaks.Count; ++i)
                                        rawPeaks.Add(new ScoringUtils.Peak(peaks[i].X, peaks[i].Y));
                                    // Remove the precursor and associated neutral loss peaks
                                    double precursorMZ = spectrum.Element.precursors[0].selectedIons[0].cvParam(pwiz.CLI.CVID.MS_selected_ion_m_z).value;
                                    ScoringUtils.erasePrecursorIons(precursorMZ, ref rawPeaks);

                                    Peptide secondaryPeptide;
                                    string secondaryInterp = secondaryResult.ToSimpleString();
                                    secondaryInterp = secondaryInterp.Replace('(', '[');
                                    secondaryInterp = secondaryInterp.Replace(')', ']');
                                    try
                                    {
                                        secondaryPeptide = new Peptide(secondaryInterp,
                                            pwiz.CLI.proteome.ModificationParsing.ModificationParsing_Auto,
                                            pwiz.CLI.proteome.ModificationDelimiter.ModificationDelimiter_Brackets);
                                    }
                                    catch (Exception) { return; }
                                    // NOTE: These tests should be performed in this order because the XCorr test
                                    // permanantly alters the intensities of the observed peaks.
                                    if (dialog.filterByTICRabioButton.Checked)
                                        status = ScoringUtils.compareMatchedTIC(rawPeaks, primaryPeptide, secondaryPeptide, deltaTICThresholdSetByUser);
                                    if(chargeState > 0 && dialog.filterByXCorrRadioButton.Checked)
                                    {
                                        double precursorMH = (precursorMZ*chargeState)-((chargeState-1)*1.007276);
                                        status = ScoringUtils.compareXCorrs(rawPeaks, primaryPeptide, secondaryPeptide, precursorMH, deltaXCorrThreshldSetByUser);
                                    }

                                    if (status == AttestationStatus.AMBIGUOUS)
                                        ++numAmbResults;
                                    else if (status == AttestationStatus.PRIMARY_RESULT_IS_BETTER)
                                        ++numPrimResultsAreBetter;
                                    else if (status == AttestationStatus.SECONDARY_RESULT_IS_BETTER)
                                        ++numSecondaryResultsAreBetter;
                                }
                            }
                            catch (Exception) { }
                        }
                        attestationResults.Add(new MutableKeyValuePair<UniqueSpectrumID, AttestationStatus>(primarySpectrum, status));
                    }
                    progress.showProgress("Splitting the delta mass table...");
                    if (dialog.generateNewDeltaMassTableCheckBox.Checked)
                        splitOriginalDeltaMassTable();
                    StringBuilder validationStatus = new StringBuilder();
                    validationStatus.AppendLine("Total # of PSMs attested: " + primaryResults.Count);
                    validationStatus.AppendLine("# PSMs with ambiguous results: " + numAmbResults);
                    validationStatus.AppendLine("# PSMs where primary results are better: " + numPrimResultsAreBetter);
                    validationStatus.AppendLine("# PSMs where secondary results are better: " + numSecondaryResultsAreBetter);
                    validationStatus.AppendLine("Total # of PSMs not attested: " + (primaryResults.Count - numAmbResults - numPrimResultsAreBetter - numSecondaryResultsAreBetter));
                    //MessageBox.Show(validationStatus.ToString());
                }
            }
            catch (Exception) { MessageBox.Show("Failed attestation process."); }
        }

        public void splitOriginalDeltaMassTable()
        {

            if(attestationResults.Count==0)
                return;
            
            attestedDeltaMassTable = new DeltaMassTable("Att.",0);
            attestedDeltaMassTable.Dock = DockStyle.Fill;
            AttestationStatus[] requiredStatus = new AttestationStatus[]{AttestationStatus.UNKNOWN, AttestationStatus.PRIMARY_RESULT_IS_BETTER};
            attestedDeltaMassTable.loadFromWorkspace(ref ws, ref attestationResults, requiredStatus);
            attestedDeltaMassTable.setSecondaryResults(ref secondaryResults);
            attestedDeltaMassTable.setControls(ref tabControl);
            attestedDeltaMassTable.updateUnimodStatus();
            attestedDeltaMassTable.updateRawDataPathStatus();

            unattestedDeltaMassTable = new DeltaMassTable("Unatt.",1);
            unattestedDeltaMassTable.Dock = DockStyle.Fill;
            requiredStatus = new AttestationStatus[] { AttestationStatus.SECONDARY_RESULT_IS_BETTER, AttestationStatus.AMBIGUOUS };
            unattestedDeltaMassTable.loadFromWorkspace(ref ws, ref attestationResults, requiredStatus);
            unattestedDeltaMassTable.setSecondaryResults(ref secondaryResults);
            unattestedDeltaMassTable.setControls(ref tabControl);
            unattestedDeltaMassTable.updateUnimodStatus();
            unattestedDeltaMassTable.updateRawDataPathStatus();

            UpdateTabPage update = new UpdateTabPage(updateTabPage);
            try
            {
                this.BeginInvoke(update, unattestedDeltaMassTable, "Unattested Delta Masses", "Delta Masses", 1);
                this.BeginInvoke(update, attestedDeltaMassTable, "Attested Delta Masses", "Delta Masses", 0);
                
            }
            catch (Exception)
            {
                MessageBox.Show("Error occured while attesting.");
            }
        }
        
        /// <summary>
        /// This function reads the finaly Assembly file generated by IDP Report.
        /// </summary>
        public void loadAssemble()
        {

            // IDP ghetto progress bar
            GhettoProgressControl progress = new GhettoProgressControl(5);

            if (ws == null)
            {
                ws = new Workspace();
                rtConfig = new Workspace.RunTimeConfig();
                rtConfig.MaxAmbiguousIds = 10;
            }

            loadWorkspace(ref ws, ref progress, inputAssembly);
            // Build mass tables and create traceable trees
            progress.showProgress("Building delta mass table...");
            originalDeltaMassTable = new DeltaMassTable("Orig.", -1);
            originalDeltaMassTable.Dock = DockStyle.Fill;
            primaryResults = new Map<UniqueSpectrumID,VariantInfo>();
            originalDeltaMassTable.loadFromWorkspace(ref ws, ref primaryResults);
            originalDeltaMassTable.setControls(ref tabControl);
            if(!RowTotalFilterThresholdComboBox.Enabled)
            {
                RowTotalFilterThresholdComboBox.Enabled = true;
                RowTotalFilterLabel.Enabled = true;
            }
            UpdateTabPage update = new UpdateTabPage(updateTabPage);
            try
            {
                this.BeginInvoke(update, originalDeltaMassTable, "Delta Masses", "Delta Masses", -1);
            }
            catch (Exception)
            {
                MessageBox.Show("Error while loading the assembly file...");
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            Environment.Exit(1);
        }

        /// <summary>
        /// This function triggers loading of a selected Assemble XML file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void openToolStripButton_Click(object sender, EventArgs e)
        {
            // Browse to the final assembly file
            OpenFileDialog fdlg = new OpenFileDialog();
            fdlg.Title = "Load Assembly file...";
            fdlg.InitialDirectory = @"c:\";
            fdlg.Filter = "Assemble XML (*.idpXML)|*.idpXML|All files (*.*)|*.*";
            fdlg.FilterIndex = 1;
            fdlg.RestoreDirectory = true;
            if (fdlg.ShowDialog() == DialogResult.OK)
            {
                inputAssembly = fdlg.FileName;
                // Grey out the load and start a new loading thread
                openToolStripButton.Enabled = false;
                Thread thd = new Thread(new ThreadStart(loadAssemble));
                thd.Start();
            }
        }

        /// <summary>
        /// Launches the validation process
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void openSecondaryResults_Click(object sender, EventArgs e)
        {
            dialog = new AttestationDialog();
            if(dialog.ShowDialog() == DialogResult.OK)
            {
                attest.Enabled = false;
                secondaryResultsAssembly = dialog.bigDBResultsFile.Text;
                Thread thd = new Thread(new ThreadStart(loadSecondaryResults));
                thd.Start();
            }
            
        }

        /// <summary>
        /// This function takes a workspace and extracts a map of PSMs
        /// </summary>
        /// <param name="workspace"></param>
        /// <returns></returns>
        public Map<UniqueSpectrumID, VariantInfo> extractSpectrumIDs(ref Workspace workspace)
        {
            Map<UniqueSpectrumID, VariantInfo> results = new Map<UniqueSpectrumID, VariantInfo>();
            foreach (SourceGroupList.MapPair groupItr in workspace.groups)
                foreach (SourceInfo source in groupItr.Value.getSources(true))
                    foreach (SpectrumList.MapPair sItr in source.spectra)
                    {
                        UniqueSpectrumID id = UniqueSpectrumID.extractUniqueSpectrumID(sItr.Value);
                        ResultInstance ri = sItr.Value.results[1];
                        VariantInfo vi = ri.info.peptides.Min;
                        results[id] = vi;
                    }
            return results;
        }

        /// <summary>
        /// This function updates the raw data folder path
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void addRawDataPath_Click(object sender, EventArgs e)
        {
            // Choose a folder and append it to the end of 
            // the Properties.Settings.Default.SourcePath variable
            var openFolder = new FolderBrowserDialog();
            var result = openFolder.ShowDialog();
            if (result == DialogResult.OK)
            {
                string folderName = openFolder.SelectedPath;
                if (Properties.Settings.Default.SourcePath.EndsWith(";"))
                    Properties.Settings.Default.SourcePath += folderName + ";";
                else
                    Properties.Settings.Default.SourcePath += ";" + folderName + ";";
                if(originalDeltaMassTable!=null)
                    originalDeltaMassTable.rawDataPath.Text = Properties.Settings.Default.SourcePath;
                if(attestedDeltaMassTable != null)
                    attestedDeltaMassTable.rawDataPath.Text = Properties.Settings.Default.SourcePath;
                if(unattestedDeltaMassTable != null)
                    unattestedDeltaMassTable.rawDataPath.Text = Properties.Settings.Default.SourcePath;
            }
        }

        /// <summary>
        /// This function loads the unimod XML file. It also attaches the
        /// unimod annotations for each cell in the delta mass table
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void loadUnimod_Click(object sender, EventArgs e)
        {
            // Browse to the final assembly file
            OpenFileDialog fdlg = new OpenFileDialog();
            fdlg.Title = "Load Unmod XML file...";
            fdlg.InitialDirectory = @"c:\";
            fdlg.Filter = "Unimod XML (*.xml)|*.xml|All files (*.*)|*.*";
            fdlg.FilterIndex = 1;
            fdlg.RestoreDirectory = true;
            string unimodXML = null;
            if (fdlg.ShowDialog() == DialogResult.OK)
            {
                unimodXML = fdlg.FileName;
            }
            if (unimodXML != null)
            {
                if (ws == null)
                {
                    ws = new Workspace();
                    rtConfig = new Workspace.RunTimeConfig();
                    rtConfig.MaxAmbiguousIds = 10;
                }
                try
                {
                    // Read the unimod data and associate with 
                    // each cell in the delta mass table.
                    ws.readUniModXML(unimodXML);
                    if (originalDeltaMassTable != null)
                        originalDeltaMassTable.updateUnimodStatus();
                    if(attestedDeltaMassTable != null)
                        attestedDeltaMassTable.updateUnimodStatus();
                    if(unattestedDeltaMassTable != null)
                        unattestedDeltaMassTable.updateUnimodStatus();
                    loadUnimod.Enabled = false;
                } catch (Exception ex) { MessageBox.Show(ex.ToString()); };
            }
        }
    }

    public class UniqueSpectrumID : IComparable<UniqueSpectrumID>
    {
        public string spectrumSourceName;
        public object idOrIndex;
        public int charge;

        public UniqueSpectrumID(string name, object index, int z)
        {
            spectrumSourceName = name;
            idOrIndex = index;
            charge = z;
        }

        public int CompareTo(UniqueSpectrumID other)
        {
            if (spectrumSourceName.CompareTo(other.spectrumSourceName) == 0)
            {
                int idOrIndexComp = 0;
                if (idOrIndex is string) 
                    idOrIndexComp = ((string)idOrIndex).CompareTo(other.idOrIndex as string);
                else
                    idOrIndexComp = ((int)idOrIndex).CompareTo((int)other.idOrIndex);
                if(idOrIndexComp == 0)
                    return charge.CompareTo(other.charge);
                return idOrIndexComp;
            }
            return spectrumSourceName.CompareTo(other.spectrumSourceName);
        }

        public static UniqueSpectrumID extractUniqueSpectrumID(SpectrumInfo spec)
        {
            UniqueSpectrumID id;
            string source = Path.GetFileNameWithoutExtension(spec.id.source.name);
            object index = spec.nativeID;
            if (index == null || spec.nativeID.Length == 0)
                index = spec.id.index;
            id = new UniqueSpectrumID(source, index, spec.id.charge);
            return id;
        }
    }
}
