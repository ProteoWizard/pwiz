//
// $Id: TableExporter.cs 287 2011-08-05 16:41:22Z holmanjd $
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
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s): 
//

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using IDPicker;
using IDPicker.DataModel;
using Microsoft.Win32;

namespace CustomDataSourceDialog
{
    public partial class IDPOpenDialog : Form
    {
        private BreadCrumbControl BreadCrumbs;
        private List<string> _historyQueue;
        private TreeNode _unfilteredNode;
        private Dictionary<string, List<string[]>> _spectraFolders;
        private Dictionary<string, List<string[]>> _spectraFiles;
        private Dictionary<string, IEnumerable<string>> _extensionList;
        private Dictionary<TreeNode, List<TreeNode>> _relatedNodes;
        private IEnumerable<string> _allExtensions;
        private int _placeInQueue = -1;
        private bool _navigatingHistory;
        public List<string> DataSources;
        private string _startLocation;

        public delegate string SpectraCheckDelegate(string pathToCheck);

        /// <summary>
        /// Should return "File Folder" if folder is just a regular folder
        /// </summary>
        public SpectraCheckDelegate FolderType;

        /// <summary>
        /// Should return string.Empty if file should be ignored, 
        /// otherwise just return what kind of file it is
        /// </summary>
        public SpectraCheckDelegate FileType;

        #region Startup Functions

        public IDPOpenDialog () : this(new string[] { "All Files|*.*" }) { }

        public IDPOpenDialog(IEnumerable<string> fileTypes)
        {
            InitializeComponent();
            _relatedNodes = new Dictionary<TreeNode, List<TreeNode>>();
            _extensionList = new Dictionary<string, IEnumerable<string>>();
            foreach (var item in fileTypes)
            {
                var twoSides = item.Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                if (twoSides.Length != 2)
                    continue;
                var key = String.Format("{0} ({1})", twoSides[0], twoSides[1]);
                sourceTypeComboBox.Items.Add(key);
                var extensionList = twoSides[1].Replace("*.", ".").Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                _extensionList.Add(key, extensionList);

                if (twoSides[0] == "IDPicker Files")
                    _allExtensions = extensionList;
            }
            if (_allExtensions == null)
            {
                if (_extensionList.Count > 0)
                    _allExtensions = _extensionList.First().Value;
            }
            FileTreeView.Nodes.Add(new TreeNode { Text = "/", Tag = "Root", Checked = true});
            _unfilteredNode = FileTreeView.Nodes[0];
            SetUpDialog();

            _startLocation = BreadCrumbControl.GetMostRecentLocation();
        }

        public IDPOpenDialog(IEnumerable<string> fileTypes, string initialLocation) : this(fileTypes)
        {
            _startLocation = initialLocation;
        }

        private void SetUpDialog()
        {
            _historyQueue = new List<string>();
            ArrowPicture.Tag = new ContextMenu();
            _spectraFolders = new Dictionary<string, List<string[]>>();
            _spectraFiles = new Dictionary<string, List<string[]>>();
            DataSources = new List<string>();
            sourceTypeComboBox.SelectedIndex = 0;
            
            var folderNames = new List<string>();

            //Desktop
            var specialLocation = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            var node = new TreeNode("Desktop")
                           {
                               Tag = specialLocation.FullName,
                               ImageIndex = 1,
                               SelectedImageIndex = 1
                           };
            if (specialLocation.GetDirectories().Any())
            {
                var placeholder = new TreeNode("<<Placeholder>>");
                node.Nodes.Add(placeholder);
            }
            FileTree.Nodes.Add(node);
            folderNames.Add((string)node.Tag);

            //My Documents
            specialLocation = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            node = new TreeNode("My Documents")
                       {
                           Tag = specialLocation.FullName,
                           ImageIndex = 2,
                           SelectedImageIndex = 2
                       };
            if (specialLocation.GetDirectories().Any())
            {
                var placeholder = new TreeNode("<<Placeholder>>");
                node.Nodes.Add(placeholder);
            }
            FileTree.Nodes.Add(node);
            folderNames.Add((string)node.Tag);

            //Add drives
            foreach (var drive in DriveInfo.GetDrives())
            {
                var tn = new TreeNode(drive.Name) {Tag = drive.Name};
                switch (drive.DriveType)
                {
                    case DriveType.CDRom:
                    case DriveType.Removable:
                        tn.ImageIndex = 6;
                        tn.SelectedImageIndex = 6;
                        break;
                    case DriveType.Fixed:
                        tn.ImageIndex = 5;
                        tn.SelectedImageIndex = 5;
                        break;
                    default:
                        tn.ImageIndex = 7;
                        tn.SelectedImageIndex = 7;
                        break;
                }
                bool canExpand;

                try
                {
                    canExpand = drive.RootDirectory.GetDirectories().Any();
                }
                catch (Exception)
                {
                    canExpand = false;
                }

                if (canExpand)
                {
                    var placeholder = new TreeNode("<<Placeholder>>");
                    tn.Nodes.Add(placeholder);
                }
                FileTree.Nodes.Add(tn);
                folderNames.Add((string)tn.Tag);
            }

            //add Bread crumbs
            BreadCrumbs = new BreadCrumbControl { Parent = BreadCrumbPanel, Dock = DockStyle.Fill };
            BreadCrumbs.Navigate += NavigateToFolder;
        }

