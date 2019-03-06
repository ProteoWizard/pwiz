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

#ifndef _SPECTRUMPEAKEXTRACTOR_HPP
#define _SPECTRUMPEAKEXTRACTOR_HPP

#include <vector>
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"
#include <boost/smart_ptr/shared_array.hpp>
#include "DemuxSolver.hpp"
#include "DemuxHelpers.hpp"

namespace pwiz{
namespace analysis{

    /// Extracts sets of centroided peaks from spectra using a user-defined list of peaks to extract
    class SpectrumPeakExtractor
    {
    public:

        /// Generates a SpectrumPeakExtractor 
        /// \param peakMzList The m/z values around which will be searched to centroid peaks.
        ///                   The size of this list defines how many peak centroids will be output.
        /// \param ppmError The tolerance that defines how far around a nominal peak to search relative to its m/z.
        /// \pre The peakMzList must be sorted from smallest to largest mz values with no duplicates.
        SpectrumPeakExtractor(const std::vector<double>& peakMzList, const pwiz::chemistry::MZTolerance& massError);

        /// Extracts centroided peaks from an input spectrum.
        /// The peaks extracted are chosen from the peakMzList provided during initialization of the SpectrumPeakExtractor.
        /// Peaks are extracted to a user-defined row of a user-provided matrix.
        /// \param[in] spectrum Spectrum from which peaks are searched and binned.
        /// \param[out] matrix The matrix into which peaks will be extracted.
        /// \param[in] rowNum The index of the matrix row to extract peaks to.
        /// \param[in] weight The relative weight to apply to this row (multiplies the output row by the given scalar).
        /// \pre The size of the output matrix row must be the same as the size of the peakMzList given during instantiation.
        void operator()(msdata::Spectrum_const_ptr spectrum, MatrixType& matrix, size_t rowNum, double weight=1.0) const;
        
        /// Returns the number of peaks extracted
        size_t numPeaks() const;
    
    private:
        
        boost::shared_array< std::pair<double, double> > _ranges; ///< defines the set of m/z windows to search, one for each peak in the search list.
        
        size_t _numPeakBins; ///< the number of peaks given in the search list peakMzList. This is also the number of centroids that will be output.
        
        double _maxDelta; ///< the m/z half-window size to bin around a peak based on the set ppm error.
        
        double _minValue; ///< the minimum m/z that will be searched for peaks across the full m/z range.
        
        double _maxValue; ///< the maximum m/z that will be searched for peaks across the full m/z range.
    };
    
} // namespace analysis
} // namespace pwiz

#endif // _SPECTRUMPEAKEXTRACTOR_HPP 