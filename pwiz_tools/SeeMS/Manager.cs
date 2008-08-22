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

		public ManagedDataSource( DataSource source )
		{
			this.source = source;
			spectrumListForm = new SpectrumListForm();
            spectrumProcessingForm = new SpectrumProcessingForm();
			chromatogramListForm = new ChromatogramListForm();
			//graphInfoMap = new GraphInfoMap();
		}

		private DataSource source;
		public DataSource Source { get { return source; } }

		private SpectrumListForm spectrumListForm;
		public SpectrumListForm SpectrumListForm { get { return spectrumListForm; } }

        private SpectrumProcessingForm spectrumProcessingForm;
        public SpectrumProcessingForm SpectrumProcessingForm { get { return spectrumProcessingForm; } }

		private ChromatogramListForm chromatogramListForm;
		public ChromatogramListForm ChromatogramListForm { get { return chromatogramListForm; } }

        //private ChromatogramProcessingForm chromatogramProcessingForm;
        //public ChromatogramProcessingForm ChromatogramProcessingForm { get { return chromatogramProcessingForm; } }

		//private GraphInfoMap graphInfoMap;
		//public GraphInfoMap GraphInfoMap { get { return graphInfoMap; } }

        public Chromatogram GetChromatogram( int index )
        { return GetChromatogram( index, false ); }

        public Chromatogram GetChromatogram( int index, bool getBinaryData )
        { return new Chromatogram( this, source.MSDataFile.run.chromatogramList.chromatogram( index, getBinaryData ) ); }

        //public Chromatogram GetChromatogram( int index, bool getBinaryData, ChromatogramList chromatogramList );

        public Chromatogram GetChromatogram( Chromatogram metaChromatogram, ChromatogramList chromatogramList )
        {
            Chromatogram chromatogram = new Chromatogram( metaChromatogram, chromatogramList.chromatogram( metaChromatogram.Index, true ) );
            return chromatogram;
        }

        public MassSpectrum GetMassSpectrum( int index )
        { return GetMassSpectrum( index, false ); }

        public MassSpectrum GetMassSpectrum( int index, bool getBinaryData )
        { return GetMassSpectrum( index, getBinaryData, source.MSDataFile.run.spectrumList ); }

        public MassSpectrum GetMassSpectrum( int index, bool getBinaryData, SpectrumList spectrumList )
        { return new MassSpectrum( this, spectrumList.spectrum( index, getBinaryData ) ); }

        public MassSpectrum GetMassSpectrum( MassSpectrum metaSpectrum, SpectrumList spectrumList )
        {
            MassSpectrum spectrum = new MassSpectrum( metaSpectrum, spectrumList.spectrum( metaSpectrum.Index, true ) );
            spectrum.Tag = metaSpectrum.Tag;
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
		private seems mainForm;
		private DataSourceMap dataSourceMap;

		public Manager(seems mainForm)
		{
			this.mainForm = mainForm;
			dataSourceMap = new DataSourceMap();

            LoadDefaultAnnotationSettings();
		}

		public void OpenFile(string filepath)
		{
			try
			{
				mainForm.SetProgressPercentage(0);
				mainForm.SetStatusLabel("Opening source file.");

				DataSourceMap.InsertResult insertResult = dataSourceMap.Insert( filepath, null );
				if( insertResult.WasInserted )
				{
					// file was not already open; create a new data source
					insertResult.Element.Value = new ManagedDataSource( new DataSource( filepath ) );
					initializeManagedDataSource( insertResult.Element.Value );
				} else
				{
					GraphForm newGraph = OpenGraph( true );
					DataSource source = insertResult.Element.Value.Source;
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
            return graphForm;
		}

        public GraphForm OpenGraph( bool giveFocus )
        {
            GraphForm graphForm = new GraphForm();
            graphForm.ZedGraphControl.PreviewKeyDown += new PreviewKeyDownEventHandler( graphForm_PreviewKeyDown );
            graphForm.Show( mainForm.DockPanel, DockState.Document );
            if( giveFocus )
                graphForm.Activate();

            return graphForm;
        }

        private GraphItem getGraphItemWithData( GraphItem item )
        {
            ManagedDataSource source = item.Source;
            if( item.IsChromatogram )
            {
                ChromatogramList cl = source.Source.MSDataFile.run.chromatogramList;
                return source.GetChromatogram( item as Chromatogram, cl );
            } else
            {
                SpectrumList sl = source.SpectrumProcessingForm.ProcessingListView.ProcessingWrapper( source.Source.MSDataFile.run.spectrumList );
                return source.GetMassSpectrum( item as MassSpectrum, sl );
            }
        }

		private void initializeManagedDataSource( ManagedDataSource managedDataSource )
		{
			try
			{
				DataSource source = managedDataSource.Source;
				MSDataFile msDataFile = source.MSDataFile;
				ChromatogramListForm chromatogramListForm = managedDataSource.ChromatogramListForm;
				SpectrumListForm spectrumListForm = managedDataSource.SpectrumListForm;

				chromatogramListForm.Text = source.Name + " chromatograms";
				chromatogramListForm.TabText = source.Name + " chromatograms";
				chromatogramListForm.ShowIcon = false;
                chromatogramListForm.CellDoubleClick += new ChromatogramListCellDoubleClickHandler( chromatogramListForm_CellDoubleClick );
                chromatogramListForm.CellClick += new ChromatogramListCellClickHandler( chromatogramListForm_CellClick );

				spectrumListForm.Text = source.Name + " spectra";
				spectrumListForm.TabText = source.Name + " spectra";
				spectrumListForm.ShowIcon = false;
                spectrumListForm.CellDoubleClick += new SpectrumListCellDoubleClickHandler( spectrumListForm_CellDoubleClick );
                spectrumListForm.CellClick += new SpectrumListCellClickHandler(spectrumListForm_CellClick);

				bool firstChromatogramLoaded = false;
				bool firstSpectrumLoaded = false;
				GraphForm firstGraph = null;

				ChromatogramList cl = msDataFile.run.chromatogramList;
				SpectrumList sl = msDataFile.run.spectrumList;
                //sl = new SpectrumList_Filter( sl, new SpectrumList_FilterAcceptSpectrum( acceptSpectrum ) );

				if( sl == null )
					throw new Exception( "Error loading metadata: no spectrum list" );
				
				int ticIndex = 0;
				if( cl != null )
				{
					ticIndex = cl.findNative( "TIC" );
					if( ticIndex < cl.size() )
					{
						pwiz.CLI.msdata.Chromatogram tic = cl.chromatogram( ticIndex );
                        Chromatogram ticChromatogram = new Chromatogram( managedDataSource, tic );
                        ticChromatogram.AnnotationSettings = defaultChromatogramAnnotationSettings;
						chromatogramListForm.Add( ticChromatogram );
						source.Chromatograms.Add( ticChromatogram );
						firstGraph = OpenGraph( true );
						showData(firstGraph, ticChromatogram );
						firstChromatogramLoaded = true;
						chromatogramListForm.Show( mainForm.DockPanel, DockState.DockBottom );
						Application.DoEvents();
					}
				}

				CVParam spectrumType = msDataFile.fileDescription.fileContent.cvParamChild( CVID.MS_spectrum_type );
				if( spectrumType.cvid == CVID.CVID_Unknown && !sl.empty() )
					spectrumType = sl.spectrum( 0 ).cvParamChild( CVID.MS_spectrum_type );

				if( spectrumType.cvid == CVID.MS_SRM_spectrum )
				{
					if( cl != null && cl.empty() )
						throw new Exception( "Error loading metadata: SRM file contains no chromatograms" );

				} else //if( spectrumType.cvid == CVID.MS_MS1_spectrum ||
					   //    spectrumType.cvid == CVID.MS_MSn_spectrum )
				{
					if( sl.empty() )
						throw new Exception( "Error loading metadata: MSn file contains no spectra" );
				}// else
				//	throw new Exception( "Error loading metadata: unable to open files with spectrum type \"" + spectrumType.name + "\"" );

				if( cl != null )
				{
					// load the rest of the chromatograms
					for( int i = 0; i < cl.size(); ++i )
					{
						if( i == ticIndex )
							continue;
						pwiz.CLI.msdata.Chromatogram c = cl.chromatogram( i );

						mainForm.SetStatusLabel( String.Format( "Loading chromatograms from source file ({0} of {1})...",
										( i + 1 ), cl.size() ) );
						mainForm.SetProgressPercentage( ( i + 1 ) * 100 / cl.size() );

                        if( mainForm.IsDisposed )
                            return;

                        Chromatogram chromatogram = new Chromatogram( managedDataSource, c );
                        chromatogram.AnnotationSettings = defaultChromatogramAnnotationSettings;
						chromatogramListForm.Add( chromatogram );
						source.Chromatograms.Add( chromatogram );
						if( !firstChromatogramLoaded )
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
					pwiz.CLI.msdata.Spectrum s = sl.spectrum( i );

					if( ( ( i + 1 ) % 100 ) == 0 || ( i + 1 ) == sl.size() )
					{
						mainForm.SetStatusLabel( String.Format( "Loading spectra from source file ({0} of {1})...",
										( i + 1 ), sl.size() ) );
						mainForm.SetProgressPercentage( ( i + 1 ) * 100 / sl.size() );
					}

                    if( mainForm.IsDisposed )
                        return;

                    MassSpectrum spectrum = new MassSpectrum( managedDataSource, s );
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
                    selectedGraphItems.Add( cell.OwningRow.Tag as GraphItem );
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

        private void showData(GraphForm hostGraph, GraphItem item )
        {
            GraphItem gWithData = getGraphItemWithData( item );
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
            pane.Add( gWithData );
            hostGraph.Refresh();
        }

        private void showDataOverlay(GraphForm hostGraph, GraphItem item )
        {
            GraphItem gWithData = getGraphItemWithData( item );
            hostGraph.PaneList[0].Add( gWithData );
            hostGraph.Refresh();
        }

        private void showDataStacked(GraphForm hostGraph, GraphItem item )
        {
            GraphItem gWithData = getGraphItemWithData( item );
            Pane pane = new Pane();
            pane.Add( gWithData );
            hostGraph.PaneList.Add( pane );
            hostGraph.Refresh();
        }

        void graphListForm_showAsCurrentGraph( object sender, EventArgs e )
        {
            GraphForm currentGraphForm = mainForm.CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );
            GraphItem g = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as GraphItem;

            GraphItem gWithData = getGraphItemWithData( g );
            currentGraphForm.PaneList.Clear();
            Pane pane = new Pane();
            pane.Add( gWithData );
            currentGraphForm.PaneList.Add( pane );
            currentGraphForm.Refresh();
        }

        void graphListForm_overlayOnCurrentGraph( object sender, EventArgs e )
        {
            GraphForm currentGraphForm = mainForm.CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );
            GraphItem g = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as GraphItem;

            GraphItem gWithData = getGraphItemWithData( g );
            currentGraphForm.PaneList[0].Add( gWithData );
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
                GraphItem gWithData = getGraphItemWithData( g );
                pane.Add( gWithData );
            }
            currentGraphForm.PaneList.Add( pane );
            currentGraphForm.Refresh();
        }

        void graphListForm_showAsNewGraph( object sender, EventArgs e )
        {
            GraphForm newGraph = OpenGraph( true );
            GraphItem g = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as GraphItem;

            GraphItem gWithData = getGraphItemWithData( g );
            Pane pane = new Pane();
            pane.Add( gWithData );
            newGraph.PaneList.Add( pane );
            newGraph.Refresh();
        }

        void graphListForm_showAllAsNewGraph( object sender, EventArgs e )
        {
            List<GraphItem> gList = ( ( sender as ToolStripMenuItem ).Owner as ContextMenuStrip ).Tag as List<GraphItem>;
            foreach( GraphItem g in gList )
            {
                GraphForm newGraph = OpenGraph( true );

                GraphItem gWithData = getGraphItemWithData( g );
                Pane pane = new Pane();
                pane.Add( gWithData );
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

            GraphItem gWithData = getGraphItemWithData( g );
            Pane pane = new Pane();
            pane.Add( gWithData );
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
                GraphItem gWithData = getGraphItemWithData( g );
                Pane pane = new Pane();
                pane.Add( gWithData );
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
                GraphItem gWithData = getGraphItemWithData( g );
                pane.Add( gWithData );
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
                GraphItem gWithData = getGraphItemWithData( g );
                Pane pane = new Pane();
                pane.Add( gWithData );
                newGraph.PaneList.Add( pane );
            }
            newGraph.Refresh();
        }

        public void ShowDataProcessing()
        {
            GraphForm currentGraphForm = mainForm.CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );

            if( currentGraphForm.PaneList[0][0].IsMassSpectrum )
            {
                SpectrumProcessingForm dataProcessingForm = mainForm.CurrentGraphForm.CurrentGraphItem.Source.SpectrumProcessingForm;
                dataProcessingForm.ProcessingListView.ItemsChanged += new EventHandler(processingListView_Changed);
                dataProcessingForm.Show( mainForm.DockPanel, DockState.Floating );
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

        public void ShowAnnotationSettings()
        {
            GraphForm currentGraphForm = mainForm.CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );

            AnnotationSettings settings;
            if( currentGraphForm.PaneList[0][0].IsMassSpectrum )
            {
                settings = defaultScanAnnotationSettings;
                ScanAnnotationSettingsForm annotationSettingsForm = new ScanAnnotationSettingsForm( settings );
                annotationSettingsForm.ShowDialog( mainForm );
            } else
            {
                settings = defaultChromatogramAnnotationSettings;
                ChromatogramAnnotationSettingsForm annotationSettingsForm = new ChromatogramAnnotationSettingsForm( settings );
                annotationSettingsForm.ShowDialog( mainForm );
            }
        }

        public void ShowAnnotationManualEditForm()
        {
            GraphForm currentGraphForm = mainForm.CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );

            if( currentGraphForm.PaneList[0][0].IsMassSpectrum )
            {
                AnnotationEditForm annotationEditForm = new AnnotationEditForm( defaultScanAnnotationSettings );
                annotationEditForm.ShowDialog( mainForm );
            } else
            {
                AnnotationEditForm annotationEditForm = new AnnotationEditForm( defaultChromatogramAnnotationSettings );
                annotationEditForm.ShowDialog( mainForm );
            }
        }

        private void processingListView_Changed( object sender, EventArgs e )
        {
            GraphForm currentGraphForm = mainForm.CurrentGraphForm;
            if( currentGraphForm == null )
                throw new Exception( "current graph should not be null" );

            if( currentGraphForm.PaneList[0][0].IsMassSpectrum )
            {
                mainForm.CurrentGraphForm.Refresh();
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

            showData(currentGraphForm, e.Spectrum );
            currentGraphForm.ZedGraphControl.Focus();
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

            GraphItem graphItem = graphForm.PaneList[0][0];
            DataSource source = graphItem.Source.Source;
            if( source == null || graphItem == null )
                return;

            if( !( graphItem.Tag is DataGridViewRow ) )
                throw new Exception( "Error processing keyboard input: unable to determine source row" );
            DataGridViewRow row = graphItem.Tag as DataGridViewRow;
            DataGridView gridView = row.DataGridView;

            int key = (int) e.KeyCode;
            if( key == (int) Keys.Left && row.Index > 0 )
                gridView.CurrentCell = gridView.Rows[row.Index - 1].Cells[gridView.CurrentCell.ColumnIndex];
            else if( key == (int) Keys.Right && row.Index < gridView.RowCount - 1 )
                gridView.CurrentCell = gridView.Rows[row.Index + 1].Cells[gridView.CurrentCell.ColumnIndex];
            else
                return;

            gridView.Parent.Refresh(); // update chromatogram/spectrum list
            graphForm.Pane.Refresh(); // update tab text
            showData(graphForm, gridView.CurrentRow.Tag as GraphItem );
            Application.DoEvents();

            //CurrentGraphForm.ZedGraphControl.PreviewKeyDown -= new PreviewKeyDownEventHandler( GraphForm_PreviewKeyDown );
            //Application.DoEvents();
            //CurrentGraphForm.ZedGraphControl.PreviewKeyDown += new PreviewKeyDownEventHandler( GraphForm_PreviewKeyDown );
        }

        public void ExportIntegration()
        {
			SaveFileDialog exportDialog = new SaveFileDialog();
			exportDialog.InitialDirectory = Path.GetDirectoryName( mainForm.CurrentGraphForm.CurrentSourceFilepath );
			exportDialog.OverwritePrompt = true;
			exportDialog.RestoreDirectory = true;
            exportDialog.FileName = Path.GetFileNameWithoutExtension( mainForm.CurrentGraphForm.CurrentSourceFilepath ) + "-peaks.csv";
			if( exportDialog.ShowDialog() == DialogResult.OK )
			{
				StreamWriter writer = new StreamWriter( exportDialog.FileName );
				writer.WriteLine( "Id,Area" );
                foreach( DataGridViewRow row in dataSourceMap[mainForm.CurrentGraphForm.CurrentSourceFilepath].ChromatogramListForm.GridView.Rows )
                {
                    GraphItem g = row.Tag as GraphItem;
                    writer.WriteLine( "{0},{1}", g.Id, g.TotalIntegratedArea );
                }
				writer.Close();
			}
		}
	}
}
