using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using DigitalRune.Windows.Docking;
using pwiz.CLI.msdata;
using pwiz.CLI.analysis;
using MSGraph;
using ZedGraph;
using CommandLine.Utility;

namespace seems
{
	/// <summary>
	/// Maps the filepath of a data source to its associated ManagedDataSource object
	/// </summary>
	using DataSourceMap = Map<string, ManagedDataSource>;
	using GraphInfoMap = Map<GraphItem, List<RefPair<DataGridViewRow, GraphForm>>>;
	using GraphInfoList = List<RefPair<DataGridViewRow, GraphForm>>;
	using GraphInfo = RefPair<DataGridViewRow, GraphForm>;

	/// <summary>
	/// Contains the associated spectrum and chromatogram lists for a data source
	/// </summary>
	public class ManagedDataSource
	{
		public ManagedDataSource() { }

		public ManagedDataSource( SpectrumSource source )
		{
			this.source = source;
			spectrumListForm = new SpectrumListForm();
			chromatogramListForm = new ChromatogramListForm();
            spectrumDataProcessing = new DataProcessing();
            //chromatogramDataProcessing = new DataProcessing();
			//graphInfoMap = new GraphInfoMap();
		}

		private SpectrumSource source;
		public SpectrumSource Source { get { return source; } }

		private SpectrumListForm spectrumListForm;
		public SpectrumListForm SpectrumListForm { get { return spectrumListForm; } }

		private ChromatogramListForm chromatogramListForm;
		public ChromatogramListForm ChromatogramListForm { get { return chromatogramListForm; } }

        public DataProcessing spectrumDataProcessing;
        //public DataProcessing chromatogramDataProcessing;

		//private GraphInfoMap graphInfoMap;
		//public GraphInfoMap GraphInfoMap { get { return graphInfoMap; } }

        public Chromatogram GetChromatogram( int index )
        { return GetChromatogram( index, source.MSDataFile.run.chromatogramList ); }

        public Chromatogram GetChromatogram( int index, ChromatogramList chromatogramList )
        { return new Chromatogram( this, index, chromatogramList ); }

        public Chromatogram GetChromatogram( Chromatogram metaChromatogram, ChromatogramList chromatogramList )
        {
            Chromatogram chromatogram = new Chromatogram( metaChromatogram, chromatogramList.chromatogram( metaChromatogram.Index, true ) );
            return chromatogram;
        }

        public MassSpectrum GetMassSpectrum( int index )
        { return GetMassSpectrum( index, source.MSDataFile.run.spectrumList ); }

        public MassSpectrum GetMassSpectrum( int index, SpectrumList spectrumList )
        { return new MassSpectrum( this, index, spectrumList ); }

        public MassSpectrum GetMassSpectrum( MassSpectrum metaSpectrum, SpectrumList spectrumList )
        {
            MassSpectrum spectrum = new MassSpectrum( metaSpectrum, spectrumList.spectrum( metaSpectrum.Index, true ) );
            //MassSpectrum realMetaSpectrum = ( metaSpectrum.Tag as DataGridViewRow ).Tag as MassSpectrum;
            //realMetaSpectrum.Element.dataProcessing = spectrum.Element.dataProcessing;
            //realMetaSpectrum.Element.defaultArrayLength = spectrum.Element.defaultArrayLength;
            return spectrum;
        }
	}


	/// <summary>
	/// Maps the filepath of a data source to its associated ManagedDataSource object
	/// </summary>
	//public class DataSourceMap : Map<string, ManagedDataSource> { }

	/// <summary>
	/// Manages the application
	/// Tracks data sources, their spectrum/chromatogram lists, and any associated graph forms
	/// Handles events from sources, lists, and graph forms
	/// </summary>
	public class Manager
	{
		private seemsForm mainForm;
		private DataSourceMap dataSourceMap;

        private SpectrumProcessingForm spectrumProcessingForm;
        private SpectrumAnnotationForm spectrumAnnotationForm;
        private DataProcessing spectrumGlobalDataProcessing;

        public IList<GraphForm> CurrentGraphFormList
        {
            get
            {
                List<GraphForm> graphFormList = new List<GraphForm>();
                foreach( IDockableForm form in mainForm.DockPanel.Documents )
                {
                    if( form is GraphForm )
                        graphFormList.Add( form as GraphForm );
                }
                return graphFormList;
            }
        }

        public Manager( seemsForm mainForm )
        {
            this.mainForm = mainForm;
            dataSourceMap = new DataSourceMap();

            spectrumProcessingForm = new SpectrumProcessingForm();
            spectrumProcessingForm.ProcessingChanged += new EventHandler( processingListView_Changed );
            spectrumProcessingForm.GlobalProcessingOverrideButton.Click += new EventHandler( processingOverrideButton_Click );
            spectrumProcessingForm.RunProcessingOverrideButton.Click += new EventHandler( processingOverrideButton_Click );
            spectrumProcessingForm.GotFocus += new EventHandler( form_GotFocus );
            spectrumProcessingForm.HideOnClose = true;

            spectrumAnnotationForm = new SpectrumAnnotationForm();
            spectrumAnnotationForm.AnnotationChanged += new EventHandler( spectrumAnnotationForm_AnnotationChanged );
            spectrumAnnotationForm.GotFocus += new EventHandler( form_GotFocus );
            spectrumAnnotationForm.HideOnClose = true;

            spectrumGlobalDataProcessing = new DataProcessing();

            LoadDefaultAnnotationSettings();
        }

