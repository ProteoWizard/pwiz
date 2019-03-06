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

#ifndef _OVERLAPDEMULTIPLEXER_HPP
#define _OVERLAPDEMULTIPLEXER_HPP

#include "IDemultiplexer.hpp"
#include "DemuxHelpers.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"

namespace pwiz {
namespace analysis {
    
    /// Implementation of the IDemultiplexer interface that is able to handle overlap experiments.
    class OverlapDemultiplexer : public IDemultiplexer
    {
    public:
    
        /// User-defined options for demultiplexing
        struct Params
        {
            Params() :
            massError(10.0, pwiz::chemistry::MZTolerance::PPM),
            applyWeighting(true),
            interpolateRetentionTime(true)
            {}
            
            /// Mass error for extracting MS/MS peaks
            pwiz::chemistry::MZTolerance massError;
            
            /// Weight the spectra nearby to the input spectrum more heavily in the solve
            /// than the outer ones
            bool applyWeighting;
            
            /// Set to true to interpolate all scans in the demux block to the same retention time
            bool interpolateRetentionTime;
        };
        /// Constructs an OverlapDemultiplexer with optional user-specified parameters
        /// @param p Options to use in demultiplexing (see Params for available options)
        explicit OverlapDemultiplexer(Params p = Params());
        
        virtual ~OverlapDemultiplexer();
        
        /// \name IDemultiplexer interface
        ///@{

        void Initialize(msdata::SpectrumList_const_ptr sl, IPrecursorMaskCodec::const_ptr pmc) override;
        void BuildDeconvBlock(size_t index,
            const std::vector<size_t>& muxIndices,
            DemuxTypes::MatrixPtr& masks,
            DemuxTypes::MatrixPtr& signal) const override;
        void GetMatrixBlockIndices(size_t indexToDemux, std::vector<size_t>& muxIndices, double demuxBlockExtra) const override;
        const std::vector<size_t>& SpectrumIndices() const override;
        ///@}

    protected:

        /// Performs interpolation on a matrix of intensities using a vector of scanTimes and outputs them to a row vector of interpolated intensities
        /// @param[out] interpolatedIntensities The row vector of interpolated intensities mapped from timeToInterpolate.
        ///             The Ref template convinces Eigen that this can be column or row.
        /// @param[in] timeToInterpolate The time corresponding to the scan to be demuxed
        /// @param[in] intensities A matrix of intensities of a number of nearby spectra. Each spectrum should have a row of transitions.
        /// @param[in] scanTimes A vector of scanTimes. Because the same scan times are used for every transition in a given spectrum it is only
        ///            necessary to pass a vector of scanTimes rather than a matrix. This vector is reused for interpolating every transition.
        static void InterpolateMuxRegion(
            Eigen::Ref<Eigen::MatrixXd, 0, Eigen::Stride<Eigen::Dynamic, Eigen::Dynamic> > interpolatedIntensities,
            double timeToInterpolate,
            Eigen::Ref<const Eigen::MatrixXd> intensities,
            Eigen::Ref<const Eigen::VectorXd> scanTimes);
        
        /// Takes two vectors of equal length and solves an interpolation for the given point.
        /// @param pointToInterpolate Independent variable to interpolate
        /// @param points The independent values as a monotonically increasing series
        /// @param values The dependent values
        /// @return Returns the solved interpolation value
        /// \pre points must be sorted in order of increasing value with no duplicates
        /// \pre points and values must be of the same size
        static double InterpolateMatrix(double pointToInterpolate, Eigen::Ref<const Eigen::VectorXd> points, Eigen::Ref<const Eigen::VectorXd> values);

    private:
    
        /// The number of mux spectra nearby the spectrum to demux (in both retention time and m/z space) to use for demuxing
        size_t overlapRegionsInApprox_;
        
        /// The number of spectra with identical isolation parameters to use for interpolation
        size_t cyclesInBlock_;
        
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
#endif // _OVERLAPDEMULTIPLEXER_HPP