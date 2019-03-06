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

#ifndef _SPECTRUMLIST_DEMUX_HPP
#define _SPECTRUMLIST_DEMUX_HPP

#include "pwiz/data/msdata/SpectrumListWrapper.hpp"
#include <boost/smart_ptr/scoped_ptr.hpp>
#include "pwiz/utility/chemistry/MZTolerance.hpp"

namespace pwiz {
namespace analysis {

    /// SpectrumList decorator implementation that can demultiplex spectra of several precursor windows acquired in the same scan.
    /** 
     * SpectrumList_Demux can separate multiplexed spectra into several demultiplexed spectra by inferring from adjacent multiplexed spectra. This method
     * can handle variable fill times, requiring that the user specify whether the fill times have varied.
    */
    class PWIZ_API_DECL SpectrumList_Demux : public msdata::SpectrumListWrapper
    {
    public:

        /// User-defined options for demultiplexing
        struct Params
        {
            /// Optimization methods available
            enum class Optimization
            {
                NONE,
                OVERLAP_ONLY
            };

            /// Converts an optimization enum to a string
            static std::string optimizationToString(Optimization opt);

            /// Converts a string to an optimization enum (returns NONE enum if no enum matches the string)
            static Optimization stringToOptimization(const std::string& s);

            Params() :
                massError(10, pwiz::chemistry::MZTolerance::PPM),
                demuxBlockExtra(0.0),
                nnlsMaxIter(50),
                nnlsEps(1e-10),
                applyWeighting(true),
                regularizeSums(true),
                variableFill(false),
                interpolateRetentionTime(true),
                optimization(Optimization::NONE),
                minimumWindowSize(0.2)
            {}

            /// Error scalar for extracting MS/MS peaks.
            pwiz::chemistry::MZTolerance massError;
            
            /// Multiplier to expand or reduce the # of spectra considered when demux'ing.
            /// If 0, a fully determined system of equation is built. If > 1.0, the number
            /// of rows included in the system is extended demuxBlockExtra * (# scans in 1 duty cycle)
            
            double demuxBlockExtra;
            
            /// Maximum iterations for NNLS solve
            int nnlsMaxIter;
            
            /// Epsilon value for convergence criterion of NNLS solver
            double nnlsEps;
            
            /// Weight the spectra nearby to the input spectrum more heavily in the solve
            /// than the outer ones. This is only applied if interpolateRetentionTime is false
            bool applyWeighting;
            
            /// After demux solve, scale the sum of the intensities contributed form each
            /// of the input windows to match the non-demux'd intensity
            bool regularizeSums;
            
            /// Set to true if fill times are allowed to vary for each scan window
            bool variableFill;
            
            bool interpolateRetentionTime;

            /// Optimizations can be chosen when experimental design is known
            Optimization optimization;

            double minimumWindowSize;
        };

        /// Generates an abstract SpectrumList_Demux decorator from inner SpectrumList
        /// @param inner The inner SpectrumList
        /// @param p User-defined options
        SpectrumList_Demux(const msdata::SpectrumListPtr& inner, const Params& p = Params());
        
        virtual ~SpectrumList_Demux();

        /// \name SpectrumList Interface
        ///@{

        msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;
        msdata::SpectrumPtr spectrum(size_t index, msdata::DetailLevel detailLevel) const;
        size_t size() const;
        const msdata::SpectrumIdentity& spectrumIdentity(size_t index) const;
        ///@}

    private:
        class Impl;
        boost::scoped_ptr<Impl> impl_;
    };

    typedef SpectrumList_Demux::Params::Optimization DemuxOptimization;

} // namespace analysis
} // namespace pwiz

#endif // _SPECTRUMLIST_DEMUX_HPP