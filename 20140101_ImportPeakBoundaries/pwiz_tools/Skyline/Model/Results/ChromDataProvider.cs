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
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;

namespace pwiz.Skyline.Model.Results
{
    internal abstract class ChromDataProvider : IDisposable
    {
        private readonly int _startPercent;
        private readonly int _endPercent;
        protected readonly IProgressMonitor _loader;

        protected ChromDataProvider(ChromFileInfo fileInfo,
                                    ProgressStatus status,
                                    int startPercent,
                                    int endPercent,
                                    IProgressMonitor loader)
        {
            FileInfo = fileInfo;
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

        public ChromFileInfo FileInfo { get; private set; }

        public ProgressStatus Status { get; private set; }

        public ChromatogramLoadingStatus LoadingStatus { get { return (ChromatogramLoadingStatus) Status; } }

        public abstract IEnumerable<KeyValuePair<ChromKey, int>> ChromIds { get; }

        public abstract void GetChromatogram(int id, out ChromExtra extra,
            out float[] times, out float[] intensities, out float[] massErrors);

        public abstract double? MaxRetentionTime { get; }

        public abstract double? MaxIntensity { get; }

        public abstract bool IsProcessedScans { get; }

        public abstract bool IsSingleMzMatch { get; }

        public abstract void ReleaseMemory();

        public abstract void Dispose();
    }

    internal sealed class ChromatogramDataProvider : ChromDataProvider
    {
        private readonly IList<KeyValuePair<ChromKey, int>> _chromIds =
            new List<KeyValuePair<ChromKey, int>>();
        private readonly int[] _chromIndices;

        private MsDataFileImpl _dataFile;

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
                                        ChromFileInfo fileInfo,
                                        bool throwIfSlow,
                                        ProgressStatus status,
                                        int startPercent,
                                        int endPercent,
                                        IProgressMonitor loader)
            : base(fileInfo, status, startPercent, endPercent, loader)
        {
            _dataFile = dataFile;

            if (throwIfSlow)
            {
                // Both WIFF files and Thermo Raw files have potential performance bugs
                // that have work-arounds.  WIFF files tend to take longer to load early
                // chromatograms, while Thermo files remain fairly consistent.
                if (_dataFile.IsThermoFile)
                {
                    _readMaxMinutes = 4;
                    _slowLoadWorkAround = LoadingTooSlowlyException.Solution.local_file;
                }
                // WIFF file issues have been fixed with new WIFF reader library, and mzWiff.exe
                // has been removed from the installation.
//                else if (_dataFile.IsABFile)
//                {
//                    _readMaxMinutes = 4;
//                    _slowLoadWorkAround = LoadingTooSlowlyException.Solution.mzwiff_conversion;
//                }
            }

            int len = dataFile.ChromatogramCount;
            _chromIndices = new int[len];

            int indexPrecursor = -1;
            double lastPrecursor = 0;
            for (int i = 0; i < len; i++)
            {
                int index;
                string id = dataFile.GetChromatogramId(i, out index);

                if (!ChromKey.IsKeyId(id))
                    continue;

                var chromKey = ChromKey.FromId(id);
                if (chromKey.Precursor != lastPrecursor)
                {
                    lastPrecursor = chromKey.Precursor;
                    indexPrecursor++;
                }
                var ki = new KeyValuePair<ChromKey, int>(chromKey, index);
                _chromIndices[index] = indexPrecursor;
                _chromIds.Add(ki);
            }

            if (_chromIds.Count == 0)
                throw new NoSrmDataException(dataFile.FilePath);

            SetPercentComplete(50);
        }

        public override IEnumerable<KeyValuePair<ChromKey, int>> ChromIds
        {
            get { return _chromIds; }
        }

        public override void GetChromatogram(int id, out ChromExtra extra, out float[] times, out float[] intensities, out float[] massErrors)
        {
            // No mass errors in SRM
            massErrors = null;
            if (_readChromatograms == 0)
            {
                _readStartTime = DateTime.Now;
            }

            string chromId;
            _dataFile.GetChromatogram(id, out chromId, out times, out intensities);

            // Assume that each chromatogram will be read once, though this may
            // not always be completely true.
            _readChromatograms++;

            if (!System.Diagnostics.Debugger.IsAttached)
            {
                double predictedMinutes = ExpectedReadDurationMinutes;
                if (_readMaxMinutes > 0 && predictedMinutes > _readMaxMinutes)
                {
                    throw new LoadingTooSlowlyException(_slowLoadWorkAround, Status, predictedMinutes, _readMaxMinutes);
                }
            }

            if (_readChromatograms < _chromIds.Count)
                SetPercentComplete(50 + _readChromatograms * 50 / _chromIds.Count);

            int index = _chromIndices[id];
            extra = new ChromExtra(index, -1);  // TODO: is zero the right value?

            // Display in AllChromatogramsGraph
            LoadingStatus.Transitions.AddTransition(
                index, -1,
                times,
                intensities);
        }

        private double ExpectedReadDurationMinutes
        {
            get { return DateTime.Now.Subtract(_readStartTime).TotalMinutes * _chromIds.Count / _readChromatograms; }
        }

        public override double? MaxIntensity
        {
            get { return null; }
        }

        public override double? MaxRetentionTime
        {
            get { return null; }
        }

        public override bool IsProcessedScans
        {
            get { return false; }
        }

        public override bool IsSingleMzMatch
        {
            get { return false; }
        }

        public static bool HasChromatogramData(MsDataFileImpl dataFile)
        {
            int len = dataFile.ChromatogramCount;

            // Many files have just one TIC chromatogram
            if (len < 2)
                return false;

            for (int i = 0; i < len; i++)
            {
                int index;
                string id = dataFile.GetChromatogramId(i, out index);

                if (ChromKey.IsKeyId(id))
                    return true;
            }
            return false;
        }

        public override void ReleaseMemory()
        {
            Dispose();
        }

        public override void Dispose()
        {
            if (_dataFile != null)
                _dataFile.Dispose();
            _dataFile = null;
        }
    }
}
