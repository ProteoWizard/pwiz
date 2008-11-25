using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using pwiz.CLI.msdata;

namespace seems
{
    public partial class OpenDataSourceDialog : Form
    {
        private ListViewColumnSorter listViewColumnSorter;

        public OpenDataSourceDialog()
        {
            InitializeComponent();

            listViewColumnSorter = new ListViewColumnSorter();
            listView.ListViewItemSorter = listViewColumnSorter;

            DialogResult = DialogResult.Cancel;

            string[] sourceTypes = new string[]
            {
                "Any spectra format",
				"mzML",
				//"mzData",
				"mzXML",
				"Thermo RAW",
                "Waters RAW",
				//"Analyst WIFF",
				//"Bruker YEP",
                //"Bruker BAF",
                //"Bruker FID",
				"Mascot Generic",
                "Bruker Data Exchange",
				//"Sequest DTA"
            };

            sourceTypeComboBox.Items.AddRange( sourceTypes );
            sourceTypeComboBox.SelectedIndex = 0;

            ImageList smallImageList = new ImageList();
            smallImageList.ColorDepth = ColorDepth.Depth32Bit;
            smallImageList.Images.Add( Properties.Resources.folder );
            smallImageList.Images.Add( Properties.Resources.file );
            smallImageList.Images.Add( Properties.Resources.DataProcessing );
            listView.SmallImageList = smallImageList;

            TreeView tv = new TreeView();
            tv.Indent = 8;
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
            CurrentDirectory = initialDirectory;
            return base.ShowDialog();
        }

        private Stack<string> previousDirectories = new Stack<string>();

        private string currentDirectory;
        private string CurrentDirectory
        {
            get { return currentDirectory; }
            set
            {
                if( Directory.Exists( value ) )
                {
                    currentDirectory = value;
                    populateListViewFromDirectory( currentDirectory );
                    populateComboBoxFromDirectory( currentDirectory );
                }
            }
        }

        private string initialDirectory;
        public string InitialDirectory
        {
            get { return initialDirectory; }
            set { initialDirectory = value; }
        }

        public string DataSource
        {
            get { return dataSources[0]; }
        }

        private string[] dataSources;
        public string[] DataSources
        {
            get { return dataSources; }
        }

        private class SourceInfo
        {
            public string name;
            public string type;
            public int spectra;
            public UInt64 size;
            public DateTime dateModified;
            public string ionSource;
            public string analyzer;
            public string detector;
            public string contentType;

            public void populateFromMSData( MSData msInfo )
            {
                spectra = msInfo.run.spectrumList.size();
                ionSource = analyzer = detector = "";
                foreach( InstrumentConfiguration ic in msInfo.instrumentConfigurationList )
                {
                    SortedDictionary<int, string> ionSources = new SortedDictionary<int, string>();
                    SortedDictionary<int, string> analyzers = new SortedDictionary<int, string>();
                    SortedDictionary<int, string> detectors = new SortedDictionary<int, string>();
                    foreach( pwiz.CLI.msdata.Component c in ic.componentList )
                    {
                        CVParam term;
                        switch( c.type )
                        {
                            case ComponentType.ComponentType_Source:
                                term = c.cvParamChild( CVID.MS_ionization_type );
                                if( !term.empty() )
                                    ionSources.Add( c.order, term.name );
                                break;
                            case ComponentType.ComponentType_Analyzer:
                                term = c.cvParamChild( CVID.MS_mass_analyzer_type );
                                if( !term.empty() )
                                    analyzers.Add( c.order, term.name );
                                break;
                            case ComponentType.ComponentType_Detector:
                                term = c.cvParamChild( CVID.MS_detector_type );
                                if( !term.empty() )
                                    detectors.Add( c.order, term.name );
                                break;
                        }
                    }

                    if( ionSource.Length > 0 )
                        ionSource += ", ";
                    ionSource += String.Join( "/", new List<string>( ionSources.Values ).ToArray() );

                    if( analyzer.Length > 0 )
                        analyzer += ", ";
                    analyzer += String.Join( "/", new List<string>( analyzers.Values ).ToArray() );

                    if( detector.Length > 0 )
                        detector += ", ";
                    detector += String.Join( "/", new List<string>( detectors.Values ).ToArray() );
                }

                System.Collections.Generic.Set<string> contentTypes = new System.Collections.Generic.Set<string>();
                CVParamList cvParams = msInfo.fileDescription.fileContent.cvParams;
                if( cvParams.Count > 0 )
                {
                    foreach( CVParam term in msInfo.fileDescription.fileContent.cvParams )
                        contentTypes.Add( term.name );
                    contentType = String.Join( ", ", new List<string>( contentTypes.Keys ).ToArray() );
                }
            }

