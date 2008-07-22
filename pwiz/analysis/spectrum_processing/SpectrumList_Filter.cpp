//
// SpectrumList_Filter.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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

#include "SpectrumList_Filter.hpp"
#include <stdexcept>
#include <iostream>


namespace pwiz {
namespace analysis {


using namespace pwiz::util;
using namespace pwiz::msdata;
using namespace std;
using boost::logic::tribool;
using boost::lexical_cast;


//
// SpectrumList_Filter::Impl
//


struct SpectrumList_Filter::Impl
{
    const SpectrumListPtr original;
    std::vector<SpectrumIdentity> spectrumIdentities; // local cache, with fixed up index fields
    std::vector<size_t> indexMap; // maps index -> original index

    Impl(SpectrumListPtr original, const Predicate& predicate);
    void pushSpectrum(const SpectrumIdentity& spectrumIdentity);
};


SpectrumList_Filter::Impl::Impl(SpectrumListPtr _original, const Predicate& predicate)
:   original(_original)
{
    if (!original.get()) throw runtime_error("[SpectrumList_Filter] Null pointer");

    // iterate through the spectra, using predicate to build the sub-list
    for (size_t i=0, end=original->size(); i<end; i++)
    {
        if (predicate.done()) break;

        // first try to determine acceptance based on SpectrumIdentity alone
        const SpectrumIdentity& spectrumIdentity = original->spectrumIdentity(i);
        tribool accepted = predicate.accept(spectrumIdentity);

        if (accepted)
        {
            pushSpectrum(spectrumIdentity);            
        }
        else if (!accepted)
        {
            // do nothing 
        }
        else // indeterminate
        {
            // not enough info -- we need to retrieve the Spectrum
            SpectrumPtr spectrum = original->spectrum(i, false);
            if (predicate.accept(*spectrum))
                pushSpectrum(spectrumIdentity);            
        }
    }
}


void SpectrumList_Filter::Impl::pushSpectrum(const SpectrumIdentity& spectrumIdentity)
{
    indexMap.push_back(spectrumIdentity.index);
    spectrumIdentities.push_back(spectrumIdentity);
    spectrumIdentities.back().index = spectrumIdentities.size()-1;
}


//
// SpectrumList_Filter
//


PWIZ_API_DECL SpectrumList_Filter::SpectrumList_Filter(const SpectrumListPtr original, const Predicate& predicate)
:   SpectrumListWrapper(original), impl_(new Impl(original, predicate))
{}


PWIZ_API_DECL size_t SpectrumList_Filter::size() const
{
    return impl_->indexMap.size();
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Filter::spectrumIdentity(size_t index) const
{
    return impl_->spectrumIdentities.at(index);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Filter::spectrum(size_t index, bool getBinaryData) const
{
    size_t originalIndex = impl_->indexMap.at(index);
    SpectrumPtr originalSpectrum = impl_->original->spectrum(originalIndex, getBinaryData);  

    SpectrumPtr newSpectrum(new Spectrum(*originalSpectrum));
    newSpectrum->index = index;

    return newSpectrum;
}


//
// SpectrumList_FilterPredicate_IndexSet 
//


PWIZ_API_DECL SpectrumList_FilterPredicate_IndexSet::SpectrumList_FilterPredicate_IndexSet(const IntegerSet& indexSet)
:   indexSet_(indexSet), eos_(false)
{}


PWIZ_API_DECL tribool SpectrumList_FilterPredicate_IndexSet::accept(const SpectrumIdentity& spectrumIdentity) const
{
    if (indexSet_.hasUpperBound((int)spectrumIdentity.index)) eos_ = true;
    bool result = indexSet_.contains((int)spectrumIdentity.index);
    return result;
}


PWIZ_API_DECL bool SpectrumList_FilterPredicate_IndexSet::done() const
{
    return eos_; // end of set
}


//
// SpectrumList_FilterPredicate_ScanNumberSet 
//


PWIZ_API_DECL SpectrumList_FilterPredicate_ScanNumberSet::SpectrumList_FilterPredicate_ScanNumberSet(const IntegerSet& scanNumberSet)
:   scanNumberSet_(scanNumberSet), eos_(false)
{}


PWIZ_API_DECL tribool SpectrumList_FilterPredicate_ScanNumberSet::accept(const SpectrumIdentity& spectrumIdentity) const
{
    int scanNumber = lexical_cast<int>(spectrumIdentity.nativeID);
    if (scanNumberSet_.hasUpperBound(scanNumber)) eos_ = true;
    bool result = scanNumberSet_.contains(scanNumber);
    return result;
}


PWIZ_API_DECL bool SpectrumList_FilterPredicate_ScanNumberSet::done() const
{
    return eos_; // end of set
}


} // namespace analysis
} // namespace pwiz
