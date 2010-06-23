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
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasaris
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Xml;

using IdPickerGui.MODEL;
using IdPickerGui.BLL;
using IDPicker;
using pwiz.CLI.proteome;

namespace IdPickerGui
{
    public partial class ExportReportForm : Form
    {
        private const string EXPORT_DIR = "\\export";

        private IDPickerInfo idPickerRequest;
        public IDPickerInfo IdPickerRequest
        {
            get { return idPickerRequest; }
            set { idPickerRequest = value; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="request"></param>
        public ExportReportForm(IDPickerInfo request)
        {
            try
            {
                InitializeComponent();

                IdPickerRequest = request;

                loadFormValues();

                checkEnableExportButton();
            }
            catch (Exception exc)
            {
                throw new Exception("Error initializing ExportReportForm.cs\r\n", exc);
            }
        }

        public ExportReportForm( IDPickerInfo request, string autoExportItem )
        {
            try
            {
                InitializeComponent();

                IdPickerRequest = request;

                loadFormValues();

                cboExportType.SelectedItem = autoExportItem;

                btnExport_Click( this, EventArgs.Empty );
            }
            catch( Exception exc )
            {
                throw new Exception( "Error initializing ExportReportForm.cs\r\n", exc );
            }
        }

        /// <summary>
        /// Log Exceptions (and inner exceptions) to file. Show ExceptionsDialogForm.
        /// </summary>
        /// <param name="exc"></param>
        private void HandleExceptions(Exception exc)
        {
            ExceptionsDialogForm excForm = new ExceptionsDialogForm();
            StringBuilder sbDetails = new StringBuilder();

            try
            {
                ExceptionManager.logExceptionsByFormToFile(this, exc, DateTime.Now);

                Exception subExc = exc.InnerException;
                sbDetails.Append(exc.Message);

                while (subExc != null)
                {
                    sbDetails.Append(subExc.Message + "\r\n");
                    subExc = subExc.InnerException;
                }

                excForm.Details = sbDetails.ToString();
                excForm.Msg = "An error has occurred in the application.\r\n\r\n";
                excForm.loadForm(ExceptionsDialogForm.ExceptionType.Error);

                excForm.ShowDialog(this);
            }
            catch
            {
                throw exc;
            }

        }

        /// <summary>
        /// Set default form values
        /// </summary>
        private void loadFormValues()
        {
            try
            {
                if (IdPickerRequest.ReportName.Length > 30)
                {
                    lblReportNameValue.Text = IdPickerRequest.ReportName.Substring(0, 30) + "...";
                }
                else
                {
                    lblReportNameValue.Text = IdPickerRequest.ReportName;
                }

                cboExportType.SelectedIndex = 0; // default to zip-type export
                tbSourceExtensions.Enabled = cbSourceFiles.Checked;
                cboSourceIncludeMode.Enabled = cbSourceFiles.Checked;
                tbSourceExtensions.Text = IDPicker.Properties.Settings.Default.SourceExtensions;
                cboSourceIncludeMode.SelectedIndex = 0; // default to include all matching files

                cbQuantitationMethod.SelectedIndex = 0; // default to no quantitation
            }
            catch (Exception exc)
            {
                throw new Exception("Error loading form values\r\n", exc);
            }
        }

        /// <summary>
        /// Reset group box displayed
        /// </summary>
        private void resetExportOptions()
        {
            try
            {
                gbCSVOptions.Enabled = false;
                gbZipOptions.Enabled = false;
            }
            catch (Exception exc)
            {
                throw new Exception("Error resetting export options\r\n", exc);
            }
        }

        /// <summary>
        /// Display appropriate group box based on selected export type
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cboExportType_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                resetExportOptions();

                if (cboExportType.SelectedItem.Equals("ZIP"))
                {
                    gbZipOptions.Enabled = true;
                    gbZipOptions.Visible = true;
                    gbCSVOptions.Enabled = false;
                    gbCSVOptions.Visible = false;
                }
                else if (cboExportType.SelectedItem.Equals("TSV"))
                {
                    gbZipOptions.Enabled = false;
                    gbZipOptions.Visible = false;
                    gbCSVOptions.Enabled = true;
                    gbCSVOptions.Visible = true;
                }
                else if (cboExportType.SelectedItem.Equals("XML"))
                {
                    gbZipOptions.Enabled = false;
                    gbZipOptions.Visible = false;
                    gbCSVOptions.Enabled = false;
                    gbCSVOptions.Visible = false;
                }

                checkEnableExportButton();
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error selecting export option\r\n", exc));
            }
        }