        private void IDPOpenDialog_Load(object sender, EventArgs e)
        {
            //if FileType is not defined try to get type from the registry
            if (FileType == null)
                FileType += x =>
                                {
                                    try
                                    {
                                        var ext = (new FileInfo(x)).Extension;
                                        var key = Registry.ClassesRoot.OpenSubKey(ext);
                                        var type = key.GetValue("") as string;
                                        key = Registry.ClassesRoot.OpenSubKey(type);
                                        var desc = key.GetValue("") as string;
                                        return (desc);
                                    }
                                    catch { return "Unknown"; }
                                };

            //if FolderType is not defined assume every folder is actually a folder
            if (FolderType == null)
                FolderType += x => "File Folder";

            if (string.IsNullOrEmpty(_startLocation)) return;

            if (File.Exists(_startLocation))
            {
                //navigate to parent of passed file
                var currentlevel = Path.GetFileName(_startLocation) ?? string.Empty;
                _startLocation = _startLocation.Remove(_startLocation.Length - currentlevel.Length).TrimEnd('\\');
            }
            if (Directory.Exists(_startLocation))
                NavigateToFolder(_startLocation, null);


        }

     #endregion

        public List<string> GetFileNames()
        {
            return GetFileNamesRecursively(FileTreeView.Nodes[0]);
        }

        private List<string> GetFileNamesRecursively(TreeNode currentNode)
        {
            var fileList = new List<String>();
            foreach (TreeNode child in currentNode.Nodes)
            {
                if ((string)child.Tag == "Folder")
                    fileList.AddRange(GetFileNamesRecursively(child));
                else if ((string)child.Tag == "Source")
                    fileList.AddRange(from TreeNode item in child.Nodes
                                      where item.Checked
                                      select item.ToolTipText);
            }

            return fileList;
        }

        public TreeNode GetTreeStructure()
        {
            return TrimmedTree(FileTreeView.Nodes[0]);
        }

        private TreeNode TrimmedTree(TreeNode currentNode)
        {
            var newNode = new TreeNode
                              {
                                  Text = currentNode.Text,
                                  ToolTipText = currentNode.ToolTipText,
                                  Tag = "Folder",
                                  Checked = true
                              };
            foreach (TreeNode child in currentNode.Nodes)
            {
                if ((string)child.Tag == "Folder")
                {
                    var newChildNode = TrimmedTree(child);
                    if (newChildNode != null)
                        newNode.Nodes.Add(newChildNode);
                }
                else if ((string)child.Tag == "Source")
                    if (child.Nodes.Cast<TreeNode>()
                        .Any(granchild => granchild.Checked))
                        newNode.Nodes.Add(new TreeNode
                                              {
                                                  Text = child.Text,
                                                  ToolTipText = child.ToolTipText,
                                                  Tag = child.Tag,
                                                  Checked = true
                                              });
            }
            return newNode.Nodes.Count > 0 ? newNode : null;
        }

