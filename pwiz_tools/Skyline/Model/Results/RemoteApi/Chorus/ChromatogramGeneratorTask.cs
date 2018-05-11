/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using pwiz.Skyline.Model.Results.RemoteApi.GeneratedCode;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.RemoteApi.Chorus
{
    public class ChromatogramGeneratorTask
    {
        private bool _started;
        private bool _finished;
        private ChromatogramCache _chromatogramCache;
        private List<ChromKeyIndices> _chromKeyIndiceses;
        
        public ChromatogramGeneratorTask(ChromTaskList chromTaskList, ChorusAccount chorusAccount, ChorusUrl chorusUrl,
            ChromatogramRequestDocument chromatogramRequestDocument)
        {
            ChromTaskList = chromTaskList;
            ChorusAccount = chorusAccount;
            ChorusUrl = chorusUrl;
            ChromatogramRequestDocument = chromatogramRequestDocument;
        }

        public ChromTaskList ChromTaskList { get; private set; }
        public ChorusSession ChorusSession { get { return ChromTaskList.ChorusSession; } }
        public ChorusAccount ChorusAccount { get; private set; }
        public ChorusUrl ChorusUrl { get; private set; }
        public ChromatogramRequestDocument ChromatogramRequestDocument { get; private set; }

        public void Start()
        {
            lock (this)
            {
                if (_started)
                {
                    return;
                }
                _started = true;
            }
            Task.Factory.StartNew(() =>
            {
                try
                {
                    SendRequest();
                }
                finally
                {
                    lock (this)
                    {
                        _finished = true;
                        ChromTaskList.OnTaskCompleted(this);
                    }
                }
            });
        }

        public bool IsStarted()
        {
            return _started;
        }

        public bool IsFinished()
        {
            return _finished;
        }

        private void SendRequest()
        {
            while (true)
            {
                try
                {
                    var chromatogramCache = ChorusSession.GenerateChromatograms(ChorusAccount, ChorusUrl, ChromatogramRequestDocument);
                    if (null == chromatogramCache)
                    {
                        return;
                    }
                    var chromKeyIndiceses = chromatogramCache.GetChromKeys(ChorusUrl).ToList();
                    lock (this)
                    {
                        _chromatogramCache = chromatogramCache;
                        _chromKeyIndiceses = chromKeyIndiceses;
                    }
                    return;
                }
                catch (Exception exception)
                {
                    ChromTaskList.HandleException(exception);
                    return;
                }
            }
        }

        public bool GetChromatogram(ChromKey chromKey, out TimeIntensities timeIntensities)
        {
            int keyIndex = -1;
            if (_chromKeyIndiceses != null)
            {
                var tolerance = (float)ChromTaskList.SrmDocument.Settings.TransitionSettings.Instrument.MzMatchTolerance;
                keyIndex = _chromKeyIndiceses.IndexOf(entry => EqualsTolerant(chromKey, entry.Key, tolerance));
            }
            if (keyIndex == -1 || _chromKeyIndiceses == null)   // Keep ReSharper from complaining
            {
                timeIntensities = null;
                return false;
            }
            ChromKeyIndices chromKeyIndices = _chromKeyIndiceses[keyIndex];
            var chromGroupInfo = _chromatogramCache.LoadChromatogramInfo(chromKeyIndices.GroupIndex);
            chromGroupInfo.ReadChromatogram(_chromatogramCache);
            var tranInfo = chromGroupInfo.GetTransitionInfo(chromKeyIndices.TranIndex);
            if (tranInfo.TimeIntensities == null || tranInfo.TimeIntensities.NumPoints == 0)
            {
                // Chorus returns zero length chromatogram to indicate that no spectra matched
                // the precursor filter.
                timeIntensities = null;
                return false;
            }
            timeIntensities = CoalesceIntensities(tranInfo.TimeIntensities);
            return true;
        }

        /// <summary>
        /// Returns true if the two ChromKeys are close enough to match.
        /// Chorus sometimes rounds off the numbers of the precursor and product mz's
        /// of the chromatograms we ask for.
        /// </summary>
        private bool EqualsTolerant(ChromKey key1, ChromKey key2, float tolerance)
        {
            return Equals(key1.Source, key2.Source) && Equals(key1.Target, key2.Target) &&
                   Equals(key1.Extractor, key2.Extractor)
                   && 0 == key1.Precursor.CompareTolerant(key2.Precursor, tolerance)
                   && 0 == key2.Product.CompareTolerant(key2.Product, tolerance);
        }

        /// <summary>
        /// If the chromatogram contains duplicate times, combine those duplicate times by summing the intensities
        /// and appropriately averaging the mass errors.
        /// TODO(nicksh): Remove this once Chorus changes to not have these duplicates.
        /// </summary>
        private TimeIntensities CoalesceIntensities(TimeIntensities timeIntensities)
        {
            IList<float> times = timeIntensities.Times;
            IList<float> intensities = timeIntensities.Intensities;
            IList<int> scanIds = timeIntensities.ScanIds;
            IList<float> massErrors = timeIntensities.MassErrors;
            List<float> newTimes = new List<float>();
            List<int> newScanIds = new List<int>();
            List<float> newIntensities = new List<float>();
            List<float> newMassErrors = new List<float>();

            float? curTime = null;
            int curScanId = 0;
            double curIntensity = 0;
            double curMassError = 0;
            bool anyCoalescing = false;

            for (int i = 0; i < times.Count; i++)
            {
                if (times[i] != curTime)
                {
                    if (curTime.HasValue)
                    {
                        newTimes.Add(curTime.Value);
                        newScanIds.Add(curScanId);
                        newIntensities.Add((float) curIntensity);
                        newMassErrors.Add((float) curMassError);
                    }
                    curTime = times[i];
                    curIntensity = intensities[i];
                    if (null != scanIds)
                    {
                        curScanId = scanIds[i];
                    }
                    if (null != massErrors)
                    {
                        curMassError = massErrors[i];
                    }
                }
                else
                {
                    anyCoalescing = true;
                    var newIntensity = curIntensity + intensities[i];
                    if (newIntensity > 0)
                    {
                        if (null != massErrors)
                        {
                            curMassError = (curMassError*curIntensity + massErrors[i]*intensities[i])/newIntensity;
                        }
                        curIntensity = newIntensity;
                    }
                    else
                    {
                        curIntensity = intensities[i];
                        if (null != massErrors)
                        {
                            curMassError = massErrors[i];
                        }
                    }
                }
            }
            if (curTime.HasValue)
            {
                newTimes.Add(curTime.Value);
                newScanIds.Add(curScanId);
                newIntensities.Add((float)curIntensity);
                newMassErrors.Add((float)curMassError);
            }
            if (!anyCoalescing)
            {
                return timeIntensities;
            }
            return new TimeIntensities(newTimes, newIntensities, massErrors == null ? null : newMassErrors, scanIds == null ? null : newScanIds);
        }

        internal class MemoryPooledStream : IPooledStream
        {
            public MemoryPooledStream(Stream stream)
            {
                Stream = stream;
            }
            public int GlobalIndex
            {
                get { return 0; }
            }

            public Stream Stream
            {
                get;
                private set;
            }

            public bool IsModified
            {
                get { return false; }
            }

            public string ModifiedExplanation
            {
                get { return "Unmodified"; }    // Not L10N
            }

            public bool IsOpen
            {
                get { return Stream != null; }
            }

            public void CloseStream()
            {
                Stream.Close();
            }
        }
    }
}
