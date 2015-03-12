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

#define PWIZ_SOURCE
#include "MSIHandler.hpp"
#include "pwiz/utility/misc/Std.hpp"
namespace pwiz {
namespace util {


MSIHandler::Record::Record(const std::vector<std::string>& fields)
{
    scan = atol(fields.at(0).c_str());
    time = atof(fields.at(1).c_str());
    mz = atof(fields.at(2).c_str());
    mass = atof(fields.at(3).c_str());
    intensity = atof(fields.at(4).c_str());
    charge = atol(fields.at(5).c_str());
    chargeStates = atol(fields.at(6).c_str());
    kl = atof(fields.at(7).c_str());
    background = atof(fields.at(8).c_str());
    median = atof(fields.at(9).c_str());
    peaks = atol(fields.at(10).c_str());
    scanFirst = atol(fields.at(11).c_str());
    scanLast = atol(fields.at(12).c_str());
    scanCount = atol(fields.at(13).c_str());
}

MSIHandler::Record::Record(const Record& r)
{
    scan = r.scan;
    time = r.time;
    mz = r.mz;
    mass = r.mass;
    intensity = r.intensity;
    charge = r.charge;
    chargeStates = r.chargeStates;
    kl = r.kl;
    background = r.background;
    median = r.median;
    peaks = r.peaks;
    scanFirst = r.scanFirst;
    scanLast = r.scanLast;
    scanCount = r.scanCount;
}

class MSIHandler::Impl
{
    public:

    Impl(){}
    
    vector<MSIHandler::Record> records;
};

MSIHandler::MSIHandler()
    : pimpl(new Impl())
{
}
    
bool MSIHandler::updateRecord(const std::vector<std::string>& fields)
{
    bool result = true;
    
    Record record(fields);

    pimpl->records.push_back(record);

    return result;
}

size_t MSIHandler::size() const
{
    return pimpl->records.size();
}

MSIHandler::Record MSIHandler::record(size_t index) const
{
    return pimpl->records.at(index);
}

MSIHandler::Record MSIHandler::lastRecord() const
{
    return pimpl->records.at(pimpl->records.size()-1);
}

} // namespace pwiz
} // namespace utility
