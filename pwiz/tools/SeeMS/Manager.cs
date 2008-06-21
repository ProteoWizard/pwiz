using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Extensions;
using DigitalRune.Windows.Docking;
using pwiz.CLI.msdata;

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
			graphInfoMap = new GraphInfoMap();
		}

		private DataSource source;
		public DataSource Source { get { return source; } }

		private SpectrumListForm spectrumListForm;
		public SpectrumListForm SpectrumListForm { get { return spectrumListForm; } }

        private SpectrumProcessingForm spectrumProcessingForm;
        public SpectrumProcessingForm SpectrumProcessingForm { get { return spectrumProcessingForm; } }

		private ChromatogramListForm chromatogramListForm;
		public ChromatogramListForm ChromatogramListForm { get { return chromatogramListForm; } }

        //private ChromatogramProcessingForm spectrumProcessingForm;
        //public ChromatogramProcessingForm SpectrumProcessingForm { get { return spectrumProcessingForm; } }

		private GraphInfoMap graphInfoMap;
		public GraphInfoMap GraphInfoMap { get { return graphInfoMap; } }
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
                        showData( newGraph, insertResult.Element.Value, source.Chromatograms[0] );
					else
                        showData( newGraph, insertResult.Element.Value, source.Spectra[0] );
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

        public void UpdateGraph( GraphForm graphForm )
        {
            List<GraphItem> graphItems = graphForm.GraphItems;
            if( graphItems.Count == 0 )
                return;
            showData( graphForm, dataSourceMap[graphItems[0].Source.CurrentFilepath], graphItems[0] );
            for( int i = 1; i < graphItems.Count; ++i )
                showDataOverlay( graphForm, dataSourceMap[graphItems[i].Source.CurrentFilepath], graphItems[i] );
        }

        private void showData( GraphForm hostGraph, ManagedDataSource managedDataSource, GraphItem item )
        {
            if( item.IsChromatogram )
            {
                ChromatogramList cl = managedDataSource.Source.MSDataFile.run.chromatogramList;
                hostGraph.ShowData( managedDataSource.Source, managedDataSource.Source.GetChromatogram( item as Chromatogram, cl ) );
            } else
            {
                SpectrumList sl = managedDataSource.SpectrumProcessingForm.ProcessingListView.ProcessingWrapper(managedDataSource.Source.MSDataFile.run.spectrumList);
                hostGraph.ShowData( managedDataSource.Source, managedDataSource.Source.GetMassSpectrum( item as MassSpectrum, sl ) );
            }
        }

        private void showData( GraphForm hostGraph, ManagedDataSource managedDataSource, DataGridViewRow row )
        {
            showData( hostGraph, managedDataSource, row.Tag as GraphItem );
        }

        private void showDataOverlay( GraphForm hostGraph, ManagedDataSource managedDataSource, GraphItem item )
        {
            if( item.IsChromatogram )
            {
                hostGraph.ShowDataOverlay( managedDataSource.Source, managedDataSource.Source.GetChromatogram( item.Index, true ) );
            } else
            {
                SpectrumList sl = managedDataSource.SpectrumProcessingForm.ProcessingListView.ProcessingWrapper( managedDataSource.Source.MSDataFile.run.spectrumList );
                hostGraph.ShowDataOverlay( managedDataSource.Source, managedDataSource.Source.GetMassSpectrum( item as MassSpectrum, sl ) );
            }
        }

        private void showDataOverlay( GraphForm hostGraph, ManagedDataSource managedDataSource, DataGridViewRow row )
        {
            showDataOverlay( hostGraph, managedDataSource, row.Tag as GraphItem );
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

				spectrumListForm.Text = source.Name + " spectra";
				spectrumListForm.TabText = source.Name + " spectra";
				spectrumListForm.ShowIcon = false;
                spectrumListForm.CellDoubleClick += new SpectrumListCellDoubleClickHandler( spectrumListForm_CellDoubleClick );

				bool firstChromatogramLoaded = false;
				bool firstSpectrumLoaded = false;
				GraphForm firstGraph = null;

				ChromatogramList cl = msDataFile.run.chromatogramList;
				SpectrumList sl = msDataFile.run.spectrumList;

				if( sl == null )
					throw new Exception( "Error loading metadata: no spectrum list" );
				
				int ticIndex = 0;
				if( cl != null )
				{
					ticIndex = cl.findNative( "TIC" );
					if( ticIndex < cl.size() )
					{
						pwiz.CLI.msdata.Chromatogram tic = cl.chromatogram( ticIndex );
						Chromatogram ticChromatogram = new Chromatogram( source, tic );
						chromatogramListForm.Add( ticChromatogram );
						source.Chromatograms.Add( ticChromatogram );
						firstGraph = OpenGraph( true );
						showData( firstGraph, managedDataSource, ticChromatogram );
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

						Chromatogram chromatogram = new Chromatogram( source, c );
						chromatogramListForm.Add( chromatogram );
						source.Chromatograms.Add( chromatogram );
						if( !firstChromatogramLoaded )
						{
							firstChromatogramLoaded = true;
							chromatogramListForm.Show( mainForm.DockPanel, DockState.DockBottom );
							firstGraph = OpenGraph( true );
                            showData( firstGraph, managedDataSource, chromatogram );
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

					MassSpectrum spectrum = new MassSpectrum( source, s );
					spectrumListForm.Add( spectrum );
					source.Spectra.Add( spectrum );
					if( !firstSpectrumLoaded )
					{
						firstSpectrumLoaded = true;
						spectrumListForm.Show( mainForm.DockPanel, DockState.DockBottom );
						if( firstChromatogramLoaded )
						{
							GraphForm spectrumGraph = CreateGraph();
							spectrumGraph.Show( firstGraph.Pane, DockPaneAlignment.Bottom, 0.5 );
                            showData( spectrumGraph, managedDataSource, spectrum );
						} else
						{
							firstGraph = OpenGraph( true );
                            showData( firstGraph, managedDataSource, spectrum );
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

        public void ShowDataProcessing()
        {
            if( mainForm.CurrentGraphForm != null &&
                mainForm.CurrentGraphForm.CurrentGraphItem != null &&
                mainForm.CurrentGraphForm.CurrentGraphItem.IsMassSpectrum )
            {
                SpectrumProcessingForm dataProcessingForm = dataSourceMap[mainForm.CurrentGraphForm.CurrentGraphItem.Source.CurrentFilepath].SpectrumProcessingForm;
                dataProcessingForm.ProcessingListView.ItemsChanged += new EventHandler(processingListView_Changed);
                dataProcessingForm.Show( mainForm.DockPanel, DockState.Floating );
            }
        }

        private void processingListView_Changed( object sender, EventArgs e )
        {
            if( mainForm.CurrentGraphForm != null &&
                mainForm.CurrentGraphForm.CurrentGraphItem != null &&
                mainForm.CurrentGraphForm.CurrentGraphItem.IsMassSpectrum )
            {
                UpdateGraph( mainForm.CurrentGraphForm );
            }
        }


        private void chromatogramListForm_CellDoubleClick( object sender, ChromatogramListCellDoubleClickEventArgs e )
        {
            if( e.Chromatogram == null )
                return;

            GraphForm currentGraphForm = mainForm.CurrentGraphForm;
            if( currentGraphForm == null )
                currentGraphForm = OpenGraph( true );

            showData( currentGraphForm, dataSourceMap[e.Chromatogram.Source.CurrentFilepath], e.Chromatogram );
            currentGraphForm.ZedGraphControl.Focus();
        }

        private void spectrumListForm_CellDoubleClick( object sender, SpectrumListCellDoubleClickEventArgs e )
        {
            if( e.Spectrum == null )
                return;

            GraphForm currentGraphForm = mainForm.CurrentGraphForm;
            if( currentGraphForm == null )
                currentGraphForm = OpenGraph( true );

            showData(currentGraphForm, dataSourceMap[e.Spectrum.Source.CurrentFilepath], e.Spectrum );
            currentGraphForm.ZedGraphControl.Focus();
        }

        private void graphForm_PreviewKeyDown( object sender, PreviewKeyDownEventArgs e )
        {
            if( !( sender is GraphForm ) && !( sender is ZedGraph.ZedGraphControl ) )
                throw new Exception( "Error processing keyboard input: unable to handle sender " + sender.ToString() );

            GraphForm graphForm;
            if( sender is GraphForm )
                graphForm = sender as GraphForm;
            else
                graphForm = ( sender as ZedGraph.ZedGraphControl ).Parent as GraphForm;

            DataSource source = graphForm.DataSource;
            GraphItem graphItem = graphForm.CurrentGraphItem;
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
            showData( graphForm, dataSourceMap[source.CurrentFilepath], gridView.CurrentRow );
            Application.DoEvents();

            //CurrentGraphForm.ZedGraphControl.PreviewKeyDown -= new PreviewKeyDownEventHandler( GraphForm_PreviewKeyDown );
            //Application.DoEvents();
            //CurrentGraphForm.ZedGraphControl.PreviewKeyDown += new PreviewKeyDownEventHandler( GraphForm_PreviewKeyDown );
        }
	}
}
