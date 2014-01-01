//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#ifndef _SPECTRUMLIST_PEAKFILTER_HPP_ 
#define _SPECTRUMLIST_PEAKFILTER_HPP_ 


#include <boost/scoped_ptr.hpp>
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"
#include "pwiz/analysis/common/DataFilter.hpp"


namespace pwiz {
namespace analysis {

/// SpectrumList implementation that returns spectra with the specified SpectrumDataFilter operation applied
class PWIZ_API_DECL SpectrumList_PeakFilter : public msdata::SpectrumListWrapper
{
    public:

    SpectrumList_PeakFilter(const msdata::SpectrumListPtr& inner,
                            SpectrumDataFilterPtr filterFunctor);

    /// peak filters work on any SpectrumList
    static bool accept(const msdata::SpectrumListPtr& inner) {return true;}

    virtual msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;

    private:
	SpectrumDataFilterPtr filterFunctor_;
};

} // namespace analysis 
} // namespace pwiz


#endif // _SPECTRUMLIST_PEAKFILTER_HPP_ 