            public string[] ToArray()
            {
                if( type == "File Folder" )
                {
                    return new string[]
                    {
                        name,
                        type,
                        "",
                        "",
                        String.Format( "{0} {1}", dateModified.ToShortDateString(),
                                                  dateModified.ToShortTimeString()),
                        "",
                        "",
                        "",
                        ""
                    };
                } else
                {
                    return new string[]
                    {
                        name,
                        type,
                        spectra.ToString(),
                        String.Format( new FileSizeFormatProvider(), "{0:fs}", size ),
                        String.Format( "{0} {1}", dateModified.ToShortDateString(),
                                                  dateModified.ToShortTimeString() ),
                        ionSource,
                        analyzer,
                        detector,
                        contentType
                    };
                }
            }
        }

        private string getSourceType( DirectoryInfo dirInfo )
        {
            try
            {
                string type = MSDataFile.identify( dirInfo.FullName );
                if( type == String.Empty )
                    return "File Folder";
                return type;
            } catch
            {
                return "";
            }
        }

        private string getSourceType( FileInfo fileInfo )
        {
            try
            {
                return MSDataFile.identify( fileInfo.FullName );
            } catch
            {
                return "";
            }
        }

        private SourceInfo getSourceInfo( DirectoryInfo dirInfo )
        {
            SourceInfo sourceInfo = new SourceInfo();
            sourceInfo.type = getSourceType( dirInfo );
            sourceInfo.name = dirInfo.Name;
            sourceInfo.dateModified = dirInfo.LastWriteTime;

            if( listView.View != View.Details ||
                ( sourceTypeComboBox.SelectedIndex > 0 &&
                  sourceTypeComboBox.SelectedItem.ToString() != sourceInfo.type ) )
                return sourceInfo;

            if( sourceInfo.type == "File Folder" )
            {
                return sourceInfo;
            } else if( sourceInfo.type != String.Empty )
            {
                try
                {
                    //MSDataFile msInfo = new MSDataFile( dirInfo.FullName );
                    //sourceInfo.populateFromMSData( msInfo );
                    
                } catch
                {
                    sourceInfo.spectra = 0;
                    sourceInfo.type = "Invalid " + sourceInfo.type;
                }

                sourceInfo.size = 0;
                foreach( FileInfo fileInfo in dirInfo.GetFiles("*", SearchOption.AllDirectories) )
                    sourceInfo.size += (UInt64) fileInfo.Length;
                return sourceInfo;
            }
            return null;
        }

        private SourceInfo getSourceInfo( FileInfo fileInfo )
        {
            SourceInfo sourceInfo = new SourceInfo();
            sourceInfo.type = getSourceType( fileInfo );
            sourceInfo.name = fileInfo.Name;
            if( sourceInfo.type != String.Empty )
            {
                if( listView.View != View.Details ||
                    ( sourceTypeComboBox.SelectedIndex > 0 &&
                      sourceTypeComboBox.SelectedItem.ToString() != sourceInfo.type ) )
                    return sourceInfo;

                try
                {
                    MSDataFile msInfo = new MSDataFile( fileInfo.FullName );
                    sourceInfo.populateFromMSData( msInfo );
                } catch
                {
                    sourceInfo.spectra = 0;
                    sourceInfo.type = "Invalid " + sourceInfo.type;
                }

                sourceInfo.size = (UInt64) fileInfo.Length;
                sourceInfo.dateModified = fileInfo.LastWriteTime;
                return sourceInfo;
            }
            return null;
        }

