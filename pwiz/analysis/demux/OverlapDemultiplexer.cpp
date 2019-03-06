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

#include "OverlapDemultiplexer.hpp"
#include "SpectrumPeakExtractor.hpp"
#include "IInterpolation.hpp"
#include "DemuxHelpers.hpp"
#include "CubicHermiteSpline.hpp"
#include <boost/make_shared.hpp>

namespace pwiz {
namespace analysis {
    using namespace std;
    using namespace DemuxTypes;
    using namespace msdata;

    OverlapDemultiplexer::OverlapDemultiplexer(Params p) :
        params_(p)
    {
        overlapRegionsInApprox_ = 7;
        cyclesInBlock_ = 3;
    }

    OverlapDemultiplexer::~OverlapDemultiplexer()
    {
    }

    void OverlapDemultiplexer::Initialize(SpectrumList_const_ptr sl, IPrecursorMaskCodec::const_ptr pmc)
    {
        assert(sl);
        assert(pmc);
        if (!sl || !pmc)
            return;
        sl_ = sl;
        pmc_ = pmc;
    }

    void OverlapDemultiplexer::BuildDeconvBlock(size_t index, const vector<size_t>& muxIndices, MatrixPtr& masks, MatrixPtr& signal) const
    {
        if (!sl_ || !pmc_)
            throw runtime_error("BuildDeconvBlock() Null pointer to SpectrumList and/or IPrecursorMaskCodec. OverlapDemultiplexer may not have been initialized.");

        // get the list of peaks to demultiplex
        Spectrum_const_ptr deconvSpectrum = sl_->spectrum(index, true);
        BinaryDataArrayPtr mzsToDemux = deconvSpectrum->getMZArray();
        SpectrumPeakExtractor peakExtractor(mzsToDemux->data, params_.massError);

        // initialize mask and intensities matrices.
        size_t numMuxSpectra = overlapRegionsInApprox_; // m
        size_t numDemuxSpectra = overlapRegionsInApprox_; // n
        size_t numTransitions = mzsToDemux->data.size(); // k
        masks = boost::make_shared<MatrixType>(numMuxSpectra, numDemuxSpectra);
        signal = boost::make_shared<MatrixType>(numMuxSpectra, numTransitions);
        
        // Find the scans nearest to the deconvolution windows of the spectrum of interest
        vector<size_t> deconvIndices;
        pmc_->SpectrumToIndices(deconvSpectrum, deconvIndices);
        double sum = 0.0;
        for (auto i : deconvIndices) { sum += i; }
        auto centerOfDeconvIndices = sum / deconvIndices.size();

        // Find the range of deconv window indices that will be used to extract the mask for this spectrum
        auto idealLowerMzBound = lround(centerOfDeconvIndices - overlapRegionsInApprox_ / 2.0);
        auto lowerMZBound = max(idealLowerMzBound, 0l);
        lowerMZBound = min(lowerMZBound, static_cast<long>(pmc_->GetNumDemuxWindows() - overlapRegionsInApprox_));

        vector<pair<double, size_t> > demuxWindowDistances;
        auto specPerCycle = pmc_->GetSpectraPerCycle();
        Spectrum_const_ptr offsetSpectrum;
        vector<size_t> offsetIndices;
        for (auto scanIndex = muxIndices.begin(); scanIndex < muxIndices.end(); ++scanIndex)
        {
            offsetSpectrum = sl_->spectrum(*scanIndex, true);
            pmc_->SpectrumToIndices(offsetSpectrum, offsetIndices);
            double offsetSum = 0.0;
            for (auto i : offsetIndices) { offsetSum += i; }
            auto centerOfOffsetIndices = offsetSum / offsetIndices.size();
            auto distance = centerOfOffsetIndices - centerOfDeconvIndices;
            demuxWindowDistances.push_back(pair<double, size_t>(distance, *scanIndex));
        }

        // Sort by distances from spectrum of interest in m/z space
        sort(demuxWindowDistances.begin(), demuxWindowDistances.end(), [](const pair<double, size_t>& left, const pair<double, size_t>& right)
        {
            return abs(left.first) < abs(right.first) && abs(abs(left.first) - abs(right.first)) > 10e-4;
        });

        // Choose the regions near enough to the spectrum demux windows of interest to be included
        vector<pair<double, size_t>> bestMaskAverages;
        bestMaskAverages.reserve(overlapRegionsInApprox_);
        copy(demuxWindowDistances.begin(), demuxWindowDistances.begin() + overlapRegionsInApprox_, back_inserter(bestMaskAverages));

        sort(bestMaskAverages.begin(), bestMaskAverages.end(), [](const pair<double, size_t>& left, const pair<double, size_t>& right)
        {
            return left.first < right.first && abs(left.first - right.first) > 10e-4;
        });

        // Get the indices of the chosen regions
        vector<size_t> scansInDeconv;
        scansInDeconv.reserve(bestMaskAverages.size());
        transform(bestMaskAverages.begin(), bestMaskAverages.end(), back_inserter(scansInDeconv), [](const pair<double, size_t> &p)
        {
            return p.second;
        });

        // Fill masks
        for (size_t matrixRow = 0; matrixRow < scansInDeconv.size(); ++matrixRow)
        {
            size_t currentIndex = scansInDeconv[matrixRow];
            Spectrum_const_ptr muxSpectrum = sl_->spectrum(currentIndex, true);
            auto fullMaskRow = pmc_->GetMask(muxSpectrum); /// GetMask could be made more efficient to only return a specified subset of masks
            masks->row(matrixRow) = fullMaskRow.segment(lowerMZBound, numDemuxSpectra);
        }

        // Fill signal
        if (params_.interpolateRetentionTime)
        {
        // Get retention time for scan to be deconvolved
        double deconvStartTime;
        if (!TryGetStartTime(*deconvSpectrum, deconvStartTime))
            throw runtime_error("BuildDeconvBlock() Tried to process an MS2 scan without retention times written.");

        // Cache the data that will be used for interpolation
        vector<MatrixPtr> binnedIntensitiesCache;
        vector<boost::shared_ptr<VectorXd> > scanTimesCache;

        // Loop through the scans and cache intensity and time data for interpolation
        for (size_t matrixRow = 0; matrixRow < scansInDeconv.size(); ++matrixRow)
        {
            binnedIntensitiesCache.push_back(boost::make_shared<MatrixType>(cyclesInBlock_, numTransitions));
            scanTimesCache.push_back(boost::make_shared<VectorXd>(cyclesInBlock_));

            // Find scans around the spectrum to use for interpolation
            auto scan = scansInDeconv[matrixRow];
            vector<size_t> interpolationSpectraIndices;
            if (!FindNearbySpectra(interpolationSpectraIndices, sl_, scan, cyclesInBlock_, specPerCycle))
                throw runtime_error("BuildDeconvBlock() Not enough spectra to interpolate for the overlap.");

            // Extract retention times and peak intensities for scan
            for (size_t i = 0; i < interpolationSpectraIndices.size(); ++i)
            {
                auto spectrumIndex = interpolationSpectraIndices.at(i);
                Spectrum_const_ptr currentSpectrum = sl_->spectrum(spectrumIndex, true);
                
                double startTime;
                if (!TryGetStartTime(*currentSpectrum, startTime))
                    throw runtime_error("BuildDeconvBlock() Tried to process an MS2 scan without retention times written.");

                // Record scan time
                (*scanTimesCache.back())[i] = startTime;

                // Bin and record transitions
                peakExtractor(currentSpectrum, *binnedIntensitiesCache.back(), i);
            }
        }
        // Perform interpolation
        // TODO parallelize this
        for (size_t matrixRow = 0; matrixRow < overlapRegionsInApprox_; ++matrixRow)
        {
            InterpolateMuxRegion(signal->row(matrixRow), deconvStartTime, *binnedIntensitiesCache.at(matrixRow), *scanTimesCache.at(matrixRow));
        }
        }
        else
        {
            int specPerCycle = pmc_->GetSpectraPerCycle();
            for (int matrixRow = 0; matrixRow < scansInDeconv.size(); ++matrixRow)
            {
                size_t currentIndex = scansInDeconv[matrixRow];
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
                peakExtractor(s, *signal, matrixRow, weight);
            }
        }

        // Cache the indices for the spectrum
        spectrumIndices_.clear();
        for (auto demuxIndex : deconvIndices)
        {
            assert(demuxIndex >= lowerMZBound);
            spectrumIndices_.push_back(demuxIndex - lowerMZBound);
        }
    }

