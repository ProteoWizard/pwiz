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
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Configuration;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using IdPickerGui.MODEL;
using IdPickerGui.BLL;
using IDPicker;

namespace IdPickerGui
{

    public partial class RunReportForm : Form
    {
        private string[] targetFileExtensions = { ".pepXML", ".idpXML", ".xml" };
        private int numNewGroups = 0;
        private Point groupsTreeViewRightClickPointToClient;
        private string invalidPathCharsToolTipText;
        private Dictionary<string, InputFileTag> selPathToTagDictionary = new Dictionary<string,InputFileTag>();
        private Dictionary<string, string> pathsToErrorsDictionary = new Dictionary<string, string>();
        private ToolTip invalidPathCharsToolTip;
        private ToolTip buttonToolTip;
        private IDPickerInfo idPickerRequest;
        private IdPickerActions runReportActions;

        private delegate void SortGroupsTreeDelegate();

        public int NumNewGroups
        {
            get { return numNewGroups; }
            set { numNewGroups = value; }
        }
        public Point GroupsTreeViewRightClickPointToClient
        {
            get { return groupsTreeViewRightClickPointToClient; }
            set { groupsTreeViewRightClickPointToClient = value; }

        }
        public string[] TargetFileExtensions
        {
            get { return targetFileExtensions; }
            set { targetFileExtensions = value; }
        }
        public IDPickerInfo IdPickerRequest
        {
            get { return idPickerRequest; }
            set { idPickerRequest = value; }
        }
        public IdPickerActions RunReportActions
        {
            get { return runReportActions; }
            set { runReportActions = value; }
        }
        public ToolTip InvalidPathCharsToolTip
        {
            get { return invalidPathCharsToolTip; }
            set { invalidPathCharsToolTip = value; }
        }
        public ToolTip ButtonToolTip
        {
            get { return buttonToolTip; }
            set { buttonToolTip = value; }
        }
        public string InvalidPathCharsToolTipText
        {
            get { return invalidPathCharsToolTipText; }
            set { invalidPathCharsToolTipText = value; }
        }
        public Dictionary<string, InputFileTag> SelPathToTagDictionary
        {
            get { return selPathToTagDictionary; }
            set { selPathToTagDictionary = value; }
        }
        public Dictionary<string, string> PathsToErrorsDictionary
        {
            get { return pathsToErrorsDictionary; }
            set { pathsToErrorsDictionary = value; }

        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public RunReportForm()
        {
            InitializeComponent();

            setupToolTips();

            IdPickerRequest = new IDPickerInfo();

            loadNewIdPickerRequestFromProperties();

            addRootNodeToGroupsTreeView();
        
        }

        /// <summary>
        /// Constructor for cloned request
        /// </summary>
        /// <param name="other"></param>
		public RunReportForm( IDPickerInfo other )
		{
			InitializeComponent();

            setupToolTips();

			IdPickerRequest = other;

            loadAndSetClonedIdPickerRequestValues();

            fillGroupsFromClonedRequest(tbSrcDir.Text.TrimEnd('/', '\\'));

		}

        /// <summary>
        /// Load default values for form, default values for request, and
        /// setup images for groups tree view
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RunReportForm_Load(object sender, EventArgs e)
        {
            setupGroupsTreeView();

            loadFormDefaultsFromRequest();
            
        }

        /// <summary>
        /// Find valid path for database filename in pepxml file using search paths in properties
        /// </summary>
        /// <param name="dbFileName"></param>
        /// <returns></returns>
        private string findDatabasePath(string dbFileName)
        {
            return IDPicker.Util.FindDatabaseInSearchPath(dbFileName, tbSrcDir.Text);
        }

        /// <summary>
        /// Load request with values on RunReportForm and values from properties
        /// </summary>
        private void buildIdPickerRequest()
        {
            StringBuilder sbErrorMsg = new StringBuilder();
            int numErrors = 0;
            int numNonGroupedFiles = 0;

            try
            {
                IdPickerRequest.SrcPathToTagCollection.Clear();

                // files assigned to groups
                fillSrcPathToTagCollectionFromGroupsTreeRecursively(tvGroups.Nodes[0], tbSrcDir.Text.TrimEnd('/', '\\'));

                foreach (ListViewItem lvi in lvNonGroupedFiles.Items)
                {
                    InputFileTag nonGroupedFileTag = new InputFileTag();

                    nonGroupedFileTag.TypeDesc = "file";
                    nonGroupedFileTag.GroupName = string.Empty;
                    nonGroupedFileTag.FullPath = lvi.Name;
                    numNonGroupedFiles++;

                    IdPickerRequest.SrcPathToTagCollection.Add(nonGroupedFileTag);
                }

                // needed this number for ghetto prog bars
                IdPickerRequest.NumGroupedFiles = IdPickerRequest.SrcPathToTagCollection.Count - numNonGroupedFiles;

                IdPickerRequest.Id = getIdForNewIdPickerReq();
                IdPickerRequest.ReportName = tbReportName.Text;
                IdPickerRequest.ResultsDir = tbResultsDir.Text;
                IdPickerRequest.DatabasePath = findDatabasePath(cboDbsInFiles.SelectedItem.ToString());
                IdPickerRequest.SrcFilesDir = tbSrcDir.Text.TrimEnd('/', '\\');
                IdPickerRequest.IncludeSubdirectories = cbInclSubDirs.Checked;
                IdPickerRequest.DateRequested = DateTime.Now;
                IdPickerRequest.DecoyPrefix = tbDecoyPrefix.Text;


                try
                {
                    IdPickerRequest.MaxFDR = Convert.ToSingle(cboMaxFdr.Text) / 100.0f;
                }
                catch (Exception)
                {
                    sbErrorMsg.Append("Maximum FDR\r\n");
                    numErrors++;

                }

                

                if (!tbMaxAmbigIds.Text.Equals(string.Empty))
                {

                    try
                    {
                        IdPickerRequest.MaxAmbiguousIds = Convert.ToInt32(tbMaxAmbigIds.Text);
                    }
                    catch (Exception)
                    {
                        sbErrorMsg.Append("Maximum ambiguous ids\r\n");
                        numErrors++;

                    }

                }
                 if (!tbMinPepLength.Text.Equals(string.Empty))
                {

                    try
                    {
                        IdPickerRequest.MinPeptideLength = Convert.ToInt32(tbMinPepLength.Text);
                    }
                    catch (Exception)
                    {
                        sbErrorMsg.Append("Minimum peptide length\r\n");
                        numErrors++;

                    }
                }
                if (!tbMinDistinctPeptides.Text.Equals(string.Empty))
                {

                    try
                    {
                        IdPickerRequest.MinDistinctPeptides = Convert.ToInt32(tbMinDistinctPeptides.Text);
                    }
                    catch (Exception)
                    {
                        sbErrorMsg.Append("Minimum distinct peptides\r\n");
                        numErrors++;

                    }
                }
                if (!tbMinAdditionalPeptides.Text.Equals(string.Empty))
                {

                    try
                    {
                        IdPickerRequest.MinAdditionalPeptides = Convert.ToInt32(tbMinAdditionalPeptides.Text);
                    }
                    catch (Exception)
                    {
                        sbErrorMsg.Append("Minimum additional peptides\r\n");
                        numErrors++;

                    }
                }
                if( !tbMinSpectraPerProtein.Text.Equals( string.Empty ) )
                {
                    try
                    {
                        idPickerRequest.MinSpectraPerProetin = Convert.ToInt32( tbMinSpectraPerProtein.Text );
                    } catch( Exception )
                    {
                        sbErrorMsg.Append( "Minimum spectra per protein\r\n" );
                        ++numErrors;
                    }
                }

                if (numErrors > 0)
                {
                    throw new Exception("Please check the following fields:\r\n" + sbErrorMsg.ToString());
                }

            }
            catch (Exception exc)
            {
                throw exc;
            }

        }

        /// <summary>
        /// Open FolderBrowserDialog and return dir result when selected
        /// </summary>
        /// <param name="sDefaultDir">Default highlited dir</param>
        /// <returns>Selected dir</returns>
        private string openBrowseDialog(string sPrevDir, Boolean newFolderOption)
        {
            try
            {
                FolderBrowserDialog dlgBrowseSource = new FolderBrowserDialog();

                if (!sPrevDir.Equals(string.Empty))
                {
                    dlgBrowseSource.SelectedPath = sPrevDir;
                }
                else
                {
                    dlgBrowseSource.SelectedPath = "c:\\";
                }

                dlgBrowseSource.ShowNewFolderButton = newFolderOption;

                DialogResult result = dlgBrowseSource.ShowDialog();

                if (result == DialogResult.OK)
                {
                    return dlgBrowseSource.SelectedPath;
                }

                return string.Empty;

            }
            catch (Exception exc)
            {
                throw new Exception("Error opening file dialog\r\n", exc);
            }
        }

        void readInputFile(string filepath, out InputFileType inputFileType, out string dbPath)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.ValidationType = ValidationType.None;
            settings.ValidationFlags = System.Xml.Schema.XmlSchemaValidationFlags.None;
            settings.IgnoreProcessingInstructions = true;
            settings.ProhibitDtd = false;
            settings.XmlResolver = null;

            int numTagsRead = 0;
            bool foundXMLTag = false;
            bool foundDatabase = false;
            inputFileType = InputFileType.Unknown;
            dbPath = null;

            using (StreamReader xmlStream = new StreamReader(filepath, true))
            using (XmlReader reader = XmlTextReader.Create(xmlStream, settings))
            {
                reader.Read();
                numTagsRead++;

                if (reader.Name.Equals("xml"))
                {
                    foundXMLTag = true;
                }

                if (foundXMLTag)
                {
                    // assuming msms tag appears in file before database tag
                    while (!foundDatabase && reader.Read() && numTagsRead < 20)
                    {
                        numTagsRead++;

                        if (reader.Name == "msms_pipeline_analysis")
                        {
                            inputFileType = InputFileType.PepXML;
                        }
                        else if (reader.Name == "idPickerPeptides")
                        {
                            inputFileType = InputFileType.IdpXML;
                        }
                        else if (reader.Name == "search_database")
                        {
                            dbPath = Path.GetFileName(getAttribute(reader, "local_path").Replace(".pro", ""));
                            foundDatabase = true;
                        }
                        else if (reader.Name == "proteinIndex")
                        {
                            dbPath = Path.GetFileName(getAttribute(reader, "database").Replace(".pro", ""));
                            if (!String.IsNullOrEmpty(dbPath))
                                foundDatabase = true;
                        }
                    }

                    if (inputFileType == InputFileType.IdpXML && !foundDatabase)
                    {
                        // old idpXML, look for database in spectraSources
                        while (!foundDatabase && reader.Read())
                        {
                            if (reader.Name == "processingParam" &&
                                getAttribute(reader, "name") == "ProteinDatabase")
                            {
                                dbPath = Path.GetFileName(getAttribute(reader, "value"));
                                foundDatabase = true;
                            }
                        }
                    }
                }
            }
        }

        #region getAttribute convenience functions
        private bool hasAttribute (XmlReader reader, string attribute)
        {
            if (reader.MoveToAttribute(attribute))
            {
                reader.MoveToElement();
                return true;
            }
            return false;
        }

        private string getAttribute (XmlReader reader, string attribute)
        {
            return getAttributeAs<string>(reader, attribute);
        }

        private string getAttribute (XmlReader reader, string attribute, bool throwIfAbsent)
        {
            return getAttributeAs<string>(reader, attribute, throwIfAbsent);
        }

        private T getAttributeAs<T> (XmlReader reader, string attribute)
        {
            return getAttributeAs<T>(reader, attribute, false);
        }

