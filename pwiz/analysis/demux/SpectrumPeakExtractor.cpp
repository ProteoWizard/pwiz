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

#define PWIZ_SOURCE

#include "SpectrumPeakExtractor.hpp"

namespace pwiz {
namespace analysis {
    SpectrumPeakExtractor::SpectrumPeakExtractor(const std::vector<double>& peakMzList, const pwiz::chemistry::MZTolerance& massError)
    {
        _numPeakBins = peakMzList.size();
        _ranges = boost::shared_array< std::pair<double, double> >(new std::pair<double, double>[_numPeakBins]);
        _maxDelta = 0.0;
        for (size_t i = 0; i < peakMzList.size(); ++i)
        {
            double peakMz = peakMzList[i];
            double deltaMz = peakMz - (peakMz - massError);
            if (deltaMz > _maxDelta) _maxDelta = deltaMz;
            _ranges[i].first = peakMz - deltaMz;
            _ranges[i].second = peakMz + deltaMz;
        }
        _minValue = _ranges[0].first;
        _maxValue = _ranges[_numPeakBins - 1].second;

        boost::shared_array< std::pair<double, double> > range_copy = boost::shared_array< std::pair<double, double> >(new std::pair<double, double>[_numPeakBins]);
        std::copy(_ranges.get(), _ranges.get() + _numPeakBins, range_copy.get());

        /* Verify non-overlapping of ranges. Overlap may be observed if the peaks are not centroided. Overlapping ranges are undesirable because they
         * could duplicate signal, which should be a conserved quantity. */
        for (size_t i = 0; i + 1 < _numPeakBins; ++i)
        {
            if (range_copy[i].second > range_copy[i + 1].first)
            {
                // find center and snap edges to this center
                double center = (range_copy[i].second + range_copy[i].first + range_copy[i + 1].second + range_copy[i + 1].first) / 4.0;
                _ranges[i].second = center;
                _ranges[i + 1].first = center;
                
            }
        }
    }

    void SpectrumPeakExtractor::operator()(msdata::Spectrum_const_ptr spectrum, MatrixType& m, size_t rowNum, double weight) const
    {
        // Puts intensities observed in the spectrum into bins specified by _ranges
        msdata::BinaryDataArray_const_ptr mzArray = spectrum->getMZArray();
        msdata::BinaryDataArray_const_ptr intensityArray = spectrum->getIntensityArray();

        // intitialize the filtered values to zero
        m.row(rowNum).setZero();

        size_t binStartIndex = 0;
        for (size_t queryIndex = 0; queryIndex < mzArray->data.size(); ++queryIndex)
        {
            // iterating through each "query" peak in the MS/MS spectrum to be filtered
            double query = mzArray->data[queryIndex];
            if (query < _minValue) continue;
            if (query > _maxValue) break;
            double minStart = query - _maxDelta;
            // move the starting point for this search and future searches to the first bin
            // that could possibly contain this peak
            for (; binStartIndex < _numPeakBins; ++binStartIndex)
            {
                if (_ranges[binStartIndex].first >= minStart) break;
            }
            // look forward for peak bins that contain this query
            for (size_t binIndex = binStartIndex; binIndex < _numPeakBins; ++binIndex)
            {
                // stop once past the query
                if (_ranges[binIndex].first > query) break;
                if (_ranges[binIndex].first <= query && query <= _ranges[binIndex].second)
                {
                    m.row(rowNum)[binIndex] += intensityArray->data[queryIndex];
                }
            }
        }
        // for (int i = 0; i < _numPeakBins; ++i) intensities[i] *= weight;
        m.row(rowNum) *= weight;
    }

    size_t SpectrumPeakExtractor::numPeaks() const
    {
        return _numPeakBins;
    }
} // namespace analysis
} // namespace pwiz