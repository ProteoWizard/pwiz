//
// $Id: SpectrumList_PeakFilter.hpp 1191 2009-08-14 19:33:05Z chambm $
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


#ifndef _IDATAFILTER_HPP_ 
#define _IDATAFILTER_HPP_ 


namespace pwiz {
namespace analysis {

struct ISpectrumDataFilter
{
    virtual void operator () (const pwiz::msdata::SpectrumPtr) const = 0;
    virtual void describe(pwiz::msdata::ProcessingMethod&) const = 0;
    virtual ~ISpectrumDataFilter() {}
};

struct IChromatogramDataFilter
{
    virtual void operator () (const pwiz::msdata::ChromatogramPtr) const = 0;
    virtual void describe(pwiz::msdata::ProcessingMethod&) const = 0;
    virtual ~IChromatogramDataFilter() {}
};

} // namespace analysis 
} // namespace pwiz


#endif // _ISPECTRUMDATAFILTER_HPP_ 
