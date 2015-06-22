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
        protected SpectrumProcessor _spectrumProcessor;
        protected readonly List<int> _msMsSpectra;
        protected int _msMsIndex;
        protected ILsSolver _solver;
        protected AbstractDeconvSolverHandler _deconvHandler;

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

        protected void InitializeFile()
        {
            if (!AnalyzeFile())
                throw new IOException(Resources.OverlapDemultiplexer_InitializeFile_OverlapDemultiplexer_InitializeFile_Improperly_formed_overlap_multiplexing_file);
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
                    if (_isoMapper.NumWindows > 1000)
                    {
                        throw new InvalidDataException(Resources.AbstractDemultiplexer_AnalyzeFile_Isolation_scheme_is_set_to_multiplexing_but_file_does_not_appear_to_contain_multiplexed_acquisition_data_);
                    }
                }
                var scanNumber = _msMsSpectra[i];
                MsPrecursor[] precs = _file.GetPrecursors(scanNumber);
                _isoMapper.Add(precs, _filter);
                if (!nIsoWindowsPerScan.HasValue)
                {
                    nIsoWindowsPerScan = precs.Length;
                }
            }
            if (!nIsoWindowsPerScan.HasValue)
                return false;
            IsoWindowsPerScan = nIsoWindowsPerScan.Value;
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
                                   out int endMsMsIndex, out int centerIndex)
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
            centerIndex = _msMsIndex;
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
            HashSet<int> binIndicesSet, double[] peakSums, IEnumerable<KeyValuePair<int, int>> queryBinEnumerator, 
            AbstractDeconvSolverHandler deconvHandler, ref double[][] deconvIntensities, ref double[][] deconvMzs)
        {
            var originalDeconvHandler = _deconvHandler;
            _deconvHandler = deconvHandler;
            List<int> binIndicesList = binIndicesSet.ToList();
            try
            {
                CorrectPeakIntensities(ref originalSpectrum, binIndicesList, peakSums,
                                       queryBinEnumerator, ref deconvIntensities, ref deconvMzs);
            }
            finally
            {
                _deconvHandler = originalDeconvHandler;
            }
        }
        #endregion

        protected void CorrectPeakIntensities(ref MsDataSpectrum originalSpectrum,
                                              List<int> binIndicesList,
                                              IList<double> peakSums,
                                              IEnumerable<KeyValuePair<int, int>> queryBinEnumerator,
                                              ref double[][] deconvIntensities,
                                              ref double[][] deconvMzs)
        {
            // Will do binary search on this
            binIndicesList.Sort();
            int numDeconvolvedTrans = peakSums.Count;
            var reverseBinIndicesList = new Dictionary<int, int>(numDeconvolvedTrans);
            int numBinIndices = binIndicesList.Count;
            for (int i = 0; i < numBinIndices; ++i)
                reverseBinIndicesList[binIndicesList[i]] = i;
            int numDeconvSpectra = deconvIntensities.Length;
            for (int i = 0; i < numDeconvSpectra; ++i)
            {
                originalSpectrum.Intensities.CopyTo(deconvIntensities[i], 0);
                originalSpectrum.Mzs.CopyTo(deconvMzs[i], 0);
                for (int j = 0; j < originalSpectrum.Intensities.Length; ++j)
                {
                    deconvIntensities[i][j] = deconvIntensities[i][j]/deconvIntensities.Length;
                }
            }
            int? prevPeakIndex = null;

            double[] peakCorrections = new double[numDeconvSpectra];
            int[] numBins = new int[numDeconvSpectra];
            for (int i = 0; i < numDeconvSpectra; ++i)
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
                            for (int deconvSpecIndex = 0; deconvSpecIndex < numDeconvSpectra; ++deconvSpecIndex)
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
                    for (int i = 0; i < numDeconvSpectra; ++i)
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
                    for (int deconvSpecIndex = 0; deconvSpecIndex < numDeconvSpectra; ++deconvSpecIndex)
                    {
                        // TODO: Figure out why this fires during the demultiplexing test
                        // Debug.Assert(isoIndices.Count == 1);
                        //var isoIndex = isoIndices[0];
                        // Find if this bin is in this deconvolved spectrum
                        //if (!_spectrumProcessor.TransBinner.BinInPrecursor(bin, isoIndex)) continue;
                        int tranIndex = reverseBinIndicesList[bin];
                        if (peakSums[tranIndex] == 0.0) continue;
                        peakCorrections[deconvSpecIndex] +=
                            _deconvHandler.Solution.Matrix[deconvSpecIndex, tranIndex] / peakSums[tranIndex];
                        numBins[deconvSpecIndex]++;
                    }
                }
            }
            // Handle the end case
            if (binsConsidered > 0 && prevPeakIndex.HasValue)
            {
                var originalPeakIntensity = originalSpectrum.Intensities[prevPeakIndex.Value];
                var originalPeakMz = originalSpectrum.Mzs[prevPeakIndex.Value];
                for (int deconvSpecIndex = 0; deconvSpecIndex < numDeconvSpectra; ++deconvSpecIndex)
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
                    string.Format("OverlapDemultiplexer: GetDeconvolvedSpectra: Index {0} is out of range", index)); // Not L10N
            }

            // If the first time called, initialize the cache
            if (!_initialized)
                InitializeFile();

            // Figure out the maximum possible start/stop indices in msMsSpectra for spectra needed
            // First, find the index in msMsSpectra containing the scan queried (index)
            int startIndex, endIndex, centerIndex;
            ScanCached processedSpec;
            if (!_spectrumProcessor.TryGetSpectrum(index, out processedSpec))
            {
                processedSpec = _spectrumProcessor.AddSpectrum(index, _file.GetSpectrum(index));
            }

            int[] deconvIndices = processedSpec.DeconvIndices;
            if (originalSpectrum == null)
                originalSpectrum = _file.GetSpectrum(index);
            FindStartStop(index, originalSpectrum, out startIndex, out endIndex, out centerIndex);

            MsDataSpectrum[] returnSpectra;

            var binIndicesList = _spectrumProcessor.BinIndicesFromDeconvWindows(deconvIndices);
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

            _deconvHandler.Clear();
            _deconvHandler.SetDeconvIndices(deconvIndices);
            _deconvHandler.CurrentScan = centerIndex - startIndex;
            for (int i = startIndex; i <= endIndex; ++i)
            {
                int scanIndex = _msMsSpectra[i];
                IEnumerable<double> spectrumData;
                double[] mask;
                double? retentionTime;
                // Add only transitions contained in the document in the precursor
                // windows for the spectrum
                if (!_spectrumProcessor.TryGetFilteredSpectrumData(scanIndex, binIndicesList,
                                                                   out spectrumData, out mask,
                                                                   out retentionTime))
                {
                    _spectrumProcessor.AddSpectrum(scanIndex, _file.GetSpectrum(scanIndex));
                    _spectrumProcessor.TryGetFilteredSpectrumData(scanIndex, binIndicesList,
                                                                  out spectrumData, out mask,
                                                                  out retentionTime);
                }
                _deconvHandler.AddScan(mask, spectrumData, scanIndex, retentionTime);
            }

            _deconvHandler.Solve(_solver);
            int numDeconvolvedTrans = _deconvHandler.NumTransitions;
            double[] peakSums = new double[numDeconvolvedTrans];
            // For each transition, calculate its intensity summed over 
            // each deconvolved spectrum 
            for (int transIndex = 0; transIndex < numDeconvolvedTrans; ++transIndex)
            {
                double intensitySum = 0.0;
                for (int j = 0; j < deconvIndices.Count(); ++j)
                    intensitySum += _deconvHandler.Solution.Matrix[j, transIndex];
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
                CorrectPeakIntensities(ref originalSpectrum, binIndicesList, peakSums,
                                       queryBinEnumerator, ref deconvIntensities, ref deconvMzs);
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

    public abstract class AbstractDeconvSolverHandler
    {
        public int MaxScans { get; private set; }
        public int MaxTransitions { get; private set; }
        public int NumScans { get { return Masks.NumRows; } }
        public int NumTransitions { get { return BinnedData.NumCols; } }
        public int NumDeconvWindows { get; private set; }
        public MatrixWrap Masks { get; private set; }
        public MatrixWrap BinnedData { get; private set; }
        public MatrixWrap Solution { get; private set; }

        public IList<int> DeconvIndices { get; private set; }
        public int CurrentScan { get; set; } 
        public IList<int> ScanNumbers { get; private set; }
        public IList<double?> ScanTimes { get; private set; }
        protected DeconvBlock _deconvBlock;

        protected AbstractDeconvSolverHandler(int numDeconvWindows, int maxScans, int maxTransitions)
        {
            NumDeconvWindows = numDeconvWindows;
            Masks = new MatrixWrap(maxScans, numDeconvWindows);
            BinnedData = new MatrixWrap(maxScans, maxTransitions);
            Solution = new MatrixWrap(numDeconvWindows, maxTransitions);
            Solution.SetNumRows(0);
            MaxScans = maxScans;
            MaxTransitions = maxTransitions;
            DeconvIndices = new List<int>();
            ScanNumbers = new List<int>();
            ScanTimes = new List<double?>();
        }

        public void Clear()
        {
            Masks.Reset();
            BinnedData.Reset();
            Solution.Reset();
            DeconvIndices = new List<int>();
            ScanNumbers.Clear();
            ScanTimes.Clear();
            Masks.SetNumCols(NumDeconvWindows);
        }

        public void SetDeconvIndices(IList<int> deconvIndices)
        {
            DeconvIndices = deconvIndices;
            Solution.SetNumRows(deconvIndices.Count);
        }

        public void AddScan(double[] mask, IEnumerable<double> data, int scanNum, double? scanTime)
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

            // Update scan info
            ScanNumbers.Add(scanNum);
            ScanTimes.Add(scanTime);
        }

        public void Solve(ILsSolver solver)
        {
            BuildDeconvBlock();
            SolveDeconvBlock(solver);
            DeconvBlockToSolution();
        }

        protected abstract void BuildDeconvBlock();

        protected virtual void SolveDeconvBlock(ILsSolver solver)
        {
            solver.Solve(_deconvBlock);
        }

        protected abstract void DeconvBlockToSolution();
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
        protected readonly List<DeconvolutionRegion> _deconvRegions;
        protected bool _deconvRegionsUpdated;

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
            _precursors = new List<MsPrecursor>();
            _deconvRegions = new List<DeconvolutionRegion>();
            _deconvRegionsUpdated = false;
        }

        public int Add(IEnumerable<MsPrecursor> precursors, SpectrumFilter filter)
        {
            int countAdded = 0;
            foreach (var precursor in precursors)
            {
                if (!precursor.IsolationMz.HasValue)
                {
                    throw new ArgumentException(Resources.AbstractIsoWindowMapper_Add_Scan_in_imported_file_appears_to_be_missing_an_isolation_window_center_);
                }

                // use the Skyline document isolation scheme to determine the boundaries of the isolation window
                double isolationCenter = precursor.IsolationMz.Value;
                double? isolationWidth = null;
                filter.CalcDiaIsolationValues(ref isolationCenter, ref isolationWidth);
                if (!isolationWidth.HasValue)
                {
                    throw new ArgumentException(Resources.AbstractIsoWindowMapper_Add_The_isolation_width_for_a_scan_in_the_imported_file_could_not_be_determined_);
                }

                double isoMzLeft = isolationCenter - isolationWidth.Value / 2.0;
                double isoMzRight = isoMzLeft + isolationWidth.Value;
                long hash = IsoWindowHasher.Hash(precursor.IsolationMz.Value);
                if (_isolationWindowD.ContainsKey(hash))
                    continue;
                _isolationWindows.Add(new IsoWin(isoMzLeft, isoMzRight));
                _isolationWindowD[hash] = _isolationWindows.Count - 1;
                _precursors.Add(precursor);
                countAdded++;
                _deconvRegionsUpdated = false;
            }
            return countAdded;
        }

        // Derived Types must each define their own way of computing deconvolution regions
        protected abstract void FindDeconvRegions();
        
        public void DetermineDeconvRegions()
        {
            FindDeconvRegions();
            _deconvRegionsUpdated = true;
        }

        public bool TryGetWindowIndex(double isolationWindow, out int index)
        {
            long hash = IsoWindowHasher.Hash(isolationWindow);
            return _isolationWindowD.TryGetValue(hash, out index);
        }

        public void GetWindowMask(MsDataSpectrum s, out int[] isolationIndices, out int[] deconvIndices, 
            ref double[] mask)
        {
            if (!_deconvRegionsUpdated)
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
                        string.Format(Resources.AbstractIsoWindowMapper_GetWindowMask_Tried_to_get_a_window_mask_for__0___a_spectrum_with_previously_unobserved_isolation_windows__Demultiplexing_requires_a_repeating_cycle_of_isolation_windows_,
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
                    string.Format("IsolationWindowMapper: isoIndex {0} out of range", // Not L10N
                                  isoIndex));
            }
            return _isolationWindows[isoIndex];
        }

        public MsPrecursor GetPrecursor(int isoIndex)
        {
            if (isoIndex < 0 || isoIndex > _precursors.Count)
            {
                throw new ArgumentOutOfRangeException(
                    string.Format("IsolationWindowMapper: isoIndex {0} out of range", // Not L10N
                                  isoIndex));
            }
            return _precursors[isoIndex];
        }

        public bool TryGetIsosForDeconv(int deconvIndex, out int[] windowIndices)
        {
            var windowIndicesList = new List<int>();
            var deconvRegion = _deconvRegions[deconvIndex]; 
            for (int i = 0; i < _isolationWindows.Count; ++i)
            {
                IsoWin isoWin = _isolationWindows[i];
                if (isoWin.DeconvRegions.Contains(deconvRegion))
                {
                    windowIndicesList.Add(i);
                }
            }
            windowIndices = windowIndicesList.ToArray();
            return windowIndices.Length != 0;
        }

        public bool TryGetDeconvFromMz(double mz, out int deconvIndex)
        {
            if (!_deconvRegionsUpdated)
            {
                DetermineDeconvRegions();
            }
            long mzHash = IsoWindowHasher.Hash(mz);
            int i = 0;
            foreach (var deconvRegion in _deconvRegions)
            {
                if (deconvRegion.Start < mzHash && mzHash < deconvRegion.Stop)
                {
                    deconvIndex = i;
                    return true;
                }
                ++i;
            }
            deconvIndex = -1;
            return false;
        }


        public bool TryGetWindowFromMz(double mz, out int windowIndex)
        {
            long mzHash = IsoWindowHasher.Hash(mz);
            int i = 0;
            foreach (var isoWin in _isolationWindows)
            {
                if (isoWin.Start < mzHash && mzHash < isoWin.Stop)
                {
                    windowIndex = i;
                    return true;
                }
                ++i;
            }
            windowIndex = -1;
            return false;
        }

       public int MaxDeconvInIsoWindow()
       {
          return _isolationWindows.Select(isoWin => isoWin.DeconvRegions.Count).Max();
       }
    }

    public sealed class IsoWin
    {
        public long Start { get; private set; }
        public long Stop { get; private set; }
        public List<DeconvolutionRegion> DeconvRegions { get; private set; }

        public double StartMz { get { return IsoWindowHasher.UnHash(Start); } }
        public double StopMz { get { return IsoWindowHasher.UnHash(Stop); } }

        public IsoWin(double start, double end)
        {
            Start = IsoWindowHasher.Hash(start);
            Stop = IsoWindowHasher.Hash(end);
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

        public double CenterMz
        {
            get { return (StartMz + StopMz)/2.0; }
        }

       public double Width
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
        public ScanCached(double[] mask, double[] data, int[] isoIndices, int[] deconvIndices, double? retentionTime = null) : this()
        {
            Mask = mask;
            Data = data;
            IsoIndices = isoIndices;
            DeconvIndices = deconvIndices;
            RetentionTime = retentionTime;
        }

        public double[] Mask { get; set; }
        public double[] Data { get; set; }
        public int[] IsoIndices { get; set; }
        public int[] DeconvIndices { get; set; }
        public double? RetentionTime { get; set; }
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

        public List<int> BinIndicesFromDeconvWindows(int[] deconvIndices)
        {
            var returnIndices = _transBinner.BinsForDeconvWindows(deconvIndices).ToList();
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
            double? retentionTime = s.RetentionTime;
            // Add this data to the cache
            var scanCache = new ScanCached(mask, binnedData, isoIndices, deconvIndices, retentionTime);
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
                                            out double[] mask,
                                            out double? retentionTime)
        {
            ScanCached spectrum;
            if (!TryGetSpectrum(scanIndex, out spectrum))
            {
                spectrumData = null;
                mask = null;
                retentionTime = null;
                return false;
            }
            spectrumData = binIndices.Select(i => spectrum.Data[i]);
            mask = spectrum.Mask;
            retentionTime = spectrum.RetentionTime;
            return true;
        }
    }

    public sealed class TransitionInfo : IComparable<TransitionInfo>
    {
        public TransitionInfo(double startMz, double endMz, int deconvIndex)
        {
            StartMz = startMz;
            EndMz = endMz;
            DeconvIndex = deconvIndex;
        }

        public double StartMz { get; private set; }
        public double EndMz { get; private set; }
        public int DeconvIndex { get; private set; }

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
        private HashSet<int>[] _deconvTransitions; 
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
                int deconvIndex;
                // This precursor is outside of the range of mapped precursor
                // isolation windows
                if (!_isoMapper.TryGetDeconvFromMz(precursorMz, out deconvIndex))
                    continue;
                foreach (var spectrumProductFilter in filterPair.Ms2ProductFilters)
                {
                    AddTransition(deconvIndex, spectrumProductFilter.TargetMz, spectrumProductFilter.FilterWidth);                    
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
            _deconvTransitions = new HashSet<int>[_isoMapper.NumDeconvRegions];
            for (int i = 0; i < _deconvTransitions.Length; ++i)
            {
                _deconvTransitions[i] = new HashSet<int>();
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
                var deconvIndex = _allTransitions[i].DeconvIndex;
                _deconvTransitions[deconvIndex].Add(i);
            }
            MinBin = 0;
            MaxBin = _allTransitions.Count - 1;
            NumBins = _allTransitions.Count;
        }

        private void AddTransition(int deconvIndex, double windowCenter, double windowWidth)
        {
            var windowStart = windowCenter - windowWidth / 2.0;
            var windowEnd = windowCenter + windowWidth / 2.0;
            if (windowWidth > _maxTransitionWidth)
                _maxTransitionWidth = windowWidth;
            if (windowStart < MinValue)
                MinValue = windowStart;
            if (windowEnd > MaxValue)
                MaxValue = windowEnd;
            _allTransitions.Add(new TransitionInfo(windowStart, windowEnd, deconvIndex));
        }

        public void BinData(double[] mzVals, double[] intensityVals, ref double[] binnedData)
        {
            if (mzVals.Length != intensityVals.Length)
                throw new IndexOutOfRangeException(String.Format(Resources.TransitionBinner_BinData_, mzVals.Length, intensityVals.Length));
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

        public ICollection<int> BinsForDeconvWindows(int[] deconvWindows)
        {
            var validBins = new HashSet<int>();

            foreach (int t in deconvWindows)
            {
                if (0 > t || t > _deconvTransitions.Length)
                {
                    throw new IndexOutOfRangeException(
                        string.Format("TransitionBinner: BinsForPrecursors: precursor index out of range: {0}", // Not L10N
                                      t));
                }
                validBins.UnionWith(_deconvTransitions[t]);
            }

            return validBins;
        }

        public bool BinInDeconvWindow(int bin, int deconvWindow)
        {
            if (0 > deconvWindow || deconvWindow > _deconvTransitions.Length)
            {
                throw new IndexOutOfRangeException(
                    string.Format("TransitionBinner: BinInPrecursor: precursor index out of range: {0}", // Not L10N
                                  deconvWindow));
            }
            return _deconvTransitions[deconvWindow].Contains(bin);
        }

        public int MaxTransitions(int numWindows)
        {
            if (0 >= numWindows || numWindows > _deconvTransitions.Length)
            {
                throw new ArgumentOutOfRangeException(
                    string.Format("TransitionBinner: MaxTransitions asked for transitions from too many precursors: {0}", // Not L10N?  Will users see this?
                                  numWindows));
            }
            var transNumsSorted = from prec in _deconvTransitions
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
                int deconvIndices;
                if (!_isoMapper.TryGetDeconvFromMz(precursorMz, out deconvIndices))
                    continue;
                var transCenter = transitions[i].Key;
                var transWidth = transitions[i].Value;
                AddTransition(deconvIndices, transCenter, transWidth);
            }
            PopulatePrecursorToTransition();
        }

        public TransitionInfo TransInfoFromBin(int queryBin)
        {
            if (MinBin > queryBin || queryBin > MaxBin)
            {
                throw new IndexOutOfRangeException( string.Format("TransitionBinner[TransInfoFromBin]: Index out of range: {0}", // Not L10N
                                                    queryBin));
            }
            return _allTransitions[queryBin];
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

        #endregion
    }
}

