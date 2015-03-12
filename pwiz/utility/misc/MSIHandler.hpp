//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
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


#ifndef _MSIREADER_HPP_
#define _MSIREADER_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include "TabReader.hpp"
#include "boost/shared_ptr.hpp"

namespace pwiz {
namespace util {

class  PWIZ_API_DECL MSIHandler : public DefaultTabHandler
{
public:

    struct Record
    {
        Record() : scan(0), time(0), mz(0), intensity(0), charge(0), chargeStates(0), kl(0), background(0), median(0), peaks(0), scanFirst(0), scanLast(0), scanCount(0) {}
        Record(const std::vector<std::string>& fields);
        Record(const Record& r);
        
        size_t scan;
        float time;
        float mz;
        float mass;
        float intensity;
        int charge;
        int chargeStates;
        float kl;
        float background;
        float median;
        int peaks;
        int scanFirst;
        int scanLast;
        int scanCount;

    };
    
    MSIHandler();
    
    virtual ~MSIHandler() {}

    virtual bool updateRecord(const std::vector<std::string>& fields);

    size_t size() const;

    Record record(size_t index) const;

    Record lastRecord() const;
    
private:
    class Impl;
    boost::shared_ptr<Impl> pimpl;
};

} // namespace pwiz
} // namespace utility


#endif //  _MSIREADER_HPP_
