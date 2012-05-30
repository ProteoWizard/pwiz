/*
 * Original author: Jarrett Egertson <jegertso .at .u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using pwiz.ProteowizardWrapper;

namespace pwiz.Skyline.Model.Results
{
    public sealed class MsxDemultiplexer
    {
        private readonly MsDataFileImpl _file;
        private readonly SpectrumFilter _filter;
        /// <summary>
        /// maps isolation window -> unique index
        /// </summary>
        private readonly IsolationWindowMapper _isoMapper; 
        private bool _initialized;
        private double _isolationWindowWidth;
        private SpectrumProcessor _spectrumProcessor;
        private readonly List<int> _msMsSpectra;
        private int _msMsIndex;
        private ILsSolver _solver;
        private DeconvBlock _deconvBlock;

        public int IsoWindowsPerScan { get; private set; }
        public int NumIsoWindows { get; private set; }
        public int DutyCycleLength { get; private set; }

        #region Test

        public int DeconvWindowEpsilonTest { get; private set; }

        public void ForceInitializeFile()
        {
            InitializeFile();
        }

        public IsolationWindowMapper IsoMapperTest { get { return _isoMapper; } }
        public SpectrumProcessor SpectrumProcessor
        {
            get { return _spectrumProcessor; }
            set { _spectrumProcessor = value; }
        }
        #endregion Test

        public MsxDemultiplexer(MsDataFileImpl file, SpectrumFilter filter)
        {
            _file = file;
            _filter = filter;
            _msMsSpectra = new List<int>();
            _isoMapper = new IsolationWindowMapper();
        }

        /// <summary>
        /// Lazy initialization
        /// </summary>
        private void InitializeFile()
        {
            if (!AnalyzeFile())
                throw new IOException("MsxDemultiplexer: InitializeFile: Improperly formed MSX file");

            DutyCycleLength = NumIsoWindows / IsoWindowsPerScan;
            DeconvWindowEpsilonTest = (int)Math.Ceiling((IsoWindowsPerScan * DutyCycleLength + 2 * DutyCycleLength) / 2.0);
            _isoMapper.WindowWidth = _isolationWindowWidth;
            InitializeProcessor();
            _initialized = true;

            var maxTransInSpectrum = _spectrumProcessor.MaxTransitions(IsoWindowsPerScan);
            var maxRowsInBlock = DeconvWindowEpsilonTest * 2;
            _deconvBlock = new DeconvBlock(NumIsoWindows, maxRowsInBlock, maxTransInSpectrum);
            _solver = new NonNegLsSolver(NumIsoWindows, maxRowsInBlock, maxTransInSpectrum);
            _file.EnableCaching((int)(DeconvWindowEpsilonTest * 2.5));
        }

        /// <summary>
        /// Analyzes the file to figure out the multiplexing scheme
        /// </summary>
        /// <returns>True if properly formed MSX file</returns>
        private bool AnalyzeFile()
        {
            int numSpectra = _file.SpectrumCount;
            for (int i = 0; i < numSpectra; ++i)
            {
                if (_file.GetMsLevel(i) == 2)
                {
                    _msMsSpectra.Add(i);
                }
            }

            int? nIsoWindowsPerScan = null;
            double? nIsolationWindowWidth = null;

            // populate the isolation window mapper (infer if all windows unique)
            int previousNumWindows = _isoMapper.NumWindows;
            for (int i = 0; i < _msMsSpectra.Count; ++i)
            {
                if ((i + 1) % 123 == 0)
                //every 123rd spectrum, check to see if we're still adding windows
                {
                    if (_isoMapper.NumWindows == previousNumWindows)
                        break;
                    previousNumWindows = _isoMapper.NumWindows;
                }
                var scanNumber = _msMsSpectra[i];
                MsPrecursor[] precs = _file.GetPrecursors(scanNumber);
                // add these precursors to the isolation window mapper
                _isoMapper.Add(precs);
                if (!nIsoWindowsPerScan.HasValue)
                {
                    nIsoWindowsPerScan = precs.Length;
                }
                if (!nIsolationWindowWidth.HasValue)
                {
                    foreach (var prec in precs.Where(p => p.IsolationWidth.HasValue))
                    {
                        nIsolationWindowWidth = prec.IsolationWidth;
                        break;
                    }
                }
            }
            /*
            foreach (int scanNumber in _msMsSpectra)
            {
                MsPrecursor[] precs = _file.GetPrecursors(scanNumber);
                // add these precursors to the isolation window mapper
                if (_isoMapper.Add(precs) == 0)
                    break;

                if (!nIsoWindowsPerScan.HasValue)
                {
                    nIsoWindowsPerScan = precs.Length;
                }
                if (!nIsolationWindowWidth.HasValue)
                {
                    foreach (var prec in precs.Where(p => p.IsolationWidth.HasValue))
                    {
                        nIsolationWindowWidth = prec.IsolationWidth;
                        break;
                    }
                }
             

            }
             */
            if (!nIsoWindowsPerScan.HasValue || !nIsolationWindowWidth.HasValue)
                return false;
            IsoWindowsPerScan = nIsoWindowsPerScan.Value;
            _isolationWindowWidth = nIsolationWindowWidth.Value;
            NumIsoWindows = _isoMapper.NumWindows;
            return true;
        }

        ///<summary>
        /// initializes the spectrum processor at the given starting spectrum
        /// </summary>
        private void InitializeProcessor()
        {
            // this is larger than necessary but should run fine anyways
            int cacheSize = (int)(DeconvWindowEpsilonTest * 2.5);
            _spectrumProcessor = new SpectrumProcessor(cacheSize, _isoMapper, _filter);
        }

        private void FindStartStop(int scanIndex, MsDataSpectrum originalSpectrum,
                                   out int startMsMsIndex, out int endMsMsIndex)
        {
            if (_msMsSpectra[_msMsIndex] < scanIndex)
            {
                _msMsIndex = _msMsSpectra.IndexOf(scanIndex, _msMsIndex);
            }
            else if (_msMsSpectra[_msMsIndex] > scanIndex)
            {
                _msMsIndex = _msMsSpectra.LastIndexOf(scanIndex, _msMsIndex);
            }
            if (_msMsIndex == -1)
                throw new IndexOutOfRangeException(string.Format("MsxDemultiplexer: MS/MS index {0} not found", scanIndex));

            int countMsMs = (int)(DutyCycleLength * IsoWindowsPerScan + (1.5 * DutyCycleLength));
            int centerIndex = _msMsIndex;
            startMsMsIndex = Math.Max(0, centerIndex - countMsMs / 2);
            endMsMsIndex = startMsMsIndex + countMsMs;
            if (endMsMsIndex >= _msMsSpectra.Count)
            {
                endMsMsIndex = _msMsSpectra.Count - 1;
                startMsMsIndex = endMsMsIndex - countMsMs;
            }
            // TODO: add a warning here if endIndex - startIndex is less than _deconvWindowEpsilon?
            // TODO: this would indicate that the file does not have enough spectra
            // TODO: if not, add index, then pull it out, add to deconv block

            // startIndex and endIndex are now the indices in _msMsScans corresponding to the
            // minimum and maximum scan number that could be needed to deconvolve this spectrum
            // make sure that all of these spectra have been processed
            for (int i = startMsMsIndex; i <= endMsMsIndex; ++i)
            {
                var scanIndexToAdd = _msMsSpectra[i];
                if (!_spectrumProcessor.HasScan(scanIndexToAdd))
                    _spectrumProcessor.AddSpectrum(scanIndexToAdd, _file.GetSpectrum(scanIndexToAdd));
            }
        }

        #region Test
        public void CorrectPeakIntensitiesTest(MsDataSpectrum originalSpectrum,
            HashSet<int> binIndicesSet, int[] isoIndices, double[] peakSums,
            Dictionary<int, int> binToDeconvIndex, IEnumerable<KeyValuePair<int, int>> queryBinEnumerator,
            DeconvBlock deconvBlock, ref double[][] deconvIntensities, ref double[][] deconvMzs)
        {
            var originalDeconvBlock = _deconvBlock;
            _deconvBlock = deconvBlock;
            try
            {
                CorrectPeakIntensities(ref originalSpectrum, binIndicesSet, isoIndices, peakSums,
                    binToDeconvIndex, queryBinEnumerator, ref deconvIntensities, ref deconvMzs);
            }
            finally 
            {
                _deconvBlock = originalDeconvBlock;
            }
        }
        #endregion

        private void CorrectPeakIntensities(ref MsDataSpectrum originalSpectrum,
            ICollection<int> binIndicesSet, IList<int> isoIndices, IList<double> peakSums,
            IDictionary<int, int> binToDeconvIndex, IEnumerable<KeyValuePair<int, int>> queryBinEnumerator,
            ref double[][] deconvIntensities, ref double[][] deconvMzs)
        {
            for (int i = 0; i < deconvIntensities.Length; ++i)
            {
                originalSpectrum.Intensities.CopyTo(deconvIntensities[i], 0);
                originalSpectrum.Mzs.CopyTo(deconvMzs[i], 0);
            }
            int? prevPeakIndex = null;

            double[] peakCorrections = new double[isoIndices.Count];
            int[] numBins = new int[isoIndices.Count];
            for (int i = 0; i < isoIndices.Count; ++i)
            {
                peakCorrections[i] = 0.0;
                numBins[i] = 0;
            }
            int binsConsidered = 0;

            foreach (var queryBin in queryBinEnumerator)
            {
                // enumerate over each peak:transition combination, one peak may fall into more than one transition
                var peakIndex = queryBin.Key;
                var bin = queryBin.Value;
                if (prevPeakIndex.HasValue && peakIndex != prevPeakIndex)
                {
                    // just finished processing a peak, add it
                    if (binsConsidered > 0)
                    // the peak fell into some transitions for deconvolved spectra
                    {
                        var originalPeakIntensity = originalSpectrum.Intensities[prevPeakIndex.Value];
                        if (originalPeakIntensity > 0.0)
                        {
                            var originalPeakMz = originalSpectrum.Mzs[prevPeakIndex.Value];
                            for (int deconvSpecIndex = 0; deconvSpecIndex < isoIndices.Count; ++deconvSpecIndex)
                            {
                                var nBins = numBins[deconvSpecIndex];
                                if (nBins <= 0)
                                {
                                    deconvIntensities[deconvSpecIndex][prevPeakIndex.Value] = 0.0;
                                    deconvMzs[deconvSpecIndex][prevPeakIndex.Value] = originalPeakMz;
                                    continue;
                                }
                                var peakCorrection = peakCorrections[deconvSpecIndex];
                                var deconvolvedPeakInt = originalPeakIntensity * peakCorrection / nBins;
                                // add this peak
                                // TODO: This is a hack until Thermo fixes intensity reporting for multiplexed spectra
                                // deconvIntensities[deconvSpecIndex][prevPeakIndex.Value] = deconvolvedPeakInt * 
                                //    IsoWindowsPerScan;
                                deconvIntensities[deconvSpecIndex][prevPeakIndex.Value] = deconvolvedPeakInt;
                                deconvMzs[deconvSpecIndex][prevPeakIndex.Value] = originalPeakMz;
                            }
                        }
                    }
                    binsConsidered = 0;
                    for (int i = 0; i < isoIndices.Count; ++i)
                    {
                        peakCorrections[i] = 0.0;
                        numBins[i] = 0;
                    }
                }
                prevPeakIndex = peakIndex;
                if (binIndicesSet.Contains(bin))
                {
                    // this bin (transition) is in one of the deconvolved spectra
                    ++binsConsidered;
                    // for each deconvolved spectrum...
                    for (int deconvSpecIndex = 0; deconvSpecIndex < isoIndices.Count; ++deconvSpecIndex)
                    {
                        var isoIndex = isoIndices[deconvSpecIndex];
                        // find if this bin is in this deconvolved spectrum
                        if (!_spectrumProcessor.TransBinner.BinInPrecursor(bin, isoIndex)) continue;
                        int tranIndex = binToDeconvIndex[bin];
                        if (peakSums[tranIndex] == 0.0) continue;
                        peakCorrections[deconvSpecIndex] +=
                            _deconvBlock.Solution.Matrix[isoIndex, tranIndex] / peakSums[tranIndex];
                        numBins[deconvSpecIndex]++;
                    }
                }
            }
            // handle the end case
            if (binsConsidered > 0 && prevPeakIndex.HasValue)
            {
                var originalPeakIntensity = originalSpectrum.Intensities[prevPeakIndex.Value];
                var originalPeakMz = originalSpectrum.Mzs[prevPeakIndex.Value];
                for (int deconvSpecIndex = 0; deconvSpecIndex < isoIndices.Count; ++deconvSpecIndex)
                {
                    var nBins = numBins[deconvSpecIndex];
                    if (nBins <= 0)
                    {
                        deconvIntensities[deconvSpecIndex][prevPeakIndex.Value] = 0.0;
                        deconvMzs[deconvSpecIndex][prevPeakIndex.Value] = originalPeakMz;
                        continue;
                    }
                    var peakCorrection = peakCorrections[deconvSpecIndex];
                    var deconvolvedPeakInt = originalPeakIntensity * peakCorrection / nBins;
                    //add this peak
                    // TODO: This is a hack until Thermo fixes intensity reporting for multiplexed spectra
                    // deconvIntensities[deconvSpecIndex][prevPeakIndex.Value] = deconvolvedPeakInt * IsoWindowsPerScan;
                    deconvIntensities[deconvSpecIndex][prevPeakIndex.Value] = deconvolvedPeakInt;
                    deconvMzs[deconvSpecIndex][prevPeakIndex.Value] = originalPeakMz;
                }
            }
        }

        public MsDataSpectrum[] GetDeconvolvedSpectra(int index, MsDataSpectrum originalSpectrum)
        {
            // make sure this index is within range
            if (index < 0 || index > _file.SpectrumCount)
                throw new IndexOutOfRangeException(string.Format("MsxDemultiplexer: GetDeconvolvedSpectra: " +
                                                          "spectrum index {0} out of range.", index));
            // if the first time called, initialize the cache
            if (!_initialized)
                InitializeFile();

            // figure out the maximum possible start/stop indices in msMsSpectra for spectra needed
            // first, find the index in msMsSpectra containing the scan queried (index)
            int startIndex, endIndex;
            SingleScanCache processedSpec;
            if (!_spectrumProcessor.TryGetSpectrum(index, out processedSpec))
            {
                _spectrumProcessor.AddSpectrum(index, _file.GetSpectrum(index));
                _spectrumProcessor.TryGetSpectrum(index, out processedSpec);
            }

            int[] isoIndices = processedSpec.IsoIndices;
            if (originalSpectrum == null)
                originalSpectrum = _file.GetSpectrum(index);
            FindStartStop(index, originalSpectrum, out startIndex, out endIndex);

            MsDataSpectrum[] returnSpectra;

            var binIndicesSet = _spectrumProcessor.BinIndicesFromIsolationWindows(isoIndices);
            if (binIndicesSet.Count == 0)
            {
                // none of the precursors for this spectrum overlap with any of the
                // precursors in the document
                // return a blank spectrum
                returnSpectra = new MsDataSpectrum[1];
                returnSpectra[0] = originalSpectrum;
                returnSpectra[0].Intensities = new double[0];
                returnSpectra[0].Mzs = new double[0];
                return returnSpectra;
            }
            List<int> binIndicesList = binIndicesSet.ToList();
            binIndicesList.Sort();

            // get each spectrum, with a predicate for isolation windows
            _deconvBlock.Clear();
            for (int i = startIndex; i <= endIndex; ++i)
            {
                int scanIndex = _msMsSpectra[i];
                IEnumerable<double> spectrumData;
                double[] mask;
                // add only transitions contained in the document in the precursor
                // windows for the spectrum
                if (!_spectrumProcessor.TryGetFilteredSpectrumData(scanIndex, binIndicesList,
                                               out spectrumData, out mask))
                {
                    _spectrumProcessor.AddSpectrum(scanIndex, _file.GetSpectrum(scanIndex));
                    _spectrumProcessor.TryGetFilteredSpectrumData(scanIndex, binIndicesList,
                                                               out spectrumData, out mask);
                }
                _deconvBlock.Add(mask, spectrumData);
            }


            // pass the deconv block to the demultiplexer
            _solver.Solve(_deconvBlock);
            int numDeconvolvedTrans = _deconvBlock.NumTransitions;
            double[] peakSums = new double[numDeconvolvedTrans];
            Dictionary<int, int> binToDeconvIndex = new Dictionary<int, int>(numDeconvolvedTrans);
            int numBinIndices = binIndicesList.Count;
            for (int i = 0; i < numBinIndices; ++i)
                binToDeconvIndex[binIndicesList[i]] = i;
            // for each transition, calculate its intensity summed over 
            // each deconvolved spectrum 
            for (int transIndex = 0; transIndex < numDeconvolvedTrans; ++transIndex)
            {
                double intensitySum = 0.0;
                foreach (var isoIndex in isoIndices)
                    intensitySum += _deconvBlock.Solution.Matrix[isoIndex, transIndex];
                peakSums[transIndex] = intensitySum;
            }

            // initialize arrays to store deconvolved spectra
            double[][] deconvIntensities = new double[isoIndices.Length][];
            double[][] deconvMzs = new double[isoIndices.Length][];
            for (int i = 0; i < isoIndices.Length; ++i)
            {
                deconvIntensities[i] = new double[originalSpectrum.Mzs.Length];
                deconvMzs[i] = new double[originalSpectrum.Mzs.Length];
            }
            try
            {
                var queryBinEnumerator = _spectrumProcessor.TransBinner.BinsFromValues(originalSpectrum.Mzs, true);
                CorrectPeakIntensities(ref originalSpectrum, binIndicesSet,
                    isoIndices, peakSums, binToDeconvIndex, queryBinEnumerator, ref deconvIntensities,
                    ref deconvMzs);
            }
            catch (InvalidOperationException)
            {
                // there are no peaks that fall in any of the transition bins
                // return a blank spectrum
                returnSpectra = new MsDataSpectrum[1];
                returnSpectra[0] = originalSpectrum;
                returnSpectra[0].Intensities = new double[0];
                returnSpectra[0].Mzs = new double[0];
                return returnSpectra;
            }
            returnSpectra = new MsDataSpectrum[isoIndices.Length];
            for (int deconvSpecIndex = 0; deconvSpecIndex < isoIndices.Length; ++deconvSpecIndex)
            {
                var deconvSpec = new MsDataSpectrum
                {
                    Intensities = deconvIntensities[deconvSpecIndex],
                    Mzs = deconvMzs[deconvSpecIndex],
                    Precursors = new MsPrecursor[1]
                };
                deconvSpec.Precursors[0] = _isoMapper.GetPrecursor(isoIndices[deconvSpecIndex]);
                deconvSpec.Centroided = originalSpectrum.Centroided;
                deconvSpec.Level = originalSpectrum.Level;
                deconvSpec.RetentionTime = originalSpectrum.RetentionTime;
                returnSpectra[deconvSpecIndex] = deconvSpec;
            }
            return returnSpectra;
        }
    }

    public class DeconvBlock
    {
        public int MaxRows { get; private set; }
        public int MaxTransitions { get; private set; }
        public int NumRows { get { return Masks.NumRows; } }
        public int NumTransitions { get { return BinnedData.NumCols; } }
        public int NumIsos { get; private set; }

        public MatrixWrap Masks { get; private set; }
        public MatrixWrap BinnedData { get ; private set;}

        public MatrixWrap Solution { get; private set; }

        public DeconvBlock(int numIsos, int maxRows, int maxTransitions)
        {
            NumIsos = numIsos;
            Masks = new MatrixWrap(maxRows, numIsos);
            BinnedData = new MatrixWrap(maxRows, maxTransitions);
            Solution = new MatrixWrap(numIsos, maxTransitions);
            Solution.SetNumRows(numIsos);
            MaxRows = maxRows;
            MaxTransitions = maxTransitions;
        }
        
        public void Clear()
        {
            Masks.Reset();
            BinnedData.Reset();
            Solution.Reset();
            Masks.SetNumCols(NumIsos);
            Solution.SetNumRows(NumIsos);
        }

        public void Add(double[] mask, IEnumerable<double> data)
        {
            // update Masks
            Masks.Matrix.SetRow(Masks.NumRows, mask);
            Masks.SetNumCols(mask.Length);
            Masks.IncrementNumRows();
            
            // update BinnedData
            int colNum = 0;
            foreach (var dataVal in data)
                BinnedData.Matrix[BinnedData.NumRows, colNum++] = dataVal;

            BinnedData.IncrementNumRows();
            BinnedData.SetNumCols(colNum);

            // update Solution
            Solution.SetNumCols(colNum);
        }
    }


    /// <summary>
    /// Generates a mapping of isolation window -> index, keeping only unique isolation windows.
    /// Hashing is based on the center m/z for the isolation window.
    /// If there are collisions, only most recent value is kept.
    /// </summary>
    public sealed class IsolationWindowMapper
    {
        private readonly Dictionary<long, int> _isolationWindowD;
        private readonly List<double> _isolationWindows;
        private readonly List<MsPrecursor> _precursors;
        private int _lastSort;  //last length of the iso width for which sort happened
        private readonly List<KeyValuePair<double,int>> _isoWindowsSorted;
        private double? _windowWidth;

        public double WindowWidth
        {
            set { _windowWidth = value; }
        }

        public int NumWindows
        {
            get { return _isolationWindows.Count(); }
        }

        public IsolationWindowMapper()
        {
            // map the hash of the isolation window center (hashed to 6th 
            // decimal precision) to index in _isolationWindows
            _isolationWindowD = new Dictionary<long, int>();  
            _isolationWindows = new List<double>();     // full double precision value for the isolation window
            _isoWindowsSorted = new List<KeyValuePair<double,int>>();
            _precursors = new List<MsPrecursor>();
        }

        /// <summary>
        /// A method of hashing an isolation window to a unique long value
        /// isolationCenter is the m/z of the center of the isolation window,
        /// this value is multiplied by 100000000 and rounded to convert the
        /// isolation m/z to a long which is used as the hash.
        /// For example: a window with m/z 475.235 would get hashed to 47523500000
        /// </summary>
        /// <param name="isolationCenter"></param>
        /// <returns>The hashed isolation window center</returns>
        private long HashWindow(double isolationCenter)
        {
            return (long)Math.Round(isolationCenter * 100000000.0);
        }

        public int Add(IEnumerable<MsPrecursor> precursors)
        {
            int countAdded = 0;
            foreach (var precursor in precursors)
            {
                double? isolationCenter = precursor.IsolationMz;
                if (!isolationCenter.HasValue)
                    continue;
                long hash = HashWindow(isolationCenter.Value);
                if (_isolationWindowD.ContainsKey(hash))
                    continue;
                var isoMz = isolationCenter.Value;
                _isolationWindows.Add(isoMz);
                _isoWindowsSorted.Add(new KeyValuePair<double, int>(isoMz, _isolationWindows.Count-1));
                _isolationWindowD[hash] = _isolationWindows.Count() - 1;
                _precursors.Add(precursor);
                countAdded++;
            }
            return countAdded;
        }

        ///<summary>
        /// looks for a window with the exact center m/z given in isolationWindow
        /// returns false if the window is not in this collection
        /// </summary>
        /// <returns>false if the window is not in this collection, true if it is</returns>
        public bool TryGetWindowIndex(double isolationWindow, out int index)
        {
            long hash = HashWindow(isolationWindow);
            return _isolationWindowD.TryGetValue(hash, out index);
        }

        public void GetWindowMask(MsDataSpectrum s, out int[] isolationIndices, ref double[] mask)
        {
            var precursorMzs = s.Precursors.Select(p => p.IsolationMz.GetValueOrDefault());
            GetWindowMask(precursorMzs, out isolationIndices, ref mask);
        }

        private void GetWindowMask(IEnumerable<double> precursorMzs, out int[] isolationIndices, 
            ref double[] windowMask)
        {
            List<int> isolationIndicesList = new List<int>();
            if (windowMask.Length!=NumWindows)
                windowMask = new double[NumWindows];
            else
            {
                // Zero array for reuse.
                for (int i = 0; i < windowMask.Length; ++i)
                {
                    windowMask[i] = 0.0;
                }
            }

            foreach (var precursorMz in precursorMzs)
            {
                int windowIndex;
                if (!TryGetWindowIndex(precursorMz, out windowIndex))
                {
                    throw new ArgumentException(string.Format("IsolationWindowMapper: Tried to get a window mask" +
                        " for {0}, a spectrum with previously unobserved isolation windows", precursorMz));
                }

                windowMask[windowIndex] = 1.0;
                isolationIndicesList.Add(windowIndex);
            }
            isolationIndices = isolationIndicesList.ToArray();
        }

        public double GetIsolationWindow(int isoIndex)
        {
            if (isoIndex < 0 || isoIndex > _isolationWindows.Count)
                throw new ArgumentOutOfRangeException(String.Format("IsolationWindowMapper: " +
                                                             "isoIndex {0} out of range", isoIndex));
            return _isolationWindows[isoIndex];
        }

        public MsPrecursor GetPrecursor(int isoIndex)
        {
            if (isoIndex < 0 || isoIndex > _precursors.Count)
                throw new ArgumentOutOfRangeException(String.Format("IsolationWindowMapper: " +
                                                             "isoIndex {0} out of range", isoIndex));
            return _precursors[isoIndex];
        }

        /// <summary>
        /// Comparer for sorting and binary search of key-value pair list by key only.
        /// </summary>
        private class KvpKeyComparer : IComparer<KeyValuePair<double,int>>
        {
            public int Compare(KeyValuePair<double,int> x, KeyValuePair<double,int> y)
            {
                return x.Key.CompareTo(y.Key);
            }
        }
        /// <summary>
        /// Find the isolation window containing the query m/z
        /// </summary>
        /// <returns>(True/False) Was a window containing this m/z found?</returns>
        public bool TryGetWindowFromMz(double mz, out int windowIndex)
        {
            if (_windowWidth == null)
            {
                throw new InvalidOperationException(String.Format("IsolationWindowMapper: TryGetWindowFromMz ({0}): " +
                                                           "_windowWidth must be set before calling this method", mz));
            }
            var comparer = new KvpKeyComparer();
            if (_lastSort < _isoWindowsSorted.Count)
            {
                _isoWindowsSorted.Sort(comparer);
                _lastSort = _isoWindowsSorted.Count;
            }

            var queryWindow = new KeyValuePair<double, int>(mz, 0);
            int index = _isoWindowsSorted.BinarySearch(queryWindow, comparer);

            if (index >=0)
            {
                // m/z matches a precursor isolation center exactly
                windowIndex = _isoWindowsSorted[index].Value;
                return true;
            }
            else
            {
                // m/z does not match a precursor isolation center exactly
                // ~index is the next window with isolation center greater than m/z
                // or _isoWindowsSorted.Count if there is no window with an isolation
                // center greater than m/z
                var nextGreatestWindow = ~index;
                // query m/z is between two isolation window centers, test each one
                var leftWindow = nextGreatestWindow == 0
                                        ? new KeyValuePair<double, int>(double.MinValue, -1)
                                        : _isoWindowsSorted[nextGreatestWindow - 1];
                if (mz <= leftWindow.Key + _windowWidth/2.0)
                {
                    windowIndex = leftWindow.Value;
                    return true;
                }

                var rightWindow = nextGreatestWindow == _isoWindowsSorted.Count
                                         ? new KeyValuePair<double, int>(double.MaxValue, -1)
                                         : _isoWindowsSorted[nextGreatestWindow];
                if (mz >= rightWindow.Key - _windowWidth / 2.0)
                {
                    windowIndex = rightWindow.Value;
                    return true;
                }
            }
            // the m/z does not fall in any of the windows
            windowIndex = -1;
            return false;
        }
    }

    public sealed class TransitionInfo : IComparable<TransitionInfo>
    {
        public TransitionInfo(double startMz, double endMz, int precursorIndex)
        {
            StartMz = startMz;
            EndMz = endMz;
            PrecursorIndex = precursorIndex;
        }

        public double StartMz { get; private set; }
        public double EndMz { get; private set; }
        public int PrecursorIndex { get; private set; }

        public int CompareTo(TransitionInfo other)
        {
            return StartMz.CompareTo(other.StartMz);
        }
        
        public int CompareTo(double other)
        {
            return StartMz.CompareTo(other);
        }
        
        public bool ContainsMz(double mz)
        {
            return (StartMz <= mz && mz <= EndMz);
        }
    }

    public struct SingleScanCache
    {
        public SingleScanCache(double[] mask, double[] data, int[] isoIndices) : this()
        {
            Mask = mask;
            Data = data;
            IsoIndices = isoIndices;
        }

        public double[] Mask { get; set; }
        public double[] Data { get; set; }
        public int[] IsoIndices { get; set; }
    }

    public sealed class TransitionBinner
    {
        private List<TransitionInfo> _allTransitions;
        private HashSet<int>[] _precursorTransitions;
        private double _maxTransitionWidth;
        private IsolationWindowMapper _isoMapper;

        public double MinValue { get; private set; }
        public double MaxValue { get; private set; }
        public int MinBin { get; private set; }
        public int MaxBin { get; private set; }
        public int NumBins { get; private set; }

        public TransitionBinner(SpectrumFilter filter, IsolationWindowMapper isoMapper)
        {
            InitializeVariables(isoMapper);
            // initialize the transitions using the filter information
            var filterPairs = filter.FilterPairs;
            foreach (var filterPair in filterPairs)
            {
                var precursorMz = filterPair.Q1;
                int precWindowIndex;
                // this precursor is outside of the range of mapped precursor
                // isolation windows
                if (!_isoMapper.TryGetWindowFromMz(precursorMz, out precWindowIndex))
                    continue;
                for (int i = 0; i < filterPair.ArrayQ3.Length; ++i)
                {
                    AddTransition(precWindowIndex, filterPair.ArrayQ3[i], filterPair.ArrayQ3Window[i]);
                }
            }
            // populate _allTransitions and precursor->transition map
            PopulatePrecursorToTransition();
        }

        private void InitializeVariables(IsolationWindowMapper isoMapper)
        {
             // initialize all variables
            _isoMapper = isoMapper;
            _allTransitions = new List<TransitionInfo>();
            _precursorTransitions = new HashSet<int>[_isoMapper.NumWindows];
            for (int i = 0; i < _precursorTransitions.Length; ++i)
            {
                _precursorTransitions[i] = new HashSet<int>();
            }
            _maxTransitionWidth = 0.0;
            MinValue = double.PositiveInfinity;
            MaxValue = double.NegativeInfinity;
            MinBin = int.MaxValue;
            MaxBin = int.MinValue;
            NumBins = 0;
        }

        private void PopulatePrecursorToTransition()
        {
             // sorts by starting m/z
            _allTransitions.Sort();
            for (int i = 0; i < _allTransitions.Count; ++i)
            {
                var precIndex = _allTransitions[i].PrecursorIndex;
                _precursorTransitions[precIndex].Add(i);
            }
            MinBin = 0;
            MaxBin = _allTransitions.Count - 1;
            NumBins = _allTransitions.Count;
        }

        private void AddTransition(int precursorIndex, double windowCenter, double windowWidth)
        {
            var windowStart = windowCenter - windowWidth / 2.0;
            var windowEnd = windowCenter + windowWidth / 2.0;
            if (windowWidth > _maxTransitionWidth)
                _maxTransitionWidth = windowWidth;
            if (windowStart < MinValue)
                MinValue = windowStart;
            if (windowEnd > MaxValue)
                MaxValue = windowEnd;
            _allTransitions.Add(new TransitionInfo(windowStart, windowEnd, precursorIndex));
        }

        public void BinData(double[] mzVals, double[] intensityVals, ref double[] binnedData)
        {
            if (mzVals.Length != intensityVals.Length)
                throw new IndexOutOfRangeException(String.Format("TransitionBinner: BinData: m/z and intensity arrays don't match in length ({0} != {1})", mzVals.Length, intensityVals.Length));
            if (binnedData.Length != NumBins)
                binnedData = new double[NumBins];
            else
            {
                for (int i = 0; i < NumBins; ++i)
                    binnedData[i] = 0.0;
            }
            foreach (var queryBin in BinsFromValues(mzVals, true))
            {
                var queryIndex = queryBin.Key;
                var binIndex = queryBin.Value;
                binnedData[binIndex] += intensityVals[queryIndex];
            }
        }

        public IEnumerable<KeyValuePair<int,int>> BinsFromValues(double[] queries, bool sorted)
        {
            if (!sorted)
                Array.Sort(queries);
            int numQueries = queries.Length;
            int binStartIndex = 0;
            for (int queryIndex = 0; queryIndex < numQueries; ++queryIndex)
            {
                double query = queries[queryIndex];
                if (query < MinValue)
                    continue;
                if (query > MaxValue)
                    yield break;
                // Look forward for the next bin that could contain the current
                // query value.
                double minStart = query - _maxTransitionWidth;  // minimum start that could contain query
                for ( ; binStartIndex < NumBins; binStartIndex++)
                {
                    if (_allTransitions[binStartIndex].StartMz >= minStart)
                        break;
                }
                // Look forward for transitions that actually contain the query
                for (int binIndex = binStartIndex; binIndex < NumBins; ++binIndex)
                {
                    var trans = _allTransitions[binIndex];
                    // Stop once past the query
                    if (trans.StartMz > query)
                        break;
                    if (trans.ContainsMz(query))
                        yield return new KeyValuePair<int, int>(queryIndex,binIndex);
                }
            }
        }

        public double LowerValueFromBin(int queryBin)
        {
            return TransInfoFromBin(queryBin).StartMz;
        }

        public double UpperValueFromBin(int queryBin)
        {
            return TransInfoFromBin(queryBin).EndMz;
        }

        public double CenterValueFromBin(int queryBin)
        {
            var transInf = TransInfoFromBin(queryBin);
            return (transInf.StartMz + transInf.EndMz) / 2.0;
        }

        public TransitionInfo TransInfoFromBin(int queryBin)
        {
            if (queryBin < MinBin || queryBin > MaxBin)
                throw new IndexOutOfRangeException(string.Format("TransitionBinner[TransInfoFromBin]: Index out of range: {0}", queryBin));
            return _allTransitions[queryBin];
        }

        public ICollection<int> BinsForPrecursors(int[] precursors)
        {
            var validBins = new HashSet<int>();

            foreach (int t in precursors)
            {
                if (0 > t|| t > _precursorTransitions.Length)
                    throw new IndexOutOfRangeException(string.Format("TransitionBinner: BinsForPrecursors: precursor index out of range: {0}", t));
                validBins.UnionWith(_precursorTransitions[t]);
            }

            return validBins;
        }

        public bool BinInPrecursor(int bin, int precursor)
        {
            if (0 > precursor || precursor > _precursorTransitions.Length)
                throw new IndexOutOfRangeException(string.Format("TransitionBinner: BinInPrecursor: precursor index out of range: {0}", precursor));
            return _precursorTransitions[precursor].Contains(bin);
        }

        public int MaxTransitions(int numWindows)
        {
            if (0 >= numWindows || numWindows > _precursorTransitions.Length)
            {
                throw new ArgumentOutOfRangeException(string.Format("TransitionBinner: MaxTransitions" +
                                                             "asked for transitions from too many" +
                                                             "precursors: {0}", numWindows));
            }
            var transNumsSorted = from prec in _precursorTransitions
                                  orderby prec.Count descending
                                  select prec.Count;
            int counter = 0;
            int maxTrans = 0;
            foreach (var transCount in transNumsSorted)
            {
                maxTrans += transCount;
                if (++counter == numWindows)
                    break;
            }
            return Math.Min(maxTrans, NumBins);
        }

        #region Test helpers

        public TransitionBinner(List<double> precursors, List<KeyValuePair<double,double>> transitions,
             IsolationWindowMapper isoMapper)
        {
            InitializeVariables(isoMapper);
            for (int i = 0; i<precursors.Count; ++i)
            {
                var precursorMz = precursors[i];
                int precWindowIndex;
                if (!_isoMapper.TryGetWindowFromMz(precursorMz, out precWindowIndex))
                    continue;
                var transCenter = transitions[i].Key;
                var transWidth = transitions[i].Value;
                AddTransition(precWindowIndex, transCenter, transWidth);
            }
            PopulatePrecursorToTransition();
        }

        #endregion
    }

    /// <summary>
    /// keeps a cache of processed spectra, performs processing on spectra when
    /// added to the cache
    /// </summary>
    public sealed class SpectrumProcessor
    {
        private readonly int _cacheSize;
        private readonly IsolationWindowMapper _isoMapper;
        private readonly TransitionBinner _transBinner;
        private readonly Dictionary<int, SingleScanCache> _cache;
        private readonly Queue<int> _scanStack; 
        private readonly ArrayPool<double> _maskPool;
        private readonly ArrayPool<double> _binnedDataPool;

        public TransitionBinner TransBinner { get { return _transBinner; } }
        public SpectrumProcessor(int cacheSize,
            IsolationWindowMapper isoMapper,
            SpectrumFilter filter)
            :this(cacheSize, isoMapper, new TransitionBinner(filter, isoMapper))
        {
        }

        public SpectrumProcessor(int cacheSize,
            IsolationWindowMapper isoMapper,
            TransitionBinner transBinner)
        {
             _cacheSize = cacheSize;
            _isoMapper = isoMapper;
            _transBinner = transBinner;
            _cache = new Dictionary<int, SingleScanCache>();
            _scanStack = new Queue<int>(cacheSize);
            _binnedDataPool = new ArrayPool<double>(_transBinner.NumBins, cacheSize);
            _maskPool = new ArrayPool<double>(isoMapper.NumWindows, cacheSize);
        }

        public bool HasScan(int scanNum)
        {
            return _cache.ContainsKey(scanNum);
        }

        public int MaxTransitions(int numWindows)
        {
            return _transBinner.MaxTransitions(numWindows);
        }

        /// <summary>
        /// Find which transition bin indices belong to the queried isolation indices
        /// </summary>
        /// <param name="isoIndices"></param>
        /// <returns>a sorted list of bin indices for the set of iso indices given</returns>
        public List<int> BinIndicesFromIsolationWindows(int[] isoIndices)
        {
            var returnIndices = _transBinner.BinsForPrecursors(isoIndices).ToList();
            returnIndices.Sort();
            return returnIndices;
        }

        public void AddSpectrum(int scanNum, MsDataSpectrum s)
        {
            if (_scanStack.Count >= _cacheSize)
                RemoveLast();

            int[] isoIndices;
            double[] isoMask = _maskPool.Get();
            _isoMapper.GetWindowMask(s, out isoIndices, ref isoMask);

            // update _isoScans,
            // normally this scan will be of greater scan number than any previous
           double[] binnedData = _binnedDataPool.Get();
            _transBinner.BinData(s.Mzs, s.Intensities, ref binnedData);
           // add this data to the cache
            _cache[scanNum] = new SingleScanCache(isoMask, binnedData, isoIndices);
            _scanStack.Enqueue(scanNum);
        }

        private void RemoveLast()
        {
            int scanToRemove = _scanStack.Dequeue();
            var removed = _cache[scanToRemove];
            _binnedDataPool.Free(removed.Data);
            _maskPool.Free(removed.Mask);
            _cache.Remove(scanToRemove);
        }

        /// <summary>
        /// Get cached spectrum information by scan index
        /// </summary>
        public bool TryGetSpectrum(int scanIndex, out SingleScanCache spectrumData)
        {
            return _cache.TryGetValue(scanIndex, out spectrumData);
        }

        /// <summary>
        /// Extract intensity values from a spectrum only for the transition
        /// bin indices given in binIndices
        /// </summary>
        public bool TryGetFilteredSpectrumData(int scanIndex, IList<int> binIndices,
                                            out IEnumerable<double> spectrumData, 
                                            out double[] isoMask)
        {
            SingleScanCache spectrum;
            if (!TryGetSpectrum(scanIndex, out spectrum))
            {
                spectrumData = null;
                isoMask = null;
                return false;
            }
            spectrumData = binIndices.Select(i => spectrum.Data[i]);
            isoMask = spectrum.Mask;
            return true;
        }

        #region Legacy code

        public void FindScanWindowExact(int centerScan, int numScans, List<int> msMsSpectra,
                                        out int startIndex, out int endIndex)
        {
            FindScanWindowExact(Enumerable.Range(0,_isoMapper.NumWindows), centerScan, numScans,
                msMsSpectra, out startIndex, out endIndex);
        }

        public void FindScanWindowExact(IEnumerable<int> isoIndices, int centerScan, int numScans, 
            List<int> msMsSpectra, out int startIndex, out int endIndex)
        {
            // this can be made more efficient,
            // don't technically need the neededScans object (it just helps for debugging)
            int[] isoIndicesArray = isoIndices.ToArray();
            int max = isoIndicesArray.Max()+1;
            SizedSet neededScans = new SizedSet(max);
            int[] numEachIsoWindow = new int[max];
            bool addForward = true;
            bool hitForward = false;
            bool hitBack = false;
            foreach (var isoIndex in isoIndicesArray)
            {
                neededScans.Add(isoIndex);
                numEachIsoWindow[isoIndex] = 0;
            }
            int centerIndex = 0;
            while (centerIndex < msMsSpectra.Count -1 && msMsSpectra[centerIndex]!=centerScan)
                ++centerIndex;

            int forwardIndex = centerIndex + 1;
            int backIndex = centerIndex - 1;
            startIndex = centerIndex;
            endIndex = centerIndex;
            SingleScanCache s;
            TryGetSpectrum(centerScan, out s);

            foreach (var isoWindow in s.IsoIndices)
                numEachIsoWindow[isoWindow] += 1;

            while(neededScans.Count >0)
            {
                if (hitBack)
                    addForward = true;
                if (hitForward)
                    addForward = false;
                int addIndex;
                if (addForward)
                {
                    addIndex = forwardIndex;
                    if (addIndex >= msMsSpectra.Count)
                    {
                        hitForward = true;
                        addIndex = backIndex;
                        startIndex = backIndex;
                    }
                    else
                    {
                        endIndex = forwardIndex;
                        forwardIndex++;
                    }
                }
                else
                {
                    addIndex = backIndex;
                    if (addIndex <0)
                    {
                        hitBack = true;
                        addIndex = forwardIndex;
                        endIndex = forwardIndex;
                    }
                    else
                    {
                        startIndex = backIndex;
                        backIndex--;
                    }
                }
                var specNumber = msMsSpectra[addIndex];
                TryGetSpectrum(specNumber, out s);
                foreach (var isoIndex in s.IsoIndices.Where(neededScans.Contains))
                {
                    numEachIsoWindow[isoIndex] += 1;
                    if (numEachIsoWindow[isoIndex] >= numScans)
                        neededScans.Remove(isoIndex);
                }
                addForward = !addForward;
            }
        }
        #endregion
    }
}