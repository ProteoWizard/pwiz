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
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.ComponentModel;
using pwiz.CLI;
using pwiz.CLI.msdata;

using ChromatogramData = System.Collections.Generic.Map<double, double>;
using FragmentToChromatogramMap = System.Collections.Generic.Map<double, System.Collections.Generic.Map<double, double>>;
using ParentToFragmentMap = System.Collections.Generic.Map<double, System.Collections.Generic.Map<double, System.Collections.Generic.Map<double, double>>>;

namespace seems
{
	public class ProgressReportEventArgs : EventArgs
	{
		// Summary:
		//     Gets the percentage complete of a SpectrumSource operation.
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
		//     Gets a string describing the status of a SpectrumSource operation.
		//
		// Returns:
		//     A string representing the current status of SpectrumSource operation.
		public string Status { get { return status; } }
		private string status;

		internal StatusReportEventArgs( string status )
		{
			this.status = status;
		}
	}

	public class SetInputFileCompletedEventArgs : EventArgs
	{
		public SpectrumSource DataSource { get { return dataSource; } }
		private SpectrumSource dataSource;

		internal SetInputFileCompletedEventArgs( SpectrumSource dataSource )
		{
			this.dataSource = dataSource;
		}
	}

	public delegate void ProgressReportEventHandler( object sender, ProgressReportEventArgs e );
	public delegate void StatusReportEventHandler( object sender, StatusReportEventArgs e );
	public delegate void SetInputFileCompletedEventHandler( object sender, SetInputFileCompletedEventArgs e );

	public class SpectrumSource
	{
		private MSData msDataFile;
		private string sourceFilepath;
		private List<Chromatogram> chromatograms = new List<Chromatogram>();
		private List<MassSpectrum> spectra = new List<MassSpectrum>();

		public string Name { get { return Path.GetFileNameWithoutExtension( sourceFilepath ); } }
		public MSData MSDataFile { get { return msDataFile; } }
		public string CurrentFilepath { get { return sourceFilepath; } }

        public List<Chromatogram> Chromatograms { get { return chromatograms; } }
		public List<MassSpectrum> Spectra { get { return spectra; } }

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

		public SpectrumSource()
		{
			// create empty data source
		}

		public SpectrumSource( string filepath )
		{
            MSDataList msdList = new MSDataList();
            var readerConfig = new ReaderConfig
            {
                simAsSpectra = Program.SimAsSpectra,
                srmAsSpectra = Program.SrmAsSpectra
            };

            if (!File.Exists(filepath))
                throw new FileNotFoundException("Filepath not found: " + filepath, filepath);

            ReaderList.FullReaderList.read(filepath, msdList, readerConfig);
            msDataFile = msdList[0];
			//msDataFile = new MSDataFile(filepath);
			sourceFilepath = filepath;

            // create dummy spectrum/chromatogram list to simplify logic
            msDataFile.run.spectrumList = msDataFile.run.spectrumList ?? new SpectrumListSimple();
            msDataFile.run.chromatogramList = msDataFile.run.chromatogramList ?? new ChromatogramListSimple();

			setInputFileWaitHandle = new EventWaitHandle( false, EventResetMode.ManualReset );
			/*setInputFileDelegate = new ParameterizedThreadStart( startSetInputFile );
			Thread setInputFileThread = new Thread( setInputFileDelegate );

			setInputFileThread.Start( (object) filepath );*/
		}

		//private ParameterizedThreadStart setInputFileDelegate;
		private void startSetInputFile( object threadArg )
		{
			SetInputFile( (string) threadArg );
			setInputFileWaitHandle.Set();
			OnSetInputFileCompleted();
		}

		public bool SetInputFile( string filepath )
		{
            return true;
		}
	}
}
