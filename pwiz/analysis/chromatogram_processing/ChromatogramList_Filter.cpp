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

#include "pwiz/data/common/cv.hpp"
#include "ChromatogramList_Filter.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace analysis {


using namespace pwiz::cv;
using namespace pwiz::util;
using namespace pwiz::msdata;

using boost::logic::tribool;


//
// ChromatogramList_Filter::Impl
//


struct ChromatogramList_Filter::Impl
{
    const ChromatogramListPtr original;
    std::vector<ChromatogramIdentity> chromatogramIdentities; // local cache, with fixed up index fields
    std::vector<size_t> indexMap; // maps index -> original index
    bool detailLevel; // the detail level needed for a non-indeterminate result

    Impl(ChromatogramListPtr original, const Predicate& predicate);
    void pushChromatogram(const ChromatogramIdentity& chromatogramIdentity);
};


ChromatogramList_Filter::Impl::Impl(ChromatogramListPtr _original, const Predicate& predicate)
:   original(_original), detailLevel(predicate.suggestedDetailLevel())
{
    if (!original.get()) throw runtime_error("[ChromatogramList_Filter] Null pointer");

    // iterate through the chromatograms, using predicate to build the sub-list
    for (size_t i=0, end=original->size(); i<end; i++)
    {
        if (predicate.done()) break;

        // first try to determine acceptance based on ChromatogramIdentity alone
        const ChromatogramIdentity& chromatogramIdentity = original->chromatogramIdentity(i);
        tribool accepted = predicate.accept(chromatogramIdentity);

        if (accepted)
        {
            pushChromatogram(chromatogramIdentity);            
        }
        else if (!accepted)
        {
            // do nothing 
        }
        else // indeterminate
        {
            // not enough info -- we need to retrieve the Chromatogram
            detailLevel = true;
            ChromatogramPtr chromatogram = original->chromatogram(i, true);

            if (predicate.accept(*chromatogram))
                pushChromatogram(chromatogramIdentity);
        }
    }
}


void ChromatogramList_Filter::Impl::pushChromatogram(const ChromatogramIdentity& chromatogramIdentity)
{
    indexMap.push_back(chromatogramIdentity.index);
    chromatogramIdentities.push_back(chromatogramIdentity);
    chromatogramIdentities.back().index = chromatogramIdentities.size()-1;
}


//
// ChromatogramList_Filter
//


PWIZ_API_DECL ChromatogramList_Filter::ChromatogramList_Filter(const ChromatogramListPtr original, const Predicate& predicate)
:   ChromatogramListWrapper(original), impl_(new Impl(original, predicate))
{}


PWIZ_API_DECL size_t ChromatogramList_Filter::size() const
{
    return impl_->indexMap.size();
}


PWIZ_API_DECL const ChromatogramIdentity& ChromatogramList_Filter::chromatogramIdentity(size_t index) const
{
    return impl_->chromatogramIdentities.at(index);
}


PWIZ_API_DECL ChromatogramPtr ChromatogramList_Filter::chromatogram(size_t index, bool getBinaryData) const
{
    size_t originalIndex = impl_->indexMap.at(index);
    ChromatogramPtr originalChromatogram = impl_->original->chromatogram(originalIndex, getBinaryData);

    ChromatogramPtr newChromatogram(new Chromatogram(*originalChromatogram));
    newChromatogram->index = index;

    return newChromatogram;
}


//
// ChromatogramList_FilterPredicate_IndexSet 
//


PWIZ_API_DECL ChromatogramList_FilterPredicate_IndexSet::ChromatogramList_FilterPredicate_IndexSet(const IntegerSet& indexSet)
:   indexSet_(indexSet), eos_(false)
{}


PWIZ_API_DECL tribool ChromatogramList_FilterPredicate_IndexSet::accept(const ChromatogramIdentity& chromatogramIdentity) const
{
    if (indexSet_.hasUpperBound((int)chromatogramIdentity.index)) eos_ = true;
    bool result = indexSet_.contains((int)chromatogramIdentity.index);
    return result;
}


PWIZ_API_DECL bool ChromatogramList_FilterPredicate_IndexSet::done() const
{
    return eos_; // end of set
}


} // namespace analysis
} // namespace pwiz

