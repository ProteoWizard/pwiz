//
// SpectrumListFilter.cpp
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


#include "SpectrumListFilter.hpp"
#include <stdexcept>
#include <iostream>


namespace pwiz {
namespace msdata {


using namespace std;
using boost::logic::tribool;


//
// SpectrumListFilter::Impl
//


struct SpectrumListFilter::Impl
{
    const SpectrumListPtr original;
    std::vector<SpectrumIdentity> spectrumIdentities; // local cache, with fixed up index fields
    std::vector<size_t> indexMap; // maps index -> original index

    Impl(SpectrumListPtr original, const Predicate& predicate);
    void pushSpectrum(const SpectrumIdentity& spectrumIdentity);
};


SpectrumListFilter::Impl::Impl(SpectrumListPtr _original, const Predicate& predicate)
:   original(_original)
{
    if (!original.get()) throw runtime_error("[SpectrumListFilter] Null pointer");

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


void SpectrumListFilter::Impl::pushSpectrum(const SpectrumIdentity& spectrumIdentity)
{
    indexMap.push_back(spectrumIdentity.index);
    spectrumIdentities.push_back(spectrumIdentity);
    spectrumIdentities.back().index = spectrumIdentities.size()-1;
}


//
// SpectrumListFilter
//


SpectrumListFilter::SpectrumListFilter(const SpectrumListPtr original, const Predicate& predicate)
:   impl_(new Impl(original, predicate))
{}


size_t SpectrumListFilter::size() const
{
    return impl_->indexMap.size();
}


const SpectrumIdentity& SpectrumListFilter::spectrumIdentity(size_t index) const
{
    return impl_->spectrumIdentities.at(index);
}


SpectrumPtr SpectrumListFilter::spectrum(size_t index, bool getBinaryData) const
{
    size_t originalIndex = impl_->indexMap.at(index);
    SpectrumPtr originalSpectrum = impl_->original->spectrum(originalIndex, getBinaryData);  

    SpectrumPtr newSpectrum(new Spectrum(*originalSpectrum));
    newSpectrum->index = index;

    return newSpectrum;
}


} // namespace msdata
} // namespace pwiz


