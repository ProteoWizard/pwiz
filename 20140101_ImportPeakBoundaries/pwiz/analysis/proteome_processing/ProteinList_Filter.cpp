//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2012 Vanderbilt University - Nashville, TN 37232
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

#include "pwiz/data/common/cv.hpp"
#include "ProteinList_Filter.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace analysis {


using namespace pwiz::cv;
using namespace pwiz::util;
using namespace pwiz::proteome;

using boost::logic::tribool;


//
// ProteinList_Filter::Impl
//


struct ProteinList_Filter::Impl
{
    const ProteinListPtr original;
    vector<size_t> indexMap; // maps index -> original index
    bool getSequence; // true if sequence is needed for a non-indeterminate result

    Impl(ProteinListPtr original, const Predicate& predicate);
    void pushProtein(const Protein& protein);
};


ProteinList_Filter::Impl::Impl(ProteinListPtr _original, const Predicate& predicate)
:   original(_original), getSequence(false)
{
    if (!original.get()) throw runtime_error("[ProteinList_Filter] Null pointer");

    // iterate through the proteins, using predicate to build the sub-list
    for (size_t i=1, end=original->size(); i<=end; ++i)
    {
        if (predicate.done()) break;

        ProteinPtr protein = original->protein(i-1, getSequence);
        tribool accepted = predicate.accept(*protein);

        if (accepted || getSequence) // if still indeterminate with getSequence = true, it passes the filter by default
        {
            pushProtein(*protein);            
        }
        else if (!accepted)
        {
            // do nothing 
        }
        else // indeterminate and !getSequence
        {
            // try again with getSequence = true
            getSequence = true;
            --i;
        }
    }
}


void ProteinList_Filter::Impl::pushProtein(const Protein& protein)
{
    indexMap.push_back(protein.index);
}


//
// ProteinList_Filter
//


PWIZ_API_DECL ProteinList_Filter::ProteinList_Filter(const ProteinListPtr original, const Predicate& predicate)
:   ProteinListWrapper(original), impl_(new Impl(original, predicate))
{}


PWIZ_API_DECL size_t ProteinList_Filter::size() const
{
    return impl_->indexMap.size();
}


PWIZ_API_DECL ProteinPtr ProteinList_Filter::protein(size_t index, bool getSequence) const
{
    size_t originalIndex = impl_->indexMap.at(index);
    ProteinPtr originalProtein = impl_->original->protein(originalIndex, getSequence);  

    ProteinPtr newProtein(new Protein(*originalProtein));
    newProtein->index = index;

    return newProtein;
}


//
// ProteinList_FilterPredicate_IndexSet 
//


PWIZ_API_DECL ProteinList_FilterPredicate_IndexSet::ProteinList_FilterPredicate_IndexSet(const IntegerSet& indexSet)
:   indexSet_(indexSet), eos_(false)
{}


PWIZ_API_DECL tribool ProteinList_FilterPredicate_IndexSet::accept(const Protein& protein) const
{
    if (indexSet_.hasUpperBound((int)protein.index)) eos_ = true;
    bool result = indexSet_.contains((int)protein.index);
    return result;
}


PWIZ_API_DECL bool ProteinList_FilterPredicate_IndexSet::done() const
{
    return eos_; // end of set
}


//
// ProteinList_FilterPredicate_IdSet 
//


PWIZ_API_DECL ProteinList_FilterPredicate_IdSet::ProteinList_FilterPredicate_IdSet(const set<string>& idSet)
:   idSet_(idSet)
{}


PWIZ_API_DECL tribool ProteinList_FilterPredicate_IdSet::accept(const Protein& protein) const
{
    return idSet_.erase(protein.id) > 0;
}


PWIZ_API_DECL bool ProteinList_FilterPredicate_IdSet::done() const
{
    return idSet_.empty(); // end of set
}


} // namespace analysis
} // namespace pwiz