        private bool abortPopulateList;
        private void populateListViewFromDirectory( string directory )
        {
            abortPopulateList = false;
            listView.Items.Clear();
            DirectoryInfo dirInfo = new DirectoryInfo( directory );

            // subitems: Name, Type, Spectra, Size, Date Modified
            foreach( DirectoryInfo subdirInfo in dirInfo.GetDirectories() )
            {
                try
                {
                    SourceInfo sourceInfo = getSourceInfo( subdirInfo );
                    if( sourceInfo != null )
                    {
                        if( sourceInfo.type == "File Folder" ||
                            sourceTypeComboBox.SelectedIndex == 0 ||
                            sourceTypeComboBox.SelectedItem.ToString() == sourceInfo.type )
                        {
                            ListViewItem item;
                            if( sourceInfo.type == "File Folder" )
                                item = new ListViewItem( sourceInfo.ToArray(), 0 );
                            else
                                item = new ListViewItem( sourceInfo.ToArray(), 2 );
                            item.SubItems[3].Tag = (object) sourceInfo.size;
                            item.SubItems[4].Tag = (object) sourceInfo.dateModified;
                            listView.Items.Add( item );
                        }
                        Application.DoEvents();
                        if( abortPopulateList )
                        {
                            //MessageBox.Show( "abort" );
                            abortPopulateList = false;
                            break;
                        }
                    }
                } catch
                {
                    // skip errors
                }
            }

            foreach( FileInfo fileInfo in dirInfo.GetFiles() )
            {
                SourceInfo sourceInfo = getSourceInfo( fileInfo );
                if( sourceInfo != null &&
                    ( sourceTypeComboBox.SelectedIndex == 0 ||
                      sourceTypeComboBox.SelectedItem.ToString() == sourceInfo.type ) )
                {
                    ListViewItem item = new ListViewItem( sourceInfo.ToArray(), 2 );
                    item.SubItems[3].Tag = (object) sourceInfo.size;
                    item.SubItems[4].Tag = (object) sourceInfo.dateModified;
                    listView.Items.Add( item );
                }
                Application.DoEvents();
                if( abortPopulateList )
                {
                    //MessageBox.Show( "abort" );
                    abortPopulateList = false;
                    break;
                }
            }
        }