        /// <summary>
        /// Export button enabled with sufficient options selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cbEntireReport_CheckStateChanged(object sender, EventArgs e)
        {
            try
            {
                checkEnableExportButton();
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error selecting export option\r\n", exc));
            }
        }

        /// <summary>
        /// Enable source file search options if option checked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cbSourceFiles_CheckStateChanged(object sender, EventArgs e)
        {
            try
            {
                checkEnableExportButton();
                tbSourceExtensions.Enabled = cbSourceFiles.Checked;
                cboSourceIncludeMode.Enabled = cbSourceFiles.Checked;
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error selecting export option\r\n", exc));
            }

        }

        /// <summary>
        /// Check to enable export button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cbSearchFiles_CheckStateChanged(object sender, EventArgs e)
        {
            try
            {
                checkEnableExportButton();
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error selecting export option\r\n", exc));
            }

        }

        /// <summary>
        /// See if sufficient user selections to enable export
        /// </summary>
        private void checkEnableExportButton()
        {
            bool allow = false;

            try
            {
                if (cboExportType.SelectedItem.Equals("ZIP"))
                {
                    allow = cbReportFiles.Checked ||
                            cbSearchFiles.Checked ||
                            cbSourceFiles.Checked ||
                            cbIncludeDatabase.Checked ||
                            cbSubsetProteinDatabase.Checked;
                }
                else if (cboExportType.SelectedItem.Equals("TSV"))
                {
                    allow = cbSummaryTable.Checked ||
                            cbSequencesPerProteinTable.Checked ||
                            cbSpectraPerProteinTable.Checked ||
                            cbSpectraPerPeptideTable.Checked;

                }
                else if (cboExportType.SelectedItem.Equals("XML"))
                    allow = true;

                btnExport.Enabled = allow;
            }
            catch (Exception exc)
            {
                throw new Exception("Error selecting export option\r\n", exc);
            }

        }

        /// <summary>
        /// Search for combination of filename with exts given in source paths keeping either
        /// the first valid combination or all valid combinations.
        /// </summary>
        /// <param name="searchFileName"></param>
        /// <param name="sourcePathList"></param>
        /// <param name="targetExts"></param>
        /// <param name="firstFound"></param>
        /// <returns></returns>
        private string[] getValidSourcePathsForFileByExts(string searchFileName, ArrayList sourcePathList, string[] targetExts, bool firstFound)
        {
            List<string> validSourcePaths = new List<string>();

            try
            {
                foreach (string sourcePath in sourcePathList)
                {
                    foreach (string ext in targetExts)
                    {
                        string extWithDot = ext;

                        // might put ext in field with or without .
                        if (!extWithDot.StartsWith("."))
                        {
                            extWithDot = "." + extWithDot;
                        }

                        string testFilePath = sourcePath + "\\" + Path.GetFileNameWithoutExtension(searchFileName) + extWithDot;

                        if (File.Exists(testFilePath))
                        {
                            validSourcePaths.Add(testFilePath);

                            if (firstFound)
                            {
                                break;
                            }
                        }
                    }
                }

                return validSourcePaths.ToArray();

            }
            catch (Exception exc)
            {
                throw new Exception("Problem evaluating database source paths\r\n", exc);
            }

        }

        /// <summary>
        /// Returns first valid path for filename in searchPaths
        /// </summary>
        /// <param name="searchPaths"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        private string getValidSearchPathForFile(ArrayList searchPaths, string filename)
        {
            try
            {
                foreach (string path in searchPaths)
                {
                    string testFilePath = path + "\\" + filename;

                    if (File.Exists(testFilePath))
                    {
                        return testFilePath;
                    }
                }

                return string.Empty;

            }
            catch (Exception exc)
            {
                throw new Exception("Problem evaluating database search paths\r\n", exc);
            }

        }