    void OverlapDemultiplexer::GetMatrixBlockIndices(size_t indexToDemux, std::vector<size_t>& muxIndices, double demuxBlockExtra) const
    {
        demuxBlockExtra = max(0.0, demuxBlockExtra);
        auto numSpectraToFind = pmc_->GetSpectraPerCycle() + size_t(round(demuxBlockExtra * pmc_->GetSpectraPerCycle()));
        if (!FindNearbySpectra(muxIndices, sl_, indexToDemux, numSpectraToFind))
            throw runtime_error("GetMatrixBlockIndices() Not enough spectra to demultiplex this block");
    }

    const std::vector<size_t>& OverlapDemultiplexer::SpectrumIndices() const
    {
        return spectrumIndices_;
    }

    void OverlapDemultiplexer::InterpolateMuxRegion(Ref<MatrixXd, 0, Stride<Dynamic, Dynamic> > interpolatedIntensities, double timeToInterpolate,
        Eigen::Ref<const Eigen::MatrixXd> intensities, Ref<const VectorXd> scanTimes)
    {
        if (interpolatedIntensities.cols() != 1 && interpolatedIntensities.rows() != 1)
            throw runtime_error("InterpolateMuxRegion() Output block is not a vector type");

        if (interpolatedIntensities.size() != intensities.cols())
            throw runtime_error("InterpolateMuxRegion() Output block does not have the expected size");

        //TODO parallelize this
        auto numTransitions = static_cast<size_t>(intensities.cols());
        for (size_t transition = 0; transition < numTransitions; ++transition)
        {
            auto intensitiesBlock = intensities.col(transition);            
            interpolatedIntensities(0, transition) = max(0.0, InterpolateMatrix(timeToInterpolate, scanTimes, intensitiesBlock));
        }
    }

    double OverlapDemultiplexer::InterpolateMatrix(double pointToInterpolate, Eigen::Ref<const Eigen::VectorXd> points, Eigen::Ref<const Eigen::VectorXd> values)
    {
        boost::shared_ptr<IInterpolation> interpolator = boost::make_shared<CubicHermiteSpline>(points, values);
        return interpolator->Interpolate(pointToInterpolate);
    }
} // namespace analysis
} // namespace pwiz