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
    public class MsxDemultiplexer : AbstractDemultiplexer
    {
        public int DutyCycleLength
        {
            get { return NumIsoWindows / IsoWindowsPerScan; }
        }

        public MsxDemultiplexer(MsDataFileImpl file, SpectrumFilter filter)
            : base(file, filter)
        {
            _isoMapper = new MsxIsolationWindowMapper();
        }

        public override int NumScansBlock
        {
            get
            {
                int dutyCycleLength = NumIsoWindows / IsoWindowsPerScan;
                return (int)(dutyCycleLength * IsoWindowsPerScan + (1.5 * dutyCycleLength));
            }
        }

        protected override void InitializeSolver()
        {
            var maxTransInSpectrum = _spectrumProcessor.MaxTransitions(IsoWindowsPerScan);
            _deconvBlock = new DeconvBlock(NumDeconvRegions, NumScansBlock, maxTransInSpectrum);
            _solver = new NonNegLsSolver(NumDeconvRegions, NumScansBlock, maxTransInSpectrum, true);
        }

        public override DeconvBlock PreprocessDeconvBlock(int[] deconvIndices)
        {
            return _deconvBlock;
        }

        public override void PostprocessDeconvBlock(DeconvBlock deconvBlock, int[] deconvIndices)
        {
            _deconvBlock = deconvBlock;
        }
    }

    public class MsxIsolationWindowMapper : AbstractIsoWindowMapper
    {
        // For MSX, make the deconvolution regions identical to the isolation windows
        public override void DetermineDeconvRegions()
        {
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
}