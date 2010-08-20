/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.IO;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    internal abstract class ChromDataProvider
    {
        private readonly int _startPercent;
        private readonly int _endPercent;
        private readonly IProgressMonitor _loader;

        protected ChromDataProvider(ProgressStatus status, int startPercent, int endPercent, IProgressMonitor loader)
        {
            Status = status;

            _startPercent = startPercent;
            _endPercent = endPercent;
            _loader = loader;
        }

        protected void SetPercentComplete(int percent)
        {
            if (_loader.IsCanceled)
            {
                _loader.UpdateProgress(Status = Status.Cancel());
                throw new LoadCanceledException(Status);
            }

            percent = Math.Min(_endPercent, (_endPercent - _startPercent) * percent / 100 + _startPercent);
            if (Status.IsPercentComplete(percent))
                return;

            _loader.UpdateProgress(Status = Status.ChangePercentComplete(percent));
        }

        public ProgressStatus Status { get; private set; }

        public abstract IEnumerable<KeyValuePair<ChromKey, int>> ChromIds { get; }

        public abstract void GetChromatogram(int id, out float[] times, out float[] intensities);

        public abstract bool IsProcessedScans { get; }
    }

    internal sealed class ChromatogramDataProvider : ChromDataProvider
    {
        private readonly IList<KeyValuePair<ChromKey, int>> _chromIds =
            new List<KeyValuePair<ChromKey, int>>();

        private readonly MsDataFileImpl _dataFile;

        /// <summary>
        /// The number of chromatograms read so far.
        /// </summary>
        private int _readChromatograms;

        /// <summary>
        /// Records the time at which chromatogram loading began to allow prediction
        /// of how long the file load will take.
        /// </summary>
        private DateTime _readStartTime;

        /// <summary>
        /// If the predicted time to load this file ever exceeds this threshold,
        /// a <see cref="LoadingTooSlowlyException"/> is thrown.
        /// </summary>
        private readonly double _readMaxMinutes;

        /// <summary>
        /// Possible work-around for a <see cref="LoadingTooSlowlyException"/>
        /// </summary>
        private readonly LoadingTooSlowlyException.Solution _slowLoadWorkAround;

        public ChromatogramDataProvider(MsDataFileImpl dataFile,
                                        bool throwIfSlow,
                                        ProgressStatus status,
                                        int startPercent,
                                        int endPercent,
                                        IProgressMonitor loader)
            : base(status, startPercent, endPercent, loader)
        {
            _dataFile = dataFile;

            if (throwIfSlow)
            {
                // Both WIFF files and Thermo Raw files have potential performance bugs
                // that have work-arounds.  WIFF files tend to take longer to load early
                // chromatograms, while Thermo files remain fairly consistent.
                if (_dataFile.IsABFile)
                {
                    _readMaxMinutes = 10;
                    _slowLoadWorkAround = LoadingTooSlowlyException.Solution.mzwiff_conversion;
                }
                else if (_dataFile.IsThermoFile)
                {
                    _readMaxMinutes = 5;
                    _slowLoadWorkAround = LoadingTooSlowlyException.Solution.local_file;
                }
            }

            int len = dataFile.ChromatogramCount;

            for (int i = 0; i < len; i++)
            {
                int index;
                string id = dataFile.GetChromatogramId(i, out index);

                if (!ChromKey.IsKeyId(id))
                    continue;

                var ki = new KeyValuePair<ChromKey, int>(ChromKey.FromId(id), index);
                _chromIds.Add(ki);
            }

            if (_chromIds.Count == 0)
                throw new NoSrmDataException();

            SetPercentComplete(50);
        }

        public override IEnumerable<KeyValuePair<ChromKey, int>> ChromIds
        {
            get { return _chromIds; }
        }

        public override void GetChromatogram(int id, out float[] times, out float[] intensities)
        {
            if (_readChromatograms == 0)
            {
                _readStartTime = DateTime.Now;
            }

            string chromId;
            _dataFile.GetChromatogram(id, out chromId, out times, out intensities);

            // Assume that each chromatogram will be read once, though this may
            // not always be completely true.
            _readChromatograms++;

            double predictedMinutes = ExpectedReadDurationMinutes;
            if (_readMaxMinutes > 0 && predictedMinutes > _readMaxMinutes)
            {
                throw new LoadingTooSlowlyException(_slowLoadWorkAround, Status, predictedMinutes, _readMaxMinutes);
            }

            if (_readChromatograms < _chromIds.Count)
                SetPercentComplete(50 + _readChromatograms * 50 / _chromIds.Count);
        }

        private double ExpectedReadDurationMinutes
        {
            get { return DateTime.Now.Subtract(_readStartTime).TotalMinutes * _chromIds.Count / _readChromatograms; }
        }

        public override bool IsProcessedScans
        {
            get { return false; }
        }
    }

    internal sealed class SpectraChromDataProvider : ChromDataProvider
    {
        private readonly List<KeyValuePair<ChromKey, ChromCollector>> _chromatograms =
            new List<KeyValuePair<ChromKey, ChromCollector>>();

        private readonly bool _isProcessedScans;

        public SpectraChromDataProvider(MsDataFileImpl dataFile,
                                        ProgressStatus status,
                                        int startPercent,
                                        int endPercent,
                                        IProgressMonitor loader)
            : base(status, startPercent, endPercent, loader)
        {
            // 10% done with this file
            SetPercentComplete(10);

            // First read all of the spectra, building chromatogram time, intensity lists
            var chromMap = new Dictionary<double, ChromDataCollector>();
            int lenSpectra = dataFile.SpectrumCount;
            int eighth = 0;
            for (int i = 0; i < lenSpectra; i++)
            {
                // Update progress indicator
                if (i * 8 / lenSpectra > eighth)
                {
                    eighth++;
                    SetPercentComplete((eighth + 1) * 10);
                }

                double? time, precursorMz;
                double[] mzArray, intensityArray;
                if (!dataFile.GetSrmSpectrum(i, out time, out precursorMz, out mzArray, out intensityArray))
                    continue;
                if (!time.HasValue)
                    throw new InvalidDataException(String.Format("Scan {0} found without scan time.", dataFile.GetSpectrumId(i)));
                if (!precursorMz.HasValue)
                    throw new InvalidDataException(String.Format("Scan {0} found without precursor m/z.", dataFile.GetSpectrumId(i)));

                ChromDataCollector collector;
                if (!chromMap.TryGetValue(precursorMz.Value, out collector))
                {
                    collector = new ChromDataCollector(precursorMz.Value);
                    chromMap.Add(precursorMz.Value, collector);
                }

                int ionCount = collector.ProductIntensityMap.Count;
                int ionScanCount = mzArray.Length;
                if (ionCount == 0)
                    ionCount = ionScanCount;

                int lenTimesCurrent = collector.TimeCount;
                for (int j = 0; j < ionScanCount; j++)
                {
                    double productMz = mzArray[j];
                    double intensity = intensityArray[j];

                    ChromCollector tis;
                    if (!collector.ProductIntensityMap.TryGetValue(productMz, out tis))
                    {
                        tis = new ChromCollector();
                        // If more than a single ion scan, add any zeros necessary
                        // to make this new chromatogram have an entry for each time.
                        if (ionScanCount > 1)
                        {
                            for (int k = 0; k < lenTimesCurrent; k++)
                                tis.Intensities.Add(0);
                        }
                        collector.ProductIntensityMap.Add(productMz, tis);
                    }
                    int lenTimes = tis.Times.Count;
                    if (lenTimes == 0 || time >= tis.Times[lenTimes - 1])
                    {
                        tis.Times.Add((float)time);
                        tis.Intensities.Add((float)intensity);
                    }
                    else
                    {
                        // Insert out of order time in the correct location
                        int iGreater = tis.Times.BinarySearch((float)time);
                        if (iGreater < 0)
                            iGreater = ~iGreater;
                        tis.Times.Insert(iGreater, (float)time);
                        tis.Intensities.Insert(iGreater, (float)intensity);
                    }
                }

                // If this was a multiple ion scan and not all ions had measurements,
                // make sure missing ions have zero intensities in the chromatogram.
                if (ionScanCount > 1 &&
                    (ionCount != ionScanCount || ionCount != collector.ProductIntensityMap.Count))
                {
                    // Times should have gotten one longer
                    lenTimesCurrent++;
                    foreach (var tis in collector.ProductIntensityMap.Values)
                    {
                        if (tis.Intensities.Count < lenTimesCurrent)
                        {
                            tis.Intensities.Add(0);
                            tis.Times.Add((float)time);
                        }
                    }
                }
            }

            if (chromMap.Count == 0)
                throw new NoSrmDataException();

            foreach (var collector in chromMap.Values)
            {
                foreach (var pair in collector.ProductIntensityMap)
                {
                    var key = new ChromKey(collector.PrecursorMz, pair.Key);
                    _chromatograms.Add(new KeyValuePair<ChromKey, ChromCollector>(key, pair.Value));
                }
            }

            // Only mzXML from mzWiff requires the introduction of zero values
            // during interpolation.
            _isProcessedScans = dataFile.IsMzWiffXml;
        }

        public override IEnumerable<KeyValuePair<ChromKey, int>> ChromIds
        {
            get
            {
                for (int i = 0; i < _chromatograms.Count; i++)
                    yield return new KeyValuePair<ChromKey, int>(_chromatograms[i].Key, i);
            }
        }

        public override void GetChromatogram(int id, out float[] times, out float[] intensities)
        {
            var tis = _chromatograms[id].Value;
            times = tis.Times.ToArray();
            intensities = tis.Intensities.ToArray();
        }

        public override bool IsProcessedScans
        {
            get { return _isProcessedScans; }
        }
    }

    internal sealed class ChromDataCollector
    {
        public ChromDataCollector(double precursorMz)
        {
            PrecursorMz = precursorMz;
            ProductIntensityMap = new Dictionary<double, ChromCollector>();
        }

        public double PrecursorMz { get; private set; }
        public Dictionary<double, ChromCollector> ProductIntensityMap { get; private set; }

        public int TimeCount
        {
            get
            {
                // Return the length of any existing time list
                foreach (var tis in ProductIntensityMap.Values)
                    return tis.Times.Count;
                return 0;
            }
        }
    }

    internal sealed class ChromCollector
    {
        public ChromCollector()
        {
            Times = new List<float>();
            Intensities = new List<float>();
        }

        public List<float> Times { get; private set; }
        public List<float> Intensities { get; private set; }
    }
}
