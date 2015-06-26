//
// $Id: BreadCrumbControl.cs 55 2011-04-28 15:57:33Z chambm $
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
using System.Text;
using System.Windows.Forms;

namespace CustomDataSourceDialog
{
    public partial class BreadCrumbControl : UserControl
    {
        public EventHandler Navigate;
        private Stack<ToolStripMenuItem> _overflowStack;
        private Dictionary<ToolStripMenuItem, ToolStripMenuItem> _itemNeighbors;
        private ToolStripMenuItem _defaultRootMenu;

        public static List<string> PathToDirectoryList(string path)
        {
            var namestack = new List<string>();

            if (path == Path.GetPathRoot(path))
            {
                namestack.Add(path);
                return namestack;
            }

            while (true)
            {
                var currentlevel = Path.GetFileName(path) ?? string.Empty;
                path = path.Remove(path.Length - currentlevel.Length);
                namestack.Add(currentlevel);
                if (path == Path.GetPathRoot(path))
                {
                    namestack.Add(path);
                    break;
                }
                path = path.TrimEnd('\\');
            }
            namestack.Reverse();
            return namestack;
        }

        public BreadCrumbControl()
        {
            InitializeComponent();
            
            //remove unnecessary colors
            var professionalColorTable = new ProfessionalColorTable {UseSystemColors = true};
            BreadCrumbTrail.Renderer = new ToolStripProfessionalRenderer(professionalColorTable);
            RightToolStrip.Renderer = new ToolStripProfessionalRenderer(professionalColorTable);
            _overflowStack = new Stack<ToolStripMenuItem>();
        }

        void ClickItem(object sender, EventArgs e)
        {
            var tsmi = (ToolStripMenuItem)sender;
            Navigate(tsmi.Tag, null);
            if (tsmi.Owner == BreadCrumbTrail)
            {
                var clickedIndex = BreadCrumbTrail.Items.IndexOf(tsmi);
                for (int x = BreadCrumbTrail.Items.Count -1; x > clickedIndex; x--)
                    BreadCrumbTrail.Items.RemoveAt(x);
            }
        }

        private void SetRoot(string driveName, Dictionary<string, IEnumerable<string>> savedNeighbors)
        {
            BreadCrumbTrail.Items.Clear();
            _itemNeighbors = new Dictionary<ToolStripMenuItem, ToolStripMenuItem>();

            //Add folder icon
            var tsmi = new ToolStripMenuItem(string.Empty, Properties.Resources.folder);
            tsmi.Click += CreateManualFileEntryControl;
            BreadCrumbTrail.Items.Add(tsmi);

            //Add directories List
            tsmi = new ToolStripMenuItem(Properties.Resources.Left_Arrow)
                       {
                           Padding = new Padding(0),
                           ImageScaling = ToolStripItemImageScaling.None
                       };
            foreach (var item in savedNeighbors[driveName])
            {
                var rootName = string.IsNullOrEmpty(Path.GetFileName(item)) ? item : Path.GetFileName(item);
                var newitem = new ToolStripMenuItem(rootName) { Tag = item};
                newitem.Click += ClickItem;
                tsmi.DropDownItems.Add(newitem);
            }
            BreadCrumbTrail.Items.Add(tsmi);
            _defaultRootMenu = tsmi;

            //Add Root directory
            var shownName = string.IsNullOrEmpty(Path.GetFileName(driveName)) ? driveName : Path.GetFileName(driveName);
            tsmi = new ToolStripMenuItem(shownName) {Tag = driveName};
            tsmi.Click += ClickItem;
            BreadCrumbTrail.Items.Add(tsmi);
            _itemNeighbors.Add(tsmi,new ToolStripMenuItem {Width = 0});

            //Allow history button to be seen
            HistoryButton.Visible = true;
        }

        private ComboBox _fileCombo;
        private void CreateManualFileEntryControl(object sender, EventArgs e)
        {
            if (_fileCombo != null &&_fileCombo.Visible)
            {
                _fileCombo.Visible = false;
                Controls.Remove(_fileCombo);
                return;
            }

            string currentDirectory;
            if (BreadCrumbTrail.Items.Count > 2)
                currentDirectory = (string)BreadCrumbTrail.Items[BreadCrumbTrail.Items.Count - 1].Tag;
            else if (_overflowStack.Any())
                currentDirectory = (string) _overflowStack.Peek().Tag;
            else
                currentDirectory = string.Empty;

            _fileCombo = new ComboBox
                                {
                                    Text = currentDirectory,
                                    Anchor =
                                        AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                                    Width = BreadCrumbTrail.Width - BreadCrumbTrail.Items[0].Width + HistoryButton.Width-1,
                                    Location = new Point(BreadCrumbTrail.Items[0].Width,0),
                                    FlatStyle = FlatStyle.Flat,
                                    AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                                    AutoCompleteSource = AutoCompleteSource.FileSystemDirectories
                                };
            _fileCombo.Items.Add(currentDirectory);
            var history = FolderHistoryInterface.GetRecentFolders();
            foreach (var item in history.Where(item => !string.IsNullOrEmpty(item) && item != currentDirectory))
                _fileCombo.Items.Add(item);

            _fileCombo.KeyDown += (x, y) =>
                                      {
                                          if (y.KeyCode == Keys.Return)
                                          {
                                              y.Handled = true;
                                              if (Directory.Exists(_fileCombo.Text))
                                              {
                                                  _fileCombo.Visible = false;
                                                  Controls.Remove(_fileCombo);
                                                  Navigate(_fileCombo.Text.TrimEnd('\\'), null);
                                              }
                                              else
                                                  MessageBox.Show("Directory does not exist");
                                          }
                                          else if (y.KeyCode == Keys.Escape)
                                          {
                                              _fileCombo.Visible = false;
                                              Controls.Remove(_fileCombo);
                                              return;
                                          }
                                      };

            Controls.Add(_fileCombo);
            _fileCombo.Select();
            _fileCombo.SelectionStart = _fileCombo.Text.Length;

            if (sender == HistoryButton)
                _fileCombo.DroppedDown = true;
            _fileCombo.BringToFront();
        }

