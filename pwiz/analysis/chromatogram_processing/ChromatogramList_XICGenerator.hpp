//
// $Id$
//
//
// Original author: Eric Purser <Eric.Purser .@. Vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _CHROMATOGRAMLIST_XICGENERATOR_HPP_ 
#define _CHROMATOGRAMLIST_XICGENERATOR_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include <boost/icl/interval_set.hpp>
#include <boost/icl/continuous_interval.hpp>
#include "ChromatogramListWrapper.hpp"


namespace pwiz {
namespace analysis {


/// ChromatogramList implementation to return native centroided chromatogram data
class PWIZ_API_DECL ChromatogramList_XICGenerator : public ChromatogramListWrapper
{
    public:

    ChromatogramList_XICGenerator(const msdata::ChromatogramListPtr& inner);

    static bool accept(const msdata::ChromatogramListPtr& inner);

    virtual msdata::ChromatogramPtr xic(double startTime, double endTime, const boost::icl::interval_set<double>& massRanges, int msLevel);
};


} // namespace analysis 
} // namespace pwiz


#endif // _CHROMATOGRAMLIST_XICGENERATOR_HPP_ 

