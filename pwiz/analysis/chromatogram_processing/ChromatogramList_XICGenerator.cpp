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


#define PWIZ_SOURCE


#include "pwiz/utility/misc/Std.hpp"
#include "ChromatogramList_XICGenerator.hpp"
#include "pwiz/data/vendor_readers/Thermo/ChromatogramList_Thermo.hpp"


namespace pwiz {
namespace analysis {


using namespace msdata;
using namespace msdata::detail;
//using namespace pwiz::util;


PWIZ_API_DECL ChromatogramList_XICGenerator::ChromatogramList_XICGenerator(const msdata::ChromatogramListPtr& inner)
:   ChromatogramListWrapper(inner)
{
    
}


PWIZ_API_DECL bool ChromatogramList_XICGenerator::accept(const msdata::ChromatogramListPtr& inner)
{
    return true;
}


PWIZ_API_DECL ChromatogramPtr ChromatogramList_XICGenerator::xic(double startTime, double endTime, const boost::icl::interval_set<double>& massRanges, int msLevel)
{
    ChromatogramList_Thermo* thermo = dynamic_cast<ChromatogramList_Thermo*>(inner_.get());
    if (thermo == NULL)
        throw runtime_error("[ChromatogramList_XICGenerator] only works directly on Thermo ChromatogramLists");

    return thermo->xic(startTime, endTime, massRanges, msLevel);
}


} // namespace analysis 
} // namespace pwiz
