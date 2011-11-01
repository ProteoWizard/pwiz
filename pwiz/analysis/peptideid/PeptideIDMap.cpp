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

struct local_iterator : public PeptideID::IteratorInternal
{
    local_iterator(map<string, PeptideID::Record>::const_iterator it)
        : it(it)
    {}
    

private:
    friend class boost::iterator_core_access;

    virtual void increment() { it++; }

    virtual bool equal(const boost::shared_ptr<PeptideID::IteratorInternal>& li) const
    {
        local_iterator* lip = dynamic_cast<local_iterator*>(li.get());
        if (lip)
            return it == lip->it;

        return false;
    }

    virtual const PeptideID::Record& dereference() const { return it->second; }

    map<string, PeptideID::Record>::const_iterator it;
};

typedef boost::shared_ptr<local_iterator> plocal_iterator;
}

namespace pwiz {
namespace peptideid {


PWIZ_API_DECL PeptideID::Record PeptideIDMap::record(const Location& location) const
{
    map<string,PeptideID::Record>::const_iterator it = this->find(location.nativeID);
    if (it !=  map<string,PeptideID::Record>::end()) return it->second;
    return PeptideID::Record();
}

PWIZ_API_DECL PeptideID::Iterator PeptideIDMap::begin() const
{
    return PeptideID::Iterator(plocal_iterator(new local_iterator(map<string,PeptideID::Record>::begin())));
}

PWIZ_API_DECL PeptideID::Iterator PeptideIDMap::end() const
{
    return PeptideID::Iterator(plocal_iterator(new local_iterator(map<string,PeptideID::Record>::end())));
}

} // namespace peptideid
} // namespace pwiz

