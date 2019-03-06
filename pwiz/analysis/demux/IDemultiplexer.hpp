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

#ifndef _IDEMULTIPLEXER_HPP
#define _IDEMULTIPLEXER_HPP

#include "IPrecursorMaskCodec.hpp"

namespace pwiz{
namespace analysis{

    /// Interface for calculating demultiplexing scheme.
    class IDemultiplexer
    {
    public:

        /// Shared pointer definition
        typedef boost::shared_ptr<IDemultiplexer> ptr;

        /// Constant shared pointer definition
        typedef boost::shared_ptr<const IDemultiplexer> const_ptr;

        /// Initializes the demultiplexer using the demux scheme provided by an IPrecursorMaskCodec
        virtual void Initialize(msdata::SpectrumList_const_ptr slc, IPrecursorMaskCodec::const_ptr pmc) = 0;

        /// Translates a spectrum into a set of matrices to be solved by NNLS
        /// @param[in] index Index of the requested spectrum to be demultiplexed
        /// @param[in] muxIndices The indices to mulitplexed spectra to use for demultiplexing. These spectra should be near in time to the spectrum
        ///                       to demultiplex and there should be enough to provide a unique solution.
        /// @param[out] masks The design matrix with rows corresponding to individual spectra and columns corresponding to MS1 isolation windows
        /// @param[out] signal A transition (MS1 isolation -> MS2 point/centroid) to be deconvolved formatted as a column vector
        ///                   (or a set of transitions formatted as a matrix)
        virtual void BuildDeconvBlock(size_t index,
            const std::vector<size_t>& muxIndices,
            DemuxTypes::MatrixPtr& masks,
            DemuxTypes::MatrixPtr& signal) const = 0;

        /// Figures out which spectra to include in the system of equations to demux. This skips over MS1 spectra and returns the indices
        /// of a range of MS2 spectra that can be used to demultiplex the chosen spectrum. This handles the case where the chosen spectrum
        /// is at the beginning or end of a file and chooses a sufficient number of nearby MS2 spectra accordingly. More indices will be
        /// included if the user has chosen to add additional demux blocks.
        /// \post The returned indices are sorted
        /// @param[in] indexToDemux Index of the requested spectrum
        /// @param[out] muxIndices Indices of the multiplexed MS2 spectra to be used for demultiplexing
        /// @param[in] demuxBlockExtra Amount to pad the block size by
        virtual void GetMatrixBlockIndices(size_t indexToDemux, std::vector<size_t> &muxIndices, double demuxBlockExtra=0.0) const = 0;

        /// Returns the indices to the demultiplexed windows in the solution matrix corresponding to the windows extracted from the spectrum
        /// whose index was provided to BuildDeconvBlock()
        /// @return Returns the demux indices for the solved spectrum
        virtual const std::vector<size_t>& SpectrumIndices() const = 0;

        virtual ~IDemultiplexer() {}
    };
} // namespace analysis
} // namespace pwiz

#endif // _IDEMULTIPLEXER_HPP
