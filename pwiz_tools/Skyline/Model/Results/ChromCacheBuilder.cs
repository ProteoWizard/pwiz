/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Crawdad;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    internal sealed class ChromCacheBuilder : ChromCacheWriter
    {
        private readonly SrmDocument _document;
        private int _currentFileIndex = -1;
        private readonly List<PeptideChromDataSets> _chromDataSets = new List<PeptideChromDataSets>();
        private bool _writerStarted;
        private bool _readedCompleted;
        private Exception _writeException;

        public ChromCacheBuilder(SrmDocument document, string cachePath, IList<string> msDataFilePaths,
                                 ILoadMonitor loader, ProgressStatus status, Action<ChromatogramCache, Exception> complete)
            : base(cachePath, loader, status, complete)
        {
            _document = document;

            MSDataFilePaths = msDataFilePaths;
        }

        private IList<string> MSDataFilePaths { get; set; }

        private bool IsTimeNormalArea
        {
            get
            {
                return !_document.Settings.HasResults ||
                       _document.Settings.MeasuredResults.IsTimeNormalArea;
            }
        }

        public void BuildCache()
        {
            lock (this)
            {
                if (_currentFileIndex != -1)
                    return;
                _currentFileIndex = 0;
                BuildNextFile();
            }
        }

        private void BuildNextFile()
        {
            lock (this)
            {
                if (_currentFileIndex >= MSDataFilePaths.Count)
                {
                    Complete(null);
                    return;
                }

                // Check for cancellation on every chromatogram, because there
                // have been some files that load VERY slowly, and appear to hang
                // on a single file.
                if (_loader.IsCanceled)
                {
                    _loader.UpdateProgress(_status = _status.Cancel());
                    Complete(null);
                    return;
                }

                // If not cancelled, update progress.
                string dataFilePath = MSDataFilePaths[_currentFileIndex];
                string message = String.Format("Caching file {0}", dataFilePath);
                int percent = _currentFileIndex*100/MSDataFilePaths.Count;
                _status = _status.ChangeMessage(message).ChangePercentComplete(percent);
                _loader.UpdateProgress(_status);

                try
                {
                    string dataFilePathPart;
                    dataFilePath = ChromatogramSet.GetExistingDataFilePath(CachePath, dataFilePath, out dataFilePathPart);                        
                    if (dataFilePath == null)
                        throw new FileNotFoundException(String.Format("The file {0} does not exist.", dataFilePathPart), dataFilePathPart);
                    MSDataFilePaths[_currentFileIndex] = dataFilePath;

                    // HACK: Force the thread that the writer will use into existence
                    // This allows Reader_Waters to function normally the first time through.
                    //
                    // TODO: Use of Reader_Waters will, however, eventually kill the ThreadPool
                    // So, something better needs to be worked out, if we can't get a fix
                    // from Waters.
                    //
                    // This does not actually start the loop, but calling the function once,
                    // seems to reserve a thread somehow, so that the next call works.
                    Action<int, bool> writer = WriteLoop;
                    writer.BeginInvoke(_currentFileIndex, true, null, null);

                    // Read the instrument data indexes
                    int sampleIndex = SampleHelp.GetPathSampleIndexPart(dataFilePath);
                    if (sampleIndex == -1)
                        sampleIndex = 0;

                    using (var inFile = new MsDataFileImpl(dataFilePathPart, sampleIndex))
                    {

                        // Check for cancelation
                        if (_loader.IsCanceled)
                        {
                            _loader.UpdateProgress(_status = _status.Cancel());
                            Complete(null);
                            return;
                        }
                        if (_outStream == null)
                            _outStream = _loader.StreamManager.CreateStream(_fs.SafeName, FileMode.Create, true);

                        // Read and write the mass spec data
                        ChromDataProvider provider;
                        if (inFile.ChromatogramCount > 0)
                            provider = CreateChromatogramProvider(inFile);
                        else if (inFile.SpectrumCount > 0)
                            provider = CreateSpectraChromProvider(inFile);
                        else
                        {
                            throw new InvalidDataException(String.Format("The sample {0} contains no usable data.",
                                                                         SampleHelp.GetFileSampleName(dataFilePath)));
                        }

                        Read(provider);

                        _status = provider.Status;
                    }

                    if (_status.IsCanceled)
                        Complete(null);
                }
                catch (NoSrmDataException)
                {
                    Complete(new InvalidDataException(String.Format("No SRM/MRM data found in {0}.",
                        SampleHelp.GetFileSampleName(MSDataFilePaths[_currentFileIndex]))));
                }
                catch (Exception x)
                {
                    // Add a more generic message to an exception message that may
                    // be fairly unintelligible to the user, but keep the exception
                    // message, because ProteoWizard "Unsupported file format" comes
                    // in on this channel.
                    Complete(x);
                }
            }
        }

        private void Read(ChromDataProvider provider)
        {
            var dictPeptideChromData = new Dictionary<int, PeptideChromDataSets>();
            var listChromData = new List<PeptideChromDataSets>();

            var listMzPrecursors = new List<KeyValuePair<double, TransitionGroupDocNode>>(Precursors);
            listMzPrecursors.Sort((p1, p2) => p1.Key.CompareTo(p2.Key));

            foreach (var chromDataSet in GetChromDataSets(provider))
            {
                foreach (var matchingGroup in GetMatchingGroups(chromDataSet, listMzPrecursors))
                {
                    AddChromDataSet(provider.IsPorcessedScans,
                                    matchingGroup.Value,
                                    matchingGroup.Key,
                                    dictPeptideChromData,
                                    listChromData);
                }
            }

            listChromData.AddRange(dictPeptideChromData.Values);
            listChromData.Sort((p1, p2) =>
                Comparer.Default.Compare(p1.DataSets[0].PrecursorMz, p2.DataSets[0].PrecursorMz));

            foreach (var pepChromData in listChromData)
            {
                pepChromData.Load(provider);
                PostChromDataSet(pepChromData, false);
            }
            PostChromDataSet(null, true);
        }

        private IEnumerable<KeyValuePair<double, TransitionGroupDocNode>> Precursors
        {
            get
            {
                return from nodeGroup in _document.TransitionGroups
                       select new KeyValuePair<double, TransitionGroupDocNode>(nodeGroup.PrecursorMz, nodeGroup);
            }
        }

        private IEnumerable<ChromDataSet> GetChromDataSets(ChromDataProvider provider)
        {
            var listKeyIndex = new List<KeyValuePair<ChromKey, int>>(provider.ChromIds);
            listKeyIndex.Sort((p1, p2) => p1.Key.CompareTo(p2.Key));

            ChromKey lastKey = new ChromKey(0, 0);
            ChromDataSet chromDataSet = null;
            foreach (var keyIndex in listKeyIndex)
            {
                var key = keyIndex.Key;
                var chromData = new ChromData(key, keyIndex.Value);

                if (chromDataSet != null && key.Precursor == lastKey.Precursor)
                    chromDataSet.Add(chromData);
                else
                {
                    if (chromDataSet != null)
                        yield return chromDataSet;

                    chromDataSet = new ChromDataSet(IsTimeNormalArea, chromData);
                }
                lastKey = key;
            }

            yield return chromDataSet;
        }

        private static void AddChromDataSet(bool isProcessedScans,
                                            ChromDataSet chromDataSet,
                                            TransitionGroupDocNode nodeGroup,
                                            IDictionary<int, PeptideChromDataSets> dictPeptideChromData,
                                            ICollection<PeptideChromDataSets> listChromData)
        {
            // If there was no matching precursor, just add this as a stand-alone set
            if (nodeGroup == null)
            {
                listChromData.Add(new PeptideChromDataSets(isProcessedScans, chromDataSet));
                return;
            }

            // Otherwise, add it to the dictionary by its peptide GlobalIndex to make
            // sure precursors are grouped by peptide
            int id = nodeGroup.TransitionGroup.Peptide.GlobalIndex;
            PeptideChromDataSets pepDataSets;
            if (!dictPeptideChromData.TryGetValue(id, out pepDataSets))
            {
                pepDataSets = new PeptideChromDataSets(isProcessedScans);
                dictPeptideChromData.Add(id, pepDataSets);
            }
            chromDataSet.DocNode = nodeGroup;
            pepDataSets.DataSets.Add(chromDataSet);
        }

        private static IEnumerable<KeyValuePair<TransitionGroupDocNode, ChromDataSet>> GetMatchingGroups(
            ChromDataSet chromDataSet, List<KeyValuePair<double, TransitionGroupDocNode>> listMzPrecursors)
        {
            // Find the first precursor m/z that is greater than or equal to the
            // minimum possible match value
            double minMzMatch = chromDataSet.PrecursorMz - TransitionInstrument.MAX_MZ_MATCH_TOLERANCE;
            double maxMzMatch = chromDataSet.PrecursorMz + TransitionInstrument.MAX_MZ_MATCH_TOLERANCE;
            var lookup = new KeyValuePair<double, TransitionGroupDocNode>(minMzMatch, null);
            int i = listMzPrecursors.BinarySearch(lookup, MZ_COMPARER);
            if (i < 0)
                i = ~i;
            // Enumerate all possible matching precursor values, collecting the ones
            // with potentially matching product ions
            var listMatchingGroups = new List<KeyValuePair<TransitionGroupDocNode, IList<ChromData>>>();
            for (; i < listMzPrecursors.Count && listMzPrecursors[i].Key <= maxMzMatch; i++)
            {
                var nodeGroup = listMzPrecursors[i].Value;
                var groupData = GetMatchingData(nodeGroup, chromDataSet);
                if (groupData != null)
                    listMatchingGroups.Add(new KeyValuePair<TransitionGroupDocNode, IList<ChromData>>(nodeGroup, groupData));
            }

            FilterMatchingGroups(listMatchingGroups);

            if (listMatchingGroups.Count == 0)
            {
                // No matches found
                yield return new KeyValuePair<TransitionGroupDocNode, ChromDataSet>(
                    null, chromDataSet);                
            }
            else if (listMatchingGroups.Count == 1)
            {
                // If only one match is found, return product ions for the precursor, whether they
                // all match or not.
                yield return new KeyValuePair<TransitionGroupDocNode, ChromDataSet>(
                    listMatchingGroups[0].Key, chromDataSet);
            }
            else
            {
                // Otherwise, split up the product ions among the precursors they matched
                bool isTimeNormalArea = chromDataSet.IsTimeNormalArea;

                foreach (var match in listMatchingGroups)
                {
                    var chromDataPart = new ChromDataSet(isTimeNormalArea, match.Value.ToArray());
                    yield return new KeyValuePair<TransitionGroupDocNode, ChromDataSet>(
                        match.Key, chromDataPart);
                }
            }
        }

        private static void FilterMatchingGroups(
                List<KeyValuePair<TransitionGroupDocNode, IList<ChromData>>> listMatchingGroups)
        {
            if (listMatchingGroups.Count < 2)
                return;
            // Filter for only matches that do not match a strict subset of another match.
            // That is, if there is a precursor that matches 4 product ions, and another that
            // matches 2 of those same 4, then we want to discard the one with only 2.
            var listFiltered = new List<KeyValuePair<TransitionGroupDocNode, IList<ChromData>>>();
            foreach (var match in listMatchingGroups)
            {
                var subset = match;
                if (!listMatchingGroups.Contains(superset => IsMatchSubSet(subset, superset)))
                    listFiltered.Add(match);
            }
            listMatchingGroups.Clear();
            listMatchingGroups.AddRange(listFiltered);
        }

        private static bool IsMatchSubSet(KeyValuePair<TransitionGroupDocNode, IList<ChromData>> subset,
            KeyValuePair<TransitionGroupDocNode, IList<ChromData>> superset)
        {
            var subList = subset.Value;
            var superList = superset.Value;
            // Can't be a subset, if it doesn't have fewer element in its list
            if (subList.Count >= superList.Count)
                return false;
            foreach (var chromData in subList)
            {
                // Not a subset, if it contains something that is not in the superset list
                if (!superList.Contains(chromData))
                    return false;
            }
            // Must be a subset
            return true;
        }

// ReSharper disable SuggestBaseTypeForParameter
        private static IList<ChromData> GetMatchingData(TransitionGroupDocNode nodeGroup, ChromDataSet chromDataSet)
// ReSharper restore SuggestBaseTypeForParameter
        {
            // Look for potential product ion matches
            var listMatchingData = new List<ChromData>();
            const float tolerance = (float) TransitionInstrument.MAX_MZ_MATCH_TOLERANCE;
            foreach (var chromData in chromDataSet.Chromatograms)
            {
                foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                {
                    if (ChromKey.CompareTolerant(chromData.Key.Product,
                            (float) nodeTran.Mz, tolerance) == 0)
                    {
                        listMatchingData.Add(chromData);
                        break;
                    }
                }
            }
            // Only return a match, if at least two product ions match
            if (listMatchingData.Count < 2)
                return null;
            return listMatchingData;
        }

        private static readonly MzComparer MZ_COMPARER = new MzComparer();

        internal class MzComparer : IComparer<KeyValuePair<double, TransitionGroupDocNode>>
        {
            public int Compare(KeyValuePair<double, TransitionGroupDocNode> p1,
                               KeyValuePair<double, TransitionGroupDocNode> p2)
            {
                return Comparer.Default.Compare(p1.Key, p2.Key);
            }
        }

        private int StartPercent { get { return _currentFileIndex*100/MSDataFilePaths.Count; } }
        private int EndPercent { get { return (_currentFileIndex + 1)*100/MSDataFilePaths.Count; } }

        private ChromDataProvider CreateChromatogramProvider(MsDataFileImpl dataFile)
        {
            return new ChromatogramDataProvider(dataFile, _status, StartPercent, EndPercent, _loader);
        }

        private SpectraChromDataProvider CreateSpectraChromProvider(MsDataFileImpl dataFile)
        {
            return new SpectraChromDataProvider(dataFile, _status, StartPercent, EndPercent, _loader);
        }

        private abstract class ChromDataProvider
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
                percent = Math.Min(_endPercent, (_endPercent - _startPercent) * percent / 100 + _startPercent);
                if (Status.IsPercentComplete(percent))
                    return;
                
                if (_loader.IsCanceled)
                {
                    _loader.UpdateProgress(Status = Status.Cancel());
                    return;
                }

                _loader.UpdateProgress(Status = Status.ChangePercentComplete(percent));
            }

            public ProgressStatus Status { get; private set; }

            public abstract IEnumerable<KeyValuePair<ChromKey, int>> ChromIds { get; }

            public abstract void GetChromatogram(int id, out float[] times, out float[] intensities);

            public abstract bool IsPorcessedScans { get; }
        }

        private sealed class SpectraChromDataProvider : ChromDataProvider
        {
            private readonly List<KeyValuePair<ChromKey, ChromCollector>> _chromatograms =
                new List<KeyValuePair<ChromKey, ChromCollector>>();

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
                        SetPercentComplete((eighth + 1)*10);
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

            public override bool IsPorcessedScans
            {
                get { return true; }
            }
        }

        private sealed class ChromatogramDataProvider : ChromDataProvider
        {
            private readonly IList<KeyValuePair<ChromKey, int>> _chromIds =
                new List<KeyValuePair<ChromKey, int>>();

            private readonly MsDataFileImpl _dataFile;
            private int _readChromatograms;

            public ChromatogramDataProvider(MsDataFileImpl dataFile,
                                            ProgressStatus status,
                                            int startPercent,
                                            int endPercent,
                                            IProgressMonitor loader)
                : base(status, startPercent, endPercent, loader)
            {
                _dataFile = dataFile;

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
                string chromId;
                _dataFile.GetChromatogram(id, out chromId, out times, out intensities);

                // Assume that each chromatogram will be read once, though this may
                // not always be completely true.
                _readChromatograms++;
                if (_readChromatograms < _chromIds.Count)
                    SetPercentComplete(50 + _readChromatograms*50/_chromIds.Count);
            }

            public override bool IsPorcessedScans
            {
                get { return false; }
            }
        }


        private class NoSrmDataException : IOException
        {
            public NoSrmDataException()
                : base("No SRM/MRM data found")
            {
            }
        }

        private void PostChromDataSet(PeptideChromDataSets chromDataSet, bool complete)
        {
            lock (_chromDataSets)
            {
                // First check for any errors on the writer thread
                if (_writeException != null)
                    throw _writeException;

                // Add new chromatogram data set, if not empty
                if (chromDataSet != null)
                {
                    _chromDataSets.Add(chromDataSet);
                }
                // Update completion status
                _readedCompleted = _readedCompleted || complete;
                // Notify the writer thread, if necessary
                if (_readedCompleted || _chromDataSets.Count > 0)
                {
                    if (_writerStarted)
                        Monitor.Pulse(_chromDataSets);
                    else
                    {
                        // Start the writer thread
                        _writerStarted = true;
                        Action<int, bool> writer = WriteLoop;
                        writer.BeginInvoke(_currentFileIndex, false, null, null);
                    }

                    // If this is the last read, then wait for the
                    // writer to complete, in case of an exception.
                    if (_readedCompleted)
                    {
                        int countSets = _chromDataSets.Count;
                        if (countSets > 0)
                        {
                            // Wait while work is being accomplished by the writer, but not
                            // if it is hung.
                            bool completed;
                            do
                            {
                                countSets = _chromDataSets.Count;
                                // Wait 5 seconds for some work to complete.  In debug mode,
                                // a shorter time may not be enough to load DLLs necessary
                                // for the first iteration.
                                completed = Monitor.Wait(_chromDataSets, 5000);
                            }
                            while (!completed && countSets != _chromDataSets.Count);

                            // Try calling the write loop directly on this thread.
                            if (!completed)
                                WriteLoop(_currentFileIndex, false);                                
                        }

                        if (_writeException != null)
                            throw _writeException;
                    }
                }
            }
        }

        private void WriteLoop(int currentFileIndex, bool primeThread)
        {
            // HACK: This is a huge hack, for a temporary work-around to the problem
            // of Reader_Waters (or DACServer.dll) killing the ThreadPool.  WriteLoop
            // is called once as a no-op to force the thread it will use during
            // processing into existence before the file is opened.
            if (primeThread)
                return;

            try
            {
                for (;;)
                {
                    PeptideChromDataSets chromDataSetNext;
                    lock (_chromDataSets)
                    {
                        while (!_readedCompleted && _chromDataSets.Count == 0)
                            Monitor.Wait(_chromDataSets);

                        // If reading is complete, and there are no more sets to process,
                        // begin next file.
                        if (_readedCompleted && _chromDataSets.Count == 0)
                        {
                            // Write loop completion may have already been executed
                            if (_currentFileIndex != currentFileIndex)
                                return;

                            string dataFilePath = MSDataFilePaths[_currentFileIndex];
                            _listCachedFiles.Add(new ChromCachedFile(dataFilePath));
                            _currentFileIndex++;

                            // Allow the reader thread to exit
                            lock (_chromDataSets)
                            {
                                Monitor.Pulse(_chromDataSets);
                            }

                            Action build = BuildNextFile;
                            build.BeginInvoke(null, null);
                            return;
                        }

                        chromDataSetNext = _chromDataSets[0];
                        _chromDataSets.RemoveAt(0);
                    }

                    chromDataSetNext.PickChromatogramPeaks();

                    foreach (var chromDataSet in chromDataSetNext.DataSets)
                    {
                        if (_outStream == null)
                            throw new InvalidDataException("Failure writing cache file.");

                        long location = _outStream.Position;

                        float[] times = chromDataSet.Times;
                        float[][] intensities = chromDataSet.Intensities;
                        // Write the raw chromatogram points
                        byte[] points = ChromatogramCache.TimeIntensitiesToBytes(times, intensities);
                        // Compress the data (can be huge for AB data with lots of zeros)
                        byte[] peaksCompressed = points.Compress(3);
                        int lenCompressed = peaksCompressed.Length;
                        _outStream.Write(peaksCompressed, 0, lenCompressed);

                        // Add to header list
                        //                        Debug.Assert(headData.MaxPeakIndex != -1);

                        var header = new ChromGroupHeaderInfo(chromDataSet.PrecursorMz,
                                                              currentFileIndex,
                                                              chromDataSet.Count,
                                                              _listTransitions.Count,
                                                              chromDataSet.CountPeaks,
                                                              _listPeaks.Count,
                                                              chromDataSet.MaxPeakIndex,
                                                              times.Length,
                                                              lenCompressed,
                                                              location);

                        foreach (var chromData in chromDataSet.Chromatograms)
                        {
                            _listTransitions.Add(new ChromTransition(chromData.Key.Product));

                            // Add to peaks list
                            foreach (var peak in chromData.Peaks)
                                _listPeaks.Add(peak);
                        }

                        _listGroups.Add(header);
                    }
                }
            }
            catch (Exception x)
            {
                lock (_chromDataSets)
                {
                    _writeException = x;
                    // Make sure the reader thread can exit
                    Monitor.Pulse(_chromDataSets);
                }
            }
        }

        private sealed class ChromDataCollector
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

        private sealed class ChromCollector
        {
            public ChromCollector()
            {
                Times = new List<float>();
                Intensities = new List<float>();
            }

            public List<float> Times { get; private set; }
            public List<float> Intensities { get; private set; }
        }

        private sealed class PeptideChromDataSets
        {
            private const double TIME_DELTA_VARIATION_THRESHOLD = 0.001;
            private const double TIME_DELTA_MAX_RATIO_THRESHOLD = 25;
            private const int MINIMUM_DELTAS_PER_CHROM = 4;

            private readonly List<ChromDataSet> _dataSets = new List<ChromDataSet>();
            private readonly bool _isProcessedScans;

            public PeptideChromDataSets(bool isProcessedScans)
            {
                _isProcessedScans = isProcessedScans;
            }

            public PeptideChromDataSets(bool isProcessedScans, ChromDataSet chromDataSet)
                : this(isProcessedScans)
            {
                DataSets.Add(chromDataSet);
            }

            public IList<ChromDataSet> DataSets { get { return _dataSets; } }

            private IEnumerable<ChromDataSet> ComparableDataSets
            {
                get
                {
                    return from dataSet in DataSets
                           where dataSet.DocNode != null && dataSet.DocNode.RelativeRT != RelativeRT.Unknown
                           select dataSet;
                }
            }

            private IEnumerable<ChromData> ChromDatas
            {
                get
                {
                    foreach (var chromDataSet in _dataSets)
                    {
                        foreach (var chromData in chromDataSet.Chromatograms)
                            yield return chromData;
                    }
                }
            }

            public void Load(ChromDataProvider provider)
            {
                foreach (var set in _dataSets)
                    set.Load(provider);
            }

            public void PickChromatogramPeaks()
            {
                // Make sure times are evenly spaced before doing any peak detection.
                EvenlySpaceTimes();

                foreach (var chromDataSet in _dataSets)
                    chromDataSet.PickChromatogramPeaks();

                PickPeptidePeaks();

                foreach (var chromDataSet in _dataSets)
                    chromDataSet.StorePeaks();
            }

            private bool ThermoZerosFix()
            {
                bool fixApplied = false;
                foreach (var chromDataSet in DataSets)
                    fixApplied = chromDataSet.ThermoZerosFix() || fixApplied;
                return fixApplied;
            }

            // Moved to ProteoWizard
// ReSharper disable UnusedMember.Local
            private bool WiffZerosFix()
// ReSharper restore UnusedMember.Local
            {
                bool fixApplied = false;
                foreach (var chromDataSet in DataSets)
                    fixApplied = chromDataSet.WiffZerosFix() || fixApplied;
                return fixApplied;
            }

            private void EvenlySpaceTimes()
            {
                // Handle an issue where the ProteoWizard Reader_Thermo returns chromatograms
                // with alternating zero intensity scans with real data
                if (ThermoZerosFix())
                {
                    EvenlySpaceTimes();
                    return;
                }
                // Moved to ProteoWizard
//                else if (WiffZerosFix())
//                {
//                    EvenlySpaceTimes();
//                    return;
//                }

                // Accumulate time deltas looking for variation that violates our ability
                // to do valid peak detection with Crawdad.
                bool foundVariation = false;

                List<double> listDeltas = new List<double>();
                List<double> listMaxDeltas = new List<double>();
                double maxIntensity = 0;
                float[] firstTimes = null;
                double expectedTimeDelta = 0;
                int countChromData = 0;
                foreach (var chromData in ChromDatas)
                {
                    countChromData++;
                    if (firstTimes == null)
                    {
                        firstTimes = chromData.Times;
                        if (firstTimes.Length == 0)
                            continue;
                        expectedTimeDelta = (firstTimes[firstTimes.Length - 1] - firstTimes[0])/firstTimes.Length;
                    }
                    if (firstTimes.Length != chromData.Times.Length)
                        foundVariation = true;

                    double lastTime = 0;
                    var times = chromData.Times;
                    if (times.Length > 0)
                        lastTime = times[0];
                    for (int i = 1, len = chromData.Times.Length; i < len; i++)
                    {
                        double time = times[i];
                        double delta = time - lastTime;
                        lastTime = time;
                        listDeltas.Add(Math.Round(delta, 4));

                        // Collect the 10 deltas after the maximum peak
                        if (chromData.Intensities[i] > maxIntensity)
                        {
                            maxIntensity = chromData.Intensities[i];
                            listMaxDeltas.Clear();
                            listMaxDeltas.Add(delta);
                        }
                        else if (0 < listMaxDeltas.Count && listMaxDeltas.Count < 10)
                        {
                            listMaxDeltas.Add(delta);
                        }

                        if (!foundVariation && (time != firstTimes[i] ||
                                                Math.Abs(delta - expectedTimeDelta) > TIME_DELTA_VARIATION_THRESHOLD))
                        {
                            foundVariation = true;
                        }
                    }
                }

                // If time deltas are sufficiently evenly spaced, then no further processing
                // is necessary.
                if (!foundVariation && listDeltas.Count > 0)
                    return;

                // Interpolate the existing points onto time intervals evently spaced
                // by the minimum interval observed in the measuered data.
                double intervalDelta = 0;
                var statDeltas = new Statistics(listDeltas);
                if (statDeltas.Length > 0)
                {
                    double[] bestDeltas = statDeltas.Modes();
                    if (bestDeltas.Length == 0 || bestDeltas.Length > listDeltas.Count/2)
                        intervalDelta = statDeltas.Min();
                    else if (bestDeltas.Length == 1)
                        intervalDelta = bestDeltas[0];
                    else
                    {
                        var statIntervals = new Statistics(bestDeltas);
                        intervalDelta = statIntervals.Min();
                    }
                }

                intervalDelta = EnsureMinDelta(intervalDelta);

                bool inferZeros = false;
                if (_isProcessedScans &&
                    (statDeltas.Length < countChromData * MINIMUM_DELTAS_PER_CHROM ||
                     statDeltas.Max() / intervalDelta > TIME_DELTA_MAX_RATIO_THRESHOLD))
                {
                    inferZeros = true; // Verbose expression for easy breakpoint placement

                    // Try really hard to use a delta that will work for the maximum peak
                    intervalDelta = EnsureMinDelta(GetIntervalMaxDelta(listMaxDeltas, intervalDelta));
                }

                // Create a master set of time intervals that all points for
                // this peptide will be mapped onto.
                double start, end;
                GetExtents(inferZeros, intervalDelta, out start, out end);

                var listTimesNew = new List<float>();
                for (double t = start; t < end; t += intervalDelta)
                    listTimesNew.Add((float)t);
                float[] timesNew = listTimesNew.ToArray();

                // Perform interpolation onto the new times
                foreach (var chromDataSet in DataSets)
                {
                    // Determine what segment of the new time intervals array covers this precursor
                    int startSet, endSet;
                    chromDataSet.GetExtents(inferZeros, intervalDelta, timesNew, out startSet, out endSet);

                    float[] timesNewPrecursor = timesNew;
                    int countTimes = endSet - startSet + 1;  // +1 because endSet is inclusive
                    if (countTimes != timesNewPrecursor.Length)
                    {
                        // Copy the segment into a new array for this precursor only
                        timesNewPrecursor = new float[countTimes];
                        Array.Copy(timesNew, startSet, timesNewPrecursor, 0, countTimes);
                    }

                    foreach (var chromData in chromDataSet.Chromatograms)
                    {
                        chromData.Interpolate(timesNewPrecursor, intervalDelta, inferZeros);
                    }
                    chromDataSet.Offset = startSet;
                }
            }

            /// <summary>
            /// Gets extents that can contain all of the precursor sets.
            /// </summary>
            private void GetExtents(bool inferZeros, double intervalDelta, out double start, out double end)
            {
                start = double.MaxValue;
                end = double.MinValue;
                foreach (var chromDataSet in DataSets)
                {
                    double startSet, endSet;
                    chromDataSet.GetExtents(inferZeros, intervalDelta, out startSet, out endSet);

                    start = Math.Min(start, startSet);
                    end = Math.Max(end, endSet);
                }
            }

            private static double EnsureMinDelta(double intervalDelta)
            {
                // Never go smaller than 1/5 a second.
                if (intervalDelta < 0.2 / 60)
                    intervalDelta = 0.2 / 60;  // For breakpoint setting
                return intervalDelta;
            }

            private static double GetIntervalMaxDelta(IList<double> listMaxDeltas, double intervalDelta)
            {
                const int magnitude = 8;    // 8x counted as an order of magnitude difference
                if (listMaxDeltas.Count > 0 && listMaxDeltas[0] / magnitude < intervalDelta)
                {
                    intervalDelta = listMaxDeltas[0];
                    for (int i = 1; i < listMaxDeltas.Count; i++)
                    {
                        double delta = listMaxDeltas[i];
                        // If an order of magnitude change in time interval is encountered stop
                        if (intervalDelta / magnitude > delta || delta > intervalDelta * magnitude)
                            break;
                        // Calculate a weighted mean
                        intervalDelta = (intervalDelta * i + delta) / (i + 1);
                    }
                }
//                else if (listMaxDeltas.Count > 0 && listMaxDeltas[0] / magnitude > intervalDelta)
//                {
//                    Console.WriteLine("Max delta {0} too much larger than {1}", listMaxDeltas[0], intervalDelta);
//                }
                return intervalDelta;
            }

            private void PickPeptidePeaks()
            {
                // Only possible to improve upon individual precursor peak picking,
                // if there are more than one precursor
                if (ComparableDataSets.Count() < 2)
                    return;

                // Merge all the peaks into a single set
                var allPeakGroups = MergePeakGroups();
                if (allPeakGroups.Count == 0)
                    return;

                // Create coeluting groups
                var listPeakSets = new List<PeptideChromDataPeakList>();
                while (allPeakGroups.Count > 0)
                {
                    PeptideChromDataPeak peak = allPeakGroups[0];
                    allPeakGroups.RemoveAt(0);

                    PeptideChromDataPeakList peakSet = FindCoelutingPeptidePeaks(peak, allPeakGroups);

                    listPeakSets.Add(peakSet);
                }

                // Sort descending by the peak picking score
                listPeakSets.Sort((p1, p2) => Comparer.Default.Compare(p2.ProductArea, p1.ProductArea));

                // Reset best picked peaks and reintegrate if necessary
                var peakSetBest = listPeakSets[0];
                foreach (var chargePeakGroup in peakSetBest.ChargeGroups)
                {
                    PeptideChromDataPeak peakBest = null;
                    foreach (var peak in chargePeakGroup)
                    {
                        // Ignore precursors with unknown relative RT. They do not participate
                        // in peptide peak matching.
                        if (peak.Data.DocNode.RelativeRT == RelativeRT.Unknown)
                            continue;

                        peak.Data.SetBestPeak(peak.PeakGroup, peakBest);
                        if (peakBest == null)
                            peakBest = peak;
                    }
                }
            }

            private PeptideChromDataPeakList FindCoelutingPeptidePeaks(PeptideChromDataPeak dataPeakMax, IList<PeptideChromDataPeak> allPeakGroups)
            {
                TransitionGroupDocNode nodeGroupMax = dataPeakMax.Data.DocNode;
                CrawdadPeak peakMax = dataPeakMax.PeakGroup[0].Peak;
                int offset = dataPeakMax.Data.Offset;
                int startMax = peakMax.StartIndex + offset;
                int endMax = peakMax.EndIndex + offset;
                int timeMax = peakMax.TimeIndex + offset;

                var listPeaks = new PeptideChromDataPeakList(dataPeakMax);
                foreach (var chromData in _dataSets)
                {
                    if (ReferenceEquals(chromData, dataPeakMax.Data))
                        continue;

                    int iPeakBest = -1;
                    double bestProduct = 0;

                    // Find nearest peak in remaining set that is less than 1/4 length
                    // from the primary peak's center
                    for (int i = 0, len = allPeakGroups.Count; i < len; i++)
                    {
                        var peakGroup = allPeakGroups[i];
                        if (!ReferenceEquals(peakGroup.Data, chromData))
                            continue;

                        // Exclude peaks that do not overlap with the maximum peak
                        TransitionGroupDocNode nodeGroup = peakGroup.Data.DocNode;
                        var peak = peakGroup.PeakGroup[0];
                        offset = peakGroup.Data.Offset;
                        int startPeak = peak.Peak.StartIndex + offset;
                        int endPeak = peak.Peak.EndIndex + offset;
                        if (Math.Min(endPeak, endMax) - Math.Max(startPeak, startMax) <= 0)
                            continue;

                        if (nodeGroup.TransitionGroup.PrecursorCharge == nodeGroupMax.TransitionGroup.PrecursorCharge)
                        {
                            int timeIndex = peak.Peak.TimeIndex + offset;
                            if (nodeGroup.RelativeRT == RelativeRT.Matching && nodeGroupMax.RelativeRT == RelativeRT.Matching)
                            {
                                // If the peaks are supposed to have the same elution time,
                                // then be more strict about how they overlap
                                if (startMax >= timeIndex || timeIndex >= endMax)
                                    continue;
                            }
                            else if (nodeGroup.RelativeRT == RelativeRT.Matching && nodeGroupMax.RelativeRT == RelativeRT.Preceding)
                            {
                                // If the maximum is supposed to precede this, look for any
                                // indication that this relationship holds, by testing the peak apex
                                // and the peak center.
                                if (timeIndex < timeMax && (startPeak + endPeak)/2 < (startMax + endMax)/2)
                                    continue;
                            }
                            else if (nodeGroup.RelativeRT == RelativeRT.Preceding && nodeGroupMax.RelativeRT == RelativeRT.Matching)
                            {
                                // If this peak is supposed to precede the maximum, look for any
                                // indication that this relationship holds, by testing the peak apex
                                // and the peak center.
                                if (timeIndex > timeMax && (startPeak + endPeak)/2 > (startMax + endMax)/2)
                                    continue;
                            }
                        }

                        // Choose the next best peak that overlaps
                        if (peakGroup.PeakGroup.ProductArea > bestProduct)
                        {
                            iPeakBest = i;
                            bestProduct = peakGroup.PeakGroup.ProductArea;
                        }
                    }

                    if (iPeakBest == -1)
                        listPeaks.Add(new PeptideChromDataPeak(chromData, null));
                    else
                    {
                        listPeaks.Add(new PeptideChromDataPeak(chromData, allPeakGroups[iPeakBest].PeakGroup));
                        allPeakGroups.RemoveAt(iPeakBest);
                    }
                }
                return listPeaks;
            }

            private IList<PeptideChromDataPeak> MergePeakGroups()
            {
                List<PeptideChromDataPeak> allPeaks = new List<PeptideChromDataPeak>();
                var listEnumerators = ComparableDataSets.ToList().ConvertAll(
                    dataSet => dataSet.PeakSets.GetEnumerator());

                // Merge with list of chrom data that will match the enumerators
                // list, as completed enumerators are removed.
                var listUnmerged = new List<ChromDataSet>(_dataSets);
                // Initialize an enumerator for each set of raw peaks, or remove
                // the set, if the list is found to be empty
                for (int i = listEnumerators.Count - 1; i >= 0; i--)
                {
                    if (!listEnumerators[i].MoveNext())
                    {
                        listEnumerators.RemoveAt(i);
                        listUnmerged.RemoveAt(i);
                    }
                }

                while (listEnumerators.Count > 0)
                {
                    double maxIntensity = 0;
                    int iMaxEnumerator = -1;

                    for (int i = 0; i < listEnumerators.Count; i++)
                    {
                        double intensity = listEnumerators[i].Current.ProductArea;
                        if (intensity > maxIntensity)
                        {
                            maxIntensity = intensity;
                            iMaxEnumerator = i;
                        }
                    }

                    // If no peaks left, stop looping.
                    if (iMaxEnumerator == -1)
                        break;

                    var maxData = listUnmerged[iMaxEnumerator];
                    var maxEnumerator = listEnumerators[iMaxEnumerator];
                    var maxPeak = maxEnumerator.Current;
                    Debug.Assert(maxPeak != null);

                    allPeaks.Add(new PeptideChromDataPeak(maxData, maxPeak));
                    if (!maxEnumerator.MoveNext())
                    {
                        listEnumerators.RemoveAt(iMaxEnumerator);
                        listUnmerged.RemoveAt(iMaxEnumerator);
                    }
                }
                return allPeaks;
            }
        }

        private sealed class PeptideChromDataPeak
        {
            public PeptideChromDataPeak(ChromDataSet data, ChromDataPeakList peakGroup)
            {
                Data = data;
                PeakGroup = peakGroup;
            }

            public ChromDataSet Data { get; private set; }
            public ChromDataPeakList PeakGroup { get; private set; }
        }

        private sealed class PeptideChromDataPeakList : Collection<PeptideChromDataPeak>
        {
            public PeptideChromDataPeakList(PeptideChromDataPeak peak)
            {
                Add(peak);
            }

            private int PeakCount { get; set; }
            private double TotalArea { get; set; }

            public double ProductArea { get; private set; }

            public IEnumerable<IGrouping<int, PeptideChromDataPeak>> ChargeGroups
            {
                get
                {
                    return from peak in this
                           orderby peak.PeakGroup != null ? peak.PeakGroup.ProductArea : 0 descending
                           group peak by peak.Data.DocNode.TransitionGroup.PrecursorCharge into g
                           select g;
                }
            }

            private void AddPeak(PeptideChromDataPeak dataPeak)
            {
                if (dataPeak.PeakGroup != null)
                {
                    PeakCount++;

                    TotalArea += dataPeak.PeakGroup.ProductArea;

                    ProductArea = TotalArea * Math.Pow(10.0, PeakCount);
                }
            }

            private void SubtractPeak(PeptideChromDataPeak dataPeak)
            {
                if (dataPeak.PeakGroup != null)
                {
                    PeakCount--;

                    if (PeakCount == 0)
                        TotalArea = 0;
                    else
                        TotalArea -= dataPeak.PeakGroup.ProductArea;

                    ProductArea = TotalArea*Math.Pow(10.0, PeakCount);
                }
            }

            protected override void ClearItems()
            {
                PeakCount = 0;
                TotalArea = 0;
                ProductArea = 0;

                base.ClearItems();
            }

            protected override void InsertItem(int index, PeptideChromDataPeak item)
            {
                AddPeak(item);
                base.InsertItem(index, item);
            }

            protected override void RemoveItem(int index)
            {
                SubtractPeak(this[index]);
                base.RemoveItem(index);
            }

            protected override void SetItem(int index, PeptideChromDataPeak item)
            {
                SubtractPeak(this[index]);
                AddPeak(item);
                base.SetItem(index, item);
            }
        }

        private sealed class ChromDataSet
        {
            private readonly List<ChromData> _listChromData = new List<ChromData>();
            private readonly bool _isTimeNormalArea;

            private List<ChromDataPeakList> _listPeakSets = new List<ChromDataPeakList>();

            public ChromDataSet(bool isTimeNormalArea, params ChromData[] arrayChromData)
            {
                _isTimeNormalArea = isTimeNormalArea;
                _listChromData.AddRange(arrayChromData);
            }

            public IEnumerable<ChromData> Chromatograms { get { return _listChromData; } }

            public int Count { get { return _listChromData.Count; } }

            public void Add(ChromData chromData)
            {
                _listChromData.Add(chromData);
            }

            public int Offset { get; set; }

            public bool IsTimeNormalArea { get { return _isTimeNormalArea; } }

            public IEnumerable<ChromDataPeakList> PeakSets { get { return _listPeakSets; } }

            public TransitionGroupDocNode DocNode { get; set; }

            public float PrecursorMz
            {
                get { return _listChromData.Count > 0 ? _listChromData[0].Key.Precursor : 0; }
            }

            public int CountPeaks
            {
                get { return _listChromData.Count > 0 ? _listChromData[0].Peaks.Count : 0; }
            }

            public int MaxPeakIndex
            {
                get { return _listChromData.Count > 0 ? _listChromData[0].MaxPeakIndex : 0; }                    
            }

            public float[] Times
            {
                get { return _listChromData.Count > 0 ? _listChromData[0].Times : new float[0]; }
            }

            public float[][] Intensities
            {
                get { return _listChromData.ConvertAll(data => data.Intensities).ToArray(); }
            }

            public void Load(ChromDataProvider provider)
            {
                foreach (var chromData in Chromatograms)
                    chromData.Load(provider);
            }

            private float MinRawTime
            {
                get
                {
                    float min = Single.MaxValue;
                    foreach (var chromData in _listChromData)
                    {
                        if (chromData.RawTimes.Length > 0)
                            min = Math.Min(min, chromData.RawTimes[0]);                            
                    }
                    return min;
                }
            }

            private float MaxStartTime
            {
                get
                {
                    float max = Single.MinValue;
                    foreach (var chromData in _listChromData)
                    {
                        if (chromData.RawTimes.Length > 0)
                            max = Math.Max(max, chromData.RawTimes[0]);                            
                    }
                    return max;
                }                    
            }

            private float MaxRawTime
            {
                get
                {
                    float max = Single.MinValue;
                    foreach (var chromData in _listChromData)
                    {
                        if (chromData.RawTimes.Length > 0)
                            max = Math.Max(max, chromData.RawTimes[chromData.RawTimes.Length - 1]);
                    }
                    return max;
                }
            }

            private float MinEndTime
            {
                get
                {
                    float min = Single.MaxValue;
                    foreach (var chromData in _listChromData)
                    {
                        if (chromData.RawTimes.Length > 0)
                            min = Math.Min(min, chromData.RawTimes[chromData.RawTimes.Length - 1]);                            
                    }
                    return min;
                }                    
            }

            /// <summary>
            /// If the minimum time is greater than two cycles from the maximum start,
            /// then use the minimum, and interpolate other transitions from it.
            /// Otherwise, try to avoid zeros at the edges, since they can create
            /// change that look like a peak.
            /// </summary>
            /// <param name="interval">Interval that will be used for interpolation</param>
            /// <returns>Value to use as the start time for chromatograms that do not infer zeros</returns>
            private float GetNonZeroStart(double interval)
            {
                float min = MinRawTime;
                float max = MaxStartTime;
                if (max - min > interval * 2)
                    return min;
                return max;
            }

            /// <summary>
            /// If the maximum time is greater than two cycles from the minimum end,
            /// then use the maximum, and interpolate other transitions to it.
            /// Otherwise, try to avoid zeros at the edges, since they can create
            /// change that looks like a peak.
            /// </summary>
            /// <param name="interval">Interval that will be used for interpolation</param>
            /// <returns>Value to use as the end time for chromatograms that do not infer zeros</returns>
            private float GetNonZeroEnd(double interval)
            {
                float min = MinEndTime;
                float max = MaxRawTime;
                if (max - min > interval * 2)
                    return max;
                return min;
            }

            public void GetExtents(bool inferZeros, double intervalDelta, out double start, out double end)
            {
                if (inferZeros)
                {
                    // If infering zeros, make sure values start and end with zero.
                    start = MinRawTime - intervalDelta * 2;
                    end = MaxRawTime + intervalDelta * 2;
                }
                else
                {
                    // Otherwise, do best to use a non-zero start
                    start = GetNonZeroStart(intervalDelta);
                    end = GetNonZeroEnd(intervalDelta);
                }
            }

            public void GetExtents(bool inferZeros, double intervalDelta, float[] timesNew, out int start, out int end)
            {
                // Get the extent times
                double startTime, endTime;
                GetExtents(inferZeros, intervalDelta, out startTime, out endTime);

                // Search forward for the time that best matches the start time.
                int i;
                for (i = 0; i < timesNew.Length; i++)
                {
                    float time = timesNew[i];
                    if (time == startTime)
                        break;
                    else if (time > startTime)
                    {
                        if (inferZeros)
                            i = Math.Max(0, i - 1);
                        break;
                    }
                }
                start = i;
                // Search backward from the end for the time that best matches the end time.
                int lastTime = timesNew.Length - 1;
                for (i = lastTime; i >= 0; i--)
                {
                    float time = timesNew[i];
                    if (time == endTime)
                        break;
                    else if (time < endTime)
                    {
                        if (inferZeros)
                            i = Math.Min(lastTime, i + 1);
                        break;
                    }
                }
                end = i;

                // Make sure the final time interval contains at least one time.
                if (start > end)
                    throw new InvalidOperationException(string.Format("The time interval {0} to {1} is not valid.", start, end));
            }

            private const double NOISE_CORRELATION_THRESHOLD = 0.95;
            private const int MINIMUM_PEAKS = 3;

            /// <summary>
            /// Do initial grouping of and ranking of peaks using the Crawdad
            /// peak detector.
            /// </summary>
            public void PickChromatogramPeaks()
            {
                // Make sure chromatograms are in sorted order
                _listChromData.Sort((c1, c2) => c1.Key.CompareTo(c2.Key));

                // Mark all optimization chromatograms
                MarkOptimizationData();

//                if (Math.Round(_listChromData[0].Key.Precursor) == 585)
//                    Console.WriteLine("Issue");

                // First use Crawdad to find the peaks
                _listChromData.ForEach(chromData => chromData.FindPeaks());

                // Merge sort all peaks into a single list
                IList<ChromDataPeak> allPeaks = MergePeaks();

                // Inspect 20 most intense peak regions
                var listRank = new List<double>();
                for (int i = 0; i < 20; i++)
                {
                    if (allPeaks.Count == 0)
                        break;

                    ChromDataPeak peak = allPeaks[0];
                    allPeaks.RemoveAt(0);
                    ChromDataPeakList peakSet = FindCoelutingPeaks(peak, allPeaks);
//                    Console.WriteLine("peak {0}: {1:F01}", i + 1, peakSet.TotalArea / 1000);

                    _listPeakSets.Add(peakSet);
                    listRank.Add(i);
                }

                if (_listPeakSets.Count == 0)
                    return;

                // Sort by total area descending
                _listPeakSets.Sort((p1, p2) => Comparer<double>.Default.Compare(p2.TotalArea, p1.TotalArea));

                // The peak will be a signigificant spike above the norm for this
                // data.  Find a cut-off by removing peaks until the remaining
                // peaks correlate well in a linear regression.
                var listAreas = _listPeakSets.ConvertAll(set => set.TotalArea);
                // Keep at least 3 peaks
                listRank.RemoveRange(0, Math.Min(MINIMUM_PEAKS, listRank.Count));
                listAreas.RemoveRange(0, Math.Min(MINIMUM_PEAKS, listAreas.Count));
                int iRemove = 0;
                // And there must be at least 5 peaks in the line to qualify for removal
                for (int i = 0, len = listAreas.Count; i < len - 4; i++)
                {
                    var statsRank = new Statistics(listRank);
                    var statsArea = new Statistics(listAreas);
                    double rvalue = statsArea.R(statsRank);
                    //                Console.WriteLine("i = {0}, r = {1}", i, rvalue);
                    if (Math.Abs(rvalue) > NOISE_CORRELATION_THRESHOLD)
                    {
                        iRemove = i + MINIMUM_PEAKS;
                        RemoveNonOverlappingPeaks(_listPeakSets, iRemove);
                        break;
                    }
                    listRank.RemoveAt(0);
                    listAreas.RemoveAt(0);
                }
                if (iRemove == 0)
                    iRemove = _listPeakSets.Count;
                // Add small peaks under the chosen peaks, to make adding them easier
                foreach (var peak in allPeaks)
                {
                    if (IsOverlappingPeak(peak, _listPeakSets, iRemove))
                        _listPeakSets.Add(new ChromDataPeakList(peak, _listChromData));
                }

                // Sort by product score
                _listPeakSets.Sort((p1, p2) => Comparer<double>.Default.Compare(p2.ProductArea, p1.ProductArea));

                // Since Crawdad can have a tendency to pick peaks too narrow,
                // use the peak group information to extend the peaks to make
                // them wider.
                // This does not handle reintegration.  Peaks must be reintegrated below.
                _listPeakSets = ExtendPeaks(_listPeakSets);
            }

            /// <summary>
            /// Store the final peaks back on the individual <see cref="ChromDataSet"/> objects
            /// </summary>
            public void StorePeaks()
            {
                // If there are no peaks to store, do nothing.
                if (_listPeakSets.Count == 0)
                    return;

                // Pick the maximum peak by the product score
                ChromDataPeakList peakSetMax = _listPeakSets[0];

                // Sort them back into retention time order
                _listPeakSets.Sort((l1, l2) =>
                    (l1[0].Peak != null ? l1[0].Peak.StartIndex : 0) -
                    (l2[0].Peak != null ? l2[0].Peak.StartIndex : 0));

                // Set the processed peaks back to the chromatogram data
                int maxPeakIndex = _listPeakSets.IndexOf(peakSetMax);
                HashSet<ChromKey> primaryPeakKeys = new HashSet<ChromKey>();
                for (int i = 0, len = _listPeakSets.Count; i < len; i++)
                {
                    var peakSet = _listPeakSets[i];
                    var peakMax = peakSet[0].Peak;

                    // Store the primary peaks that are part of this group.
                    primaryPeakKeys.Clear();
                    foreach (var peak in peakSet)
                    {
                        if (peak.Peak != null)
                            primaryPeakKeys.Add(peak.Data.Key);
                    }

                    foreach (var peak in peakSet)
                    {
                        // Set the max peak index on the data for each transition,
                        // but only the first time through.
                        if (i == 0)
                            peak.Data.MaxPeakIndex = maxPeakIndex;

                        // Reintegrate a new peak based on the max peak
                        ChromPeak.FlagValues flags = 0;
                        // If the entire peak set is a result of forced integration from peptide
                        // peak matching, then flag each peak
                        if (peakSet.IsForcedIntegration)
                            flags |= ChromPeak.FlagValues.forced_integration;
                        else if (peak.Peak == null)
                        {
                            // Mark the peak as forced integration, if it was not part of the original
                            // coeluting set, unless it is optimization data for which the primary peak
                            // was part of the original set
                            if (!peak.Data.IsOptimizationData || !primaryPeakKeys.Contains(peak.Data.PrimaryKey))
                                flags |= ChromPeak.FlagValues.forced_integration;                            
                        }
                        // Use correct time normalization flag (backward compatibility with v0.5)
                        if (_isTimeNormalArea)
                            flags |= ChromPeak.FlagValues.time_normalized;
                        peak.Data.Peaks.Add(peak.CalcChromPeak(peakMax, flags));
                    }
                }
            }

            private static List<ChromDataPeakList> ExtendPeaks(IEnumerable<ChromDataPeakList> listPeakSets)
            {
                var listExtendedSets = new List<ChromDataPeakList>();
                foreach (var peakSet in listPeakSets)
                {
                    peakSet.Extend();
                    if (!PeaksOverlap(peakSet, listExtendedSets))
                        listExtendedSets.Add(peakSet);
                }
                return listExtendedSets;
            }

            private static bool PeaksOverlap(IList<ChromDataPeak> peakSetTest, IEnumerable<ChromDataPeakList> peakSets)
            {
                foreach (var peakSet in peakSets)
                {
                    if (PeaksOverlap(peakSet[0].Peak, peakSetTest[0].Peak))
                    {
                        // Check peaks where their largest peaks overlap to make
                        // sure they have transitions with measured signal in common.
                        var sharedPeaks = from dataPeak in peakSet
                                          join dataPeakTest in peakSetTest on
                                              dataPeak.Data.Key equals dataPeakTest.Data.Key
                                          where dataPeak.Peak != null && dataPeakTest.Peak != null
                                          select dataPeak;
                        return sharedPeaks.Count() > 0;
                    }
                }
                return false;
            }

            private static bool PeaksOverlap(CrawdadPeak peak1, CrawdadPeak peak2)
            {
                // Peaks overlap, if they have intersecting area.
                return Math.Min(peak1.EndIndex, peak2.EndIndex) -
                       Math.Max(peak1.StartIndex, peak2.StartIndex) > 0;
            }

            private IList<ChromDataPeak> MergePeaks()
            {
                List<ChromDataPeak> allPeaks = new List<ChromDataPeak>();
                var listEnumerators = _listChromData.ConvertAll(item => item.RawPeaks.GetEnumerator());
                // Merge with list of chrom data that will match the enumerators
                // list, as completed enumerators are removed.
                var listUnmerged = new List<ChromData>(_listChromData);
                // Initialize an enumerator for each set of raw peaks, or remove
                // the set, if the list is found to be empty
                for (int i = listEnumerators.Count - 1; i >= 0; i--)
                {
                    if (!listEnumerators[i].MoveNext())
                    {
                        listEnumerators.RemoveAt(i);
                        listUnmerged.RemoveAt(i);
                    }
                }

                while (listEnumerators.Count > 0)
                {
                    float maxIntensity = 0;
                    int iMaxEnumerator = -1;

                    for (int i = 0; i < listEnumerators.Count; i++)
                    {
                        float intensity = listEnumerators[i].Current.Area;
                        if (intensity > maxIntensity)
                        {
                            maxIntensity = intensity;
                            iMaxEnumerator = i;
                        }
                    }

                    // If only zero area peaks left, stop looping.
                    if (iMaxEnumerator == -1)
                        break;

                    var maxData = listUnmerged[iMaxEnumerator];
                    var maxEnumerator = listEnumerators[iMaxEnumerator];
                    var maxPeak = maxEnumerator.Current;
                    Debug.Assert(maxPeak != null);
                    // Discard peaks that occur at the edge of their range.
                    // These are not useful in SRM.
                    // TODO: Fix Crawdad peak detection to make this unnecessary
                    if (maxPeak.StartIndex != maxPeak.TimeIndex && maxPeak.EndIndex != maxPeak.TimeIndex)
                        allPeaks.Add(new ChromDataPeak(maxData, maxPeak));
                    if (!maxEnumerator.MoveNext())
                    {
                        listEnumerators.RemoveAt(iMaxEnumerator);
                        listUnmerged.RemoveAt(iMaxEnumerator);
                    }
                }
                return allPeaks;
            }

            private static void RemoveNonOverlappingPeaks(IList<ChromDataPeakList> listPeakSets, int iRemove)
            {
                for (int i = listPeakSets.Count - 1; i >= iRemove; i--)
                {
                    if (!IsOverlappingPeak(listPeakSets[i][0], listPeakSets, iRemove))
                        listPeakSets.RemoveAt(i);
                }
            }

            private static bool IsOverlappingPeak(ChromDataPeak peak,
                                                  IList<ChromDataPeakList> listPeakSets, int count)
            {
                var peak1 = peak.Peak;
                int overlapThreshold = (int)Math.Round((peak1.EndIndex - peak1.StartIndex)/2.0);
                for (int i = 0; i < count; i++)
                {
                    var peak2 = listPeakSets[i][0].Peak;
                    if (Math.Min(peak1.EndIndex, peak2.EndIndex) - Math.Max(peak1.StartIndex, peak2.StartIndex) >= overlapThreshold)
                        return true;
                }
                return false;
            }

            private void MarkOptimizationData()
            {
                int iFirst = 0;
                for (int i = 0; i < _listChromData.Count; i++)
                {
                    if (i < _listChromData.Count - 1 &&
                        ChromatogramInfo.IsOptimizationSpacing(_listChromData[i].Key.Product, _listChromData[i+1].Key.Product))
                    {
                        if (_listChromData[i + 1].Key.Product < _listChromData[i].Key.Product)
                        {
                            throw new InvalidDataException(String.Format("Incorrectly sorted chromatograms {0} > {1}",
                                                                         _listChromData[i + 1].Key.Product, _listChromData[i].Key.Product));
                        }
                    }
                    else
                    {
                        if (iFirst != i)
                        {
                            // The middle element in the run is the regression value.
                            // Mark it as not optimization data.
                            var primaryData = _listChromData[(i - iFirst) / 2 + iFirst];
                            // Set the primary key for all members of this group.
                            for (int j = iFirst; j <= i; j++)
                            {
                                _listChromData[j].IsOptimizationData = true;
                                _listChromData[j].PrimaryKey = primaryData.Key;
                            }
                            primaryData.IsOptimizationData = false;
                        }
                        // Start a new run with the next value
                        iFirst = i + 1;
                    }
                }
            }

            // Moved to ProteoWizard
            public bool WiffZerosFix()
            {
                if (!HasFlankingZeros)
                    return false;

                // Remove flagging zeros
                foreach (var chromData in _listChromData)
                {
                    var times = chromData.Times;
                    var intensities = chromData.Intensities;
                    int start = 0;
                    while (start < intensities.Length - 1 && intensities[start] == 0)
                        start++;
                    int end = intensities.Length;
                    while (end > 0 && intensities[end - 1] == 0)
                        end--;

                    // Leave at least one bounding zero
                    if (start > 0)
                        start--;
                    if (end < intensities.Length)
                        end++;

                    var timesNew = new float[end - start];
                    var intensitiesNew = new float[end - start];
                    Array.Copy(times, start, timesNew, 0, timesNew.Length);
                    Array.Copy(intensities, start, intensitiesNew, 0, intensitiesNew.Length);
                    chromData.FixChromatogram(timesNew, intensitiesNew);
                }
                return true;
            }

            private bool HasFlankingZeros
            {
                get
                {
                    // Check for case where all chromatograms have at least
                    // 10 zero intensity entries on either side of the real data.
                    foreach (var chromData in _listChromData)
                    {
                        var intensities = chromData.Intensities;
                        if (intensities.Length < 10)
                            return false;
                        for (int i = 0; i < 10; i++)
                        {
                            if (intensities[i] != 0)
                                return false;
                        }
                        for (int i = intensities.Length - 1; i < 10; i++)
                        {
                            if (intensities[i] != 0)
                                return false;
                        }
                    }
                    return true;
                }
            }

            public bool ThermoZerosFix()
            {
                // Check for interleaving zeros
                if (!HasThermZerosBug)
                    return false;
                // Remove interleaving zeros
                foreach (var chromData in _listChromData)
                {
                    var times = chromData.Times;
                    var intensities = chromData.Intensities;
                    var timesNew = new float[intensities.Length / 2];
                    var intensitiesNew = new float[intensities.Length / 2];
                    for (int i = (intensities.Length > 0 && intensities[0] == 0 ? 1 : 0), iNew = 0; iNew < timesNew.Length; i += 2, iNew++)
                    {
                        timesNew[iNew] = times[i];
                        intensitiesNew[iNew] = intensities[i];
                    }
                    chromData.FixChromatogram(timesNew, intensitiesNew);
                }
                return true;
            }

            private bool HasThermZerosBug
            {
                get
                {
                    // Make sure the intensity arrays are not just empty to avoid
                    // an infinite loop.
                    bool seenData = false;
                    // Check for interleaving zeros and non-zero values
                    foreach (var chromData in _listChromData)
                    {
                        var intensities = chromData.Intensities;
                        for (int i = (intensities.Length > 0 && intensities[0] == 0 ? 0 : 1); i < intensities.Length; i += 2)
                        {
                            if (intensities[i] != 0)
                                return false;
                            // Because WIFF files have lots of zeros
                            if (i < intensities.Length - 1 && intensities[i + 1] == 0)
                                return false;
                            seenData = true;
                        }
                    }
                    return seenData;
                }
            }

            private ChromDataPeakList FindCoelutingPeaks(ChromDataPeak dataPeakMax,
                                                         IList<ChromDataPeak> allPeaks)
            {
                CrawdadPeak peakMax = dataPeakMax.Peak;
                float areaMax = peakMax.Area;
                int centerMax = peakMax.TimeIndex;
                int startMax = peakMax.StartIndex;
                int endMax = peakMax.EndIndex;
                int widthMax = peakMax.Length;
                int deltaMax = (int)Math.Round(widthMax / 4.0, 0);
                var listPeaks = new ChromDataPeakList(dataPeakMax);
                foreach (var chromData in _listChromData)
                {
                    if (ReferenceEquals(chromData, dataPeakMax.Data))
                        continue;

                    int iPeakNearest = -1;
                    int deltaNearest = deltaMax;

                    // Find nearest peak in remaining set that is less than 1/4 length
                    // from the primary peak's center
                    for (int i = 0, len = allPeaks.Count; i < len; i++)
                    {
                        var peak = allPeaks[i];
                        if (!ReferenceEquals(peak.Data, chromData))
                            continue;

                        // Exclude peaks where the apex is not inside the max peak,
                        // or apex is at one end of the peak
                        int timeIndex = peak.Peak.TimeIndex;
                        int startPeak = peak.Peak.StartIndex;
                        int endPeak = peak.Peak.EndIndex;
                        if (startMax >= timeIndex || timeIndex >= endMax ||
                            startPeak == timeIndex || timeIndex == endPeak)
                            continue;
                        // or peak area is less than 1% of max peak area
                        if (peak.Peak.Area * 100 < areaMax)
                            continue;
                        // or when FWHM is very narrow, usually a good indicator of noise
                        if (/* peak.Peak.Fwhm < 1.2 too agressive || */ peak.Peak.Fwhm * 12 < widthMax)
                            continue;
                        // or where the peak does not overlap at least 50% of the max peak
                        int intersect = Math.Min(endMax, peak.Peak.EndIndex) -
                                        Math.Max(startMax, peak.Peak.StartIndex) + 1;   // +1 for inclusive end
                        int lenPeak = peak.Peak.Length;
                        // Allow 25% coverage, if the peak is entirely inside the max, since
                        // sometimes Crawdad breaks smaller peaks up.
                        int factor = (intersect == lenPeak ? 4 : 2);
                        if (intersect * factor < widthMax)
                            continue;
                        int delta = Math.Abs(timeIndex - centerMax);
                        // If apex delta and FWHM are not very close to the max peak, make further checks
                        if (delta * 4.0 > deltaMax || Math.Abs(peak.Peak.Fwhm - peakMax.Fwhm)/peakMax.Fwhm > 0.05)
                        {
                            // If less than 2/3 of the peak is inside the max peak, or 1/2 if the
                            // peak entirely contains the max peak.
                            double dFactor = (intersect == widthMax ? 2.0 : 1.5);
                            if (intersect * dFactor < lenPeak)
                                continue;
                            // or where either end is more than 2/3 of the intersect width outside
                            // the max peak.
                            if (intersect != lenPeak)
                            {
                                dFactor = 1.5;
                                if ((startMax - peak.Peak.StartIndex) * dFactor > intersect ||
                                    (peak.Peak.EndIndex - endMax) * dFactor > intersect)
                                    continue;
                            }
                        }

                        if (delta <= deltaNearest)
                        {
                            deltaNearest = delta;
                            iPeakNearest = i;
                        }
                    }

                    if (iPeakNearest == -1)
                        listPeaks.Add(new ChromDataPeak(chromData, null));
                    else
                    {
                        listPeaks.Add(new ChromDataPeak(chromData, allPeaks[iPeakNearest].Peak));
                        allPeaks.RemoveAt(iPeakNearest);
                    }
                }
                return listPeaks;
            }

            public void SetBestPeak(ChromDataPeakList peakSet, PeptideChromDataPeak bestPeptidePeak)
            {
                if (peakSet != null)
                {
                    // If the best peak by peptide matching is not already at the
                    // head of the list, then move it there
                    if (peakSet != _listPeakSets[0])
                    {
                        _listPeakSets.Remove(peakSet);
                        _listPeakSets.Insert(0, peakSet);
                    }
                    // If there is a different best peptide peak, and it should have
                    // the same retention time charachteristics, then reset the integration
                    // boundaries of this peak set
                    if (bestPeptidePeak != null && IsSameRT(bestPeptidePeak.Data))
                    {
                        var peak = peakSet[0].Peak;
                        var peakBest = bestPeptidePeak.PeakGroup[0].Peak;
                        int offsetBest = bestPeptidePeak.Data.Offset;
                        peak.StartIndex = Math.Max(0, GetIndex(peakBest.StartIndex + offsetBest));
                        peak.EndIndex = Math.Min(Times.Length - 1, GetIndex(peakBest.EndIndex + offsetBest));

                        Debug.Assert(peak.StartIndex < peak.EndIndex);
                    }
                }
                // If no peak was found at the peptide level for this data set,
                // but there is a best peak for the peptide
                else if (bestPeptidePeak != null)
                {
                    ChromDataPeak peakAdd = null;

                    // If no overlapping peak was found for this precursor, then create
                    // a peak with the same extents as the best peak.  This peak will
                    // appear as missing, if Integrate All is not selected.
                    var peakBest = bestPeptidePeak.PeakGroup[0].Peak;
                    int offsetBest = bestPeptidePeak.Data.Offset;
                    int startIndex = Math.Max(0, GetIndex(peakBest.StartIndex + offsetBest));
                    int endIndex = Math.Min(Times.Length - 1, GetIndex(peakBest.EndIndex + offsetBest));
                    if (startIndex < endIndex)
                    {
                        var chromData = _listChromData[0];
                        peakAdd = new ChromDataPeak(chromData, chromData.CalcPeak(startIndex, endIndex));
                    }                        

                    // If there is still no peak to add, create an empty one
                    if (peakAdd == null)
                    {
                        peakAdd = new ChromDataPeak(_listChromData[0], null);
                    }

                    _listPeakSets.Insert(0,
                        new ChromDataPeakList(peakAdd, _listChromData) {IsForcedIntegration = true});
                }
            }

            private int GetIndex(int indexPeptide)
            {
                return indexPeptide - Offset;
            }

            private bool IsSameRT(ChromDataSet chromDataSet)
            {
                return DocNode.RelativeRT == RelativeRT.Matching &&
                    chromDataSet.DocNode.RelativeRT == RelativeRT.Matching;
            }
        }

        private sealed class ChromData
        {
            /// <summary>
            /// Maximum number of peaks to label on a graph
            /// </summary>
            private const int MAX_PEAKS = 20;

            public ChromData(ChromKey key, int providerId)
            {
                Key = PrimaryKey = key;
                ProviderId = providerId;
                Peaks = new List<ChromPeak>();
                MaxPeakIndex = -1;
            }

            public void Load(ChromDataProvider provider)
            {
                float[] times, intensities;
                provider.GetChromatogram(ProviderId, out times, out intensities);
                RawTimes = Times = times;
                RawIntensities = Intensities = intensities;
            }

            public void FindPeaks()
            {
                Finder = new CrawdadPeakFinder();
                Finder.SetChromatogram(Times, Intensities);
                // Don't find peaks for optimization data.  Optimization data will
                // have its peak extents set based on the primary data.
                if (IsOptimizationData)
                    RawPeaks = new CrawdadPeak[0];
                else
                {
                    RawPeaks = Finder.CalcPeaks(MAX_PEAKS);
                    // Calculate smoothing for later use in extendint the Crawdad peaks
                    IntensitiesSmooth = ChromatogramInfo.SavitzkyGolaySmooth(Intensities);
                }
            }

            private CrawdadPeakFinder Finder { get; set; }

            public ChromKey Key { get; private set; }
            private int ProviderId { get; set; }
            public float[] RawTimes { get; private set; }
            private float[] RawIntensities { get; set; }
            public IEnumerable<CrawdadPeak> RawPeaks { get; private set; }

            /// <summary>
            /// Time array shared by all transitions of a precursor, and on the
            /// same scale as all other precursors of a peptide.
            /// </summary>
            public float[] Times { get; private set; }

            /// <summary>
            /// Intensity array linear-interpolated to the shared time scale.
            /// </summary>
            public float[] Intensities { get; private set; }

            /// <summary>
            /// Intensities with Savitzky-Golay smoothing applied.
            /// </summary>
            public float[] IntensitiesSmooth { get; private set; }

            public IList<ChromPeak> Peaks { get; private set; }
            public int MaxPeakIndex { get; set; }
            public bool IsOptimizationData { get; set; }
            public ChromKey PrimaryKey { get; set; }

            public void FixChromatogram(float[] timesNew, float[] intensitiesNew)
            {
                RawTimes = Times = timesNew;
                RawIntensities = Intensities = intensitiesNew;
            }

            public CrawdadPeak CalcPeak(int startIndex, int endIndex)
            {
                return Finder.GetPeak(startIndex, endIndex);
            }

            public ChromPeak CalcChromPeak(CrawdadPeak peakMax, ChromPeak.FlagValues flags)
            {
                // Reintegrate all peaks to the max peak, even the max peak itself, since its boundaries may
                // have been extended from the Crawdad originals.
                if (peakMax == null)
                    return ChromPeak.EMPTY;

                var peak = CalcPeak(peakMax.StartIndex, peakMax.EndIndex);
                return new ChromPeak(peak, flags, Times);
            }

            public void Interpolate(float[] timesNew, double intervalDelta, bool inferZeros)
            {
                var intensNew = new List<float>();
                var timesMeasured = RawTimes;
                var intensMeasured = RawIntensities;

                int iTime = 0;
                double timeLast = timesNew[0];
                double intenLast = (inferZeros || intensMeasured.Length == 0 ? 0 : intensMeasured[0]);
                for (int i = 0; i < timesMeasured.Length && iTime < timesNew.Length; i++)
                {
                    double intenNext;
                    float time = timesMeasured[i];
                    float inten = intensMeasured[i];

                    // Continue enumerating points until one is encountered
                    // that has a greater time value than the point being assigned.
                    while (i < timesMeasured.Length - 1 && time < timesNew[iTime])
                    {
                        i++;
                        time = timesMeasured[i];
                        inten = intensMeasured[i];
                    }

                    if (i >= timesMeasured.Length)
                        break;

                    // If the next measured intensity is more than the new delta
                    // away from the intensity being assigned, then interpolated
                    // the next point toward zero, and set the last intensity to
                    // zero.
                    if (inferZeros && intenLast > 0 && timesNew[iTime] + intervalDelta < time)
                    {
                        intenNext = intenLast + (timesNew[iTime] - timeLast) * (0 - intenLast) / (timesNew[iTime] + intervalDelta - timeLast);
                        intensNew.Add((float)intenNext);
                        timeLast = timesNew[iTime++];
                        intenLast = 0;
                    }

                    if (inferZeros)
                    {
                        // If the last intensity was zero, and the next measured time
                        // is more than a delta away, assign zeros until within a
                        // delta of the measured intensity.
                        while (intenLast == 0 && iTime < timesNew.Length && timesNew[iTime] + intervalDelta < time)
                        {
                            intensNew.Add(0);
                            timeLast = timesNew[iTime++];
                        }
                    }
                    else
                    {
                        // Up to just before the current point, project the line from the
                        // last point to the current point at each interval.
                        while (iTime < timesNew.Length && timesNew[iTime] + intervalDelta < time)
                        {
                            intenNext = intenLast + (timesNew[iTime] - timeLast) * (inten - intenLast) / (time - timeLast);
                            intensNew.Add((float)intenNext);
                            iTime++;
                        }
                    }

                    if (iTime >= timesNew.Length)
                        break;

                    // Interpolate from the last intensity toward the measured
                    // intenisty now within a delta of the point being assigned.
                    if (time == timeLast)
                        intenNext = intenLast;
                    else
                        intenNext = intenLast + (timesNew[iTime] - timeLast) * (inten - intenLast) / (time - timeLast);
                    intensNew.Add((float)intenNext);
                    iTime++;
                    intenLast = inten;
                    timeLast = time;
                }

                // Fill any unassigned intensities with zeros.
                while (intensNew.Count < timesNew.Length)
                    intensNew.Add(0);

                // Reassign times and intensities.
                Times = timesNew;
                Intensities = intensNew.ToArray();
            }
        }

        private sealed class ChromDataPeak
        {
            public ChromDataPeak(ChromData data, CrawdadPeak peak)
            {
                Data = data;
                Peak = peak;
            }

            public ChromData Data { get; private set; }
            public CrawdadPeak Peak { get; private set; }

            public override string ToString()
            {
                return Peak == null ? Data.Key.ToString() :
                    String.Format("{0} - area = {1:F0}, start = {2}, end = {3}",
                        Data.Key, Peak.Area, Peak.StartIndex, Peak.EndIndex);
            }

            public ChromPeak CalcChromPeak(CrawdadPeak peakMax, ChromPeak.FlagValues flags)
            {
                return Data.CalcChromPeak(peakMax, flags);
            }
        }

        private sealed class ChromDataPeakList : Collection<ChromDataPeak>
        {
            public ChromDataPeakList(ChromDataPeak peak)
            {
                Add(peak);
            }

            public ChromDataPeakList(ChromDataPeak peak, IEnumerable<ChromData> listChromData)
                : this(peak)
            {
                foreach (var chromData in listChromData)
                {
                    if (!ReferenceEquals(chromData, peak.Data))
                        Add(new ChromDataPeak(chromData, null));
                }
            }

            /// <summary>
            /// True if this set of peaks was created to satisfy forced integration
            /// rules.
            /// </summary>
            public bool IsForcedIntegration { get; set; }

            private int PeakCount { get; set; }
            public double TotalArea { get; private set; }
            public double ProductArea { get; private set; }

            private const int MIN_TOLERANCE_LEN = 4;
            private const int MIN_TOLERANCE_SMOOTH_FWHM = 3;
            private const float FRACTION_FWHM_LEN = 0.5F;
            private const float DESCENT_TOL = 0.005f;
            private const float ASCENT_TOL = 0.50f;

            public void Extend()
            {
                // Only extend for peak groups with multiple peaks
                if (Count < 2)
                    return;

                var peakPrimary = this[0];

                // Look a number of steps dependent on the width of the peak, since interval width
                // may vary.
                int toleranceLen = Math.Max(MIN_TOLERANCE_LEN, (int)Math.Round(peakPrimary.Peak.Fwhm * FRACTION_FWHM_LEN));

                peakPrimary.Peak.StartIndex = ExtendBoundary(peakPrimary, peakPrimary.Peak.StartIndex, -1, toleranceLen);
                peakPrimary.Peak.EndIndex = ExtendBoundary(peakPrimary, peakPrimary.Peak.EndIndex, 1, toleranceLen);
            }

            private int ExtendBoundary(ChromDataPeak peakPrimary, int indexBoundary, int increment, int toleranceLen)
            {
                if (peakPrimary.Peak.Fwhm >= MIN_TOLERANCE_SMOOTH_FWHM)
                {
                    indexBoundary = ExtendBoundary(peakPrimary, false, indexBoundary, increment, toleranceLen);
                }
                // Because smoothed data can have a tendency to reach baseline one
                // interval sooner than the raw data, do a final check to choose the
                // boundary correctly for the raw data.
                indexBoundary = RetractBoundary(peakPrimary, true, indexBoundary, -increment);
                indexBoundary = ExtendBoundary(peakPrimary, true, indexBoundary, increment, toleranceLen);
                return indexBoundary;
            }

            private int ExtendBoundary(ChromDataPeak peakPrimary, bool useRaw, int indexBoundary, int increment, int toleranceLen)
            {
                float maxIntensity, deltaIntensity;
                GetIntensityMetrics(indexBoundary, useRaw, out maxIntensity, out deltaIntensity);

                int lenIntensities = peakPrimary.Data.Intensities.Length;
                // Look for a descent proportional to the height of the peak.  Because, SRM data is
                // so low noise, just looking for any descent can lead to boundaries very far away from
                // the peak.
                float height = peakPrimary.Peak.Height;
                double minDescent = height * DESCENT_TOL;
                // Put a limit on how high intensity can go before the search is terminated
                double maxHeight = ((height - maxIntensity) * ASCENT_TOL) + maxIntensity;

                // Extend the index in the direction of the increment
                for (int i = indexBoundary + increment;
                     i > 0 && i < lenIntensities - 1 && Math.Abs(indexBoundary - i) < toleranceLen;
                     i += increment)
                {
                    float maxIntensityCurrent, deltaIntensityCurrent;
                    GetIntensityMetrics(i, useRaw, out maxIntensityCurrent, out deltaIntensityCurrent);

                    // If intensity goes above the maximum, stop looking
                    if (maxIntensityCurrent > maxHeight)
                        break;

                    // If descent greater than tolerance, step until it no longer is
                    while (maxIntensity - maxIntensityCurrent > minDescent)
                    {
                        indexBoundary += increment;
                        if (indexBoundary == i)
                            maxIntensity = maxIntensityCurrent;
                        else
                            GetIntensityMetrics(indexBoundary, useRaw, out maxIntensity, out deltaIntensity);
                    }
                }

                return indexBoundary;
            }

            private int RetractBoundary(ChromDataPeak peakPrimary, bool useRaw, int indexBoundary, int increment)
            {
                float maxIntensity, deltaIntensity;
                GetIntensityMetrics(indexBoundary, useRaw, out maxIntensity, out deltaIntensity);

                int lenIntensities = peakPrimary.Data.Intensities.Length;
                // Look for a descent proportional to the height of the peak.  Because, SRM data is
                // so low noise, just looking for any descent can lead to boundaries very far away from
                // the peak.
                float height = peakPrimary.Peak.Height;
                double maxAscent = height * DESCENT_TOL;
                // Put a limit on how high intensity can go before the search is terminated
                double maxHeight = ((height - maxIntensity) * ASCENT_TOL) + maxIntensity;

                // Extend the index in the direction of the increment
                for (int i = indexBoundary + increment; i > 0 && i < lenIntensities - 1; i += increment)
                {
                    float maxIntensityCurrent, deltaIntensityCurrent;
                    GetIntensityMetrics(i, useRaw, out maxIntensityCurrent, out deltaIntensityCurrent);

                    // If intensity goes above the maximum, stop looking
                    if (maxIntensityCurrent > maxHeight || maxIntensityCurrent - maxIntensity > maxAscent)
                        break;

                    maxIntensity = maxIntensityCurrent;
                    indexBoundary = i;
                }

                return indexBoundary;
            }

            private void GetIntensityMetrics(int i, bool useRaw, out float maxIntensity, out float deltaIntensity)
            {
                var peakData = this[0];
                var intensities = (useRaw ? peakData.Data.Intensities
                                          : peakData.Data.IntensitiesSmooth);
                float minIntensity = maxIntensity = intensities[i];
                for (int j = 1; j < Count; j++)
                {
                    peakData = this[j];
                    // If this transition doesn't have a measured peak, then skip it.
                    if (peakData.Peak == null)
                        continue;

                    float currentIntensity = (useRaw ? peakData.Data.Intensities[i]
                                                     : peakData.Data.IntensitiesSmooth[i]);
                    if (currentIntensity > maxIntensity)
                        maxIntensity = currentIntensity;
                    else if (currentIntensity < minIntensity)
                        minIntensity = currentIntensity;
                }
                deltaIntensity = maxIntensity - minIntensity;
            }

            private void AddPeak(ChromDataPeak dataPeak)
            {
                // Avoid using optimization data in scoring
                if (dataPeak.Peak != null && !dataPeak.Data.IsOptimizationData)
                {
                    double area = dataPeak.Peak.Area;
                    if (PeakCount == 0)
                        TotalArea = area;
                    else
                        TotalArea += area;
                    PeakCount++;

                    ProductArea = TotalArea*Math.Pow(10.0, PeakCount);
                }
            }

            private void SubtractPeak(ChromDataPeak dataPeak)
            {
                // Avoid using optimization data in scoring
                if (dataPeak.Peak != null && !dataPeak.Data.IsOptimizationData)
                {
                    double area = dataPeak.Peak.Area;
                    PeakCount--;
                    if (PeakCount == 0)
                        TotalArea = 0;
                    else
                        TotalArea -= area;

                    ProductArea = TotalArea * Math.Pow(10.0, PeakCount);
                }
            }

            protected override void ClearItems()
            {
                PeakCount = 0;
                TotalArea = 0;
                ProductArea = 0;

                base.ClearItems();
            }

            protected override void InsertItem(int index, ChromDataPeak item)
            {
                AddPeak(item);
                base.InsertItem(index, item);
            }

            protected override void RemoveItem(int index)
            {
                SubtractPeak(this[index]);
                base.RemoveItem(index);
            }

            protected override void SetItem(int index, ChromDataPeak item)
            {
                SubtractPeak(this[index]);
                AddPeak(item);
                base.SetItem(index, item);
            }
        }
    }
}