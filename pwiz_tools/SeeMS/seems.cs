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
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Collections.Specialized;
using System.Xml;
using System.Text.RegularExpressions;
using pwiz.CLI;
using pwiz.CLI.msdata;
using JWC;
using Microsoft.Win32;
using DigitalRune.Windows.Docking;
using SpyTools;
using CommandLine.Utility;

namespace seems
{
	public partial class seemsForm : Form
	{
        public static string Version = "0.5";
        public static string LastModified = "2/10/2009";

		private bool isLoaded = false;
		private OpenDataSourceDialog browseToFileDialog;

		private MruStripMenu recentFilesMenu;
		private string seemsRegistryLocation = "Software\\SeeMS";
		private RegistryKey seemsRegistryKey;

        private Manager manager;
        public Manager Manager { get { return manager; } }
        public DockPanel DockPanel { get { return dockPanel; } }
        public GraphForm CurrentGraphForm { get { return Manager.CurrentGraphForm; } }

		public ToolStrip ToolStrip1 { get { return toolStrip1; } }
		public StatusStrip StatusStrip1 { get { return statusStrip1; } }
		public ToolStripStatusLabel StatusLabel { get { return toolStripStatusLabel1; } }
		public ToolStripProgressBar StatusProgressBar { get { return toolStripProgressBar1; } }

        public ToolStripButton DataProcessingButton { get { return dataProcessingButton; } }
		public ToolStripButton AnnotationButton { get { return annotationButton; } }

		public seemsForm()
        {
			InitializeComponent();

            ToolStripManager.RenderMode = ToolStripManagerRenderMode.Professional;

			this.Load += seems_Load;
			this.Resize += seems_Resize;
			this.LocationChanged += seems_LocationChanged;

			seemsRegistryKey = Registry.CurrentUser.OpenSubKey( seemsRegistryLocation );
			if( seemsRegistryKey != null )
				seemsRegistryKey.Close();

			recentFilesMenu = new MruStripMenu( recentFilesFileMenuItem, new MruStripMenu.ClickedHandler( recentFilesFileMenuItem_Click ), seemsRegistryLocation + "\\Recent File List", true );

            browseToFileDialog = new OpenDataSourceDialog();
			browseToFileDialog.InitialDirectory = "C:\\";

            DockPanelManager.RenderMode = DockPanelRenderMode.VisualStyles;

            manager = new Manager(dockPanel);

            Manager.GraphFormGotFocus += new GraphFormGotFocusHandler(Manager_GraphFormGotFocus);
            Manager.LoadDataSourceProgress += new LoadDataSourceProgressEventHandler(Manager_LoadDataSourceProgress);
		}

        void Manager_GraphFormGotFocus (Manager sender, GraphForm graphForm)
        {
            if (graphForm.FocusedPane != null && graphForm.FocusedItem.Tag is MassSpectrum)
            {
                AnnotationButton.Enabled = true;
                DataProcessingButton.Enabled = true;
            }
            else
            {
                AnnotationButton.Enabled = false;
                DataProcessingButton.Enabled = false;
            }
        }

		private void seems_Load( object sender, EventArgs e )
		{
			this.StartPosition = FormStartPosition.Manual;
			this.Location = Properties.Settings.Default.MainFormLocation;
			this.Size = Properties.Settings.Default.MainFormSize;
			this.WindowState = Properties.Settings.Default.MainFormWindowState;
			isLoaded = true;
		}

		private void seems_LocationChanged( object sender, EventArgs e )
		{
			if( isLoaded && this.WindowState == FormWindowState.Normal )
				Properties.Settings.Default.MainFormLocation = this.Location;
		}

