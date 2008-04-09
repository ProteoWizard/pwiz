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


SpectrumListFilter::SpectrumListFilter(SpectrumListPtr original, const Predicate& predicate)
:   original_(original)
{
    if (!original_.get()) throw runtime_error("[SpectrumListFilter] Null pointer");

    for (size_t i=0; i<original_->size(); i++)
        if (predicate.accept(*original_->spectrum(i, false))) 
            indexMap_.push_back(i);

    // TODO: use SpectrumIterator to end loop early when we have an IntegerSet 
}


size_t SpectrumListFilter::size() const
{
    return indexMap_.size();
}


const SpectrumIdentity& SpectrumListFilter::spectrumIdentity(size_t index) const
{
    size_t originalIndex = indexMap_.at(index);
    return original_->spectrumIdentity(originalIndex);

    // TODO: store index internally

}


SpectrumPtr SpectrumListFilter::spectrum(size_t index, bool getBinaryData) const
{
    size_t originalIndex = indexMap_.at(index);
    SpectrumPtr originalSpectrum = original_->spectrum(originalIndex, getBinaryData);  

    SpectrumPtr newSpectrum(new Spectrum(*originalSpectrum));
    newSpectrum->index = index;

    return newSpectrum;
}


} // namespace msdata
} // namespace pwiz