        public void ParseArgs( string[] args )
        {
            try
            {
                Arguments argParser = new Arguments( args );

                if( argParser["help"] != null ||
                    argParser["h"] != null ||
                    argParser["?"] != null )
                {
                    Console.WriteLine( "TODO" );
                    mainForm.Close();
                    return;
                }

                mainForm.BringToFront();
                mainForm.Focus();
                mainForm.Activate();
                mainForm.Show();
                Application.DoEvents();

                if( argParser["datasource"] != null )
                {
                    if( argParser["spectrum"] != null )
                    {
                        if( argParser["annotation"] != null )
                            OpenFile( argParser["datasource"], Convert.ToInt32( argParser["spectrum"] ), argParser["annotation"] );
                        else
                            OpenFile( argParser["datasource"], Convert.ToInt32( argParser["spectrum"] ) );
                    } else
                        OpenFile( argParser["datasource"] );
                }
            } catch( Exception ex )
            {
                string message = ex.Message;
                if( ex.InnerException != null )
                    message += "\n\nAdditional information: " + ex.InnerException.Message;
                MessageBox.Show( message,
                                "Error parsing command line arguments",
                                MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
                                0, false );
            }
        }

        public void OpenFile( string filepath )
        {
            OpenFile( filepath, -1 );
        }

        public void OpenFile(string filepath, int index)
		{
            OpenFile( filepath, -1, "" );
        }

