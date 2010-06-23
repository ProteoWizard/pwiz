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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Diagnostics;
using System.Security.Principal;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Configuration;

using IdPickerGui.MODEL;
using IdPickerGui.BLL;
using IDPicker;

namespace IdPickerGui
{
    public partial class IDPickerForm : Form
    {
        public static string Version { get { return Util.GetAssemblyVersion( System.Reflection.Assembly.GetExecutingAssembly().GetName() ); } }
        public static DateTime LastModified { get { return Util.GetAssemblyLastModified( System.Reflection.Assembly.GetExecutingAssembly().GetName() ); } }

		private bool isLoaded = false;
        private string idPickerInfosPath;
        private ArrayList prevPickInfos;
        private DataGridViewCellEventArgs mouseLocation;
        private Dictionary<string, FileSystemWatcher> resultDirWatchers;
        
        public ArrayList PrevPickInfos
        {
            get { return prevPickInfos; }
            set { prevPickInfos = value; }
        }
        public string IDPickerInfosPath
        {
            get { return idPickerInfosPath; }
            set { idPickerInfosPath = value; }
        }
        public Dictionary<string, FileSystemWatcher> ResultDirWatchers
        {
            get { return resultDirWatchers; }
            set { resultDirWatchers = value; }
        }

        /// <summary>
        /// Constructor. Setup default paths for history file, dest dir, log file, and debug level.
        /// </summary>
        public IDPickerForm()
        {
            try
            {
                PrevPickInfos = new ArrayList();
				ResultDirWatchers = new Dictionary<string, FileSystemWatcher>();
                IDPickerInfosPath = getPathForHistoryFile();
                evaluateDefaultDestinationDirectory();
                ExceptionManager.LogFilePath = Environment.CurrentDirectory + "\\" + IDPicker.Properties.Settings.Default.LogFileName;
                ExceptionManager.DebugLevel = IDPicker.Properties.Settings.Default.DebugLevel;

                InitializeComponent();

            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error setting up main form.", exc));
            }
            
        }