        /// <summary>
        /// Search files selected for idpicker process (.pepxml, .xml)
        /// </summary>
        /// <returns></returns>
        private string[] getSearchPathsToZip()
        {
            List<string> validFilePaths = new List<string>();

            try
            {
                foreach (InputFileTag tag in IdPickerRequest.SrcPathToTagCollection)
                {
                    if (tag.GroupName != string.Empty)
                    {
                        //origFilePaths.Add(tag.FileInfo.FullName);
                        string filePath = tag.FullPath;

                        // orig path valid
                        if (File.Exists(filePath))
                        {
                            validFilePaths.Add(filePath);
                        }
                        else        // orig path invalid
                        {
                            string correctedFilePath = IDPicker.Util.FindSearchInSearchPath(Path.GetFileName(filePath), IdPickerRequest.SrcFilesDir);

                            // empty string if no valid path found
                            if (!correctedFilePath.Equals(string.Empty))
                            {
                                validFilePaths.Add(correctedFilePath);
                            }

                        }
                    }

                }

                return validFilePaths.ToArray();

                // no current method of reporting missing files

            }
            catch (Exception exc)
            {
                throw new Exception("Problem evaluating source files search paths\r\n", exc);
            }


        }

        /// <summary>
        /// Entry point for export zip option
        /// </summary>
        /// <returns></returns>
        private bool exportZipOption()
        {
            string exportDir = string.Empty;
            string zipFilePath = string.Empty;
            string dbPath = string.Empty;
            bool continueOverwrite = true;

            List<string> filesToZip = new List<string>();

            try
            {
                exportDir = IdPickerRequest.ResultsDir + "\\export";
                zipFilePath = IdPickerRequest.ResultsDir + "\\export\\" + IdPickerRequest.ReportName + ".zip";
                dbPath = IdPickerRequest.DatabasePath;

                if (cbReportFiles.Checked)
                {
                    filesToZip = new List<string>(Directory.GetFiles(IdPickerRequest.ResultsDir));
                }

                if (cbSearchFiles.Checked)
                {
                    filesToZip.AddRange(getSearchPathsToZip());
                }

                if (cbSourceFiles.Checked)
                {
                    ArrayList sourcePathList = new ArrayList(IDPicker.Properties.Settings.Default.SourcePaths);
                    string[] sourceExts = tbSourceExtensions.Text.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    bool firstOnly = false;

                    if (cboSourceIncludeMode.SelectedIndex == 1)
                    {
                        firstOnly = true;
                    }

                    foreach (InputFileTag tag in idPickerRequest.SrcPathToTagCollection)
                    {
                        if (tag.GroupName != string.Empty)
                        {
                            filesToZip.AddRange(getValidSourcePathsForFileByExts(Path.GetFileName(tag.FullPath), sourcePathList, sourceExts, firstOnly));
                        }
                    }
                }

                if (cbIncludeDatabase.Checked)
                {
                    // orig db path invalid, check search paths
                    if (!File.Exists(dbPath))
                    {
                        dbPath = getValidSearchPathForFile(new ArrayList(IDPicker.Properties.Settings.Default.FastaPaths), Path.GetFileName(IdPickerRequest.DatabasePath));
                    }

                    if (!dbPath.Equals(string.Empty))
                    {
                        filesToZip.Add(dbPath);
                    }
                    else
                    {
                        if (DialogResult.Yes == MessageBox.Show("The database " + Path.GetFileName(IdPickerRequest.DatabasePath) + " could not be located. Continue?", "IDPicker", MessageBoxButtons.YesNo, MessageBoxIcon.Information))
                        {
                            continueOverwrite = true;
                        }
                        else
                        {
                            continueOverwrite = false;
                        }

                    }
                }

                if (cbSubsetProteinDatabase.Checked || cbSubsetSpectra.Checked)
                {
                    exportAssembledReportOption();
                    if (File.Exists(IdPickerRequest.ReportName + "-subset.fasta"))
                        filesToZip.Add(IdPickerRequest.ReportName + "-subset.fasta");
                }

                if (filesToZip.Count > 0 && continueOverwrite)
                {
                    if (!Directory.Exists(exportDir))
                    {
                        Directory.CreateDirectory(exportDir);
                    }

                    if (File.Exists(zipFilePath) && continueOverwrite)
                    {
                        if (DialogResult.Yes == MessageBox.Show("Overwrite file " + zipFilePath + "?", "IDPicker", MessageBoxButtons.YesNo, MessageBoxIcon.Information))
                        {
                            continueOverwrite = true;
                        }
                        else
                        {
                            continueOverwrite = false;
                        }
                    }

                    if (continueOverwrite)
                    {
                        ExportReportManager.zipReportFiles(filesToZip.ToArray(), zipFilePath);

                        MessageBox.Show("Files have been written to " + zipFilePath + ".", "IDPicker", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        if (cbOpenExplorer.Checked)
                        {
                            System.Diagnostics.Process.Start("explorer.exe", "/n,/e," + exportDir);
                        }
                    }

                }
                else
                {
                    MessageBox.Show("No files found to export.", "IDPicker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return continueOverwrite;
            }
            catch (Exception exc)
            {

                throw new Exception("Error exporting files\r\n", exc);

            }
        }


        /// <summary>
        /// Entry point for export csv option
        /// </summary>
        /// <returns></returns>
        private bool exportAssembledReportOption()
        {
            try
            {
                //ExportReportManager.createCsvTables();
                Form ghettoProgressForm = new Form();
                ghettoProgressForm.SizeGripStyle = SizeGripStyle.Show;
                ghettoProgressForm.ShowInTaskbar = true;
                ghettoProgressForm.TopLevel = true;
                ghettoProgressForm.TopMost = true;
                ghettoProgressForm.AutoSize = true;
                ghettoProgressForm.AutoSizeMode = AutoSizeMode.GrowOnly;
                ghettoProgressForm.FormBorderStyle = FormBorderStyle.SizableToolWindow;
                ghettoProgressForm.StartPosition = FormStartPosition.CenterScreen;
                ghettoProgressForm.MaximizeBox = false;
                ghettoProgressForm.MinimizeBox = true;
                ghettoProgressForm.Text = "Generating report...";
                ghettoProgressForm.Size = new System.Drawing.Size(450, 50);

                ProgressBar ghettoProgressBar = new ProgressBar();
                ghettoProgressBar.Dock = DockStyle.Fill;
                ghettoProgressBar.Style = ProgressBarStyle.Continuous;
                ghettoProgressBar.Step = 1;
                ghettoProgressBar.Minimum = 0;
                ghettoProgressBar.Maximum = 21 + (IdPickerRequest.MinAdditionalPeptides > 0 ? 1 : 0);
                ghettoProgressForm.Controls.Add(ghettoProgressBar);
                ghettoProgressForm.Show();

                Application.DoEvents();

                Workspace ws = new Workspace();

                StringBuilder indistinctModsOverride = new StringBuilder();
                StringBuilder distinctModsOverride = new StringBuilder();
                foreach (ModOverrideInfo mod in IdPickerRequest.ModOverrides)
                    if (mod.Type.ModTypeValue == 0)
                        distinctModsOverride.Append(" " + mod.Name + " " + mod.Mass);
                    else
                        indistinctModsOverride.Append(" " + mod.Name + " " + mod.Mass);

                ws.distinctPeptideSettings = new Workspace.DistinctPeptideSettings(
                    IdPickerRequest.ModsAreDistinctByDefault, distinctModsOverride.ToString(), indistinctModsOverride.ToString());

                ghettoProgressForm.Text = "Reading files...";
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return false;
                }

                int readFiles = 0;

                foreach (InputFileTag tag in IdPickerRequest.SrcPathToTagCollection)
                {
                    if (tag.GroupName != string.Empty)
                    {
                        string path;
                        if (tag.FileType == InputFileType.IdpXML)
                            path = tag.FullPath;
                        else
                            path = IdPickerRequest.ResultsDir + "\\qonverted\\" + Path.ChangeExtension(tag.FullPath, ".idpXML").Substring(3);

                        string group = tag.GroupName;

                        using (StreamReader sr = new StreamReader(path))
                        {
                            ws.readPeptidesXml(sr, group, IdPickerRequest.MaxFDR, IdPickerRequest.MaxResultRank);
                            ++readFiles;
                            ghettoProgressForm.Text = "Reading files... (" + readFiles + " of " + IdPickerRequest.NumGroupedFiles + ")";
                            Application.DoEvents();
                        }
                    }

                }

                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return false;
                }

                ghettoProgressForm.Text = "Filtering out peptides shorter than " + IdPickerRequest.MinPeptideLength + " residues...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return false;
                }

                ws.filterByMinimumPeptideLength(IdPickerRequest.MinPeptideLength);

                ghettoProgressForm.Text = "Filtering out results with more than " + IdPickerRequest.MaxAmbiguousIds + " ambiguous ids...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return false;
                }

                ws.filterByResultAmbiguity(IdPickerRequest.MaxAmbiguousIds);

                ghettoProgressForm.Text = "Filtering out proteins with less than " + IdPickerRequest.MinDistinctPeptides + " distinct peptides...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return false;
                }