        public void OpenFile( string filepath, int index, string annotation )
        {
			try
			{
				mainForm.SetProgressPercentage(0);
				mainForm.SetStatusLabel("Opening source file.");

				DataSourceMap.InsertResult insertResult = dataSourceMap.Insert( filepath, null );
				if( insertResult.WasInserted )
				{
					// file was not already open; create a new data source
					insertResult.Element.Value = new ManagedDataSource( new SpectrumSource( filepath ) );
					initializeManagedDataSource( insertResult.Element.Value, index, annotation );
				} else
				{
					GraphForm newGraph = OpenGraph( true );
					SpectrumSource source = insertResult.Element.Value.Source;
					if( source.Chromatograms.Count > 0 )
                        showData(newGraph, source.Chromatograms[0] );
					else
                        showData(newGraph, source.Spectra[0] );
				}

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

		public GraphForm CreateGraph()
		{
			GraphForm graphForm = new GraphForm();
            graphForm.ZedGraphControl.PreviewKeyDown += new PreviewKeyDownEventHandler( graphForm_PreviewKeyDown );
            graphForm.GotFocus += new EventHandler( form_GotFocus );
            graphForm.ItemGotFocus += new EventHandler( form_GotFocus );
            return graphForm;
		}

        public GraphForm OpenGraph( bool giveFocus )
        {
            GraphForm graphForm = new GraphForm();
            graphForm.ZedGraphControl.PreviewKeyDown += new PreviewKeyDownEventHandler( graphForm_PreviewKeyDown );
            graphForm.GotFocus += new EventHandler( form_GotFocus );
            graphForm.ItemGotFocus += new EventHandler( form_GotFocus );
            graphForm.Show( mainForm.DockPanel, DockState.Document );
            if( giveFocus )
                graphForm.Activate();

            return graphForm;
        }

        void form_GotFocus( object sender, EventArgs e )
        {
            GraphForm form = sender as GraphForm;
            if( form != null &&
                form.FocusedPane != null &&
                form.FocusedPane.CurrentItemType == MSGraphItemType.Spectrum )
            {
                mainForm.AnnotationButton.Enabled = true;
                mainForm.DataProcessingButton.Enabled = true;
                mainForm.SetStatusLabel( form.FocusedItem.Label.Text + " item got focus." );

                //spectrumProcessingForm.UpdateProcessing( form.FocusedItem.Tag as MassSpectrum );
                spectrumAnnotationForm.UpdateAnnotations( form.FocusedItem.Tag as MassSpectrum );
            } else
            {
                mainForm.AnnotationButton.Enabled = false;
                mainForm.DataProcessingButton.Enabled = false;
                mainForm.SetStatusLabel( sender.ToString() + " form got focus." );
            }
        }

        private MassSpectrum getMetaSpectrum( MassSpectrum spectrum )
        {
            return spectrum.OwningListForm.GetSpectrum( spectrum.OwningListForm.IndexOf( spectrum ) );
        }

		private void initializeManagedDataSource( ManagedDataSource managedDataSource, int index, string annotation )
        {
			try
			{
				SpectrumSource source = managedDataSource.Source;
				MSDataFile msDataFile = source.MSDataFile;
				ChromatogramListForm chromatogramListForm = managedDataSource.ChromatogramListForm;
				SpectrumListForm spectrumListForm = managedDataSource.SpectrumListForm;

				chromatogramListForm.Text = source.Name + " chromatograms";
				chromatogramListForm.TabText = source.Name + " chromatograms";
				chromatogramListForm.ShowIcon = false;
                chromatogramListForm.CellDoubleClick += new ChromatogramListCellDoubleClickHandler( chromatogramListForm_CellDoubleClick );
                chromatogramListForm.CellClick += new ChromatogramListCellClickHandler( chromatogramListForm_CellClick );
                chromatogramListForm.GotFocus += new EventHandler( form_GotFocus );

				spectrumListForm.Text = source.Name + " spectra";
				spectrumListForm.TabText = source.Name + " spectra";
				spectrumListForm.ShowIcon = false;
                spectrumListForm.CellDoubleClick += new SpectrumListCellDoubleClickHandler( spectrumListForm_CellDoubleClick );
                spectrumListForm.CellClick += new SpectrumListCellClickHandler( spectrumListForm_CellClick );
                spectrumListForm.FilterChanged += new SpectrumListFilterChangedHandler( spectrumListForm_FilterChanged );
                spectrumListForm.GotFocus += new EventHandler( form_GotFocus );

				bool firstChromatogramLoaded = false;
				bool firstSpectrumLoaded = false;
				GraphForm firstGraph = null;

				ChromatogramList cl = msDataFile.run.chromatogramList;
				SpectrumList sl = msDataFile.run.spectrumList;
                //sl = new SpectrumList_Filter( sl, new SpectrumList_FilterAcceptSpectrum( acceptSpectrum ) );

				if( sl == null )
					throw new Exception( "Error loading metadata: no spectrum list" );
				
                // conditionally load the spectrum at the specified index first
                if( index > -1 )
                {
                    MassSpectrum spectrum = managedDataSource.GetMassSpectrum( index );

                    spectrum.AnnotationSettings = defaultScanAnnotationSettings;
                    spectrumListForm.Add( spectrum );
                    source.Spectra.Add( spectrum );

                    firstSpectrumLoaded = true;
                    spectrumListForm.Show( mainForm.DockPanel, DockState.DockBottom );
                    Application.DoEvents();

                    if( !String.IsNullOrEmpty( annotation ) )
                    {
                        string[] annotationArgs = annotation.Split( " ".ToCharArray() );
                        if( annotationArgs.Length == 3 &&
                            annotationArgs[0] == "pfr" ) // peptide fragmentation
                        {
                            string sequence = annotationArgs[1];
                            string seriesArgs = annotationArgs[2];
                            string[] seriesList = seriesArgs.Split( ",".ToCharArray() );
                            bool a, b, c, x, y, z;
                            a = b = c = x = y = z = false;
                            foreach( string series in seriesList )
                                switch( series )
                                {
                                    case "a": a = true; break;
                                    case "b": b = true; break;
                                    case "c": c = true; break;
                                    case "x": x = true; break;
                                    case "y": y = true; break;
                                    case "z": z = true; break;
                                }
                            IAnnotation pfr = new PeptideFragmentationAnnotation( sequence, a, b, c, x, y, z );
                            spectrum.AnnotationList.Add( pfr );
                        }
                    }

                    firstGraph = OpenGraph( true );
                    showData( firstGraph, spectrum );
                }

				int ticIndex = 0;
				if( cl != null )
				{
					ticIndex = cl.findNative( "TIC" );
					if( ticIndex < cl.size() )
					{
						pwiz.CLI.msdata.Chromatogram tic = cl.chromatogram( ticIndex );
                        Chromatogram ticChromatogram = managedDataSource.GetChromatogram( ticIndex );
                        ticChromatogram.AnnotationSettings = defaultChromatogramAnnotationSettings;
						chromatogramListForm.Add( ticChromatogram );
						source.Chromatograms.Add( ticChromatogram );
                        if( !firstSpectrumLoaded )
                        {
                            firstGraph = OpenGraph( true );
                            showData( firstGraph, ticChromatogram );
                            firstChromatogramLoaded = true;
                            chromatogramListForm.Show( mainForm.DockPanel, DockState.DockBottom );
                            Application.DoEvents();
                        }
					}
				}

                // get spectrum type from fileContent if possible, otherwise from first spectrum
				CVParam spectrumType = msDataFile.fileDescription.fileContent.cvParamChild( CVID.MS_spectrum_type );
				if( spectrumType.cvid == CVID.CVID_Unknown && !sl.empty() )
					spectrumType = sl.spectrum( 0 ).cvParamChild( CVID.MS_spectrum_type );

				if( cl != null )
				{
					// load the rest of the chromatograms
					for( int i = 0; i < cl.size(); ++i )
					{
						if( i == ticIndex )
							continue;

                        Chromatogram chromatogram = managedDataSource.GetChromatogram( ticIndex );

						mainForm.SetStatusLabel( String.Format( "Loading chromatograms from {2} ({0} of {1})...",
                                        ( i + 1 ), cl.size(), managedDataSource.Source.Name ) );
						mainForm.SetProgressPercentage( ( i + 1 ) * 100 / cl.size() );

                        if( mainForm.IsDisposed )
                            return;

                        chromatogram.AnnotationSettings = defaultChromatogramAnnotationSettings;
						chromatogramListForm.Add( chromatogram );
						source.Chromatograms.Add( chromatogram );
						if( !firstSpectrumLoaded && !firstChromatogramLoaded )
						{
							firstChromatogramLoaded = true;
							chromatogramListForm.Show( mainForm.DockPanel, DockState.DockBottom );
                            Application.DoEvents();
							firstGraph = OpenGraph( true );
                            showData(firstGraph, chromatogram );
						}
						Application.DoEvents();
					}
				}
				
				// get all scans by sequential access
				for( int i = 0; i < sl.size(); ++i )
				{
                    if( i == index ) // skip the preloaded spectrum
                        continue;

                    MassSpectrum spectrum = managedDataSource.GetMassSpectrum( i );

					if( ( ( i + 1 ) % 100 ) == 0 || ( i + 1 ) == sl.size() )
					{
						mainForm.SetStatusLabel( String.Format( "Loading spectra from {2} ({0} of {1})...",
										( i + 1 ), sl.size(), managedDataSource.Source.Name ) );
						mainForm.SetProgressPercentage( ( i + 1 ) * 100 / sl.size() );
					}

                    if( mainForm.IsDisposed )
                        return;

                    spectrum.AnnotationSettings = defaultScanAnnotationSettings;
					spectrumListForm.Add( spectrum );
					source.Spectra.Add( spectrum );
					if( !firstSpectrumLoaded )
					{
						firstSpectrumLoaded = true;
						spectrumListForm.Show( mainForm.DockPanel, DockState.DockBottom );
                        Application.DoEvents();
						if( firstChromatogramLoaded )
						{
							GraphForm spectrumGraph = CreateGraph();
							spectrumGraph.Show( firstGraph.Pane, DockPaneAlignment.Bottom, 0.5 );
                            showData(spectrumGraph, spectrum );
						} else
						{
							firstGraph = OpenGraph( true );
                            showData(firstGraph, spectrum );
						}
					}
					Application.DoEvents();
				}

				mainForm.SetStatusLabel( "Finished loading source metadata." );
				mainForm.SetProgressPercentage( 100 );

			} catch( Exception ex )
			{
				string message = "SeeMS encountered an error reading metadata from \"" + managedDataSource.Source.CurrentFilepath + "\" (" + ex.Message + ")";
				if( ex.InnerException != null )
					message += "\n\nAdditional information: " + ex.InnerException.Message;
				MessageBox.Show( message,
								"Error reading source metadata",
								MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
								0, false );
				mainForm.SetStatusLabel( "Failed to read source metadata." );
			}
		}

        void chromatogramListForm_CellClick( object sender, ChromatogramListCellClickEventArgs e )
        {
            if( e.Chromatogram == null || e.Button != MouseButtons.Right )
                return;

            ChromatogramListForm chromatogramListForm = sender as ChromatogramListForm;

            List<GraphItem> selectedGraphItems = new List<GraphItem>();
            Set<int> selectedRows = new Set<int>();
            foreach( DataGridViewCell cell in chromatogramListForm.GridView.SelectedCells )
            {
                if( selectedRows.Insert( cell.RowIndex ).WasInserted )
                    selectedGraphItems.Add( cell.OwningRow.Tag as GraphItem );
            }

            if( selectedRows.Count == 0 )
                chromatogramListForm.GridView[e.ColumnIndex, e.RowIndex].Selected = true;

            ContextMenuStrip menu = new ContextMenuStrip();
            if( mainForm.CurrentGraphForm != null )
            {
                if( selectedRows.Count == 1 )
                {
                    menu.Items.Add( "Show as Current Graph", null, new EventHandler( graphListForm_showAsCurrentGraph ) );
                    menu.Items.Add( "Overlay on Current Graph", null, new EventHandler( graphListForm_overlayOnCurrentGraph ) );
                    menu.Items.Add( "Stack on Current Graph", null, new EventHandler( graphListForm_stackOnCurrentGraph ) );
                } else
                {
                    menu.Items.Add( "Overlay All on Current Graph", null, new EventHandler( graphListForm_overlayAllOnCurrentGraph ) );
                    menu.Items.Add( "Stack All on Current Graph", null, new EventHandler( graphListForm_showAllAsStackOnCurrentGraph ) );
                }
            }

            if( selectedRows.Count == 1 )
            {
                menu.Items.Add( "Show as New Graph", null, new EventHandler( graphListForm_showAsNewGraph ) );
                menu.Items.Add( "Show Table of Data Points", null, new EventHandler( graphListForm_showTableOfDataPoints ) );
                menu.Items[0].Font = new Font( menu.Items[0].Font, FontStyle.Bold );
                menu.Tag = e.Chromatogram;
            } else
            {
                menu.Items.Add( "Show All as New Graphs", null, new EventHandler( graphListForm_showAllAsNewGraph ) );
                menu.Items.Add( "Overlay All on New Graph", null, new EventHandler( graphListForm_overlayAllOnNewGraph ) );
                menu.Items.Add( "Stack All on New Graph", null, new EventHandler( graphListForm_showAllAsStackOnNewGraph ) );
                menu.Tag = selectedGraphItems;
            }

            menu.Show( Form.MousePosition );
        }

        void spectrumListForm_CellClick( object sender, SpectrumListCellClickEventArgs e )
        {
            if( e.Spectrum == null || e.Button != MouseButtons.Right )
                return;

            SpectrumListForm spectrumListForm = sender as SpectrumListForm;

            List<GraphItem> selectedGraphItems = new List<GraphItem>();
            Set<int> selectedRows = new Set<int>();
            foreach( DataGridViewCell cell in spectrumListForm.GridView.SelectedCells )
            {
                if( selectedRows.Insert( cell.RowIndex ).WasInserted )
                    selectedGraphItems.Add( spectrumListForm.GetSpectrum( cell.RowIndex ) as GraphItem );
            }

            if( selectedRows.Count == 0 )
                spectrumListForm.GridView[e.ColumnIndex, e.RowIndex].Selected = true;

            ContextMenuStrip menu = new ContextMenuStrip();
            if( mainForm.CurrentGraphForm != null )
            {
                if( selectedRows.Count == 1 )
                {
                    menu.Items.Add( "Show as Current Graph", null, new EventHandler( graphListForm_showAsCurrentGraph ) );
                    menu.Items.Add( "Overlay on Current Graph", null, new EventHandler( graphListForm_overlayOnCurrentGraph ) );
                    menu.Items.Add( "Stack on Current Graph", null, new EventHandler( graphListForm_stackOnCurrentGraph ) );
                } else
                {
                    menu.Items.Add( "Overlay All on Current Graph", null, new EventHandler( graphListForm_overlayAllOnCurrentGraph ) );
                    menu.Items.Add( "Stack All on Current Graph", null, new EventHandler( graphListForm_showAllAsStackOnCurrentGraph ) );
                }
            }

            if( selectedRows.Count == 1 )
            {
                menu.Items.Add( "Show as New Graph", null, new EventHandler( graphListForm_showAsNewGraph ) );
                menu.Items.Add( "Show Table of Data Points", null, new EventHandler( graphListForm_showTableOfDataPoints ) );
                menu.Items[0].Font = new Font( menu.Items[0].Font, FontStyle.Bold );
                menu.Tag = e.Spectrum;
            } else
            {
                menu.Items.Add( "Show All as New Graphs", null, new EventHandler( graphListForm_showAllAsNewGraph ) );
                menu.Items.Add( "Overlay All on New Graph", null, new EventHandler( graphListForm_overlayAllOnNewGraph ) );
                menu.Items.Add( "Stack All on New Graph", null, new EventHandler( graphListForm_showAllAsStackOnNewGraph ) );
                menu.Tag = selectedGraphItems;
            }

            menu.Show( Form.MousePosition );
        }

        #region various methods to create graph forms
        private void showData( GraphForm hostGraph, GraphItem item )
        {
            Pane pane;
            if( hostGraph.PaneList.Count == 0 || hostGraph.PaneList.Count > 1 )
            {
                hostGraph.PaneList.Clear();
                pane = new Pane();
                hostGraph.PaneList.Add( pane );
            } else
            {
                pane = hostGraph.PaneList[0];
                pane.Clear();
            }
            pane.Add( item );
            hostGraph.Refresh();
        }

        private void showDataOverlay(GraphForm hostGraph, GraphItem item )
        {
            hostGraph.PaneList[0].Add( item );
            hostGraph.Refresh();
        }

        private void showDataStacked(GraphForm hostGraph, GraphItem item )
        {
            Pane pane = new Pane();
            pane.Add( item );
            hostGraph.PaneList.Add( pane );
            hostGraph.Refresh();
        }

        void graphListForm_showAsCurrentGraph( object sender, EventArgs e )
        {
            GraphForm currentGraphForm = mainForm.CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );
            GraphItem g = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as GraphItem;

            currentGraphForm.PaneList.Clear();
            Pane pane = new Pane();
            pane.Add( g );
            currentGraphForm.PaneList.Add( pane );
            currentGraphForm.Refresh();
        }

        void graphListForm_overlayOnCurrentGraph( object sender, EventArgs e )
        {
            GraphForm currentGraphForm = mainForm.CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );
            GraphItem g = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as GraphItem;

            currentGraphForm.PaneList[0].Add( g );
            currentGraphForm.Refresh();
        }

