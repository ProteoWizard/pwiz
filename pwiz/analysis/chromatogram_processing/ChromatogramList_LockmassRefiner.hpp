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


#ifndef _CHROMATOGRAMLIST_LOCKMASSREFINER_HPP_ 
#define _CHROMATOGRAMLIST_LOCKMASSREFINER_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "ChromatogramListWrapper.hpp"


namespace pwiz {
namespace analysis {


/// ChromatogramList implementation to replace peak profiles with picked peaks
class PWIZ_API_DECL ChromatogramList_LockmassRefiner : public ChromatogramListWrapper
{
    public:

        ChromatogramList_LockmassRefiner(const msdata::ChromatogramListPtr& inner, double lockmassMzPosScans, double lockmassMzNegScans, double lockmassTolerance);


    static bool accept(const msdata::ChromatogramListPtr& inner);

    virtual msdata::ChromatogramPtr chromatogram(size_t index, bool getBinaryData = false) const;
    virtual msdata::ChromatogramPtr chromatogram(size_t index, msdata::DetailLevel detailLevel) const;

    private:
    double mzPositiveScans_, mzNegativeScans_, tolerance_;
};


} // namespace analysis 
} // namespace pwiz


#endif // _CHROMATOGRAMLIST_LOCKMASSREFINER_HPP_ 
