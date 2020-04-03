//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Threading;
using pwiz.CLI.cv;
using pwiz.CLI.data;
using pwiz.CLI.msdata;
using pwiz.MSGraph;

namespace seems
{
    public partial class OpenDataSourceDialog : Form
    {
        private ListViewColumnSorter listViewColumnSorter;
        private BackgroundWorker backgroundSourceLoader;
        private MSGraphControl ticGraphControl;

        private class BackgroundSourceLoaderArgs
        {
            public BackgroundSourceLoaderArgs()
            {
                sourceDirectories = new List<DirectoryInfo>();
                sourceFiles = new List<FileInfo>();
                sourceTypeFilter = null;
                getDetails = false;
            }

            public SourceInfo[] sourceInfoList;

            /// <summary>
            /// Lists paths to Waters, Bruker, Agilent, etc. source directories
            /// </summary>
            public List<DirectoryInfo> SourceDirectories
            {
                get { return sourceDirectories; }
                set { sourceDirectories = value; }
            }
            private List<DirectoryInfo> sourceDirectories;

            /// <summary>
            /// Lists paths to mzML, mzXML, Thermo, etc. source files
            /// </summary>
            public List<FileInfo> SourceFiles
            {
                get { return sourceFiles; }
                set { sourceFiles = value; }
            }
            private List<FileInfo> sourceFiles;

            /// <summary>
            /// The total number of source directories and files.
            /// </summary>
            public int TotalSourceCount { get { return sourceDirectories.Count + sourceFiles.Count; } }

            /// <summary>
            /// If not null or empty, this string must match getSourceType() for a source to pass the filter
            /// </summary>
            public string SourceTypeFilter
            {
                get { return sourceTypeFilter; }
                set { sourceTypeFilter = value; }
            }
            private string sourceTypeFilter;

            /// <summary>
            /// If true, will open files to get some file-level metadata
            /// </summary>
            public bool GetDetails
            {
                get { return getDetails; }
                set { getDetails = value; }
            }
            private bool getDetails;
        }

        public OpenDataSourceDialog()
        {
            InitializeComponent();

            listViewColumnSorter = new ListViewColumnSorter();
            listView.ListViewItemSorter = listViewColumnSorter;

            DialogResult = DialogResult.Cancel;

            var sourceTypes = new List<string>();
            foreach (var typeExtsPair in ReaderList.FullReaderList.getFileExtensionsByType())
                if (typeExtsPair.Value.Count > 0) // e.g. exclude UNIFI
                    sourceTypes.Add(typeExtsPair.Key);
            sourceTypes.Sort();
            sourceTypes.Insert(0, "Any spectra format");

            sourceTypeComboBox.Items.AddRange( sourceTypes.ToArray() );
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

            ticGraphControl = new MSGraphControl()
            {
                Dock = DockStyle.Fill,
            };
            ticGraphControl.GraphPane.Legend.IsVisible = false;
            ticGraphControl.Visible = false;
            splitContainer1.Panel2.Controls.Add( ticGraphControl );
            splitContainer1.Panel2Collapsed = false;
        }

        void initializeBackgroundSourceLoader()
        {
            if( backgroundSourceLoader != null )
                backgroundSourceLoader.CancelAsync();

            backgroundSourceLoader = new BackgroundWorker();
            backgroundSourceLoader.WorkerReportsProgress = true;
            backgroundSourceLoader.WorkerSupportsCancellation = true;
            backgroundSourceLoader.DoWork += new DoWorkEventHandler( backgroundSourceLoader_DoWork );
            backgroundSourceLoader.ProgressChanged += new ProgressChangedEventHandler( backgroundSourceLoader_ProgressChanged );
        }