        void graphListForm_overlayAllOnCurrentGraph( object sender, EventArgs e )
        {
            GraphForm currentGraphForm = mainForm.CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );
            List<GraphItem> gList = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as List<GraphItem>;

            currentGraphForm.PaneList.Clear();
            Pane pane = new Pane();
            foreach( GraphItem g in gList )
            {
                pane.Add( g );
            }
            currentGraphForm.PaneList.Add( pane );
            currentGraphForm.Refresh();
        }

        void graphListForm_showAsNewGraph( object sender, EventArgs e )
        {
            GraphForm newGraph = OpenGraph( true );
            GraphItem g = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as GraphItem;

            Pane pane = new Pane();
            pane.Add( g );
            newGraph.PaneList.Add( pane );
            newGraph.Refresh();
        }

        void graphListForm_showAllAsNewGraph( object sender, EventArgs e )
        {
            List<GraphItem> gList = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as List<GraphItem>;
            foreach( GraphItem g in gList )
            {
                GraphForm newGraph = OpenGraph( true );

                Pane pane = new Pane();
                pane.Add( g );
                newGraph.PaneList.Add( pane );
                newGraph.Refresh();
            }
        }

        void graphListForm_stackOnCurrentGraph( object sender, EventArgs e )
        {
            GraphForm currentGraphForm = mainForm.CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );
            GraphItem g = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as GraphItem;

