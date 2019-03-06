//
// $Id$
//
//
// Original author: Jarrett Egertson <jegertso .@. uw.edu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

#include "MSXDemultiplexer.hpp"
#include "SpectrumPeakExtractor.hpp"
#include "DemuxHelpers.hpp"


namespace pwiz {
namespace analysis {
    using namespace std;
    using namespace DemuxTypes;
    using namespace msdata;

    MSXDemultiplexer::MSXDemultiplexer(Params p) :
        params_(p)
    {
    }

    MSXDemultiplexer::~MSXDemultiplexer()
    {
    }

    void MSXDemultiplexer::Initialize(SpectrumList_const_ptr sl, IPrecursorMaskCodec::const_ptr pmc)
    {
        assert(sl);
        assert(pmc);
        if (!sl || !pmc)
            return;
        sl_ = sl;
        pmc_ = pmc;
    }

    void MSXDemultiplexer::BuildDeconvBlock(size_t index, const vector<size_t>& muxIndices, MatrixPtr& masks, MatrixPtr& signal) const
    {
        assert(sl_);
        assert(pmc_);
        if (!sl_ || !pmc_)
            throw runtime_error("Null pointer to SpectrumList and/or IPrecursorMaskCodec, MSXDemultiplexer may not have been initialized.");

        // get the list of peaks to demultiplex
        BinaryDataArrayPtr mzsToDemux = sl_->spectrum(index, true)->getMZArray();
        SpectrumPeakExtractor peakExtractor(mzsToDemux->data, params_.massError);

        // initialize mask and intensities matrices
        masks.reset(new MatrixType(muxIndices.size(), pmc_->GetDemuxBlockSize()));
        signal.reset(new MatrixType(muxIndices.size(), mzsToDemux->data.size()));
        
        int specPerCycle = pmc_->GetSpectraPerCycle();
        for (int matrixRow = 0; matrixRow < muxIndices.size(); ++matrixRow)
        {
            size_t currentIndex = muxIndices[matrixRow];
            Spectrum_const_ptr s = sl_->spectrum(currentIndex, true);
            DemuxScalar weight = 1.0;
            if (params_.applyWeighting)
            {
                /* This method of weighting tries to minimize the added variance from changing intensity during
                * chromatogram elution. Given a point on the elution peak, one can model the change in intensity as a
                * function of difference in retention time. By modeling this function for all possible points on the
                * peak it is possible to find a best fit weighting scheme that minimizes intensity variance. To
                * describe the model itself in more detail, ScanDiff contains the difference in retention time and
                * 5 / specPerCycle contains information about the typical peak width (or standard deviation).
                */
                int scanDiff = int(index) - int(currentIndex);
                weight = 1.0 / (1.0 + pow(5.0 * scanDiff / specPerCycle, 2));
            }
            pmc_->GetMask(s, *masks, matrixRow, weight);
            if (params_.variableFill)
            {
                // the intensity values in a variable fill scan are written out
                // as the # ions / total injection time (summed)
                double totalInjectionTime = 0.0;
                for (const auto& p : s->precursors)
                {
                    auto injectParam = p.userParam("MultiFillTime");
                    if (injectParam.empty())
                    {
                        throw runtime_error("[SpectrumlList_MsxDemux] Tried to process an MS2 scan without multi-fill times written using variable fill demux ");
                    }
                    totalInjectionTime += injectParam.valueAs<double>();
                }
                weight *= totalInjectionTime / 1000.0;    // need total injection time in seconds
            }
            peakExtractor(s, *signal, matrixRow, weight);
        }

        // cache the spectrum indices
        Spectrum_const_ptr sPtr = sl_->spectrum(index, true);
        pmc_->SpectrumToIndices(sPtr, spectrumIndices_);
    }

    void MSXDemultiplexer::GetMatrixBlockIndices(size_t indexToDemux, std::vector<size_t>& muxIndices, double demuxBlockExtra) const
    {
        demuxBlockExtra = max(0.0, demuxBlockExtra);
        auto numSpectraToFind = pmc_->GetDemuxBlockSize() + size_t(round(demuxBlockExtra * pmc_->GetSpectraPerCycle()));
        if (!FindNearbySpectra(muxIndices, sl_, indexToDemux, numSpectraToFind))
            throw runtime_error("GetMatrixBlockIndices() Not enough spectra to demultiplex this block");
    }

    const std::vector<size_t>& MSXDemultiplexer::SpectrumIndices() const
    {
        return spectrumIndices_;
    }
} // namespace analysis
} // namespace pwiz