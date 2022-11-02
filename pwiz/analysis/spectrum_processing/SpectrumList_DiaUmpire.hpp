//
// Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
//
// Copyright 2020 Matt Chambers
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

#ifndef _SPECTRUMLIST_DIAUMPIRE_HPP
#define _SPECTRUMLIST_DIAUMPIRE_HPP

#include "pwiz/data/msdata/SpectrumListWrapper.hpp"
#include <boost/smart_ptr/scoped_ptr.hpp>
#include "pwiz/analysis/dia_umpire/DiaUmpire.hpp"

namespace pwiz {
namespace analysis {

    /// SpectrumList wrapper that generates pseudo-MS/MS spectra from DIA spectra using the DiaUmpire algorithm
    class PWIZ_API_DECL SpectrumList_DiaUmpire : public msdata::SpectrumListWrapper
    {
        public:

        /// Crates a SpectrumList_DiaUmpire wrapper around an inner SpectrumList
        SpectrumList_DiaUmpire(const pwiz::msdata::MSData& msd, const msdata::SpectrumListPtr& inner, const DiaUmpire::Config& config = DiaUmpire::Config(), const pwiz::util::IterationListenerRegistry* ilr = nullptr);
        
        virtual ~SpectrumList_DiaUmpire();

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

} // namespace analysis
} // namespace pwiz

#endif // _SPECTRUMLIST_DIAUMPIRE_HPP