		private void seems_FormClosing( object sender, FormClosingEventArgs e )
		{
            Properties.Settings.Default.MainFormLocation = this.Location;
            Properties.Settings.Default.MainFormSize = this.Size;
            Properties.Settings.Default.MainFormWindowState = this.WindowState;
			Properties.Settings.Default.Save();

			/*foreach( DataSourceMap.MapPair sourceItr in dataSources )
				if( sourceItr.Value != null &&
					sourceItr.Value.first != null &&
					sourceItr.Value.first.MSDataFile != null )
					sourceItr.Value.first.MSDataFile.Dispose();*/
		}

		public void setFileControls( bool enabled )
		{
			// this is no longer relevant i think
		}

		public void setScanControls( bool enabled )
		{
			// if on MS1, enable mass fingerprint
			// if on MS2+, enable fragmentation
			// if on MS that is centroided in the file, disable peak processing
			// if on SRM, disable annotation
		}

		private void openFile( string filepath )
		{
            // update recent files list
            recentFilesMenu.AddFile( filepath, Path.GetFileName( filepath ) );
            recentFilesMenu.SaveToRegistry();

			Manager.OpenFile(filepath);
		}

        private delegate void ParseArgsCallback (string[] args);
        public void ParseArgs (string[] args)
        {
            if (InvokeRequired)
            {
                ParseArgsCallback d = new ParseArgsCallback(ParseArgs);
                Invoke(d, new object[] { args });
                return;
            }

            try
            {
                Arguments argParser = new Arguments(args);

                if (argParser["help"] != null ||
                    argParser["h"] != null ||
                    argParser["?"] != null)
                {
                    Console.WriteLine("TODO");
                    Close();
                    return;
                }

                BringToFront();
                Focus();
                Activate();
                Show();
                Application.DoEvents();

                string datasource = null;
                foreach (string arg in args)
                    if (!arg.StartsWith("--index") && !arg.StartsWith("id") && !arg.StartsWith("annotation"))
                    {
                        datasource = arg;
                        break;
                    }

                IAnnotation annotation = null;
                if (argParser["annotation"] != null)
                    annotation = AnnotationFactory.ParseArgument(argParser["annotation"]);

                if (datasource != null)
                {
                    if (argParser["index"] != null)
                    {
                        Manager.OpenFile(datasource, Convert.ToInt32(argParser["index"]), annotation);
                    }
                    else if (argParser["id"] != null)
                    {
                        Manager.OpenFile(datasource, argParser["id"], annotation);
                    }
                    else
                        Manager.OpenFile(datasource);
                }
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                if (ex.InnerException != null)
                    message += "\n\nAdditional information: " + ex.InnerException.Message;
                MessageBox.Show(message,
                                "Error parsing command line arguments",
                                MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
                                0, false);
            }
        }

        public void Manager_LoadDataSourceProgress (Manager sender, string status, int percentage, CancelEventArgs e)
        {
            if (toolStrip1.InvokeRequired)
            {
                LoadDataSourceProgressEventHandler d = new LoadDataSourceProgressEventHandler(Manager_LoadDataSourceProgress);
                Invoke(d, new object[] { sender, status, percentage, e });
            }
            else
            {
                if (toolStrip1.IsDisposed)
                {
                    e.Cancel = true;
                    return;
                }

                if (status.Length > 0)
                {
                    toolStripStatusLabel1.Text = status;
                    toolStripStatusLabel1.Visible = true;
                }
                else
                    toolStripStatusLabel1.Visible = false;

                switch (percentage)
                {
                    case 100:
                        toolStripProgressBar1.Visible = false;
                        break;
                    default:
                        toolStripProgressBar1.Visible = true;
                        toolStripProgressBar1.Minimum = 0;
                        toolStripProgressBar1.Maximum = 100;
                        toolStripProgressBar1.Value = percentage;
                        break;
                }

                toolStrip1.Refresh();
            }
        }

		private void openFile_Click( object sender, EventArgs e )
        {
			if( browseToFileDialog.ShowDialog() == DialogResult.OK )
			{
                foreach( string dataSource in browseToFileDialog.DataSources )
                    openFile( dataSource );
			}
		}

