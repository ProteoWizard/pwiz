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
// The Initial Developer of the DirecTag peptide sequence tagger is Matt Chambers.
// Contributor(s): Surendra Dasaris
//
// The Initial Developer of the ScanRanker GUI is Zeqiang Ma.
// Contributor(s): 
//
// Copyright 2009 Vanderbilt University
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Threading;

using System.Collections;
using System.Xml;

namespace ScanRanker
{

    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        # region functions to enable or disable controls
        /// <summary>
        /// functions to enable or disable controls
        /// </summary>
        private void enableAssessmentControls()
        {
            foreach (Control c in gbAssessment.Controls)
            {
                c.Enabled = true;
            }
            tbMetricsFileSuffixForRemoval.Enabled = false;
            //tbMetricsFileSuffixForRemoval.Text = string.Empty;
            tbMetricsFileSuffixForRecovery.Enabled = false;
        }
        private void enableRemovalControls()
        {
            foreach (Control c in gbRemoval.Controls)
            {
                c.Enabled = true;
            }
            if (cbAssessement.Checked)
            {
                tbMetricsFileSuffixForRemoval.Enabled = false;
                //tbMetricsFileSuffixForRemoval.Text = string.Empty;
            }
        }
        private void enableRecoveryControls()
        {
            foreach (Control c in gbRecovery.Controls)
            {
                c.Enabled = true;
            }
            if (cbAssessement.Checked)
            {
                tbMetricsFileSuffixForRecovery.Enabled = false;                               
            }
        }
        private void disableAssessmentControls()
        {
            foreach (Control c in gbAssessment.Controls)
            {
                c.Enabled = false;
            }
            cbAssessement.Enabled = true;
            tbMetricsFileSuffixForRemoval.Enabled = (cbRemoval.Checked) ? true : false;
            tbMetricsFileSuffixForRecovery.Enabled = (cbRecovery.Checked) ? true : false;            
           // tbMetricsFileSuffixForRemoval.Text = string.Empty;
        }
        private void disableRemovalControls()
        {
            foreach (Control c in gbRemoval.Controls)
            {
                c.Enabled = false;
            }
            cbRemoval.Enabled = true;

        }
        private void disableRecoveryControls()
        {
            foreach (Control c in gbRecovery.Controls)
            {
                c.Enabled = false;
            }
            cbRecovery.Enabled = true;
        }
        private void cbAssessement_CheckedChanged(object sender, EventArgs e)
        {
            if (cbAssessement.Checked)
            {
                enableAssessmentControls();
            }
            else
            {
                disableAssessmentControls();
            }

        }
        private void cbRemoval_CheckedChanged(object sender, EventArgs e)
        {
            if (cbRemoval.Checked)
            {
                enableRemovalControls();
            }
            else
            {
                disableRemovalControls();
            }
        }
        private void cbRecovery_CheckedChanged(object sender, EventArgs e)
        {
            if (cbRecovery.Checked)
            {
                enableRecoveryControls();
                //cbAssessement.Checked = Enabled;
            }
            else
            {
                disableRecoveryControls();
            }
        }

        #endregion

        private void MainForm_Load(object sender, EventArgs e)
        {
            /// <summary>
            /// default for running quality assessment and removal
            /// </summary>
            cbAssessement.Checked = true;
            cbRemoval.Checked = true;
            cbRecovery.Checked = false;
            tbMetricsFileSuffixForRemoval.Enabled = false;
            disableRecoveryControls();
            tbInputFileFilters.Text = string.Empty;

            tbOutputMetricsSuffix.Text = "-ScanRankerMetrics";
            tbMetricsFileSuffixForRemoval.Text = "-ScanRankerMetrics";
            tbMetricsFileSuffixForRecovery.Text = "-ScanRankerMetrics";
            tbOutFileNameSuffixForRemoval.Text = "-Top"+ tbRemovalCutoff.Text +"PercHighQualSpec";
            tbOutFileNameSuffixForRecovery.Text = "-Labeled";
            cbWriteOutUnidentifiedSpectra.Checked = true;
        }