        /// <summary>
        /// Show tip of the day message form
        /// </summary>
        /// <param name="msg"></param>
        private void showTipOfTheDay()
        {
            try
            {

                TipOfTheDayForm form = new TipOfTheDayForm();

                form.ShowDialog(this);

            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error displaying tip of the day information.", exc));
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
        /// Restore form position, size, location from properties
        /// </summary>
        private void restorePreviousFormSettings()
        {
            try
            {
                this.StartPosition = FormStartPosition.Manual;
                this.Location = Properties.Settings.Default.LastWindowLocation;
                this.Size = Properties.Settings.Default.LastWindowSize;
                this.WindowState = Properties.Settings.Default.LastWindowState;
                isLoaded = true;

            }
            catch (Exception exc)
            {
                throw new Exception("Error restoring previous application settings.", exc);
            }
        }

        /// <summary>
        /// Show ExportReportForm from right-click report or left click export icon im MyReports
        /// </summary>
        /// <param name="id"></param>
		private void exportReport( int id )
		{
            try
            {
                ExportReportForm form = new ExportReportForm(getCurrIdPickerInfoById(id));

                form.ShowDialog();
            }
            catch (Exception exc)
            {
                throw new Exception("Error exporting report\r\n", exc);                
            }
		}

        /// <summary>
        /// Get (per user) the path to place where user.config is stored by default
        /// so can put the history file of info objects (xml) there too
        /// </summary>
        /// <returns></returns>
        private string getPathForHistoryFile()
        {
            string fileName = "PrevIdPickerRequests.xml";
            string prevPickerInfosPath = Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData ) +
                "\\Vanderbilt University\\IDPicker";

            try
            {
#if !DEBUG
                if( !Debugger.IsAttached )
                {
                    Configuration config = ConfigurationManager.OpenExeConfiguration( ConfigurationUserLevel.PerUserRoamingAndLocal );
                    prevPickerInfosPath = config.FilePath.Substring( 0, config.FilePath.LastIndexOf( "\\" ) );
                }
#endif
                Directory.CreateDirectory( prevPickerInfosPath );
                prevPickerInfosPath += "\\" + fileName;
                return prevPickerInfosPath;
            }
            catch (Exception exc)
            {
                throw new Exception("Error loading history file.", exc);
            }

        }

        /// <summary>
        /// Fill MyReports DataGridView by building datatable from in memory storage of request objects
        /// allows filling current list first by loading from history file then building my reports
        /// basically so don't have to write out and load from file each time
        /// </summary>
        /// <param name="fromFile"></param>
        private void fillMyReportsDataGridView(bool fromFile)
        {
            try
            {
                if (fromFile)
                {
                    loadPrevIdPickerInfosFromFile();
                }

                if (PrevPickInfos.Count > 0)
                {
                    dgvReports.AutoGenerateColumns = false;
                    dgvReports.DataSource = buildMyReportsDataTable();

                    dgvReports.ClearSelection();
                }
                else
                {
                        dgvReports.Enabled = false;
                        lblStartHere.Visible = true;
                }

            }
            catch (Exception exc)
            {
                throw new Exception("Error loading My Reports data.", exc);
            }
            

        }

        /// <summary>
        /// Serialize the request objects in memory to the history file
        /// </summary>
        public void updatePrevIdPickerInfosFile()
        {
            try
            {
                Type[] extraTypes = new Type[2];

                extraTypes[0] = typeof(InputFileTag);
                extraTypes[1] = typeof(KeyedCollection<string, InputFileTag>);
                
                
                if (PrevPickInfos.Count > 0)
                {
                    XmlSerializer xmlSer = new XmlSerializer(typeof(IDPickerInfo[]), extraTypes);

                    StreamWriter sw = new StreamWriter(IDPickerInfosPath, false);

                    xmlSer.Serialize(sw, (IDPickerInfo[])PrevPickInfos.ToArray(typeof(IDPickerInfo)));

                    sw.Close();
                }

            }
            catch (Exception exc)
            {
                throw new Exception("Error saving history.", exc);
            }

        }

        /// <summary>
        /// If dest dir in properties is blank then default is put in: mydoc/my idpicker reports.
        /// </summary>
        private void evaluateDefaultDestinationDirectory()
        {
            try
            {
                string destDir = Properties.Settings.Default.ResultsDir;

                if (!destDir.Equals(string.Empty))
                {
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\My IDPicker Reports");

                        Properties.Settings.Default.ResultsDir = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\My IDPicker Reports";

                        Properties.Settings.Default.Save();

                        throw new Exception("The default report destination directory does not exist and has been overwritten with " + Properties.Settings.Default.ResultsDir + ".");   
                    }
                }
                else
                {
                    Properties.Settings.Default.ResultsDir = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\My IDPicker Reports";

                    Properties.Settings.Default.Save();
                }

            }
            catch (Exception exc)
            {
                throw new Exception("Error setting default destination directory", exc);
            }

        }