		private void cascadeWindowMenuItem_Click( object sender, EventArgs e )
		{
			LayoutMdi( MdiLayout.Cascade );
		}

		private void tileHorizontalWindowMenuItem_Click( object sender, EventArgs e )
		{
			LayoutMdi( MdiLayout.TileHorizontal );
		}

		private void tileVerticalWindowMenuItem_Click( object sender, EventArgs e )
		{
			LayoutMdi( MdiLayout.TileVertical );
		}

		private void arrangeIconsWindowMenuItem_Click( object sender, EventArgs e )
		{
			LayoutMdi( MdiLayout.ArrangeIcons );
		}

		private void closeAllWindowMenuItem_Click( object sender, EventArgs e )
		{
			foreach( Form f in MdiChildren )
				f.Close();
		}

		private void recentFilesFileMenuItem_Click( int index, string filepath )
		{
			openFile( filepath );
		}

		private void exitFileMenuItem_Click( object sender, EventArgs e )
		{
			Application.Exit();
		}

		private void aboutHelpMenuItem_Click( object sender, EventArgs e )
		{
            AboutForm form = new AboutForm();
            form.ShowDialog();
		}

		// workaround for MDI Window list bug
		private void windowToolStripMenuItem_DropDownOpening( object sender, EventArgs e )
		{
			if( ActiveMdiChild != null )
			{
				Form activeMdiChild = ActiveMdiChild;
				ActivateMdiChild( null );
				ActivateMdiChild( activeMdiChild );
			}
		}

		private void toolStripPanel1_Layout( object sender, LayoutEventArgs e )
		{
            dockPanel.Location = new Point(0, toolStripPanel1.Height);
            dockPanel.Height = ClientSize.Height - toolStripPanel2.Height - toolStripPanel1.Height;
            dockPanel.Width = ClientSize.Width;
		}

		private void toolStripPanel2_Layout( object sender, LayoutEventArgs e )
		{
            dockPanel.Location = new Point(0, toolStripPanel1.Height);
            dockPanel.Height = ClientSize.Height - toolStripPanel2.Height - toolStripPanel1.Height;
            dockPanel.Width = ClientSize.Width;
		}

		private void seems_ResizeBegin( object sender, EventArgs e )
		{
            if (CurrentGraphForm != null && CurrentGraphForm.WindowState == FormWindowState.Maximized)
			{
                CurrentGraphForm.SuspendLayout();
                CurrentGraphForm.ZedGraphControl.Visible = false;
			}
		}

		private void seems_Resize( object sender, EventArgs e )
		{
            dockPanel.Location = new Point(0, toolStripPanel1.Height);
            dockPanel.Height = ClientSize.Height - toolStripPanel2.Height - toolStripPanel1.Height;
            dockPanel.Width = ClientSize.Width;
		}

		private void seems_ResizeEnd( object sender, EventArgs e )
		{
			if( isLoaded && this.WindowState != FormWindowState.Minimized )
			{
				if( this.WindowState == FormWindowState.Normal )
					Properties.Settings.Default.MainFormSize = this.Size;
				Properties.Settings.Default.MainFormWindowState = this.WindowState;
			}

            if (CurrentGraphForm != null && CurrentGraphForm.WindowState == FormWindowState.Maximized)
			{
                CurrentGraphForm.ResumeLayout();
                CurrentGraphForm.ZedGraphControl.Visible = true;
                CurrentGraphForm.Refresh();
			}
		}

        private void dataProcessingButton_Click( object sender, EventArgs e )
        {
            Manager.ShowDataProcessing();
        }

        private void previewAsMzMLToolStripMenuItem_Click( object sender, EventArgs e )
        {
            Manager.ShowCurrentSourceAsMzML();
        }

        private void annotationButton_Click( object sender, EventArgs e )
        {
            Manager.ShowAnnotationForm();
        }

        private void eventLogToolStripMenuItem_Click( object sender, EventArgs e )
        {
            //eventLog.Show();
        }
	}
}