        private void populateComboBoxFromDirectory( string directory )
        {
            lookInComboBox.SuspendLayout();

            // TODO: get network share info

            // remove old drive entries
            while( lookInComboBox.Items.Count > 4 )
                lookInComboBox.Items.RemoveAt( 4 );

            DirectoryInfo dirInfo = new DirectoryInfo(directory);

            // fill tree view with special locations and drives
            TreeNode myComputerNode = lookInComboBox.Items[3] as TreeNode;
            int driveCount = 0;
            foreach( DriveInfo driveInfo in DriveInfo.GetDrives() )
            {
                string label;
                string sublabel = driveInfo.Name;
                driveReadiness[sublabel] = driveInfo.IsReady;
                int imageIndex;
                ++driveCount;
                switch( driveInfo.DriveType )
                {
                    case DriveType.Fixed:
                        if( driveInfo.VolumeLabel.Length > 0 )
                            label = driveInfo.VolumeLabel;
                        else
                            label = "Local Drive";
                        imageIndex = 5;
                        break;
                    case DriveType.CDRom:
                        if( driveInfo.IsReady && driveInfo.VolumeLabel.Length > 0 )
                            label = driveInfo.VolumeLabel;
                        else
                            label = "Optical Drive";
                        imageIndex = 6;
                        break;
                    case DriveType.Removable:
                        if( driveInfo.IsReady && driveInfo.VolumeLabel.Length > 0 )
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
                TreeNode driveNode;
                if( label.Length > 0 )
                    driveNode = myComputerNode.Nodes.Add( sublabel, String.Format( "{0} ({1})", label, sublabel ), imageIndex, imageIndex );
                else
                    driveNode = myComputerNode.Nodes.Add( sublabel, sublabel, imageIndex, imageIndex );
                driveNode.Tag = sublabel;
                lookInComboBox.Items.Insert( 3 + driveCount, driveNode );

                if( sublabel == dirInfo.Root.Name )
                {
                    List<string> branches = new List<string>( directory.Split( new char[] {
                                                 Path.DirectorySeparatorChar,
                                                 Path.AltDirectorySeparatorChar },
                                                 StringSplitOptions.RemoveEmptyEntries ) );
                    TreeNode pathNode = driveNode;
                    for( int i = 1; i < branches.Count; ++i )
                    {
                        ++driveCount;
                        pathNode = pathNode.Nodes.Add( branches[i], branches[i], 8, 8 );
                        pathNode.Tag = String.Join( Path.DirectorySeparatorChar.ToString(),
                                                    branches.GetRange( 0, i ).ToArray() );
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
                            dataSources = sourcePaths;
                            DialogResult = DialogResult.OK;
                            Close();
                        } else
                            MessageBox.Show( String.Join( "\r\n", invalidPaths.ToArray() ), "Some source paths are invalid" );
                    }
                    break;
                case Keys.F5:
                    abortPopulateList = true;
                    populateListViewFromDirectory( currentDirectory ); // refresh
                    break;
            }
        }

        private void listView_ItemActivate( object sender, EventArgs e )
        {
            ListViewItem item = listView.SelectedItems[0];
            if( item.SubItems[1].Text == "File Folder" )
            {
                if( currentDirectory != null )
                    previousDirectories.Push( currentDirectory ); 
                CurrentDirectory = Path.Combine( CurrentDirectory, item.SubItems[0].Text );
                abortPopulateList = true;
            }  else
            {
                dataSources = new string[] { Path.Combine( CurrentDirectory, item.SubItems[0].Text ) };
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void listView_ColumnClick( object sender, ColumnClickEventArgs e )
        {
            // Determine if the clicked column is already the column that is being sorted.
            if( e.Column == listViewColumnSorter.SortColumn )
            {
                // Reverse the current sort direction for this column.
                if( listViewColumnSorter.Order == SortOrder.Ascending )
                    listViewColumnSorter.Order = SortOrder.Descending;
                else
                    listViewColumnSorter.Order = SortOrder.Ascending;
            } else
            {
                // Set the column number that is to be sorted; default to ascending.
                listViewColumnSorter.SortColumn = e.Column;
                listViewColumnSorter.Order = SortOrder.Ascending;
            }

            // Perform the sort with these new sort options.
            listView.Sort();
        }

        private void openButton_Click( object sender, EventArgs e )
        {
            if( listView.SelectedItems.Count > 0 )
            {
                List<string> dataSourceList = new List<string>();
                foreach( ListViewItem item in listView.SelectedItems )
                {
                    if( item.SubItems[1].Text != "File Folder" )
                        dataSourceList.Add( Path.Combine( CurrentDirectory, item.SubItems[0].Text ) );
                }
                dataSources = dataSourceList.ToArray();
                abortPopulateList = true;
                DialogResult = DialogResult.OK;
                Close();
                Application.DoEvents();
            } else
                MessageBox.Show( "Please select one or more data sources.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
        }

        private void cancelButton_Click( object sender, EventArgs e )
        {
            abortPopulateList = true;
            DialogResult = DialogResult.Cancel;
            Close();
            Application.DoEvents();
        }

        private void smallIconsToolStripMenuItem_Click( object sender, EventArgs e )
        {
            foreach( ToolStripDropDownItem item in viewsDropDownButton.DropDownItems )
                ( item as ToolStripMenuItem ).Checked = false;
            ( viewsDropDownButton.DropDownItems[0] as ToolStripMenuItem ).Checked = true;
            listView.View = View.SmallIcon;
        }

        private void largeIconsToolStripMenuItem_Click( object sender, EventArgs e )
        {
            foreach( ToolStripDropDownItem item in viewsDropDownButton.DropDownItems )
                ( item as ToolStripMenuItem ).Checked = false;
            ( viewsDropDownButton.DropDownItems[1] as ToolStripMenuItem ).Checked = true;
            listView.View = View.LargeIcon;
        }

        private void tilesToolStripMenuItem_Click( object sender, EventArgs e )
        {
            foreach( ToolStripDropDownItem item in viewsDropDownButton.DropDownItems )
                ( item as ToolStripMenuItem ).Checked = false;
            ( viewsDropDownButton.DropDownItems[2] as ToolStripMenuItem ).Checked = true;
            listView.View = View.Tile;
        }

        private void listToolStripMenuItem_Click( object sender, EventArgs e )
        {
            foreach( ToolStripDropDownItem item in viewsDropDownButton.DropDownItems )
                ( item as ToolStripMenuItem ).Checked = false;
            ( viewsDropDownButton.DropDownItems[3] as ToolStripMenuItem ).Checked = true;
            listView.View = View.List;
        }

        private void detailsToolStripMenuItem_Click( object sender, EventArgs e )
        {
            if( listView.View != View.Details )
            {
                foreach( ToolStripDropDownItem item in viewsDropDownButton.DropDownItems )
                    ( item as ToolStripMenuItem ).Checked = false;
                ( viewsDropDownButton.DropDownItems[4] as ToolStripMenuItem ).Checked = true;
                listView.View = View.Details;
                populateListViewFromDirectory( currentDirectory );
            }
        }

        private void upOneLevelButton_Click( object sender, EventArgs e )
        {
            DirectoryInfo parentDirectory = Directory.GetParent(currentDirectory);
            if( parentDirectory == null )
            {
                parentDirectory = new DirectoryInfo( Environment.GetFolderPath( Environment.SpecialFolder.MyComputer ) );
            }

            if( parentDirectory != null && parentDirectory.FullName != currentDirectory )
            {
                previousDirectories.Push( currentDirectory );
                CurrentDirectory = parentDirectory.FullName;
            }
        }

        private void backButton_Click( object sender, EventArgs e )
        {
            if( previousDirectories.Count > 0 )
                CurrentDirectory = previousDirectories.Pop();
        }

        private void listView_ItemSelectionChanged( object sender, ListViewItemSelectionChangedEventArgs e )
        {
            if( listView.SelectedItems.Count > 1 )
            {
                List<string> dataSourceList = new List<string>();
                foreach( ListViewItem item in listView.SelectedItems )
                {
                    if( item.SubItems[1].Text != "File Folder" )
                        dataSourceList.Add( String.Format( "\"{0}\"", item.SubItems[0].Text ) );
                }
                sourcePathTextBox.Text = String.Join( " ", dataSourceList.ToArray() );
            } else if( listView.SelectedItems.Count > 0 )
            {
                sourcePathTextBox.Text = listView.SelectedItems[0].SubItems[0].Text;
            } else
                sourcePathTextBox.Text = "";
        }

        private void sourceTypeComboBox_SelectionChangeCommitted( object sender, EventArgs e )
        {
            populateListViewFromDirectory( currentDirectory );
            abortPopulateList = true;
        }

        private void listView_KeyDown( object sender, KeyEventArgs e )
        {
            switch( e.KeyCode )
            {
                case Keys.F5:
                    populateListViewFromDirectory( currentDirectory ); // refresh
                    abortPopulateList = true;
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

        }

        private void myNetworkPlacesButton_Click( object sender, EventArgs e )
        {

        }

        System.Collections.Generic.Map<string, bool> driveReadiness = new System.Collections.Generic.Map<string, bool>();
        private void lookInComboBox_DropDown( object sender, EventArgs e )
        {
            
        }

        private void lookInComboBox_DrawItem( object sender, DrawItemEventArgs e )
        {
            if( e.Index < 0 )
                return;

            TreeNode node = lookInComboBox.Items[e.Index] as TreeNode;

            int x, y, indent;
            if( ( e.State & DrawItemState.ComboBoxEdit ) == DrawItemState.ComboBoxEdit )
            {
                x = 2;
                y = 2;
                indent = 0;
            } else
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

            TreeNode node = lookInComboBox.Items[e.Index] as TreeNode;
            int x = node.TreeView.Indent / 2;
            int indent = node.TreeView.Indent * node.Level;
            e.ItemHeight = 16;
            e.ItemWidth = x + indent + 16 + (int) e.Graphics.MeasureString( node.Text, lookInComboBox.Font ).Width;
        }

        private void lookInComboBox_SelectionChangeCommitted( object sender, EventArgs e )
        {
            if( lookInComboBox.SelectedIndex < 0 )
                lookInComboBox.SelectedIndex = 0;

            string location = ( lookInComboBox.SelectedItem as TreeNode ).Tag as string;
            switch( location )
            {
                case "My Recent Documents":
                    CurrentDirectory = Environment.GetFolderPath( Environment.SpecialFolder.Recent );
                    break;
                case "Desktop":
                    CurrentDirectory = Environment.GetFolderPath( Environment.SpecialFolder.DesktopDirectory );
                    break;
                case "My Documents":
                    CurrentDirectory = Environment.GetFolderPath( Environment.SpecialFolder.Personal );
                    break;
                case "My Computer":
                    break;
                case "My Network Places":
                    break;
                default:
                    if( driveReadiness[location] )
                        CurrentDirectory = location;
                    else
                        CurrentDirectory = currentDirectory;
                    break;
            }
        }
    }
}