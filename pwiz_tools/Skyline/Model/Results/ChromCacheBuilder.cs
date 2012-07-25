/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009-2010 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model.Results
{
    internal sealed class FileBuildInfo
    {
        public FileBuildInfo(MsDataFileImpl file)
        {
            StartTime = file.RunStartTime;
            InstrumentInfoList = file.GetInstrumentConfigInfoList();
        }

        public DateTime? StartTime { get; private set; }
        public IEnumerable<MsInstrumentConfigInfo> InstrumentInfoList { get; private set; }
    }

    internal sealed class ChromCacheBuilder : ChromCacheWriter
    {
        // Lock on this to access these variables
        private readonly SrmDocument _document;
        private LibraryRetentionTimes _libraryRetentionTimes;
        private int _currentFileIndex = -1;
        private FileBuildInfo _currentFileInfo;
        private string _tempFileSubsitute;

        // Lock on _chromDataSets to access these variables
        private readonly List<PeptideChromDataSets> _chromDataSets = new List<PeptideChromDataSets>();
        private bool _writerStarted;
        private bool _readCompleted;
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

        public override void Dispose()
        {
            RemoveTempFile();

            base.Dispose();
        }

        private void RemoveTempFile()
        {
            if (_tempFileSubsitute != null)
            {
                FileEx.DeleteIfPossible(_tempFileSubsitute);
                _tempFileSubsitute = null;
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
                try
                {
                    BuildNextFileInner();
                }
                finally
                {
                    lock (_chromDataSets)
                    {
                        // Release any writer thread.
                        if (!_writerStarted)
                            Monitor.Pulse(_chromDataSets);
                    }
                }
            }
        }

        private void BuildNextFileInner()
        {
            // If there is a temp file, rewind and retry last file
            if (_tempFileSubsitute != null)
            {
                _listCachedFiles.RemoveAt(--_currentFileIndex);
                if (_outStream != null)
                {
                    try { _loader.StreamManager.Finish(_outStream); }
                    catch (IOException) { }

                    _outStream = null;
                }
            }

            if (_currentFileIndex >= MSDataFilePaths.Count)
            {
                ExitRead(null);
                return;
            }

            // Check for cancellation on every chromatogram, because there
            // have been some files that load VERY slowly, and appear to hang
            // on a single file.
            if (_loader.IsCanceled)
            {
                _loader.UpdateProgress(_status = _status.Cancel());
                ExitRead(null);
                return;
            }

            // If not cancelled, update progress.
            string dataFilePath = MSDataFilePaths[_currentFileIndex];

            if (_tempFileSubsitute == null)
            {
                string message = String.Format("Importing {0}", dataFilePath);
                int percent = _currentFileIndex * 100 / MSDataFilePaths.Count;
                _status = _status.ChangeMessage(message).ChangePercentComplete(percent);
                _loader.UpdateProgress(_status);
            }

            try
            {
                string dataFilePathPart;
                dataFilePath = ChromatogramSet.GetExistingDataFilePath(CachePath, dataFilePath, out dataFilePathPart);                        
                if (dataFilePath == null)
                    throw new FileNotFoundException(String.Format("The file {0} does not exist.", dataFilePathPart), dataFilePathPart);
                MSDataFilePaths[_currentFileIndex] = dataFilePath;

                if (_tempFileSubsitute != null)
                    dataFilePath = dataFilePathPart = _tempFileSubsitute;

                // HACK: Force the thread that the writer will use into existence
                // This allowed teh DACServer Reader_Waters to function normally the first time through.
                // It is no longer necessary for the MassLynxRaw version of Reader_Waters,
                // but is kept to avoid destabilizing code changes.
                //
                // This does not actually start the loop, but calling the function once,
                // seems to reserve a thread somehow, so that the next call works.
                Action<int, bool> writer = WriteLoop;
                writer.BeginInvoke(_currentFileIndex, true, null, null);

                // Read the instrument data indexes
                int sampleIndex = SampleHelp.GetPathSampleIndexPart(dataFilePath);
                if (sampleIndex == -1)
                    sampleIndex = 0;

                // Once a ChromDataProvider is created, it owns disposing of the MSDataFileImpl.
                MsDataFileImpl inFile = null;
                ChromDataProvider provider = null;
                try
                {
                    var enableSimSpectrum = _document.Settings.TransitionSettings.FullScan.IsEnabledMs;
                    inFile = new MsDataFileImpl(dataFilePathPart, sampleIndex, enableSimSpectrum);

                    // Check for cancelation);
                    if (_loader.IsCanceled)
                    {
                        _loader.UpdateProgress(_status = _status.Cancel());
                        ExitRead(null);
                        return;
                    }
                    if (_outStream == null)
                        _outStream = _loader.StreamManager.CreateStream(_fs.SafeName, FileMode.Create, true);

                    _currentFileInfo = new FileBuildInfo(inFile);
                    _libraryRetentionTimes = null;
                    // Read and write the mass spec data)
                    if (ChromatogramDataProvider.HasChromatogramData(inFile))
                        provider = CreateChromatogramProvider(inFile, _tempFileSubsitute == null);
                    else if (SpectraChromDataProvider.HasSpectrumData(inFile))
                    {                            
                        if (_document.Settings.TransitionSettings.FullScan.IsEnabled)
                        {
                            if (_libraryRetentionTimes == null)
                            {
                                _libraryRetentionTimes = _document.Settings.GetRetentionTimes(dataFilePathPart);
                            }
                        }

                        provider = CreateSpectraChromProvider(inFile, _document);
                    }
                    else
                    {
                        throw new InvalidDataException(String.Format("The sample {0} contains no usable data.",
                                                                        SampleHelp.GetFileSampleName(dataFilePath)));
                    }

                    Read(provider);

                    _status = provider.Status;

                    if (_status.IsCanceled)
                        ExitRead(null);

                    RemoveTempFile();
                }
                catch (LoadingTooSlowlyException x)
                {
                    _status = x.Status;
                    _tempFileSubsitute = VendorIssueHelper.CreateTempFileSubstitute(dataFilePathPart,
                        sampleIndex, x, _loader, ref _status);
                    // Trigger next call to BuildNextFile from the write thread
                    PostChromDataSet(null, true);
                }
                finally
                {
                    if (provider != null)
                        provider.Dispose();
                    else if (inFile != null)
                        inFile.Dispose();
                }
            }
            catch (LoadCanceledException x)
            {
                _status = x.Status;
                ExitRead(null);
            }
            catch (NoSrmDataException)
            {
                ExitRead(new InvalidDataException(String.Format("No SRM/MRM data found in {0}.",
                    SampleHelp.GetFileSampleName(MSDataFilePaths[_currentFileIndex]))));
            }
            catch (Exception x)
            {
                // Add a more generic message to an exception message that may
                // be fairly unintelligible to the user, but keep the exception
                // message, because ProteoWizard "Unsupported file format" comes
                // in on this channel.
                ExitRead(x);
            }
        }

        private void ExitRead(Exception x)
        {
            Complete(x);
            lock (_chromDataSets)
            {
                _writerStarted = false;
            }
        }

        private void Read(ChromDataProvider provider)
        {
            lock (_chromDataSets)
            {
                _readCompleted = false;
            }

            var dictPeptideChromData = new Dictionary<int, PeptideChromDataSets>();
            var listChromData = new List<PeptideChromDataSets>();

            var listMzPrecursors = new List<PeptidePrecursorMz>(Precursors);
            listMzPrecursors.Sort((p1, p2) => p1.PrecursorMz.CompareTo(p2.PrecursorMz));

            bool singleMatch = provider.IsSingleMzMatch;

            foreach (var chromDataSet in GetChromDataSets(provider))
            {
                foreach (var matchingGroup in GetMatchingGroups(chromDataSet, listMzPrecursors, singleMatch))
                {
                    AddChromDataSet(provider.IsProcessedScans,
                                    matchingGroup.Value,
                                    matchingGroup.Key,
                                    dictPeptideChromData,
                                    listChromData);
                }
            }

            listChromData.AddRange(dictPeptideChromData.Values);
            listChromData.Sort((p1, p2) =>
                Comparer.Default.Compare(p1.DataSets[0].PrecursorMz, p2.DataSets[0].PrecursorMz));

            // Avoid holding onto chromatogram data sets for entire read
            dictPeptideChromData.Clear();

            for (int i = 0; i < listChromData.Count; i++)
            {
                var pepChromData = listChromData[i];
                pepChromData.Load(provider);
                PostChromDataSet(pepChromData, false);

                // Release the reference to the chromatogram data set so that
                // it can be garbage collected after it has been written
                listChromData[i] = null;
            }
            // Release all provider memory before waiting for write completion
            provider.ReleaseMemory();
            PostChromDataSet(null, true);
        }

        private sealed class PeptidePrecursorMz
        {
            public PeptidePrecursorMz(PeptideDocNode nodePeptide,
                                      TransitionGroupDocNode nodeGroup,
                                      double precursorMz)
            {
                NodePeptide = nodePeptide;
                NodeGroup = nodeGroup;
                PrecursorMz = precursorMz;
            }

            public PeptideDocNode NodePeptide { get; private set; }
            public TransitionGroupDocNode NodeGroup { get; private set; }
            public double PrecursorMz { get; private set; }
        }

        private IEnumerable<PeptidePrecursorMz> Precursors
        {
            get
            {
                return from nodePep in _document.Peptides
                       from nodeGroup in nodePep.Children.Cast<TransitionGroupDocNode>()
                       select new PeptidePrecursorMz(nodePep, nodeGroup, nodeGroup.PrecursorMz);
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

        private void AddChromDataSet(bool isProcessedScans,
                                            ChromDataSet chromDataSet,
                                            PeptidePrecursorMz peptidePrecursorMz,
                                            IDictionary<int, PeptideChromDataSets> dictPeptideChromData,
                                            ICollection<PeptideChromDataSets> listChromData)
        {
            // If there was no matching precursor, just add this as a stand-alone set
            if (peptidePrecursorMz == null)
            {
                listChromData.Add(new PeptideChromDataSets(new double[0], isProcessedScans, chromDataSet));
                return;
            }

            // Otherwise, add it to the dictionary by its peptide GlobalIndex to make
            // sure precursors are grouped by peptide
            var nodePep = peptidePrecursorMz.NodePeptide;
            int id = nodePep.Peptide.GlobalIndex;
            PeptideChromDataSets pepDataSets;
            if (!dictPeptideChromData.TryGetValue(id, out pepDataSets))
            {
                double[] retentionTimes = _document.Settings.GetRetentionTimes(SampleHelp.GetPathFilePart(MSDataFilePaths[_currentFileIndex]),
                        nodePep.Peptide.Sequence, nodePep.ExplicitMods);

                pepDataSets = new PeptideChromDataSets(retentionTimes, isProcessedScans);
                dictPeptideChromData.Add(id, pepDataSets);
            }
            chromDataSet.DocNode = peptidePrecursorMz.NodeGroup;
            pepDataSets.DataSets.Add(chromDataSet);
        }

        private static IEnumerable<KeyValuePair<PeptidePrecursorMz, ChromDataSet>> GetMatchingGroups(
            ChromDataSet chromDataSet, List<PeptidePrecursorMz> listMzPrecursors, bool singleMatch)
        {
            // Find the first precursor m/z that is greater than or equal to the
            // minimum possible match value
            double minMzMatch = chromDataSet.PrecursorMz - TransitionInstrument.MAX_MZ_MATCH_TOLERANCE;
            double maxMzMatch = chromDataSet.PrecursorMz + TransitionInstrument.MAX_MZ_MATCH_TOLERANCE;
            var lookup = new PeptidePrecursorMz(null, null, minMzMatch);
            int i = listMzPrecursors.BinarySearch(lookup, MZ_COMPARER);
            if (i < 0)
                i = ~i;
            // Enumerate all possible matching precursor values, collecting the ones
            // with potentially matching product ions
            var listMatchingGroups = new List<KeyValuePair<PeptidePrecursorMz, IList<ChromData>>>();
            for (; i < listMzPrecursors.Count && listMzPrecursors[i].PrecursorMz <= maxMzMatch; i++)
            {
                var peptidePrecursorMz = listMzPrecursors[i];
                var nodeGroup = peptidePrecursorMz.NodeGroup;
                var groupData = GetMatchingData(nodeGroup, chromDataSet);
                if (groupData != null)
                    listMatchingGroups.Add(new KeyValuePair<PeptidePrecursorMz, IList<ChromData>>(peptidePrecursorMz, groupData));
            }

            // Only use this method of finding potential matching groups, if multiple
            // matches are allowed.  Otherwise, it may discard the right match.
            if (!singleMatch)
                FilterMatchingGroups(listMatchingGroups);

            if (listMatchingGroups.Count == 0)
            {
                // No matches found
                yield return new KeyValuePair<PeptidePrecursorMz, ChromDataSet>(
                    null, chromDataSet);                
            }
            else if (listMatchingGroups.Count == 1)
            {
                // If only one match is found, return product ions for the precursor, whether they
                // all match or not.
                yield return new KeyValuePair<PeptidePrecursorMz, ChromDataSet>(
                    listMatchingGroups[0].Key, chromDataSet);
            }
            else
            {
                // Otherwise, split up the product ions among the precursors they matched
                bool isTimeNormalArea = chromDataSet.IsTimeNormalArea;

                // If this is single matching, as in full-scan filtering, return only nodes
                // matching a single precursor m/z value.  The one closest to the data.
                float? bestMz = null;
                if (singleMatch)
                {
                    float matchMz = chromDataSet.PrecursorMz;
                    foreach (var match in listMatchingGroups)
                    {
                        float currentMz = (float) match.Key.PrecursorMz;
                        if (!bestMz.HasValue || Math.Abs(matchMz - currentMz) < Math.Abs(matchMz - bestMz.Value))
                            bestMz = currentMz;
                    }
                }

                // Make sure the same chrom data object is not added twice, or two threads
                // may end up processing it at the same time.
                var setChromData = new HashSet<ChromData>();
                foreach (var match in listMatchingGroups.Where(match =>
                    !bestMz.HasValue || bestMz.Value == (float) match.Key.PrecursorMz))
                {
                    var arrayChromData = match.Value.ToArray();
                    for (int j = 0; j < arrayChromData.Length; j++)
                    {
                        var chromData = arrayChromData[j];
                        if (setChromData.Contains(chromData))
                            arrayChromData[j] = chromData.CloneForWrite();
                        setChromData.Add(chromData);
                    }
                    var chromDataPart = new ChromDataSet(isTimeNormalArea, arrayChromData);
                    yield return new KeyValuePair<PeptidePrecursorMz, ChromDataSet>(
                        match.Key, chromDataPart);
                }
            }
        }

        private static void FilterMatchingGroups(
                List<KeyValuePair<PeptidePrecursorMz, IList<ChromData>>> listMatchingGroups)
        {
            if (listMatchingGroups.Count < 2)
                return;
            // Filter for only matches that do not match a strict subset of another match.
            // That is, if there is a precursor that matches 4 product ions, and another that
            // matches 2 of those same 4, then we want to discard the one with only 2.
            var listFiltered = new List<KeyValuePair<PeptidePrecursorMz, IList<ChromData>>>();
            foreach (var match in listMatchingGroups)
            {
                var subset = match;
                if (!listMatchingGroups.Contains(superset => IsMatchSubSet(subset, superset)))
                    listFiltered.Add(match);
            }
            listMatchingGroups.Clear();
            listMatchingGroups.AddRange(listFiltered);
        }

        private static bool IsMatchSubSet(KeyValuePair<PeptidePrecursorMz, IList<ChromData>> subset,
            KeyValuePair<PeptidePrecursorMz, IList<ChromData>> superset)
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
            // Only return a match, if at least two product ions match, or the precursor
            // has only a single product ion, and it matches
            int countChildren = nodeGroup.Children.Count;
            if (countChildren == 0 || listMatchingData.Count < Math.Min(2, countChildren))
                return null;
            return listMatchingData;
        }

        private static readonly MzComparer MZ_COMPARER = new MzComparer();

        private class MzComparer : IComparer<PeptidePrecursorMz>
        {
            public int Compare(PeptidePrecursorMz p1, PeptidePrecursorMz p2)
            {
                return Comparer.Default.Compare(p1.PrecursorMz, p2.PrecursorMz);
            }
        }

        private int StartPercent { get { return _currentFileIndex*100/MSDataFilePaths.Count; } }
        private int EndPercent { get { return (_currentFileIndex + 1)*100/MSDataFilePaths.Count; } }

        private ChromDataProvider CreateChromatogramProvider(MsDataFileImpl dataFile, bool throwIfSlow)
        {
            return new ChromatogramDataProvider(dataFile, throwIfSlow, _status, StartPercent, EndPercent, _loader);
        }

        private SpectraChromDataProvider CreateSpectraChromProvider(MsDataFileImpl dataFile, SrmDocument document)
        {
            // New WIFF reader library no longer needs this, and mzWiff.exe has been removed from the installation
            // The old WiffFileDataReader messed up the precursor m/z values for targeted
            // spectra.  The mzWiff mzXML converter must be used instead.
//            if (dataFile.IsABFile && !dataFile.IsMzWiffXml)
//            {
                // This will show an error about the import taking 10 hours, which is not really true, if the computer running Skyline does not have Analyst installed
//                throw new LoadingTooSlowlyException(LoadingTooSlowlyException.Solution.mzwiff_conversion, _status,
//                    10*60, 4);
//            }
            // If this is a performance work-around, then make sure the progress indicator
            // does not jump backward perceptibly.
            int startPercent = (_tempFileSubsitute != null ? (StartPercent + EndPercent)/2 : StartPercent);
                
            return new SpectraChromDataProvider(dataFile, document, _status, startPercent, EndPercent, _loader);
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
                _readCompleted = _readCompleted || complete;
                // Notify the writer thread, if necessary
                if (_readCompleted || _chromDataSets.Count > 0)
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
                    if (_readCompleted)
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
                                // Wait 10 seconds for some work to complete.  In debug mode,
                                // a shorter time may not be enough to load DLLs necessary
                                // for the first iteration.
                                completed = Monitor.Wait(_chromDataSets, 10*1000);
                            }
                            while (!completed && countSets != _chromDataSets.Count);

                            // Try calling the write loop directly on this thread.
                            if (!completed && _chromDataSets.Count > 0)
                                WriteLoop(_currentFileIndex, false);                                
                        }

                        if (_writeException != null)
                            throw _writeException;
                    }
                }
            }
        }

        /// <summary>
        /// List of write threads for debugging write thread leaks.
        /// </summary>
        private static readonly List<Thread> WRITE_THREADS = new List<Thread>() ;

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
                        try
                        {
                            if (WRITE_THREADS.Count > 0 && !_readCompleted)
                                Console.WriteLine("Existing write threads: {0}", string.Join(", ", WRITE_THREADS.Select(t => t.ManagedThreadId)));
                            WRITE_THREADS.Add(Thread.CurrentThread);
                            while (_writerStarted && !_readCompleted && _chromDataSets.Count == 0)
                                Monitor.Wait(_chromDataSets);

                            if (!_writerStarted)
                                return;

                            // If reading is complete, and there are no more sets to process,
                            // begin next file.
                            if (_readCompleted && _chromDataSets.Count == 0)
                            {
                                // Once inside here, the thread is going to exit.
                                _writerStarted = false;

                                // Write loop completion may have already been executed
                                if (_currentFileIndex != currentFileIndex)
                                    return;

                                string dataFilePath = MSDataFilePaths[_currentFileIndex];
                                DateTime fileWriteTime = ChromCachedFile.GetLastWriteTime(dataFilePath);
                                DateTime? runStartTime = _currentFileInfo.StartTime;
                                _listCachedFiles.Add(new ChromCachedFile(dataFilePath, fileWriteTime, runStartTime, _currentFileInfo.InstrumentInfoList));
                                _currentFileIndex++;

                                // Allow the reader thread to exit
                                Monitor.Pulse(_chromDataSets);

                                Action build = BuildNextFile;
                                build.BeginInvoke(null, null);
                                return;
                            }

                            chromDataSetNext = _chromDataSets[0];
                            _chromDataSets.RemoveAt(0);
                        }
                        finally
                        {
                            WRITE_THREADS.Remove(Thread.CurrentThread);
                        }
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
                        byte[] pointsCompressed = points.Compress(3);
                        int lenCompressed = pointsCompressed.Length;
                        _outStream.Write(pointsCompressed, 0, lenCompressed);

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

                        int? transitionPeakCount = null;
                        foreach (var chromData in chromDataSet.Chromatograms)
                        {
                            _listTransitions.Add(new ChromTransition(chromData.Key.Product));

                            // Make sure all transitions have the same number of peaks, as this is a cache requirement
                            if (!transitionPeakCount.HasValue)
                                transitionPeakCount = chromData.Peaks.Count;
                            else if (transitionPeakCount.Value != chromData.Peaks.Count)
                                throw new InvalidDataException(string.Format("Transitions of the same precursor found with different peak counts {0} and {1}", transitionPeakCount, chromData.Peaks.Count));

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
    }
}