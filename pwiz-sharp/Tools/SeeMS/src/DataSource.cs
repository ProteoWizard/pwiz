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

using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Mzml;

using ChromatogramData = System.Collections.Generic.Dictionary<double, double>;
using FragmentToChromatogramMap = System.Collections.Generic.Dictionary<double, System.Collections.Generic.Map<double, double>>;
using ParentToFragmentMap = System.Collections.Generic.Dictionary<double, System.Collections.Generic.Map<double, System.Collections.Generic.Map<double, double>>>;

namespace Pwiz.SeeMS
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
		private OpenDataSourceDialog.MSDataRunPath sourceFilepath;
		private List<Chromatogram> chromatograms = new List<Chromatogram>();
		private List<MassSpectrum> spectra = new List<MassSpectrum>();

		public string Name { get; set; }
		public MSData MSDataFile { get; private set; }
		public string CurrentFilepath { get { return sourceFilepath.Filepath; } }
		public OpenDataSourceDialog.MSDataRunPath CurrentMsDataRunPath { get { return sourceFilepath; } }

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

        // Full reader list: mzML + MGF (built-in) plus Thermo + Bruker + Waters + Agilent + Sciex.
        // Constructed once and reused; same shape MsConvert.Converter uses.
        private static readonly ReaderList s_fullReaderList = BuildFullReaderList();
        public static ReaderList FullReaderList => s_fullReaderList;

        private static ReaderList BuildFullReaderList()
        {
            var list = Pwiz.Vendor.Thermo.ThermoReaderRegistration.CreateDefaultWithThermo();
            list.Add(new Pwiz.Vendor.Bruker.Reader_Bruker
            {
                CombineIonMobilitySpectra = Pwiz.SeeMS.Settings.Default.CombineIonMobilitySpectra,
            });
            list.Add(new Pwiz.Vendor.Waters.Reader_Waters());
            list.Add(new Pwiz.Vendor.Agilent.Reader_Agilent());
            list.Add(new Pwiz.Vendor.Sciex.Reader_Sciex());
            return list;
        }

        public static ReaderConfig GetReaderConfig()
        {
            return new ReaderConfig
            {
                SimAsSpectra = Pwiz.SeeMS.Settings.Default.SimAsSpectra,
                SrmAsSpectra = Pwiz.SeeMS.Settings.Default.SrmAsSpectra,
                CombineIonMobilitySpectra = Pwiz.SeeMS.Settings.Default.CombineIonMobilitySpectra,
                IgnoreZeroIntensityPoints = Pwiz.SeeMS.Settings.Default.IgnoreZeroIntensityPoints,
                AcceptZeroLengthSpectra = Pwiz.SeeMS.Settings.Default.AcceptZeroLengthSpectra,
                AllowMsMsWithoutPrecursor = false
            };
        }

		public SpectrumSource(OpenDataSourceDialog.MSDataRunPath filepath)
		{
            if (!File.Exists(filepath.Filepath) && !Directory.Exists(filepath.Filepath)) // Some mass spec "files" are really directory structures
                throw new FileNotFoundException("Filepath not found: " + filepath, filepath.Filepath);

		    MSDataFile = new MSData();
            // pwiz-sharp ReaderList.Read takes (path, msd, config); the cpp/CLI runIndex
            // round-trips through ReaderConfig.RunIndex. Use the full reader list (mzML +
            // MGF + Thermo + Bruker + Waters + Agilent + Sciex) so vendor files identify.
            var readerConfig = GetReaderConfig();
            readerConfig.RunIndex = filepath.RunIndex;
            FullReaderList.Read(filepath.Filepath, MSDataFile, readerConfig);
			sourceFilepath = filepath;

            // create dummy spectrum/chromatogram list to simplify logic
		    MSDataFile.Run.SpectrumList = MSDataFile.Run.SpectrumList ?? new SpectrumListSimple();
		    MSDataFile.Run.ChromatogramList = MSDataFile.Run.ChromatogramList ?? new ChromatogramListSimple();

			Name = MSDataFile.Run.Id;

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

namespace Pwiz.Data.MsData
{
    public static class SpectrumExtensions
    {

        public static double[] GetIonMobilityArray(this Spectrum s)
        {
            if (!s.Id.StartsWith("merged="))
                return null;
            // pwiz-sharp BinaryDataArray.Data is IList<double>; cpp returned a flat array.
            // Materialize to double[] so callers' indexing/loop-counting works unchanged.
            var d = s.GetArrayByCvid(Pwiz.Data.Common.Cv.CVID.MS_mean_ion_mobility_drift_time_array)?.Data ??
                    s.GetArrayByCvid(Pwiz.Data.Common.Cv.CVID.MS_mean_inverse_reduced_ion_mobility_array)?.Data ??
                    s.GetArrayByCvid(Pwiz.Data.Common.Cv.CVID.MS_raw_ion_mobility_array)?.Data ??
                    s.GetArrayByCvid(Pwiz.Data.Common.Cv.CVID.MS_raw_inverse_reduced_ion_mobility_array)?.Data;
            return d is null ? null : System.Linq.Enumerable.ToArray(d);
        }
    }
}
