/*
 * Original author: Dario Amodei <jegertso .at .u.washington.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Linq;
using MathNet.Numerics.LinearAlgebra.Double;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Results
{
    public class OverlapDemultiplexer : AbstractDemultiplexer
    {
        public OverlapDemultiplexer(MsDataFileImpl file, SpectrumFilter filter)
            : base(file, filter)
        {
            _isoMapper = new OverlapIsolationWindowMapper();
            _overlapRegionsInApprox = 7;
        }

        public int _overlapRegionsInApprox { get; private set; }

        public override int NumScansBlock
        {
            get { return NumDeconvRegions - 1; }
        }

        protected override void InitializeSolver()
        {
            var maxTransInSpectrum = _spectrumProcessor.MaxTransitions(1);
            _deconvBlock = new DeconvBlock(NumDeconvRegions, NumScansBlock+1, maxTransInSpectrum);
            _solver = new OverlapLsSolver(_overlapRegionsInApprox,_overlapRegionsInApprox, maxTransInSpectrum);
        }

        public override DeconvBlock PreprocessDeconvBlock(int[] deconvIndices)
        {
            // Re-index the rows of the deconvBlock so that overlap and isolation windows are ordered by m/z 
            // and the matrix always takes the same near-diagonal form
            Reindex();
            // Select only the most relevant rows to demultiplex
            int rowStart = RowStart(deconvIndices[0]);
            int rowEnd = rowStart + _overlapRegionsInApprox - 2;
            int columnStart = rowStart;
            int columnEnd = columnStart + _overlapRegionsInApprox - 1;
            var newDeconvBlock = MakeDeconvSubblock(_deconvBlock, rowStart, rowEnd, columnStart, columnEnd);
            // Add a regularizer row to make sure the solution isn't under-determined
            double[] maskRegularizer = new double[newDeconvBlock.NumIsos];
            double[] solutionRegularizer = new double[newDeconvBlock.NumTransitions];
            maskRegularizer[0] = 1.0;
            newDeconvBlock.Add(maskRegularizer, solutionRegularizer);
            return newDeconvBlock;
        }

        public override void PostprocessDeconvBlock(DeconvBlock deconvBlock, int[] deconvIndices)
        {
            int isosStart = RowStart(deconvIndices[0]);
            InsertDeconvSubblock(deconvBlock, isosStart, ref _deconvBlock);
        }

        public int RowStart(int deconvIndex)
        {
            int maxLeftBoundary = NumDeconvRegions - _overlapRegionsInApprox;
            if (maxLeftBoundary < 0)
            {
                throw new Exception(string.Format(Resources.OverlapDemultiplexer_RowStart_Number_of_regions__0__in_overlap_demultiplexer_approximation_must_be_less_than_number_of_scans__1__,
                    _overlapRegionsInApprox,
                    NumDeconvRegions));
            }
            return Math.Min(maxLeftBoundary, Math.Max(0, deconvIndex - _overlapRegionsInApprox / 2));
        }

        public void InsertDeconvSubblock(DeconvBlock blockToInsert, int isosStart, ref DeconvBlock targetBlock)
        {
            int numIsos = blockToInsert.NumIsos;
            int isosEnd = isosStart + numIsos - 1;
            int maxTransitions = targetBlock.MaxTransitions;
            if(0 > isosStart || isosEnd >= targetBlock.NumIsos)
            {
                throw new Exception(Resources.OverlapDemultiplexer_attempt_to_insert_slice_of_deconvolution_matrix_failed_out_of_range);
            }
            for (int i = isosStart; i <= isosEnd; ++i)
            {
                for (int j = 0; j < maxTransitions; ++j)
                {
                    targetBlock.Solution.Matrix[i,j] = blockToInsert.Solution.Matrix[i-isosStart,j];
                }
            }
        }

        public DeconvBlock MakeDeconvSubblock(DeconvBlock deconvBlock, int rowStart, int rowEnd, int columnStart, int columnEnd)
        {
            if (rowStart < 0 ||
                columnStart < 0 ||
                columnEnd < columnStart ||
                rowEnd < rowStart ||
                rowEnd >= deconvBlock.NumRows ||
                columnEnd >= deconvBlock.NumIsos)
            {
                throw new Exception(Resources.OverlapDemultiplexer_attempt_to_take_slice_of_deconvolution_matrix_failed_out_of_range);
            }
            int numRows = rowEnd - rowStart + 1;
            int numCols = columnEnd - columnStart + 1;
            int maxTransitions = deconvBlock.MaxTransitions;
            int numTransitions = deconvBlock.NumTransitions;
            var newDeconvBlock = new DeconvBlock(numCols, numRows+1, maxTransitions);
            for (int i = rowStart; i <= rowEnd; ++i)
            {
                var sliceMask = new DenseVector(numCols);
                var sliceData = new DenseVector(numTransitions);
                deconvBlock.Masks.Matrix.Row(i,columnStart,numCols,sliceMask);
                deconvBlock.BinnedData.Matrix.Row(i, 0, numTransitions, sliceData);
                newDeconvBlock.Add(sliceMask,sliceData);
            }
            return newDeconvBlock;
        }

        public void Reindex()
        {
            int numRows = _deconvBlock.NumRows;
            int numTransitions = _deconvBlock.NumTransitions;
            DeconvBlock deconvBlockNew = new DeconvBlock(NumDeconvRegions, NumScansBlock+1, _deconvBlock.MaxTransitions);
            var reIndex = new int [numRows];
            for (int i = 0; i < numRows; ++i)
            {
                reIndex[i] = -1;
            }
            for (int i = 0; i < numRows; ++i)
            {
                var currentRow = new DenseVector(NumDeconvRegions);
                _deconvBlock.Masks.Matrix.Row(i, currentRow);
                int firstNonzero = FirstNonZero(currentRow);
                if (firstNonzero == -1)
                {
                    throw new Exception(Resources.OverlapDemultiplexer_the_isolation_window_overlap_scheme_does_not_cover_all_isolation_windows);
                }
                reIndex[firstNonzero] = i;
            }
            for (int i = 0; i < numRows; ++i)
            {
                if (reIndex[i] == -1)
                {
                    throw new Exception(Resources.OverlapDemultiplexer_the_isolation_window_overlap_scheme_does_not_cover_all_isolation_windows);
                }
                var currentMaskRow = new DenseVector(NumDeconvRegions);
                var currentBinnedRow = new DenseVector(numTransitions);
                _deconvBlock.Masks.Matrix.Row(reIndex[i],currentMaskRow);
                _deconvBlock.BinnedData.Matrix.Row(reIndex[i],0,numTransitions,currentBinnedRow);
                deconvBlockNew.Add(currentMaskRow,currentBinnedRow);
            }
            _deconvBlock = deconvBlockNew;
        }

        private int FirstNonZero(DenseVector currentRow)
        {
            for (int i = 0; i < currentRow.Count; i++)
            {
                if (currentRow[i] != 0)
                    return i;
            }
            return -1;
        }
    }

    public class OverlapIsolationWindowMapper : AbstractIsoWindowMapper
    {
        public override void DetermineDeconvRegions()
        {
            // Set the minimum size of an overlap region
            const double minDeconvWindow = 0.02;
            long minDeconvHash = IsoWindowHasher.Hash(minDeconvWindow);
            var isolationWindows = _isolationWindows;
            // Generate the set of overlap regions
            List<long> isoBoundaries = new List<long>(isolationWindows.Count * 2);
            foreach (var isoWindow in isolationWindows)
            {
                isoBoundaries.Add(isoWindow.Start);
                isoBoundaries.Add(isoWindow.Stop);
            }
            isoBoundaries = isoBoundaries.Distinct().ToList();

            isoBoundaries.Sort();
            for (int i = 0; i < isoBoundaries.Count - 1; ++i)
            {
                long widthRegion = isoBoundaries[i + 1] - isoBoundaries[i];
                long centerRegion = (isoBoundaries[i + 1] + isoBoundaries[i]) / 2;
                int windowIndex;
                if (widthRegion >= minDeconvHash && TryGetWindowFromMz(IsoWindowHasher.UnHash(centerRegion), out windowIndex))
                {
                    _deconvRegions.Add(new DeconvolutionRegion(isoBoundaries[i], isoBoundaries[i + 1], _deconvRegions.Count));
                }
            }

            // Now map each isolation window to its overlap regions
            foreach (var isoWin in isolationWindows) isoWin.DeconvRegions.Clear();
            var isoWinArray = isolationWindows.ToArray();
            // Sort the isolation windows by the start value
            Array.Sort(isoWinArray, (win, isoWin) => Comparer<long>.Default.Compare(win.Start, isoWin.Start));
            int overlapStartSearch = 0;
            foreach (IsoWin currentIso in isoWinArray)
            {
                for (int overlapIndex = overlapStartSearch; overlapIndex < _deconvRegions.Count(); ++overlapIndex)
                {
                    var currentOverlap = _deconvRegions[overlapIndex];
                    if (currentIso.Start >= currentOverlap.Stop)
                    {
                        overlapStartSearch = overlapIndex;
                        continue;
                    }
                    if (currentIso.Contains(currentOverlap)) currentIso.DeconvRegions.Add(currentOverlap);
                    if (currentOverlap.Start >= currentIso.Stop) break;
                }
            }
        }
    }
}