        /// <summary>
        /// Deserialize request info objects from file to memory
        /// </summary>
        private void loadPrevIdPickerInfosFromFile()
        {
            try
            {
                if (File.Exists(IDPickerInfosPath))
                {
                    PrevPickInfos = new ArrayList();

                    XmlSerializer xmlSer = new XmlSerializer(typeof(IDPickerInfo[]));

                    StreamReader sr = new StreamReader(IDPickerInfosPath);

                    PrevPickInfos.AddRange((IDPickerInfo[])xmlSer.Deserialize(sr));
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error loading history.", exc);
            }

        }

        /// <summary>
        /// Build data table from req info objects in memory. Checks to be
        /// sure their active and specific files exist in the dest dir. Also
        /// sets a watch on dest dir so they will be removed if dir deleted
        /// outside the application.
        /// </summary>
        /// <returns></returns>
        private DataTable buildMyReportsDataTable()
        {
            try
            {
                DataTable dt = new DataTable();

                dt.Columns.Add(new DataColumn("ID", typeof(int)));
                dt.Columns.Add(new DataColumn("REPORT_NAME", typeof(string)));
                dt.Columns.Add(new DataColumn("DATE_RUN", typeof(DateTime)));
                dt.Columns.Add(new DataColumn("SRC_FILES_DIR", typeof(string)));
                dt.Columns.Add(new DataColumn("DATABASE_PATH", typeof(string)));
                dt.Columns.Add(new DataColumn("RESULTS_DIR", typeof(string)));

                foreach (IDPickerInfo pInfo in PrevPickInfos)
                {
                    if (Convert.ToBoolean(pInfo.Active))
                    {
						if( Directory.Exists( pInfo.ResultsDir ) )
						{
							if( File.Exists( pInfo.ResultsDir + "/index.html" ) &&
								File.Exists( pInfo.ResultsDir + "/idpicker-scripts.js" ) &&
								File.Exists( pInfo.ResultsDir + "/idpicker-style.css" ) )
							{
								DataRow dr = dt.NewRow();

								dt.Rows.Add( dr );

								dr["ID"] = pInfo.Id.ToString();
								dr["REPORT_NAME"] = pInfo.ReportName;
								dr["DATE_RUN"] = pInfo.DateRequested.ToString();
								dr["SRC_FILES_DIR"] = pInfo.SrcFilesDir;
								dr["DATABASE_PATH"] = Path.GetFileName( pInfo.DatabasePath );
								dr["RESULTS_DIR"] = pInfo.ResultsDir;


								// set a watch on the results directory to be informed if it gets changed

								if( !ResultDirWatchers.ContainsKey( pInfo.ResultsDir ) )
								{
									ResultDirWatchers.Add( pInfo.ResultsDir, null );
									FileSystemWatcher watcher = ResultDirWatchers[pInfo.ResultsDir] = new FileSystemWatcher();
									watcher.Path = Directory.GetParent( pInfo.ResultsDir ).FullName;
									watcher.IncludeSubdirectories = true;
									watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;
									watcher.Renamed += new RenamedEventHandler( watcher_Renamed );
									watcher.Deleted += new FileSystemEventHandler( watcher_Deleted );
									watcher.EnableRaisingEvents = true;
								}
							}
						}
						else
						{
							removePrevRequestFromHistory( pInfo.Id );
						}
                       
                    }

                }

                if (dt.Rows.Count == 0)
                {
                    dgvReports.Enabled = false;
                    lblStartHere.Visible = true;
                }
                else
                {
                    dgvReports.Enabled = true;
                    lblStartHere.Visible = false;
                }

                return dt;
            }
            catch (Exception exc)
            {
                throw new Exception("Error building My Reports.", exc);
            }

        }


        /// <summary>
        /// (event) Dest dir deleted outside of application.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		void watcher_Deleted( object sender, FileSystemEventArgs e )
		{
			if( e.ChangeType == WatcherChangeTypes.Deleted )
			{
				bool needRebuild = false;
				foreach( IDPickerInfo info in PrevPickInfos )
				{
					if( info.ResultsDir == Path.GetDirectoryName( e.FullPath ) && info.Active == 1 )
					{
						if( info.ResultsDir == e.FullPath ||
							e.Name.Contains( "index.html" ) ||
							e.Name.Contains( "idpicker-scripts.js" ) ||
							e.Name.Contains( "idpicker-style.css" ) )
						{
							info.Active = 0;
							needRebuild = true;
							break;
						}
					}
				}

				if( needRebuild )
				{
					updatePrevIdPickerInfosFile();
					rebuildMyReportsDataTable();
				}
			}
		}

        /// <summary>
        /// (event) Dest dir deleted outside of application.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		void watcher_Renamed( object sender, RenamedEventArgs e )
		{
			bool needRebuild = false;
			foreach( IDPickerInfo info in PrevPickInfos )
			{
				if( info.ResultsDir == Path.GetDirectoryName( e.FullPath ) && info.Active == 1 )
				{
					if( info.ResultsDir == e.FullPath ||
						e.Name.Contains( "index.html" ) ||
						e.Name.Contains( "idpicker-scripts.js" ) ||
						e.Name.Contains( "idpicker-style.css" ) )
					{
						info.Active = 0;
						needRebuild = true;
						break;
					}
				}
			}

			if( needRebuild )
			{
				updatePrevIdPickerInfosFile();
				rebuildMyReportsDataTable();
			}
		}

        /// <summary>
        /// (Delegate) Rebuild MyReports from dir watchers.
        /// </summary>
		private delegate void RebuildMyReportsDataTableCallback();

        /// <summary>
        /// (Callback) Rebuild MyReports from dir watchers.
		/// </summary>
        private void rebuildMyReportsDataTable()
		{
			if( dgvReports.InvokeRequired )
			{
				RebuildMyReportsDataTableCallback d = new RebuildMyReportsDataTableCallback( rebuildMyReportsDataTable );
				Invoke( d, new object[] {} );
			} else
			{
				dgvReports.DataSource = buildMyReportsDataTable();
			}
		}

        /// <summary>
        /// Open report in new tab from MyReports by double clicking or left clicking view icon
        /// </summary>
        /// <param name="id"></param>
        public void openReport(int id)
        {
            bool allowTab = true;

            try
            {
                IDPickerInfo pInfo = getCurrIdPickerInfoById(id);

                // orig had report names as unique so couldn't open
                // open duplicate reports but I think they want
                // diff reports with same name
                /*
                foreach (TabPage tab in tabReportsView.TabPages)
                {
                    if (tab.Text.Equals(pInfo.ReportName))
                    {
                        allowTab = false;
                        break;
                    }
                }
                */

                if (allowTab)
                {
                    TabPage tabNew = new TabPage(pInfo.ReportName);
                    tabReportsView.TabPages.Add(tabNew);
                    tabNew.Tag = id;

                    ReportBrowserControl reportBrowser = new ReportBrowserControl(new Uri(pInfo.ResultsDir + "/index.html"));
                    reportBrowser.WebBrowser.Navigating += new WebBrowserNavigatingEventHandler( WebBrowser_Navigating );
                    reportBrowser.Dock = DockStyle.Fill;
                    
                    /*
                    reportBrowser.BorderStyle = BorderStyle.FixedSingle;

                    Rectangle adjustedBound = tabNew.Bounds;
                    adjustedBound.Size = new Size(tabNew.Bounds.Width - 10, tabNew.Bounds.Height - 40);

                    Panel pnlBrowser = new Panel();
                    pnlBrowser.Bounds = adjustedBound;
                    pnlBrowser.Controls.Add(reportBrowser);
                    pnlBrowser.Dock = DockStyle.None;
                    pnlBrowser.Anchor = AnchorStyles.Left;
                    pnlBrowser.Anchor = AnchorStyles.Top;
                    pnlBrowser.Anchor = AnchorStyles.Right;
                    pnlBrowser.Anchor = AnchorStyles.Bottom;


                    reportBrowser.Bounds = adjustedBound;

                     * */

                    tabNew.Controls.Add(reportBrowser);
                    

                    tabNew.ToolTipText = pInfo.ReportName;

                    //tabNew.ContextMenuStrip = cmRightClickTab;

                    //tabReportsView.TabPages.Add(tabNew);
                    tabReportsView.SelectedTab = tabNew;
                }
               
            }
            catch (Exception exc)
            {
                throw new Exception("Error opening report.", exc);
            }

        }

        private void WebBrowser_Navigating( object sender, WebBrowserNavigatingEventArgs e )
        {
            if( e.Url.AbsolutePath.Contains( "cgi-bin" ) )
            {
                e.Cancel = true;

                if( e.Url.AbsolutePath.Contains( "generateSpectrumSvg.cgi" ) )
                {
                    string source = null;
                    string nativeID = null;
                    int charge = -1;
                    string sequence = null;

                    SpectrumGraph.ParseQuery( e.Url, out source,
                                              out nativeID, out charge, out sequence );

                    if( source != null &&  nativeID != null )
                    {
                        try
                        {
                            IDPickerInfo info = getCurrIdPickerInfoById( (int) tabReportsView.SelectedTab.Tag );
                            SpectrumGraph.Show( this, info, source, nativeID, charge, sequence );
                        } catch( Exception ex )
                        {
                            HandleExceptions( ex );
                        }
                    } else
                    {
                        HandleExceptions( new Exception( "Invalid call to generateSpectrumSvg.cgi: " + e.Url.ToString() ) );
                    }
                } else if( e.Url.AbsolutePath.Contains( "generateSequenceCoverage.cgi" ) )
                {
                    try
                    {
                        IDPickerInfo info = getCurrIdPickerInfoById( (int) tabReportsView.SelectedTab.Tag );
                        var queryNameValue = System.Web.HttpUtility.ParseQueryString( e.Url.Query );
                        string accession = queryNameValue["accession"];
                        using (StreamReader reader = new StreamReader(Path.Combine( info.ResultsDir, "coverage.txt" )))
                        {
                            while( !reader.EndOfStream )
                            {
                                string line = reader.ReadLine();
                                string[] tokens = line.Split( '\t' );
                                if( tokens.Length == 0 )
                                    continue;
                                if( tokens[0] == accession )
                                {
                                    var sequenceCoverage = new SequenceCoverage( info.DatabasePath, queryNameValue["accession"], SequenceCoverageQueryParser.Parse( tokens ) )
                                    {
                                        Width = this.Width / 2,
                                        Height = this.Height / 2
                                    };
                                    sequenceCoverage.Show(this);
                                    return;
                                }
                            }
                        }
                    } catch( Exception ex )
                    {
                        HandleExceptions( ex );
                    }
                }
            }
        }

        // TODO:    Move storage of info objects from ArrayList to Dictionary
        //          so can access with a key (id or results path)

        /// <summary>
        /// Get req object from memory by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private IDPickerInfo getCurrIdPickerInfoById(int id)
        {
            try
            {
                foreach (IDPickerInfo pInfo in PrevPickInfos)
                {
                    if (pInfo.Id == id)
                    {
                        return pInfo;
                    }

                }

                return new IDPickerInfo();

            }
            catch (Exception exc)
            {
                throw new Exception("Error loading current IdPicker requests.", exc);
            }

        }
  
        /// <summary>
        /// Delete entire directory
        /// </summary>
        /// <param name="dirPath"></param>
        private void deleteDirectory(string dirPath)
        {
            try
            {
                if (Directory.Exists(dirPath))
                {
                    Directory.Delete(dirPath, true);
                }

            }
            catch (Exception exc)
            {
                throw new Exception("Problem deleting directory.", exc);
            }

        }

        /// <summary>
        /// Set req object active property to 0 
        /// </summary>
        /// <param name="id"></param>
        private void removePrevRequestFromHistory(int id)
        {
            try
            {
                foreach (IDPickerInfo pInfo in PrevPickInfos)
                {
                    if (pInfo.Id == id)
                    {
                        pInfo.Active = 0;

                        break;
                    }
                }

            }
            catch (Exception exc)
            {
                throw new Exception("Error removing previous report from history.", exc);
            }

        }

        /// <summary>
        /// Entry point for deleting report..allows for deleting entire 
        /// dir option or just removing from MyReports (active = 0)
        /// </summary>
        /// <param name="id"></param>
        private void deleteOrRemoveDirectory(int id)
        {
            try
            {
                DeleteReportForm form = new DeleteReportForm();

                form.ShowDialog(this);

				// remove entry from history
				if( form.DialogResult != DialogResult.Cancel )
				{
					ResultDirWatchers[getCurrIdPickerInfoById( id ).ResultsDir].Dispose();
					Application.DoEvents();
					removePrevRequestFromHistory( id );

					updatePrevIdPickerInfosFile();

					fillMyReportsDataGridView( false );
				}

                // delete report dir
                if (form.DialogResult == DialogResult.Yes)
                {
                    deleteDirectory(getCurrIdPickerInfoById(id).ResultsDir);
                }


            }
            catch (Exception exc)
            {
                throw new Exception("Problem deleting directory.", exc);
            }

        }

        /// <summary>
        /// Open report by report id (in new tab). Later on there might
        /// be other ways of opening new reports?
        /// </summary>
        /// <param name="id"></param>
        private void openNewReport(int id)
        {

            try
            {
                openReport(id);
            }
            catch (Exception exc)
            {
                throw new Exception("Error opening report.", exc);
            }


        }

        /// <summary>
        /// Gets id text from datagridview cell nd converts to integer. We
        /// were doing this in many places so having it here cuts repeating
        /// this bit..
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private int getReqIdFromMyReportsRow(int row)
        {
            try
            {
                return Convert.ToInt32( dgvReports.Rows[row].Cells[0].Value.ToString() );
            }
            catch (Exception exc)
            {
                throw new Exception("Error reading value from My Reports cell.", exc);
            }
        }





        // event handlers


        /// <summary>
        /// Restore size, pos etc of form and fill MyReports from history file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IDPickerForm_Load(object sender, EventArgs e)
        {
            try
            {
                restorePreviousFormSettings();

                fillMyReportsDataGridView(true);

                if (Convert.ToBoolean(Properties.Settings.Default.ShowTipsOnStartup))
                {
                    showTipOfTheDay();
                }

            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error loading report history.", exc));
            }

        }

        /// <summary>
        /// Save form state to properties
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IDPickerForm_Resize(object sender, EventArgs e)
        {
            try
            {

                if (isLoaded && this.WindowState != FormWindowState.Minimized)
                {
                    if (this.WindowState == FormWindowState.Normal)
                        Properties.Settings.Default.LastWindowSize = this.Size;
                    Properties.Settings.Default.LastWindowState = this.WindowState;
                }

            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error resizing form.", exc));
            }
        }

        /// <summary>
        /// Save form state to properties
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IDPickerForm_LocationChanged(object sender, EventArgs e)
        {
            try
            {
                if (isLoaded && this.WindowState == FormWindowState.Normal)
                    Properties.Settings.Default.LastWindowLocation = this.Location;
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error moving form.", exc));
            }

        }

        /// <summary>
        /// Save user settings or properties
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IDPickerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                Properties.Settings.Default.Save();
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error closing form.", exc));
            }
        }