        public void NavigateToFolder(string path, Dictionary<string, IEnumerable<string>> neighborList)
        {
            var navigationList = PathToDirectoryList(path);
            if (!navigationList.Any())
                return;

            if (_fileCombo != null && _fileCombo.Visible)
            {
                _fileCombo.Visible = false;
                Controls.Remove(_fileCombo);
            }

            //check for higher level roots
            while (neighborList.Last().Key != navigationList.First())
            {
                if (navigationList.Count == 1)
                    return;
                navigationList[0] = Path.Combine(navigationList[0], navigationList[1]);
                navigationList.RemoveAt(1);
            }

            ShowHiddenCrumbs();

            //confirm or set root
            if (BreadCrumbTrail.Items.Count < 3
                || BreadCrumbTrail.Items[2].Text != navigationList[0])
                SetRoot(navigationList[0], neighborList);

            var pathSoFar = new StringBuilder(navigationList[0].TrimEnd('\\'));
            navigationList.RemoveAt(0);
            var expectedNext = 4;

            //go through, add as needed and remove where not
            foreach (var item in navigationList)
            {
                pathSoFar.AppendFormat(@"\{0}", item);
                if (BreadCrumbTrail.Items.Count > expectedNext)
                {
                    var fullName = ((string) BreadCrumbTrail.Items[expectedNext].Tag);
                    if (fullName.ToLower() == pathSoFar.ToString().ToLower())
                    {
                        expectedNext += 2;
                        continue;
                    }
                    for (var x = BreadCrumbTrail.Items.Count - 1; x >= expectedNext - 1; x--)
                    {
                        var itemToRemove = BreadCrumbTrail.Items[x];
                        if (_itemNeighbors.ContainsKey((ToolStripMenuItem) itemToRemove))
                            _itemNeighbors.Remove((ToolStripMenuItem) itemToRemove);
                        BreadCrumbTrail.Items.Remove(itemToRemove);
                    }
                }

                //add neighbor directories
                var neighborTSMI = new ToolStripMenuItem(Properties.Resources.Right_Arrow)
                                       {Padding = new Padding(0), ImageScaling = ToolStripItemImageScaling.None};
                foreach (var dir in neighborList[pathSoFar.ToString()])
                {
                    var newitem = new ToolStripMenuItem(Path.GetFileName(dir)) {Tag = dir};
                    newitem.Click += ClickItem;
                    neighborTSMI.DropDownItems.Add(newitem);
                }
                BreadCrumbTrail.Items.Add(neighborTSMI);

                //add main directory
                var mainTSMI = new ToolStripMenuItem(item) {Tag = pathSoFar.ToString()};
                mainTSMI.Click += ClickItem;
                BreadCrumbTrail.Items.Add(mainTSMI);
                expectedNext += 2;

                _itemNeighbors.Add(mainTSMI, neighborTSMI);
            }

            for (var x = BreadCrumbTrail.Items.Count - 1; x >= expectedNext - 1; x--)
            {
                var itemToRemove = BreadCrumbTrail.Items[x];
                if (_itemNeighbors.ContainsKey((ToolStripMenuItem) itemToRemove))
                    _itemNeighbors.Remove((ToolStripMenuItem) itemToRemove);
                BreadCrumbTrail.Items.RemoveAt(x);
            }

            _overflowStack = new Stack<ToolStripMenuItem>();
            CheckBreadcrumbSize();
        }

