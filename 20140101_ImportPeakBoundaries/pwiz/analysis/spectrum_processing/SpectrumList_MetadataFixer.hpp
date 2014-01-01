//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#ifndef _SPECTRUMLIST_METADATAFIXER_HPP_ 
#define _SPECTRUMLIST_METADATAFIXER_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"


namespace pwiz {
namespace analysis {


/// SpectrumList implementation to add (or replace) base peak and total ion metadata
/// with new values calculated from the current binary data.
class PWIZ_API_DECL SpectrumList_MetadataFixer : public msdata::SpectrumListWrapper
{
    public:

    SpectrumList_MetadataFixer(const msdata::SpectrumListPtr& inner);

    static bool accept(const msdata::SpectrumListPtr& inner);

    virtual msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;
};


} // namespace analysis 
} // namespace pwiz


#endif // _SPECTRUMLIST_METADATAFIXER_HPP_ 
