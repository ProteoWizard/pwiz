﻿/*
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
using System.Drawing;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    internal abstract class ChromDataProvider : IDisposable
    {
        private readonly int _startPercent;
        private readonly int _endPercent;
        protected readonly IProgressMonitor _loader;

        protected ChromDataProvider(ChromFileInfo fileInfo,
                                    IProgressStatus status,
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

        /// <summary>
        /// Notify the provider that the first pass is complete and determine whether the chromatogram
        /// list needs to be reloaded.
        /// </summary>
        /// <returns>True if the chromatogram list needs to be reloaded</returns>
        public virtual bool CompleteFirstPass()
        {
            return false;  // Do nothing by default.
        }

        public ChromFileInfo FileInfo { get; private set; }

        public IProgressStatus Status { get; protected set; }

        public abstract IEnumerable<ChromKeyProviderIdPair> ChromIds { get; }

        public virtual IResultFileMetadata ResultFileData { get { return null; } }

        public virtual void SetRequestOrder(IList<IList<int>> orderedSets) { }

        public abstract bool GetChromatogram(int id, ChromatogramGroupId chromatogramGroupId, Color color, out ChromExtra extra, out TimeIntensities timeIntensities);

        public abstract double? MaxRetentionTime { get; }

        public abstract double? MaxIntensity { get; }

        public virtual double? TicArea { get { return FileInfo.TicArea; } }

        public abstract eIonMobilityUnits IonMobilityUnits { get; }

        public abstract bool IsProcessedScans { get; }

        public abstract bool IsSingleMzMatch { get; }

        public virtual bool HasMidasSpectra { get { return false; } }

        public virtual bool HasSonarSpectra { get { return false; } }

        public virtual bool IsSrm { get { return FileInfo.IsSrm; } }

        // Used for offering hints to user when document transition polarities don't agree with the raw data
        public abstract bool SourceHasPositivePolarityData { get; }
        public abstract bool SourceHasNegativePolarityData { get; }

        public abstract void ReleaseMemory();

        public abstract void Dispose();
    }


    /// <summary>
    /// Keeps track of the Total Ion Current chromatogram, the base peak chromatogram, and the QC Trace chromatograms.
    /// When this class is used by ChromDataProvider, the chromatograms are identified by the chromatogram index
    /// in the data file.
    /// When this class is used by SpectraChromDataProvider, the chromatograms are identified by an integer which is
    /// an index into the list returned by <see cref="ListChromKeys"/>.
    /// </summary>
    public sealed class GlobalChromatogramExtractor
    {
        private MsDataFileImpl _dataFile;
        private const string TIC_CHROMATOGRAM_ID = @"TIC";
        private const string BPC_CHROMATOGRAM_ID = @"BPC";
        private Dictionary<int, MsDataFileImpl.QcTrace> _qcTracesByChromatogramIndex;


        public GlobalChromatogramExtractor(MsDataFileImpl dataFile)
        {
            _dataFile = dataFile;

            if (dataFile.ChromatogramCount > 0 && dataFile.GetChromatogramId(0, out _) == TIC_CHROMATOGRAM_ID)
                TicChromatogramIndex = 0;
            if (dataFile.ChromatogramCount > 1 && dataFile.GetChromatogramId(1, out _) == BPC_CHROMATOGRAM_ID)
                BpcChromatogramIndex = 1;
            QcTraces = ImmutableList.ValueOfOrEmpty(dataFile.GetQcTraces());
            _qcTracesByChromatogramIndex = QcTraces.ToDictionary(qcTrace => qcTrace.Index);
        }

        public int? TicChromatogramIndex { get; set; }
        public int? BpcChromatogramIndex { get; }

        public ImmutableList<MsDataFileImpl.QcTrace> QcTraces { get; }

        public string GetChromatogramId(int index, out int indexId)
        {
            return _dataFile.GetChromatogramId(index, out indexId);
        }

        public bool GetChromatogram(int index, out float[] times, out float[] intensities)
        {
            if (index == TicChromatogramIndex || index == BpcChromatogramIndex)
            {
                return ReadChromatogramFromDataFile(index, out times, out intensities);
            }

            if (_qcTracesByChromatogramIndex.TryGetValue(index, out var qcTrace))
            {
                times = MsDataFileImpl.ToFloatArray(qcTrace.Times);
                intensities = MsDataFileImpl.ToFloatArray(qcTrace.Intensities);
                return true;
            }

            times = intensities = null;
            return false;
        }
        private bool ReadChromatogramFromDataFile(int chromatogramIndex, out float[] times, out float[] intensities)
        {
            _dataFile.GetChromatogram(chromatogramIndex, out _, out times, out intensities, true);
            return times != null;
        }

        /// <summary>
        /// Returns true if the TIC chromatogram present in the .raw file can be relied on
        /// for the calculation of total MS1 ion current.
        /// </summary>
        public bool IsTicChromatogramUsable()
        {
            if (!TicChromatogramIndex.HasValue)
            {
                return false;
            }

            float[] times;
            if (!GetChromatogram(TicChromatogramIndex.Value, out times, out _))
            {
                return false;
            }

            if (times.Length <= 1)
            {
                return false;
            }

            return true;
        }

        public int ChromatogramCount
        {
            get
            {
                int count = QcTraces.Count;
                if (TicChromatogramIndex.HasValue)
                {
                    count++;
                }

                if (BpcChromatogramIndex.HasValue)
                {
                    count++;
                }

                return count;
            }
        }

        /// <summary>
        /// Returns a flat list of the global chromatograms.
        /// This list is always in the following order:
        /// Total Ion Current
        /// Base Peak
        /// all qc traces
        /// </summary>
        public IList<ChromKey> ListChromKeys()
        {
            var list = new List<ChromKey>();
            foreach (var possibleGlobalIndex in new[] { TicChromatogramIndex, BpcChromatogramIndex })
            {
                if (!possibleGlobalIndex.HasValue)
                    continue;
                int globalIndex = possibleGlobalIndex.Value;
                list.Add(ChromKey.FromId(GetChromatogramId(globalIndex, out _), false));
            }

            foreach (var qcTrace in QcTraces)
            {
                list.Add(ChromKey.FromQcTrace(qcTrace));
            }
            Assume.AreEqual(list.Count, ChromatogramCount);
            return list;
        }

        /// <summary>
        /// Gets the chromatogram data for the chromatogram at a particular position in the list
        /// returned by <see cref="ListChromKeys"/>
        /// </summary>
        public bool GetChromatogramAt(int index, out float[] times, out float[] intensities)
        {
            if (TicChromatogramIndex.HasValue)
            {
                if (index == 0)
                {
                    return ReadChromatogramFromDataFile(TicChromatogramIndex.Value, out times, out intensities);
                }

                index--;
            }

            if (BpcChromatogramIndex.HasValue)
            {
                if (index == 0)
                {
                    return ReadChromatogramFromDataFile(BpcChromatogramIndex.Value, out times, out intensities);
                }

                index--;
            }

            if (index >= 0 && index < QcTraces.Count)
            {
                var qcTrace = QcTraces[index];
                times = MsDataFileImpl.ToFloatArray(qcTrace.Times);
                intensities = MsDataFileImpl.ToFloatArray(qcTrace.Intensities);
                return true;
            }

            times = intensities = null;
            return false;
        }
    }

    internal sealed class ChromatogramDataProvider : ChromDataProvider
    {
        private readonly List<ChromKeyProviderIdPair> _chromIds = new List<ChromKeyProviderIdPair>();
        private readonly int[] _chromIndices;

        private MsDataFileImpl _dataFile;
        private GlobalChromatogramExtractor _globalChromatogramExtractor;

        private readonly bool _hasMidasSpectra;
        private readonly bool _hasSonarSpectra;
        private readonly bool _sourceHasNegativePolarityData;
        private readonly bool _sourceHasPositivePolarityData;
        private readonly eIonMobilityUnits _ionMobilityUnits;
        private readonly OptimizableRegression _optimizableRegression;

        /// <summary>
        /// The number of chromatograms read so far.
        /// </summary>
        private int _readChromatograms;

        public ChromatogramDataProvider(MsDataFileImpl dataFile,
                                        ChromFileInfo fileInfo,
                                        SrmDocument document,
                                        IProgressStatus status,
                                        int startPercent,
                                        int endPercent,
                                        IProgressMonitor loader)
            : base(fileInfo, status, startPercent, endPercent, loader)
        {
            _dataFile = dataFile;
            _globalChromatogramExtractor = new GlobalChromatogramExtractor(dataFile);
            _optimizableRegression = document.Settings.MeasuredResults?.Chromatograms
                .FirstOrDefault(c => null != c.OptimizationFunction && c.ContainsFile(fileInfo.FilePath))
                ?.OptimizationFunction;
            
            int len = dataFile.ChromatogramCount;
            _chromIndices = new int[len];

            bool fixCEOptForShimadzu = dataFile.IsShimadzuFile;
            int indexPrecursor = -1;
            var lastPrecursor = SignedMz.ZERO;
            for (int i = 0; i < len; i++)
            {
                int index;
                string id = dataFile.GetChromatogramId(i, out index);

                if (!ChromKey.IsKeyId(id))
                    continue;

                var chromKey = ChromKey.FromId(id, fixCEOptForShimadzu);
                if (chromKey.Precursor != lastPrecursor)
                {
                    lastPrecursor = chromKey.Precursor;
                    indexPrecursor++;
                }
                if (chromKey.Precursor.IsNegative)
                {
                    _sourceHasNegativePolarityData = true;
                }
                else
                {
                    _sourceHasPositivePolarityData = true;
                }
                var ki = new ChromKeyProviderIdPair(chromKey, index);
                _chromIndices[index] = indexPrecursor;
                _chromIds.Add(ki);
            }

            if (fixCEOptForShimadzu)
            {
                SetOptStepsFromCeValues();
            }
            else if (_optimizableRegression != null)
            {
                SetOptStepsFromProductMz(document);
            }

            if (_chromIds.Count == 0)
                throw new NoSrmDataException(FileInfo.FilePath);

            // CONSIDER: TIC and BPC are not well defined for SRM and produced chromatograms with over 100,000 points in
            // Agilent CE optimization data. So, keep them off for now.
//            foreach (int globalIndex in _globalChromatogramExtractor.GlobalChromatogramIndexes)
//            {
//                _chromIndices[globalIndex] = globalIndex;
//                _chromIds.Add(new ChromKeyProviderIdPair(ChromKey.FromId(_globalChromatogramExtractor.GetChromatogramId(globalIndex, out int indexId), false), globalIndex));
//            }

            foreach (var qcTrace in _globalChromatogramExtractor.QcTraces)
            {
                _chromIndices[qcTrace.Index] = qcTrace.Index;
                _chromIds.Add(new ChromKeyProviderIdPair(ChromKey.FromQcTrace(qcTrace), qcTrace.Index));
            }

            // CONSIDER(kaipot): Some way to support mzML files converted from MIDAS wiff files
            _hasMidasSpectra = (dataFile.IsABFile) && SpectraChromDataProvider.HasSpectrumData(dataFile);

            _hasSonarSpectra = dataFile.IsWatersSonarData();

            _ionMobilityUnits = dataFile.IonMobilityUnits;

            SetPercentComplete(50);
        }

        private void SetOptStepsFromProductMz(SrmDocument doc)
        {
            var idToIndex = new Dictionary<int, int>();
            for (var i = 0; i < _chromIds.Count; i++)
                idToIndex[_chromIds[i].ProviderId] = i;

            foreach (var matchingGroup in ChromCacheBuilder.GetMatchingGroups(doc, this))
            {
                ChromKey lastChromKey = null;
                ChromKey firstChromKey = null;
                var curGroup = new List<ChromData>();
                foreach (var chromData in matchingGroup.Value.Chromatograms.OrderBy(chromData =>
                             chromData.Key.Product))
                {
                    if (lastChromKey != null)
                    {
                        bool optimizationSpacing =
                            ChromatogramInfo.IsOptimizationSpacing(lastChromKey.Product, chromData.Key.Product);
                        if (!optimizationSpacing)
                        {
                            if (_dataFile.IsAgilentFile)
                            {
                                // Agilent files sometimes round off the Q3 values, so if we consider all chromatograms
                                // within mzMatchTolerance of the document transition product m/z to be part of the same group of optimization steps

                                // We do not know what the document transition product m/z is at this point, so if the m/z is within twice the tolerance
                                // of the first product m/z in the series, that is good enough
                                double tolerance = doc.Settings.TransitionSettings.Instrument.MzMatchTolerance * 2;
                                if (chromData.Key.Product.CompareTolerant(firstChromKey.Product, tolerance) == 0)
                                {
                                    optimizationSpacing = true;
                                }
                                // TODO(nicksh): Populate ChromKey.CollisionEnergy so that we can guarantee that these
                                // ChromKeys are sorted correctly by collision energy
                            }
                        }

                        if (!optimizationSpacing)
                        {
                            SetOptStepsForGroup(idToIndex, matchingGroup.Key?.NodeGroup, curGroup);
                            firstChromKey = null;
                        }
                    }

                    curGroup.Add(chromData);
                    lastChromKey = chromData.Key;
                    firstChromKey ??= lastChromKey;
                }

                if (lastChromKey != null)
                {
                    SetOptStepsForGroup(idToIndex, matchingGroup.Key?.NodeGroup, curGroup);
                }
            }
        }
        private void SetOptStepsFromCeValues()
        {
            // Shimadzu can't do the necessary product m/z stepping for itself.
            // So, they provide the CE values in their IDs and we need to adjust
            // product m/z values for them to support CE optimization.

            // Need to sort by keys to ensure everything is in the right order.
            _chromIds.Sort();

            int indexLast = 0;
            var lastPrecursor = SignedMz.ZERO;
            var lastProduct = SignedMz.ZERO;
            for (int i = 0; i < _chromIds.Count; i++)
            {
                var chromKey = _chromIds[i].Key;
                if (chromKey.Precursor != lastPrecursor || chromKey.Product != lastProduct)
                {
                    int count = i - indexLast;
                    if (HasConstantCEInterval(indexLast, count))
                    {
                        AddCESteps(indexLast, count);
                    }
                    lastPrecursor = chromKey.Precursor;
                    lastProduct = chromKey.Product;
                    indexLast = i;
                }
            }
            int finalCount = _chromIds.Count - indexLast;
            if (HasConstantCEInterval(indexLast, finalCount))
            {
                AddCESteps(indexLast, finalCount);
            }
        }

        private void SetOptStepsForGroup(IReadOnlyDictionary<int, int> idToIndex, TransitionGroupDocNode transitionGroupDocNode, IList<ChromData> chromDatas)
        {
            if (chromDatas.Count <= 1)
            {
                chromDatas.Clear();
                return;
            }

            int centerIdx = (chromDatas.Count + 1) / 2 - 1;
            if (transitionGroupDocNode != null)
            {
                var centerMz = chromDatas[centerIdx].Key.Product;
                var closestTransition = transitionGroupDocNode.Transitions.OrderBy(t => Math.Abs(t.Mz - centerMz))
                    .FirstOrDefault();
                if (closestTransition != null)
                {
                    centerIdx = OptStepChromatograms.IndexOfCenter(closestTransition.Mz, chromDatas.Select(c => c.Key.Product),
                        _optimizableRegression.StepCount);
                }
            }
            for (var i = 0; i < chromDatas.Count; i++)
            {
                SetOptimizationStep(idToIndex[chromDatas[i].ProviderId], i - centerIdx, chromDatas[centerIdx].Key.Product);
            }

            chromDatas.Clear();
        }

        private float GetCE(int i)
        {
            return _chromIds[i].Key.CollisionEnergy;
        }

        private bool HasConstantCEInterval(int start, int count)
        {
            // Need at least 3 steps for CE optimization
            if (count < 3)
                return false;

            double ceStart = GetCE(start);
            double ceEnd = GetCE(start + count - 1);
            double expectedInterval = (ceEnd - ceStart)/(count - 1);
            if (expectedInterval == 0)
                return false;

            for (int i = 1; i < count; i++)
            {
                double interval = GetCE(start + i) - GetCE(start + i - 1);
                if (Math.Abs(interval - expectedInterval) > 0.001)
                    return false;
            }
            return true;
        }

        private void AddCESteps(int start, int count)
        {
            int step = count / 2;
            for (int i = count - 1; i >= 0; i--)
            {
                SetOptimizationStep(start + i, step--, null);
            }
        }

        private void SetOptimizationStep(int i, int step, SignedMz? newProductMz)
        {
            var chromId = _chromIds[i];
            var chromKeyNew = chromId.Key.ChangeOptimizationStep(step, newProductMz);
            _chromIds[i] = new ChromKeyProviderIdPair(chromKeyNew, chromId.ProviderId);
        }

        public override IEnumerable<ChromKeyProviderIdPair> ChromIds
        {
            get { return _chromIds; }
        }

        public override eIonMobilityUnits IonMobilityUnits { get { return _ionMobilityUnits; } }

        public override bool GetChromatogram(int id, ChromatogramGroupId chromatogramGroupId, Color color, out ChromExtra extra, out TimeIntensities timeIntensities)
        {
            float[] times, intensities;
            if (!_globalChromatogramExtractor.GetChromatogram(id, out times, out intensities))
            {
                _dataFile.GetChromatogram(id, out _, out times, out intensities);
            }

            timeIntensities = new TimeIntensities(times, intensities, null, null);

            // Assume that each chromatogram will be read once, though this may
            // not always be completely true.
            _readChromatograms++;

            if (_readChromatograms < _chromIds.Count)
                SetPercentComplete(50 + _readChromatograms * 50 / _chromIds.Count);

            int index = _chromIndices[id];
            extra = new ChromExtra(index, -1);  // TODO: is zero the right value?

            // Display in AllChromatogramsGraph
            var loadingStatus = Status as ChromatogramLoadingStatus;
            if (loadingStatus != null)
                loadingStatus.Transitions.AddTransition(
                    chromatogramGroupId,
                    color,
                    index, -1,
                    times,
                    intensities);
            return true;
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

        public override bool IsSrm
        {
            get { return true; }
        }

        public override bool HasMidasSpectra
        {
            get { return _hasMidasSpectra; }
        }

        public override bool HasSonarSpectra
        {
            get { return _hasSonarSpectra; }
        }

        public override bool SourceHasPositivePolarityData
        {
            get { return _sourceHasPositivePolarityData; }
        }

        public override bool SourceHasNegativePolarityData
        {
            get { return _sourceHasNegativePolarityData; }
        }

        public static bool HasChromatogramData(MsDataFileImpl dataFile)
        {
            return dataFile.HasChromatogramData;
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