        void backgroundSourceLoader_ProgressChanged( object sender, ProgressChangedEventArgs arg )
        {
            if( ( sender as BackgroundWorker ).CancellationPending )
                return;

            SourceInfo[] sourceInfoList = arg.UserState as SourceInfo[];
            if( sourceInfoList != null )
            {
                foreach( SourceInfo sourceInfo in sourceInfoList )
                {
                    // mandatory first pass will be without details for a quick listing;
                    // optional second pass will have details of already listed items

                    if( !sourceInfo.hasDetails ) // first pass
                    {
                        if( sourceInfo.type == "File Folder" ||
                            sourceTypeComboBox.SelectedIndex == 0 ||
                            sourceTypeComboBox.SelectedItem.ToString() == sourceInfo.type )
                        {
                            // subitems: Name, Type, Spectra, Size, Date Modified
                            ListViewItem item;
                            if( sourceInfo.type == "File Folder" )
                                item = new ListViewItem( sourceInfo.ToArray(), 0 );
                            else
                                item = new ListViewItem( sourceInfo.ToArray(), 2 );
                            item.SubItems[3].Tag = (object) sourceInfo.size;
                            item.SubItems[4].Tag = (object) sourceInfo.dateModified;
                            item.Name = sourceInfo.name;
                            listView.Items.Add( item );
                        }
                    } else // second pass
                    {
                        ListViewItem item = listView.Items[sourceInfo.name];
                        if( item == null ) // a virtual document from a multi-run source
                        {
                            if( sourceInfo.type == "File Folder" )
                                item = new ListViewItem( sourceInfo.ToArray(), 0 );
                            else
                                item = new ListViewItem( sourceInfo.ToArray(), 2 );
                            item.SubItems[3].Tag = (object) sourceInfo.size;
                            item.SubItems[4].Tag = (object) sourceInfo.dateModified;
                            item.Name = sourceInfo.name;
                            listView.Items.Add( item );
                        }

                        if( sourceInfo.type != "File Folder" )
                        {
                            item.SubItems[2].Text = sourceInfo.spectra.ToString();
                            item.SubItems[3].Tag = (object) sourceInfo.size;
                            item.SubItems[4].Tag = (object) sourceInfo.dateModified;
                            item.SubItems[5].Text = sourceInfo.ionSource;
                            item.SubItems[6].Text = sourceInfo.analyzer;
                            item.SubItems[7].Text = sourceInfo.detector;
                            item.SubItems[8].Text = sourceInfo.contentType;
                        }
                    }
                }
            }
        }

        void backgroundSourceLoader_DoWork( object sender, DoWorkEventArgs arg )
        {
            var worker = sender as BackgroundWorker;
            var workerArgs = arg.Argument as BackgroundSourceLoaderArgs;

            var directoriesPassingFilter = new List<DirectoryInfo>();
            var filesPassingFilter = new List<FileInfo>();

            for( int i = 0; i < workerArgs.SourceDirectories.Count && !backgroundSourceLoader.CancellationPending; ++i )
            {
                try
                {
                    DirectoryInfo directory = workerArgs.SourceDirectories[i];
                    FileInfo[] files = directory.GetFiles(); // trigger unauthorized access

                    SourceInfo[] sourceInfo = getSourceInfo( directory, false );
                    if( sourceInfo == null ||
                        sourceInfo.Length == 0 ||
                        ( !String.IsNullOrEmpty( workerArgs.SourceTypeFilter ) &&
                         sourceInfo[0].type != "File Folder" &&
                         sourceInfo[0].type != workerArgs.SourceTypeFilter ) )
                        continue;
                    directoriesPassingFilter.Add( directory );
                    worker.ReportProgress( 0, (object) sourceInfo );
                } catch
                {
                    // ignore directories we don't have permission for
                }
            }

            for( int i = 0; i < workerArgs.SourceFiles.Count && !backgroundSourceLoader.CancellationPending; ++i )
            {
                SourceInfo[] sourceInfo = getSourceInfo( workerArgs.SourceFiles[i], false );
                if( sourceInfo == null ||
                    sourceInfo.Length == 0 ||
                    ( !String.IsNullOrEmpty( workerArgs.SourceTypeFilter ) &&
                     sourceInfo[0].type != "File Folder" &&
                     sourceInfo[0].type != workerArgs.SourceTypeFilter ) )
                    continue;
                filesPassingFilter.Add( workerArgs.SourceFiles[i] );
                worker.ReportProgress( 0, (object) sourceInfo );
            }

            if( workerArgs.GetDetails )
            {
                for( int i = 0; i < directoriesPassingFilter.Count && !backgroundSourceLoader.CancellationPending; ++i )
                    worker.ReportProgress( 0, (object) getSourceInfo( directoriesPassingFilter[i], true ) );

                for( int i = 0; i < filesPassingFilter.Count && !backgroundSourceLoader.CancellationPending; ++i )
                    worker.ReportProgress( 0, (object) getSourceInfo( filesPassingFilter[i], true ) );
            }

            arg.Cancel = worker.CancellationPending;
        }

