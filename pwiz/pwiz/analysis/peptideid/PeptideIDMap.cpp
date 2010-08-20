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


#define PWIZ_SOURCE

#include "PeptideIDMap.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::peptideid;

namespace {

struct local_iterator : public PeptideID::Iterator
{
    local_iterator(map<string, PeptideID::Record>::const_iterator it,
                   map<string, PeptideID::Record>::const_iterator)
        : it(it), end(end)
    {}
    
    virtual PeptideID::Record next()
    {
        PeptideID::Record record = (*it).second;
        it++;
        
        return record;
    }
    
    virtual bool hasNext()
    {
        return it != end;
    }

    map<string, PeptideID::Record>::const_iterator it;
    map<string, PeptideID::Record>::const_iterator end;
};

}

namespace pwiz {
namespace peptideid {


PWIZ_API_DECL PeptideID::Record PeptideIDMap::record(const Location& location) const
{
    map<string,PeptideID::Record>::const_iterator it = this->find(location.nativeID);
    if (it != this->end()) return it->second;
    return PeptideID::Record();
}

PWIZ_API_DECL shared_ptr<PeptideID::Iterator> PeptideIDMap::iterator() const
{
    return shared_ptr<PeptideID::Iterator>(new local_iterator(begin(), end()));
}

} // namespace peptideid
} // namespace pwiz

