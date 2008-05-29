using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.ComponentModel;
using pwiz.CLI.msdata;
using Extensions;

using ChromatogramData = Extensions.Map<double, double>;
using FragmentToChromatogramMap = Extensions.Map<double, Extensions.Map<double, double>>;
using ParentToFragmentMap = Extensions.Map<double, Extensions.Map<double, Extensions.Map<double, double>>>;

namespace seems
{
	public class ProgressReportEventArgs : EventArgs
	{
		// Summary:
		//     Gets the percentage complete of a DataSource operation.
		//
		// Returns:
		//     An integer representing the current percentage complete. Range: [0,100]
		public int Percentage { get { return percentage; } }
		private int percentage;

		internal ProgressReportEventArgs( int percentage )
		{
			this.percentage = percentage;
		}

		internal ProgressReportEventArgs( double percentage )
		{
			this.percentage = Convert.ToInt32( Math.Floor( percentage * 100 ) );
		}
	}

	public class StatusReportEventArgs : EventArgs
	{
		// Summary:
		//     Gets a string describing the status of a DataSource operation.
		//
		// Returns:
		//     A string representing the current status of DataSource operation.
		public string Status { get { return status; } }
		private string status;

		internal StatusReportEventArgs( string status )
		{
			this.status = status;
		}
	}

	public class SetInputFileCompletedEventArgs : EventArgs
	{
		public DataSource DataSource { get { return dataSource; } }
		private DataSource dataSource;

		internal SetInputFileCompletedEventArgs( DataSource dataSource )
		{
			this.dataSource = dataSource;
		}
	}

	public delegate void ProgressReportEventHandler( object sender, ProgressReportEventArgs e );
	public delegate void StatusReportEventHandler( object sender, StatusReportEventArgs e );
	public delegate void SetInputFileCompletedEventHandler( object sender, SetInputFileCompletedEventArgs e );

	public class DataSource
	{
		private MSDataFile msDataFile;
		private string sourceFilepath;
		private List<GraphItem> sourceGraphItems = new List<GraphItem>();
		private bool doCentroiding;

		public string Name { get { return Path.GetFileNameWithoutExtension( sourceFilepath ); } }
		public MSDataFile MSDataFile { get { return msDataFile; } }
		public string CurrentFilepath { get { return sourceFilepath; } }
		//public SeemsScan CurrentChromatogram { get { return sourceChromatogram; } }
		public List<GraphItem> CurrentGraphItems { get { return sourceGraphItems; } }
		public bool DoCentroiding { get { return doCentroiding; } set { doCentroiding = value; } }

		public event ProgressReportEventHandler ProgressReport;
		public event StatusReportEventHandler StatusReport;
		public event SetInputFileCompletedEventHandler SetInputFileCompleted;

		protected void OnProgressReport( int percentageComplete )
		{
			if( ProgressReport != null )
				ProgressReport( this, new ProgressReportEventArgs( percentageComplete ) );
		}

		protected void OnStatusReport( string status )
		{
			if( StatusReport != null )
				StatusReport( this, new StatusReportEventArgs( status ) );
		}

		protected void OnSetInputFileCompleted()
		{
			if( SetInputFileCompleted != null )
				SetInputFileCompleted( this, new SetInputFileCompletedEventArgs( this ) );
		}

		private EventWaitHandle setInputFileWaitHandle;
		public EventWaitHandle SetInputFileEventWaitHandle { get { return setInputFileWaitHandle; } }

		public DataSource()
		{
			// create empty data source
		}

		public DataSource( string filepath )
		{
			msDataFile = new MSDataFile(filepath);

			setInputFileWaitHandle = new EventWaitHandle( false, EventResetMode.ManualReset );
			setInputFileDelegate = new ParameterizedThreadStart( startSetInputFile );
			Thread setInputFileThread = new Thread( setInputFileDelegate );
			//setInputFileThread.IsBackground = true;
			sourceFilepath = filepath;

			setInputFileThread.Start( (object) filepath );
		}

		private ParameterizedThreadStart setInputFileDelegate;
		private void startSetInputFile( object threadArg )
		{
			SetInputFile( (string) threadArg );
			setInputFileWaitHandle.Set();
			OnSetInputFileCompleted();
		}