            Pane pane = new Pane();
            pane.Add( g );
            currentGraphForm.PaneList.Add( pane );
            currentGraphForm.Refresh();
        }

        void graphListForm_showAllAsStackOnCurrentGraph( object sender, EventArgs e )
        {
            GraphForm currentGraphForm = mainForm.CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );
            List<GraphItem> gList = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as List<GraphItem>;

            currentGraphForm.PaneList.Clear();
            foreach( GraphItem g in gList )
            {
                Pane pane = new Pane();
                pane.Add( g );
                currentGraphForm.PaneList.Add( pane );
            }
            currentGraphForm.Refresh();
        }

        void graphListForm_overlayAllOnNewGraph( object sender, EventArgs e )
        {
            List<GraphItem> gList = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as List<GraphItem>;

            GraphForm newGraph = OpenGraph( true );
            Pane pane = new Pane();
            foreach( GraphItem g in gList )
            {
                pane.Add( g );
            }
            newGraph.PaneList.Add( pane );
            newGraph.Refresh();
        }

        void graphListForm_showAllAsStackOnNewGraph( object sender, EventArgs e )
        {
            List<GraphItem> gList = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as List<GraphItem>;

            GraphForm newGraph = OpenGraph( true );
            foreach( GraphItem g in gList )
            {
                Pane pane = new Pane();
                pane.Add( g );
                newGraph.PaneList.Add( pane );
            }
            newGraph.Refresh();
        }
        #endregion

        void graphListForm_showTableOfDataPoints( object sender, EventArgs e )
        {
            GraphItem g = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as GraphItem;

            DockableForm form = new DockableForm();
            form.Text = g.Id + " Data";
            DataGridView table = new DataGridView();
            table.Tag = g;
            table.Dock = DockStyle.Fill;
            table.VirtualMode = true;
            table.CellValueNeeded += new DataGridViewCellValueEventHandler( table_CellValueNeeded );
            table.EditMode = DataGridViewEditMode.EditProgrammatically;
            table.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            table.AllowUserToAddRows = false;
            table.AllowUserToDeleteRows = false;
            table.RowHeadersVisible = false;
            table.Columns.Add( "mz", "m/z" );
            table.Columns.Add( "intensity", "Intensity" );
            table.Rows.Insert(0, g.Points.Count);

            form.Controls.Add( table );
            form.Show( mainForm.DockPanel, DockState.Floating );
        }

        void table_CellValueNeeded( object sender, DataGridViewCellValueEventArgs e )
        {
            GraphItem gWithData = (sender as DataGridView).Tag as GraphItem;
            if( e.ColumnIndex == 0 )
                e.Value = gWithData.Points[e.RowIndex].X;
            else
                e.Value = gWithData.Points[e.RowIndex].Y;
        }

        public void ShowDataProcessing()
        {
            GraphForm currentGraphForm = mainForm.CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );

            if( currentGraphForm.FocusedPane.CurrentItemType == MSGraphItemType.Spectrum )
            {
                spectrumProcessingForm.UpdateProcessing( currentGraphForm.FocusedItem.Tag as MassSpectrum );
                mainForm.DockPanel.DefaultFloatingWindowSize = spectrumProcessingForm.Size;
                spectrumProcessingForm.Show( mainForm.DockPanel, DockState.Floating );
            }
        }

        private AnnotationSettings defaultScanAnnotationSettings;
        private AnnotationSettings defaultChromatogramAnnotationSettings;
        public void LoadDefaultAnnotationSettings()
        {
            defaultScanAnnotationSettings = new AnnotationSettings();
            defaultScanAnnotationSettings.ShowXValues = Properties.Settings.Default.ShowScanMzLabels;
            defaultScanAnnotationSettings.ShowYValues = Properties.Settings.Default.ShowScanIntensityLabels;
            defaultScanAnnotationSettings.ShowMatchedAnnotations = Properties.Settings.Default.ShowScanMatchedAnnotations;
            defaultScanAnnotationSettings.ShowUnmatchedAnnotations = Properties.Settings.Default.ShowScanUnmatchedAnnotations;
            defaultScanAnnotationSettings.MatchTolerance = Properties.Settings.Default.MzMatchTolerance;
            defaultScanAnnotationSettings.MatchToleranceOverride = Properties.Settings.Default.ScanMatchToleranceOverride;
            defaultScanAnnotationSettings.MatchToleranceUnit = (MatchToleranceUnits) Properties.Settings.Default.MzMatchToleranceUnit;

            // ms-product-label -> (label alias, known color)
            defaultScanAnnotationSettings.LabelToAliasAndColorMap["y"] = new Pair<string, Color>( "y", Color.Blue );
            defaultScanAnnotationSettings.LabelToAliasAndColorMap["b"] = new Pair<string, Color>( "b", Color.Red );
            defaultScanAnnotationSettings.LabelToAliasAndColorMap["y-NH3"] = new Pair<string, Color>( "y^", Color.Green );
            defaultScanAnnotationSettings.LabelToAliasAndColorMap["y-H2O"] = new Pair<string, Color>( "y*", Color.Cyan );
            defaultScanAnnotationSettings.LabelToAliasAndColorMap["b-NH3"] = new Pair<string, Color>( "b^", Color.Orange );
            defaultScanAnnotationSettings.LabelToAliasAndColorMap["b-H2O"] = new Pair<string, Color>( "b*", Color.Violet );

            defaultChromatogramAnnotationSettings = new AnnotationSettings();
            defaultChromatogramAnnotationSettings.ShowXValues = Properties.Settings.Default.ShowChromatogramTimeLabels;
            defaultChromatogramAnnotationSettings.ShowYValues = Properties.Settings.Default.ShowChromatogramIntensityLabels;
            defaultChromatogramAnnotationSettings.ShowMatchedAnnotations = Properties.Settings.Default.ShowChromatogramMatchedAnnotations;
            defaultChromatogramAnnotationSettings.ShowUnmatchedAnnotations = Properties.Settings.Default.ShowChromatogramUnmatchedAnnotations;
            defaultChromatogramAnnotationSettings.MatchTolerance = Properties.Settings.Default.TimeMatchTolerance;
            defaultChromatogramAnnotationSettings.MatchToleranceOverride = Properties.Settings.Default.ChromatogramMatchToleranceOverride;
            defaultChromatogramAnnotationSettings.MatchToleranceUnit = (MatchToleranceUnits) Properties.Settings.Default.TimeMatchToleranceUnit;
        }

        public void ShowAnnotationForm()
        {
            GraphForm currentGraphForm = mainForm.CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );

            if( currentGraphForm.FocusedPane.CurrentItemType == MSGraphItemType.Spectrum )
            {
                spectrumAnnotationForm.UpdateAnnotations( currentGraphForm.FocusedItem.Tag as MassSpectrum );
                mainForm.DockPanel.DefaultFloatingWindowSize = spectrumAnnotationForm.Size;
                spectrumAnnotationForm.Show( mainForm.DockPanel, DockState.Floating );
            }
        }

        void spectrumAnnotationForm_AnnotationChanged( object sender, EventArgs e )
        {
            if( sender is SpectrumAnnotationForm )
            {
                foreach( GraphForm form in CurrentGraphFormList )
                {
                    bool refresh = false;
                    foreach( Pane pane in form.PaneList )
                        for( int i = 0; i < pane.Count && !refresh; ++i )
                        {
                            if( pane[i].IsMassSpectrum &&
                                pane[i].Id == spectrumAnnotationForm.CurrentSpectrum.Id )
                            {
                                refresh = true;
                                break;
                            }
                        }
                    if( refresh )
                        form.Refresh();
                }
            }
        }

        private void processingListView_Changed( object sender, EventArgs e )
        {
            if( sender is SpectrumProcessingForm )
            {
                SpectrumProcessingForm spf = sender as SpectrumProcessingForm;

                foreach( GraphForm form in CurrentGraphFormList )
                {
                    foreach( Pane pane in form.PaneList )
                        for( int i = 0; i < pane.Count; ++i )
                        {
                            if( pane[i].IsMassSpectrum &&
                                pane[i].Id == spf.CurrentSpectrum.Id )
                            {
                                ( pane[i] as MassSpectrum ).SpectrumList = spectrumProcessingForm.ProcessingListView.ProcessingWrapper( pane[i].Source.Source.MSDataFile.run.spectrumList );
                                pane[i].Source.SpectrumListForm.UpdateRow(
                                    pane[i].Source.SpectrumListForm.IndexOf( spf.CurrentSpectrum ),
                                    (pane[i] as MassSpectrum).SpectrumList );
                            }
                        }
                    form.Refresh();
                }

                getMetaSpectrum( spf.CurrentSpectrum ).DataProcessing = spf.ProcessingListView.DataProcessing;
            }
        }

        private void processingOverrideButton_Click( object sender, EventArgs e )
        {
            bool global = sender == spectrumProcessingForm.GlobalProcessingOverrideButton;
            bool spectrum = sender == spectrumProcessingForm.GlobalProcessingOverrideButton || sender == spectrumProcessingForm.RunProcessingOverrideButton;

            if( spectrum )
            {
                IList<ManagedDataSource> sources;
                if( !global )
                {
                    sources = new List<ManagedDataSource>();
                    sources.Add( spectrumProcessingForm.CurrentSpectrum.Source );
                } else
                    sources = dataSourceMap.Values;

                foreach( ManagedDataSource source in sources )
                {
                    foreach( DataGridViewRow row in source.SpectrumListForm.GridView.Rows )
                    {
                        if( !row.Displayed )
                            continue;

                        source.SpectrumListForm.GetSpectrum(row.Index).DataProcessing = spectrumProcessingForm.ProcessingListView.DataProcessing;
                        source.SpectrumListForm.UpdateRow( row.Index,
                            spectrumProcessingForm.ProcessingListView.ProcessingWrapper( source.Source.MSDataFile.run.spectrumList ) );
                        Application.DoEvents();
                    }
                }

                foreach( GraphForm form in CurrentGraphFormList )
                {
                    foreach( Pane pane in form.PaneList )
                        for( int i = 0; i < pane.Count; ++i )
                        {
                            if( pane[i].IsMassSpectrum && ( global ||
                                ( !global && spectrumProcessingForm.CurrentSpectrum.Source == pane[i].Source ) ) )
                            {
                                ( pane[i] as MassSpectrum ).SpectrumList = spectrumProcessingForm.ProcessingListView.ProcessingWrapper( pane[i].Source.Source.MSDataFile.run.spectrumList );
                            }
                        }
                    form.Refresh();
                }
            }
        }

        private void chromatogramListForm_CellDoubleClick( object sender, ChromatogramListCellDoubleClickEventArgs e )
        {
            if( e.Chromatogram == null || e.Button != MouseButtons.Left )
                return;

            GraphForm currentGraphForm = mainForm.CurrentGraphForm;
            if( currentGraphForm == null )
                currentGraphForm = OpenGraph( true );

            showData(currentGraphForm, e.Chromatogram );
            currentGraphForm.ZedGraphControl.Focus();
        }

        private void spectrumListForm_CellDoubleClick( object sender, SpectrumListCellDoubleClickEventArgs e )
        {
            if( e.Spectrum == null )
                return;

            GraphForm currentGraphForm = mainForm.CurrentGraphForm;
            if( currentGraphForm == null )
                currentGraphForm = OpenGraph( true );

            spectrumProcessingForm.UpdateProcessing( e.Spectrum );
            spectrumAnnotationForm.UpdateAnnotations( e.Spectrum );
            showData( currentGraphForm, e.Spectrum );
            currentGraphForm.ZedGraphControl.Focus();
        }

        private void spectrumListForm_FilterChanged( object sender, SpectrumListFilterChangedEventArgs e )
        {
            if( e.Matches == e.Total )
                mainForm.SetStatusLabel( "Filters reset to show all spectra." );
            else
                mainForm.SetStatusLabel( String.Format( "{0} of {1} spectra matched the filter settings.", e.Matches, e.Total ) );
        }

        private void graphForm_PreviewKeyDown( object sender, PreviewKeyDownEventArgs e )
        {
            if( !( sender is GraphForm ) && !( sender is MSGraph.MSGraphControl ) )
                throw new Exception( "Error processing keyboard input: unable to handle sender " + sender.ToString() );

            GraphForm graphForm;
            if( sender is GraphForm )
                graphForm = sender as GraphForm;
            else
                graphForm = ( sender as MSGraph.MSGraphControl ).Parent as GraphForm;

            GraphItem graphItem = graphForm.FocusedItem.Tag as GraphItem;
            SpectrumSource source = graphItem.Source.Source;
            if( source == null || graphItem == null )
                return;

            DataGridView gridView = graphItem.IsChromatogram ? ( graphItem as Chromatogram ).OwningListForm.GridView
                                                             : ( graphItem as MassSpectrum ).OwningListForm.GridView;
            int rowIndex = graphItem.IsChromatogram ? 0
                                                    : ( graphItem as MassSpectrum ).OwningListForm.IndexOf( graphItem as MassSpectrum );

            int key = (int) e.KeyCode;
            if( ( key == (int) Keys.Left || key == (int) Keys.Up ) && rowIndex > 0 )
                gridView.CurrentCell = gridView[gridView.CurrentCell.ColumnIndex, rowIndex - 1];
            else if( ( key == (int) Keys.Right || key == (int) Keys.Down ) && rowIndex < gridView.RowCount - 1 )
                gridView.CurrentCell = gridView[gridView.CurrentCell.ColumnIndex, rowIndex + 1];
            else
                return;

            if( graphItem.IsMassSpectrum ) // update spectrum processing form
            {
                MassSpectrum spectrum = ( graphItem as MassSpectrum ).OwningListForm.GetSpectrum( gridView.CurrentCellAddress.Y );
                spectrumProcessingForm.UpdateProcessing( spectrum );
                spectrumAnnotationForm.UpdateAnnotations( spectrum );
            }

            gridView.Parent.Refresh(); // update chromatogram/spectrum list
            graphForm.Pane.Refresh(); // update tab text
            showData( graphForm, ( graphItem as MassSpectrum ).OwningListForm.GetSpectrum( gridView.CurrentCellAddress.Y ) );
            Application.DoEvents();

            //CurrentGraphForm.ZedGraphControl.PreviewKeyDown -= new PreviewKeyDownEventHandler( GraphForm_PreviewKeyDown );
            //Application.DoEvents();
            //CurrentGraphForm.ZedGraphControl.PreviewKeyDown += new PreviewKeyDownEventHandler( GraphForm_PreviewKeyDown );
        }

        public void ExportIntegration()
        {
			/*SaveFileDialog exportDialog = new SaveFileDialog();
            string filepath = mainForm.CurrentGraphForm.Sources[0].Source.CurrentFilepath;
            exportDialog.InitialDirectory = Path.GetDirectoryName( filepath );
			exportDialog.OverwritePrompt = true;
			exportDialog.RestoreDirectory = true;
            exportDialog.FileName = Path.GetFileNameWithoutExtension( filepath ) + "-peaks.csv";
			if( exportDialog.ShowDialog() == DialogResult.OK )
			{
				StreamWriter writer = new StreamWriter( exportDialog.FileName );
				writer.WriteLine( "Id,Area" );
                foreach( DataGridViewRow row in dataSourceMap[filepath].ChromatogramListForm.GridView.Rows )
                {
                    GraphItem g = row.Tag as GraphItem;
                    writer.WriteLine( "{0},{1}", g.Id, g.TotalIntegratedArea );
                }
				writer.Close();
			}*/
		}

        public void ShowCurrentSourceAsMzML()
        {
            GraphForm currentGraphForm = mainForm.CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );

            Form previewForm = new Form();
            previewForm.StartPosition = FormStartPosition.CenterParent;
            previewForm.Text = "MzML preview of " + currentGraphForm.PaneList[0][0].Source.Source.CurrentFilepath;
            TextBox previewText = new TextBox();
            previewText.Multiline = true;
            previewText.ReadOnly = true;
            previewText.Dock = DockStyle.Fill;
            previewForm.Controls.Add( previewText );

            previewForm.Show( mainForm );
            Application.DoEvents();

            string tmp = Path.GetTempFileName();
            System.Threading.ParameterizedThreadStart threadStart = new System.Threading.ParameterizedThreadStart( startWritePreviewMzML );
            System.Threading.Thread writeThread = new System.Threading.Thread( threadStart );
            writeThread.Start( new KeyValuePair<string, MSDataFile>( tmp, currentGraphForm.PaneList[0][0].Source.Source.MSDataFile ));
            writeThread.Join( 1000 );
            while( writeThread.IsAlive )
            {
                FileStream tmpStream = File.Open( tmp, FileMode.Open, FileAccess.Read, FileShare.None );
                previewText.Text = new StreamReader( tmpStream ).ReadToEnd();
                writeThread.Join( 1000 );
            }
        }

        private void startWritePreviewMzML( object threadArg )
        {
            KeyValuePair<string, MSDataFile> sourcePair = (KeyValuePair<string, MSDataFile>) threadArg;
            sourcePair.Value.write( sourcePair.Key );
        }
    }
}
