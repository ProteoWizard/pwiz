/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Drawing;
using System.IO;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Results
{
    public class TransitionFullScanInfo
    {
        public string Name;
        public ChromSource Source;
        public int[][] ScanIds;
        public Color Color;
        public double PrecursorMz;
        public double ProductMz;
        public double? ExtractionWidth;
    }

    public interface IScanProvider : IDisposable
    {
        string DocFilePath { get; }
        MsDataFileUri DataFilePath { get; }
        ChromSource Source { get; }
        float[] Times { get; }
        TransitionFullScanInfo[] Transitions { get; }
        FullScan[] GetScans(int scanId);
        void Adopt(IScanProvider scanProvider);
    }

    public class ScanProvider : IScanProvider
    {
        private MsDataFileImpl _dataFile;

        public ScanProvider(string docFilePath, MsDataFileUri dataFilePath, ChromSource source, float[] times, TransitionFullScanInfo[] transitions)
        {
            DocFilePath = docFilePath;
            DataFilePath = dataFilePath;
            Source = source;
            Times = times;
            Transitions = transitions;
        }

        public void Adopt(IScanProvider other)
        {
            if (!Equals(DocFilePath, other.DocFilePath) || !Equals(DataFilePath, other.DataFilePath))
                return;
            var scanProvider = other as ScanProvider;
            if (scanProvider == null)
                return;
            _dataFile = scanProvider._dataFile;
            scanProvider._dataFile = null;
        }

        public string DocFilePath { get; private set; }
        public MsDataFileUri DataFilePath { get; private set; }
        public ChromSource Source { get; private set; }
        public float[] Times { get; private set; }
        public TransitionFullScanInfo[] Transitions { get; private set; }

        public FullScan[] GetScans(int scanId)
        {
            var fullScans = new FullScan[1];
            fullScans[0] = new FullScan { Spectrum = GetDataFile().GetSpectrum(scanId) };
            return fullScans;
        }

        private MsDataFileImpl GetDataFile()
        {
            if (_dataFile == null)
            {
                string dataFilePath = FindDataFilePath();
                if (dataFilePath == null)
                    throw new FileNotFoundException(string.Format(Resources.ScanProvider_GetScans_The_data_file__0__could_not_be_found__either_at_its_original_location_or_in_the_document_or_document_parent_folder_, DataFilePath));
                int sampleIndex = SampleHelp.GetPathSampleIndexPart(dataFilePath);
                if (sampleIndex == -1)
                    sampleIndex = 0;
                // Full-scan extraction always uses SIM as spectra
                _dataFile = new MsDataFileImpl(dataFilePath, sampleIndex, true);
            }
            return _dataFile;
        }

        public string FindDataFilePath()
        {
            var msDataFilePath = DataFilePath as MsDataFilePath;
            if (null == msDataFilePath)
            {
                return null;
            }
            string dataFilePath = msDataFilePath.FilePath;
            if (File.Exists(dataFilePath))
                return dataFilePath;
            string fileName = Path.GetFileName(dataFilePath) ?? string.Empty;
            string docDir = Path.GetDirectoryName(DocFilePath) ?? Directory.GetCurrentDirectory();
            dataFilePath = Path.Combine(docDir,  fileName);
            if (File.Exists(dataFilePath))
                return dataFilePath;
            string docParentDir = Path.GetDirectoryName(docDir) ?? Directory.GetCurrentDirectory();
            dataFilePath = Path.Combine(docParentDir, fileName);
            if (File.Exists(dataFilePath))
                return dataFilePath;
            return null;
        }

        public void Dispose()
        {
            if (_dataFile != null)
            {
                _dataFile.Dispose();
                _dataFile = null;
            }
        }
    }

    public class FullScan
    {
        public double? DriftTime { get; set; }
        public MsDataSpectrum Spectrum { get; set; }
    }
}