                ws.filterByDistinctPeptides(IdPickerRequest.MinDistinctPeptides);

                ghettoProgressForm.Text = "Filtering out proteins with less than " + IdPickerRequest.MinSpectraPerProetin + " spectra...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return false;
                }

                ws.filterBySpectralCount(IdPickerRequest.MinSpectraPerProetin);

                ghettoProgressForm.Text = "Assembling protein groups...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return false;
                }

                ws.assembleProteinGroups();

                ghettoProgressForm.Text = "Assembling peptide groups...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return false;
                }

                ws.assemblePeptideGroups();

                ghettoProgressForm.Text = "Assembling clusters...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return false;
                }

                ws.assembleClusters();

                int clusterCount = 0;
                foreach (ClusterInfo c in ws.clusters)
                {
                    ++clusterCount;
                    ghettoProgressForm.Text = "Assembling minimum covering set for cluster " + clusterCount + " of " + ws.clusters.Count + "...";
                    Application.DoEvents();
                    if (ghettoProgressForm.IsDisposed)
                    {
                        IdPickerRequest.RunStatus = RunStatus.Cancelled;
                        return false;
                    }

                    ws.assembleMinimumCoveringSet(c);
                }
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return false;
                }

                if (IdPickerRequest.MinAdditionalPeptides > 0)
                {
                    ghettoProgressForm.Text = "Filtering workspace by minimum covering set...";
                    ghettoProgressBar.PerformStep();
                    Application.DoEvents();
                    if (ghettoProgressForm.IsDisposed)
                    {
                        IdPickerRequest.RunStatus = RunStatus.Cancelled;
                        return false;
                    }

                    ws.filterByMinimumCoveringSet(IdPickerRequest.MinAdditionalPeptides);
                }

                ws.assembleSourceGroups();

                ghettoProgressForm.Text = "Verifying integrity of the workspace...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return false;
                }

                try
                {
                    ws.validate(IdPickerRequest.MaxFDR, IdPickerRequest.MinDistinctPeptides, IdPickerRequest.MaxAmbiguousIds);
                }
                catch (Exception)
                {
                    return false;
                }

                ghettoProgressForm.Text = "Writing output files...";
                ghettoProgressBar.PerformStep();
                Application.DoEvents();
                if (ghettoProgressForm.IsDisposed)
                {
                    IdPickerRequest.RunStatus = RunStatus.Cancelled;
                    return false;
                }

                string outputPrefix = IdPickerRequest.ReportName;
                string exportDir = IdPickerRequest.ResultsDir + "\\export";
                if (!Directory.Exists(exportDir))
                    Directory.CreateDirectory(exportDir);
                Directory.SetCurrentDirectory(exportDir);

                StreamWriter outputStream;

                if (cboExportType.SelectedItem.Equals("XML"))
                {
                    string wsAssembledXmlFilename = outputPrefix + "-assembled.idpXML";

                    ghettoProgressForm.Text = "Exporting assembled IDPickerXML file...";
                    ghettoProgressBar.Value = ghettoProgressBar.Maximum;
                    Application.DoEvents();
                    if (ghettoProgressForm.IsDisposed)
                    {
                        IdPickerRequest.RunStatus = RunStatus.Cancelled;
                        return false;
                    }

                    try
                    {
                        outputStream = new StreamWriter(wsAssembledXmlFilename);
                        ws.assemblePeptidesXmlToStream(outputStream);
                    }
                    catch (Exception)
                    {
                        ghettoProgressForm.Close();
                        return false;
                    }
                }
                else if (cboExportType.SelectedItem.Equals("ZIP"))
                {
                    if (cbSubsetProteinDatabase.Checked)
                    {
                        string wsSubsetFilename = outputPrefix + "-subset.fasta";
                        ProteomeDataFile pd = new ProteomeDataFile(idPickerRequest.DatabasePath);
                        ProteomeDataFileSubset.write(pd, wsSubsetFilename, ws.proteins.Keys);
                    }
                }
                else if (cboExportType.SelectedItem.Equals("TSV"))
                {
                    string wsSummaryFilename = outputPrefix + "-summary.tsv";
                    string wsSequencesPerProteinByGroupFilename = outputPrefix + "-sequences-per-protein-by-group.tsv";
                    string wsSpectraPerProteinByGroupFilename = outputPrefix + "-spectra-per-protein-by-group.tsv";
                    string wsSpectraPerPeptideByGroupFilename = outputPrefix + "-spectra-per-peptide-by-group.tsv";
                    string wsSpectraTableFilename = outputPrefix + "-spectrum-table.tsv";
                    string wsProteinGroupToPeptideGroupFilename = outputPrefix + "-protein-to-peptide-table.tsv";
                    string wsSpectraPerPeptideGroupFilename = outputPrefix + "-spectra-per-peptide-group.tsv";

                    QuantitationInfo.Method method = QuantitationInfo.Method.None;
                    if (cbQuantitationMethod.SelectedIndex == 1)
                        method = QuantitationInfo.Method.ITRAQ4Plex;
                    else if (cbQuantitationMethod.SelectedIndex == 2)
                        method = QuantitationInfo.Method.ITRAQ8Plex;

                    // calculate quantitation data
                    if (cbSpectrumTable.Checked || cbSpectraPerPeptideTable.Checked)
                        QuantifyingTransmogrifier.quantify(ws, IdPickerRequest.SrcFilesDir, method);

                    if (cbSummaryTable.Checked)
                    {
                        ghettoProgressForm.Text = "Exporting overall summary...";
                        ghettoProgressBar.PerformStep();
                        Application.DoEvents();
                        if (ghettoProgressForm.IsDisposed)
                        {
                            IdPickerRequest.RunStatus = RunStatus.Cancelled;
                            return false;
                        }

                        try
                        {
                            outputStream = new StreamWriter(wsSummaryFilename);
                            Presentation.exportSummaryTable(ws, outputStream, outputPrefix, '\t');
                            outputStream.Close();
                        }
                        catch (Exception)
                        {
                        }
                    }

                    if (cbSpectrumTable.Checked)
                    {
                        ghettoProgressForm.Text = "Exporting spectrum table...";
                        ghettoProgressBar.PerformStep();
                        Application.DoEvents();
                        if (ghettoProgressForm.IsDisposed)
                        {
                            IdPickerRequest.RunStatus = RunStatus.Cancelled;
                            return false;
                        }

                        try
                        {
                            outputStream = new StreamWriter(wsSpectraTableFilename);
                            Presentation.exportSpectraTable(ws, outputStream, outputPrefix, method, '\t');
                            outputStream.Close();
                        }
                        catch (Exception)
                        {
                        }
                    }

                    if (cbSequencesPerProteinTable.Checked)
                    {
                        ghettoProgressForm.Text = "Exporting sequences per protein by group table...";
                        ghettoProgressBar.PerformStep();
                        Application.DoEvents();
                        if (ghettoProgressForm.IsDisposed)
                        {
                            IdPickerRequest.RunStatus = RunStatus.Cancelled;
                            return false;
                        }

                        try
                        {
                            outputStream = new StreamWriter(wsSequencesPerProteinByGroupFilename);
                            Presentation.exportProteinSequencesTable(ws, outputStream, outputPrefix, '\t');
                            outputStream.Close();
                        }
                        catch (Exception)
                        {
                        }
                    }

                    if (cbSpectraPerProteinTable.Checked)
                    {
                        ghettoProgressForm.Text = "Exporting spectra per protein by group table...";
                        ghettoProgressBar.PerformStep();
                        Application.DoEvents();
                        if (ghettoProgressForm.IsDisposed)
                        {
                            IdPickerRequest.RunStatus = RunStatus.Cancelled;
                            return false;
                        }

                        try
                        {
                            outputStream = new StreamWriter(wsSpectraPerProteinByGroupFilename);
                            Presentation.exportProteinSpectraTable(ws, outputStream, outputPrefix, '\t');
                            outputStream.Close();
                        }
                        catch (Exception)
                        {
                        }
                    }

                    if (cbSpectraPerPeptideTable.Checked)
                    {
                        ghettoProgressForm.Text = "Exporting spectra per peptide by group...";
                        ghettoProgressBar.PerformStep();
                        Application.DoEvents();
                        if (ghettoProgressForm.IsDisposed)
                        {
                            IdPickerRequest.RunStatus = RunStatus.Cancelled;
                            return false;
                        }

                        try
                        {
                            outputStream = new StreamWriter(wsSpectraPerPeptideByGroupFilename);
                            Presentation.exportPeptideSpectraTable(ws, outputStream, outputPrefix, method, '\t');
                            outputStream.Close();
                        }
                        catch (Exception)
                        {
                        }
                    }

                    if (cbPeptideProteinGroupAssociationTableCheckBox.Checked)
                    {
                        ghettoProgressForm.Text = "Exporting protein group to peptide group association table...";
                        ghettoProgressBar.PerformStep();
                        Application.DoEvents();
                        if (ghettoProgressForm.IsDisposed)
                        {
                            IdPickerRequest.RunStatus = RunStatus.Cancelled;
                            return false;
                        }

                        try
                        {
                            outputStream = new StreamWriter(wsProteinGroupToPeptideGroupFilename);
                            Presentation.exportProteinGroupToPeptideGroupTable(ws, outputStream, '\t');
                            outputStream.Close();
                        }
                        catch (Exception)
                        {
                        }
                    }

                    if (cbSpectraPerPeptideGroupTable.Checked)
                    {
                        ghettoProgressForm.Text = "Exporting spectra per peptide group by source group...";
                        ghettoProgressBar.PerformStep();
                        Application.DoEvents();
                        if (ghettoProgressForm.IsDisposed)
                        {
                            IdPickerRequest.RunStatus = RunStatus.Cancelled;
                            return false;
                        }

                        try
                        {
                            outputStream = new StreamWriter(wsSpectraPerPeptideGroupFilename);
                            Presentation.exportPeptideGroupSpectraTable(ws, outputStream, outputPrefix, method, '\t');
                            outputStream.Close();
                        }
                        catch (Exception)
                        {
                        }
                    }
                }

                ghettoProgressBar.PerformStep();
                Application.DoEvents();

                ghettoProgressForm.Close();
                Application.DoEvents();

                if (cbOpenExplorer.Checked)
                {
                    System.Diagnostics.Process.Start("explorer.exe", "/n,/e," + exportDir);
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error occurred during export TSV option\r\n", exc);
            }

            return true;
        }

        /// <summary>
        /// Handler for export button calls export entry point
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnExport_Click(object sender, EventArgs e)
        {
            bool exportOk = false;


            try
            {
                if (cboExportType.SelectedItem.Equals("ZIP"))
                {
                    exportOk = exportZipOption();
                }
                else if (cboExportType.SelectedItem.Equals("TSV"))
                {
                    exportOk = exportAssembledReportOption();
                }
                else if( cboExportType.SelectedItem.Equals( "XML" ) )
                {
                    exportOk = exportAssembledReportOption();
                }

                if (exportOk)
                {
                    Close();
                }


            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error occurred during export\r\n", exc));
            }


        }

        /// <summary>
        /// Check sufficient user selections to enable export
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkStateChanged(object sender, EventArgs e)
        {
            try
            {
                checkEnableExportButton();

            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error occurred during export\r\n", exc));
            }
        }
    }
}