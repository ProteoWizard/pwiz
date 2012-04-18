//
// $Id$
//
//
// Original author: Brian Pratt <brian.pratt <a.t> insilicos.com>
//
// Copyright 2012  Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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


#ifndef _SPECTRUMLIST_ZeroSamplesFilter_HPP_ 
#define _SPECTRUMLIST_ZeroSamplesFilter_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"


namespace pwiz {
namespace analysis {


/// SpectrumList implementation to return smoothed spectral data
class PWIZ_API_DECL SpectrumList_ZeroSamplesFilter : public msdata::SpectrumListWrapper
{
    public:

    enum mode_t {REMOVE_ZEROS,ADD_MISSING_ZEROS};
    SpectrumList_ZeroSamplesFilter(const msdata::SpectrumListPtr& inner,const util::IntegerSet& msLevelsToFilter,
        mode_t mode, size_t FlankingZeroCount);


    static bool accept(const msdata::SpectrumListPtr& inner);

    virtual msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;

    private:
    const mode_t mode_; // REMOVE_ZEROS or ADD_MISSING_ZEROS
    const size_t flankingZeroCount_; // used if adding missing zeros
    const util::IntegerSet msLevelsToFilter_;
};


} // namespace analysis 
} // namespace pwiz


#endif // _SPECTRUMLIST_ZeroSamplesFilter_HPP_ 