        private void btnSrcFileBrowse_Click(object sender, EventArgs e)
        {
            try
            {
                string selFile = Workspace.OpenDirBrowseDialog(tbSrcDir.Text,false);
                if (!selFile.Equals(string.Empty))
                {
                    tbSrcDir.Text = selFile;
                    tbOutputDir.Text = selFile;
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error opening directory dialog\r\n", exc);
                //HandleExceptions(exc);
            }

            if (!tbSrcDir.Text.Equals(string.Empty))
            {
                updateFileTreeView();
            }
        }
        
        private void btnListFiles_Click(object sender, EventArgs e)
        {
            if (!tbSrcDir.Text.Equals(string.Empty))
            {
                updateFileTreeView();
            }
        }

        private void tbInputFileFilters_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)  // enter key pressed
            {
                if (!tbSrcDir.Text.Equals(string.Empty))
                {
                    updateFileTreeView();
                }
            }
        }

        private void btnOutputDirBrowse_Click(object sender, EventArgs e)
        {
            try
            {
                string selFile = Workspace.OpenDirBrowseDialog(tbOutputDir.Text, true);
                if (!selFile.Equals(string.Empty))
                {
                    tbOutputDir.Text = selFile;
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error opening direcoty dialog\r\n", exc);
                //HandleExceptions(exc);
            }
        }

        private void tbRemovalCutoff_TextChanged(object sender, EventArgs e)
        {
            string outputRemovalFileName = "-HighQualSpec" + tbRemovalCutoff.Text + "Perc";
            tbOutFileNameSuffixForRemoval.Text = outputRemovalFileName;
        }
        
        #region code to populate files in tree view, modified from IDPicker code
        ///// <summary>
        ///// Instead of using GetFiles with filter in our recursive building
        ///// of our file sel treeview. We get all files in each sub dir
        ///// and filter them using the array of target exts.
        ///// </summary>
        ///// <param name="fileInfos"></param>
        ///// <returns></returns>
        //private FileInfo[] filterFileInfoListByExt(FileInfo[] fileInfos)
        //{
        //    ArrayList fileInfosToKeep = new ArrayList();

        //    try
        //    {
        //        foreach (FileInfo file in fileInfos)
        //        {
        //            foreach (string ext in inputFileExtensions)
        //            {
        //                if (file.Name.EndsWith(ext))
        //                {
        //                    fileInfosToKeep.Add(file);

        //                    break;
        //                }
        //            }
        //        }

        //        return (FileInfo[])fileInfosToKeep.ToArray(typeof(FileInfo));

        //    }
        //    catch (Exception exc)
        //    {
        //        throw new Exception("Error filtering fileinfo list\r\n", exc);
        //    }

        //}
        /// <summary>
        /// filter file list by "filters" parameter, if multiple filters, all need to be matched
        /// </summary>
        /// <param name="fileInfos"></param>
        /// <param name="filters"></param>
        /// <returns></returns>
        private FileInfo[] filterFileInfoList(FileInfo[] fileInfos, string[] filters)
        {
            ArrayList fileInfosToKeep = new ArrayList();

            try
            {
                foreach (FileInfo file in fileInfos)
                {
                    int matchCnt = 0; 
                    foreach (string filter in filters)
                    {
                        if (file.Name.Contains(filter))
                        {
                            matchCnt++;
                        }
                    }
                    if (matchCnt == filters.Length)
                    {
                        fileInfosToKeep.Add(file);
                    }
                }

                return (FileInfo[])fileInfosToKeep.ToArray(typeof(FileInfo));

            }
            catch (Exception exc)
            {
                throw new Exception("Error filtering fileinfo list\r\n", exc);
            }
        }

        /// <summary>
        /// Returns last level of filename in path heirarchy. Used to give
        /// file sel tree nodes a dir name or file name only and preserve
        /// the explorer like look of it
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string returnLastPathLevel(string path)
        {
            try
            {
                return path.Substring(path.LastIndexOf(@"\") + 1).Replace("\\", "/");
                //return path.Substring(path.LastIndexOf(@"\") + 1);
            }
            catch (Exception exc)
            {
                throw new Exception("Error evaluating path\r\n", exc);
            }
        }

        /// <summary>
        /// Populate a tree view with a directory structure
        /// </summary>
        /// <param name="sPath"></param>
        /// <param name="bInclSubFolders"></param>
        private void fillFileTreeViewRecursively(string currPath, TreeNode nodeParent, bool bInclSubDirs, string filterText, string basePath)
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(currPath);

                DirectoryInfo[] dis = di.GetDirectories();

                //FileInfo[] fis = di.GetFiles("*" + filterText + "*" + ".pepxml", SearchOption.TopDirectoryOnly);

                FileInfo[] fis = di.GetFiles("*" + filterText + "*", SearchOption.TopDirectoryOnly);
                //string[] filters = filterText.Split(new Char[] { ';', ' ', '\t', ',' }); // filter separated by ";",",", ignore " " and "\t"
                //FileInfo[] fis = di.GetFiles();
                //fis = filterFileInfoList(fis, filters);

                //fis = filterFileInfoListByExt(fis);

                foreach (FileInfo fi in fis)
                {
                    TreeNode newFileNode;
                    //  string dbInFile = string.Empty;

                    newFileNode = new TreeNode(returnLastPathLevel(fi.FullName));

                    InputFileTag newFileTag = new InputFileTag("file", fi.FullName, true);

                    newFileNode.Tag = newFileTag;

                    //checkAndSetFilteredFileNode(newFileNode, basePath);

                    nodeParent.Nodes.Add(newFileNode);
                    newFileNode.Name = fi.FullName;

                }

                if (bInclSubDirs)
                {
                    foreach (DirectoryInfo diSub in dis)
                    {
                        TreeNode newDirNode;
                        string parentPath = string.Empty;

                        newDirNode = new TreeNode(returnLastPathLevel(diSub.FullName));
                        newDirNode.Tag = new InputFileTag("dir", diSub.FullName, true);

                        fillFileTreeViewRecursively(diSub.FullName, newDirNode, bInclSubDirs, filterText, basePath);

                        if (newDirNode.Nodes.Count > 0)
                        {
                            nodeParent.Nodes.Add(newDirNode);
                            newDirNode.Name = diSub.FullName;
                        }

                    }
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error loading file list\r\n", exc);
            }
        }

        /// <summary>
        /// Call method to act on tree node recursively.
        /// </summary>
        /// <param name="treeView"></param>
        private void checkAllNodesInTreeView(TreeNodeCollection tnc, bool bIsChecked)
        {
            try
            {
                foreach (TreeNode node in tnc)
                {
                    node.Checked = bIsChecked;
                }
            }
            catch (Exception exc)
            {
                throw new Exception("File selection error\r\n", exc);

            }
        }

        private void tvSelDirs_AfterCheck(object sender, TreeViewEventArgs e)
        {
            try
            {
                if (e.Node.Nodes.Count > 0 && e.Node.Checked)
                {
                    checkAllNodesInTreeView(e.Node.Nodes, true);
                }
                else if (e.Node.Nodes.Count > 0 && !e.Node.Checked)
                {
                    checkAllNodesInTreeView(e.Node.Nodes, false);
                }

            }
            catch (Exception exc)
            {
                throw new Exception(exc.Message);
            }
        }

        private void updateFileTreeView()
        {
            try
            {
                tvSelDirs.Nodes.Clear();
                if (Directory.Exists(tbSrcDir.Text))
                {
                    tvSelDirs.BeginUpdate();
                    Cursor = Cursors.WaitCursor;
                    string basePath = tbSrcDir.Text.Replace("\\", "/");
                    string selPath = basePath;

                    TreeNode rootNode = tvSelDirs.Nodes.Add("/");

                    rootNode.Tag = new InputFileTag("root", "/", true);

                    //fillFileTreeViewRecursively(selPath, rootNode, cbInclSubDirs.Checked, tbFilter.Text, basePath);
                    fillFileTreeViewRecursively(selPath, rootNode, true, tbInputFileFilters.Text, basePath);

                    // no target files found in sel dir
                    if (tvSelDirs.Nodes[0].Nodes.Count == 0)
                    {
                        tvSelDirs.Nodes.Clear();
                    }
                    else
                    {
                        tvSelDirs.ExpandAll();
                    }
                    Cursor = Cursors.Arrow;
                    tvSelDirs.EndUpdate();
                }
                else
                {
                    throw new Exception("Source directory not found.");
                }
            }
            catch (Exception exc)
            {
                throw new Exception(exc.Message);
            }

        }
        #endregion

        #region code to get a list of selected file from tree view
        /// <summary>
        /// Given a node and an array list, fill the list with the names of all the checked files
        /// </summary>
        private void GetCheckedFiles(TreeNode node, ArrayList fileNames, string srcDir)
        {
            // if this is a leaf…
            if (node.Nodes.Count == 0)
            {
                // if the node was checked…
                if (node.Checked)
                {
                    // get the full path and add it to the arrayList
                    string fullPath = srcDir + GetParentString(node);
                    fileNames.Add(fullPath);
                }
            }
            else // if this node is not a leaf
            {
                // call this for all the subnodes
                foreach (TreeNode n in node.Nodes)
                {
                    GetCheckedFiles(n, fileNames, srcDir);
                }
            }
        }

        /// <summary>
        /// Given a node, return the full pathname
        /// </summary>
        private string GetParentString(TreeNode node)
        {
            // if this is the root node (c:\) return the text
            if (node.Parent == null)
            {
                return node.Text;
            }
            else
            {
                // recurse up and get the path then
                // add this node and a slash
                // if this node is the leaf, don't add the slash
                return GetParentString(node.Parent) + node.Text + (node.Nodes.Count == 0 ? "" : "/");
            }
        }

        private ArrayList GetFileList(TreeView tv, string srcDir)
        {
            ArrayList fileNames = new ArrayList();
            //fill array with the full path of each selected file
            foreach (TreeNode n in tv.Nodes)
            {
                GetCheckedFiles(n, fileNames, srcDir);
            }
            // Create a list to hold the fileInfo objects
            ArrayList fileList = new ArrayList();
            // for each of the filenames we have in our unsorted list
            // if the name corresponds to a file (and not a directory)
            // add it to the file list
            foreach (string fileName in fileNames)
            {
                // create a file with the name
                FileInfo file = new FileInfo(fileName);
                // see if it exists on the disk
                // this fails if it was a directory
                if (file.Exists)
                {
                    // both the key and the value are the file
                    // would it be easier to have an empty value?
                    fileList.Add(file);
                }
            }
            return fileList;
        }
        #endregion

        private void btnPepXMLBrowse_Click(object sender, EventArgs e)
        {
            try
            {
                string selFile = Workspace.OpenDirBrowseDialog(tbPepXMLDir.Text, false);
                if (!selFile.Equals(string.Empty))
                {
                    tbPepXMLDir.Text = selFile;
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error opening directory dialog\r\n", exc);
                //HandleExceptions(exc);
            }
        }

        private void btnDBBrowse_Click(object sender, EventArgs e)
        {
            try
            {
                string selFile = Workspace.OpenFileBrowseDialog(tbDBFile.Text);
                if (!selFile.Equals(string.Empty))
                {
                    tbDBFile.Text = selFile;
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error opening file dialog\r\n", exc);
            }
            //if (tbDBFile.Text.Equals(string.Empty))
            //{
            //    MessageBox.Show("Please select a database file!");
            //    return;
            //}
        }
        
        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
        
        /// <summary>
        ///  write cfg file for DirecTag
        /// </summary>
        private void bulidAndWriteDirectagInfo(DirecTagInfo directagInfo)
        {
            directagInfo.NumChargeStates = Convert.ToInt32(tbNumChargeStates.Text);
            directagInfo.FragmentMzTolerance = Convert.ToSingle(tbFragmentTolerance.Text);
            directagInfo.IsotopeMzTolerance = Convert.ToSingle(tbFragmentTolerance.Text) / 2;
            directagInfo.TagLength = Convert.ToInt32(tbTagLength.Text);
            directagInfo.PrecursorMzTolerance = Convert.ToSingle(tbPrecursorTolerance.Text);
            directagInfo.UseAvgMassOfSequences = (rbAverage.Checked) ? 1 : 0;
            directagInfo.UseChargeStateFromMS = (cbUseChargeStateFromMS.Checked) ? 1 : 0;
            directagInfo.UseMultipleProcessors = (cbUseMultipleProcessors.Checked) ? 1 : 0;
            directagInfo.StaticMods = tbStaticMods.Text;
             
            directagInfo.WriteOutTags = (cbWriteOutTags.Checked) ? 1 : 0;
            directagInfo.WriteScanRankerMetrics = (cbAssessement.Checked) ? 1 : 0;
            //directagInfo.ScanRankerMetricsFileName; // given in cmd line
            directagInfo.WriteHighQualSpectra = (cbRemoval.Checked) ? 1 : 0;
            //directagInfo.HighQualSpecFileName = fileBaseName + tbOutFileNameSuffixForRemoval.Text + ".txt"; //given in cmd line
            directagInfo.OutputFormat = cmbOutputFileFormat.Text;
            directagInfo.HighQualSpecCutoff = Convert.ToSingle(tbRemovalCutoff.Text) / 100.0f;
            
            directagInfo.WriteDirectagCfg();
        }
        
        /// <summary>
        /// get IDPicker cfg from MainForm
        /// </summary>
        /// <returns></returns>
        private IDPickerInfo getIdpickerCfg()
        {
            IDPickerInfo idpickerCfg = new IDPickerInfo();
            idpickerCfg.PepXMLFileDir = tbPepXMLDir.Text;
            idpickerCfg.DBFile = tbDBFile.Text;
            idpickerCfg.MaxFDR = Convert.ToDouble(tbMaxFDR.Text) / 100;
            idpickerCfg.DecoyPrefix = tbDecoyPrefix.Text;
            idpickerCfg.ScoreWeights = cmbScoreWeights.Text;
            idpickerCfg.NormalizeSearchScores = (cbNormSearchScores.Checked) ? 1 : 0;
            idpickerCfg.OptimizeScoreWeights = (cbOptimizeScoreWeights.Checked) ? 1 : 0;
            //idpickerCfg.PepXMLFile given when running idpqonvert
            return idpickerCfg;
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            ArrayList fileList = new ArrayList();
            fileList = GetFileList(tvSelDirs, tbSrcDir.Text.Replace("\\", "/"));
            if (fileList.Count == 0)
            {
                MessageBox.Show("please select input files");
                return;
            }
            List<string> allowedFormat = new List<string>(new string[] { "mzXML", "mzxml", "mzML", "mzml", "mgf", "MGF", "MS2", "ms2" });

            // run directag, allow write high quality spectra by directag, allow add identification label and write unidentified spectra
            if (cbAssessement.Checked)  
            {
                # region  Error checking
                if (tbSrcDir.Text.Equals(string.Empty))
                {
                    MessageBox.Show("Error: Please select input file directory!");
                    return;
                }
                if (tbOutputDir.Text.Equals(string.Empty) || !Directory.Exists(tbOutputDir.Text))
                {
                    MessageBox.Show("Error: Please select correct output directory!");
                    return;
                }
                //List<string> allowedFormat = new List<string>(new string[] { "mzXML", "mzxml", "mzML", "mzml", "mgf", "MGF", "MS2", "ms2" });
                if (cbRemoval.Checked)
                {                    
                    if (!allowedFormat.Exists(element => element.Equals(cmbOutputFileFormat.Text)))
                    {
                        MessageBox.Show("Please select proper output format");
                        return;
                    }
                    if (Convert.ToInt32(tbRemovalCutoff.Text) <= 0 || Convert.ToInt32(tbRemovalCutoff.Text) > 100)
                    {
                        MessageBox.Show("Please specify proper cutoff between 0 and 100!");
                        return;
                    }
                    if (tbOutFileNameSuffixForRemoval.Text.Equals(string.Empty))
                    {
                        MessageBox.Show("Please specify output file suffix!");
                        return;
                    }
                }
                if (cbRecovery.Checked)
                {
                    if (tbPepXMLDir.Text.Equals(string.Empty))
                    {
                        MessageBox.Show("Error: Please specify pepXML file directory!");
                        return;
                    }
                    if (tbDBFile.Text.Equals(string.Empty))
                    {
                        MessageBox.Show("Error: Please specify database file!");
                        return;
                    }
                    if (tbMaxFDR.Text.Equals(string.Empty))
                    {
                        MessageBox.Show("Error: Please specify FDR!");
                        return;
                    }
                    double fdr = Convert.ToDouble(tbMaxFDR.Text);
                    if (fdr <= 0 || fdr >= 100)
                    {
                        MessageBox.Show("Please input proper FDR between 0 and 100");
                        return;
                    }
                    if (cmbScoreWeights.Text.Equals(string.Empty))
                    {
                        MessageBox.Show("Error: Please specify database search score weights!");
                        return;
                    }
                    if (tbOutFileNameSuffixForRecovery.Text.Equals(string.Empty))
                    {
                        MessageBox.Show("Error: Please specify output file suffix!");
                        return;
                    }
                    if (cbWriteOutUnidentifiedSpectra.Checked)
                    {
                        if (Convert.ToInt32(tbRecoveryCutoff.Text) <= 0 || Convert.ToInt32(tbRecoveryCutoff.Text) > 100)
                        {
                            MessageBox.Show("Please specify proper recovery cutoff between 0 and 100!");
                            return;
                        }                        
                        if (!allowedFormat.Exists(element => element.Equals(cmbRecoveryOutFormat.Text)))
                        {
                            MessageBox.Show("Please select proper output format");
                            return;
                        }
                    }
                }

                # endregion

                RunDirecTagAction directagAction = new RunDirecTagAction();
                directagAction.InFileList = fileList;
                directagAction.OutMetricsSuffix = tbOutputMetricsSuffix.Text;
                directagAction.OutputDir = tbOutputDir.Text;
                if (cbRemoval.Checked)
                {
                    directagAction.OutputFilenameSuffixForRemoval = tbOutFileNameSuffixForRemoval.Text;
                    directagAction.OutputFormat = cmbOutputFileFormat.Text;
                }
                if (cbRecovery.Checked)
                {
                    directagAction.AddLabel = true;
                    //directagAction.IdpickerCfg = idpickerCfg;
                    directagAction.IdpickerCfg = getIdpickerCfg();
                    directagAction.OutputFilenameSuffixForRecovery = tbOutFileNameSuffixForRecovery.Text;
                    if (cbWriteOutUnidentifiedSpectra.Checked)
                    {
                        directagAction.WriteOutUnidentifedSpectra = true;
                        directagAction.RecoveryCutoff = Convert.ToSingle(tbRecoveryCutoff.Text) / 100.0f;
                        directagAction.RecoveryOutFormat = cmbRecoveryOutFormat.Text;
                    }
                }

                if ( cbAdjustScoreByGroup.Checked)
                {
                    directagAction.AdjustScanRankerScoreByGroup = true;
                }

                string outputDir = tbOutputDir.Text;
                Directory.SetCurrentDirectory(outputDir);

                if (Workspace.statusForm == null || Workspace.statusForm.IsDisposed)
                {
                    Workspace.statusForm = new TextBoxForm(this);
                    Workspace.statusForm.Show();
                    Application.DoEvents();
                }

                // run directag 
                bgDirectagRun.WorkerSupportsCancellation = true;
                bgDirectagRun.RunWorkerCompleted += bgDirectagRun_RunWorkerCompleted;
                DirecTagInfo directagInfo = new DirecTagInfo();
                bulidAndWriteDirectagInfo(directagInfo);
                //Thread t = new Thread(delegate() { directagAction.RunDirectag(); });
                //t.Start();
                //t.Join();
                //bgDirectagRun.ProgressChanged += bgDirectagRun_ProgressChanged;
                bgDirectagRun.RunWorkerAsync(directagAction);

            }
            else  
            {
                #region error checking
                
                if (cbRemoval.Checked)
                {
                    if (tbOutputDir.Text.Equals(string.Empty) || !Directory.Exists(tbOutputDir.Text))
                    {
                        MessageBox.Show("Error: Please set up the correct output directory!");
                        return;
                    }
                    
                    if (!allowedFormat.Exists(element => element.Equals(cmbOutputFileFormat.Text)))
                    {
                        MessageBox.Show("Please select proper output format");
                        return;
                    }
                    if (Convert.ToInt32(tbRemovalCutoff.Text) <= 0 || Convert.ToInt32(tbRemovalCutoff.Text) > 100)
                    {
                        MessageBox.Show("Please specify proper cutoff between 0 and 100!");
                        return;
                    }
                    if (tbMetricsFileSuffixForRemoval.Text.Equals(string.Empty) || tbOutFileNameSuffixForRemoval.Text.Equals(string.Empty))
                    {
                        MessageBox.Show("Please specify metrics and output file suffix!");
                        return;
                    }
                }

                if (cbRecovery.Checked)
                {
                    //if (!cbAssessement.Checked)
                    //{
                    //    MessageBox.Show("Please run quality assessment with Spectra Recovery");
                    //    cbAssessement.Checked = Enabled;
                    //    return;
                    //}
                    //if (tbOutputDir.Text.Equals(string.Empty) || !Directory.Exists(tbOutputDir.Text))
                    //{
                    //    MessageBox.Show("Error: Please set up correct output directory!");
                    //    return;
                    //}
                    //if (tbOutFileNameSuffixForRecovery.Text.Equals(string.Empty))
                    //{
                    //    MessageBox.Show("Error: Please specify output file suffix!");
                    //    return;
                    //}
                    if (tbMetricsFileSuffixForRecovery.Text.Equals(string.Empty))
                    {
                        MessageBox.Show("Error: Please specify quality metrics file suffix!");
                        return;
                    }
                    if (tbPepXMLDir.Text.Equals(string.Empty))
                    {
                        MessageBox.Show("Error: Please specify pepXML file directory!");
                        return;
                    }
                    if (tbDBFile.Text.Equals(string.Empty))
                    {
                        MessageBox.Show("Error: Please specify database file!");
                        return;
                    }
                    if (tbMaxFDR.Text.Equals(string.Empty))
                    {
                        MessageBox.Show("Error: Please specify FDR!");
                        return;
                    }
                    double fdr = Convert.ToDouble(tbMaxFDR.Text);
                    if (fdr <= 0 || fdr >= 100)
                    {
                        MessageBox.Show("Please input proper FDR between 0 and 100");
                        return;
                    }
                    if (cmbScoreWeights.Text.Equals(string.Empty))
                    {
                        MessageBox.Show("Error: Please specify database search score weights!");
                        return;
                    }
                    if (tbOutFileNameSuffixForRecovery.Text.Equals(string.Empty))
                    {
                        MessageBox.Show("Error: Please specify output file suffix!");
                        return;
                    }
                    if (cbWriteOutUnidentifiedSpectra.Checked)
                    {
                        if (Convert.ToInt32(tbRecoveryCutoff.Text) <= 0 || Convert.ToInt32(tbRecoveryCutoff.Text) > 100)
                        {
                            MessageBox.Show("Please specify proper recovery cutoff between 0 and 100!");
                            return;
                        }
                        if (!allowedFormat.Exists(element => element.Equals(cmbRecoveryOutFormat.Text)))
                        {
                            MessageBox.Show("Please select proper output format");
                            return;
                        }
                    }

                }
                #endregion

                // spectra removal based on metrics file, without running directag
                if (cbRemoval.Checked)  
                {
                    WriteSpectraAction writeHighQualSpectra = new WriteSpectraAction();
                    writeHighQualSpectra.InFileList = fileList;
                    writeHighQualSpectra.MetricsFileSuffix = tbMetricsFileSuffixForRemoval.Text;
                    writeHighQualSpectra.Cutoff = Convert.ToSingle(tbRemovalCutoff.Text) / 100.0f;
                    writeHighQualSpectra.OutFormat = cmbOutputFileFormat.Text;
                    writeHighQualSpectra.OutFileSuffix = tbOutFileNameSuffixForRemoval.Text;
                    string outputDir = tbOutputDir.Text;
                    Directory.SetCurrentDirectory(outputDir);

                    if (Workspace.statusForm == null || Workspace.statusForm.IsDisposed)
                    {
                        Workspace.statusForm = new TextBoxForm(this);
                        Workspace.statusForm.Show();
                        Application.DoEvents();
                    }

                    bgWriteSpectra.WorkerSupportsCancellation = true;
                    bgWriteSpectra.RunWorkerCompleted += bgWriteSpectra_RunWorkerCompleted;
                    bgWriteSpectra.RunWorkerAsync(writeHighQualSpectra);

                }

                // add spectra label based on metrics file, without running directag
                if (cbRecovery.Checked)
                {
                    //AddSpectraLabelAction addSpectraLabelAction = new AddSpectraLabelAction();
                    //addSpectraLabelAction.InFileList = fileList;
                    ////addSpectraLabelAction.MetricsFile = fileBaseName + tbMetricsFileSuffixForRecovery.Text + ".txt";
                    //addSpectraLabelAction.IdpCfg = getIdpickerCfg();
                    //string outputDir = tbOutputDir.Text; 
                    //addSpectraLabelAction.OutDir = outputDir;
                    ////addSpectraLabelAction.OutFilename = fileBaseName + tbOutFileNameSuffixForRecovery.Text + ".txt";
                    //Directory.SetCurrentDirectory(outputDir);

                    //if (Workspace.statusForm == null || Workspace.statusForm.IsDisposed)
                    //{
                    //    Workspace.statusForm = new TextBoxForm(this);
                    //    Workspace.statusForm.Show();
                    //    Application.DoEvents();
                    //}

                    //bgAddLabels.WorkerSupportsCancellation = true;
                    //bgAddLabels.RunWorkerCompleted += bgAddLabels_RunWorkerCompleted;
                    //bgAddLabels.RunWorkerAsync(addSpectraLabelAction);


                    AddSpectraLabelAction addSpectraLabelAction = new AddSpectraLabelAction();
                    addSpectraLabelAction.InFileList = fileList;
                    addSpectraLabelAction.IdpCfg = getIdpickerCfg();
                    addSpectraLabelAction.OutFileSuffix = tbOutFileNameSuffixForRecovery.Text;
                    string outputDir = tbOutputDir.Text;
                    addSpectraLabelAction.OutDir = outputDir;
                    addSpectraLabelAction.MetricsFileSuffix = tbMetricsFileSuffixForRecovery.Text;
                    //if (cbAdjustScoreByGroup.Checked)
                    //{
                    //    addSpectraLabelAction.MetricsFileSuffix = tbOutputMetricsSuffix.Text + "-adjusted"; //name hard coded in directag
                    //}
                    if (cbWriteOutUnidentifiedSpectra.Checked)
                    {
                        addSpectraLabelAction.WriteOutUnidentifiedSpectra = true;
                        addSpectraLabelAction.RecoveryCutoff = Convert.ToSingle(tbRecoveryCutoff.Text) / 100.0f;
                        addSpectraLabelAction.RecoveryOutFormat = cmbRecoveryOutFormat.Text;
                    }
                    //addSpectraLabelAction.AddSpectraLabel();
                    Directory.SetCurrentDirectory(outputDir);

                    if (Workspace.statusForm == null || Workspace.statusForm.IsDisposed)
                    {
                        Workspace.statusForm = new TextBoxForm(this);
                        Workspace.statusForm.Show();
                        Application.DoEvents();
                    }

                    bgAddLabels.WorkerSupportsCancellation = true;
                    bgAddLabels.RunWorkerCompleted += bgAddLabels_RunWorkerCompleted;
                    bgAddLabels.RunWorkerAsync(addSpectraLabelAction);
                }
            }
        }

        private void bgDirectagRun_DoWork(object sender, DoWorkEventArgs e)
        {
            // Do not access the form's BackgroundWorker reference directly.
            // Instead, use the reference provided by the sender parameter.
            BackgroundWorker bw = sender as BackgroundWorker;
            // Extract the argument.
            RunDirecTagAction arg = e.Argument as RunDirecTagAction;
            // If the operation was canceled by the user, 
            // set the DoWorkEventArgs.Cancel property to true.
            //if (bw.CancellationPending)
            //{
            //    e.Cancel = true;
            //    return;
            //}
            // Start the time-consuming operation.
            //e.Result = TimeConsumingOperation(bw, arg);
            while (!bw.CancellationPending)
            {
                arg.RunDirectag();
                return;
            }

        }

        private void bgDirectagRun_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                MessageBox.Show("Operation was canceled");
            }
            else
            if (e.Error != null)
            {
                string msg = String.Format("An error occurred: {0}", e.Error.Message);
                MessageBox.Show(msg);
            }
            //else
            //{
            //    string msg = "Operation completed";
            //    MessageBox.Show(msg);
            //}
        }

        private void bgWriteSpectra_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bw = sender as BackgroundWorker;
            WriteSpectraAction arg = e.Argument as WriteSpectraAction;
            while (!bw.CancellationPending)
            {
                arg.Write();
                return;
            }

        }

        private void bgWriteSpectra_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                string msg = String.Format("An error occurred: {0}", e.Error.Message);
                MessageBox.Show(msg);
            }
        }

        private void bgAddLabels_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bw = sender as BackgroundWorker;
            AddSpectraLabelAction arg = e.Argument as AddSpectraLabelAction;
            while (!bw.CancellationPending)
            {
                arg.AddSpectraLabel();
                return;
            }
        }

        private void bgAddLabels_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                string msg = String.Format("An error occurred: {0}", e.Error.Message);
                MessageBox.Show(msg);
            }
        }

        private void linkLabelHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            //System.Diagnostics.Process.Start("IExplore", "http://www.some.net");
            string helpPageURL = "file:///" + Path.GetDirectoryName(Application.ExecutablePath) + "/help.mht";
            System.Diagnostics.Process.Start(helpPageURL);
        }




        //public void CancelBgWorker()
        //{
        //    //if (bgDirectagRun.IsBusy)
        //    //if (bgDirectagRun.WorkerSupportsCancellation == true)
        //        bgDirectagRun.CancelAsync();
        //    //if (bgWriteSpectra.IsBusy)
        //    if (bgWriteSpectra.WorkerSupportsCancellation == true)
        //        bgWriteSpectra.CancelAsync();
        //    //if (bgAddLabels.IsBusy)
        //    if (bgAddLabels.WorkerSupportsCancellation == true)
        //        bgAddLabels.CancelAsync();
        //}

    }
}