        /// <summary>
        /// Get tooltip for datagridview cell
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgvReports_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            try
            {
                if (e.RowIndex > -1)
                {
                    // daterequested
                    if (e.ColumnIndex == 1)
                    {
                        int selReqId = getReqIdFromMyReportsRow(e.RowIndex);

                        e.ToolTipText = getCurrIdPickerInfoById(selReqId).DateRunStart.ToString();

                    }

                    // reportname
                    if (e.ColumnIndex == 2)
                    {
                        int selReqId = getReqIdFromMyReportsRow(e.RowIndex);

                        e.ToolTipText = getCurrIdPickerInfoById(selReqId).ToString();

                    }

                    if (e.ColumnIndex == 6)
                    {
                        e.ToolTipText = "Open";
                    }
                    if (e.ColumnIndex == 7)
                    {
                        e.ToolTipText = "Export";
                    }
                    if (e.ColumnIndex == 8)
                    {
                        e.ToolTipText = "Delete";
                    }
                }
            }
            catch (Exception)
            {
                e.ToolTipText = "Error retrieving report information..";

                ExceptionManager.logExceptionMessageByFormToFile(this, e.ToolTipText, DateTime.Now);
            }

        }

        /// <summary>
        /// Cursor gets changed to hand over all rows now (can double click to open new report)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="location"></param>
        private void dgvReports_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                mouseLocation = e;

                if (e.RowIndex != -1)
                {
                    dgvReports.Cursor = Cursors.Hand;
                }
              
            }
            catch (Exception exc)
            {
                ExceptionManager.logExceptionMessageByFormToFile(this, exc.Message, DateTime.Now);
            }

        }

        /// <summary>
        /// Keep up with the location of mouse
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="location"></param>
        private void dgvReports_CellMouseLeave(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                mouseLocation = e;

                if (e.RowIndex != -1)
                {
                    dgvReports.Cursor = Cursors.Arrow;
                }
               

            }
            catch (Exception exc)
            {
                ExceptionManager.logExceptionMessageByFormToFile(this, exc.Message, DateTime.Now);
            }

        }

        /// <summary>
        /// New report used for New and Clone. Displays the Run Report wizard,
        /// updates the history file and refills MyReports
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnNewReport_Click(object sender, EventArgs e)
        {
            RunReportForm form;

            try
            {
                if( dgvReports.SelectedRows.Count > 0 && newMenuItem.Text.Contains( "Clone" ) )
                    form = new RunReportForm( getCurrIdPickerInfoById( getReqIdFromMyReportsRow( dgvReports.CurrentRow.Index ) ).Clone() as IDPickerInfo );
                else
                    form = new RunReportForm();

                form.ShowDialog(this);

                // run automatic TSV export if user checked it
                if( form.autoExportCheckBox.Checked )
                    new ExportReportForm( form.IdPickerRequest, "TSV" );

                updatePrevIdPickerInfosFile();

                fillMyReportsDataGridView(false);
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("New report request failed.", exc));
            }

        }

        // TODO: Right-click close tabs doesn't work that great

        /// <summary>
        /// Close tabs by right clicking on them
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void closeTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (tabReportsView.SelectedIndex != 0)
                {
                    tabReportsView.TabPages.Remove(tabReportsView.SelectedTab);
                }
                else
                {
                    MessageBox.Show("Please select the tab you wish to close.", "IdPickerGui", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error unable to close tabs.", exc));
            }

        }

        /// <summary>
        /// Open new report by double clicking in MyReports
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgvReports_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex != -1)
                {
                    openNewReport(getReqIdFromMyReportsRow(e.RowIndex));
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error opening report.", exc));
            }

        }

        /// <summary>
        /// Close all tabs in MyReports except the first main one which is required
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void closeAllButThiToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {

                for (int i = (tabReportsView.TabCount - 1); i > 0; i--)
                {
                    tabReportsView.TabPages.RemoveAt(i);
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error closing tabs.", exc));
            }

        }

        /// <summary>
        /// Open or View, Export, and Delete icons clicked in MyReports
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgvReports_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            try
            {
                if (e.RowIndex != -1)
                {
                    // open report icon
                    if (e.ColumnIndex == 6)
                    {
                        openNewReport(getReqIdFromMyReportsRow(e.RowIndex));
                    }

                    // export report icon
                    else if (e.ColumnIndex == 7)
                    {
                        exportReport(getReqIdFromMyReportsRow(e.RowIndex));
                    }

                    // delete report icon
                    else if (e.ColumnIndex == 8)
                    {
                        deleteOrRemoveDirectory(getReqIdFromMyReportsRow(e.RowIndex));
                    }

                }
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error selecting report in gridview\r\n", exc));
            }

        }

        /// <summary>
        /// Not sure. I think we were having issues with the datagridview always selcting
        /// a default cell or row after each interaction
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgvReports_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            try
            {
                if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
                {
                    dgvReports.CurrentCell = null;
                    dgvReports.CurrentCell = dgvReports.Rows[e.RowIndex].Cells[e.ColumnIndex];
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error selecting report in gridview.", exc));
            }
        }

        /// <summary>
        /// Show ToolsOptionsForm for setting scores and weights and mods.
        /// Saved in user settings or properties with each ok on this form.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                ToolsOptionsForm form = new ToolsOptionsForm();

                form.ShowDialog(this);
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error loading Tools/Options form.", exc));
            }

        }
        
        /// <summary>
        /// Right click view report (MyReports)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void viewMenuItem_Click( object sender, EventArgs e )
		{
            try
            {
                if (dgvReports.SelectedRows.Count > 0)
                    openNewReport(getReqIdFromMyReportsRow(dgvReports.SelectedRows[0].Index));
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error attempting to view selected report.", exc));
            }

         }

        /// <summary>
         /// Right click export report (MyReports)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		private void exportMenuItem_Click( object sender, EventArgs e )
		{
            try
            {
                if (dgvReports.SelectedRows.Count > 0)
                    exportReport(getReqIdFromMyReportsRow(dgvReports.SelectedRows[0].Index));
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error attempting to export selected report.", exc));
            }
		}

        /// <summary>
        /// Right click delete report (MyReports)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		private void deleteMenuItem_Click( object sender, EventArgs e )
		{

            try
            {
                if (dgvReports.SelectedRows.Count > 0)
                    deleteOrRemoveDirectory(getReqIdFromMyReportsRow(dgvReports.SelectedRows[0].Index));
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error attempting to delete selected report.", exc));
            }
		}

        /// <summary>
        /// File -> Exit
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		private void exitToolStripMenuItem_Click( object sender, EventArgs e )
		{
            try
            {
                this.Close();
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error attempting to exit application.", exc));
            }

		}

        /// <summary>
        /// Right click new report (MyReports)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		private void newMenuItem_Click( object sender, EventArgs e )
		{
            try
            {
                btnNewReport_Click(sender, e);
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error attempting to start new report.", exc));
            }
		}

        /// <summary>
        /// Right click report option shows New if not on report row or clone if
        /// user right clicks existing report in MyReports
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		private void cmRightClickReportName_Opening( object sender, CancelEventArgs e )
		{
			try
			{
				Rectangle currentCellBounds = dgvReports.GetCellDisplayRectangle( dgvReports.CurrentCellAddress.X, dgvReports.CurrentCellAddress.Y, true );
                bool reportWasClicked = dgvReports.SelectedRows.Count > 0 &&
					                    currentCellBounds.Contains( dgvReports.PointToClient( Control.MousePosition ) );
                if( reportWasClicked )
                {
                    newMenuItem.Text = "Clone...";

                    string sourceDir = getCurrIdPickerInfoById( getReqIdFromMyReportsRow( dgvReports.SelectedRows[0].Index ) ).SrcFilesDir;
                    openSourceDirectoryToolStripMenuItem.Enabled = Directory.Exists(sourceDir);
                    if( openSourceDirectoryToolStripMenuItem.Enabled )
                        openSourceDirectoryToolStripMenuItem.Text = "Open Source Directory";
                    else
                        openSourceDirectoryToolStripMenuItem.Text = "(source directory missing)";

                    string resultsDir = getCurrIdPickerInfoById( getReqIdFromMyReportsRow( dgvReports.SelectedRows[0].Index ) ).ResultsDir;
                    openResultsDirectoryToolStripMenuItem.Enabled = Directory.Exists( resultsDir );
                    if( openResultsDirectoryToolStripMenuItem.Enabled )
                        openResultsDirectoryToolStripMenuItem.Text = "Open Results Directory";
                    else
                        openResultsDirectoryToolStripMenuItem.Text = "(results directory missing)";
                } else
                    newMenuItem.Text = "New...";

                    toolStripSeparator3.Visible = reportWasClicked;
                    openSourceDirectoryToolStripMenuItem.Visible = reportWasClicked;
                    openResultsDirectoryToolStripMenuItem.Visible = reportWasClicked;
			}
			catch( Exception exc )
			{
				HandleExceptions( new Exception( "Error opening context menu.", exc ) );
			}
		}

        private void cmRightClickTab_Opening(object sender, CancelEventArgs e)
        {
            try
            {
                // my reports tab only open
                if (tabReportsView.TabCount == 1)
                {
                    e.Cancel = true;
                }
                // right click on my reports tab close all option only
                else if (tabReportsView.SelectedTab.Equals(tabMyReports))
                {
                    closeTabToolStripMenuItem.Visible = false;
                }
                // both close and close all
                else
                {
                    closeTabToolStripMenuItem.Visible = true;
                }
                
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error opening context menu.", exc));
            }
        }

        private void showTipsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                showTipOfTheDay();

            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error loading report history.", exc));
            }

        }

        private void aboutToolStripMenuItem_Click( object sender, EventArgs e )
        {
            AboutForm aboutForm = new AboutForm();
            aboutForm.ShowDialog();
        }

        private void documentationToolStripMenuItem_Click( object sender, EventArgs e )
        {
            Uri uri = new Uri( String.Format( "file:///{0}/idpicker-2-1-gui.html", Application.StartupPath.Replace( "\\", "/" ) ) );

            HtmlHelpForm form = new HtmlHelpForm( uri );

            form.Show();
        }

        private void openSourceDirectoryToolStripMenuItem_Click( object sender, EventArgs e )
        {
            try
            {
                if( dgvReports.SelectedRows.Count > 0 )
                {
                    string sourcePath = getCurrIdPickerInfoById( getReqIdFromMyReportsRow( dgvReports.SelectedRows[0].Index ) ).SrcFilesDir;
                    System.Diagnostics.Process.Start( "explorer.exe", "/n,/e," + sourcePath );
                }
            } catch( Exception exc )
            {
                HandleExceptions( new Exception( "Error attempting to open source directory.", exc ) );
            }
        }

        private void openResultsDirectoryToolStripMenuItem_Click( object sender, EventArgs e )
        {
            try
            {
                if( dgvReports.SelectedRows.Count > 0 )
                {
                    string resultsPath = getCurrIdPickerInfoById( getReqIdFromMyReportsRow( dgvReports.SelectedRows[0].Index ) ).ResultsDir;
                    System.Diagnostics.Process.Start( "explorer.exe", "/n,/e," + resultsPath );
                }
            } catch( Exception exc )
            {
                HandleExceptions( new Exception( "Error attempting to open source directory.", exc ) );
            }
        }
    }
}
