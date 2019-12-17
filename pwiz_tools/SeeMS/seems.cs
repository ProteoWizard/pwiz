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
using System.Linq;
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

            combineIonMobilitySpectraToolStripMenuItem.Checked = Properties.Settings.Default.CombineIonMobilitySpectra;
            ignoreZeroIntensityPointsToolStripMenuItem.Checked = Properties.Settings.Default.IgnoreZeroIntensityPoints;
            acceptZeroLengthSpectraToolStripMenuItem.Checked = Properties.Settings.Default.AcceptZeroLengthSpectra;
            timeInMinutesToolStripMenuItem.Checked = Properties.Settings.Default.TimeInMinutes;

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

            if (Properties.Settings.Default.DefaultDecimalPlaces < decimalPlacesToolStripMenuItem.DropDownItems.Count)
                (decimalPlacesToolStripMenuItem.DropDownItems[Properties.Settings.Default.DefaultDecimalPlaces] as ToolStripMenuItem).Checked = true;

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
            Environment.Exit(0);
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

			Manager.OpenFile(filepath, closeIfOpen: true);
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
                if (args.Any(o => Regex.Match(o, "(-{1,2}|/)(help|\\?)").Success))
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
                IAnnotation annotation = null;
                var idOrIndexList = new List<object>();
                var idOrIndexListByFile = new Dictionary<string, List<object>>();
                var annotationByFile = new Dictionary<string, IAnnotation>();

                for (int i=0; i < args.Length; ++i)
                {
                    string arg = args[i];
                    // does the arg specify a data source?
                    if (arg.StartsWith("--index"))
                    {
                        idOrIndexList.Add(Convert.ToInt32(args[i+1]));
                        ++i;
                    }
                    else if (arg.StartsWith("--id"))
                    {
                        idOrIndexList.Add(args[i+1]);
                        ++i;
                    }
                    else if (arg.StartsWith("--annotation"))
                    {
                        annotation = AnnotationFactory.ParseArgument(args[i+1]);
                        ++i;
                    }
                    else
                    {
                        if (datasource != null)
                        {
                            idOrIndexListByFile[datasource].AddRange(idOrIndexList);
                            annotationByFile[datasource] = annotation;

                            idOrIndexList.Clear();
                            annotation = null;
                        }

                        datasource = arg;
                        if (!idOrIndexListByFile.ContainsKey(datasource))
                            idOrIndexListByFile[datasource] = new List<object>();
                    }
                }

                if (datasource != null)
                {
                    idOrIndexListByFile[datasource].AddRange(idOrIndexList);
                    annotationByFile[datasource] = annotation;

                    idOrIndexList.Clear();
                    annotation = null;
                }

                foreach (var fileListPair in idOrIndexListByFile)
                {
                    if (fileListPair.Value.Count > 0)
                        Manager.OpenFile(fileListPair.Key, fileListPair.Value, annotationByFile[fileListPair.Key]);
                    else
                        Manager.OpenFile(fileListPair.Key);
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

        private void timeToMzHeatmapsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Manager.LoadAllMetadata(CurrentGraphForm.Sources[0]);

            var heatmapForm = new TimeMzHeatmapForm(Manager, CurrentGraphForm.Sources[0]);
            heatmapForm.Show(DockPanel, DockState.Document);
        }

        private void decimalPlaces_Click(object sender, EventArgs e)
        {
            string decimalPlacesStr = (sender as ToolStripMenuItem)?.Text ?? throw new ArgumentException();
            Properties.Settings.Default.DefaultDecimalPlaces = Int32.Parse(decimalPlacesStr);
            Properties.Settings.Default.Save();

            foreach (ToolStripMenuItem item in decimalPlacesToolStripMenuItem.DropDownItems)
                item.Checked = false;
            (decimalPlacesToolStripMenuItem.DropDownItems[Properties.Settings.Default.DefaultDecimalPlaces] as ToolStripMenuItem).Checked = true;

            Refresh();
        }

        private void combineIonMobilitySpectraToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.CombineIonMobilitySpectra = combineIonMobilitySpectraToolStripMenuItem.Checked;
            Properties.Settings.Default.Save();
        }

        private void ignoreZeroIntensityPointsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.IgnoreZeroIntensityPoints = ignoreZeroIntensityPointsToolStripMenuItem.Checked;
            Properties.Settings.Default.Save();
        }

        private void acceptZeroLengthSpectraToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.AcceptZeroLengthSpectra = acceptZeroLengthSpectraToolStripMenuItem.Checked;
            Properties.Settings.Default.Save();
        }

        private void timeInMinutesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.TimeInMinutes = timeInMinutesToolStripMenuItem.Checked;
            Properties.Settings.Default.Save();
        }
    }
}