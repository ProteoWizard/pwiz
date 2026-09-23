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

    boost::shared_ptr<SpectrumList_IgnoreCalibrationScans> result(new SpectrumList_IgnoreCalibrationScans(inner));

    // The declaration promised calibration spectra and the file has none - hand back the list we
    // were given rather than a wrapper that would report having removed them
    if (result->size() == inner->size())
        return inner;

    return result;
}


SpectrumList_IgnoreCalibrationScans::SpectrumList_IgnoreCalibrationScans(const SpectrumListPtr& inner)
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
            // FullMetadata rather than FullData so the arrays are not decoded. Note this is not as
            // cheap as it sounds for mzML: the parser still reads each encoded array off disk before
            // discarding it, so this pass costs a sequential read of the file. The fileContent gate
            // in create() is what keeps that off files with nothing to remove.
            SpectrumPtr s = inner_->spectrum(i, DetailLevel_FullMetadata);
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
