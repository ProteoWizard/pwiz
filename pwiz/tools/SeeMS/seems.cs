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
using pwiz.CLI.msdata;
using JWC;
using Microsoft.Win32;
using Extensions;

namespace seems
{
	using DataSourceMap = Map<string, DataSource>;

	public partial class seems : Form
	{
		private bool isLoaded = false;
		private OpenFileDialog browseToFileDialog;

		private HeaderComboBox.HeaderComboBoxControl dataSourceComboBox;
		private HeaderComboBox.HeaderComboBoxControl scanNumberComboBox;
		private ToolStripControlHost dataSourceComboBoxToolStripHost;
		private ToolStripControlHost scanNumberComboBoxToolStripHost;
		private string scanNumberHeaderString = String.Format( "{0,-8}{1,-10}{2,-5}{3,-15:E}{4,-15}",
			"Number", "Time (m)", "MSn", "TIC", "Parent M/Z" );

		private MruStripMenu recentFilesMenu;
		private string seemsRegistryLocation = "Software\\SeeMS";
		private RegistryKey seemsRegistryKey;

		private DataSourceMap dataSources;
		private bool pendingActivation; // a graph form is being activated

		public HeaderComboBox.HeaderComboBoxControl ScanNumberComboBox { get { return scanNumberComboBox; } }
		public ToolStripControlHost ScanNumberComboBoxHost { get { return scanNumberComboBoxToolStripHost; } }
		public ToolStrip ToolStrip1 { get { return toolStrip1; } }
		public ToolStripLabel ToolStripScanLabel { get { return toolStripLabel2; } }
		public StatusStrip StatusStrip1 { get { return statusStrip1; } }
		public ToolStripStatusLabel StatusLabel { get { return toolStripStatusLabel1; } }
		public ToolStripProgressBar StatusProgressBar { get { return toolStripProgressBar1; } }
		public GraphForm CurrentGraphForm { get { return ( (GraphForm) ActiveMdiChild ); } }
		public ToolStripMenuItem CentroidMenuItem { get { return centroidToolStripMenuItem; } }
		public ToolStripMenuItem DeisotopeMenuItem { get { return deisotopeToolStripMenuItem; } }
		public ToolStripMenuItem UseVendorCentroidMenuItem { get { return vendorToolStripMenuItem; } }
		public ToolStripDropDownButton PeakProcessingButton { get { return peakProcessingToolStripDropDownButton; } }
		public ToolStripDropDownButton AnnotateButton { get { return annotateToolStripDropDownButton; } }

