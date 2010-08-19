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
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    public partial class OpenDataSourceDialog : Form
    {
        public const string TYPE_WIFF = "ABSciex WIFF";
        public const string TYPE_AGILENT = "Agilent Data";
        public const string TYPE_THERMO_RAW = "Thermo RAW";
        public const string TYPE_WATERS_RAW = "Waters RAW";
        public const string TYPE_MZML = "mzML";
        public const string TYPE_MZXML = "mzXML";

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
				TYPE_WIFF,
                TYPE_AGILENT,
				TYPE_THERMO_RAW,
                TYPE_WATERS_RAW,
				TYPE_MZML,
				TYPE_MZXML,
				//"mzData",
				//"Bruker YEP",
                //"Bruker BAF",
                //"Bruker FID",
				//"Mascot Generic",
                //"Bruker Data Exchange",
				//"Sequest DTA"
            };

            sourceTypeComboBox.Items.AddRange( sourceTypes );
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
            private set
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
            public const string FOLDER_TYPE = "File Folder";

            public static bool isFolderType(string type)
            {
                return Equals(type, FOLDER_TYPE);
            }

            public const string UNKNOWN_TYPE = "unknown";

            public static bool isUnknownType(string type)
            {
                return Equals(type, UNKNOWN_TYPE);
            }

// ReSharper disable InconsistentNaming
            public string name;
            public string type;
            public int imageIndex;
            public UInt64 size;
            public DateTime dateModified;