        private T getAttributeAs<T> (XmlReader reader, string attribute, bool throwIfAbsent)
        {
            if (reader.MoveToAttribute(attribute))
            {
                TypeConverter c = TypeDescriptor.GetConverter(typeof(T));
                if (c == null || !c.CanConvertFrom(typeof(string)))
                    throw new Exception("unable to convert from string to " + typeof(T).Name);
                T value = (T) c.ConvertFromString(reader.Value);
                reader.MoveToElement();
                return value;
            }
            else if (throwIfAbsent)
                throw new Exception("missing required attribute \"" + attribute + "\"");
            else if (typeof(T) == typeof(string))
                return (T) TypeDescriptor.GetConverter(typeof(T)).ConvertFromString(String.Empty);
            else
                return default(T);
        }
        #endregion

        /// <summary>
        /// Perform checks on target file assigned to node in file selection treeview.
        /// Checks include begins with xml, has database tag, has msms tag..
        /// The InputFileTag or NodeTag is also set for the node which contains this
        /// meta data from the file. Changing the appearance of the node based on these
        /// checks also once happened here.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="basePath"></param>
        private void checkAndSetFilteredFileNode(TreeNode node, string basePath)
        {
            InputFileTag nodeTag = node.Tag as InputFileTag;

            string dbPath = string.Empty;
            StringBuilder sbError = new StringBuilder();
            nodeTag.PassedChecks = true;
            InputFileType foundFileType = InputFileType.Unknown;

            try
            {
                readInputFile(nodeTag.FullPath, out foundFileType, out dbPath);

                if (foundFileType == InputFileType.Unknown)
                {
                    sbError.Append("msms_pipeline_analysis or idPickerPeptides tag not found.");
                    nodeTag.PassedChecks = false;
                }

                if (String.IsNullOrEmpty(dbPath))
                {
                    sbError.Append("database tag not found.");
                    nodeTag.PassedChecks = false;
                }

                nodeTag.DatabasePath = dbPath;
                nodeTag.GroupName = convertFilePathIntoGroupName(nodeTag.FullPath, basePath);

                if (!String.IsNullOrEmpty(dbPath) && !cboDbsInFiles.Items.Contains(dbPath))
                {
                    cboDbsInFiles.Items.Add(dbPath);
                }

                nodeTag.PassedChecks = nodeTag.PassedChecks && foundFileType != InputFileType.Unknown;

                if (!nodeTag.PassedChecks)
                {
                    node.ForeColor = Color.Salmon;
                    node.Text += "  *";
                    nodeTag.ErrorMsg = sbError.ToString();
                    node.ToolTipText = sbError.ToString();
                    nodeTag.AllowSelection = false;
                }
                else
                {
                    node.ForeColor = Color.Black;
                    //node.ToolTipText = nodeTag.FullPath;
                    nodeTag.AllowSelection = true;
                    nodeTag.FileType = foundFileType;
                }

            }
            catch (Exception)
            {
                nodeTag.PassedChecks = false;
                node.ForeColor = Color.Salmon;
                node.Text += "  *";
                nodeTag.ErrorMsg = "File format error.";
                node.ToolTipText = "File format error.";
                nodeTag.AllowSelection = false;
            }
        }

