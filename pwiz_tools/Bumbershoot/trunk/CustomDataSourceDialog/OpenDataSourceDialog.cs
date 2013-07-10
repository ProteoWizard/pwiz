//
// $Id: OpenDataSourceDialog.cs 55 2011-04-28 15:57:33Z chambm $
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CustomDataSourceDialog
{
    public partial class OpenDataSourceDialog : Form
    {
        private BreadCrumbControl BreadCrumbs;
        private List<string> _historyQueue;
        private List<ListViewItem> _unfilteredItems;
        private Dictionary<string, List<string[]>> _spectraFolders;
        private Dictionary<string, List<string[]>> _spectraFiles;
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

        public OpenDataSourceDialog(IEnumerable<string> fileTypes)
        {
            InitializeComponent();
            foreach (var item in fileTypes)
                sourceTypeComboBox.Items.Add(item);
            SetUpDialog();
            var recent = FolderHistoryInterface.GetRecentFolders().Where(x=>!string.IsNullOrEmpty(x)).ToList();
            _startLocation = recent.Any()
                                 ? recent.FirstOrDefault()
                                 : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        public OpenDataSourceDialog(IEnumerable<string> fileTypes, string initialLocation)
        {
            InitializeComponent();
            foreach (var item in fileTypes)
                sourceTypeComboBox.Items.Add(item);
            SetUpDialog();

            _startLocation = initialLocation;
        }

        private void SetUpDialog()
        {
            _historyQueue = new List<string>();
            ArrowPicture.Tag = new ContextMenu();
            _unfilteredItems = new List<ListViewItem>();
            _spectraFolders = new Dictionary<string, List<string[]>>();
            _spectraFiles = new Dictionary<string, List<string[]>>();
            DataSources = new List<string>();
            sourceTypeComboBox.SelectedIndex = 0;
            
            var folderNames = new List<string>();

            //Desktop
            var specialLocation = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            var node = new TreeNode("Desktop")
                           {
                               Tag = specialLocation.FullName,
                               ImageIndex = 1
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
                           ImageIndex = 2
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
                        break;
                    case DriveType.Fixed:
                        tn.ImageIndex = 5;
                        break;
                    default:
                        tn.ImageIndex = 7;
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

        private void OpenDataSourceDialogue_Load(object sender, EventArgs e)
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

            //navigate to parent of passed file
            if (!Directory.Exists(_startLocation) && File.Exists(_startLocation))
            {
                var currentlevel = Path.GetFileName(_startLocation) ?? string.Empty;
                _startLocation = _startLocation.Remove(_startLocation.Length - currentlevel.Length).TrimEnd('\\');
            }
            if (Directory.Exists(_startLocation))
                NavigateToFolder(_startLocation, null);
        }

     #endregion

        private void NavigateToFolder(object folder, EventArgs e)
        {
            string folderInfo = folder as string;

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
            foreach (TreeNode node in FileTree.Nodes)
            {
                if (String.Equals(node.Text, dirList[0], StringComparison.InvariantCultureIgnoreCase))
                {
                    currentNode = node;
                    currentNode.Expand();
                }
                else
                    node.Collapse();
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


            #region Show item in folder view

            FolderViewList.Items.Clear();

            //regular folders
            _unfilteredItems = new List<ListViewItem>();
            foreach (TreeNode node in mainNode.Nodes)
            {
                var lvi = new ListViewItem {Text = node.Text, ImageIndex = 8, Tag = node.Tag};
                lvi.SubItems.Add(string.Empty);
                lvi.SubItems.Add("File Folder");
                FolderViewList.Items.Add(lvi);
                _unfilteredItems.Add(lvi);
            }

            //Spectra Folders
            if (_spectraFolders.ContainsKey((string)mainNode.Tag))
            {
                foreach (var item in _spectraFolders[(string)mainNode.Tag])
                {
                    var lvi = new ListViewItem {Text = Path.GetFileName(item[0]), ImageIndex = 9, Tag = item[0]};
                    lvi.SubItems.Add(string.Empty);
                    lvi.SubItems.Add(item[1]);
                    FolderViewList.Items.Add(lvi);
                    _unfilteredItems.Add(lvi);
                }
            }

            FolderViewList.Sort();

            //Spectra Files
            if (_spectraFiles.ContainsKey((string)mainNode.Tag))
            {
                foreach (var item in _spectraFolders[(string)mainNode.Tag])
                {
                    var lvi = new ListViewItem {Text = Path.GetFileName(item[0]), ImageIndex = 10, Tag = item[0]};
                    lvi.SubItems.Add(string.Empty);
                    lvi.SubItems.Add(item[1]);
                    FolderViewList.Items.Add(lvi);
                    _unfilteredItems.Add(lvi);
                }
            }
            else
            {
                var fileList = new List<string[]>();
                var di = new DirectoryInfo((string) mainNode.Tag);
                try
                {
                    foreach (var item in di.GetFiles())
                    {
                        var type = FileType(item.FullName);
                        if (!string.IsNullOrEmpty(type))
                        {
                            fileList.Add(new[] {item.FullName, type});
                            var lvi = new ListViewItem {Text = item.Name, ImageIndex = 10, Tag = item.FullName};
                            lvi.SubItems.Add(FileInfoLengthToString(item.Length));
                            lvi.SubItems.Add(type);
                            lvi.SubItems.Add(item.LastWriteTime.ToString());
                            FolderViewList.Items.Add(lvi);
                            _unfilteredItems.Add(lvi);
                        }
                    }
                }
                catch(IOException)
                {
                    //device cant be read at the moment if this is triggered
                }
            }

            #endregion

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
            ApplyFilters(false);

        }

        private static string FileInfoLengthToString(long length)
        {
            //bytes
            if (length > 1000)
                length = length/1000;
            else
                return "1 KB";

            //kilobytes
            if (length > 10000)
                length = length / 1000;
            else
                return String.Format("{0:0,0} KB", length);

            //megabytes
            if (length > 10000)
                length = length / 1000;
            else
                return String.Format("{0:0,0} MB", length);

            //gigabytes
            return String.Format("{0:0,0} GB", length);
        }

        private void sourceTypeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplyFilters(false);
        }

     #region FolderViewList Events

        private void FolderViewList_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var listItem = FolderViewList.GetItemAt(e.Location.X, e.Location.Y);
            if (listItem.SubItems.Count > 2 && listItem.SubItems[2] != null
                && listItem.SubItems[2].Text != "File Folder")
            {
                DialogResult = DialogResult.OK;
            }
            else
                NavigateToFolder(listItem.Tag, null);
        }

        private void FolderViewList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (FolderViewList.SelectedItems.Count == 0)
                return;

            DataSources = new List<string>();
            sourcePathTextBox.Text = string.Empty;

            foreach (ListViewItem item in FolderViewList.SelectedItems)
            {
                if (item.SubItems.Count > 2 && item.SubItems[2] != null
                && item.SubItems[2].Text != "File Folder")
                {
                    sourcePathTextBox.Text += item.Text + ";  ";
                    DataSources.Add((string)item.Tag);
                }
            }
            sourcePathTextBox.Text = sourcePathTextBox.Text.Trim().TrimEnd(';');
        }

        #endregion

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
                    var newFolder = new TreeNode(folder.Name) {ImageIndex = 8, Tag = folder.FullName};
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
                    var newFolder = new TreeNode(folder.Name) { ImageIndex = 8, Name = folder.FullName, Tag = folder.FullName };
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

        private void FileTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            FileTree.SelectedNode = null;
            if (e.Action != TreeViewAction.ByMouse)
                return;
            var node = e.Node;
            SelectAndPreviewNode(node);
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

     #region View events

        private void ViewControl_Click(object sender, EventArgs e)
        {
            var viewDropDown = new ContextMenuStrip();

            //List option
            var tsmi = new ToolStripMenuItem("List");
            tsmi.Click += (x, y) =>
            {
                FolderViewList.View = View.List;
                ViewControl.Tag = "List";
            };
            if ((string)ViewControl.Tag == "List")
                tsmi.CheckState = CheckState.Indeterminate;
            viewDropDown.Items.Add(tsmi);

            //Small Icons option
            tsmi = new ToolStripMenuItem("Small Icons");
            tsmi.Click += (x, y) =>
            {
                FolderViewList.View = View.SmallIcon;
                ViewControl.Tag = "Small Icons";
            };
            if ((string)ViewControl.Tag == "Small Icons")
                tsmi.CheckState = CheckState.Indeterminate;
            viewDropDown.Items.Add(tsmi);

            //Large Icons option
            tsmi = new ToolStripMenuItem("Large Icons");
            tsmi.Click += (x, y) =>
            {
                FolderViewList.View = View.LargeIcon;
                ViewControl.Tag = "Large Icons";
            };
            if ((string)ViewControl.Tag == "Large Icons")
                tsmi.CheckState = CheckState.Indeterminate;
            viewDropDown.Items.Add(tsmi);

            //Details option
            tsmi = new ToolStripMenuItem("Details");
            tsmi.Click += (x, y) =>
            {
                FolderViewList.View = View.Details;
                ViewControl.Tag = "Details";
            };
            if ((string)ViewControl.Tag == "Details")
                tsmi.CheckState = CheckState.Indeterminate;
            viewDropDown.Items.Add(tsmi);

            viewDropDown.Show((PictureBox)sender, 0, 21);
        }

        private void ViewControl_MouseEnter(object sender, EventArgs e)
        {
            EffectPanel.Visible = true;
        }

        private void ViewControl_MouseLeave(object sender, EventArgs e)
        {
            EffectPanel.Visible = false;
        }

     #endregion

     #region Search Functions

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            //currently this really takes a while

            //if (SearchBox.ForeColor != SystemColors.GrayText)
            //    ApplyFilters();
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
                ApplyFilters(true);
                e.Handled = true;
            }
        }

        private void SearchButton_MouseEnter(object sender, EventArgs e)
        {
            SearchButton.Image = Properties.Resources.SearchButtonLit;
        }

        private void SearchButton_MouseLeave(object sender, EventArgs e)
        {
            SearchButton.Image = Properties.Resources.SearchButton;
        }

        private void SearchButton_Click(object sender, EventArgs e)
        {
            ApplyFilters(true);
        }

     #endregion

        private void ApplyFilters(bool useSearch)
        {
            var regexConversion = new Regex("^" + SearchBox.Text.ToLower().Replace("+", "\\+")
                .Replace(".", "\\.").Replace('?', '.').Replace("*", ".*") + "$");
            var resultsList = new List<ListViewItem>();
            if(!useSearch || SearchBox.ForeColor == SystemColors.GrayText || string.IsNullOrEmpty(SearchBox.Text))
                resultsList = _unfilteredItems;
            else
            {
                foreach (var item in _unfilteredItems)
                {
                    if (item.SubItems[2] != null && item.SubItems[2].Text == "File Folder")
                    {
                        var folder = new DirectoryInfo((string)item.Tag);
                        try
                        {
                            if (folder.GetFiles(SearchBox.Text.Trim('*') + '*', SearchOption.AllDirectories).Any() ||
                                folder.GetDirectories(SearchBox.Text.Trim('*') + '*', SearchOption.AllDirectories).Any())
                                resultsList.Add(item);
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (regexConversion.IsMatch(item.Text.ToLower()))
                            resultsList.Add(item);
                    }
                }
            }

            if (sourceTypeComboBox.Text == "Any spectra format" || sourceTypeComboBox.Text == "All Files")
            {
                FolderViewList.Items.Clear();
                foreach (var item in resultsList)
                    FolderViewList.Items.Add(item);
            }
            else
            {
                FolderViewList.Items.Clear();
                foreach (var item in resultsList)
                {
                    if (item.SubItems.Count > 2 && item.SubItems[2] != null &&
                        (item.SubItems[2].Text == sourceTypeComboBox.Text ||
                         item.SubItems[2].Text == "File Folder"))
                        FolderViewList.Items.Add(item);
                }
            }
            sourcePathTextBox.Text = string.Empty;
        }

        private void OpenDataSourceDialogue_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK && DataSources.Any())
                FolderHistoryInterface.AddFolderToHistory((new FileInfo(DataSources[0])).DirectoryName);
        }

        private void BreadCrumbPanel_Resize(object sender, EventArgs e)
        {
            BreadCrumbs.CheckBreadcrumbSize();
        }

        private void sourcePathTextBox_PreviewKeyDown (object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                if (File.Exists(sourcePathTextBox.Text) && !String.IsNullOrEmpty(FileType(sourcePathTextBox.Text)) ||
                    (Directory.Exists(sourcePathTextBox.Text) && FolderType(sourcePathTextBox.Text) != "File Folder"))
                {
                    DataSources = new List<string>() { sourcePathTextBox.Text };
                    DialogResult = DialogResult.OK;
                }
                else if (Directory.Exists(sourcePathTextBox.Text))
                    NavigateToFolder(sourcePathTextBox.Text, EventArgs.Empty);
            }
        }

        private void FolderViewList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                foreach (ListViewItem item in FolderViewList.Items)
                    item.Selected = true;
            }
            else if (e.KeyCode == Keys.Return && FolderViewList.SelectedItems.Count > 0)
            {
                e.Handled = true;
                if ( FolderViewList.SelectedItems.Count == 1 &&
                    FolderViewList.SelectedItems[0].SubItems[2].Text == "File Folder" &&
                    Directory.Exists((string)FolderViewList.SelectedItems[0].Tag))
                    NavigateToFolder((string)FolderViewList.SelectedItems[0].Tag, null);
                else
                {
                    var allFiles = true;
                    foreach (ListViewItem item in FolderViewList.SelectedItems)
                        if (item.SubItems[2].Text == "File Folder")
                        {
                            allFiles = false;
                            break;
                        }
                    if (allFiles)
                        DialogResult = DialogResult.OK;
                    else
                        MessageBox.Show("Not all selected items are valid files.");
                }
            }
        }
    }
}
