//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _SPECTRUMLIST_MSn_HPP_
#define _SPECTRUMLIST_MSn_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include "SpectrumListBase.hpp"
#include "SpectrumList_MSn.hpp"
#include <iosfwd>
#include <stdexcept>


namespace pwiz {
namespace msdata {

enum MSn_Type {MSn_Type_UNKNOWN, MSn_Type_BMS1, MSn_Type_CMS1, MSn_Type_BMS2, MSn_Type_CMS2, MSn_Type_MS1, MSn_Type_MS2};

struct MSnHeader 
{
    char header[16][128];
    MSnHeader()
    {
        for(int i=0; i<16; i++)
        {
            header[i][0] = '\0';
        }
    }
};

/// implementation of SpectrumList, backed by an MGF file
class PWIZ_API_DECL SpectrumList_MSn : public SpectrumListBase
{
    public:

    static SpectrumListPtr create(boost::shared_ptr<std::istream> is,
                                  const MSData& msd,
                                  MSn_Type filetype);
};


} // namespace msdata
} // namespace pwiz

#endif // _SPECTRUMLIST_MSn_HPP_