        private TreeNode GetSpecificNode(TreeNode parent, string target)
        {
            foreach (TreeNode child in parent.Nodes)
            {
                if (child.ToolTipText.ToLower() == target.ToLower())
                    return child;
                if ((string)child.Tag == "File") continue;
                var result = GetSpecificNode(child, target);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void CorrectNamingScheme(TreeNode node, string prefix)
        {
            if (node.Parent == null)
            {
                node.Text = "/";
                foreach (TreeNode child in node.Nodes)
                {
                    if ((string)child.Tag == "Folder")
                        CorrectNamingScheme(child,"/");
                }
            }
            else
            {
                var cousins = from TreeNode cousin in node.Parent.Nodes where cousin != node select cousin.Text;
                var di = new DirectoryInfo(node.ToolTipText);
                var newName = prefix + (prefix == "/" ? string.Empty:"/") + di.Name;
                if (cousins.Contains(newName))
                {
                    for (var x = 1;x < int.MaxValue;x++)
                    {
                        var newName1 = newName;
                        var repeats = (from string name in cousins where name == newName1 select name).ToList();
                        if (!repeats.Any()) continue;
                        newName = newName + x;
                        break;
                    }
                }
                node.Text = newName;
                foreach (TreeNode child in node.Nodes)
                {
                    if ((string)child.Tag == "Folder")
                        CorrectNamingScheme(child, newName);
                }
            }
        }

        private void NavigateToFolder(object folder, EventArgs e)
        {
            var folderInfo = (string) folder;

            //check if refresh requested
            if (folderInfo.EndsWith(@":"))
            {
                FileTree.CollapseAll();
                folderInfo = folderInfo.TrimEnd(':');
                if (_spectraFolders.ContainsKey(folderInfo))
                    _spectraFolders.Remove((folderInfo));
            }
            //process the info
            var dirList = BreadCrumbControl.PathToDirectoryList(folderInfo);
            TreeNode currentNode = null;
            if (folderInfo.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)))
            {
                var desktopList =
                    BreadCrumbControl.PathToDirectoryList(
                        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
                for (var x = 0; x < desktopList.Count - 1; x++)
                    dirList.RemoveAt(0);
                foreach (TreeNode node in FileTree.Nodes)
                    if (node.Text == "Desktop")
                        currentNode = node;
                    else
                        node.Collapse();
            }
            else if (folderInfo.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)))
            {
                var documentsList =
                    BreadCrumbControl.PathToDirectoryList(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                for (var x = 0; x < documentsList.Count - 1; x++)
                    dirList.RemoveAt(0);
                foreach (TreeNode node in FileTree.Nodes)
                    if (node.Text == "My Documents")
                        currentNode = node;
                    else
                        node.Collapse();
            }
            else
            {
                foreach (TreeNode node in FileTree.Nodes)
                {
                    if (node.Text.ToLower() == dirList[0].ToLower())
                    {
                        currentNode = node;
                        currentNode.Expand();
                    }
                    else
                        node.Collapse();
                }
            }

            //if you cant find the root you cant go anywhere
            if (currentNode == null)
                return;
            dirList.RemoveAt(0);
            foreach (var item in dirList)
            {
                var found = false;
                if (!currentNode.IsExpanded)
                    currentNode.Expand();
                foreach (TreeNode node in currentNode.Nodes)
                {
                    if (node.Text.ToLower() == item.ToLower())
                    {
                        found = true;
                        currentNode = node;
                    }
                    else
                        node.Collapse();
                }
                if (!found)
                    break;
            }
            SelectAndPreviewNode(currentNode);
        }

        private void SelectAndPreviewNode(TreeNode mainNode)
        {
            //make sure folder is visible in tree and has corresponding neighbor entry
            if (mainNode.Nodes.Count > 0)
            {
                mainNode.Expand();
                foreach (TreeNode node in mainNode.Nodes)
                    node.Collapse();
            }
            FileTree.SelectedNode = mainNode;

            var neighborList = new Dictionary<string, IEnumerable<string>>();
            var tempNode = mainNode;

            //create neighbor list
            while (tempNode.Parent != null)
            {
                var fullName = (string) tempNode.Tag;
                var tempList = (from TreeNode item in tempNode.Parent.Nodes select (string) item.Tag);
                neighborList.Add(fullName, tempList);
                tempNode = tempNode.Parent;
            }
            {
                var tempList = (from TreeNode item in FileTree.Nodes select (string) item.Tag);
                neighborList.Add((string)tempNode.Tag, tempList);
            }

            BreadCrumbs.NavigateToFolder((string)mainNode.Tag, neighborList);

            //update history
            if (_historyQueue.Count >= 10)
            {
                _historyQueue.RemoveAt(0);
                _placeInQueue--;
            }

            if (!_navigatingHistory && 
                (!_historyQueue.Any() || (string)mainNode.Tag != _historyQueue.Last()))
            {
                for (var x = _historyQueue.Count - 1; x > _placeInQueue; x--)
                    _historyQueue.RemoveAt(x);
                _historyQueue.Add((string) mainNode.Tag);
                _placeInQueue = _historyQueue.Count-1;
            }
            UpdateHistoryButtons();

            //apply filters
            ApplyFilters();

        }

        private void sourceTypeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplyFilters();
        }

