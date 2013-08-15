/*
 * Original author: Jarrett Egertson <jegertso .at .u.washington.edu>,
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
using System.Linq;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// An abstract class from which demultiplexers are derived. It takes in spectra from a file, 
    /// stores all the isolation windows included in these spectra, and further divides each spectrum 
    /// into a set of "deconvolution regions" defined in a way dependent on the specific demultiplexing method
    /// (the deconvolution regions are often the overlaps bewtween isolation windows), and applies a solver 
    /// algorithm (details given by the specific derived class) to deconvolve the spectra.
    /// </summary>
    public abstract class AbstractDemultiplexer : IDemultiplexer
    {
        protected readonly MsDataFileImpl _file;
        protected readonly SpectrumFilter _filter;
        protected AbstractIsoWindowMapper _isoMapper;
        protected bool _initialized;
        protected double _isolationWindowWidth;
        protected SpectrumProcessor _spectrumProcessor;
        protected readonly List<int> _msMsSpectra;
        protected int _msMsIndex;
        protected ILsSolver _solver;
        protected DeconvBlock _deconvBlock;

        public int NumIsoWindows { get; private set; }
        public int NumDeconvRegions { get; private set; }
        public int IsoWindowsPerScan { get; private set; }

        public abstract int NumScansBlock { get; }

        #region Test helpers

        public void ForceInitializeFile()
        {
            InitializeFile();
        }

        public AbstractIsoWindowMapper IsoMapperTest { get { return _isoMapper; } }
        public SpectrumProcessor SpectrumProcessor
        {
            get { return _spectrumProcessor; }
            set { _spectrumProcessor = value; }
        }
        #endregion Test

        protected AbstractDemultiplexer(MsDataFileImpl file, SpectrumFilter filter)
        {
            _file = file;
            _filter = filter;
            _msMsSpectra = new List<int>();
        }

        // Regularization of the Matrix, how to regularize and how many rows
        // to add depend on the type of demultiplexer. Also computational shortcuts
        public abstract DeconvBlock PreprocessDeconvBlock(int[] deconvIndices);

        // Unpack computational shortcuts and regularization rows
        public abstract void PostprocessDeconvBlock(DeconvBlock deconvBlock, int[] deconvIndices);

        protected void InitializeFile()
        {
            if (!AnalyzeFile())
                throw new IOException(Resources.OverlapDemultiplexer_InitializeFile_OverlapDemultiplexer_InitializeFile_Improperly_formed_overlap_multiplexing_file);
            _isoMapper.WindowWidth = _isolationWindowWidth;
            _isoMapper.DetermineDeconvRegions();
            NumDeconvRegions = _isoMapper.NumDeconvRegions;
            NumIsoWindows = _isoMapper.NumWindows;
            InitializeProcessor();
            InitializeSolver();
            _initialized = true;
            _file.EnableCaching((int)(NumScansBlock*1.5));
        }

        protected bool AnalyzeFile()
        {
            int numSpectra = _file.SpectrumCount;
            for (int i = 0; i < numSpectra; ++i)
            {
                if (_file.GetMsLevel(i) == 2)
                {
                    _msMsSpectra.Add(i);
                }
            }
            double? nIsolationWindowWidth = null;
            int? nIsoWindowsPerScan = null;

            int previousNumWindows = _isoMapper.NumWindows;
            for (int i = 0; i < _msMsSpectra.Count; ++i)
            {
                if ((i + 1) % 123 == 0)
                // Every 123rd spectrum, check to see if we're still adding windows
                {
                    if (_isoMapper.NumWindows == previousNumWindows)
                        break;
                    previousNumWindows = _isoMapper.NumWindows;
                }
                var scanNumber = _msMsSpectra[i];
                MsPrecursor[] precs = _file.GetPrecursors(scanNumber);
                // If the precursor values are null, fill in with the specified window sizes
                //foreach (MsPrecursor prec in precs)
                //{
                //    if (prec.IsolationWidth == null ||
                //        prec.IsolationWindowLower == null ||
                //        prec.IsolationWindowUpper == null ||
                //        prec.IsolationWindowTargetMz == null)
                //    {
                //        prec.IsolationWidth = _filter
                //    }
                //}
                // Add these precursors to the isolation window mapper
                _isoMapper.Add(precs);
                if (!nIsolationWindowWidth.HasValue)
                {
                    foreach (var prec in precs.Where(p => p.IsolationWidth.HasValue))
                    {
                        nIsolationWindowWidth = prec.IsolationWidth;
                        break;
                    }
                }
                if (!nIsoWindowsPerScan.HasValue)
                {
                    nIsoWindowsPerScan = precs.Length;
                }
            }
            if (!nIsoWindowsPerScan.HasValue || !nIsolationWindowWidth.HasValue)
                return false;
            IsoWindowsPerScan = nIsoWindowsPerScan.Value;
            _isolationWindowWidth = nIsolationWindowWidth.Value;
            NumIsoWindows = _isoMapper.NumWindows;
            return true;
        }

        protected abstract void InitializeSolver();

        protected void InitializeProcessor()
        {
            int cacheSize = (int)((NumScansBlock - 1) * 1.5);
            _spectrumProcessor = new SpectrumProcessor(cacheSize, _isoMapper, _filter);
        }

        protected void FindStartStop(int scanIndex, MsDataSpectrum originalSpectrum, out int startMsMsIndex,
                                   out int endMsMsIndex)
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
                throw new IndexOutOfRangeException(
                    string.Format(Resources.MsxDemultiplexer_FindStartStop_MsxDemultiplexer_MS_MS_index__0__not_found,
                                  scanIndex));
            int countMsMs = NumScansBlock - 1;
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
            HashSet<int> binIndicesSet, IList<int> isoIndices, IList<int> deconvIndices,double[] peakSums,
            Dictionary<int, int> binToDeconvIndex, IEnumerable<KeyValuePair<int, int>> queryBinEnumerator,
            DeconvBlock deconvBlock, ref double[][] deconvIntensities, ref double[][] deconvMzs)
        {
            var originalDeconvBlock = _deconvBlock;
            _deconvBlock = deconvBlock;
            List<int> binIndicesList = binIndicesSet.ToList();
            try
            {
                CorrectPeakIntensities(ref originalSpectrum, binIndicesList, isoIndices,deconvIndices, peakSums,
                    binToDeconvIndex, queryBinEnumerator, ref deconvIntensities, ref deconvMzs);
            }
            finally
            {
                _deconvBlock = originalDeconvBlock;
            }
        }
        #endregion

        protected void CorrectPeakIntensities(ref MsDataSpectrum originalSpectrum,
                                              List<int> binIndicesList,
                                              IList<int> isoIndices,
                                              IList<int> deconvIndices,
                                              IList<double> peakSums,
                                              IDictionary<int, int> binToDeconvIndex,
                                              IEnumerable<KeyValuePair<int, int>> queryBinEnumerator,
                                              ref double[][] deconvIntensities,
                                              ref double[][] deconvMzs)
        {
            // Will do binary search on this
            binIndicesList.Sort();
            for (int i = 0; i < deconvIntensities.Length; ++i)
            {
                originalSpectrum.Intensities.CopyTo(deconvIntensities[i], 0);
                originalSpectrum.Mzs.CopyTo(deconvMzs[i], 0);
                for (int j = 0; j < originalSpectrum.Intensities.Length; ++j)
                {
                    deconvIntensities[i][j] = deconvIntensities[i][j]/deconvIntensities.Length;
                }
            }
            int? prevPeakIndex = null;

            double[] peakCorrections = new double[deconvIndices.Count];
            int[] numBins = new int[deconvIndices.Count];
            for (int i = 0; i < deconvIndices.Count; ++i)
            {
                peakCorrections[i] = 0.0;
                numBins[i] = 0;
            }
            int binsConsidered = 0;

            foreach (var queryBin in queryBinEnumerator)
            {
                // Enumerate over each peak:transition combination, one peak may fall into more than one transition
                var peakIndex = queryBin.Key;
                var bin = queryBin.Value;
                if (prevPeakIndex.HasValue && peakIndex != prevPeakIndex)
                {
                    // Just finished processing a peak, add it
                    if (binsConsidered > 0)
                    // The peak fell into some transitions for deconvolved spectra
                    {
                        var originalPeakIntensity = originalSpectrum.Intensities[prevPeakIndex.Value];
                        if (originalPeakIntensity > 0.0)
                        {
                            var originalPeakMz = originalSpectrum.Mzs[prevPeakIndex.Value];
                            for (int deconvSpecIndex = 0; deconvSpecIndex < deconvIndices.Count; ++deconvSpecIndex)
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
                                // Add this peak
                                // TODO: This is a hack until Thermo fixes intensity reporting for multiplexed spectra
                                // deconvIntensities[deconvSpecIndex][prevPeakIndex.Value] = deconvolvedPeakInt * 
                                //    IsoWindowsPerScan;
                                deconvIntensities[deconvSpecIndex][prevPeakIndex.Value] = deconvolvedPeakInt;
                                deconvMzs[deconvSpecIndex][prevPeakIndex.Value] = originalPeakMz;
                            }
                        }
                    }
                    binsConsidered = 0;
                    for (int i = 0; i < deconvIndices.Count; ++i)
                    {
                        peakCorrections[i] = 0.0;
                        numBins[i] = 0;
                    }
                }
                prevPeakIndex = peakIndex;
                if (binIndicesList.BinarySearch(bin) >= 0)
                {
                    // This bin (transition) is in one of the deconvolved spectra
                    ++binsConsidered;
                    // For each deconvolved spectrum...
                    for (int deconvSpecIndex = 0; deconvSpecIndex < deconvIndices.Count; ++deconvSpecIndex)
                    {
                        // TODO: Figure out why this fires during the demultiplexing test
                        // Debug.Assert(isoIndices.Count == 1);
                        //var isoIndex = isoIndices[0];
                        var deconvIndex = deconvIndices[deconvSpecIndex];
                        // Find if this bin is in this deconvolved spectrum
                        //if (!_spectrumProcessor.TransBinner.BinInPrecursor(bin, isoIndex)) continue;
                        int tranIndex = binToDeconvIndex[bin];
                        if (peakSums[tranIndex] == 0.0) continue;
                        peakCorrections[deconvSpecIndex] +=
                            _deconvBlock.Solution.Matrix[deconvIndex, tranIndex] / peakSums[tranIndex];
                        numBins[deconvSpecIndex]++;
                    }
                }
            }
            // Handle the end case
            if (binsConsidered > 0 && prevPeakIndex.HasValue)
            {
                var originalPeakIntensity = originalSpectrum.Intensities[prevPeakIndex.Value];
                var originalPeakMz = originalSpectrum.Mzs[prevPeakIndex.Value];
                for (int deconvSpecIndex = 0; deconvSpecIndex < deconvIndices.Count; ++deconvSpecIndex)
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
                    // Add this peak
                    // TODO: This is a hack until Thermo fixes intensity reporting for multiplexed spectra
                    // deconvIntensities[deconvSpecIndex][prevPeakIndex.Value] = deconvolvedPeakInt * IsoWindowsPerScan;
                    deconvIntensities[deconvSpecIndex][prevPeakIndex.Value] = deconvolvedPeakInt;
                    deconvMzs[deconvSpecIndex][prevPeakIndex.Value] = originalPeakMz;
                }
            }
        }

        public MsDataSpectrum[] GetDeconvolvedSpectra(int index, MsDataSpectrum originalSpectrum)
        {
            // Make sure this index is within range
            if (index < 0 || index > _file.SpectrumCount)
            {
                throw new IndexOutOfRangeException(
                    string.Format(
                        Resources
                            .OverlapDemultiplexer_GetDeconvolvedSpectra_OverlapDemultiplexer__GetDeconvolvedSpectra__Index__0__is_out_of_range,
                        index));
            }

            // If the first time called, initialize the cache
            if (!_initialized)
                InitializeFile();

            // Figure out the maximum possible start/stop indices in msMsSpectra for spectra needed
            // First, find the index in msMsSpectra containing the scan queried (index)
            int startIndex, endIndex;
            ScanCached processedSpec;
            if (!_spectrumProcessor.TryGetSpectrum(index, out processedSpec))
            {
                processedSpec = _spectrumProcessor.AddSpectrum(index, _file.GetSpectrum(index));
            }

            int[] isoIndices = processedSpec.IsoIndices;
            int[] deconvIndices = processedSpec.DeconvIndices;
            if (originalSpectrum == null)
                originalSpectrum = _file.GetSpectrum(index);
            FindStartStop(index, originalSpectrum, out startIndex, out endIndex);

            MsDataSpectrum[] returnSpectra;

            var binIndicesList = _spectrumProcessor.BinIndicesFromIsolationWindows(isoIndices);
            if (binIndicesList.Count == 0)
            {
                // None of the precursors for this spectrum overlap with any of the
                // precursors in the document
                // Return a blank spectrum
                returnSpectra = new MsDataSpectrum[1];
                returnSpectra[0] = originalSpectrum;
                returnSpectra[0].Intensities = new double[0];
                returnSpectra[0].Mzs = new double[0];
                return returnSpectra;
            }

            // Get each spectrum, with a predicate for isolation windows
            _deconvBlock.Clear();
            for (int i = startIndex; i <= endIndex; ++i)
            {
                int scanIndex = _msMsSpectra[i];
                IEnumerable<double> spectrumData;
                double[] mask;
                // Add only transitions contained in the document in the precursor
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
            // Add regularization rows and computationatl shortcuts if necessary
            var deconvBlockProcessed = PreprocessDeconvBlock(deconvIndices);
            // Pass the deconv block to the demultiplexer
            _solver.Solve(deconvBlockProcessed);
            // Add in missing rows/columns and unpack computational shortcuts if necessary
            PostprocessDeconvBlock(deconvBlockProcessed,deconvIndices);
            int numDeconvolvedTrans = _deconvBlock.NumTransitions;
            double[] peakSums = new double[numDeconvolvedTrans];
            var binToDeconvIndex = new Dictionary<int, int>(numDeconvolvedTrans);
            int numBinIndices = binIndicesList.Count;
            for (int i = 0; i < numBinIndices; ++i)
                binToDeconvIndex[binIndicesList[i]] = i;
            // For each transition, calculate its intensity summed over 
            // each deconvolved spectrum 
            for (int transIndex = 0; transIndex < numDeconvolvedTrans; ++transIndex)
            {
                double intensitySum = 0.0;
                foreach (var deconvIndex in deconvIndices)
                    intensitySum += _deconvBlock.Solution.Matrix[deconvIndex, transIndex];
                peakSums[transIndex] = intensitySum;
            }

            // Initialize arrays to store deconvolved spectra
            double[][] deconvIntensities = new double[deconvIndices.Length][];
            double[][] deconvMzs = new double[deconvIndices.Length][];
            for (int i = 0; i < deconvIndices.Length; ++i)
            {
                deconvIntensities[i] = new double[originalSpectrum.Mzs.Length];
                deconvMzs[i] = new double[originalSpectrum.Mzs.Length];
            }
            try
            {
                var queryBinEnumerator = _spectrumProcessor.TransBinner.BinsFromValues(originalSpectrum.Mzs, true);
                CorrectPeakIntensities(ref originalSpectrum, binIndicesList, isoIndices, deconvIndices, 
                    peakSums, binToDeconvIndex, queryBinEnumerator, ref deconvIntensities, ref deconvMzs);
            }
            catch (InvalidOperationException)
            {
                // There are no peaks that fall in any of the transition bins
                // Return a blank spectrum
                returnSpectra = new MsDataSpectrum[1];
                returnSpectra[0] = originalSpectrum;
                returnSpectra[0].Intensities = new double[0];
                returnSpectra[0].Mzs = new double[0];
                return returnSpectra;
            }
            returnSpectra = new MsDataSpectrum[deconvIndices.Length];
            for (int deconvSpecIndex = 0; deconvSpecIndex < deconvIndices.Length; ++deconvSpecIndex)
            {
                var deconvSpec = new MsDataSpectrum
                {
                    Intensities = deconvIntensities[deconvSpecIndex],
                    Mzs = deconvMzs[deconvSpecIndex],
                    Precursors = new MsPrecursor[1]
                };
                var deconvRegion = _isoMapper.GetDeconvRegion(deconvIndices[deconvSpecIndex]);
                var precursor = new MsPrecursor
                    {
                        PrecursorMz = deconvRegion.CenterMz,
                        IsolationWindowLower =  deconvRegion.Width/2.0,
                        IsolationWindowUpper =  deconvRegion.Width/2.0
                    };
                deconvSpec.Precursors[0] = precursor;
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
        public MatrixWrap BinnedData { get; private set; }

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
            // Update Masks
            Masks.Matrix.SetRow(Masks.NumRows, mask);
            Masks.SetNumCols(mask.Length);
            Masks.IncrementNumRows();

            // Update BinnedData
            int colNum = 0;
            foreach (var dataVal in data)
                BinnedData.Matrix[BinnedData.NumRows, colNum++] = dataVal;

            BinnedData.IncrementNumRows();
            BinnedData.SetNumCols(colNum);

            // Update Solution
            Solution.SetNumCols(colNum);
        }
    }

    public abstract class AbstractIsoWindowMapper : IIsoWinMapper
    {
        protected readonly Dictionary<long, int> _isolationWindowD;
        protected readonly List<IsoWin> _isolationWindows;
        protected readonly List<MsPrecursor> _precursors;
        protected int _lastSort;   // Last length of the iso width for which sort happened
        protected readonly List<KeyValuePair<double, int>> _isoWindowsSorted;
        protected double? _windowWidth;
        protected readonly List<DeconvolutionRegion> _deconvRegions;

        public double WindowWidth
        {
            set { _windowWidth = value; }
        }
        public int NumWindows
        {
            get { return _isolationWindows.Count; }
        }

        public int NumDeconvRegions
        {
            get { return _deconvRegions.Count; }
        }

        public DeconvolutionRegion GetDeconvRegion(int index)
        {
            return new DeconvolutionRegion(_deconvRegions[index]);
        }

        protected AbstractIsoWindowMapper()
        {
            _isolationWindowD = new Dictionary<long, int>();
            _isolationWindows = new List<IsoWin>();
            _isoWindowsSorted = new List<KeyValuePair<double, int>>();
            _precursors = new List<MsPrecursor>();
            _deconvRegions = new List<DeconvolutionRegion>();
        }

        public int Add(IEnumerable<MsPrecursor> precursors)
        {
            int countAdded = 0;
            foreach (var precursor in precursors)
            {
                double? isolationCenter = precursor.IsolationMz;
                double? isolationLower = precursor.IsolationWindowLower;
                double? isolationUpper = precursor.IsolationWindowUpper;

                if (!isolationCenter.HasValue)
                {
                    throw new ArgumentException(Resources.OverlapIsolationWindowMapper_Add_OverlapIsolationWindowMapper__Tried_to_add_an_isolation_window_with_no_center);
                }
                if (!isolationLower.HasValue)
                {
                    throw new ArgumentException(Resources.OverlapIsolationWindowMapper_Add_OverlapIsolationWindowMapper__Tried_to_add_an_isolatio_window_with_no_lower_boundary);
                }
                if (!isolationUpper.HasValue)
                {
                    throw new ArgumentException(Resources.OverlapIsolationWindowMapper_Add_OverlapIsolationWindowMapper__Tried_to_add_an_isolation_window_with_no_upper_boundary);
                }
                   
                long hash = IsoWindowHasher.Hash(isolationCenter.Value);
                if (_isolationWindowD.ContainsKey(hash))
                    continue;
                var isoMz = isolationCenter.Value;
                var isoMzLeft = isoMz - isolationLower.Value;
                var isoMzRight = isoMz + isolationUpper.Value;
                _isolationWindows.Add(new IsoWin(isoMzLeft, isoMzRight, isoMz));
                _isoWindowsSorted.Add(new KeyValuePair<double, int>(isoMz, _isolationWindows.Count - 1));
                _isolationWindowD[hash] = _isolationWindows.Count - 1;
                _precursors.Add(precursor);
                countAdded++;
            }
            return countAdded;
        }

        // Derived Types must each define their own way of computing deconvolution regions
        public abstract void DetermineDeconvRegions();

        public bool TryGetWindowIndex(double isolationWindow, out int index)
        {
            long hash = IsoWindowHasher.Hash(isolationWindow);
            return _isolationWindowD.TryGetValue(hash, out index);
        }

        public void GetWindowMask(MsDataSpectrum s, out int[] isolationIndices, out int[] deconvIndices, 
            ref double[] mask)
        {
            if (!_deconvRegions.Any())
            {
                DetermineDeconvRegions();
            }
            var isolationIndicesList = new List<int>();
            var deconvIndicesList = new List<int>();
            if (mask.Length!=NumDeconvRegions)
                mask = new double[NumDeconvRegions];
            else
            {
                for (int i = 0; i < mask.Length; ++i)
                {
                    mask[i] = 0.0;
                }   
            }
            foreach (var precursor in s.Precursors)
            {
                var precursorMz = precursor.IsolationMz.GetValueOrDefault();
                int isoIndex;
                if (!TryGetWindowIndex(precursorMz, out isoIndex))
                {
                    throw new ArgumentException(
                        string.Format(Resources.IsolationWindowMapper_GetWindowMask_IsolationWindowMapper_Tried_to_get_a_window_mask_for__0__a_spectrum_with_previously_unobserved_isolation_windows,
                                      precursorMz));
                }
                isolationIndicesList.Add(isoIndex);
                foreach (var deconvRegion in _isolationWindows[isoIndex].DeconvRegions)
                {
                    var oIndex = deconvRegion.Id;
                    mask[oIndex] = 1.0;
                    deconvIndicesList.Add(oIndex);
                }
            }
            deconvIndices = deconvIndicesList.ToArray();
            isolationIndices = isolationIndicesList.ToArray();
        }

        public IsoWin GetIsolationWindow(int isoIndex)
        {
            if (isoIndex < 0 || isoIndex > _isolationWindows.Count)
            {
                throw new ArgumentOutOfRangeException(
                    string.Format(Resources.IsolationWindowMapper_GetIsolationWindow_IsolationWindowMapper_isoIndex__0__out_of_range,
                                  isoIndex));
            }
            return _isolationWindows[isoIndex];
        }

        public MsPrecursor GetPrecursor(int isoIndex)
        {
            if (isoIndex < 0 || isoIndex > _precursors.Count)
            {
                throw new ArgumentOutOfRangeException(
                    string.Format(Resources.IsolationWindowMapper_GetIsolationWindow_IsolationWindowMapper_isoIndex__0__out_of_range,
                                  isoIndex));
            }
            return _precursors[isoIndex];
        }

        // DetermineDeconvRegions must be called before calling this
        // in current code this is called when file is initialized
        public bool TryGetAllWindowsFromMz(double mz, out int[] windowIndices)
        {
            var windowIndicesList = new List<int>();
            long mzHash = IsoWindowHasher.Hash(mz);
            DeconvolutionRegion deconvRegionForMz = null;
            foreach (var deconvRegion in _deconvRegions)
            {
                if (deconvRegion.Start < mzHash && mzHash < deconvRegion.Stop)
                {
                    // Should only ever see one of these
                    if (deconvRegionForMz != null)
                    {
                        throw new Exception(Resources.Demultiplexer_GetDeconvRegionsForMz_TheIsolationSchemeIsInconsistentlySpecified);
                    }
                    deconvRegionForMz = deconvRegion;
                }
            }
            if (deconvRegionForMz == null)
            {
                windowIndices = windowIndicesList.ToArray();
                return false;
            }
            for (int i = 0 ; i < _isolationWindows.Count ; ++i)
            {
                IsoWin isoWin = _isolationWindows[i];
                if (isoWin.DeconvRegions.Contains(deconvRegionForMz))
                {
                    windowIndicesList.Add(i);
                }
            }
            windowIndices = windowIndicesList.ToArray();
            return windowIndices.Length != 0;
        }

        /// <summary>
        /// Find the isolation window containing the query m/z
        /// </summary>
        /// <returns>(True/False) Was a window containing this m/z found?</returns>
        public bool TryGetWindowFromMz(double mz, out int windowIndex)
        {
            var comparer = new KvpKeyComparer();
            if (_lastSort < _isoWindowsSorted.Count)
            {
                _isoWindowsSorted.Sort(comparer);
                _lastSort = _isoWindowsSorted.Count;
            }

            var queryWindow = new KeyValuePair<double, int>(mz, 0);
            int index = _isoWindowsSorted.BinarySearch(queryWindow, comparer);

            if (index >= 0)
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
                // Query m/z is between two isolation window centers, test each one
                var leftWindow = nextGreatestWindow == 0
                                        ? new KeyValuePair<double, int>(double.MinValue, -1)
                                        : _isoWindowsSorted[nextGreatestWindow - 1];
                if (mz <= leftWindow.Key + _windowWidth / 2.0)
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
            // The m/z does not fall in any of the windows
            windowIndex = -1;
            return false;
        }
    }

    public sealed class IsoWin
    {
        public long Start { get; private set; }
        public long Stop { get; private set; }
        public double Center { get; private set; }
        public List<DeconvolutionRegion> DeconvRegions { get; private set; }

        public double StartMz { get { return IsoWindowHasher.UnHash(Start); } }
        public double StopMz { get { return IsoWindowHasher.UnHash(Stop); } }

        public IsoWin(double start, double end, double center)
        {
            Start = IsoWindowHasher.Hash(start);
            Stop = IsoWindowHasher.Hash(end);
            Center = center;
            DeconvRegions = new List<DeconvolutionRegion>();
        }

        public bool Contains(DeconvolutionRegion ovlp)
        {
            return ovlp.Start >= Start && ovlp.Stop <= Stop;
        }
    }

    public sealed class DeconvolutionRegion
    {
        public long Start { get; private set; }
        public long Stop { get; private set; }
        public int Id { get; private set; }

        public double StartMz { get { return IsoWindowHasher.UnHash(Start); } }
        public double StopMz { get { return IsoWindowHasher.UnHash(Stop); } }

        internal DeconvolutionRegion(long start, long stop, int id)
        {
            Start = start;
            Stop = stop;
            Id = id;
        }

        internal double CenterMz
        {
            get { return (StartMz + StopMz)/2.0; }
        }

        internal double Width
        {
            get { return StopMz - StartMz; }
        }

        public DeconvolutionRegion(DeconvolutionRegion deconvRegion)
        {
            Start = deconvRegion.Start;
            Stop = deconvRegion.Stop;
            Id = deconvRegion.Id;
        }
    }

    public struct ScanCached
    {
        public ScanCached(double[] mask, double[] data, int[] isoIndices, int[] deconvIndices) : this()
        {
            Mask = mask;
            Data = data;
            IsoIndices = isoIndices;
            DeconvIndices = deconvIndices;
        }

        public double[] Mask { get; set; }
        public double[] Data { get; set; }
        public int[] IsoIndices { get; set; }
        public int[] DeconvIndices { get; set; }
    }

    public sealed class SpectrumProcessor
    {
        private readonly int _cacheSize;
        private readonly AbstractIsoWindowMapper _isoMapper;
        private readonly TransitionBinner _transBinner;
        private readonly Dictionary<int, ScanCached> _cache;
        private readonly Queue<int> _scanStack;
        private readonly ArrayPool<double> _maskPool;
        private readonly ArrayPool<double> _binnedDataPool;

        public TransitionBinner TransBinner { get { return _transBinner; } }

        public SpectrumProcessor(int cacheSize,
            AbstractIsoWindowMapper isoMapper,
            SpectrumFilter filter)
            :this(cacheSize, isoMapper, new TransitionBinner(filter, isoMapper))
        {
        }

        public SpectrumProcessor(int cacheSize, AbstractIsoWindowMapper isoMapper, 
            TransitionBinner transBinner)
        {
            _cacheSize = cacheSize;
            _isoMapper = isoMapper;
            _transBinner = transBinner;
            _cache = new Dictionary<int, ScanCached>();
            _scanStack = new Queue<int>(cacheSize);
            _binnedDataPool = new ArrayPool<double>(_transBinner.NumBins, cacheSize);
            _maskPool = new ArrayPool<double>(isoMapper.NumDeconvRegions, cacheSize);
        }

        public bool HasScan(int scanNum)
        {
            return _cache.ContainsKey(scanNum);
        }

        public int MaxTransitions(int numWindows)
        {
            return _transBinner.MaxTransitions(numWindows);
        }

        public List<int> BinIndicesFromIsolationWindows(int[] isoIndices)
        {
            var returnIndices = _transBinner.BinsForPrecursors(isoIndices).ToList();
            returnIndices.Sort();
            return returnIndices;
        }

        public ScanCached AddSpectrum(int scanNum, MsDataSpectrum s)
        {
            if (_scanStack.Count >= _cacheSize)
                RemoveLast();

            int[] isoIndices;
            double[] mask = _maskPool.Get();
            int[] deconvIndices;
            _isoMapper.GetWindowMask(s, out isoIndices, out deconvIndices, ref mask);

            // Update _isoScans
            // Normally this scan will be of greater scan number than any previous
            double[] binnedData = _binnedDataPool.Get();
            _transBinner.BinData(s.Mzs, s.Intensities, ref binnedData);
            // Add this data to the cache
            var scanCache = new ScanCached(mask, binnedData, isoIndices, deconvIndices);
            _cache.Add(scanNum, scanCache); 
            _scanStack.Enqueue(scanNum);
            return scanCache;
        }

        private void RemoveLast()
        {
            int scanToRemove = _scanStack.Dequeue();
            var removed = _cache[scanToRemove];
            _binnedDataPool.Free(removed.Data);
            _maskPool.Free(removed.Mask);
            _cache.Remove(scanToRemove);
        }

        public bool TryGetSpectrum(int scanIndex, out ScanCached spectrumData)
        {
            return _cache.TryGetValue(scanIndex, out spectrumData);
        }

        public bool TryGetFilteredSpectrumData(int scanIndex, IList<int> binIndices,
                                            out IEnumerable<double> spectrumData,
                                            out double[] mask)
        {
            ScanCached spectrum;
            if (!TryGetSpectrum(scanIndex, out spectrum))
            {
                spectrumData = null;
                mask = null;
                return false;
            }
            spectrumData = binIndices.Select(i => spectrum.Data[i]);
            mask = spectrum.Mask;
            return true;
        }
    }

    public sealed class TransitionInfo : IComparable<TransitionInfo>
    {
        public TransitionInfo(double startMz, double endMz, int[] precursorIndices)
        {
            StartMz = startMz;
            EndMz = endMz;
            PrecursorIndices = precursorIndices;
        }

        public double StartMz { get; private set; }
        public double EndMz { get; private set; }
        public int[] PrecursorIndices { get; private set; }

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

    public sealed class TransitionBinner
    {
        private List<TransitionInfo> _allTransitions;
        private HashSet<int>[] _precursorTransitions;
        private double _maxTransitionWidth;
        private IIsoWinMapper _isoMapper;

        public double MinValue { get; private set; }
        public double MaxValue { get; private set; }
        public int MinBin { get; private set; }
        public int MaxBin { get; private set; }
        public int NumBins { get; private set; }

        public TransitionBinner(SpectrumFilter filter, IIsoWinMapper isoMapper)
        {
            InitializeVariables(isoMapper);
            // Initialize the transitions using the filter information
            var filterPairs = filter.FilterPairs;
            foreach (var filterPair in filterPairs)
            {
                var precursorMz = filterPair.Q1;
                int[] precWindowIndices;
                // This precursor is outside of the range of mapped precursor
                // isolation windows
                if (!_isoMapper.TryGetAllWindowsFromMz(precursorMz, out precWindowIndices))
                    continue;
                for (int i = 0; i < filterPair.ArrayQ3.Length; ++i)
                {
                    AddTransition(precWindowIndices, filterPair.ArrayQ3[i], filterPair.ArrayQ3Window[i]);
                }
            }
            // Populate _allTransitions and precursor->transition map
            PopulatePrecursorToTransition();
        }

        private void InitializeVariables(IIsoWinMapper isoMapper)
        {
            // Initialize all variables
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
            // Sorts by starting m/z
            _allTransitions.Sort();
            for (int i = 0; i < _allTransitions.Count; ++i)
            {
                var precIndices = _allTransitions[i].PrecursorIndices;
                foreach (var precIndex in precIndices)
                {
                    _precursorTransitions[precIndex].Add(i);   
                }
            }
            MinBin = 0;
            MaxBin = _allTransitions.Count - 1;
            NumBins = _allTransitions.Count;
        }

        private void AddTransition(int[] precursorIndices, double windowCenter, double windowWidth)
        {
            var windowStart = windowCenter - windowWidth / 2.0;
            var windowEnd = windowCenter + windowWidth / 2.0;
            if (windowWidth > _maxTransitionWidth)
                _maxTransitionWidth = windowWidth;
            if (windowStart < MinValue)
                MinValue = windowStart;
            if (windowEnd > MaxValue)
                MaxValue = windowEnd;
            _allTransitions.Add(new TransitionInfo(windowStart, windowEnd, precursorIndices));
        }

        public void BinData(double[] mzVals, double[] intensityVals, ref double[] binnedData)
        {
            if (mzVals.Length != intensityVals.Length)
                throw new IndexOutOfRangeException(String.Format(Resources.TransitionBinner_BinData_mz_and_intensity_arrays_dont_match_in_length__0__1__, mzVals.Length, intensityVals.Length));
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

        public IEnumerable<KeyValuePair<int, int>> BinsFromValues(double[] queries, bool sorted)
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
                for (; binStartIndex < NumBins; binStartIndex++)
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
                        yield return new KeyValuePair<int, int>(queryIndex, binIndex);
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
            if (MinBin > queryBin || queryBin > MaxBin)
            {
                throw new IndexOutOfRangeException(
                    string.Format(Resources.TransitionBinner_TransInfoFromBin_TransitionBinner_TransInfoFromBin_Index_out_of_range__0__,
                                  queryBin));
            }
            return _allTransitions[queryBin];
        }

        public ICollection<int> BinsForPrecursors(int[] precursors)
        {
            var validBins = new HashSet<int>();

            foreach (int t in precursors)
            {
                if (0 > t || t > _precursorTransitions.Length)
                {
                    throw new IndexOutOfRangeException(
                        string.Format(Resources.TransitionBinner_BinsForPrecursors_TransitionBinner_BinsForPrecursors_precursor_index_out_of_range__0__,
                                      t));
                }
                validBins.UnionWith(_precursorTransitions[t]);
            }

            return validBins;
        }

        public bool BinInPrecursor(int bin, int precursor)
        {
            if (0 > precursor || precursor > _precursorTransitions.Length)
            {
                throw new IndexOutOfRangeException(
                    string.Format(Resources.TransitionBinner_BinInPrecursor_TransitionBinner_BinInPrecursor_precursor_index_out_of_range__0__,
                                  precursor));
            }
            return _precursorTransitions[precursor].Contains(bin);
        }

        public int MaxTransitions(int numWindows)
        {
            if (0 >= numWindows || numWindows > _precursorTransitions.Length)
            {
                throw new ArgumentOutOfRangeException(
                    string.Format(Resources.TransitionBinner_MaxTransitions_TransitionBinner_MaxTransitionsasked_for_transitions_from_too_manyprecursors__0__,
                                  numWindows));
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

        public TransitionBinner(List<double> precursors, List<KeyValuePair<double, double>> transitions,
             AbstractIsoWindowMapper isoMapper)
        {
            InitializeVariables(isoMapper);
            for (int i = 0; i < precursors.Count; ++i)
            {
                var precursorMz = precursors[i];
                int[] precWindowIndices;
                if (!_isoMapper.TryGetAllWindowsFromMz(precursorMz, out precWindowIndices))
                    continue;
                var transCenter = transitions[i].Key;
                var transWidth = transitions[i].Value;
                AddTransition(precWindowIndices, transCenter, transWidth);
            }
            PopulatePrecursorToTransition();
        }

        #endregion
    }
}