        private void ShowHiddenCrumbs()
        {
            if (!_overflowStack.Any())
                return;

            if (BreadCrumbTrail.Items.Count == 2)
                BreadCrumbTrail.Items.Add(_overflowStack.Pop());

            while (_overflowStack.Any())
            {
                var topLevelNeighbors = _itemNeighbors[(ToolStripMenuItem)BreadCrumbTrail.Items[2]];
                BreadCrumbTrail.Items.Insert(2, topLevelNeighbors);
                BreadCrumbTrail.Items.Insert(2, _overflowStack.Pop());
            }

            BreadCrumbTrail.Items.RemoveAt(1);
            BreadCrumbTrail.Items.Insert(1, new ToolStripMenuItem(Properties.Resources.Left_Arrow)
            {
                Padding = new Padding(0),
                ImageScaling = ToolStripItemImageScaling.None
            });
            foreach (ToolStripMenuItem item in _defaultRootMenu.DropDownItems)
            {
                var tsmi = new ToolStripMenuItem(item.Text) { Tag = item.Tag };
                tsmi.Click += ClickItem;
                ((ToolStripMenuItem)BreadCrumbTrail.Items[1]).DropDownItems.Add(tsmi);
            }
        }

        public void CheckBreadcrumbSize()
        {
            if (BreadCrumbTrail.Items.Count < 2)
                return;

            var acceptableWidth = BreadCrumbTrail.Width - 2;
            var changesMade = false;
            var totalWidth = BreadCrumbTrail.Items.Cast<ToolStripMenuItem>().Sum(item => item.Width);

            if (totalWidth > acceptableWidth)
            {
                var overflowAt = -1;
                var sizeUsed = BreadCrumbTrail.Items[0].Width + BreadCrumbTrail.Items[1].Width +
                               BreadCrumbTrail.Items[BreadCrumbTrail.Items.Count - 1].Width;
                if (sizeUsed > acceptableWidth)
                    overflowAt = BreadCrumbTrail.Items.Count - 1;
                else
                {
                    for (var x = BreadCrumbTrail.Items.Count - 2; x > 2; x -= 2)
                    {
                        var sizeIfAccepted = sizeUsed + BreadCrumbTrail.Items[x].Width +
                                             BreadCrumbTrail.Items[x - 1].Width;
                        if (sizeIfAccepted > acceptableWidth)
                        {
                            overflowAt = x;
                            break;
                        }
                        sizeUsed = sizeIfAccepted;
                    }
                }
                for (var x = 2; x <= overflowAt; x += 2)
                    _overflowStack.Push((ToolStripMenuItem) BreadCrumbTrail.Items[x]);
                for (var x = overflowAt; x > 1; x--)
                    BreadCrumbTrail.Items.RemoveAt(x);
                changesMade = true;
            }
            else if (_overflowStack.Any())
            {
                if (BreadCrumbTrail.Items.Count == 2)
                {
                    BreadCrumbTrail.Items.Add(_overflowStack.Pop());
                }
                if (_overflowStack.Any())
                {
                    var topLevelNeighbors = _itemNeighbors[(ToolStripMenuItem)BreadCrumbTrail.Items[2]];

                    while (_overflowStack.Any() &&
                            totalWidth + topLevelNeighbors.Width +
                            _overflowStack.Peek().Width < acceptableWidth)
                    {
                        totalWidth += topLevelNeighbors.Width + _overflowStack.Peek().Width;
                        BreadCrumbTrail.Items.Insert(2, topLevelNeighbors);
                        BreadCrumbTrail.Items.Insert(2, _overflowStack.Pop());
                        topLevelNeighbors = _itemNeighbors[(ToolStripMenuItem)BreadCrumbTrail.Items[2]];
                        changesMade = true;
                    }
                }
            }

            if (changesMade)
            {
                BreadCrumbTrail.Items.RemoveAt(1);
                BreadCrumbTrail.Items.Insert(1, new ToolStripMenuItem(Properties.Resources.Left_Arrow)
                                                    {
                                                        Padding = new Padding(0),
                                                        ImageScaling = ToolStripItemImageScaling.None
                                                    });
                if (_overflowStack.Any())
                {
                    foreach(var item in _overflowStack)
                    {
                        var newitem = new ToolStripMenuItem(item.Text) { Tag = item.Tag };
                        newitem.Click += ClickItem;
                        ((ToolStripMenuItem) BreadCrumbTrail.Items[1]).DropDownItems.Add(newitem);
                    }

                    ((ToolStripMenuItem) BreadCrumbTrail.Items[1]).DropDownItems.Add(new ToolStripSeparator());
                }
                
                foreach (ToolStripMenuItem item in _defaultRootMenu.DropDownItems)
                {
                    var tsmi = new ToolStripMenuItem(item.Text) {Tag = item.Tag};
                    tsmi.Click += ClickItem;
                    ((ToolStripMenuItem) BreadCrumbTrail.Items[1]).DropDownItems.Add(tsmi);
                }
            }
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            string currentDirectory;
            if (BreadCrumbTrail.Items.Count > 2)
                currentDirectory = (string)BreadCrumbTrail.Items[BreadCrumbTrail.Items.Count - 1].Tag;
            else if (_overflowStack.Any())
                currentDirectory = (string)_overflowStack.Peek().Tag;
            else
                return;

            Navigate(currentDirectory+@":", null);
        }

        private void BreadCrumbTrail_MouseClick(object sender, MouseEventArgs e)
        {
            var item = BreadCrumbTrail.GetItemAt(e.Location);
            if (item == null && BreadCrumbTrail.Items.Count > 1)
                CreateManualFileEntryControl(null,null);
        }
    }
}