     #region FileTree Events

        private void FileTree_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            e.Node.Nodes.Clear();
            var fullName = (string) e.Node.Tag;
            var nonFolderContents = new List<string[]>();
            var foldersAlreadyFound = _spectraFolders.ContainsKey((string)e.Node.Tag);
            var nodeList = new List<TreeNode>();

            if (BreadCrumbControl.PathToDirectoryList(fullName).Count == 1)
            {
                var di = new DriveInfo(fullName);
                foreach (var folder in di.RootDirectory.GetDirectories())
                {
                    var newFolder = new TreeNode(folder.Name) {ImageIndex = 8, SelectedImageIndex = 8, Tag = folder.FullName};
                    try
                    {
                        if (folder.GetDirectories().Any())
                        newFolder.Nodes.Add("<<Placeholder>>");
                    }
                    catch
                    {
                        //cannot access folder
                    }

                    if (foldersAlreadyFound)
                    {
                        var info = folder;
                        if (!_spectraFolders[(string) e.Node.Tag].Any(item => item[0] == info.FullName))
                            nodeList.Add(newFolder);
                    }
                    else
                    {
                        var sourceType = FolderType(folder.FullName);
                        if (sourceType == "File Folder")
                            nodeList.Add(newFolder);
                        else if (!string.IsNullOrEmpty(sourceType))
                            nonFolderContents.Add(new[] {folder.FullName, sourceType});
                    }
                }

                var sortedList = nodeList.OrderBy(x => x.Text);

                foreach (var item in sortedList)
                    e.Node.Nodes.Add(item);
            }
            else
            {
                var di = new DirectoryInfo(fullName);
                foreach (var folder in di.GetDirectories())
                {
                    var newFolder = new TreeNode(folder.Name) { ImageIndex = 8, SelectedImageIndex = 8, Name = folder.FullName, Tag = folder.FullName };
                    try
                    {
                        if (folder.GetDirectories().Any())
                            newFolder.Nodes.Add("<<Placeholder>>");
                    }
                    catch
                    {
                        //cannot access folder
                    }

                    if (foldersAlreadyFound)
                    {
                        var info = folder;
                        if (!_spectraFolders[(string)e.Node.Tag].Any(item => item[0] == info.FullName))
                            nodeList.Add(newFolder);
                    }
                    else
                    {
                        var sourceType = FolderType(folder.FullName);
                        if (sourceType == "File Folder")
                            nodeList.Add(newFolder);
                        else if (!string.IsNullOrEmpty(sourceType))
                            nonFolderContents.Add(new[] {folder.FullName, sourceType});
                    }
                }

                var sortedList = nodeList.OrderBy(x => x.Text);

                foreach (var item in sortedList)
                    e.Node.Nodes.Add(item);
            }