// ReSharper restore InconsistentNaming

            public bool isFolder
            {
                get { return isFolderType(type); }
            }

            public bool isUnknown
            {
                get { return isUnknownType(type); }
            }

            public string[] ToArray()
            {
                if( type == FOLDER_TYPE )
                {
                    return new[]
                    {
                        name,
                        type,
                        "",
                        String.Format( "{0} {1}", dateModified.ToShortDateString(),
                                                  dateModified.ToShortTimeString())
                    };
                }
                else
                {
                    return new[]
                    {
                        name,
                        type,
                        String.Format( new FileSizeFormatProvider(), "{0:fs}", size ),
                        String.Format( "{0} {1}", dateModified.ToShortDateString(),
                                                  dateModified.ToShortTimeString() )
                    };
                }
            }
        }

        private static string getSourceTypeFromXML(string filepath)
        {
            XmlReaderSettings settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.None,
                ProhibitDtd = false,
                XmlResolver = null
            };
            using(XmlReader reader = XmlReader.Create( new StreamReader( filepath, true ), settings ))
            {
                try
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            switch (reader.Name.ToLower())
                            {
                                case "mzml":
                                case "indexmzml":
                                    return "mzML";
                                case "mzxml":
                                case "msrun":
                                    return "mzXML";
                                    //case "mzdata":
                                    //    return "mzData";
                                case "root":
                                    return "Bruker Data Exchange";
                                default:
                                    return SourceInfo.UNKNOWN_TYPE;
                            }
                        }
                    }
                }
                catch(XmlException)
                {
                    return SourceInfo.UNKNOWN_TYPE;
                }
            }
            return SourceInfo.UNKNOWN_TYPE;
        }

        private static string getSourceType(DirectoryInfo dirInfo)
        {
            if( dirInfo.Name.EndsWith( ".raw" ) &&
                    dirInfo.GetFiles( "_FUNC*.DAT" ).Length > 0 )
                return TYPE_WATERS_RAW;
            if( dirInfo.Name.EndsWith( ".d" ) &&
                    dirInfo.GetDirectories( "AcqData" ).Length > 0 )
                return TYPE_AGILENT;
            return SourceInfo.FOLDER_TYPE;
        }

        public static bool IsDataSource(DirectoryInfo dirInfo)
        {
            return !SourceInfo.isFolderType(getSourceType(dirInfo));
        }

        private static string getSourceType(FileSystemInfo fileInfo)
        {
            //if( fileInfo.Name == "fid" )
            //    return "Bruker FID";

            switch( fileInfo.Extension.ToLower() )
            {
                case ".raw": return TYPE_THERMO_RAW;
                case ".wiff": return TYPE_WIFF;
                //case ".mgf": return "Mascot Generic";
                //case ".dta": return "Sequest DTA";
                //case ".yep": return "Bruker YEP";
                //case ".baf": return "Bruker BAF";
                //case ".ms2": return "MS2";
                case ".mzxml": return TYPE_MZXML;
                //case ".mzdata": return "mzData";
                case ".mzml": return TYPE_MZML;
                case ".xml": return getSourceTypeFromXML(fileInfo.FullName);
                default: return SourceInfo.UNKNOWN_TYPE;
            }
        }

        public static bool IsDataSource(FileInfo fileInfo)
        {
            return !SourceInfo.isUnknownType(getSourceType(fileInfo));
        }

        private SourceInfo getSourceInfo( DirectoryInfo dirInfo )
        {
            string type = getSourceType(dirInfo);
            SourceInfo sourceInfo = new SourceInfo
            {
                type = type,
                imageIndex = (SourceInfo.isFolderType(type) ? 0 : 2),
                name = dirInfo.Name,
                dateModified = dirInfo.LastWriteTime
            };

            if(listView.View != View.Details ||
                    (sourceTypeComboBox.SelectedIndex > 0 &&
                     sourceTypeComboBox.SelectedItem.ToString() != sourceInfo.type))
                return sourceInfo;

            if(sourceInfo.isFolder)
            {
                return sourceInfo;
            }
            else if(!sourceInfo.isUnknown)
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
            string type = getSourceType(fileInfo);
            SourceInfo sourceInfo = new SourceInfo
                                        {
                                            type = type,
                                            imageIndex = (SourceInfo.isUnknownType(type) ? 1 : 2),
                                            name = fileInfo.Name
                                        };
            if( !sourceInfo.isUnknown )
            {
                if(listView.View != View.Details ||
                        (sourceTypeComboBox.SelectedIndex > 0 &&
                         sourceTypeComboBox.SelectedItem.ToString() != sourceInfo.type))
                    return sourceInfo;
                sourceInfo.size = (UInt64) fileInfo.Length;
                sourceInfo.dateModified = fileInfo.LastWriteTime;
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
                    string label;
                    string sublabel = driveInfo.Name;
                    int imageIndex;
                    try
                    {
                        _driveReadiness[sublabel] = driveInfo.IsReady;
                        switch (driveInfo.DriveType)
                        {
                            case DriveType.Fixed:
                                label = driveInfo.VolumeLabel.Length > 0 ? driveInfo.VolumeLabel : "Local Drive";
                                imageIndex = 3;
                                break;
                            case DriveType.CDRom:
                                if (driveInfo.IsReady && driveInfo.VolumeLabel.Length > 0)
                                    label = driveInfo.VolumeLabel;
                                else
                                    label = "Optical Drive";
                                imageIndex = 4;
                                break;
                            case DriveType.Removable:
                                if (driveInfo.IsReady && driveInfo.VolumeLabel.Length > 0)
                                    label = driveInfo.VolumeLabel;
                                else
                                    label = "Removable Drive";
                                imageIndex = 4;
                                break;
                            case DriveType.Network:
                                label = "Network Share";
                                imageIndex = 5;
                                break;
                            default:
                                label = "";
                                imageIndex = 0;
                                break;
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        _driveReadiness[sublabel] = false;
                        label = "Network Share (access failure)";
                        imageIndex = 5;
                    }

                    string name = driveInfo.Name;
                    if (label != "")
                        name = string.Format("{0} ({1})", label, name);
                    listSourceInfo.Add(new SourceInfo
                                           {
                                               type = SourceInfo.FOLDER_TYPE,
                                               imageIndex = imageIndex,
                                               name = name,
                                               dateModified = driveInfo.RootDirectory.LastWriteTime
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
                string label;
                string sublabel = driveInfo.Name;
                int imageIndex;
                ++driveCount;
                try
                {
                    _driveReadiness[sublabel] = driveInfo.IsReady;
                    switch (driveInfo.DriveType)
                    {
                        case DriveType.Fixed:
                            label = driveInfo.VolumeLabel.Length > 0 ? driveInfo.VolumeLabel : "Local Drive";
                            imageIndex = 5;
                            break;
                        case DriveType.CDRom:
                            if (driveInfo.IsReady && driveInfo.VolumeLabel.Length > 0)
                                label = driveInfo.VolumeLabel;
                            else
                                label = "Optical Drive";
                            imageIndex = 6;
                            break;
                        case DriveType.Removable:
                            if (driveInfo.IsReady && driveInfo.VolumeLabel.Length > 0)
                                label = driveInfo.VolumeLabel;
                            else
                                label = "Removable Drive";
                            imageIndex = 6;
                            break;
                        case DriveType.Network:
                            label = "Network Share";
                            imageIndex = 7;
                            break;
                        default:
                            label = "";
                            imageIndex = 8;
                            break;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    _driveReadiness[sublabel] = false;
                    label = "Network Share (access failure)";
                    imageIndex = 7;
                }
                TreeNode driveNode;
                if (label.Length > 0)
                    driveNode = myComputerNode.Nodes.Add( sublabel, String.Format( "{0} ({1})", label, sublabel ), imageIndex, imageIndex );
                else
                    driveNode = myComputerNode.Nodes.Add( sublabel, sublabel, imageIndex, imageIndex );
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
                        pathNode.Tag = String.Join( Path.DirectorySeparatorChar.ToString(),
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
                if (_listViewColumnSorter.Order == SortOrder.Ascending)
                    _listViewColumnSorter.Order = SortOrder.Descending;
                else
                    _listViewColumnSorter.Order = SortOrder.Ascending;
            } else
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
                Close();
                Application.DoEvents();
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
                                    return;
                                }
                                break;
                            }
                        }                        
                    }
                    // If location for this drive is not ready, stick with the current directory.
                    CurrentDirectory = _currentDirectory;
                    break;
            }
        }
    }
}