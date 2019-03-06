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

#ifndef _MSXDEMULTIPLEXER_HPP
#define _MSXDEMULTIPLEXER_HPP

#include "IDemultiplexer.hpp"
#include "DemuxHelpers.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"

namespace pwiz {
namespace analysis {

    /// Implementation of the IDemultiplexer interface that is able to handle both MSX experiments, including ones with overlap. For analyzing overlap
    /// data without MSX it is recommended to use the OverlapDemultiplexer instead for better chromatographic interpolation.
    class MSXDemultiplexer : public IDemultiplexer
    {
    public:

        /// User-defined options for demultiplexing
        struct Params
        {
            Params() :
                massError(10.0, pwiz::chemistry::MZTolerance::PPM),
                applyWeighting(true),
                variableFill(false)
            {}

            /// Mass error for extracting MS/MS peaks
            pwiz::chemistry::MZTolerance massError;

            /// Weight the spectra nearby to the input spectrum more heavily in the solve
            /// than the outer ones
            bool applyWeighting;

            /// Set to true if fill times are allowed to vary for each scan window
            bool variableFill;
        };

        /// Constructs an MSXDemultiplexer with optional user-specified parameters
        /// @param p Options to use in demultiplexing (see Params for available options)
        explicit MSXDemultiplexer(Params p = Params());

        virtual ~MSXDemultiplexer();

        /// \name IDemultiplexer interface
        ///@{

        void Initialize(msdata::SpectrumList_const_ptr slc, IPrecursorMaskCodec::const_ptr pmc) override;
        void BuildDeconvBlock(size_t index,
            const std::vector<size_t>& muxIndices,
            DemuxTypes::MatrixPtr& masks,
            DemuxTypes::MatrixPtr& signal) const override;
        void GetMatrixBlockIndices(size_t indexToDemux, std::vector<size_t>& muxIndices, double demuxBlockExtra) const override;
        const std::vector<size_t>& SpectrumIndices() const override;
        ///@}
        
    private:

        /// A SpectrumList that provides access to the spectra specified in the muxIndices list provided to BuildDeconvBlock()
        msdata::SpectrumList_const_ptr sl_;

        /// An IPrecursorMaskCodec that provides information about the experiment's scheme and can generate the masks for given mux spectra
        IPrecursorMaskCodec::const_ptr pmc_;

        /// A set of user-defined options
        Params params_;

        /// A cache of the indices provided by SpectrumIndices()
        mutable std::vector<size_t> spectrumIndices_;
    };
} // namespace analysis
} // namespace pwiz
#endif // _MSXDEMULTIPLEXER_HPP