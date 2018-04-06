//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
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


#ifndef _SPECTRUMLIST_BTDX_HPP_
#define _SPECTRUMLIST_BTDX_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "SpectrumListBase.hpp"
#include <iosfwd>
#include <stdexcept>


namespace pwiz {
namespace msdata {


/// SpectrumList backed by a Bruker BioTools DataExchange XML file
class PWIZ_API_DECL SpectrumList_BTDX : public SpectrumListBase
{
    public:

    static SpectrumListPtr create(boost::shared_ptr<std::istream> is,
                                  const MSData& msd);
};


} // namespace msdata
} // namespace pwiz


#endif // _SPECTRUMLIST_BTDX_HPP_

