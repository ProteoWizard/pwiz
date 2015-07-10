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
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.RemoteApi
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

        public bool GetChromatogram(ChromKey chromKey, out float[] times, out int[] scanIds, out float[] intensities, out float[] massErrors)
        {
            int keyIndex = -1;
            if (_chromKeyIndiceses != null)
            {
                var tolerance = (float)ChromTaskList.SrmDocument.Settings.TransitionSettings.Instrument.MzMatchTolerance;
                // Null out the OptionalMinTime and OptionalMaxTime of the ChromKey we are looking for.
                var chromKeyToFind = new ChromKey(chromKey.TextId, chromKey.Precursor, chromKey.IonMobilityValue,
                    chromKey.IonMobilityExtractionWidth, chromKey.Product, chromKey.CollisionEnergy,
                    chromKey.ExtractionWidth, chromKey.Source, chromKey.Extractor, chromKey.HasCalculatedMzs,
                    chromKey.HasScanIds, null, null);
                keyIndex = _chromKeyIndiceses.IndexOf(entry => entry.Key.CompareTolerant(chromKeyToFind, tolerance) == 0);
            }
            if (keyIndex == -1 || _chromKeyIndiceses == null)   // Keep ReSharper from complaining
            {
                times = null;
                scanIds = null;
                intensities = null;
                massErrors = null;
                return false;
            }
            ChromKeyIndices chromKeyIndices = _chromKeyIndiceses[keyIndex];
            var chromGroupInfo = _chromatogramCache.LoadChromatogramInfo(chromKeyIndices.GroupIndex);
            chromGroupInfo.ReadChromatogram(_chromatogramCache);
            var tranInfo = chromGroupInfo.GetTransitionInfo(chromKeyIndices.TranIndex);
            times = tranInfo.Times;
            if (times.Length == 0)
                throw new IOException(Resources.ChromatogramGeneratorTask_GetChromatogram_Unexpected_zero_length_chromatogram_returned_from_Chorus_);
            if (null != tranInfo.ScanIds)
            {
                scanIds = tranInfo.ScanIds[(short) chromKeyIndices.Key.Source];
            }
            else
            {
                scanIds = null;
            }
            intensities = tranInfo.Intensities;
            massErrors = null;
            if (tranInfo.MassError10Xs != null)
                massErrors = tranInfo.MassError10Xs.Select(m => m / 10.0f).ToArray();
            return true;
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