		public bool SetInputFile( string filepath )
		{
			MSDataFile tempInterface;

			try
			{
				OnStatusReport( "Loading metadata from source file..." );
				OnProgressReport( 0 );

				tempInterface = new MSDataFile(filepath);
				pwiz.CLI.msdata.ChromatogramList cl = tempInterface.run.chromatogramList;
				pwiz.CLI.msdata.SpectrumList sl = tempInterface.run.spectrumList;

				int ticIndex = cl.findNative("TIC");
				if( ticIndex < cl.size() )
				{
					pwiz.CLI.msdata.Chromatogram tic = cl.chromatogram(ticIndex, true);
					sourceGraphItems.Add( new Chromatogram( this, tic ) );
				}

				CVParam spectrumType = tempInterface.fileDescription.fileContent.cvParamChild( CVID.MS_spectrum_type );
				if( spectrumType.cvid == CVID.CVID_Unknown && !sl.empty() )
					spectrumType = sl.spectrum(0).cvParamChild( CVID.MS_spectrum_type );

				if( spectrumType.cvid == CVID.MS_SRM_spectrum )
				{
					if( cl.empty() )
						throw new Exception( "Error loading metadata: SRM file contains no chromatograms" );

					// load the rest of the chromatograms
					for( int i = 0; i < cl.size(); ++i )
					{
						if( i == ticIndex )
							continue;
						pwiz.CLI.msdata.Chromatogram c = cl.chromatogram( i );

						OnStatusReport( String.Format( "Loading chromatograms from source file ({0} of {1})...",
										( i + 1 ), cl.size() ) );
						OnProgressReport( ( i + 1 ) * 100 / cl.size() );

						sourceGraphItems.Add( new Chromatogram( this, c ) );
					}

				} else if( spectrumType.cvid == CVID.MS_MS1_spectrum ||
						   spectrumType.cvid == CVID.MS_MSn_spectrum )
				{
					// get all scans by sequential access
					for( int i = 0; i < sl.size(); ++i )
					{
						pwiz.CLI.msdata.Spectrum s = sl.spectrum( i );

						if( ( ( i + 1 ) % 100 ) == 0 || ( i + 1 ) == sl.size() )
						{
							OnStatusReport( String.Format( "Loading spectra from source file ({0} of {1})...",
											( i + 1 ), sl.size() ) );
							OnProgressReport( ( i + 1 ) * 100 / sl.size() );
						}

						sourceGraphItems.Add( new MassSpectrum( this, s ) );
					}
				} else
					throw new Exception( "Error loading metadata: unable to open files with spectrum type \"" + spectrumType.name + "\"" );
					
				OnStatusReport( "Finished loading source metadata." );
				OnProgressReport( 100 );

				/*if( transitionMap.Count > 0 )
				{
					OnStatusReport( "Generating chromatograms for SRM/MRM data..." );
					OnProgressReport( 0 );
					Map<double, RefPair<ZedGraph.PointPairList, Map<double, ZedGraph.PointPairList>>> transitionChromatograms = new Map<double, RefPair<ZedGraph.PointPairList, Map<double, ZedGraph.PointPairList>>>();
					foreach( ParentToFragmentMap.MapPair pfPair in transitionMap )
					{
						Map<double, List<double>> parentPeaks = new Map<double, List<double>>();
						foreach( FragmentToChromatogramMap.MapPair fcPair in pfPair.Value )
						{
							ZedGraph.PointPairList fragmentChromatogram = transitionChromatograms[pfPair.Key].second[fcPair.Key] = new ZedGraph.PointPairList();
							foreach( ChromatogramData.MapPair tiPair in fcPair.Value )
							{
								fragmentChromatogram.Add( tiPair.Key, tiPair.Value );
								parentPeaks[tiPair.Key].Add(tiPair.Value);
							}
						}

						ZedGraph.PointPairList parentChromatogram = transitionChromatograms[pfPair.Key].first = new ZedGraph.PointPairList();
						ZedGraph.PointPairList productChromatogram = transitionChromatograms[pfPair.Key].second[pfPair.Key] = new ZedGraph.PointPairList();
						foreach( Map<double, List<double>>.MapPair itr in parentPeaks )
						{
							double totalIntensity = 0, productIntensity = 1;
							foreach( double intensity in itr.Value )
							{
								totalIntensity += intensity;
								productIntensity *= intensity;
							}
							parentChromatogram.Add( itr.Key, totalIntensity );
							productChromatogram.Add( itr.Key, productIntensity );
						}

						OnProgressReport( transitionChromatograms.Count * 100 / transitionMap.Count );
					}

					foreach( Map<double, RefPair<ZedGraph.PointPairList, Map<double, ZedGraph.PointPairList>>>.MapPair kvp1 in transitionChromatograms )
					{
						sourceGraphItems.Add( new Chromatogram( kvp1.Value.first, String.Format( "{0} (Sum)", Math.Round( kvp1.Key, 2 ) ) ) );
						sourceGraphItems.Add( new Chromatogram( kvp1.Value.second[kvp1.Key], String.Format( "{0} (Product)", Math.Round( kvp1.Key, 2 ) ) ) );
						foreach( Map<double, ZedGraph.PointPairList>.MapPair kvp2 in kvp1.Value.second )
							if( kvp1.Key != kvp2.Key )
								sourceGraphItems.Add( new Chromatogram( kvp2.Value, String.Format( "{0} -> {1}", Math.Round( kvp1.Key, 2 ), Math.Round( kvp2.Key, 2 ) ) ) );
					}

					OnStatusReport( "Finished loading source metadata." );
					OnProgressReport( 100 );
				}*/
			} catch( Exception ex )
			{
				string message = "SeeMS encountered an error reading metadata from \"" + filepath + "\" (" + ex.Message + ")";
				if( ex.InnerException != null )
					message += "\n\nAdditional information: " + ex.InnerException.Message;
				MessageBox.Show( message,
								"Error reading source metadata",
								MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
								0, false );
				OnStatusReport( "Failed to read source metadata." );
				return false;
			}

			return true;
		}
	}
}