        /// <summary>
        /// Instead of using GetFiles with filter in our recursive building
        /// of our file sel treeview. We get all files in each sub dir
        /// and filter them using the array of target exts.
        /// </summary>
        /// <param name="fileInfos"></param>
        /// <returns></returns>
        private FileInfo[] filterFileInfoListByExt(FileInfo[] fileInfos)
        {
            ArrayList fileInfosToKeep = new ArrayList();

            try
            {
                foreach (FileInfo file in fileInfos)
                {
                    foreach (string ext in TargetFileExtensions)
                    {
                        if (file.Name.EndsWith(ext))
                        {
                            fileInfosToKeep.Add(file);

                            break;
                        }
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
        /// Setup db selection when file sel treeview is built
        /// </summary>
        private void enableListFilesSelection()
        {

            try
            {
                cboDbsInFiles.Enabled = true;
                lblDbInSelFiles.Enabled = true;
                cboDbsInFiles.SelectedIndex = 0;
             
            }
            catch (Exception exc)
            {
                throw new Exception("Error enabling list files process\r\n", exc);
            }

        }
        
        /// <summary>
        /// The things we need to do each time the list files process begins
        /// </summary>
        private void resetListFilesProcess()
        {
            try
            {
                tvSelDirs.Nodes.Clear();
                cboDbsInFiles.Items.Clear();
                cboDbsInFiles.Enabled = false;
                lblDbInSelFiles.Enabled = false;
                pathsToErrorsDictionary.Clear();

            }
            catch (Exception exc)
            {
                throw new Exception("Error displaying file errors\r\n", exc);
            }
        }

        /// <summary>
        /// Collapse all group treeview nodes
        /// </summary>
        /// <param name="treeNode"></param>
        private void collapseGroupNodesRecursively(TreeNode treeNode)
        {
            try
            {
                foreach (TreeNode tn in treeNode.Nodes)
                {
                    collapseGroupNodesRecursively(tn);
                }

                treeNode.Collapse();

            }
            catch (Exception exc)
            {
                throw new Exception("Error collapsing group nodes\r\n", exc);
            }

        }
      
        /// <summary>
        /// Populate a tree view with a directory structure looking for . m_fileExtension files.
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

                FileInfo[] fis = di.GetFiles();

                fis = filterFileInfoListByExt(fis);

                foreach (FileInfo fi in fis)
                {
                    TreeNode newFileNode;
                    string dbInFile = string.Empty;

                    newFileNode = new TreeNode( returnLastPathLevel(fi.FullName) );

                    InputFileTag newFileTag = new InputFileTag("file", fi.FullName, true);

                    newFileNode.Tag = newFileTag;

                    checkAndSetFilteredFileNode(newFileNode, basePath);

                    if (newFileTag.PassedChecks)    // file must have database to be included
                    {
                        nodeParent.Nodes.Add(newFileNode);
						newFileNode.Name = fi.FullName;
                    }
                    else
                    {
                        PathsToErrorsDictionary.Add(newFileTag.FullPath, newFileTag.ErrorMsg);
                    }
                    
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
            }
            catch (Exception exc)
            {
                throw new Exception("Error evaluating path\r\n", exc);
            }
        }

        /// <summary>
        /// All file sel treeview nodes text is make gray and their
        /// InputFileTag.AllowSelection property set to false rendering
        /// them uncheckable
        /// </summary>
        /// <param name="treeNode"></param>
        private void disableAllNodeSelectionRecursively(TreeNode treeNode)
        {
            InputFileTag currInputFileTag = (InputFileTag)treeNode.Tag;

            try
            {
                if (currInputFileTag != null && !currInputFileTag.IsRoot)
                {
                    // if not dir node then must be file node with tag
                    // set to database value read from pepxml file
                    if (!currInputFileTag.IsDir && currInputFileTag.PassedChecks)
                    {
                        treeNode.ForeColor = Color.LightGray;
                        ((InputFileTag)treeNode.Tag).AllowSelection = false;

                        /*
                        // if file node is only leaf node in this collection
                        // gray out the dir node
                        TreeNode parentNode = treeNode.Parent;
                        InputFileTag parentInputFileTag = (InputFileTag)parentNode.Tag;
                        if (parentNode != null && parentInputFileTag != null)
                        {
                            if (parentInputFileTag.Tag.Equals("dir") && parentNode.Nodes.Count == 1)
                            {
                                treeNode.Parent.ForeColor = Color.LightGray;

                            }

                        }
                         * */

                    }
                    if (currInputFileTag.IsDir)
                    {
                        treeNode.ForeColor = Color.Black;
                        ((InputFileTag)treeNode.Tag).AllowSelection = true;
                    }
                }

                foreach (TreeNode node in treeNode.Nodes)
                {
                    disableAllNodeSelectionRecursively(node);
                }
            }
            catch (Exception exc)
            {
                throw new Exception("File selection error\r\n", exc);

            }

        }

        /// <summary>
        /// Used to check for emtpy group nodes
        /// </summary>
        /// <param name="treeNode"></param>
        /// <param name="numEmpty"></param>
        private void getNumEmptyGroupNodesRecursively(TreeNode treeNode, ref int numEmpty)
        {
            InputFileTag currTag = treeNode.Tag as InputFileTag;

            try
            {
                if (currTag.IsGroup && treeNode.Nodes.Count == 0)
                {
                    numEmpty++;
                }

                foreach (TreeNode node in treeNode.Nodes)
                {
                    getNumEmptyGroupNodesRecursively(node, ref numEmpty);
                }

            }
            catch (Exception exc)
            {

                throw new Exception("Error checking for empty group nodes: getNumEmptyGroupNodesRecursively()\r\n", exc);

            }

        }
    
        /// <summary>
        /// Used to make sure a input file is selected before allowing next to step 2
        /// </summary>
        /// <param name="treeNode"></param>
        /// <param name="numChecked"></param>
        private void getNumCheckedFileNodesRecursively(TreeNode treeNode, ref int numChecked)
        {

            try
            {
                if ((treeNode.Tag as InputFileTag).IsFile && treeNode.Checked)
                {
                    numChecked++;
                }
                

                foreach (TreeNode node in treeNode.Nodes)
                {
                    getNumCheckedFileNodesRecursively(node, ref numChecked);
                }

            }
            catch (Exception exc)
            {

                throw new Exception("Error recursing tree to evaluating selected files\r\n", exc);

            }

        }

        /// <summary>
        /// All file nodes database prop in tag is checked against selected db in
        /// cbo, if doesn't match, the file node text is gray and AllowSelection = false
        /// else it is true and text stays black
        /// </summary>
        /// <param name="treeNode"></param>
        /// <param name="selDbName"></param>
        private void disableNodeSelectionByDatabaseRecursively(TreeNode treeNode, string selDbName)
        {
            InputFileTag currInputFileTag = (InputFileTag)treeNode.Tag;

            try
            {
                if (currInputFileTag != null && !currInputFileTag.IsRoot && currInputFileTag.PassedChecks)
                {
                    // if not dir node then must be file node with tag
                    // set to database value read from pepxml file
                    if (!currInputFileTag.IsDir && !(currInputFileTag.DatabasePath.Equals(selDbName)))
                    {
                        treeNode.ForeColor = Color.LightGray;
                        ((InputFileTag)treeNode.Tag).AllowSelection = false;

                        /*
                        // if file node is only leaf node in this collection
                        // gray out the dir node
                        TreeNode parentNode = treeNode.Parent;
                        InputFileTag parentInputFileTag = (InputFileTag)parentNode.Tag;
                        if (parentNode != null && parentInputFileTag != null)
                        {
                            if (parentInputFileTag.Tag.Equals("dir") && parentNode.Nodes.Count == 1)
                            {
                                treeNode.Parent.ForeColor = Color.LightGray;

                            }

                        }
                         * */

                    }
                    if (currInputFileTag.IsDir || currInputFileTag.DatabasePath.Equals(selDbName))
                    {
                        treeNode.ForeColor = Color.Black;
                        ((InputFileTag)treeNode.Tag).AllowSelection = true;
                    }
                }

                foreach (TreeNode node in treeNode.Nodes)
                {
                    disableNodeSelectionByDatabaseRecursively(node, selDbName);
                }
            }
            catch (Exception exc)
            {
                throw new Exception("File selection error\r\n", exc);

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

        /// <summary>
        /// Sel file paths and file node tags are added to the requests collection
        /// </summary>
        /// <param name="node"></param>
        /// <param name="basePath"></param>
        public void fillSrcPathToTagCollectionFromGroupsTreeRecursively(TreeNode node, string basePath)
        {
            InputFileTag currInputFileTag = node.Tag as InputFileTag;

            try
            {
                if (currInputFileTag.IsFile)
                {
                    string groupName = node.FullPath.Replace("//", "/");    // first node is '/' seperator is '/' so always starts with '//'

                    if ((node.Parent.Tag as InputFileTag).IsRoot)
                    {
                        currInputFileTag.GroupName = node.Parent.Text;
                    }
                    else
                    {
                        currInputFileTag.GroupName = groupName.Substring(0, groupName.LastIndexOf("/"));
                    }
                    
                    InputFileType inputFileType;
                    string dbPath;
                    readInputFile(currInputFileTag.FullPath, out inputFileType, out dbPath);

                    currInputFileTag.FileType = inputFileType;
                    currInputFileTag.DatabasePath = dbPath;

                    IdPickerRequest.SrcPathToTagCollection.Add(currInputFileTag);
                }

                foreach (TreeNode n in node.Nodes)
                {
                    fillSrcPathToTagCollectionFromGroupsTreeRecursively(n, basePath);
                }
            }
            catch (Exception exc)
            {
                throw new Exception("File selection error\r\n", exc);

            }

        }

        /// <summary>
        /// We need to keep track of the node tags assigned to the file nodes
        /// selected in the sel file tree view as they are moved in and out
        /// of the groups tree view. The group file nodes carry a key of the original
        /// path to the input file and this is used to retrieve the tag from the
        /// collection being filled here.
        /// </summary>
        /// <param name="node"></param>
        private void loadSelFileAndTagDictionaryRecursively(TreeNode node)
        {
            InputFileTag currInputFileTag = node.Tag as InputFileTag;

            try
            {
                if (node.Checked && currInputFileTag.IsFile)
                {
                    if (!SelPathToTagDictionary.ContainsKey(currInputFileTag.FullPath))
                    {
                        SelPathToTagDictionary.Add(currInputFileTag.FullPath, currInputFileTag);
                    }
                }

                foreach (TreeNode n in node.Nodes)
                {
                    loadSelFileAndTagDictionaryRecursively(n);
                }
            }
            catch (Exception exc)
            {
                throw new Exception("File selection error\r\n", exc);

            }

        }

        /// <summary>
        /// Group node gets context menu and image and new tag
        /// </summary>
        /// <param name="newGroupNode"></param>
        private void setupGroupNode(TreeNode newGroupNode)
        {
            try
            {
                newGroupNode.Tag = new InputFileTag("group", string.Empty, true);

                newGroupNode.ContextMenuStrip = cmRightClickGroupNode;
                newGroupNode.ImageIndex = 0;
                newGroupNode.SelectedImageIndex = 0;
            }
            catch (Exception exc)
            {
                throw new Exception("Error setting up group node\r\n", exc);

            }
        }

        /// <summary>
        /// File node gets context menu, image, and new tag
        /// </summary>
        /// <param name="newFileNode"></param>
        /// <param name="tag"></param>
        private void setupFileNode(TreeNode newFileNode)
        {
            try
            {
                InputFileTag tag = newFileNode.Tag as InputFileTag;

                newFileNode.ContextMenuStrip = cmRightClickFileNode;
                newFileNode.Name = tag.FullPath;
                newFileNode.ToolTipText = tag.FullPath;
                newFileNode.ImageIndex = 1;
                newFileNode.SelectedImageIndex = 1;
            }
            catch (Exception exc)
            {
                throw new Exception("Error setting up file node\r\n", exc);

            }
        }

        /// <summary>
        /// Want to add a root group node for new reports but not cloned reports
        /// </summary>
        private void addRootNodeToGroupsTreeView()
        {
            try
            {
                TreeNode rootNode = new TreeNode("/");
                rootNode.Tag = new InputFileTag("root", "/", true); ;
                rootNode.ContextMenuStrip = cmRightClickGroupNode;

                tvGroups.Nodes.Add(rootNode);
            }
            catch (Exception exc)
            {
                throw exc;
            }
        }

        /// <summary>
        /// Setup treeview with image list and comparer for groups treeview
        /// </summary>
        private void setupGroupsTreeView()
        {
            ImageList imageList = new ImageList();

            try
            {
                imageList.Images.Add(Properties.Resources.XPfolder_closed);
                imageList.Images.Add(Properties.Resources.file);

                tvGroups.ImageList = imageList;

                tvGroups.TreeViewNodeSorter = new GroupNodeSorter();

            }
            catch (Exception exc)
            {

                throw new Exception("Error initializing tree view image list\r\n", exc);
            }
                 
        }

        /// <summary>
        /// Fill groups (in cloned request) from collection in previous request instead of
        /// from the list of files selected as in a new request
        /// </summary>
        /// <param name="basePath"></param>
        private void fillGroupsFromClonedRequest(string basePath)
        {
            SourceGroupList groupList = new SourceGroupList();

            try
            {
                tvGroups.Nodes.Clear();
                lvNonGroupedFiles.Items.Clear();

                foreach (InputFileTag tag in IdPickerRequest.SrcPathToTagCollection)
                {
                    if (tag.GroupName != string.Empty)
                    {
                        SourceGroupInfo groupInfo = new SourceGroupInfo();

                        string groupName = tag.GroupName + "/" + Path.GetFileName(tag.FullPath);

                        groupInfo = groupList[groupName];

                        groupInfo.name = groupName;

                        Set<SourceInfo> sources = new Set<SourceInfo>();
                        SourceInfo si = new SourceInfo();
                        si.name = tag.FullPath;
                        sources.Add(si);

                        groupInfo.setSources(sources);
                    }
                    else // non grouped files
                    {
                        lvNonGroupedFiles.Items.Add(createListViewItemFromNodeTag(tag));
                    }
                 

                }

                groupList.assembleParentGroups();

                SourceGroupInfo rootGroup = groupList["/"];

                TreeNode rootNode = new TreeNode(rootGroup.name);

                //rootNode.BackColor = Color.Yellow;
                rootNode.Tag = new InputFileTag("root", "/", true); ;
                rootNode.ContextMenuStrip = cmRightClickGroupNode;

                tvGroups.Nodes.Add(rootNode);

                fillGroupsFromClonedRequestRecursively(rootGroup.getChildGroups(), rootNode);

                tvGroups.ExpandAll();

            }
            catch (Exception exc)
            {
                throw new Exception("Error loading groups\r\n", exc);
            }

        }

        /// <summary>
        /// When user hits next we reconcile selcted files to groups but also need to
        /// compare in reverse so we can keep any existing empty groups the user has
        /// defined
        /// </summary>
        /// <param name="treeNode"></param>
        /// <param name="groupList"></param>
        private void addEmptyGroupsToGroupListRecursively(TreeNode treeNode, ref SourceGroupList groupList)
        {
            try
            {
                InputFileTag currTag = treeNode.Tag as InputFileTag;

                // emtpy group currently defined, add to refactor
                if (treeNode.Nodes.Count == 0 && currTag.IsGroup)
                {
                    string groupName = treeNode.FullPath.Replace("//", "/");

                    SourceGroupInfo groupInfo = groupList[groupName];

                    groupInfo.name = groupName;

                }

                foreach (TreeNode node in treeNode.Nodes)
                {
                    addEmptyGroupsToGroupListRecursively(node, ref groupList);
                }

            }
            catch (Exception exc)
            {
                throw exc;
            }
            
        }

        /// <summary>
        /// Allowing groups to stay on the groups step so each time files are selected
        /// in step 1 and the user hits next..the list of files is compared to the list
        /// of groups and previously defined groups are kept, new files are placed in default 
        /// groups, and unchecked files are removed from groups (but groups stay) or removed
        /// from ungrouped files accordingly.
        /// </summary>
        /// <param name="basePath"></param>
        private void reFactorGroupsListIncludingNewFiles(string basePath)
        {
            SourceGroupList groupList = new SourceGroupList();

            string groupName = string.Empty;

            try
            {
                foreach (string path in SelPathToTagDictionary.Keys)
                {
                    TreeNode[] groupedFileNodes = tvGroups.Nodes.Find(path, true);

                    SourceGroupInfo groupInfo = new SourceGroupInfo();

                    Set<SourceInfo> sources = new Set<SourceInfo>();
                    SourceInfo si = new SourceInfo();
                    si.name = path;
                    sources.Add(si);
                    
                    // file already grouped, do not give default grouping
                    if (groupedFileNodes.Length == 1)
                    {
                        groupName = groupedFileNodes[0].FullPath.Replace("//", "/");

                        groupInfo = groupList[groupName];

                        groupInfo.name = groupName;

                    }
                    else if (!lvNonGroupedFiles.Items.ContainsKey(path))
                    // file not currently grouped, give default grouping
                    {
                        groupName = convertFilePathIntoGroupName(path, basePath);

                        groupInfo = groupList[groupName];

                        groupInfo.name = groupName;

                    }

                    groupInfo.setSources(sources);
                }

                // restore any empty groups
                addEmptyGroupsToGroupListRecursively(tvGroups.Nodes[0], ref groupList);

                groupList.assembleParentGroups();

                SourceGroupInfo rootGroup = groupList["/"];

                TreeNode rootNode = new TreeNode(rootGroup.name);

                //rootNode.BackColor = Color.Yellow;
                rootNode.Tag = new InputFileTag("root", "/", true); ;
                rootNode.ContextMenuStrip = cmRightClickGroupNode;

                reFactorGroupsListIncludingNewFilesRecursively(rootGroup.getChildGroups(), rootNode);

                tvGroups.Nodes.Clear();

                tvGroups.Nodes.Add(rootNode);

                tvGroups.ExpandAll();

            }
            catch (Exception exc)
            {
                throw new Exception("Error loading groups\r\n", exc);
            }

        }

        /// <summary>
        /// Allowing groups to stay on the groups step so each time files are selected
        /// in step 1 and the user hits next..the list of files is compared to the list
        /// of groups and previously defined groups are kept, new files are placed in default 
        /// groups, and unchecked files are removed from groups (but groups stay) or removed
        /// from ungrouped files accordingly.
        /// </summary>
        /// <param name="basePath"></param>
        private void reFactorGroupsListIncludingNewFilesRecursively(SourceGroupList groupList, TreeNode currNode)
        {
            string origFilePath = null;

            try
            {
                foreach (SourceGroupInfo groupInfo in groupList.Values)
                {
                    TreeNode newGroupNode = new TreeNode(groupInfo.getGroupName());

                    if (groupInfo.getChildGroups().Count == 0)
                    {
                        Set<SourceInfo> sources = groupInfo.getSources();

                        // file
                        if (sources.Count == 1) // know its a file
                        {
                            origFilePath = (sources.Keys[0] as SourceInfo).name;

                            TreeNode[] groupedFileNodes = tvGroups.Nodes.Find(origFilePath, true);

                            // file already grouped, do not give default grouping
                            if (groupedFileNodes.Length == 1)
                            {
                                newGroupNode.Tag = groupedFileNodes[0].Tag as InputFileTag;

                                if ((newGroupNode.Tag as InputFileTag).IsFile)
                                {
                                    setupFileNode(newGroupNode);
                                }

                            }
                            else
                            {
                                newGroupNode.Tag = SelPathToTagDictionary[origFilePath];

                                setupFileNode(newGroupNode);
                            }

                        }
                        else 
                        {
                            setupGroupNode(newGroupNode);
                        }
                        
                    }
                    else
                    {
                        setupGroupNode(newGroupNode);
                    }

                    reFactorGroupsListIncludingNewFilesRecursively(groupInfo.getChildGroups(), newGroupNode);

                    currNode.Nodes.Add(newGroupNode);

                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error filling groups tree\r\n", exc);

            }

        }



        /// <summary>
        /// Fill groups (in cloned request) from collection in previous request instead of
        /// from the list of files selected as in a new request
        /// </summary>
        /// <param name="basePath"></param>
        private void fillGroupsFromClonedRequestRecursively(SourceGroupList groupList, TreeNode currNode)
        {
            try
            {
                foreach (SourceGroupInfo groupInfo in groupList.Values)
                {
                    TreeNode newGroupNode = new TreeNode(groupInfo.getGroupName());

                    if (groupInfo.getChildGroups().Count == 0)
                    {
                        Set<SourceInfo> sources = groupInfo.getSources();
                        SourceInfo si = sources.Keys[0];

                        newGroupNode.Tag = IdPickerRequest.SrcPathToTagCollection[si.name];

                        setupFileNode(newGroupNode);

                    }
                    else
                    {
                        setupGroupNode(newGroupNode);
                    }

                    fillGroupsFromClonedRequestRecursively(groupInfo.getChildGroups(), newGroupNode);

                    currNode.Nodes.Add(newGroupNode);
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error filling groups tree\r\n", exc);

            }

        }

        ///<summary>
        /// Puts selected input files into default groups..builds groups treeview
        /// </summary>
        /// <param name="groupList"></param>
        /// <param name="currNode"></param>
        private void fillGroupsFromSelectedFilePathsRecursively(SourceGroupList groupList, TreeNode currNode)
        {
            try
            {
                foreach (SourceGroupInfo groupInfo in groupList.Values)
                {
                    TreeNode newGroupNode = new TreeNode(groupInfo.getGroupName());

                    if (groupInfo.getChildGroups().Count == 0)
                    {
                        newGroupNode.Tag = SelPathToTagDictionary[tbSrcDir.Text.TrimEnd('/', '\\') + groupInfo.name.Replace("/", "\\")];

                        setupFileNode(newGroupNode);

                    }
                    else
                    {
                        setupGroupNode(newGroupNode);
                    }

                    fillGroupsFromSelectedFilePathsRecursively(groupInfo.getChildGroups(), newGroupNode);

                    currNode.Nodes.Add(newGroupNode);
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error filling groups tree\r\n", exc);

            }

        }
        
        ///<summary>
        /// Puts selected input files into default groups..builds groups treeview
        /// </summary>
        /// <param name="groupList"></param>
        /// <param name="currNode"></param>
        private void fillGroupsFromSelectedFilePaths(string basePath)
        {
            SourceGroupList groupList = new SourceGroupList();

            try
            {
                tvGroups.Nodes.Clear();

                foreach (string path in SelPathToTagDictionary.Keys)
                {
                    //string groupName = Path.GetDirectoryName(path);

                    string groupName = convertFilePathIntoGroupName(path, basePath);

                    SourceGroupInfo groupInfo = new SourceGroupInfo();

                    //string groupName = returnGroupNameForPathAndSubDirs(basePath, path);

                    groupInfo = groupList[groupName];

                    groupInfo.name = groupName;

                }

                groupList.assembleParentGroups();

                SourceGroupInfo rootGroup = groupList["/"];

                TreeNode rootNode = new TreeNode(rootGroup.name);

                //rootNode.BackColor = Color.Yellow;
                rootNode.Tag = new InputFileTag("root", "/", true); ;
                rootNode.ContextMenuStrip = cmRightClickGroupNode;

                tvGroups.Nodes.Add(rootNode);

                fillGroupsFromSelectedFilePathsRecursively(rootGroup.getChildGroups(), rootNode);

                tvGroups.ExpandAll();

            }
            catch (Exception exc)
            {
                throw new Exception("Error loading groups\r\n", exc);
            }

        }

        /// <summary>
        /// Convert file path into group name. The base path is removed and forward 
        /// slash instead of backslash as delimiter
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="basePath"></param>
        /// <returns></returns>
        

        private string convertFilePathIntoGroupName(string filePath, string basePath)
        {
            try
            {
                filePath = filePath.Replace("/", "\\");
                basePath = basePath.Replace("/", "\\");

                string groupName = filePath.Replace(basePath, string.Empty).Replace("\\", "/");

                //groupName = groupName.Substring(0, groupName.IndexOf(".") - 1);

                return groupName;
            }
            catch (Exception exc)
            {
                throw new Exception("Error evaluating group name\r\n", exc);

            }

        }

        /// <summary>
        /// Setup form values for a cloned request, restore selected input files
        /// </summary>
        private void loadAndSetClonedIdPickerRequestValues()
        {
            StringBuilder fileNotFoundErrors = new StringBuilder();
            int fnfCount = 0;

            try
            {
                if (Directory.Exists(IdPickerRequest.SrcFilesDir))
                {
                    tbSrcDir.Text = IdPickerRequest.SrcFilesDir;

                    cbInclSubDirs.Checked = IdPickerRequest.IncludeSubdirectories;
                    btnGetFileNames_Click(this, new EventArgs());

                    string oldDatabase = Path.GetFileName(IdPickerRequest.DatabasePath);
                    if (cboDbsInFiles.Items.Contains(oldDatabase))
                        cboDbsInFiles.SelectedItem = oldDatabase;
                    else
                        throw new Exception("old database \"" + oldDatabase + "\" doesn't exist anymore");

                    foreach (InputFileTag tag in IdPickerRequest.SrcPathToTagCollection)
                    {
                        TreeNode[] keyNodes = tvSelDirs.Nodes.Find(tag.FullPath, true);

                        if (keyNodes.Length == 1)
                        {
                            keyNodes[0].Checked = true;
                        }
                        else if (keyNodes.Length == 0)
                        {
                            fileNotFoundErrors.Append("\r\n" + tag.FullPath);
                            fnfCount++;
                        }
                    }

                    if (fnfCount > 0)
                    {
                        Exception innerExc = new Exception(fileNotFoundErrors.ToString());
                        Exception exc = new Exception("The following files were included in the original report but are no longer valid: ", innerExc);

                        HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Warning);
                    }

                }
                else
                    throw new Exception("old source directory \"" + IdPickerRequest.SrcFilesDir + "\" doesn't exist anymore");



            }
            catch (Exception exc)
            {
                throw new Exception("Error loading default values for cloned request\r\n", exc);

            }

        }

        /// <summary>
        /// IdPickerRequest is loaded from properties and the values
        /// on RunReportForm
        /// </summary>
        private void loadNewIdPickerRequestFromProperties()
        {
			List<ScoreInfo> scores = new List<ScoreInfo>();
			List<ModOverrideInfo> mods = new List<ModOverrideInfo>();

            try
            {   
                // hard coded defaults
                IdPickerRequest.NumChargeStates = 3;
                IdPickerRequest.MaxResultRank = 1;
                IdPickerRequest.GenerateBipartiteGraphs = false;

                // defaults from properties
                IdPickerRequest.DecoyPrefix = IDPicker.Properties.Settings.Default.DecoyPrefix;
                IdPickerRequest.ResultsDir = Properties.Settings.Default.ResultsDir;
                IdPickerRequest.SrcFilesDir = Properties.Settings.Default.SourceDirectory;
                IdPickerRequest.MinDistinctPeptides = IDPicker.Properties.Settings.Default.MinDistinctPeptides;
                IdPickerRequest.MaxFDR = IDPicker.Properties.Settings.Default.MaxFdr;
                IdPickerRequest.MaxAmbiguousIds = IDPicker.Properties.Settings.Default.MaxAmbiguousIds;
                IdPickerRequest.MinPeptideLength = IDPicker.Properties.Settings.Default.MinPeptideLength;
                IdPickerRequest.MinAdditionalPeptides = IDPicker.Properties.Settings.Default.MinAdditionalPeptides;
                IdPickerRequest.MinSpectraPerProetin = IDPicker.Properties.Settings.Default.MinSpectraPerProtein;


                foreach (string s in IDPicker.Properties.Settings.Default.Scores)
                {
                    string[] scoreData = s.Split(',');

                    scores.Add(new ScoreInfo(scoreData[0], scoreData[1]));
                }
                IdPickerRequest.ScoreWeights = scores.ToArray();


                foreach (string s in IDPicker.Properties.Settings.Default.Mods)
                {
                    string[] modData = s.Split(',');

                    mods.Add(new ModOverrideInfo(modData[0], modData[1], Convert.ToInt32(modData[2])));
                }
                IdPickerRequest.ModOverrides = mods.ToArray();

                IdPickerRequest.OptimizeScorePermutations = IDPicker.Properties.Settings.Default.OptimizeScorePermutations;
                IdPickerRequest.OptimizeScoreWeights = IDPicker.Properties.Settings.Default.ApplyScoreOptimization;
                IdPickerRequest.NormalizeSearchScores = IDPicker.Properties.Settings.Default.NormalizeSearchScores;
				IdPickerRequest.ModsAreDistinctByDefault = IDPicker.Properties.Settings.Default.ModsAreDistinctByDefault;

            }
            catch (Exception exc)
            {
                throw new Exception("Error loading default values for new request\r\n", exc);
                
            }


        }

        /// <summary>
        /// Evaluate last info obj in memory and return id + 1
        /// </summary>
        /// <returns></returns>
        private int getIdForNewIdPickerReq()
        {
            try
            {
                IDPickerForm mainForm = (IDPickerForm)this.Owner;

                if (mainForm.PrevPickInfos.Count > 0)
                {
                    IDPickerInfo pInfo = (IDPickerInfo)mainForm.PrevPickInfos[mainForm.PrevPickInfos.Count - 1];

                    return (pInfo.Id + 1);
                }
                else
                {
                    return (1);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Error retrieving request id.", "IDPicker", MessageBoxButtons.OK, MessageBoxIcon.Error);

                ExceptionManager.logExceptionMessageByFormToFile(this, e.Message, DateTime.Now);

                return -1;
            }

        }

        /// <summary>
        /// Request info added to memory
        /// </summary>
        private void addNewIdPickerReqToCurr()
        {
            try
            {

                IDPickerForm mainForm = (IDPickerForm)this.Owner;

                if (mainForm.PrevPickInfos != null)
                {
                    mainForm.PrevPickInfos.Add(IdPickerRequest);
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error adding new request\r\n", exc);
            }

        }

        /// <summary>
        /// Check values and file sel before allowing prog to step 2
        /// </summary>
        /// <returns></returns>
        private bool validateStep1()
        {
            int numErrs = 0;
            int numFilesSelected = 0;

            StringBuilder sbErrMsg = new StringBuilder();

            try
            {
                // check report name supplied
                if (tbReportName.Text.Equals(string.Empty))
                {
                    sbErrMsg.Append("Report Name not supplied\r\n");

                    numErrs++;
                }

                try
                {
                    if (Path.GetPathRoot(tbResultsDir.Text).Equals(string.Empty))
                    {
                        sbErrMsg.Append("Cannot provide relative path (Path must begin with drive letter)\r\n");

                        numErrs++;
                    }
                }
                catch (Exception exc)
                {
                    sbErrMsg.Append(exc.Message + "\r\n");

                    numErrs++;
                }

                if (tvSelDirs.Nodes.Count > 0)
                {
                    getNumCheckedFileNodesRecursively(tvSelDirs.Nodes[0], ref numFilesSelected);
                }
                if (numFilesSelected <= 0)
                {
                    sbErrMsg.Append("No input files selected\r\n");

                    numErrs++;

                }

                if (tbResultsDir.Text.Equals(string.Empty))
                {
                    sbErrMsg.Append("Results directory invalid\r\n");

                    numErrs++;
                }

                if (numErrs > 0)
                {
                    throw new Exception(sbErrMsg.ToString());
                }
                else
                {
                    return true;
                }

            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);

                return false;
            }
        }

        /// <summary>
        /// Validate the request before allowing report to run
        /// </summary>
        /// <returns></returns>
        private bool validateAndBuildStep2()
        {
            int numErrs = 0;
            int numEmptyGroups = 0;
            StringBuilder sbErrMsg = new StringBuilder();

            try
            {
                
                // no continue with empty groups

                getNumEmptyGroupNodesRecursively(tvGroups.Nodes[0], ref numEmptyGroups);

                if (numEmptyGroups > 0 || tvGroups.Nodes[0].Nodes.Count == 0)
                {
                    sbErrMsg.Append("Groups without files (Emtpy Groups)\r\nPlease remove empty groups then try again.\r\n");

                    numErrs++;

                }
                 
                /*
                 * 07/08/08 Allow continue with non grouped files
                // no continue with files not assigned to groups

                if (lvFiles.Items.Count > 0)
                {
                    sbErrMsg.Append("Files not assigned to group\r\n");

                    numErrs++;
                }
                 * */

                // included build request here so can get full list of errors
                // from step 2 including checking files and groups and the
                // data in the fields

                try
                {
                    buildIdPickerRequest();
                }
                catch (Exception exc)
                {
                    sbErrMsg.Append(exc.Message + "\r\n");

                    numErrs++;
                }

                if (numErrs > 0)
                {
                    throw new Exception(sbErrMsg.ToString());
                }
                else
                {
                    return true;
                }
              
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);

                return false;
            }

        }

        /// <summary>
        /// Log Exceptions (and inner exceptions) to file. Show ExceptionsDialogForm.
        /// </summary>
        /// <param name="exc"></param>
        private void HandleExceptions(Exception exc, ExceptionsDialogForm.ExceptionType excType)
        {
            try
            {
                ExceptionsDialogForm excForm = new ExceptionsDialogForm();
                StringBuilder sbDetails = new StringBuilder();

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
                excForm.loadForm(excType);

                excForm.ShowDialog(this);
            }
            catch
            {
                throw exc;
            }

        }

        /// <summary>
        /// Load values on form from request (new or cloned)
        /// </summary>
        private void loadFormDefaultsFromRequest()
        {

            try
            {
                tbReportName.Text = IdPickerRequest.ReportName;
                tbDecoyPrefix.Text = IdPickerRequest.DecoyPrefix;
                tbResultsDir.Text = IdPickerRequest.ResultsDir;
                tbSrcDir.Text = IdPickerRequest.SrcFilesDir;
                tbMinDistinctPeptides.Text = IdPickerRequest.MinDistinctPeptides.ToString();
                tbMinSpectraPerProtein.Text = IdPickerRequest.MinSpectraPerProetin.ToString();
                cboMaxFdr.SelectedItem = IdPickerRequest.MaxFDR.ToString();
                tbMaxAmbigIds.Text = IdPickerRequest.MaxAmbiguousIds.ToString();
                tbMinPepLength.Text = IdPickerRequest.MinPeptideLength.ToString();
                tbMinAdditionalPeptides.Text = IdPickerRequest.MinAdditionalPeptides.ToString();
                cboMaxFdr.SelectedItem = Convert.ToString(IdPickerRequest.MaxFDR * 100);

            }
            catch (Exception exc)
            {
                throw new Exception("Error loading default values\r\n", exc);
            }


        }

        /// <summary>
        /// Save values on form to properties currently unused
        /// </summary>
        private void saveDefaults()
        {

            try
            {
                Properties.Settings.Default.SourceDirectory = tbSrcDir.Text.TrimEnd('/', '\\');
                IDPicker.Properties.Settings.Default.MinDistinctPeptides = Convert.ToInt32(tbMinDistinctPeptides.Text);
                IDPicker.Properties.Settings.Default.MaxFdr = Convert.ToSingle(cboMaxFdr.SelectedItem.ToString());
                IDPicker.Properties.Settings.Default.MaxAmbiguousIds = Convert.ToInt32(tbMaxAmbigIds.Text);
                IDPicker.Properties.Settings.Default.MinPeptideLength = Convert.ToInt32(tbMinPepLength.Text);
                IDPicker.Properties.Settings.Default.MinAdditionalPeptides = Convert.ToInt32(tbMinAdditionalPeptides.Text);
                IDPicker.Properties.Settings.Default.MinSpectraPerProtein = Convert.ToInt32(tbMinSpectraPerProtein.Text);
                IDPicker.Properties.Settings.Default.Save();
                Properties.Settings.Default.Save();
            }
            catch (Exception exc)
            {
                throw new Exception("Error saving default values\r\n", exc);
            }
        }





        // event handlers

        /// <summary>
        /// Browse for source dir.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnBrowseSrcDir_Click(object sender, EventArgs e)
        {
            try
            {
                string selDir = openBrowseDialog(tbSrcDir.Text, true);

                if (!selDir.Equals(string.Empty))
                {
                    tbSrcDir.Text = selDir;
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
            
        }

        /// <summary>
        /// Handler for button to fill tree view with dirs and sub dirs.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnGetFileNames_Click(object sender, EventArgs e)
        {
            try
            {
                resetListFilesProcess();

                if (Directory.Exists(tbSrcDir.Text))
                {
                    tvSelDirs.BeginUpdate();
                    Cursor = Cursors.WaitCursor;

                    try
                    {
                        string basePath = tbSrcDir.Text.Replace("\\", "/").TrimEnd('/');
                        string selPath = basePath;

                        TreeNode rootNode = tvSelDirs.Nodes.Add("/");

                        rootNode.Tag = new InputFileTag("root", "/", true);

                        fillFileTreeViewRecursively(selPath, rootNode, cbInclSubDirs.Checked, tbFilter.Text, basePath);

                        // no target files found in sel dir
                        if (tvSelDirs.Nodes[0].Nodes.Count == 0)
                        {
                            resetListFilesProcess();
                        }
                        else
                        {
                            tvSelDirs.ExpandAll();
                        }

                    }
                    catch
                    {
                        resetListFilesProcess();
                    }

                    Cursor = Cursors.Arrow;
                    tvSelDirs.EndUpdate();
                    lblStatus.Text = string.Empty;

                    if (cbShowFileErrors.Checked)
                    {
                        showFileErrorsInDialog();
                    }

                    if (tvSelDirs.Nodes.Count > 0 && cboDbsInFiles.Items.Count >= 1)
                    {
                        enableListFilesSelection();
                    }
                    else if (tvSelDirs.Nodes.Count == 0)
                    {
                        throw new Exception("No files found.");
                    }

                }
                else
                {
                    throw new Exception("Source directory not found.");
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }

        }

        /// <summary>
        /// User checks an input file and it is added to non groups files listview 
        /// </summary>
        /// <param name="inputFileTag"></param>
        private void addInputFileToGroupsStep(InputFileTag inputFileTag)
        {
            try
            {
                if (!SelPathToTagDictionary.ContainsKey(inputFileTag.FullPath))
                {
                    SelPathToTagDictionary.Add(inputFileTag.FullPath, inputFileTag);
                }

                if (lvNonGroupedFiles.Items.IndexOfKey(inputFileTag.FullPath) < 0)
                {
                    lvNonGroupedFiles.Items.Add(createListViewItemFromNodeTag(inputFileTag));
                }
            }
            catch (Exception exc)
            {
                throw exc;
            }

        }

        /// <summary>
        /// User unchecks an input file and it is removed from groups tree or ungrouped files list view
        /// </summary>
        /// <param name="inputFileTag"></param>
        private void removeInputFileFromGroupsStep(InputFileTag inputFileTag)
        {
            try
            {
                if (SelPathToTagDictionary.ContainsKey(inputFileTag.FullPath))
                {
                    SelPathToTagDictionary.Remove(inputFileTag.FullPath);
                }
                
                if (lvNonGroupedFiles.Items.IndexOfKey(inputFileTag.FullPath) >= 0)
                {
                    lvNonGroupedFiles.Items.RemoveByKey(inputFileTag.FullPath);
                }

                TreeNode[] groupedFileNodes = tvGroups.Nodes.Find(inputFileTag.FullPath, true);

                if (groupedFileNodes.Length == 1)
                {
                    groupedFileNodes[0].Remove();
                }
                

            }
            catch (Exception exc)
            {
                throw exc;
            }

        }

        /// <summary>
        /// Handler for check in tree view.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }

        }

        /// <summary>
        /// Show advanced options form
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAdvancedStep1_Click(object sender, EventArgs e)
        {
            try
            {
                RunReportAdvancedOptionsForm form = new RunReportAdvancedOptionsForm();

                form.ShowDialog(this);
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }

        }

        /// <summary>
        /// Remove any ungrouped files that are no long selected for process on first step
        /// </summary>
        private void checkAndRemoveAnyUnGroupedFilesNoLongerSelected()
        {
            try
            {
                foreach (ListViewItem lvi in lvNonGroupedFiles.Items)
                {
                    if (!SelPathToTagDictionary.ContainsKey(lvi.Name))
                    {
                        lvNonGroupedFiles.Items.Remove(lvi);
                    }
                }
            }
            catch (Exception exc)
            {
                throw exc;
            }

        }

        /// <summary>
        /// Get list of file paths in groups tree that were no longer selected in step 1
        /// (Can't remove tree nodes while iterating the collection)
        /// </summary>
        /// <param name="treeNode"></param>
        /// <param name="pathsToRemove"></param>
        private void getUnSelFilePathsFromGroupsRecursively(TreeNode treeNode, ref List<string> pathsToRemove)
        {
            try
            {
                foreach (TreeNode node in treeNode.Nodes)
                {
                    getUnSelFilePathsFromGroupsRecursively(node, ref pathsToRemove);
                }

                InputFileTag currTag = treeNode.Tag as InputFileTag;

                // if group node is file and is no long selected in first step
                // remove from groups treeview
                if (currTag.IsFile && !SelPathToTagDictionary.ContainsKey(currTag.FullPath))
                {
                    pathsToRemove.Add(currTag.FullPath);
                }

            }
            catch (Exception exc)
            {
                throw exc;
            }
        }

        /// <summary>
        /// Go through files on group page and make sure they are still selected
        /// as input files..if not remove them from groups treeview
        /// </summary>
        private void checkAndRemoveUnSelFilesFromGroupsRecursively(TreeNode treeNode)
        {
            try
            {
                List<string> unSelPaths = new List<string>();
                    
                getUnSelFilePathsFromGroupsRecursively(tvGroups.Nodes[0], ref unSelPaths);

                foreach (string filePath in unSelPaths)
                {
                    tvGroups.Nodes.Find(filePath, true)[0].Remove();
                }

            }
            catch (Exception exc)
            {
                throw exc;
            }

        }

        /// <summary>
        /// Selected files are added to ungrouped list if previously ungrouped
        /// else they are left in there existing groups
        /// </summary>
        private void checkAndSetInputFilesAndGroups()
        {
            try
            {
                foreach (KeyValuePair<string,InputFileTag> kvp in SelPathToTagDictionary)
                {
                    TreeNode[] keyGroupNodes = tvGroups.Nodes.Find(kvp.Value.FullPath, true);

                    // if sel file not represented on groups page
                    // add to ungrouped files list
                    // this way groups get to stay and do not have to be reset when new
                    // file selected
                    if (keyGroupNodes.Length == 0 && !lvNonGroupedFiles.Items.ContainsKey(kvp.Value.FullPath))
                    {
                        lvNonGroupedFiles.Items.Add(createListViewItemFromNodeTag(kvp.Value));
                    }

                }

            }
            catch (Exception exc)
            {
                throw exc;
            }

        }

        /// <summary>
        /// Next on step 1. Reset list of files selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnNextStep1_Click(object sender, EventArgs e)
        {
            try
            {
                if (validateStep1())
                {
                    pnlRunReportStep1.Visible = false;
                    pnlRunReportStep2.Visible = true;

                    this.Text = "Configure Data Groupings and Filters";

                    SelPathToTagDictionary.Clear();

                    // build col of sel input files
                    loadSelFileAndTagDictionaryRecursively(tvSelDirs.Nodes[0]);

                    // remove any files from groups that are no longer seledted
                    checkAndRemoveUnSelFilesFromGroupsRecursively(tvGroups.Nodes[0]);

                    // remove any files from ungrouped list that are no longer selected for process
                    checkAndRemoveAnyUnGroupedFilesNoLongerSelected();

                    reFactorGroupsListIncludingNewFiles(tbSrcDir.Text.TrimEnd('/', '\\'));

                    if (lvNonGroupedFiles.Items.Count == 0)
                    {
                        tbStartHere.Visible = true;
                    }
                    else
                    {
                        tbStartHere.Visible = false;
                    }

                }

            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }

        }

        /// <summary>
        /// Go through list of selected pepxml files and read the database
        /// from them. Stop at point we find one that doesn't match and
        /// do not check the rest. Returns true if database value matches
        /// in all files and false upon finding the first non-matching
        /// value for database tag.
        /// </summary>
        /// <param name="paths">Paths to selected source files.</param>
        /// <returns>True if database value matches in all pepxml files.
        /// False upon finding first non-matching entry.</returns>
        private bool checkDatabaseInPepXmlFiles(string[] paths)
        {
            ArrayList dbPaths = new ArrayList();
            string currPath = string.Empty;

            try
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    currPath = paths[i];

                    string pathInFile = IdPickerActions.getDatabaseFromPepxmlFile(currPath);

                    if (!dbPaths.Contains(pathInFile))
                    {
                        dbPaths.Add(pathInFile);
                    }

                    if (dbPaths.Count > 1)
                    {
                        return false;
                    }

                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;

        }

        /// <summary>
        /// From step 2 to step 1
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnBackStep2_Click(object sender, EventArgs e)
        {
            try
            {
                pnlRunReportStep1.Visible = true;
                pnlRunReportStep2.Visible = false;

                // if hit back and clone groups tree built as default on next
                //IdPickerRequest.SrcPathToTagCollection.Clear();

                this.Text = "Load and Qonvert Search Results";
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }

        }
        
        /// <summary>
        /// Finish step 2, RunReportForm disabled and report process begins. When
        /// complete form is enabled again and closes if completes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnFinish_Click(object sender, EventArgs e)
        {
            try
            {
                if (validateAndBuildStep2())
                {
                    Enabled = false;

                    runIdPickerReport();

                    Enabled = true;
                }

            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);

                tbStartHere.BackColor = Color.White;

                Enabled = true;
            }
        }

        /// <summary>
        /// Run qonvert exe and assemble and report steps from workspace dll. The
        /// info req object is set with result values by the report process or 
        /// exceptions are thrown
        /// </summary>
        private void runIdPickerReport()
        {
            RunReportActions = new IdPickerActions();

            try
            {
                RunReportActions.IdPickerRequest = IdPickerRequest;

                if( !Directory.Exists( IdPickerRequest.ResultsDir ) )
                    Directory.CreateDirectory( IdPickerRequest.ResultsDir );
                else
                {
                    DialogResult answer = MessageBox.Show( "The destination directory \"" + IdPickerRequest.ResultsDir + "\" already exists, do you want to overwrite it with the new report?",
                                                            "Overwrite destination?", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2 );
                    if( answer == DialogResult.Yes )
                    {
                        IDPickerForm form = this.Owner as IDPickerForm;
                        IDPickerInfo oldInfo = null;
                        foreach( IDPickerInfo info in form.PrevPickInfos )
                        {
                            if( info.ResultsDir == IdPickerRequest.ResultsDir )
                            {
                                oldInfo = info;
                                break;
                            }
                        }
                        if( oldInfo != null )
                            oldInfo.Active = 0;

                        // if the source directory is the existing qonverted directory, don't delete it!
                        if(Directory.Exists(Path.Combine( IdPickerRequest.ResultsDir, "qonverted" )) &&
                            !IdPickerRequest.SrcFilesDir.Contains(Path.Combine( IdPickerRequest.ResultsDir, "qonverted" )))
                            Directory.Delete( Path.Combine( IdPickerRequest.ResultsDir, "qonverted" ), true );

                        foreach( string filepath in Directory.GetFiles( IdPickerRequest.ResultsDir, "*.html" ) )
                            File.Delete( filepath );
                        foreach( string filepath in Directory.GetFiles( IdPickerRequest.ResultsDir, "*.js" ) )
                            File.Delete( filepath );
                        foreach( string filepath in Directory.GetFiles( IdPickerRequest.ResultsDir, "*.css" ) )
                            File.Delete( filepath );
                        //if( oldInfo != null )
                            //oldInfo.Active = 1;
                    } else
                    {
                        IdPickerRequest.RunStatus = RunStatus.Cancelled;

                        throw new Exception( "destination directory already existed and user declined to overwrite it" );
                    }
                }

                RunReportActions.startRequest();

                if( IdPickerRequest.RunStatus == RunStatus.Error )
                {
                    StringBuilder error = new StringBuilder();
                    error.AppendLine( "An error occurred while running the report." );

                    error.AppendLine( "The error log:" );
                    error.AppendLine( IdPickerRequest.StdError.ToString() + Environment.NewLine );

                    error.AppendLine( "Qonvert command:" );
                    error.AppendLine( IdPickerRequest.QonvertCommandLine + Environment.NewLine );

                    error.AppendLine( "Qonvert output:" );
                    error.AppendLine( IdPickerRequest.StdOutput.ToString() );

                    throw new Exception( error.ToString() );

                    //MessageBox.Show(error.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                } else
                {
                    addNewIdPickerReqToCurr(); // add to My Reports and to history file
                    Properties.Settings.Default.SourceDirectory = tbSrcDir.Text.TrimEnd('/', '\\'); // save last source directory for next time
                    Properties.Settings.Default.Save();
                    Close();

                    // automatically open the report after closing RunReportForm
                    if( IdPickerRequest.RunStatus == RunStatus.Complete )
                        ( Owner as IDPickerForm ).openReport( IdPickerRequest.Id );
                }
            }
            catch (Exception exc)
            {
                Enabled = true;
                throw exc;
            }
        }

        /// <summary>
        /// Restrict ability to sel files in treeview by the database value selected
        /// in the cbo.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cboDbsInFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            string dbFileName = string.Empty;   // db filename from pepxml files
            string dbPath = string.Empty;       // db path found for filename using search paths

            try
            {
                if (cboDbsInFiles.SelectedIndex > -1)
                {
                    checkAllNodesInTreeView(tvSelDirs.Nodes, false);

                    dbFileName = cboDbsInFiles.SelectedItem.ToString();
                    dbPath = findDatabasePath(dbFileName);

                    if (!dbPath.Equals(string.Empty))   // database found in search paths
                    {
                        disableNodeSelectionByDatabaseRecursively(tvSelDirs.Nodes[0], dbFileName);
                    }
                    else                                // database not found in search paths
                    {
                        // try the next database, or if we are the last database, give up with an error
                        StringBuilder error = new StringBuilder();
                        error.AppendFormat( "Database \"{0}\" not found in search path (configured in Tools/Options):\r\n", dbFileName );
                        error.Append( String.Join( "\r\n", IDPicker.Util.StringCollectionToStringArray( IDPicker.Properties.Settings.Default.FastaPaths ) ) );
                        MessageBox.Show( error.ToString(), "IDPicker", MessageBoxButtons.OK, MessageBoxIcon.Information );

                        if( cboDbsInFiles.SelectedIndex + 1 < cboDbsInFiles.Items.Count )
                            ++cboDbsInFiles.SelectedIndex;
                        else
                            disableAllNodeSelectionRecursively(tvSelDirs.Nodes[0]);
                    }

                }
                tvSelDirs.Focus();

            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }

            
        }

        /// <summary>
        /// Disallow selection of nodes in file sel treeview
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tvSelDirs_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            try
            {
                e.Cancel = true;
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
        }

        /// <summary>
        /// Prevent checking of nodes with tag value of AllowSelection = false
        /// set by checkAndSetFileNode() which uses either the database value in
        /// the file or file checking criteria to render the node checkable or uncheckable
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tvSelDirs_BeforeCheck(object sender, TreeViewCancelEventArgs e)
        {
            try
            {
                if (!e.Node.Checked)
                {
                    InputFileTag currInputFileTag = (InputFileTag)e.Node.Tag;

                    e.Cancel = !currInputFileTag.AllowSelection;
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
            
        }

        /// <summary>
        /// Open browse dir dialog
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnBrowseDestDir_Click(object sender, EventArgs e)
        {
            string selDir = openBrowseDialog(tbResultsDir.Text, true);

            try
            {
                if (!selDir.Equals(string.Empty))
                {
                    tbResultsDir.Text = selDir;

                    if (!tbReportName.Text.Equals(string.Empty))
                    {
                        tbResultsDir.Text += "\\" + tbReportName.Text;
                    }
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
        }

        /// <summary>
        /// Remove files from treeview when source dir text changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbSrcDir_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (tvSelDirs.Nodes.Count > 0)
                {
                    resetListFilesProcess();
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
        }

        /// <summary>
        /// Append report name onto results directory but allow changing
        /// of results dir to new value without the report name
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		private void tbReportName_TextChanged( object sender, EventArgs e )
		{
            try
            {
                if (IdPickerRequest.ReportName == null)
                    IdPickerRequest.ReportName = string.Empty;

                if (IdPickerRequest.ReportName.Length == 0)
                {
                    tbResultsDir.Text = tbResultsDir.Text.TrimEnd("/\\".ToCharArray());
                    tbResultsDir.Text += Path.DirectorySeparatorChar + tbReportName.Text;
                }
                else if (tbResultsDir.Text.Contains(IdPickerRequest.ReportName))
                {
                    string[] pathParts = tbResultsDir.Text.Split("/\\".ToCharArray());
                    for (int i = pathParts.Length-1; i >= 0; --i)
                        if (pathParts[i] == IdPickerRequest.ReportName)
                        {
                            pathParts[i] = tbReportName.Text;
                            break;
                        }
                    tbResultsDir.Text = String.Join(new string(Path.DirectorySeparatorChar, 1), pathParts).Replace(new string(Path.DirectorySeparatorChar, 2), new string(Path.DirectorySeparatorChar, 1));
                }

                IdPickerRequest.ReportName = tbReportName.Text;
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
		}

        /// <summary>
        /// Tooltip appears when typing invalid chars in path text boxes
        /// Setup button tooltip
        /// </summary>
        private void setupToolTips()
        {
            try
            {
                InvalidPathCharsToolTip = new ToolTip();

                InvalidPathCharsToolTip.IsBalloon = true;
                InvalidPathCharsToolTip.AutoPopDelay = 5000;
                InvalidPathCharsToolTip.InitialDelay = 500;
                InvalidPathCharsToolTip.ReshowDelay = 5000;


                InvalidPathCharsToolTipText = "A file name cannot contain any of the following characters: \r\n\t \\ / : * ? \" < > |";


                ButtonToolTip = new ToolTip();
                ButtonToolTip.AutoPopDelay = 1500;
                ButtonToolTip.InitialDelay = 500;
                ButtonToolTip.ReshowDelay = 5000;

            }
            catch (Exception exc)
            {
                throw new Exception("Error setting up invalid char tooltip\r\n", exc);

            }

        }

        /// <summary>
        /// Handles showing tooltip for text boxes with path values when
        /// invalid char key is pressed..tooltip displayed, char not added to
        /// text in box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbReportName_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    // except backspace \b
                    if (e.KeyChar.Equals(c) && !e.KeyChar.Equals('\b'))
                    {
                        e.Handled = true;

                        InvalidPathCharsToolTip.Show(InvalidPathCharsToolTipText, tbReportName, tbReportName.Location, 5000);

                        break;
                    }
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }

        }

        /// <summary>
        /// Hide invalid char tooltip if form is moved
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RunReportForm_Move(object sender, EventArgs e)
        {
            try
            {
                InvalidPathCharsToolTip.Hide(this);
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
        }

        /// <summary>
        /// Check to prevent group nodes from being dragged into their children
        /// group nodes.
        /// </summary>
        /// <returns></returns>
        private bool checkIfDestGroupAChildNodeOfMe(TreeNode destNode, TreeNode dragNode)
        {
            InputFileTag destTag = (destNode.Tag as InputFileTag);
            InputFileTag dragTag = (dragNode.Tag as InputFileTag);

            try
            {
                if (destTag.IsGroup && dragTag.IsGroup && destNode.Level > dragNode.Level)
                {
                    TreeNode currNode = destNode;

                    while (currNode.Parent != null)
                    {
                        if (currNode.Parent.Equals(dragNode))
                        {
                            return true;
                        }

                        currNode = currNode.Parent;
                    }

                    return false;

                }
                else
                {
                    return false;
                }

            }
            catch (Exception exc)
            {
                throw new Exception("Error validating group move: checkIfDestGroupAChildNodeOfMe()\r\n", exc);
            }

        }

        /// <summary>
        /// Drag over tvGroups - Drag from within, Drag from listviewbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tvGroups_DragOver(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent("System.Windows.Forms.TreeNode", true))
                {
                    Point pt = tvGroups.PointToClient(new Point(e.X, e.Y));

                    TreeNode destNode = tvGroups.GetNodeAt(pt);

                    TreeNode dragNode = e.Data.GetData("System.Windows.Forms.TreeNode", true) as TreeNode;

                    TreeNode newNode = dragNode.Clone() as TreeNode;

                    if (destNode != null && dragNode != null && destNode != dragNode)
                    {
                        InputFileTag destTag = (destNode.Tag as InputFileTag);
                        InputFileTag dragTag = (dragNode.Tag as InputFileTag);

                        // cannot drop onto files
                        if (destTag.IsFile)
                        {
                            e.Effect = DragDropEffects.None;

                        }
                        else if (checkIfDestGroupAChildNodeOfMe(destNode, dragNode))
                        {
                            e.Effect = DragDropEffects.None;
                        }
                        else
                        {
                            e.Effect = DragDropEffects.Move;
                        }
                    }
                    else
                    {
                        e.Effect = DragDropEffects.None;
                    }

                }

                else if (e.Data.GetDataPresent("System.Windows.Forms.ListViewItem", true))
                {
                    Point pt = tvGroups.PointToClient(new Point(e.X, e.Y));

                    TreeNode destNode = tvGroups.GetNodeAt(pt);

                    if (destNode != null)
                    {
                        InputFileTag destTag = destNode.Tag as InputFileTag;

                        if (destTag.IsGroup || destTag.IsRoot)
                        {
                            e.Effect = DragDropEffects.Move;
                        }
                        else
                        {
                            e.Effect = DragDropEffects.None;
                        }
                    }
                }

                else
                {
                    e.Effect = DragDropEffects.None;
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error handling drag and drop\r\n", exc);
            }

        }

        /// <summary>
        /// DragDropEffects.Move only
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tvGroups_ItemDrag(object sender, ItemDragEventArgs e)
        {
            try
            {
                DoDragDrop(e.Item, DragDropEffects.Move);
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
        }

        /// <summary>
        /// Drop from within treevew, Drop from listviewbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tvGroups_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Effect == DragDropEffects.Move)
                {
                    if (e.Data.GetDataPresent("System.Windows.Forms.ListViewItem", true))
                    {
                        Point pt = tvGroups.PointToClient(new Point(e.X, e.Y));

                        TreeNode destNode = tvGroups.GetNodeAt(pt);

                        if (destNode != null)
                        {
                            if ((destNode.Tag as InputFileTag).IsGroup || (destNode.Tag as InputFileTag).IsRoot)
                            {
                                foreach (ListViewItem lvi in lvNonGroupedFiles.SelectedItems)
                                {
                                    TreeNode newNode = new TreeNode(lvi.Text);

                                    newNode.Tag = SelPathToTagDictionary[lvi.Name];
                                    newNode.Name = lvi.Name;

                                    setupFileNode(newNode);

                                    destNode.Nodes.Add(newNode);

                                    if (destNode.Nodes.Count == 1)
                                    {
                                        destNode.Expand();
                                    }

                                    lvNonGroupedFiles.Items.Remove(lvi);
                                }

                                //destNode.ExpandAll();

                                tvGroups.Sort();
                            }

                        }

                       
                    }

                    if (e.Data.GetDataPresent("System.Windows.Forms.TreeNode", true))
                    {
                        Point pt = tvGroups.PointToClient(new Point(e.X, e.Y));

                        TreeNode destNode = tvGroups.GetNodeAt(pt);

                        TreeNode dragNode = e.Data.GetData("System.Windows.Forms.TreeNode", true) as TreeNode;

                        InputFileTag destNodeTag = (destNode.Tag as InputFileTag);
                        InputFileTag dragNodeTag = (dragNode.Tag as InputFileTag);

                        TreeNode newNode = dragNode.Clone() as TreeNode;

                        if (destNode != null && dragNode != null)
                        {

                            destNode.Nodes.Add(newNode);

                            if (destNode.Nodes.Count == 1)
                            {
                                destNode.Expand();
                            }

                            dragNode.Remove();

                            tvGroups.Sort();
                        }

                    }
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
           
        }

        /// <summary>
        /// DoDragDrop(e.Item, DragDropEffects.Move
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lvFiles_ItemDrag(object sender, ItemDragEventArgs e)
        {
            try
            {
                DoDragDrop(e.Item, DragDropEffects.Move);
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
        }

        /// <summary>
        /// DragDropEffects.Move;
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lvFiles_DragEnter(object sender, DragEventArgs e)
        {
            try
            {
                e.Effect = DragDropEffects.Move;
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
        }
  
        /// <summary>
        /// Drag from groups treeview
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lvFiles_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent("System.Windows.Forms.TreeNode", true))
                {
                    TreeNode dragNode = (TreeNode)e.Data.GetData("System.Windows.Forms.TreeNode");

                    removeGroupNode(dragNode);

                    tvGroups.Sort();
                    //tvGroups.ExpandAll();
                    
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }

        }

        /// <summary>
        /// Right click file node then remove, file added to listviewbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void removeFileNodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode selNode = tvGroups.GetNodeAt(GroupsTreeViewRightClickPointToClient);

            try
            {
                removeGroupNode(selNode);
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
            
        }

        /// <summary>
        /// Sets property for keeping track of node the mouse was over
        /// when right click occurred
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tvGroups_MouseClick(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.Button == MouseButtons.Right)
                {
                    GroupsTreeViewRightClickPointToClient = e.Location;
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
        }

        /// <summary>
        /// Right click group treeview Add Node
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void addGroupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode selNode = tvGroups.GetNodeAt(GroupsTreeViewRightClickPointToClient);

            try
            {
                addEmptyGroupNode(selNode);
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
            
        }

        /// <summary>
        /// Right click group treeview Rename
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void renameGroupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode selNode = tvGroups.GetNodeAt(GroupsTreeViewRightClickPointToClient);

            try
            {
                if ((selNode.Tag as InputFileTag).IsGroup)
                {
                    selNode.BeginEdit();
                }
                else if ((selNode.Tag as InputFileTag).IsRoot)
                {
                    MessageBox.Show("Sorry, you cannot rename the root group.", "IDPicker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }

        }

        /// <summary>
        /// Gets list of tags associated with group recursive, used to determine
        /// all the files below and group to be removed (orphaned files)
        /// </summary>
        /// <param name="treeNode"></param>
        /// <param name="fileTagList"></param>
        private void getListOfOrphanedGroupFilesFromNodeRecursively(TreeNode treeNode, ref List<InputFileTag> fileTagList)
        {
            try
            {
                if ((treeNode.Tag as InputFileTag).IsFile)
                {
                    fileTagList.Add(treeNode.Tag as InputFileTag);
                }

                foreach (TreeNode subNode in treeNode.Nodes)
                {
                    getListOfOrphanedGroupFilesFromNodeRecursively(subNode, ref fileTagList);
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error in drag and drop\r\n", exc);
            }

        }

        /// <summary>
        /// Right click group treeview Remove
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void removeGroupToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            TreeNode selNode = tvGroups.GetNodeAt(GroupsTreeViewRightClickPointToClient);

            try
            {
                removeGroupNode(selNode);
            }
            catch (Exception exc)
            {
                throw new Exception("Error in drag and drop\r\n", exc);
            }


        }

        /// <summary>
        /// Set location for determining node that was clicked on
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tvGroups_MouseDown(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.Button == MouseButtons.Right)
                {
                    GroupsTreeViewRightClickPointToClient = e.Location;
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
        }

        /// <summary>
        /// Do not allow edit of root or file nodes in groups treeview
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tvGroups_BeforeLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            try
            {
                if (!(e.Node.Tag as InputFileTag).IsGroup)
                {
                    e.CancelEdit = true;
                }
                else
                {
                    btnAddGroup.Enabled = false;
                }

            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
        }

        /// <summary>
        /// Enable add group button only when group node selected by file or keyboard..
        /// Not by default selection which the control is so fond of
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tvGroups_AfterSelect(object sender, TreeViewEventArgs e)
        {
            try
            {
               
                if ((e.Node.Tag as InputFileTag).IsGroup || (e.Node.Tag as InputFileTag).IsRoot)
                {
                    btnAddGroup.Enabled = true;
                }
                else
                {
                    btnAddGroup.Enabled = false;
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
        }

        /// <summary>
        /// Consolidated code for setting up and adding empty group node
        /// </summary>
        /// <param name="selNode"></param>
        private void addEmptyGroupNode(TreeNode selNode)
        {
            try
            {
                if ((selNode.Tag as InputFileTag).IsGroup || (selNode.Tag as InputFileTag).IsRoot)
                {
                    NumNewGroups++;

                    TreeNode newNode = new TreeNode("New Group (" + NumNewGroups.ToString() + ")");

                    newNode.Tag = new InputFileTag("group", string.Empty, true);

                    setupGroupNode(newNode);

                    selNode.Nodes.Add(newNode);

                    newNode.Name = newNode.FullPath.Replace("//", "/");

                    selNode.Expand(); //selNode.ExpandAll();

                    newNode.BeginEdit();

                }
            }
            catch (Exception)
            {
                throw new Exception("Error adding empty group node\r\n");
            }

        }

        /// <summary>
        /// List view item is added with path to file as key and file name
        /// only as viewed item in listviewbox
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        private ListViewItem createListViewItemFromNodeTag(InputFileTag tag)
        {
            ListViewItem lvi = new ListViewItem();

            try
            {
                lvi.Name = tag.FullPath;
                lvi.Text = Path.GetFileName(tag.FullPath);
                lvi.ToolTipText = tag.FullPath;

                return lvi;
                
            }
            catch (Exception exc)
            {
                throw new Exception("Error creating ListViewItem from TreeNode\r\n", exc);
            }

        }

        /// <summary>
        /// Consolidated code to remove a group node (groups and files)
        /// </summary>
        /// <param name="selNode"></param>
        private void removeGroupNode(TreeNode selNode)
        {
            InputFileTag selNodeTag = selNode.Tag as InputFileTag;

            try
            {
                if (selNodeTag.IsGroup)
                {
                    List<InputFileTag> orphanFileTags = new List<InputFileTag>();

                    getListOfOrphanedGroupFilesFromNodeRecursively(selNode, ref orphanFileTags);

                    foreach (InputFileTag orphanTag in orphanFileTags)
                    {
                        if (lvNonGroupedFiles.Items.IndexOfKey(orphanTag.FullPath) < 0)
                        {
                            lvNonGroupedFiles.Items.Add(createListViewItemFromNodeTag(orphanTag as InputFileTag));
                        }
                    }

                    selNode.Remove();

                }
                else if (selNodeTag.IsFile)
                {
                    if (lvNonGroupedFiles.Items.IndexOfKey(selNodeTag.FullPath) < 0)
                    {
                        lvNonGroupedFiles.Items.Add(createListViewItemFromNodeTag(selNode.Tag as InputFileTag));
                    }

                    selNode.Remove();
                }
                else if ((selNode.Tag as InputFileTag).IsRoot && selNode.Nodes.Count > 0)
                {
                    if (DialogResult.Yes == MessageBox.Show("Do you wish to remove the root group and all its sub groups?", "IDPicker", MessageBoxButtons.YesNo, MessageBoxIcon.Information))
                    {
                        List<InputFileTag> orphanFileTags = new List<InputFileTag>();

                        getListOfOrphanedGroupFilesFromNodeRecursively(selNode, ref orphanFileTags);

                        foreach (InputFileTag orphanTag in orphanFileTags)
                        {
                            if (lvNonGroupedFiles.Items.IndexOfKey(orphanTag.FullPath) < 0)
                            {
                                lvNonGroupedFiles.Items.Add(createListViewItemFromNodeTag(orphanTag as InputFileTag));
                            }
                        } 

                        tvGroups.Nodes[0].Nodes.Clear();

                        tvGroups.SelectedNode = null;

                        
                    }

                }

                if (lvNonGroupedFiles.Items.Count <= 0)
                {
                    tbStartHere.Visible = true;
                }
                else
                {
                    tbStartHere.Visible = false;
                }
                
                btnAddGroup.Enabled = false;
            }
            catch (Exception exc)
            {
                throw new Exception("Error removing group node\r\n", exc);
            }

        }

        /// <summary>
        /// Button to add group handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAddGroup_Click(object sender, EventArgs e)
        {
            TreeNode selNode = tvGroups.SelectedNode;

            try
            {
                if (selNode != null)
                {
                    addEmptyGroupNode(selNode);

                    btnAddGroup.Enabled = false;
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
        }

        /// <summary>
        /// Allow delete (delete key) and rename (F2 key) of group nodes (not root or file)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tvGroups_KeyDown(object sender, KeyEventArgs e)
        {
            TreeNode selNode = tvGroups.SelectedNode;

            try
            {
                if (e.KeyCode.Equals(Keys.Delete))
                {
                    removeGroupNode(selNode);
                }
                else if (e.KeyCode.Equals(Keys.F2) && (selNode.Tag as InputFileTag).IsGroup)
                {
                    selNode.BeginEdit();
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
        }

        /// <summary>
        /// Show any errors found while evaluating target files for inclusion
        /// in file sel tree view in ExceptionsDialogForm
        /// </summary>
        private void showFileErrorsInDialog()
        {
            StringBuilder sbErrors = new StringBuilder();

            try
            {
                foreach (KeyValuePair<string,string> kvp in PathsToErrorsDictionary)
                {
                    sbErrors.Append("file: " + kvp.Key + "\r\n" + kvp.Value + "\r\n");
                }

                throw new Exception(sbErrors.ToString());
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("The following errors occurred while checking the files in the target directory\r\n", exc), ExceptionsDialogForm.ExceptionType.Error);
            }
        }

        /// <summary>
        /// Reset groups treeview to reflect heirarchy of actual location
        /// minus bas path
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnReset_Click(object sender, EventArgs e)
        {
            try
            {
                fillGroupsFromSelectedFilePaths(tbSrcDir.Text.TrimEnd('/', '\\'));
                lvNonGroupedFiles.Items.Clear();
                tbStartHere.Visible = true;

            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error resetting groups\r\n", exc), ExceptionsDialogForm.ExceptionType.Error);
            }
        }

        /// <summary>
        /// Remove all groups and files below root (to listviewbox)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRemoveGroups_Click(object sender, EventArgs e)
        {
            TreeNode selNode = tvGroups.Nodes[0];

            try
            {
                removeGroupNode(selNode);
            }

            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error resetting groups\r\n", exc), ExceptionsDialogForm.ExceptionType.Error);
            }

        }

        /// <summary>
        /// Drag/Drop for ungrouped files
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lvFiles_DragOver(object sender, DragEventArgs e)
        {
            try
            {
                e.Effect = DragDropEffects.Move;
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }

        }

        /// <summary>
        /// This hint needs to disappear when the user starts dragging files
        /// or moving files to the ungrouped files list view
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbStartHere_DragOver(object sender, DragEventArgs e)
        {
            try
            {
                tbStartHere.Visible = false;
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }

        }

        /// <summary>
        /// The RunReport form is being set inactive when the process starts
        /// and the background of this hint textbox didn't match the
        /// disabled color of the form so it looked weird
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbStartHere_EnabledChanged(object sender, EventArgs e)
        {
            try
            {
                tbStartHere.BackColor = this.BackColor;
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }

        }

     
        /// <summary>
        /// CTRL A to select all files
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lvFiles_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if ((e.Modifiers & Keys.Control) == Keys.Control)
                {
                    if (e.KeyCode == Keys.A)
                    {
                        foreach (ListViewItem lvi in lvNonGroupedFiles.Items)
                        {
                            lvi.Selected = true;
                        }

                    }

                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }


        }

        /// <summary>
        /// Display help html doc with anchor link
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cmWhatsThis_Click(object sender, EventArgs e)
        {
            try
            {
                Control control = (sender as ContextMenuStrip).SourceControl;
                string anchor = control.Text.Substring(0, control.Text.Length - 1);

                Uri uri = new Uri(Common.docFilePath + "#" + Common.getAnchorNameByControlName(control.Name));

                HtmlHelpForm form = new HtmlHelpForm(uri);

                form.Show();
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }
        }


        /// <summary>
        /// Used for delegate sort of groups treeview (afterlabeledit)
        /// </summary>
        private void sortGroupsTreeView()
        {
            try
            {
                tvGroups.Sort();
            }
            catch (Exception exc)
            {
                throw new Exception("Error sorting groups treeview\r\n", exc);
            }

        }


        /// <summary>
        /// Sort treeview after adding nodes - see GroupNodeSorter class
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tvGroups_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            try
            {
                // for some reason the default group name is not copied to the
                // e.label property in the event
                // so this is case for user adds node and accepts default name
                if (e.Label == null && e.Node.Text.StartsWith("New Group"))
                {
                    BeginInvoke(new SortGroupsTreeDelegate(sortGroupsTreeView));
                }
                else if (e.Label != null)
                {
                    if (e.Label != string.Empty && e.Label.IndexOfAny(new char[] { '/' }) == -1)
                    {
                        BeginInvoke(new SortGroupsTreeDelegate(sortGroupsTreeView));
                    }
                    else
                    {
                        e.CancelEdit = true;

                        throw new Exception("Invalid group name.\r\n\r\nGroup names cannot be empty or contain the following chars: '/'.");
                    }
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }

           
        }

        /// <summary>
        /// Show tooltip for collapse groups button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCollapseGroups_MouseHover(object sender, EventArgs e)
        {
            try
            {
                if (ButtonToolTip.Active)
                {
                    ButtonToolTip.Show("Collapse all groups", btnCollapseGroups, 1500);
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error displaying button tooltips\r\n", exc);
            }

        }

        /// <summary>
        /// Show tooltip for expand groups button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnExpandGroups_MouseHover(object sender, EventArgs e)
        {
            try
            {
                if (ButtonToolTip.Active)
                {
                    ButtonToolTip.Show("Expand all groups", btnExpandGroups, 1500);
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error displaying button tooltips\r\n", exc);
            }


        }

        /// <summary>
        /// Files are moved from listview into groups treeview with hierarchy
        /// reflecting their source dir minus the base path or starting dir
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void miDefaultGroups_Click(object sender, EventArgs e)
        {
            try
            {
                if (DialogResult.Yes == MessageBox.Show("All files will be placed into default groups reflecting their current location.\r\n\r\nContinue?", "IDPicker", MessageBoxButtons.YesNo, MessageBoxIcon.Information))
                {
                    fillGroupsFromSelectedFilePaths(tbSrcDir.Text.TrimEnd('/', '\\'));
                    lvNonGroupedFiles.Items.Clear();
                    tbStartHere.Visible = true;
                }

            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error resetting groups\r\n", exc), ExceptionsDialogForm.ExceptionType.Error);
            }
        }

        /// <summary>
        /// Menu item for removing all files and groups from groups treeview and 
        /// placing the files in the ungrouped files listview
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void miResetFiles_Click(object sender, EventArgs e)
        {
            TreeNode selNode = tvGroups.Nodes[0];

            try
            {
                removeGroupNode(selNode);
            }

            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error resetting groups\r\n", exc), ExceptionsDialogForm.ExceptionType.Error);
            }

        }

        /// <summary>
        /// Menu item for expanding groups
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void miExpandGroups_Click(object sender, EventArgs e)
        {
            try
            {
                tvGroups.Nodes[0].ExpandAll();
            }
            catch (Exception exc)
            {
                throw new Exception("Error collapsing groups\r\n", exc);
            }

        }

        /// <summary>
        /// Menu item for collapsing groups
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void miCollapseGroups_Click(object sender, EventArgs e)
        {
            try
            {
                collapseGroupNodesRecursively(tvGroups.Nodes[0]);

                tvGroups.Nodes[0].Expand();
            }
            catch (Exception exc)
            {
                throw new Exception("Error collapsing groups\r\n", exc);
            }

        }

        /// <summary>
        /// Background color didn't match background color of disabled form
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pnlUngroupedSpacer_EnabledChanged(object sender, EventArgs e)
        {
            try
            {
                if (!Enabled)
                {
                    pnlUngroupedSpacer.BackColor = this.BackColor;
                }
                else
                {
                    pnlUngroupedSpacer.BackColor = Color.White;
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }

        }

        /// <summary>
        /// Background color didn't match background color of disabled form
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbStartHere_EnabledChanged_1(object sender, EventArgs e)
        {
            try
            {
                if (!Enabled)
                {
                    tbStartHere.BackColor = this.BackColor;
                }
                else
                {
                    tbStartHere.BackColor = Color.White;
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc, ExceptionsDialogForm.ExceptionType.Error);
            }

        }

       
       
       
    }

    /// <summary>
    /// Comparer for sorting group nodes. Files first then folders in alpha order.
    /// Tree resorts when nodes moved, added, renamed.
    /// </summary>
    public class GroupNodeSorter : IComparer
    {
        // Compare the the strings
        public int Compare(object x, object y)
        {
            TreeNode tx = x as TreeNode;
            TreeNode ty = y as TreeNode;

            InputFileTag tagX = tx.Tag as InputFileTag;
            InputFileTag tagY = ty.Tag as InputFileTag;

            if ( (tagX.TypeDesc == "root" || tagX.TypeDesc == "group") && ( tagY.TypeDesc == "root" || tagY.TypeDesc == "group") )
            {
                return String.Compare(tx.Text, ty.Text);
            }
            else if (tagX.IsGroup || tagX.IsRoot)
            {
                return 1;
            }
            else
            {
                return 0;
            }

            
        }

    }
 
}