//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#include "SpectrumList_Sorter.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace analysis {


using namespace msdata;
using boost::logic::tribool;


//
// SpectrumList_Sorter::Impl
//


struct SpectrumList_Sorter::Impl
{
    const SpectrumListPtr original;
    std::vector<SpectrumIdentity> spectrumIdentities; // local cache, with fixed up index fields
    std::vector<size_t> indexMap; // maps index -> original index

    Impl(const SpectrumListPtr& original,
         const Predicate& predicate,
         bool stable);
    void pushSpectrum(const SpectrumIdentity& spectrumIdentity);
};


namespace {

// sorts index vector by calling the indexed spectra with the predicate
struct SortPredicate
{
    SortPredicate(const SpectrumListPtr& inner, const SpectrumList_Sorter::Predicate& predicate)
    :   inner(inner), predicate(predicate), detailLevel(DetailLevel_InstantMetadata)
    {
        if (inner->size() < 2)
            return;

        using namespace boost::logic;

        // determine the minimum detail level needed using the first two spectra

        if (indeterminate(predicate.less(inner->spectrumIdentity(0), inner->spectrumIdentity(1))))
        {
            needDetails = true;

            // not enough info -- we need to retrieve the Spectrum
            do
            {
                SpectrumPtr spectrum0 = inner->spectrum(0, detailLevel);
                SpectrumPtr spectrum1 = inner->spectrum(1, detailLevel);
                tribool lessThan = predicate.less(*spectrum0, *spectrum1);

                if (indeterminate(lessThan))
                {
                    if (detailLevel != DetailLevel_FullData)
                        detailLevel = DetailLevel(int(detailLevel) + 1);
                    else
                        throw runtime_error("[SortPredicate::ctor] indeterminate result at full detail level");
                }
                else
                    break;
            }
            while ((int) detailLevel <= (int) DetailLevel_FullData);
        }
        else
            needDetails = false;
    }

    bool operator() (size_t lhs, size_t rhs)
    {
        if (needDetails)
            return (bool) predicate.less(*inner->spectrum(lhs, detailLevel), *inner->spectrum(rhs, detailLevel));
        else
            return (bool) predicate.less(inner->spectrumIdentity(lhs), inner->spectrumIdentity(rhs));
    }

    const SpectrumListPtr inner;
    const SpectrumList_Sorter::Predicate& predicate;
    bool needDetails; // false iff spectrumIdentity is sufficient for sorting
    DetailLevel detailLevel; // the detail level needed for a non-indeterminate result
};

} // namespace


SpectrumList_Sorter::Impl::Impl(const SpectrumListPtr& _original,
                                const Predicate& predicate,
                                bool stable)
:   original(_original)
{
    if (!original.get()) throw runtime_error("[SpectrumList_Sorter] Null pointer");

    indexMap.resize(original->size());
    for (size_t i=0, end=original->size(); i < end; ++i )
        indexMap[i] = i;

    if (stable)
        stable_sort(indexMap.begin(), indexMap.end(), SortPredicate(original, predicate));
    else
        sort(indexMap.begin(), indexMap.end(), SortPredicate(original, predicate));

    spectrumIdentities.reserve(indexMap.size());
    for (size_t i=0, end=indexMap.size(); i < end; ++i )
    {
        spectrumIdentities.push_back(original->spectrumIdentity(indexMap[i]));
        spectrumIdentities.back().index = i;
    }
}


//
// SpectrumList_Sorter
//


PWIZ_API_DECL SpectrumList_Sorter::SpectrumList_Sorter(const SpectrumListPtr& original,
                                                       const Predicate& predicate,
                                                       bool stable)
:   SpectrumListWrapper(original), impl_(new Impl(original, predicate, stable))
{}


PWIZ_API_DECL size_t SpectrumList_Sorter::size() const
{
    return impl_->indexMap.size();
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_Sorter::spectrumIdentity(size_t index) const
{
    return impl_->spectrumIdentities.at(index);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_Sorter::spectrum(size_t index, bool getBinaryData) const
{
    size_t originalIndex = impl_->indexMap.at(index);
    SpectrumPtr originalSpectrum = impl_->original->spectrum(originalIndex, getBinaryData);  

    SpectrumPtr newSpectrum(new Spectrum(*originalSpectrum));
    newSpectrum->index = index;

    return newSpectrum;
}


//
// SpectrumList_SorterPredicate_ScanStartTime
//


PWIZ_API_DECL
tribool SpectrumList_SorterPredicate_ScanStartTime::less(const msdata::Spectrum& lhs,
                                                         const msdata::Spectrum& rhs) const
{
    if (lhs.scanList.empty() || rhs.scanList.empty())
        return boost::logic::indeterminate;
    CVParam lhsTime = lhs.scanList.scans[0].cvParam(MS_scan_start_time);
    CVParam rhsTime = rhs.scanList.scans[0].cvParam(MS_scan_start_time);
    if (lhsTime.empty() || rhsTime.empty())
        return boost::logic::indeterminate;
    return lhsTime.timeInSeconds() < rhsTime.timeInSeconds();
}


} // namespace analysis
} // namespace pwiz