        public new DialogResult ShowDialog()
        {
            CurrentDirectory = initialDirectory;
            return base.ShowDialog();
        }

        private Stack<string> previousDirectories = new Stack<string>();

        #region Properties
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
        #endregion

        private class SourceInfo
        {
            public string name;
            public string type;
            public UInt64 size;
            public DateTime dateModified;

            public bool hasDetails;
            public int spectra;
            public string ionSource;
            public string analyzer;
            public string detector;
            public string contentType;

            public void populateFromMSData( MSData msInfo )
            {
                hasDetails = true;
                spectra = msInfo.run.spectrumList == null ? 0 : msInfo.run.spectrumList.size();
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

        private string getSourceType( string path )
        {
            if( File.Exists( path ) )
                return getSourceType( new FileInfo( path ) );
            else if( Directory.Exists( path ) )
                return getSourceType( new DirectoryInfo( path ) );
            else
                throw new ArgumentException( "path is not a file or a directory" );
        }

        private string getSourceType( DirectoryInfo dirInfo )
        {
            try
            {
                string type = ReaderList.FullReaderList.identify( dirInfo.FullName );
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
                return ReaderList.FullReaderList.identify( fileInfo.FullName );
            } catch (Exception)
            {
                return "";
            }
        }

        private SourceInfo[] getSourceInfo( DirectoryInfo dirInfo, bool getDetails )
        {
            var sourceInfoList = new List<SourceInfo>();
            sourceInfoList.Add( new SourceInfo() );
            sourceInfoList[0].type = getSourceType( dirInfo );
            sourceInfoList[0].name = dirInfo.Name;
            sourceInfoList[0].dateModified = dirInfo.LastWriteTime;
            sourceInfoList[0].hasDetails = getDetails;

            if( !getDetails )
                return sourceInfoList.ToArray();

            if( sourceInfoList[0].type == "File Folder" )
            {
                return sourceInfoList.ToArray();
            } else if( sourceInfoList[0].type != String.Empty )
            {
                try
                {
                    MSDataFile msInfo = new MSDataFile( dirInfo.FullName );
                    sourceInfoList[0].populateFromMSData( msInfo );

                } catch( ThreadAbortException )
                {
                    return null;
                } catch
                {
                    sourceInfoList[0].spectra = 0;
                    sourceInfoList[0].type = "Invalid " + sourceInfoList[0].type;
                }

                sourceInfoList[0].size = 0;
                sourceInfoList[0].dateModified = DateTime.MinValue;
                foreach( FileInfo fileInfo in dirInfo.GetFiles( "*", SearchOption.AllDirectories ) )
                {
                    sourceInfoList[0].size += (UInt64) fileInfo.Length;
                    if( fileInfo.LastWriteTime > sourceInfoList[0].dateModified )
                        sourceInfoList[0].dateModified = fileInfo.LastWriteTime;
                }
                return sourceInfoList.ToArray();
            }
            return null;
        }

        private SourceInfo[] getSourceInfo( FileInfo fileInfo, bool getDetails )
        {
            var sourceInfoList = new List<SourceInfo>();
            sourceInfoList.Add( new SourceInfo() );
            sourceInfoList[0].type = getSourceType( fileInfo );
            sourceInfoList[0].name = fileInfo.Name;
            sourceInfoList[0].hasDetails = getDetails;
            sourceInfoList[0].size = (UInt64) fileInfo.Length;
            sourceInfoList[0].dateModified = fileInfo.LastWriteTime;
            if( sourceInfoList[0].type != String.Empty )
            {
                if( !getDetails )
                    return sourceInfoList.ToArray();

                try
                {
                    ReaderList readerList = ReaderList.FullReaderList;
                    var readerConfig = new ReaderConfig
                    {
                        simAsSpectra = Properties.Settings.Default.SimAsSpectra,
                        srmAsSpectra = Properties.Settings.Default.SrmAsSpectra,
                        combineIonMobilitySpectra = Properties.Settings.Default.CombineIonMobilitySpectra,
                        ignoreZeroIntensityPoints = Properties.Settings.Default.IgnoreZeroIntensityPoints,
                        acceptZeroLengthSpectra = Properties.Settings.Default.AcceptZeroLengthSpectra,
                        allowMsMsWithoutPrecursor = false
                    };

                    MSDataList msInfo = new MSDataList();
                    readerList.read( fileInfo.FullName, msInfo, readerConfig );

                    foreach( MSData msData in msInfo )
                    {
                        SourceInfo sourceInfo = new SourceInfo();
                        sourceInfo.type = sourceInfoList[0].type;
                        sourceInfo.name = sourceInfoList[0].name;
                        if( msInfo.Count > 1 )
                            sourceInfo.name += " (" + msData.run.id + ")";
                        sourceInfo.populateFromMSData( msData );
                        sourceInfoList.Add( sourceInfo );
                    }
                } catch
                {
                    sourceInfoList[0].spectra = 0;
                    sourceInfoList[0].type = "Invalid " + sourceInfoList[0].type;
                }

                foreach( SourceInfo sourceInfo in sourceInfoList )
                {
                    sourceInfo.size = (UInt64) fileInfo.Length;
                    sourceInfo.dateModified = fileInfo.LastWriteTime;
                }
                return sourceInfoList.ToArray();
            }
            return null;
        }


        private void populateListViewFromDirectory( string directory )
        {
            initializeBackgroundSourceLoader();
            listView.Items.Clear();
            DirectoryInfo dirInfo = new DirectoryInfo( directory );

            BackgroundSourceLoaderArgs args = new BackgroundSourceLoaderArgs();
            args.GetDetails = listView.View == View.Details;
            if( sourceTypeComboBox.SelectedIndex > 0 )
                args.SourceTypeFilter = sourceTypeComboBox.SelectedItem.ToString();

            foreach( DirectoryInfo subdirInfo in dirInfo.GetDirectories() )
                args.SourceDirectories.Add( subdirInfo );

            foreach( FileInfo fileInfo in dirInfo.GetFiles() )
                args.SourceFiles.Add( fileInfo );

            backgroundSourceLoader.RunWorkerAsync( args );
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
                // skip this drive if there's a problem accessing its properties
                try { var foo = driveInfo.VolumeLabel; } catch { continue; }

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

        // Credit to: http://stackoverflow.com/a/14247337/638445
        public static List<string> SplitOnSpacesWithQuoteEscaping(string input)
        {
            List<string> split = new List<string>();
            StringBuilder sb = new StringBuilder();
            bool splitOnQuote = false;
            char quote = '"';
            char space = ' ';
            foreach (char c in input.ToCharArray())
            {
                if (splitOnQuote)
                {
                    if (c == quote)
                    {
                        if (sb.Length > 0)
                        {
                            split.Add(sb.ToString());
                            sb.Clear();
                        }
                        splitOnQuote = false;
                    }
                    else { sb.Append(c); }
                }
                else
                {
                    if (c == space)
                    {
                        if (sb.Length > 0)
                        {
                            split.Add(sb.ToString());
                            sb.Clear();
                        }
                    }
                    else if (c == quote)
                    {
                        if (sb.Length > 0)
                        {
                            split.Add(sb.ToString());
                            sb.Clear();
                        }
                        splitOnQuote = true;
                    }
                    else { sb.Append(c); }
                }
            }
            if (sb.Length > 0) split.Add(sb.ToString());
            return split;
        }

        private void sourcePathTextBox_KeyUp( object sender, KeyEventArgs e )
        {
            // if sourcePathTextBox.Text as a whole is a file or directory, treat it as a single path;
            // otherwise split it up by spaces with quote escaping
            List<string> sourcePaths = File.Exists(sourcePathTextBox.Text) || Directory.Exists(sourcePathTextBox.Text) ?
                                       new List<string> { sourcePathTextBox.Text } :
                                       SplitOnSpacesWithQuoteEscaping(sourcePathTextBox.Text);
            switch( e.KeyCode )
            {
                case Keys.Enter:
                    if( sourcePaths.Count == 0 )
                        return;
                    else if( sourcePaths.Count == 1 && Directory.Exists(sourcePaths[0]) )
                        CurrentDirectory = sourcePaths[0];
                    else if( sourcePaths.Count == 1 && Directory.Exists( Path.Combine( CurrentDirectory, sourcePaths[0] ) ) )
                        CurrentDirectory = Path.Combine( CurrentDirectory, sourcePaths[0] );
                    else
                    {
                        // check that all manually-entered paths are valid
                        List<string> invalidPaths = new List<string>();
                        foreach( string path in sourcePaths )
                            if( !File.Exists( path ) && !File.Exists( Path.Combine( CurrentDirectory, path ) ) )
                                invalidPaths.Add( path );

                        if( invalidPaths.Count == 0 )
                        {
                            dataSources = sourcePaths.ToArray();
                            DialogResult = DialogResult.OK;
                            Close();
                        } else
                            MessageBox.Show( String.Join( "\r\n", invalidPaths.ToArray() ), "Some source paths are invalid" );
                    }
                    break;
                case Keys.F5:
                    populateListViewFromDirectory( currentDirectory ); // refresh
                    break;
            }
        }

        #region ListView handlers
        private void listView_ItemActivate( object sender, EventArgs e )
        {
            if( listView.SelectedItems.Count == 0 )
                return;

            ListViewItem item = listView.SelectedItems[0];
            if( item.SubItems[1].Text == "File Folder" )
            {
                if( currentDirectory != null )
                    previousDirectories.Push( currentDirectory ); 
                CurrentDirectory = Path.Combine( CurrentDirectory, item.SubItems[0].Text );
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

                ticGraphControl.GraphPane.GraphObjList.Clear();
                ticGraphControl.GraphPane.CurveList.Clear();
                ticGraphControl.Visible = false;

            } else if( listView.SelectedItems.Count > 0 )
            {
                sourcePathTextBox.Text = listView.SelectedItems[0].SubItems[0].Text;

                ticGraphControl.GraphPane.GraphObjList.Clear();
                ticGraphControl.GraphPane.CurveList.Clear();

                string sourcePath = Path.Combine( CurrentDirectory, sourcePathTextBox.Text );
                string sourceType = getSourceType( sourcePath );
                if( !String.IsNullOrEmpty( sourceType ) &&
                    sourceType != "File Folder" )
                {
                    using (MSData msd = new MSData())
                    {
                        ReaderList.FullReaderList.read(sourcePath, msd, 0, SpectrumSource.GetReaderConfig());
                        using (ChromatogramList cl = msd.run.chromatogramList)
                        {
                            if( cl != null && !cl.empty() && cl.find( "TIC" ) != cl.size() )
                            {
                                ticGraphControl.Visible = true;
                                pwiz.CLI.msdata.Chromatogram tic = cl.chromatogram( cl.find( "TIC" ), true );
                                Map<double, double> sortedFullPointList = new Map<double, double>();
                                IList<double> timeList = tic.binaryDataArrays[0].data;
                                IList<double> intensityList = tic.binaryDataArrays[1].data;
                                int arrayLength = timeList.Count;
                                for( int i = 0; i < arrayLength; ++i )
                                    sortedFullPointList[timeList[i]] = intensityList[i];
                                ZedGraph.PointPairList points = new ZedGraph.PointPairList(
                                    new List<double>( sortedFullPointList.Keys ).ToArray(),
                                    new List<double>( sortedFullPointList.Values ).ToArray() );
                                ZedGraph.LineItem item = ticGraphControl.GraphPane.AddCurve( "TIC", points, Color.Black, ZedGraph.SymbolType.None );
                                item.Line.IsAntiAlias = true;
                                ticGraphControl.AxisChange();
                                ticGraphControl.Refresh();
                            } else
                                ticGraphControl.Visible = false;
                        }
                    }
                } else
                    ticGraphControl.Visible = false;
            } else
                sourcePathTextBox.Text = "";
        }

        private void listView_KeyDown( object sender, KeyEventArgs e )
        {
            switch( e.KeyCode )
            {
                case Keys.F5:
                    populateListViewFromDirectory( currentDirectory ); // refresh
                    break;
            }
        }
        #endregion

        private void sourceTypeComboBox_SelectionChangeCommitted( object sender, EventArgs e )
        {
            populateListViewFromDirectory( currentDirectory );
        }

        #region Button click handlers
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
                initializeBackgroundSourceLoader();
                DialogResult = DialogResult.OK;
                Close();
                Application.DoEvents();
            } else
                MessageBox.Show( "Please select one or more data sources.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
        }

        private void cancelButton_Click( object sender, EventArgs e )
        {
            initializeBackgroundSourceLoader();
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

        private void recentDocumentsButton_Click( object sender, EventArgs e )
        {

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
        #endregion


        #region Look-In ComboBox handlers
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
        #endregion
    }
}