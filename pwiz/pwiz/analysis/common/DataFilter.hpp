//
// $Id$
//
//
// Original author: Chris Paulse <cpaulse <a.t> systemsbiology.org>
//
// Copyright 2009 Institute for Systems Biology, Seattle, WA
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


#ifndef _DATAFILTER_HPP_ 
#define _DATAFILTER_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "boost/shared_ptr.hpp"


namespace pwiz {
namespace analysis {


struct PWIZ_API_DECL SpectrumDataFilter
{
    virtual void operator () (const pwiz::msdata::SpectrumPtr) const = 0;
    virtual void describe(pwiz::msdata::ProcessingMethod&) const = 0;
    virtual ~SpectrumDataFilter() {}
};

typedef boost::shared_ptr<SpectrumDataFilter> SpectrumDataFilterPtr;


struct PWIZ_API_DECL ChromatogramDataFilter
{
    virtual void operator () (const pwiz::msdata::ChromatogramPtr) const = 0;
    virtual void describe(pwiz::msdata::ProcessingMethod&) const = 0;
    virtual ~ChromatogramDataFilter() {}
};

typedef boost::shared_ptr<ChromatogramDataFilter> ChromatogramDataFilterPtr;


} // namespace analysis 
} // namespace pwiz


#endif // _DATAFILTER_HPP_ 
