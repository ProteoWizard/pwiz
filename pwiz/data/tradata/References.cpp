//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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

#include "References.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace tradata {
namespace References {




template <typename object_type>
struct HasID
{
    const string& id_;
    HasID(const string& id) : id_(id) {}

    bool operator()(const shared_ptr<object_type>& objectPtr)
    {
        return objectPtr.get() && objectPtr->id == id_;
    }
};


template <typename object_type>
void resolve(shared_ptr<object_type>& reference, 
             const vector< shared_ptr<object_type> >& referentList)
{
    if (!reference.get() || reference->id.empty())
        return; 

    typename vector< shared_ptr<object_type> >::const_iterator it = 
        find_if(referentList.begin(), referentList.end(), HasID<object_type>(reference->id));

    if (it == referentList.end())
    {
        ostringstream oss;
        oss << "[References::resolve()] Failed to resolve reference.\n"
            << "  object type: " << typeid(object_type).name() << endl
            << "  reference id: " << reference->id << endl
            << "  referent list: " << referentList.size() << endl;
        for (typename vector< shared_ptr<object_type> >::const_iterator it=referentList.begin();
             it!=referentList.end(); ++it)
            oss << "    " << (*it)->id << endl;
        throw runtime_error(oss.str().c_str());
    }

    reference = *it;
}


template <typename object_type>
void resolve(vector < shared_ptr<object_type> >& references,
             const vector< shared_ptr<object_type> >& referentList)
{
    for (typename vector< shared_ptr<object_type> >::iterator it=references.begin();
         it!=references.end(); ++it)
        resolve(*it, referentList);
}


template <typename object_type>
void resolve(vector<object_type>& objects, const TraData& msd)
{
    for (typename vector<object_type>::iterator it=objects.begin(); it!=objects.end(); ++it)
        resolve(*it, msd);
}


template <typename object_type>
void resolve(vector< shared_ptr<object_type> >& objectPtrs, const TraData& msd)
{
    for (typename vector< shared_ptr<object_type> >::iterator it=objectPtrs.begin(); 
         it!=objectPtrs.end(); ++it)
        resolve(**it, msd);
}

PWIZ_API_DECL void resolve(RetentionTime& retentionTime, const TraData& td)
{
    resolve(retentionTime.softwarePtr, td.softwarePtrs);
}


PWIZ_API_DECL void resolve(Prediction& prediction, const TraData& td)
{
    resolve(prediction.softwarePtr, td.softwarePtrs);
    resolve(prediction.contactPtr, td.contactPtrs);
}


PWIZ_API_DECL void resolve(Configuration& configuration, const TraData& td)
{
    resolve(configuration.contactPtr, td.contactPtrs);
    resolve(configuration.instrumentPtr, td.instrumentPtrs);
}


PWIZ_API_DECL void resolve(Peptide& peptide, const TraData& td)
{
    BOOST_FOREACH(ProteinPtr& proteinPtr, peptide.proteinPtrs)
        resolve(proteinPtr, td.proteinPtrs);
}


PWIZ_API_DECL void resolve(Transition& transition, const TraData& td)
{
    resolve(transition.peptidePtr, td.peptidePtrs);
    resolve(transition.compoundPtr, td.compoundPtrs);
}


PWIZ_API_DECL void resolve(Target& target, const TraData& td)
{
    resolve(target.peptidePtr, td.peptidePtrs);
    resolve(target.compoundPtr, td.compoundPtrs);
}


PWIZ_API_DECL void resolve(TraData& td)
{
    resolve(td.peptidePtrs, td);
    resolve(td.transitions, td);
    resolve(td.targets.targetExcludeList, td);
    resolve(td.targets.targetIncludeList, td);
}


} // namespace References
} // namespace tradata
} // namespace pwiz