		public seems( string[] args )
		{
			InitializeComponent();

			this.Load += seems_Load;
			this.Resize += seems_Resize;
			this.LocationChanged += seems_LocationChanged;

			seemsRegistryKey = Registry.CurrentUser.OpenSubKey( seemsRegistryLocation );
			if( seemsRegistryKey != null )
				seemsRegistryKey.Close();

			recentFilesMenu = new MruStripMenu( recentFilesFileMenuItem, new MruStripMenu.ClickedHandler( recentFilesFileMenuItem_Click ), seemsRegistryLocation + "\\Recent File List", true );

			dataSources = new Map<string, DataSource>();
			dataSourceComboBox = new HeaderComboBox.HeaderComboBoxControl();
			dataSourceComboBox.AccessibleName = "dataSourceComboBox";
			dataSourceComboBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			dataSourceComboBox.AutoSize = false;
			dataSourceComboBox.Enabled = false;
			dataSourceComboBox.IntegralHeight = true;
			dataSourceComboBox.ItemHeight = 13;
			dataSourceComboBox.ListDisplayMember = "CurrentFilepath";
			dataSourceComboBox.Name = "dataSourceComboBox";
			dataSourceComboBox.Size = new Size( 150, 22 );
			dataSourceComboBox.Margin = new Padding( 0, 0, 15, 0 );
			dataSourceComboBox.TabIndex = 4;
			dataSourceComboBox.TextDisplayMember = "Name";
			dataSourceComboBox.SelectedIndexChanged += new EventHandler( dataSourceComboBox_SelectedIndexChanged );
			dataSourceComboBox.Dock = DockStyle.Fill;
			dataSourceComboBoxToolStripHost = new ToolStripControlHost( dataSourceComboBox );
			dataSourceComboBoxToolStripHost.Alignment = ToolStripItemAlignment.Right;
			dataSourceComboBoxToolStripHost.AutoSize = false;

			toolStrip1.Items.Insert( toolStrip1.Items.IndexOf( toolStripLabel1 ), dataSourceComboBoxToolStripHost );

			scanNumberComboBox = new HeaderComboBox.HeaderComboBoxControl();
			scanNumberComboBox.AccessibleName = "scanNumberComboBox";
			scanNumberComboBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
			scanNumberComboBox.AutoSize = false;
			scanNumberComboBox.Enabled = false;
			scanNumberComboBox.IntegralHeight = true;
			scanNumberComboBox.ItemHeight = 13;
			scanNumberComboBox.ListDisplayMember = "ComboBoxView";
			scanNumberComboBox.Name = "scanNumberComboBox";
			scanNumberComboBox.Size = new Size( 72, 22 );
			scanNumberComboBox.TabIndex = 8;
			scanNumberComboBox.TextDisplayMember = "Id";
			scanNumberComboBox.ValueMember = "Id";
			scanNumberComboBox.SelectedIndexChanged += new EventHandler( scanNumberComboBox_SelectedIndexChanged );
			scanNumberComboBox.ListBox.FindForm().FormClosed += new FormClosedEventHandler( scanNumberComboBoxListBox_FormClosed );
			scanNumberComboBox.ListHeaderText = scanNumberHeaderString;
			scanNumberComboBox.ListBox.ContextMenuStrip = scanNumberComboBoxContextMenuStrip;
			scanNumberComboBoxContextMenuStrip.Opened += new EventHandler( scanNumberComboBoxContextMenuStrip_Opened );
			scanNumberComboBox.Dock = DockStyle.Fill;
			scanNumberComboBoxToolStripHost = new ToolStripControlHost( scanNumberComboBox );
			scanNumberComboBoxToolStripHost.Alignment = ToolStripItemAlignment.Right;
			scanNumberComboBoxToolStripHost.AutoSize = false;

			toolStrip1.Items.Insert( toolStrip1.Items.IndexOf( toolStripLabel2 ), scanNumberComboBoxToolStripHost );

			setFileControls( true );
			setScanControls( false );

			browseToFileDialog = new OpenFileDialog();
			browseToFileDialog.Filter =
				"Any spectra format (*.mzData;*.mzXML;*.xml;*.raw;*.wiff;*.mgf;*.dta;fid;*.baf;*.yep)|*.mzData;*.mzXML;*.xml;*.raw;*.wiff;*.mgf;*.dta;fid;*.baf;*.yep|" +
				"mzData (*.mzData;*.xml)|*.mzData;*.xml|" +
				"mzXML (*.mzXML;*.xml)|*.mzXML;*.xml|" +
				"RAW (*.RAW)|*.raw|" +
				"WIFF (*.WIFF)|*.wiff|" +
				"Bruker (fid;*.baf;*.yep)|fid;*.baf;*.yep|" +
				"MGF (*.mgf)|*.mgf|" +
				"DTA (*.dta)|*.dta";
			browseToFileDialog.FilterIndex = 0;
			browseToFileDialog.InitialDirectory = "C:\\";

			if( args.Length > 0 )
			{
				this.BringToFront();
				this.Focus();
				this.Activate();
				this.Show();
				Application.DoEvents();

				try
				{
					openFile( args[0] );

					if( args.Length > 1 )
					{
						try
						{
							//browserForm.ElementNumberComboBox.SelectedIndex = Convert.ToInt32( args[1] );
						} catch
						{
						}
					}
				} catch( Exception ex )
				{
					string message = ex.Message;
					if( ex.InnerException != null )
						message += "\n\nAdditional information: " + ex.InnerException.Message;
					MessageBox.Show( message,
									"Error recovering from crash",
									MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
									0, false );
				}
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

		private void seems_Resize( object sender, EventArgs e )
		{
			if( isLoaded && this.WindowState != FormWindowState.Minimized )
			{
				if( this.WindowState == FormWindowState.Normal )
					Properties.Settings.Default.MainFormSize = this.Size;
				Properties.Settings.Default.MainFormWindowState = this.WindowState;
			}
		}

		private void seems_LocationChanged( object sender, EventArgs e )
		{
			if( isLoaded && this.WindowState == FormWindowState.Normal )
				Properties.Settings.Default.MainFormLocation = this.Location;
		}

		private void seems_FormClosing( object sender, FormClosingEventArgs e )
		{
			Properties.Settings.Default.Save();

			foreach( Map<string, DataSource>.MapPair sourceItr in dataSources )
				if( sourceItr.Value != null && sourceItr.Value.MSDataFile != null )
					sourceItr.Value.MSDataFile.Dispose();
		}

		public void setFileControls( bool enabled )
		{
			openFileToolStripButton.Enabled = enabled;
			openFileMenuItem.Enabled = enabled;
			closeAllWindowMenuItem.Enabled = enabled;
		}

		public void setScanControls( bool enabled )
		{
			peakProcessingToolStripDropDownButton.Enabled = enabled;
			integratePeaksToolStripButton.Enabled = enabled;
			annotateToolStripDropDownButton.Enabled = enabled;
			dataSourceComboBox.Enabled = enabled;
			if( CurrentGraphForm != null )
			{
				scanNumberComboBox.Enabled = enabled;
				setPeakIntegrationMode( peakIntegrationMode.Checked ); 
				if( enabled == true )
				{
					GraphItem selectedScan = (GraphItem) scanNumberComboBox.SelectedItem;

					if( selectedScan != null && selectedScan.IsMassSpectrum )
					{
						peakProcessingToolStripDropDownButton.Enabled = true;
						peptideFragmentationToolStripMenuItem.Enabled = true;
						peptideMassMappingToolStripMenuItem.Enabled = true;

						if( (selectedScan as MassSpectrum).Element.hasCVParam(CVID.MS_centroid_mass_spectrum) )
						{
							CentroidMenuItem.Enabled = false;
							CentroidMenuItem.Checked = true;
						} else
							CentroidMenuItem.Enabled = true;
					} else
					{
						peakProcessingToolStripDropDownButton.Enabled = false;
						peptideFragmentationToolStripMenuItem.Enabled = false;
						peptideMassMappingToolStripMenuItem.Enabled = false;
					}
				}
			}
		}

		private void openFile( string filepath )
		{
			try
			{
				DataSourceMap.InsertResult insertResult = dataSources.Insert( filepath, null );
				if( insertResult.WasInserted )
				{
					// file was not already open; create a new data source
					insertResult.Element.Value = new DataSource( filepath );
					insertResult.Element.Value.StatusReport += new StatusReportEventHandler( openFile_StatusReport );
					insertResult.Element.Value.ProgressReport += new ProgressReportEventHandler( openFile_ProgressReport );
					insertResult.Element.Value.SetInputFileCompleted += new SetInputFileCompletedEventHandler( openFile_SetInputFileCompleted );
					dataSourceComboBox.Items.Add( insertResult.Element.Value );
				} else
					GraphForm.CreateNewWindow( this, true ).ShowData( insertResult.Element.Value, 0 );


				// update recent files list
				recentFilesMenu.AddFile( filepath, Path.GetFileName( filepath ) );
				recentFilesMenu.SaveToRegistry();

			} catch( Exception ex )
			{
				string message = ex.Message;
				if( ex.InnerException != null )
					message += "\n\nAdditional information: " + ex.InnerException.Message;
				MessageBox.Show( message,
								"Error opening source file",
								MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
								0, false );
			}
		}

		private delegate void SetStatusLabelCallback( string status );
		private void SetStatusLabel( string status )
		{
			if( toolStrip1.InvokeRequired )
			{
				SetStatusLabelCallback d = new SetStatusLabelCallback( SetStatusLabel );
				Invoke( d, new object[] { status } );
			} else
			{
				if( status.Length > 0 )
				{
					toolStripStatusLabel1.Text = status;
					toolStripStatusLabel1.Visible = true;
				} else
					toolStripStatusLabel1.Visible = false;
				toolStrip1.Refresh();
			}
		}

		private delegate void SetProgressPercentageCallback( int percentage );
		private void SetProgressPercentage( int percentage )
		{
			if( toolStrip1.InvokeRequired )
			{
				SetProgressPercentageCallback d = new SetProgressPercentageCallback( SetProgressPercentage );
				Invoke( d, new object[] { percentage } );
			} else
			{
				switch( percentage )
				{
					case 0:
						toolStripProgressBar1.Visible = true;
						toolStripProgressBar1.Minimum = 0;
						toolStripProgressBar1.Maximum = 100;
						toolStripProgressBar1.Value = 0;
						break;
					case 100:
						toolStripProgressBar1.Visible = false;
						break;
					default:
						toolStripProgressBar1.Value = percentage;
						break;
				}
				toolStrip1.Refresh();
			}
		}

		public delegate void SetInputFileCompletedCallback( object sender, SetInputFileCompletedEventArgs e );
		private void openFile_SetInputFileCompleted( object sender, SetInputFileCompletedEventArgs e )
		{
			if( InvokeRequired )
			{
				SetInputFileCompletedCallback d = new SetInputFileCompletedCallback( openFile_SetInputFileCompleted );
				Invoke( d, new object[] { sender, e } );
			} else
			{
				pendingActivation = true;
				GraphForm.CreateNewWindow( this, true ).ShowData( e.DataSource, 0 );
				dataSourceComboBox.SelectedItem = CurrentGraphForm.DataSource;
				Application.DoEvents(); // repopulate scanNumberComboBox with the correct items
				scanNumberComboBox.SelectedIndex = CurrentGraphForm.CurrentGraphItemIndex;
				setScanControls( true );
			}
		}

		private void openFile_StatusReport( object sender, StatusReportEventArgs e )
		{
			SetStatusLabel( e.Status );
		}

		private void openFile_ProgressReport( object sender, ProgressReportEventArgs e )
		{
			SetProgressPercentage( e.Percentage );
		}

		private void openFile_Click( object sender, EventArgs e )
		{
			if( browseToFileDialog.ShowDialog() == DialogResult.OK )
			{
				openFile( browseToFileDialog.FileName );
			}
		}

		private void centroidToolStripMenuItem_CheckedChanged( object sender, EventArgs e )
		{
			bool doCentroiding = CentroidMenuItem.Enabled && CentroidMenuItem.Checked;
			CurrentGraphForm.DataSource.DoCentroiding = doCentroiding;
			CurrentGraphForm.updateGraph();
		}

		private void peptideFragmentationToolStripMenuItem_Click( object sender, EventArgs e )
		{
			PeptideFragmentationForm peptideFragmentationForm = new PeptideFragmentationForm();
			peptideFragmentationForm.ShowDialog( this );
			if( peptideFragmentationForm.DialogResult == DialogResult.Cancel )
				return;

			if( peptideFragmentationForm.MsProductReportXml != null )
			{
				Map<string, List<Pair<double, int>>> pointAnnotations = new Map<string, List<Pair<double, int>>>();

				XmlTextReader reader = new XmlTextReader( new StringReader( peptideFragmentationForm.MsProductReportXml ) );

				string ionSeriesName = "immonium";
				string ionSeriesIndex = "";
				int previousLabelCount = CurrentGraphForm.CurrentPointAnnotations.Count;
				while( reader.Read() )
				{
					switch( reader.NodeType )
					{
						case XmlNodeType.Element:
							if( reader.Name == "mz" )
							{
								string[] ionMzChargeStr = reader.ReadElementContentAsString().Split( ",".ToCharArray() );
								double ionMz = Convert.ToDouble( ionMzChargeStr[0] );
								int ionCharge = Convert.ToInt32( ionMzChargeStr[1] );
								pointAnnotations[ionSeriesName + ionSeriesIndex].Add( new Pair<double, int>( ionMz, ionCharge ) );
							} else if( reader.Name[0] == 'i' )
							{
								Int32 test;
								if( Int32.TryParse( reader.Name.Substring( 1 ), out test ) )
								{
									ionSeriesIndex = " " + reader.Name.Substring( 1 );
								}
							} else if( reader.Name == "name" )
							{
								ionSeriesName = reader.ReadElementContentAsString();
							}
							break;
					}
				}

				CurrentGraphForm.ScanAnnotationSettings.setScanLabels( pointAnnotations );
				if( CurrentGraphForm.CurrentPointAnnotations.Count > previousLabelCount )
					CurrentGraphForm.updateGraph();
			}
		}


		private void peptideMassMapProteinDigestToolStripMenuItem_Click( object sender, EventArgs e )
		{
			PeptideMassMapProteinDigestForm peptideMassMapProteinDigestForm = new PeptideMassMapProteinDigestForm();
			peptideMassMapProteinDigestForm.ShowDialog( this );
			if( peptideMassMapProteinDigestForm.DialogResult == DialogResult.Cancel )
				return;

			if( peptideMassMapProteinDigestForm.MsDigestReportXml != null )
			{
				Map<string, List<Pair<double, int>>> pointAnnotations = new Map<string, List<Pair<double, int>>>();

				XmlTextReader reader = new XmlTextReader( new StringReader( peptideMassMapProteinDigestForm.MsDigestReportXml ) );

				double peptideMonoMz = 0, peptideAvgMz = 0;
				int peptideCharge = 0;
				string peptideSequence;
				int previousLabelCount = CurrentGraphForm.CurrentPointAnnotations.Count;
				while( reader.Read() )
				{
					switch( reader.NodeType )
					{
						case XmlNodeType.Element:
							if( reader.Name == "peptide" )
							{
								peptideMonoMz = peptideAvgMz = 0;
								peptideCharge = 0;
								peptideSequence = "";
							} else if( reader.Name == "mi_m_over_z" )
							{
								peptideMonoMz = Convert.ToDouble( reader.ReadElementContentAsString() );
							} else if( reader.Name == "av_m_over_z" )
							{
								peptideAvgMz = Convert.ToDouble( reader.ReadElementContentAsString() );
							} else if( reader.Name == "charge" )
							{
								peptideCharge = Convert.ToInt32( reader.ReadElementContentAsString() );
							} else if( reader.Name == "database_sequence" )
							{
								peptideSequence = reader.ReadElementContentAsString();
								if( peptideMonoMz > 0 ) pointAnnotations[peptideSequence].Add( new Pair<double, int>( peptideMonoMz, peptideCharge ) );
								if( peptideAvgMz > 0 ) pointAnnotations[peptideSequence].Add( new Pair<double, int>( peptideAvgMz, peptideCharge ) );
							}
							break;
					}
				}

				CurrentGraphForm.ScanAnnotationSettings.setScanLabels( pointAnnotations );
				if( CurrentGraphForm.CurrentPointAnnotations.Count > previousLabelCount )
					CurrentGraphForm.updateGraph();
			}
		}

		private void clearToolStripMenuItem_Click( object sender, EventArgs e )
		{
			if( CurrentGraphForm.CurrentPointAnnotations.Count == 0 )
				return;

			CurrentGraphForm.CurrentPointAnnotations.Clear();
			CurrentGraphForm.updateGraph();
		}

		private void settingsToolStripMenuItem_Click( object sender, EventArgs e )
		{
			if( CurrentGraphForm.CurrentGraphItem.IsMassSpectrum )
			{
				ScanAnnotationSettingsForm annotationSettingsForm = new ScanAnnotationSettingsForm( CurrentGraphForm );
				annotationSettingsForm.ShowDialog( this );
			} else
			{
				ChromatogramAnnotationSettingsForm annotationSettingsForm = new ChromatogramAnnotationSettingsForm( CurrentGraphForm );
				annotationSettingsForm.ShowDialog( this );
			}
		}

		private void manualEditToolStripMenuItem_Click( object sender, EventArgs e )
		{
			AnnotationEditForm annotationEditForm = new AnnotationEditForm( CurrentGraphForm );
			annotationEditForm.ShowDialog( this );
		}

		private GraphForm currentGraphForm;
		private void seems_MdiChildActivate( object sender, EventArgs e )
		{
			if( CurrentGraphForm != null )
			{
				if( CurrentGraphForm.DataSource != null )
				{
					dataSourceComboBox.SelectedItem = CurrentGraphForm.DataSource;
					dataSourceComboBox.Refresh();
				}

				if( CurrentGraphForm.CurrentGraphItemIndex >= 0 )
				{
					scanNumberComboBox.SelectedIndex = CurrentGraphForm.CurrentGraphItemIndex;
					scanNumberComboBox.UpdateTextBox();
					scanNumberComboBox.Refresh();
				}

				setScanControls( true );
				if( CurrentGraphForm != currentGraphForm )
				{
					currentGraphForm = CurrentGraphForm;
					CurrentGraphForm.LostFocus += new EventHandler( GraphForm_LostFocus );
					CurrentGraphForm.ZedGraphControl.PreviewKeyDown += new PreviewKeyDownEventHandler( GraphForm_PreviewKeyDown );
					CurrentGraphForm.FormClosing += new FormClosingEventHandler( GraphForm_FormClosing );
					CurrentGraphForm.Resize += new EventHandler( GraphForm_Resize );
					CurrentGraphForm.LocationChanged += new EventHandler( GraphForm_LocationChanged );
					pendingActivation = false;
				}
			} else
				setScanControls( false );

			/*if( ActiveMdiChild == null )
			{
				dataSourceComboBox.Enabled = false;
			}*/
		}

		void GraphForm_GotFocus( object sender, EventArgs e )
		{
			if( CurrentGraphForm != currentGraphForm )
			{
				currentGraphForm = CurrentGraphForm;
				CurrentGraphForm.GotFocus -= new EventHandler( GraphForm_GotFocus );
				CurrentGraphForm.LostFocus += new EventHandler( GraphForm_LostFocus );
				CurrentGraphForm.ZedGraphControl.PreviewKeyDown += new PreviewKeyDownEventHandler( GraphForm_PreviewKeyDown );
				CurrentGraphForm.FormClosing += new FormClosingEventHandler( GraphForm_FormClosing );
				CurrentGraphForm.Resize += new EventHandler( GraphForm_Resize );
				CurrentGraphForm.LocationChanged += new EventHandler( GraphForm_LocationChanged );
				pendingActivation = false;
			}
		}

		private void GraphForm_Resize( object sender, EventArgs e )
		{
			if( isLoaded && !pendingActivation && CurrentGraphForm.WindowState != FormWindowState.Minimized )
			{
				if( CurrentGraphForm.WindowState == FormWindowState.Normal )
					Properties.Settings.Default.LastGraphFormSize = CurrentGraphForm.Size;
				Properties.Settings.Default.LastGraphFormWindowState = CurrentGraphForm.WindowState;
			}
		}

		private void GraphForm_LocationChanged( object sender, EventArgs e )
		{
			if( isLoaded && !pendingActivation && CurrentGraphForm.WindowState == FormWindowState.Normal )
				Properties.Settings.Default.LastGraphFormLocation = CurrentGraphForm.Location;
		}

		private void GraphForm_FormClosing( object sender, FormClosingEventArgs e )
		{
			Properties.Settings.Default.Save();
			setScanControls( false );
		}

		private void GraphForm_LostFocus( object sender, EventArgs e )
		{
			currentGraphForm = null;
			CurrentGraphForm.GotFocus += new EventHandler( GraphForm_GotFocus );
			CurrentGraphForm.LostFocus -= new EventHandler( GraphForm_LostFocus );
			CurrentGraphForm.ZedGraphControl.PreviewKeyDown -= new PreviewKeyDownEventHandler( GraphForm_PreviewKeyDown );
			CurrentGraphForm.FormClosing -= new FormClosingEventHandler( GraphForm_FormClosing );
			CurrentGraphForm.Resize -= new EventHandler( GraphForm_Resize );
			CurrentGraphForm.LocationChanged -= new EventHandler( GraphForm_LocationChanged );
		}

		private void GraphForm_PreviewKeyDown( object sender, PreviewKeyDownEventArgs e )
		{
			if( CurrentGraphForm.DataSource == null )
				return;

			int key = (int) e.KeyCode;
			if( key == (int) Keys.Left && scanNumberComboBox.SelectedIndex > 0 )
				scanNumberComboBox.SelectedIndex = scanNumberComboBox.SelectedIndex - 1;
			else if( key == (int) Keys.Right && scanNumberComboBox.SelectedIndex < scanNumberComboBox.Items.Count - 1 )
				scanNumberComboBox.SelectedIndex = scanNumberComboBox.SelectedIndex + 1;
			else
				return;

			//CurrentGraphForm.ZedGraphControl.PreviewKeyDown -= new PreviewKeyDownEventHandler( GraphForm_PreviewKeyDown );
			//Application.DoEvents();
			//CurrentGraphForm.ZedGraphControl.PreviewKeyDown += new PreviewKeyDownEventHandler( GraphForm_PreviewKeyDown );
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
			MessageBox.Show( "© 2008 Vanderbilt University", "About", MessageBoxButtons.OK, MessageBoxIcon.Information );
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
			
		}

		private void seems_ResizeBegin( object sender, EventArgs e )
		{
			if( CurrentGraphForm != null && CurrentGraphForm.WindowState == FormWindowState.Maximized )
			{
				CurrentGraphForm.SuspendLayout();
				CurrentGraphForm.ZedGraphControl.Visible = false;
			}
		}

		private void seems_ResizeEnd( object sender, EventArgs e )
		{
			if( CurrentGraphForm != null && CurrentGraphForm.WindowState == FormWindowState.Maximized )
			{
				CurrentGraphForm.ResumeLayout();
				CurrentGraphForm.ZedGraphControl.Visible = true;
				CurrentGraphForm.Refresh();
			}
		}

		private void dataSourceComboBox_SelectedIndexChanged( object sender, EventArgs e )
		{
			if( CurrentGraphForm != null && dataSourceComboBox.SelectedItem != null )
			{
				scanNumberComboBox.BeginUpdate();
				scanNumberComboBox.Items.Clear();
				foreach( GraphItem graphItem in (dataSourceComboBox.SelectedItem as DataSource).CurrentGraphItems )
					scanNumberComboBox.Items.Add( graphItem );
				scanNumberComboBox.EndUpdate();
				scanNumberComboBox.Refresh();

				if( scanNumberComboBox.Items.Count > 0 && scanNumberComboBox.SelectedItem != null )
					setScanControls( true );
				else
					setScanControls( false );
			}
		}

		private void scanNumberComboBox_SelectedIndexChanged( object sender, EventArgs e )
		{
			if( scanNumberComboBox.SelectedIndex < 0 )
			{
				CurrentGraphForm.ZedGraphControl.Visible = false;
				return;
			}

			CurrentGraphForm.ZedGraphControl.Visible = true;
			setScanControls( true );

			bool doCentroid = CentroidMenuItem.Enabled && CentroidMenuItem.Checked;
			//CurrentGraphForm.DataSource.MSDataFile.setCentroiding( doCentroid, doCentroid, UseVendorCentroidMenuItem.Checked );
			CurrentGraphForm.ShowData( (DataSource) dataSourceComboBox.SelectedItem, scanNumberComboBox.SelectedIndex );
			scanNumberComboBox.TextBox.Refresh();
			CurrentGraphForm.ZedGraphControl.Focus();
			//Application.DoEvents();
		}

		void scanNumberComboBoxListBox_FormClosed( object sender, FormClosedEventArgs e )
		{
			if( e.CloseReason == CloseReason.FormOwnerClosing &&
				CurrentGraphForm.DataSource != (DataSource) dataSourceComboBox.SelectedItem )
			{
				CurrentGraphForm.ShowData( (DataSource) dataSourceComboBox.SelectedItem, scanNumberComboBox.SelectedIndex );
				scanNumberComboBox.TextBox.Refresh();
				CurrentGraphForm.ZedGraphControl.Focus();
				//Application.DoEvents();
			}
		}

		private int overlaySelectedIndex;
		void scanNumberComboBoxContextMenuStrip_Opened( object sender, EventArgs e )
		{
			overlaySelectedIndex = scanNumberComboBox.ListBox.SelectedIndex;
		}

		private void openInActiveWindowToolStripMenuItem_Click( object sender, EventArgs e )
		{
			scanNumberComboBox.SelectedIndex = scanNumberComboBox.ListBox.SelectedIndex;
			scanNumberComboBox.Button.PerformClick();
		}

		private void openInNewWindowToolStripMenuItem_Click( object sender, EventArgs e )
		{
			GraphForm oldForm = CurrentGraphForm;
			int oldSelectedIndex = oldForm.CurrentGraphItemIndex;
			GraphForm graphForm = GraphForm.CreateNewWindow( this, false );
			graphForm.ShowData( (DataSource) dataSourceComboBox.SelectedItem, scanNumberComboBox.ListBox.SelectedIndex );
			scanNumberComboBox.SelectedIndex = oldSelectedIndex;
			oldForm.Activate();
			scanNumberComboBox.Button.PerformClick();
		}

		private void overlayOnActiveWindowToolStripMenuItem_Click( object sender, EventArgs e )
		{
			CurrentGraphForm.ShowDataOverlay( (DataSource) dataSourceComboBox.SelectedItem, overlaySelectedIndex );
			scanNumberComboBox.Button.PerformClick();
		}

		Point integratePeaksMouseDownLocation;
		Point integratePeaksMouseUpLocation;
		ZedGraph.LineItem integratePeaksLine;
		List<ZedGraph.PolyObj> integratePeaksAreas = new List<ZedGraph.PolyObj>();

		private void setPeakIntegrationMode( bool enabled )
		{
			if( peakIntegrationMode.Checked )
			{
				integratePeaksToolStripButton.Image = Properties.Resources.PeakIntegralActive;
				CurrentGraphForm.ZedGraphControl.IsEnableHZoom = false;
				CurrentGraphForm.ZedGraphControl.MouseDownEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler( ZedGraphControl_MouseDownEvent );
				CurrentGraphForm.ZedGraphControl.MouseUpEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler( ZedGraphControl_MouseUpEvent );
				CurrentGraphForm.ZedGraphControl.MouseMoveEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler( ZedGraphControl_MouseMoveEvent );
			} else
			{
				integratePeaksToolStripButton.Image = Properties.Resources.PeakIntegral;
				CurrentGraphForm.ZedGraphControl.IsEnableHZoom = true;
				CurrentGraphForm.ZedGraphControl.MouseDownEvent -= new ZedGraph.ZedGraphControl.ZedMouseEventHandler( ZedGraphControl_MouseDownEvent );
				CurrentGraphForm.ZedGraphControl.MouseUpEvent -= new ZedGraph.ZedGraphControl.ZedMouseEventHandler( ZedGraphControl_MouseUpEvent );
				CurrentGraphForm.ZedGraphControl.MouseMoveEvent -= new ZedGraph.ZedGraphControl.ZedMouseEventHandler( ZedGraphControl_MouseMoveEvent );
			}
		}

		private void integratePeaksButton_Click( object sender, EventArgs e )
		{
			peakIntegrationMode.Checked = !peakIntegrationMode.Checked;
			setPeakIntegrationMode( peakIntegrationMode.Checked );
		}

		bool ZedGraphControl_MouseMoveEvent( ZedGraph.ZedGraphControl sender, MouseEventArgs e )
		{
			if( CurrentGraphForm == null || !peakIntegrationMode.Checked || !CurrentGraphForm.ZedGraphControl.Focused )
				return false;

			if( integratePeaksLine != null && e.Button == MouseButtons.Left )
			{
				double x = CurrentGraphForm.ZedGraphControl.GraphPane.XAxis.Scale.ReverseTransform( (float) e.X );
				double y = CurrentGraphForm.ZedGraphControl.GraphPane.YAxis.Scale.ReverseTransform( (float) e.Y );
				//CurrentGraphForm.ZedGraphControl.GraphPane.GraphObjList.Remove( integratePeaksLine );
				//integratePeaksLine = new ZedGraph.LineItem( integratePeaksLine.Location.X1, integratePeaksLine.Location.Y1, x, y );
				//integratePeaksLine.Location.CoordinateFrame = ZedGraph.CoordType.AxisXYScale;
				//CurrentGraphForm.ZedGraphControl.GraphPane.GraphObjList.Add( integratePeaksLine );
				integratePeaksLine.Points[1].X = Math.Min( CurrentGraphForm.ZedGraphControl.GraphPane.XAxis.Scale.Max, Math.Max( CurrentGraphForm.ZedGraphControl.GraphPane.XAxis.Scale.Min, x ) );
				integratePeaksLine.Points[1].Y = Math.Min( CurrentGraphForm.ZedGraphControl.GraphPane.YAxis.Scale.Max, Math.Max( CurrentGraphForm.ZedGraphControl.GraphPane.YAxis.Scale.Min, y ) );

				if( CurrentGraphForm.ZedGraphControl.GraphPane.CurveList[0].Points is PointList )
				{
					foreach( ZedGraph.PolyObj obj in integratePeaksAreas )
						CurrentGraphForm.ZedGraphControl.GraphPane.GraphObjList.Remove( obj );
					integratePeaksAreas.Clear();

					PointList pointList = (PointList) CurrentGraphForm.ZedGraphControl.GraphPane.CurveList[0].Points;
					if( pointList.ScaledCount == 0 )
						return false;

					double x1, y1, x2, y2;
					if( integratePeaksLine.Points[0].X > integratePeaksLine.Points[1].X )
					{
						x1 = integratePeaksLine.Points[1].X;
						y1 = integratePeaksLine.Points[1].Y;
						x2 = integratePeaksLine.Points[0].X;
						y2 = integratePeaksLine.Points[0].Y;
					} else
					{
						x1 = integratePeaksLine.Points[0].X;
						y1 = integratePeaksLine.Points[0].Y;
						x2 = integratePeaksLine.Points[1].X;
						y2 = integratePeaksLine.Points[1].Y;
					}

					int lowerBoundIndex = pointList.LowerBound( x1 );
					int upperBoundIndex = pointList.LowerBound( x2 );
					if( upperBoundIndex < 0 )
						upperBoundIndex = pointList.ScaledCount - 1;

					double totalIntegratedArea = 0.0;
					int totalAreaPoints = 0;
					int totalAreaCount = 0;

					// integration line can be in any of these states:

					// * entirely to the left of the curve:
					//		- no integration
					// * entirely to the right of the curve:
					//		- no integration
					// * entirely between two consecutive points:
					//		- integration is entirely interpolated based on data slope
					// * starts to the left of the curve and ends between two consecutive points:
					//		- start integration at the X value of the first data point
					//		- end integration at the line's right X value
					// * starts between two consecutive points and ends to the right of the curve:
					//		- start integration at the line's left X value
					//		- end integration at the X value of the last data point
					// * starts between two consecutive points and ends between two different consecutive points:
					//		- start integration at the line's left X value
					//		- end integration at the line's right X value

					// * entirely above the curve:
					//		- no integration
					// * starts above the curve and ends below the curve:
					//		- start integration at the first intersection of the line with the curve
					//		- end integration at the X value where the line ends
					// * starts below the curve and ends above the curve:
					//		- start integration at the X value where the line starts
					//		- end integration at the last intersection of the line with the curve
					// * entirely below the curve:
					//		- start and end integration at the X values of the line

					// * the Y value of the line's start point is less than 0 and the end point's is not:
					//		- a special area point must be added before the first area's bottom left point
					//		- add it at the intersection of the line with the X axis
					// * the Y value of the line's end point is less than 0 and the start point's is not:
					//		- a special area point must be added after the last area's bottom right point
					//		- add it at the intersection of the line with the X axis
					// * the Y values of both the start and end points of the line are less than 0:
					//		- no special points are necessary

					if( lowerBoundIndex >= 0 && x2 > x1 )
					{
						// calculate the linear function for integration line
						double integratePeaksLineA = ( y2 - y1 ) / ( x2 - x1 );
						double integratePeaksLineB = y1 - ( integratePeaksLineA * x1 );

						// restrict the X range of the integration line to the minimum and maximum X values of the curve
						// interpolate the Y value of the integration line at those X values
						double leftInterpolatedX = Math.Max( pointList.ScaledList[0].X, x1 );
						double leftInterpolatedY = Math.Max( 0, integratePeaksLineA * leftInterpolatedX + integratePeaksLineB );
						double rightInterpolatedX = Math.Min( pointList.ScaledList[pointList.ScaledCount - 1].X, x2 );
						double rightInterpolatedY = Math.Max( 0, integratePeaksLineA * rightInterpolatedX + integratePeaksLineB );
						
						List<ZedGraph.PointD> areaPoints = new List<ZedGraph.PointD>();
						//List<List<ZedGraph.PointD>> areaTrapezoids = new List<List<ZedGraph.PointD>>();
						for( int i = Math.Max( 1, lowerBoundIndex ); i <= upperBoundIndex; ++i )
						{
							ZedGraph.PointPair rightPoint = pointList.ScaledList[i];
							ZedGraph.PointPair leftPoint = pointList.ScaledList[i - 1];

							// interpolate the Y value of the integration line at the previous and current points' X value
							double lastPointLineY = integratePeaksLineA * leftPoint.X + integratePeaksLineB;
							double curPointLineY = integratePeaksLineA * rightPoint.X + integratePeaksLineB;

							// calculate the linear function between the previous and current points
							double dataA = ( rightPoint.Y - leftPoint.Y ) / ( rightPoint.X - leftPoint.X );
							double dataB = rightPoint.Y - ( dataA * rightPoint.X );

							double leftInterpolatedPointY = dataA * leftInterpolatedX + dataB;
							double rightInterpolatedPointY = dataA * rightInterpolatedX + dataB;

							bool leftInterpolatedPointIsAboveLine = ( leftInterpolatedPointY >= leftInterpolatedY );
							bool rightInterpolatedPointIsAboveLine = ( rightInterpolatedPointY >= rightInterpolatedY );

							bool leftPointIsAboveLine = ( leftPoint.Y > lastPointLineY );
							bool rightPointIsAboveLine = ( rightPoint.Y > curPointLineY );

							bool leftPointIsLowerBound = ( i == 1 || i == lowerBoundIndex );
							bool rightPointIsUpperBound = ( i == upperBoundIndex );

							if( !leftInterpolatedPointIsAboveLine && !rightInterpolatedPointIsAboveLine ||
								!leftPointIsAboveLine && !rightPointIsAboveLine )
								continue;

							bool needIntersection = ( leftInterpolatedPointIsAboveLine != rightInterpolatedPointIsAboveLine );

							bool areaIsEmpty = ( areaPoints.Count == 0 );

							if( rightPointIsAboveLine || leftPointIsAboveLine )
							{
								if( areaIsEmpty ) // start a new area
								{
									if( leftPointIsLowerBound && leftInterpolatedPointIsAboveLine ) // interpolate the point on the curve above the line
									{
										if( y1 <= 0 && y2 > 0 )
										{
											double croppedBottomX = -integratePeaksLineB / integratePeaksLineA;
											if( croppedBottomX != leftInterpolatedX )
												areaPoints.Add( new ZedGraph.PointD( croppedBottomX, 0 ) );
										}
										areaPoints.Add( new ZedGraph.PointD( leftInterpolatedX, leftInterpolatedY ) ); // bottom left
										areaPoints.Add( new ZedGraph.PointD( leftInterpolatedX, leftInterpolatedPointY ) ); // top left

									} else if( needIntersection ) // interpolate the intersection of line and curve
									{
										double intersectX = ( dataB - integratePeaksLineB ) / ( integratePeaksLineA - dataA );
										double intersectY = dataA * intersectX + dataB;

										areaPoints.Add( new ZedGraph.PointD( intersectX, intersectY ) );
									}
								}

								if( rightPointIsUpperBound ) // end at the upper bound and add current area to the area list
								{
									if( rightInterpolatedPointIsAboveLine )
									{
										// add a new point to the current area
										//areaPoints.Add( new ZedGraph.PointD( pointList.ScaledList[i].X, pointList.ScaledList[i].Y ) );

										areaPoints.Add( new ZedGraph.PointD( rightInterpolatedX, rightInterpolatedPointY ) ); // top right
										areaPoints.Add( new ZedGraph.PointD( rightInterpolatedX, rightInterpolatedY ) ); // bottom right
										if( y2 <= 0 && y1 > 0 ) // add another point if line extends below X axis
										{
											double croppedBottomX = -integratePeaksLineB / integratePeaksLineA;
											if( croppedBottomX != rightInterpolatedX )
												areaPoints.Add( new ZedGraph.PointD( croppedBottomX, 0 ) );
										}
									} else if( needIntersection ) // interpolate the intersection of line and curve
									{
										double intersectX = ( dataB - integratePeaksLineB ) / ( integratePeaksLineA - dataA );
										double intersectY = dataA * intersectX + dataB;

										areaPoints.Add( new ZedGraph.PointD( intersectX, intersectY ) );
									}


									if( areaPoints.Count == 0 )
										continue;

									ZedGraph.PolyObj integratePeaksArea = new ZedGraph.PolyObj( areaPoints.ToArray(), Color.Black, Color.Cyan, Color.Cyan );
									integratePeaksArea.Location.CoordinateFrame = ZedGraph.CoordType.AxisXYScale;
									//integratePeaksArea.IsClosedFigure = true;
									areaPoints.Add( areaPoints[0] );
									integratePeaksAreas.Add( integratePeaksArea );

									double currentIntegratedArea = 0.0;
									for( int k, j = 0; j < areaPoints.Count; ++j )
									{
										k = ( j + 1 ) % areaPoints.Count;
										currentIntegratedArea += areaPoints[j].X * areaPoints[k].Y;
										currentIntegratedArea -= areaPoints[j].Y * areaPoints[k].X;
									}
									totalIntegratedArea += Math.Abs( currentIntegratedArea / 2.0 );
									totalAreaPoints += areaPoints.Count - 1;
									++totalAreaCount;
									areaPoints.Clear();
								} else
								{
									// add a new top right point to the current area
									areaPoints.Add( new ZedGraph.PointD( pointList.ScaledList[i].X, pointList.ScaledList[i].Y ) );
								}

							}

							if( !rightPointIsAboveLine && !rightPointIsUpperBound )// close the current area and add it to the area list
							{
								double intersectX = ( dataB - integratePeaksLineB ) / ( integratePeaksLineA - dataA );
								double intersectY = dataA * intersectX + dataB;

								areaPoints.Add( new ZedGraph.PointD( intersectX, intersectY ) );

								if( areaPoints.Count == 0 )
									continue;

								ZedGraph.PolyObj integratePeaksArea = new ZedGraph.PolyObj( areaPoints.ToArray(), Color.Black, Color.Cyan, Color.Cyan );
								integratePeaksArea.Location.CoordinateFrame = ZedGraph.CoordType.AxisXYScale;
								//integratePeaksArea.IsClosedFigure = true;
								areaPoints.Add( areaPoints[0] );
								integratePeaksAreas.Add( integratePeaksArea );
								double currentIntegratedArea = 0.0;
								for( int k, j = 0; j < areaPoints.Count; ++j )
								{
									k = ( j + 1 ) % areaPoints.Count;
									currentIntegratedArea += areaPoints[j].X * areaPoints[k].Y;
									currentIntegratedArea -= areaPoints[j].Y * areaPoints[k].X;
								}
								totalIntegratedArea += Math.Abs( currentIntegratedArea / 2.0 );
								totalAreaPoints += areaPoints.Count - 1;
								++totalAreaCount;
								areaPoints.Clear();
							}
						}

								/*if( areaPoints.Count > 2 )
								{
									List<ZedGraph.PointD> areaTrapezoid = new List<ZedGraph.PointD>();
									areaTrapezoid.Add( areaPoints[areaPoints.Count - 3] ); // top left
									areaTrapezoid.Add( areaPoints[areaPoints.Count - 2] ); // top right
									areaTrapezoid.Add( areaPoints[areaPoints.Count - 1] ); // bottom right

									// bottom left
									double bottomLeftY = Math.Max( 0, integratePeaksLineA * areaPoints[areaPoints.Count - 3].X + integratePeaksLineB );
									areaTrapezoid.Add( new ZedGraph.PointD( areaPoints[areaPoints.Count - 3].X, bottomLeftY ) );

									areaTrapezoids.Add( areaTrapezoid );
								}*/
						/*foreach( List<ZedGraph.PointD> trapezoidPoints in areaTrapezoids )
						{
							ZedGraph.PolyObj trapezoid = new ZedGraph.PolyObj( trapezoidPoints.ToArray(), Color.Black, Color.Green, Color.Green );
							trapezoid.Location.CoordinateFrame = ZedGraph.CoordType.AxisXYScale;
							trapezoid.IsClosedFigure = true;
							integratePeaksAreas.Add( trapezoid );
						}*/
						foreach( ZedGraph.PolyObj obj in integratePeaksAreas )
							CurrentGraphForm.ZedGraphControl.GraphPane.GraphObjList.Add( obj );
						//CurrentGraphForm.ZedGraphControl.GraphPane.Title.Text = totalIntegratedArea.ToString("f0") + " " + totalAreaPoints + " " + totalAreaCount;
						//CurrentGraphForm.ZedGraphControl.GraphPane.Title.IsVisible = true;
						CurrentGraphForm.CurrentGraphItem.TotalIntegratedArea = totalIntegratedArea;
						int currentIndex = scanNumberComboBox.SelectedIndex;
						object currentObject = scanNumberComboBox.SelectedItem;
						scanNumberComboBox.Items.RemoveAt( currentIndex );
						scanNumberComboBox.Items.Insert( currentIndex, currentObject );
					}
				}
				CurrentGraphForm.ZedGraphControl.Refresh();
				return false;
			}
			return true;
		}

		bool ZedGraphControl_MouseUpEvent( ZedGraph.ZedGraphControl sender, MouseEventArgs e )
		{
			if( CurrentGraphForm == null || !peakIntegrationMode.Checked || !CurrentGraphForm.ZedGraphControl.Focused )
				return false;

			int x0 = (int) Math.Round( CurrentGraphForm.ZedGraphControl.GraphPane.XAxis.Scale.Transform( integratePeaksLine.Points[0].X ) );
			int y0 = (int) Math.Round( CurrentGraphForm.ZedGraphControl.GraphPane.YAxis.Scale.Transform( integratePeaksLine.Points[0].Y ) );
			int x1 = (int) Math.Round( CurrentGraphForm.ZedGraphControl.GraphPane.XAxis.Scale.Transform( integratePeaksLine.Points[1].X ) );
			int y1 = (int) Math.Round( CurrentGraphForm.ZedGraphControl.GraphPane.YAxis.Scale.Transform( integratePeaksLine.Points[1].Y ) );
			integratePeaksMouseDownLocation.X = x0;
			integratePeaksMouseDownLocation.Y = y0;
			integratePeaksMouseUpLocation.X = x1;
			integratePeaksMouseUpLocation.Y = y1;
			return false;
		}

		bool ZedGraphControl_MouseDownEvent( ZedGraph.ZedGraphControl sender, MouseEventArgs e )
		{
			if( CurrentGraphForm == null || !peakIntegrationMode.Checked || !CurrentGraphForm.ZedGraphControl.Focused )
				return false;

			if( e.Button == MouseButtons.Left )
			{
				if( integratePeaksLine != null )
				{
					double distanceToMouseDownLocation = Math.Sqrt( Math.Pow( e.Location.X - integratePeaksMouseDownLocation.X, 2.0 ) +
														Math.Pow( e.Location.Y - integratePeaksMouseDownLocation.Y, 2.0 ) );
					if( distanceToMouseDownLocation < 5.0 )
					{
						ZedGraph.PointPair tmp = integratePeaksLine.Points[1].Clone();
						integratePeaksLine.Points[1].X = integratePeaksLine.Points[0].X;
						integratePeaksLine.Points[1].Y = integratePeaksLine.Points[0].Y;
						integratePeaksLine.Points[0].X = tmp.X;
						integratePeaksLine.Points[0].Y = tmp.Y;
						Point tmp2 = integratePeaksMouseUpLocation;
						integratePeaksMouseUpLocation = integratePeaksMouseDownLocation;
						integratePeaksMouseDownLocation = tmp2;
						return false;
					} else
					{
						double distanceToMouseUpLocation = Math.Sqrt( Math.Pow( e.Location.X - integratePeaksMouseUpLocation.X, 2.0 ) +
														Math.Pow( e.Location.Y - integratePeaksMouseUpLocation.Y, 2.0 ) );
						if( distanceToMouseUpLocation >= 5.0 )
						{
							// clear existing line and start a new one
							CurrentGraphForm.ZedGraphControl.GraphPane.CurveList.Remove( integratePeaksLine );
							integratePeaksLine = null;
							foreach( ZedGraph.PolyObj obj in integratePeaksAreas )
								CurrentGraphForm.ZedGraphControl.GraphPane.GraphObjList.Remove( obj );
							integratePeaksAreas.Clear();
							CurrentGraphForm.ZedGraphControl.Refresh();
						} else
							return false;
					}
				}

				integratePeaksMouseDownLocation = e.Location;
				double x = CurrentGraphForm.ZedGraphControl.GraphPane.XAxis.Scale.ReverseTransform( (float) e.X );
				double y = CurrentGraphForm.ZedGraphControl.GraphPane.YAxis.Scale.ReverseTransform( (float) e.Y );
				x = Math.Min( CurrentGraphForm.ZedGraphControl.GraphPane.XAxis.Scale.Max, Math.Max( CurrentGraphForm.ZedGraphControl.GraphPane.XAxis.Scale.Min, x ) );
				y = Math.Min( CurrentGraphForm.ZedGraphControl.GraphPane.YAxis.Scale.Max, Math.Max( CurrentGraphForm.ZedGraphControl.GraphPane.YAxis.Scale.Min, y ) );
				integratePeaksLine = new ZedGraph.LineItem( "", new double[] { x, x }, new double[] { y, y }, Color.Black, ZedGraph.SymbolType.None );
				integratePeaksLine.Line.IsAntiAlias = true;
				integratePeaksLine.Symbol.Type = ZedGraph.SymbolType.Square;
				integratePeaksLine.Symbol.IsVisible = true;
				integratePeaksLine.Symbol.Border.Width = 2;
				integratePeaksLine.Symbol.Border.Color = Color.Black;
				//integratePeaksLine.Location.CoordinateFrame = ZedGraph.CoordType.AxisXYScale;
				CurrentGraphForm.ZedGraphControl.GraphPane.CurveList.Add( integratePeaksLine );
			}
			return false;
		}

		private void clearCurrentIntegration_Click( object sender, EventArgs e )
		{
			if( integratePeaksLine != null )
			{
				// clear existing line and start a new one
				CurrentGraphForm.ZedGraphControl.GraphPane.CurveList.Remove( integratePeaksLine );
				integratePeaksLine = null;
				foreach( ZedGraph.PolyObj obj in integratePeaksAreas )
					CurrentGraphForm.ZedGraphControl.GraphPane.GraphObjList.Remove( obj );
				integratePeaksAreas.Clear();
				CurrentGraphForm.ZedGraphControl.Refresh();
			}

			CurrentGraphForm.CurrentGraphItem.TotalIntegratedArea = 0.0;
			int currentIndex = scanNumberComboBox.SelectedIndex;
			object currentObject = scanNumberComboBox.SelectedItem;
			scanNumberComboBox.Items.RemoveAt( currentIndex );
			scanNumberComboBox.Items.Insert( currentIndex, currentObject );
		}

		private void clearAllIntegrationsToolStripMenuItem_Click( object sender, EventArgs e )
		{
			if( integratePeaksLine != null )
			{
				// clear existing line and start a new one
				CurrentGraphForm.ZedGraphControl.GraphPane.CurveList.Remove( integratePeaksLine );
				integratePeaksLine = null;
				foreach( ZedGraph.PolyObj obj in integratePeaksAreas )
					CurrentGraphForm.ZedGraphControl.GraphPane.GraphObjList.Remove( obj );
				integratePeaksAreas.Clear();
				CurrentGraphForm.ZedGraphControl.Refresh();
			}

			scanNumberComboBox.BeginUpdate();
			scanNumberComboBox.Items.Clear();
			foreach( GraphItem graphItem in ( dataSourceComboBox.SelectedItem as DataSource ).CurrentGraphItems )
			{
				graphItem.TotalIntegratedArea = 0.0;
				scanNumberComboBox.Items.Add( graphItem );
			}
			scanNumberComboBox.EndUpdate();
			scanNumberComboBox.Refresh();
		}

		private void exportAllIntegrationsToolStripMenuItem_Click( object sender, EventArgs e )
		{
			SaveFileDialog exportDialog = new SaveFileDialog();
			exportDialog.InitialDirectory = Path.GetDirectoryName( CurrentGraphForm.CurrentSourceFilepath );
			exportDialog.OverwritePrompt = true;
			exportDialog.RestoreDirectory = true;
			exportDialog.FileName = Path.GetFileNameWithoutExtension( CurrentGraphForm.CurrentSourceFilepath ) + "-peaks.csv";
			if( exportDialog.ShowDialog() == DialogResult.OK )
			{
				StreamWriter writer = new StreamWriter( exportDialog.FileName );
				writer.WriteLine( "Id,Area" );
				foreach( GraphItem graphItem in ( dataSourceComboBox.SelectedItem as DataSource ).CurrentGraphItems )
					writer.WriteLine( "{0},{1}", graphItem.Id, graphItem.TotalIntegratedArea );
				writer.Close();
			}
		}
	}
}