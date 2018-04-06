//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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


#ifndef _EXTENDEDREADERLIST_HPP_
#define _EXTENDEDREADERLIST_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/DefaultReaderList.hpp"


namespace pwiz {
namespace msdata {


/// default ReaderList, extended to include vendor readers
class PWIZ_API_DECL ExtendedReaderList : public DefaultReaderList
{
    public:
    ExtendedReaderList();
};


} // namespace msdata
} // namespace pwiz


#endif// _EXTENDEDREADERLIST_HPP_

