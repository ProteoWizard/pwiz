//
// $Id$
//
//
// Original author: Brian Pratt <bspratt at proteinms.net>
//
// Copyright 2026 University of Washington, Seattle, WA
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

#include "SpectrumList_IgnoreCalibrationScans.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace msdata {


using namespace pwiz::cv;


PWIZ_API_DECL SpectrumListPtr SpectrumList_IgnoreCalibrationScans::create(const SpectrumListPtr& inner, const MSData& msd)
{
    if (!inner.get())
        return inner;

    if (!msd.fileDescription.fileContent.hasCVParam(MS_calibration_spectrum))
        return inner;

    // Ask for the cheapest detail level that actually populates the term rather than assuming the
    // worst. For mzML that is the difference between reading every spectrum's encoded arrays off
    // disk and not touching them at all.
    //
    // The predicate needs three answers, not two. Returning a plain bool would make the first
    // non-calibration spectrum escalate all the way to FullData and stay there, since false is how
    // min_level_accepted is told to try a higher level. So: indeterminate once this spectrum's
    // types are populated and it simply is not calibration data (ask another spectrum, keep the
    // level), and false only while nothing is populated yet (read this one in more detail). Same
    // shape as VendorReaderTestHarness.cpp's msLevel probe.
    DetailLevel detailLevel;
    try
    {
        detailLevel = inner->min_level_accepted([](const Spectrum& s) -> boost::tribool
        {
            if (s.hasCVParam(MS_calibration_spectrum))
                return true;
            if (s.hasCVParamChild(MS_spectrum_type))
                return boost::indeterminate;
            return false;
        });
    }
    catch (std::exception&)
    {
        // Either no spectrum carries the term at any detail level - the fileContent declaration was
        // false, see ProteoWizard #4499 - or a spectrum could not be read at all. Both end the same
        // way: hand back the list we were given, so calibrationSpectraAreOmitted() cannot claim to
        // have removed something, and behavior falls back to what it was before this wrapper.
        return inner;
    }

    boost::shared_ptr<SpectrumList_IgnoreCalibrationScans> result(new SpectrumList_IgnoreCalibrationScans(inner, detailLevel));

    // min_level_accepted found a spectrum carrying the term at this level, so the scan below saw it
    // too and the sizes must differ. Kept as an explicit invariant rather than an inferred one,
    // because calibrationSpectraAreOmitted() reports on it.
    if (result->size() == inner->size())
        return inner;

    return result;
}


SpectrumList_IgnoreCalibrationScans::SpectrumList_IgnoreCalibrationScans(const SpectrumListPtr& inner, DetailLevel detailLevel)
:   SpectrumListWrapper(inner)
{
    size_t innerSize = inner_->size();
    indexMap_.reserve(innerSize);
    identities_.reserve(innerSize);

    for (size_t i = 0; i < innerSize; ++i)
    {
        bool isCalibration = false;
        try
        {
            // create() resolved the cheapest level that populates the term, so this is as little
            // reading as the question can be answered with. The fileContent gate keeps the pass off
            // files with nothing to remove in the first place.
            SpectrumPtr s = inner_->spectrum(i, detailLevel);
            isCalibration = s.get() && s->hasCVParam(MS_calibration_spectrum);
        }
        catch (std::exception&)
        {
            // A spectrum that cannot be read cannot be identified as calibration data, and refusing
            // to open the file over it would be a worse answer than the one the caller asked for -
            // msconvert's --continueOnError is applied downstream and would never get the chance.
            // Keep it and let whatever reads it next report the problem.
        }

        if (isCalibration)
            continue;

        indexMap_.push_back(i);
        identities_.push_back(inner_->spectrumIdentity(i));
        identities_.back().index = identities_.size() - 1;
    }
}


PWIZ_API_DECL size_t SpectrumList_IgnoreCalibrationScans::size() const
{
    return indexMap_.size();
}


PWIZ_API_DECL const SpectrumIdentity& SpectrumList_IgnoreCalibrationScans::spectrumIdentity(size_t index) const
{
    if (index >= identities_.size())
        throw runtime_error("[SpectrumList_IgnoreCalibrationScans::spectrumIdentity] Bad index: " + lexical_cast<string>(index));
    return identities_[index];
}


PWIZ_API_DECL SpectrumPtr SpectrumList_IgnoreCalibrationScans::spectrum(size_t index, bool getBinaryData) const
{
    return spectrum(index, getBinaryData ? DetailLevel_FullData : DetailLevel_FullMetadata);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_IgnoreCalibrationScans::spectrum(size_t index, DetailLevel detailLevel) const
{
    if (index >= indexMap_.size())
        throw runtime_error("[SpectrumList_IgnoreCalibrationScans::spectrum] Bad index: " + lexical_cast<string>(index));

    SpectrumPtr originalSpectrum = inner_->spectrum(indexMap_[index], detailLevel);
    if (!originalSpectrum.get())
        return originalSpectrum;

    // Copy before renumbering. Some lists hand back a pointer into their own storage -
    // SpectrumListSimple returns the element itself, SpectrumListCache its cached copy - so
    // assigning index in place would renumber the inner list's spectrum, not ours.
    SpectrumPtr newSpectrum(new Spectrum(*originalSpectrum));
    newSpectrum->index = index; // the inner list numbered this one counting the spectra we hide
    return newSpectrum;
}


PWIZ_API_DECL size_t SpectrumList_IgnoreCalibrationScans::fromInnerIndex(size_t innerIndex) const
{
    if (innerIndex >= inner_->size())
        return size(); // the inner list did not recognize the id either

    // indexMap_ is ascending, so an entry's position in it is the index we report for it
    vector<size_t>::const_iterator it = lower_bound(indexMap_.begin(), indexMap_.end(), innerIndex);
    if (it == indexMap_.end() || *it != innerIndex)
        return size(); // found, but it is one of the ones being hidden
    return it - indexMap_.begin();
}


PWIZ_API_DECL size_t SpectrumList_IgnoreCalibrationScans::find(const string& id) const
{
    return fromInnerIndex(inner_->find(id));
}


PWIZ_API_DECL size_t SpectrumList_IgnoreCalibrationScans::findAbbreviated(const string& abbreviatedId, char delimiter) const
{
    return fromInnerIndex(inner_->findAbbreviated(abbreviatedId, delimiter));
}


PWIZ_API_DECL bool SpectrumList_IgnoreCalibrationScans::calibrationSpectraAreOmitted() const
{
    return indexMap_.size() != inner_->size() || inner_->calibrationSpectraAreOmitted();
}


} // namespace msdata
} // namespace pwiz