            if (!foldersAlreadyFound)
                _spectraFolders.Add((string)e.Node.Tag, nonFolderContents);
        }

        private void FileTree_AfterCollapse(object sender, TreeViewEventArgs e)
        {
            e.Node.Nodes.Clear();
            e.Node.Nodes.Add("<<Placeholder>>");
        }

     #endregion

     #region History Button Events

        private void BackPicture_MouseEnter(object sender, EventArgs e)
        {
            if ((string)BackPicture.Tag == "Active")
                BackPicture.Image = Properties.Resources.Back_Lit;
        }

        private void BackPicture_MouseLeave(object sender, EventArgs e)
        {
            if ((string)BackPicture.Tag == "Active")
                BackPicture.Image = Properties.Resources.Back_Active;
        }

        private void ForwardPicture_MouseEnter(object sender, EventArgs e)
        {
            if ((string)ForwardPicture.Tag == "Active")
                ForwardPicture.Image = Properties.Resources.Forward_Lit;
        }

        private void ForwardPicture_MouseLeave(object sender, EventArgs e)
        {
            if ((string)ForwardPicture.Tag == "Active")
                ForwardPicture.Image = Properties.Resources.Forward_Active;
        }

        private void ArrowPicture_MouseEnter(object sender, EventArgs e)
        {
            if (_historyQueue.Any())
                ArrowPicture.Image = Properties.Resources.Arrow_Lit;
        }

        private void ArrowPicture_MouseLeave(object sender, EventArgs e)
        {
            if (_historyQueue.Any())
                ArrowPicture.Image = Properties.Resources.Arrow_Active;
        }

        private void UpdateHistoryButtons()
        {
            if (((ContextMenu)ArrowPicture.Tag).MenuItems.Count == 0
                && _historyQueue.Any())
            {
                ArrowPicture.Image = Properties.Resources.Arrow_Active;
            }

            if (_placeInQueue == _historyQueue.Count - 1)
            {
                ForwardPicture.Image = Properties.Resources.Forward_Faded;
                ForwardPicture.Tag = "Faded";
            }
            else if ((string)ForwardPicture.Tag == "Faded")
            {
                ForwardPicture.Image = Properties.Resources.Forward_Active;
                ForwardPicture.Tag = "Active";
            }

            if (_placeInQueue < 1)
            {
                BackPicture.Image = Properties.Resources.Back_Faded;
                BackPicture.Tag = "Faded";
            }
            else if ((string)BackPicture.Tag == "Faded")
            {
                BackPicture.Image = Properties.Resources.Back_Active;
                BackPicture.Tag = "Active";
            }
        }

        private void BackPicture_Click(object sender, EventArgs e)
        {
            if (_placeInQueue > 0)
            {
                _placeInQueue--;
                _navigatingHistory = true;
                NavigateToFolder(_historyQueue[_placeInQueue],null);
                _navigatingHistory = false;
            }
        }

        private void ForwardPicture_Click(object sender, EventArgs e)
        {
            if (_placeInQueue < _historyQueue.Count -1)
            {
                _placeInQueue++;
                _navigatingHistory = true;
                NavigateToFolder(_historyQueue[_placeInQueue], null);
                _navigatingHistory = false;
            }
        }

        private void ArrowPicture_Click(object sender, EventArgs e)
        {
            var menu = (ContextMenu)ArrowPicture.Tag;

            menu.MenuItems.Clear();
            for (var x = 0; x < _historyQueue.Count; x++)
            {
                var name = Path.GetFileName(_historyQueue[x]);
                if (name == string.Empty)
                    name = _historyQueue[x];
                var mi = new MenuItem(name) {Tag = _historyQueue[x]};
                if (x == _placeInQueue)
                    mi.Checked = true;
                else
                    mi.Click += rootMenu_Click;
                menu.MenuItems.Add(mi);
            }
            menu.Show((Control)sender,new Point(0,30));
        }

        private void rootMenu_Click(object sender, EventArgs e)
        {
            var mi = (MenuItem) sender;
            _placeInQueue = mi.Parent.MenuItems.IndexOf(mi);
            _navigatingHistory = true;
            NavigateToFolder(mi.Tag as string, null);
            _navigatingHistory = false;
        }

     #endregion

     #region Search Functions

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            if (SearchBox.ForeColor != SystemColors.GrayText)
                ApplyFilters();
        }

        private void SearchBox_Enter(object sender, EventArgs e)
        {
            if (SearchBox.ForeColor == SystemColors.GrayText)
            {
                SearchBox.Text = string.Empty;
                SearchBox.ForeColor = SystemColors.WindowText;
            }
        }

        private void SearchBox_Leave(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchBox.ForeColor = SystemColors.GrayText;
                SearchBox.Text = "Search";
            }
        }
        
        private void SearchBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\\' || e.KeyChar == '/' ||
                e.KeyChar == ':' || e.KeyChar == '"' ||
                e.KeyChar == '|')
                e.Handled = true;
            else if (e.KeyChar == (char)Keys.Return)
            {
                ApplyFilters();
                e.Handled = true;
            }
        }

     #endregion

        private void ApplyFilters()
        {
            if (FileTreeView.Nodes.Count < 1)
                return;

            Regex regexConversion;
            if (SearchBox.Text == string.Empty || SearchBox.ForeColor == SystemColors.GrayText)
                regexConversion = new Regex(string.Empty);
            else
                regexConversion = new Regex("^" + SearchBox.Text.ToLower().Replace("+", "\\+")
                                                      .Replace(".", "\\.").Replace('?', '.').Replace("*", ".*") + "$");
            var errorNode = (from TreeNode node in FileTreeView.Nodes
                             where node.Name == "IDPDBErrorNode"
                             select node).SingleOrDefault();
            FileTreeView.Nodes.Clear();
            FileTreeView.Nodes.Add(FilterNode(_unfilteredNode, regexConversion));
            if (errorNode != null)
                FileTreeView.Nodes.Add(errorNode);
            CorrectNamingScheme(FileTreeView.Nodes[0],string.Empty);
            FileTreeView.ExpandAll();
        }

        private TreeNode FilterNode(TreeNode treeNode, Regex regexConversion)
        {
            var newNode = new TreeNode
                              {
                                  Text = treeNode.Text,
                                  Tag = treeNode.Tag,
                                  ToolTipText = treeNode.ToolTipText,
                                  Checked = true
                              };
            foreach (var sourceNode in treeNode.Nodes.Cast<TreeNode>().Where(node => (string)node.Tag == "Source"))
            {
                var newSourceNode = new TreeNode
                                        {
                                            Text = sourceNode.Text,
                                            Tag = sourceNode.Tag,
                                            ToolTipText = sourceNode.ToolTipText,
                                            Checked = true,
                                            ImageIndex = 10,
                                            SelectedImageIndex = 10
                                        };
                foreach (var fileNode in sourceNode.Nodes.Cast<TreeNode>().Where(node => (string) node.Tag == "File"))
                {
                    if (!File.Exists(fileNode.ToolTipText)
                        || !_extensionList.ContainsKey(sourceTypeComboBox.Text))
                        continue;

                    var info = new FileInfo(fileNode.ToolTipText);

                    if (!_extensionList[sourceTypeComboBox.Text].Contains(".*")
                        && !_extensionList[sourceTypeComboBox.Text].Contains(info.Extension))
                        continue;

                    if (regexConversion.IsMatch(fileNode.Text.ToLower()))
                        newSourceNode.Nodes.Add(fileNode);
                }
                if (newSourceNode.Nodes.Count > 0)
                    newNode.Nodes.Add(newSourceNode);
            }

            foreach (var node in treeNode.Nodes.Cast<TreeNode>().Where(node => (string)node.Tag == "Folder"))
            {
                var possibleChild = FilterNode(node, regexConversion);
                if (possibleChild != null)
                    newNode.Nodes.Add(possibleChild);
            }

            if ((string)treeNode.Tag != "Root" && treeNode.Parent != null 
                && (string)treeNode.Parent.Tag != "Root")
            {
                if (newNode.Nodes.Count == 0)
                    return null;
                if (newNode.Nodes.Count == 1 && (string) newNode.Nodes[0].Tag == "Folder")
                    return newNode.Nodes[0];
            }
            return newNode;
        }

        private void OpenDataSourceDialogue_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK && DataSources.Any())
            {
                foreach (var item in DataSources)
                FolderHistoryInterface.AddFolderToHistory(item);
            }
        }

        private void BreadCrumbPanel_Resize(object sender, EventArgs e)
        {
            if (BreadCrumbs != null)
                BreadCrumbs.CheckBreadcrumbSize();
        }

        private void AddNode_Click(object sender, EventArgs e)
        {
            var newNode = CreateNewNode((string)FileTree.SelectedNode.Tag, true);
            if (newNode == null)
                return;

            //remove filters
            var errorNode = (from TreeNode node in FileTreeView.Nodes
                             where node.Name == "IDPDBErrorNode"
                             select node).SingleOrDefault();
            FileTreeView.Nodes.Clear();
            FileTreeView.Nodes.Add(_unfilteredNode);
            if (errorNode != null)
                FileTreeView.Nodes.Add(errorNode);

            //If folder is a sub-folder find location
            var closestRelative = FindClosestRelative(FileTreeView.Nodes[0], newNode.ToolTipText);
            if (closestRelative != null)
            {
                //If folder is a super-folder move subs
                for (var x = closestRelative.Nodes.Count-1; x >= 0; x--)
                {
                    var pathList = BreadCrumbControl.PathToDirectoryList(closestRelative.Nodes[x].ToolTipText);
                    var buildingString = string.Empty;
                    var possibleRelatives = new List<string>();
                    foreach (var item in pathList)
                    {
                        buildingString = Path.Combine(buildingString, item);
                        if (item != closestRelative.Nodes[x].ToolTipText)
                            possibleRelatives.Add(buildingString);
                    }

                    if (!possibleRelatives.Contains(newNode.ToolTipText) ||
                        closestRelative.Nodes[x].ToolTipText == newNode.ToolTipText) continue;

                    var nodeToMove = closestRelative.Nodes[x];
                    closestRelative.Nodes.Remove(nodeToMove);
                    var move = nodeToMove;
                    if (newNode.Nodes.Cast<TreeNode>().Any(node => node.ToolTipText == move.ToolTipText))
                        continue;
                    newNode.Nodes.Add(nodeToMove);
                }

                closestRelative.Nodes.Add(newNode);
            }

            //save results
            _unfilteredNode = FileTreeView.Nodes[0];
            SelectAndPreviewNode(FileTree.SelectedNode);
            if (!DataSources.Contains((string)FileTree.SelectedNode.Tag))
                DataSources.Add((string) FileTree.SelectedNode.Tag);
            ApplyFilters();

        }

        private TreeNode CreateNewNode(string folderPath, bool firstLayer)
        {
            var newNode = new TreeNode
            {
                Text = "Temp",
                ToolTipText = folderPath,
                Tag = "Folder",
                Checked = true
            };

            if (!Directory.Exists(folderPath))
                return null;

            var di = new DirectoryInfo(folderPath);
            
            //if searching subtrees create sub-folders
            if (SubfolderBox.Checked)
            {
                if (firstLayer)
                    VertSplit.Enabled = false;
                foreach (var path in di.GetDirectories())
                {
                    TreeNode subNode;
                    try
                    {
                        subNode = CreateNewNode(path.FullName, false);
                        if (subNode == null)
                            continue;
                    }
                    catch (Exception)
                    {
                        //looks like couldnt access the folder there
                        continue;
                    }
                    newNode.Nodes.Add(subNode);
                }
                if (firstLayer)
                    VertSplit.Enabled = true;
            }
            foreach (var file in di.GetFiles())
            {
                try
                {
                    //have to use slightly odd method of checking extension since
                    //some extensions are multi-dotted. Allow for all possibilites
                    var destructableName = file.Name;
                    if (!_allExtensions.Contains(file.Extension))
                    {
                        while (destructableName.Length > 0 &&
                            !_allExtensions.Contains(destructableName))
                            destructableName = destructableName.Remove(0, 1);
                        if (destructableName.Length == 0)
                            continue;
                    }

                    HandleItemToAdd(ref newNode, file);
                }
                catch (Exception e)
                {
                    throw new Exception("Failed to add file \"" + file.Name + "\"", e);
                }
            }
            foreach (var source in newNode.Nodes.Cast<TreeNode>()
                     .Where(node => (string)node.Tag == "Source"))
            {
                foreach (TreeNode file in source.Nodes)
                {
					if (!file.Text.ToLower().EndsWith(".idpdb"))
                    {
                        var baseName = Path.GetFileNameWithoutExtension(file.Text);
                        var matchFound = source.Nodes.Cast<TreeNode>()
                            .Where(idpDB => idpDB.Text.ToLower().EndsWith(".idpdb"))
                            .Any(idpDB => Path.GetFileNameWithoutExtension(idpDB.Text) == baseName);
                        if (matchFound)
                        {
                            file.ForeColor = SystemColors.GrayText;
                            file.Checked = false;
                        }
                    }
                }
            }

            return newNode.Nodes.Count == 0 ? null : newNode;
        }

        private void HandleItemToAdd(ref TreeNode newNode, FileInfo file)
        {
            List<string> sourceList;
            var rootNode = newNode;
            while (rootNode.Parent != null)
                rootNode = rootNode.Parent;

            try
            {
                sourceList = GetSources(file);
            }
            catch (Exception e)
            {
                var errorNode = (from TreeNode node in FileTreeView.Nodes
                                 where node.Name == "IDPDBErrorNode"
                                 select node).SingleOrDefault();
                if (errorNode == null)
                {
                    errorNode = new TreeNode
                    {
                        Name = "IDPDBErrorNode",
                        Text = "Error",
                        Checked = false,
                        ForeColor = Color.DarkRed,
                        Tag = "Uncheckable"
                    };
                    FileTreeView.Nodes.Add(errorNode);
                }
                var thisError = new TreeNode
                {
                    Text = file.FullName,
                    ToolTipText = e.Message,
                    ForeColor = Color.DarkRed,
                    Tag = "Uncheckable"
                };
                errorNode.Nodes.Add(thisError);
                return;
            }


            var mergedFile = sourceList.Count > 1;
            var relatedNodes = new List<TreeNode>();

            foreach (var item in sourceList)
            {
                var target = GetSpecificNode(rootNode, item);
                var subNode = new TreeNode
                {
                    Text = file.FullName,
                    ToolTipText = file.FullName,
                    Tag = "File",
                    Checked = true,
                    ImageIndex = 9,
                    SelectedImageIndex = 9
                };
                if (mergedFile)
                {
                    subNode.ForeColor = SystemColors.InactiveCaption;
                    relatedNodes.Add(subNode);
                }

                if (target == null)
                {
                    var spectraNode = new TreeNode
                                          {
                                              Text = item,
                                              ToolTipText = item,
                                              Tag = "Source",
                                              Checked = true,
                                              ImageIndex = 10,
                                              SelectedImageIndex = 10
                                          };
                    spectraNode.Nodes.Add(subNode);
                    newNode.Nodes.Add(spectraNode);
                }
                else
                    target.Nodes.Add(subNode);
            }
            foreach (var node in relatedNodes)
            {
                _relatedNodes.Add(node, new List<TreeNode>());
                foreach (var neighbor in relatedNodes)
                    if (neighbor != node)
                        _relatedNodes[node].Add(neighbor);
                node.Checked = false;
            }
        }

        public static List<string> GetSources(FileInfo file)
        {
            var sourceList = new List<string>();
            if (file.Extension.ToLower() != ".idpdb")
                sourceList.Add(Parser.ParseSource(file.FullName));//Path.GetFileNameWithoutExtension(file.FullName));
            else if (SessionFactoryFactory.IsValidFile(file.FullName))
            {
                NHibernate.ISessionFactory sessionFactory = null;
                NHibernate.ISession session = null;
                try
                {
                    sessionFactory = SessionFactoryFactory.CreateSessionFactory(file.FullName, false, true);
                    session = sessionFactory.OpenSession();
                    var temp = session.QueryOver<SpectrumSource>().List();
                    sourceList.AddRange(temp.Select(item => item.Name));
                    session.Close();
                    session.Dispose();
                    sessionFactory.Close();
                    sessionFactory.Dispose();
                }
                catch
                {
                    if (session != null)
                    {
                        session.Close();
                        session.Dispose();
                    }
                    if (sessionFactory != null)
                    {
                        sessionFactory.Close();
                        sessionFactory.Dispose();
                    }
                    throw;
                }
            }

            return sourceList;
        }

        private TreeNode FindClosestRelative(TreeNode currentNode, string newPath)
        {
            var closestMatch = currentNode;

            var pathList = BreadCrumbControl.PathToDirectoryList(newPath);
            var buildingString = string.Empty;
            var possibleRelatives = new List<string>();
            foreach (var item in pathList)
            {
                buildingString = Path.Combine(buildingString,item);
                if (item != newPath)
                    possibleRelatives.Add(buildingString);
            }

            foreach (TreeNode node in currentNode.Nodes)
                if (possibleRelatives.Contains(node.ToolTipText))
                {
                    closestMatch = node.ToolTipText == newPath
                                       ? null
                                       : FindClosestRelative(node, newPath);
                    break;
                }
            return closestMatch;
        }

        private void FileTreeView_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Checked)
            {
                if (_relatedNodes.ContainsKey(e.Node))
                {
                    foreach (var neighbor in _relatedNodes[e.Node])
                        if (!neighbor.Checked)
                            neighbor.Checked = true;
                }
                else
                    foreach (TreeNode child in e.Node.Nodes)
                        if (child.ForeColor != SystemColors.GrayText
                            && child.ForeColor != SystemColors.InactiveCaption)
                            child.Checked = true;
            }
            else
            {
                if (_relatedNodes.ContainsKey(e.Node))
                {
                    foreach (var neighbor in _relatedNodes[e.Node])
                        if (neighbor.Checked)
                            neighbor.Checked = false;
                }
                foreach (TreeNode child in e.Node.Nodes)
                    child.Checked = false;
            }
        }

        private void RemoveNode_Click(object sender, EventArgs e)
        {
            if (FileTreeView.SelectedNode == null)
                return;
            if ((string)FileTreeView.SelectedNode.Tag == "Uncheckable")
            {
                FileTreeView.SelectedNode.Remove();
                return;
            }

            if (DataSources.Contains(FileTreeView.SelectedNode.ToolTipText))
                DataSources.Remove(FileTreeView.SelectedNode.ToolTipText);

            if ((string)FileTreeView.SelectedNode.Tag == "Root")
            {
                FileTreeView.SelectedNode.Nodes.Clear();
                _unfilteredNode = FileTreeView.SelectedNode;
                FileTree.CollapseAll();
            }
            else
            {
                var path = FileTreeView.SelectedNode.ToolTipText;
                var nodeToRemove = GetSpecificNode(_unfilteredNode, path);
                nodeToRemove.Remove();
                if (Directory.Exists(path))
                    NavigateToFolder(path, null);
                ApplyFilters();
            }
        }

        private void FileTree_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                AddNode_Click(sender,null);
            }
        }

        private void FileTreeView_BeforeCheck(object sender, TreeViewCancelEventArgs e)
        {
            if ((string)e.Node.Tag == "Uncheckable")
                e.Cancel = true;
        }
    }
}
