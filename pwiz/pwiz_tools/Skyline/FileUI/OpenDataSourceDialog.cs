/*
 * Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
 *
 * Copyright 2009 Vanderbilt University - Nashville, TN 37232
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.IO;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    public partial class OpenDataSourceDialog : FormEx
    {
        private readonly ListViewColumnSorter _listViewColumnSorter = new ListViewColumnSorter();
        private readonly Stack<string> _previousDirectories = new Stack<string>();

        public OpenDataSourceDialog()
        {
            InitializeComponent();

            listView.ListViewItemSorter = _listViewColumnSorter;

            DialogResult = DialogResult.Cancel;

            string[] sourceTypes = new[]
            {
                "Any spectra format",
				DataSourceUtil.TYPE_WIFF,
                DataSourceUtil.TYPE_AGILENT,
				DataSourceUtil.TYPE_THERMO_RAW,
                DataSourceUtil.TYPE_WATERS_RAW,
				DataSourceUtil.TYPE_MZML,
				DataSourceUtil.TYPE_MZXML,
				//"mzData",
				//"Bruker YEP",
                //"Bruker BAF",
                //"Bruker FID",
				//"Mascot Generic",
                //"Bruker Data Exchange",
				//"Sequest DTA"
            };

            sourceTypeComboBox.Items.AddRange(sourceTypes.Cast<object>().ToArray());
            sourceTypeComboBox.SelectedIndex = 0;

            ImageList imageList = new ImageList {ColorDepth = ColorDepth.Depth32Bit};
            imageList.Images.Add(Properties.Resources.Folder);
            imageList.Images.Add(Properties.Resources.File);
            imageList.Images.Add(Properties.Resources.DataProcessing);
            imageList.Images.Add(lookInImageList.Images[5]);
            imageList.Images.Add(lookInImageList.Images[6]);
            imageList.Images.Add(lookInImageList.Images[7]);
            listView.SmallImageList = imageList;
            listView.LargeImageList = imageList;

            TreeView tv = new TreeView {Indent = 8};
            TreeNode lookInNode = tv.Nodes.Add( "My Recent Documents", "My Recent Documents", 0, 0 );
            lookInNode.Tag = lookInNode.Text;
            lookInComboBox.Items.Add( lookInNode );
            TreeNode desktopNode = tv.Nodes.Add( "Desktop", "Desktop", 1, 1 );
            desktopNode.Tag = desktopNode.Text;
            lookInComboBox.Items.Add( desktopNode );
            lookInNode = desktopNode.Nodes.Add( "My Documents", "My Documents", 2, 2 );
            lookInNode.Tag = lookInNode.Text;
            lookInComboBox.Items.Add( lookInNode );
            TreeNode myComputerNode = desktopNode.Nodes.Add( "My Computer", "My Computer", 3, 3 );
            myComputerNode.Tag = myComputerNode.Text;
            lookInComboBox.Items.Add( myComputerNode );
            lookInComboBox.SelectedIndex = 1;
            lookInComboBox.IntegralHeight = false;
            lookInComboBox.DropDownHeight = lookInComboBox.Items.Count * lookInComboBox.ItemHeight + 2;
        }

        public new DialogResult ShowDialog()
        {
            CurrentDirectory = InitialDirectory ?? Environment.CurrentDirectory;
            return base.ShowDialog();
        }

        public new DialogResult ShowDialog(IWin32Window owner)
        {
            CurrentDirectory = InitialDirectory ?? Environment.CurrentDirectory;
            return base.ShowDialog(owner);
        }

        private string _currentDirectory;
        public string CurrentDirectory
        {
            get { return _currentDirectory; }
            set
            {
                if (value != null && (value == "" || Directory.Exists(value)))
                {
                    _currentDirectory = value;
                    populateListViewFromDirectory(_currentDirectory);
                    populateComboBoxFromDirectory(_currentDirectory);
                }
            }
        }

        public string InitialDirectory { get; set; }

        public string DataSource
        {
            get { return DataSources[0]; }
        }

        public string[] DataSources { get; private set; }

        public void SelectAllFileType(string extension)
        {
            foreach(ListViewItem item in listView.Items)
            {
                if (item.Text.ToLower().EndsWith(extension.ToLower()))
                    listView.SelectedIndices.Add(item.Index);
            }
        }

        public void SelectFile(string fileName)
        {
            foreach (ListViewItem item in listView.Items)
            {
                if (Equals(item.Text, fileName))
                    listView.SelectedIndices.Add(item.Index);
            }
        }

        private string _sourceTypeName;
        public string SourceTypeName
        {
            get { return _sourceTypeName; }
            set
            {
                _sourceTypeName = value;
                sourceTypeComboBox.SelectedItem = _sourceTypeName;
            }
        }

        private class SourceInfo
        {

// ReSharper disable InconsistentNaming
            public string name;
            public string type;
            public int imageIndex;
            public UInt64 size;
            public DateTime? dateModified;
// ReSharper restore InconsistentNaming

            public bool isFolder
            {
                get { return DataSourceUtil.IsFolderType(type); }
            }

            public bool isUnknown
            {
                get { return DataSourceUtil.IsUnknownType(type); }
            }

            public string[] ToArray()
            {
                return new[]
                {
                    name,
                    type,
                    SizeLabel,
                    DateModifiedLabel
                };
            }

            private string DateModifiedLabel
            {
                get
                {
                    return dateModified.HasValue
                               ? String.Format("{0} {1}", dateModified.Value.ToShortDateString(),
                                                          dateModified.Value.ToShortTimeString())
                               : "";
                }
            }

            private string SizeLabel
            {
                get
                {
                    return type != DataSourceUtil.FOLDER_TYPE
                        ? string.Format(new FileSizeFormatProvider(), "{0:fs}", size)
                        : "";
                }
            }
        }

        private SourceInfo getSourceInfo( DirectoryInfo dirInfo )
        {
            string type = DataSourceUtil.GetSourceType(dirInfo);
            SourceInfo sourceInfo = new SourceInfo
            {
                type = type,
                imageIndex = (DataSourceUtil.IsFolderType(type) ? 0 : 2),
                name = dirInfo.Name,
                dateModified = GetSafeDateModified(dirInfo)
            };

            if(listView.View != View.Details ||
                    (sourceTypeComboBox.SelectedIndex > 0 &&
                     sourceTypeComboBox.SelectedItem.ToString() != sourceInfo.type))
                return sourceInfo;

            if(sourceInfo.isFolder)
            {
                return sourceInfo;
            }
            if(!sourceInfo.isUnknown)
            {
                sourceInfo.size = 0;
                foreach( FileInfo fileInfo in dirInfo.GetFiles() )
                    sourceInfo.size += (UInt64) fileInfo.Length;
                return sourceInfo;
            }
            return null;
        }

        private SourceInfo getSourceInfo(FileInfo fileInfo)
        {
            string type = DataSourceUtil.GetSourceType(fileInfo);
            SourceInfo sourceInfo = new SourceInfo
                                        {
                                            type = type,
                                            imageIndex = (DataSourceUtil.IsUnknownType(type) ? 1 : 2),
                                            name = fileInfo.Name
                                        };
            if( !sourceInfo.isUnknown )
            {
                if(listView.View != View.Details ||
                        (sourceTypeComboBox.SelectedIndex > 0 &&
                         sourceTypeComboBox.SelectedItem.ToString() != sourceInfo.type))
                    return sourceInfo;
                sourceInfo.size = (UInt64) fileInfo.Length;
                sourceInfo.dateModified = GetSafeDateModified(fileInfo);
                return sourceInfo;
            }
            return null;
        }

        private bool _abortPopulateList;
        private void populateListViewFromDirectory(string directory)
        {
            _abortPopulateList = false;
            listView.Items.Clear();

            var listSourceInfo = new List<SourceInfo>();
            if (string.IsNullOrEmpty(directory))
            {
                foreach (DriveInfo driveInfo in DriveInfo.GetDrives())
                {
                    string label = "";
                    string sublabel = driveInfo.Name;
                    int imageIndex = 0;
                    _driveReadiness[sublabel] = false;
                    try
                    {
                        switch (driveInfo.DriveType)
                        {
                            case DriveType.Fixed:
                                imageIndex = 3;
                                label = "Local Drive";
                                if (driveInfo.VolumeLabel.Length > 0)
                                    label = driveInfo.VolumeLabel;
                                break;
                            case DriveType.CDRom:
                                imageIndex = 4;
                                label = "Optical Drive";
                                if (driveInfo.IsReady && driveInfo.VolumeLabel.Length > 0)
                                    label = driveInfo.VolumeLabel;
                                break;
                            case DriveType.Removable:
                                imageIndex = 4;
                                label = "Removable Drive";
                                if (driveInfo.IsReady && driveInfo.VolumeLabel.Length > 0)
                                    label = driveInfo.VolumeLabel;
                                break;
                            case DriveType.Network:
                                imageIndex = 5;
                                label = "Network Share";
                                break;
                        }
                        _driveReadiness[sublabel] = driveInfo.IsReady;
                    }
                    catch (Exception)
                    {
                        label += " (access failure)";
                    }

                    string name = driveInfo.Name;
                    if (label != "")
                        name = string.Format("{0} ({1})", label, name);

                    listSourceInfo.Add(new SourceInfo
                    {
                        type = DataSourceUtil.FOLDER_TYPE,
                        imageIndex = imageIndex,
                        name = name,
                        dateModified = GetSafeDateModified(driveInfo.RootDirectory)
                    });
                }
            }
            else
            {
                DirectoryInfo dirInfo = new DirectoryInfo(directory);

                DirectoryInfo[] arraySubDirInfo;
                FileInfo[] arrayFileInfo;
                try
                {
                    // subitems: Name, Type, Spectra, Size, Date Modified
                    arraySubDirInfo = dirInfo.GetDirectories();
                    Array.Sort(arraySubDirInfo, (d1, d2) => string.Compare(d1.Name, d2.Name, StringComparison.CurrentCultureIgnoreCase));
                    arrayFileInfo = dirInfo.GetFiles();
                    Array.Sort(arrayFileInfo, (f1, f2) => string.Compare(f1.Name, f2.Name, StringComparison.CurrentCultureIgnoreCase));
                }
                catch (Exception x)
                {
                    // Might throw access violation.
                    MessageBox.Show(this, string.Format("An error occurred attempting to retrieve the contents of this directory.\n{0}", x.Message), Program.Name);
                    return;
                }

                // Calculate information about the files, allowing the user to cancel
                foreach (var info in arraySubDirInfo)
                {
                    listSourceInfo.Add(getSourceInfo(info));
                    Application.DoEvents();
                    if (_abortPopulateList)
                    {
                        //MessageBox.Show( "abort" );
                        break;
                    }
                }

                if (!_abortPopulateList)
                {
                    foreach (var info in arrayFileInfo)
                    {
                        listSourceInfo.Add(getSourceInfo(info));
                        Application.DoEvents();
                        if (_abortPopulateList)
                        {
                            //MessageBox.Show( "abort" );
                            break;
                        }
                    }
                }                
            }

            // Populate the list
            try
            {
                listView.BeginUpdate();
                foreach (var sourceInfo in listSourceInfo)
                {
                    if (sourceInfo != null &&
                            (sourceTypeComboBox.SelectedIndex == 0 ||
                             sourceTypeComboBox.SelectedItem.ToString() == sourceInfo.type ||
                             // Always show folders
                             sourceInfo.isFolder))
                    {
                        ListViewItem item = new ListViewItem(sourceInfo.ToArray(), sourceInfo.imageIndex);
                        item.SubItems[2].Tag = sourceInfo.size;
                        item.SubItems[3].Tag = sourceInfo.dateModified;
                        
                        listView.Items.Add(item);
                    }
                }
            }
            finally
            {
                listView.EndUpdate();
            }
        }

        private static DateTime? GetSafeDateModified(FileSystemInfo dirInfo)
        {
            try
            {
                return dirInfo.LastWriteTime;
            }
            catch (IOException) {}
            catch (UnauthorizedAccessException) {}
            return null;
        }

        private void populateComboBoxFromDirectory( string directory )
        {
            lookInComboBox.SuspendLayout();

            // remove old drive entries
            while( lookInComboBox.Items.Count > 4 )
                lookInComboBox.Items.RemoveAt( 4 );

            DirectoryInfo dirInfo = (!string.IsNullOrEmpty(directory) ? new DirectoryInfo(directory) : null);

            // fill tree view with special locations and drives
            TreeNode myComputerNode = (TreeNode) lookInComboBox.Items[3];
            if (dirInfo == null)
                lookInComboBox.SelectedItem = myComputerNode;

            int driveCount = 0;
            foreach( DriveInfo driveInfo in DriveInfo.GetDrives() )
            {
                string label = "";
                string sublabel = driveInfo.Name;
                int imageIndex = 8;
                ++driveCount;
                _driveReadiness[sublabel] = false;
                try
                {
                    switch (driveInfo.DriveType)
                    {
                        case DriveType.Fixed:
                            imageIndex = 5;
                            label = "Local Drive";
                            if (driveInfo.VolumeLabel.Length > 0)
                                label = driveInfo.VolumeLabel;
                            break;
                        case DriveType.CDRom:
                            imageIndex = 6;
                            label = "Optical Drive";
                            if (driveInfo.IsReady && driveInfo.VolumeLabel.Length > 0)
                                label = driveInfo.VolumeLabel;
                            break;
                        case DriveType.Removable:
                            imageIndex = 6;
                            label = "Removable Drive";
                            if (driveInfo.IsReady && driveInfo.VolumeLabel.Length > 0)
                                label = driveInfo.VolumeLabel;
                            break;
                        case DriveType.Network:
                            imageIndex = 7;
                            label = "Network Share";
                            break;
                    }
                    _driveReadiness[sublabel] = driveInfo.IsReady;
                }
                catch (Exception)
                {
                    label += " (access failure)";
                }
                TreeNode driveNode = myComputerNode.Nodes.Add(sublabel,
                                                              label.Length > 0
                                                                  ? String.Format("{0} ({1})", label, sublabel)
                                                                  : sublabel,
                                                              imageIndex,
                                                              imageIndex);
                driveNode.Tag = sublabel;
                lookInComboBox.Items.Insert( 3 + driveCount, driveNode );

                if( dirInfo != null && sublabel == dirInfo.Root.Name )
                {
                    List<string> branches = new List<string>( directory.Split( new[] {
                                                 Path.DirectorySeparatorChar,
                                                 Path.AltDirectorySeparatorChar },
                                                 StringSplitOptions.RemoveEmptyEntries ) );
                    TreeNode pathNode = driveNode;
                    for( int i = 1; i < branches.Count; ++i )
                    {
                        ++driveCount;
                        pathNode = pathNode.Nodes.Add( branches[i], branches[i], 8, 8 );
                        pathNode.Tag = String.Join(Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture),
                                                    branches.GetRange( 0, i + 1 ).ToArray() );
                        lookInComboBox.Items.Insert( 3 + driveCount, pathNode );
                    }
                    lookInComboBox.SelectedIndex = 3 + driveCount;
                }
            }
            //desktopNode.Nodes.Add( "My Network Places", "My Network Places", 4, 4 ).Tag = "My Network Places";

            lookInComboBox.DropDownHeight = lookInComboBox.Items.Count * 18 + 2;

            lookInComboBox.ResumeLayout();
        }

        private void sourcePathTextBox_KeyUp( object sender, KeyEventArgs e )
        {
            switch( e.KeyCode )
            {
                case Keys.Enter:
                    if( Directory.Exists( sourcePathTextBox.Text ) )
                        CurrentDirectory = sourcePathTextBox.Text;
                    else if( Directory.Exists( Path.Combine( CurrentDirectory, sourcePathTextBox.Text ) ) )
                        CurrentDirectory = Path.Combine( CurrentDirectory, sourcePathTextBox.Text );
                    else
                    {
                        // check that all manually-entered paths are valid
                        string[] sourcePaths = sourcePathTextBox.Text.Split( " ".ToCharArray() );
                        List<string> invalidPaths = new List<string>();
                        foreach( string path in sourcePaths )
                            if( !File.Exists( path ) && !File.Exists( Path.Combine( CurrentDirectory, path ) ) )
                                invalidPaths.Add( path );

                        if( invalidPaths.Count == 0 )
                        {
                            DataSources = sourcePaths;
                            DialogResult = DialogResult.OK;
                            Close();
                        } else
                            MessageBox.Show(this, String.Join( "\r\n", invalidPaths.ToArray() ), "Some source paths are invalid" );
                    }
                    break;
                case Keys.F5:
                    _abortPopulateList = true;
                    populateListViewFromDirectory( _currentDirectory ); // refresh
                    break;
            }
        }

        private void listView_ItemActivate( object sender, EventArgs e )
        {
            if (listView.SelectedItems.Count == 0)
                return;

            ListViewItem item = listView.SelectedItems[0];
            if( item.SubItems[1].Text == "File Folder" )
            {
                if( _currentDirectory != null )
                    _previousDirectories.Push( _currentDirectory ); 
                CurrentDirectory = Path.Combine( CurrentDirectory, GetItemPath(item) );
                _abortPopulateList = true;
            }  else
            {
                DataSources = new[] { Path.Combine( CurrentDirectory, item.SubItems[0].Text ) };
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void listView_ColumnClick( object sender, ColumnClickEventArgs e )
        {
            // Determine if the clicked column is already the column that is being sorted.
            if( e.Column == _listViewColumnSorter.SortColumn )
            {
                // Reverse the current sort direction for this column.
                _listViewColumnSorter.Order = _listViewColumnSorter.Order == SortOrder.Ascending
                                                  ? SortOrder.Descending
                                                  : SortOrder.Ascending;
            } 
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                _listViewColumnSorter.SortColumn = e.Column;
                _listViewColumnSorter.Order = SortOrder.Ascending;
            }

            // Perform the sort with these new sort options.
            listView.Sort();
        }

        private void openButton_Click( object sender, EventArgs e )
        {
            Open();
        }

        public void Open()
        {
            if (listView.SelectedItems.Count > 0)
            {
                List<string> dataSourceList = new List<string>();
                foreach (ListViewItem item in listView.SelectedItems)
                {
                    if (item.SubItems[1].Text != "File Folder")
                        dataSourceList.Add(Path.Combine(CurrentDirectory, item.SubItems[0].Text));
                }
                DataSources = dataSourceList.ToArray();
                _abortPopulateList = true;
                DialogResult = DialogResult.OK;
            }
            else
                MessageBox.Show("Please select one or more data sources.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void cancelButton_Click( object sender, EventArgs e )
        {
            _abortPopulateList = true;
            DialogResult = DialogResult.Cancel;
            Close();
            Application.DoEvents();
        }

        private void tilesToolStripMenuItem_Click( object sender, EventArgs e )
        {
            foreach( ToolStripDropDownItem item in viewsDropDownButton.DropDownItems )
                ( (ToolStripMenuItem) item ).Checked = false;
            ( (ToolStripMenuItem) viewsDropDownButton.DropDownItems[0] ).Checked = true;
            listView.BeginUpdate();
            listView.View = View.Tile;
            listView.EndUpdate();
        }

        private void listToolStripMenuItem_Click( object sender, EventArgs e )
        {
            foreach( ToolStripDropDownItem item in viewsDropDownButton.DropDownItems )
                ( (ToolStripMenuItem) item ).Checked = false;
            ( (ToolStripMenuItem) viewsDropDownButton.DropDownItems[1] ).Checked = true;
            listView.View = View.List;
            listView.Columns[0].Width = -1;
        }

        private void detailsToolStripMenuItem_Click( object sender, EventArgs e )
        {
            if( listView.View != View.Details )
            {
                foreach( ToolStripDropDownItem item in viewsDropDownButton.DropDownItems )
                    ( (ToolStripMenuItem) item ).Checked = false;
                ( (ToolStripMenuItem) viewsDropDownButton.DropDownItems[2] ).Checked = true;
                listView.View = View.Details;
                populateListViewFromDirectory( _currentDirectory );
                listView.Columns[0].Width = 200;
            }
        }

        private void upOneLevelButton_Click( object sender, EventArgs e )
        {
            if (string.IsNullOrEmpty(_currentDirectory))
                return;

            DirectoryInfo parentDirectory = Directory.GetParent(_currentDirectory);
            string parentDirectoryName = (parentDirectory != null ? parentDirectory.FullName : "");

            if( parentDirectoryName != _currentDirectory )
            {
                _previousDirectories.Push( _currentDirectory );
                CurrentDirectory = parentDirectoryName;
            }
        }

        private void backButton_Click( object sender, EventArgs e )
        {
            if( _previousDirectories.Count > 0 )
                CurrentDirectory = _previousDirectories.Pop();
        }

        private void listView_ItemSelectionChanged( object sender, ListViewItemSelectionChangedEventArgs e )
        {
            if( listView.SelectedItems.Count > 1 )
            {
                List<string> dataSourceList = new List<string>();
                foreach( ListViewItem item in listView.SelectedItems )
                {
                    if( item.SubItems[1].Text != "File Folder" )
                        dataSourceList.Add( String.Format( "\"{0}\"", GetItemPath(item) ) );
                }
                sourcePathTextBox.Text = String.Join( " ", dataSourceList.ToArray() );
            }
            else if (listView.SelectedItems.Count > 0)
            {
                sourcePathTextBox.Text = GetItemPath(listView.SelectedItems[0]);
            }
            else
            {
                sourcePathTextBox.Text = "";
            }
        }

        private static readonly Regex REGEX_DRIVE = new Regex("\\(([A-Z]:\\\\)\\)");

        private static string GetItemPath(ListViewItem item)
        {
            string path = item.SubItems[0].Text;
            Match match = REGEX_DRIVE.Match(path);
            if (match.Success)
                path = match.Groups[1].Value;
            return path;
        }

        private void sourceTypeComboBox_SelectionChangeCommitted( object sender, EventArgs e )
        {
            _sourceTypeName = (string) sourceTypeComboBox.SelectedItem;
            populateListViewFromDirectory( _currentDirectory );
            _abortPopulateList = true;
        }

        private void listView_KeyDown( object sender, KeyEventArgs e )
        {
            switch( e.KeyCode )
            {
                case Keys.F5:
                    populateListViewFromDirectory( _currentDirectory ); // refresh
                    _abortPopulateList = true;
                    break;
            }
        }

        private void recentDocumentsButton_Click( object sender, EventArgs e )
        {
            CurrentDirectory = Environment.GetFolderPath( Environment.SpecialFolder.Recent );
        }

        private void desktopButton_Click( object sender, EventArgs e )
        {
            CurrentDirectory = Environment.GetFolderPath( Environment.SpecialFolder.DesktopDirectory );
        }

        private void myDocumentsButton_Click( object sender, EventArgs e )
        {
            CurrentDirectory = Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments );
        }

        private void myComputerButton_Click( object sender, EventArgs e )
        {
            CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);
        }

//        private void myNetworkPlacesButton_Click( object sender, EventArgs e )
//        {
//
//        }

        private readonly Dictionary<string, bool> _driveReadiness = new Dictionary<string, bool>();
        private void lookInComboBox_DropDown( object sender, EventArgs e )
        {
            
        }

        private void lookInComboBox_DrawItem( object sender, DrawItemEventArgs e )
        {
            if( e.Index < 0 )
                return;

            TreeNode node = (TreeNode) lookInComboBox.Items[e.Index];

            int x, y, indent;
            if( ( e.State & DrawItemState.ComboBoxEdit ) == DrawItemState.ComboBoxEdit )
            {
                x = 2;
                y = 2;
                indent = 0;
            }
            else
            {
                e.DrawBackground();
                e.DrawFocusRectangle();

                x = node.TreeView.Indent / 2;
                y = e.Bounds.Y;
                indent = node.TreeView.Indent * node.Level;
            }

            Image image = lookInImageList.Images[node.ImageIndex];
            e.Graphics.DrawImage( image, x + indent, y, 16, 16 );
            e.Graphics.DrawString( node.Text, lookInComboBox.Font, new SolidBrush(lookInComboBox.ForeColor), x + indent + 16, y );
        }

        private void lookInComboBox_MeasureItem( object sender, MeasureItemEventArgs e )
        {
            if( e.Index < 0 )
                return;

            TreeNode node = (TreeNode) lookInComboBox.Items[e.Index];
            int x = node.TreeView.Indent / 2;
            int indent = node.TreeView.Indent * node.Level;
            e.ItemHeight = 16;
            e.ItemWidth = x + indent + 16 + (int) e.Graphics.MeasureString( node.Text, lookInComboBox.Font ).Width;
        }

        private void lookInComboBox_SelectionChangeCommitted( object sender, EventArgs e )
        {
            if( lookInComboBox.SelectedIndex < 0 )
                lookInComboBox.SelectedIndex = 0;

            var selectedItem = (TreeNode) lookInComboBox.SelectedItem;
            string location = selectedItem.Tag as string;
            var prevDirectory = CurrentDirectory;
            switch (location)
            {
                case "My Recent Documents":
                    CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
                    break;
                case "Desktop":
                    CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    break;
                case "My Documents":
                    CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                    break;
                case "My Computer":
                    CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);
                    break;
                case "My Network Places":
                    break;
                default:
                    if (location != null)
                    {
                        // Look for the drive containing this location
                        foreach (var drivePair in _driveReadiness)
                        {
                            if (location.StartsWith(drivePair.Key))
                            {
                                // If it is ready switch to it
                                if (drivePair.Value)
                                {
                                    CurrentDirectory = location;
                                }
                                break;
                            }
                        }                        
                    }
                    // If location for this drive is not ready, stick with the current directory.
                    CurrentDirectory = _currentDirectory;
                    break;
            }
            if(!Equals(prevDirectory, CurrentDirectory))
                _previousDirectories.Push(prevDirectory);
        }
    }
}