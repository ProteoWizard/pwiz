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
using pwiz.ProteowizardWrapper;

namespace pwiz.Skyline.Model.Results
{
    public class MsxTypeDemultiplexer : AbstractDemultiplexer
    {
        public int DutyCycleLength
        {
            get { return NumIsoWindows / IsoWindowsPerScan; }
        }

        public MsxTypeDemultiplexer(MsDataFileImpl file, SpectrumFilter filter)
            : base(file, filter)
        {
        }

        public override int NumScansBlock
        {
            get
            {
                return (int)(DutyCycleLength * IsoWindowsPerScan + (1.5 * DutyCycleLength));
            }
        }

        protected override void InitializeSolver()
        {
            var maxTransInSpectrum = _spectrumProcessor.MaxTransitions(IsoWindowsPerScan);
            _deconvHandler = new MsxDeconvSolverHandler(NumDeconvRegions, NumScansBlock, maxTransInSpectrum);
            _solver = new NonNegLsSolver(NumDeconvRegions, NumScansBlock, maxTransInSpectrum, true);
        }
    }

    public class MsxDemultiplexer : MsxTypeDemultiplexer
    {
        public MsxDemultiplexer(MsDataFileImpl file, SpectrumFilter filter)
            : base(file, filter)
        {
            _isoMapper = new MsxIsolationWindowMapper();
        }
    }

    public class MsxOverlapDemultiplexer : MsxTypeDemultiplexer
    {
        public MsxOverlapDemultiplexer(MsDataFileImpl file, SpectrumFilter filter)
            : base(file, filter)
        {
            _isoMapper = new OverlapIsolationWindowMapper();
        }
        protected override void InitializeSolver()
        {
            int maxOverlapComponents = _isoMapper.MaxDeconvInIsoWindow();
            var maxTransInSpectrum = _spectrumProcessor.MaxTransitions(maxOverlapComponents * IsoWindowsPerScan);
            _deconvHandler = new MsxDeconvSolverHandler(NumDeconvRegions, NumScansBlock, maxTransInSpectrum);
            _solver = new NonNegLsSolver(NumDeconvRegions, NumScansBlock, maxTransInSpectrum, true);
        }
    }

    public class MsxIsolationWindowMapper : AbstractIsoWindowMapper
    {
        // For MSX, make the deconvolution regions identical to the isolation windows
        protected override void FindDeconvRegions()
        {
            _deconvRegions.Clear();
            foreach (var isolationWindow in _isolationWindows)
            {
                var deconvRegion = new DeconvolutionRegion(isolationWindow.Start, isolationWindow.Stop,
                                             _deconvRegions.Count);
                _deconvRegions.Add(deconvRegion);
                // Each isolation window contains only itself as a deconv region
                isolationWindow.DeconvRegions.Add(deconvRegion);
            }
        }
    }

    public class MsxDeconvSolverHandler : AbstractDeconvSolverHandler
    {
        public MsxDeconvSolverHandler(int numDeconvWindows, int maxScans, int maxTransitions) :
            base(numDeconvWindows, maxScans, maxTransitions)
        {
            _deconvBlock = new DeconvBlock(numDeconvWindows, maxScans, maxTransitions);
        }

        protected override void BuildDeconvBlock()
        {
            _deconvBlock.Masks.Resize(NumScans, NumDeconvWindows);
            _deconvBlock.Masks.Matrix.SetSubMatrix(0, NumScans, 0, NumDeconvWindows,
                                                   Masks.Matrix.SubMatrix(0, NumScans, 0, NumDeconvWindows));
            _deconvBlock.BinnedData.Resize(NumScans, NumTransitions);
            _deconvBlock.BinnedData.Matrix.SetSubMatrix(0, NumScans, 0, NumTransitions,
                                                   BinnedData.Matrix.SubMatrix(0, NumScans, 0, NumTransitions));
            _deconvBlock.Solution.Resize(NumDeconvWindows, NumTransitions);
        }

        protected override void DeconvBlockToSolution()
        {
            // Not using LINQ due to speed concerns.
            int i = 0;
            foreach (int deconvIndex in DeconvIndices)
            {
                Solution.Matrix.SetRow(i, _deconvBlock.Solution.Matrix.Row(deconvIndex));
                ++i;
            }
        }
    }
}