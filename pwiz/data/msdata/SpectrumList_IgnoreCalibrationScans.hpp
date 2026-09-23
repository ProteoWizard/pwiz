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


#ifndef _SPECTRUMLIST_IGNORECALIBRATIONSCANS_HPP_
#define _SPECTRUMLIST_IGNORECALIBRATIONSCANS_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "SpectrumListWrapper.hpp"


namespace pwiz {
namespace msdata {


/// Hides spectra labeled "calibration spectrum" (MS:1000928), which describe a calibration source
/// rather than the injected sample.
///
/// This is Reader::Config::ignoreCalibrationScans for formats that carry the label rather than a
/// vendor notion of a calibration function. Readers that know which scans are calibration without
/// reading them - SpectrumList_Waters by lockmass function, SpectrumList_UIMF by frame type - leave
/// them out of their index instead, which is cheaper; this exists for lists that can only tell by
/// looking, mzML above all.
class PWIZ_API_DECL SpectrumList_IgnoreCalibrationScans : public SpectrumListWrapper
{
    public:

    /// Wraps inner if msd says it contains calibration spectra AND at least one spectrum turns out
    /// to carry the term; returns inner untouched otherwise.
    ///
    /// Both conditions matter, for different reasons. The fileContent declaration keeps the scan
    /// off files with nothing to remove - nearly all of them - which is what makes this affordable
    /// for callers such as Skyline that set ignoreCalibrationScans for everything they open. And
    /// returning inner when the scan finds nothing keeps a wrapper that removed nothing from
    /// claiming, through calibrationSpectraAreOmitted, that it did: a file may declare the term in
    /// fileContent and carry it on no spectrum at all (see ProteoWizard #4499), and a consumer told
    /// "already handled" would then stand down over spectra that are all still present.
    ///
    /// A file whose fileContent does not admit to its calibration spectra keeps them, same as before.
    static SpectrumListPtr create(const SpectrumListPtr& inner, const MSData& msd);

    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel) const;

    /// The base class delegates these to the inner list only while the two are the same size, and
    /// hiding a spectrum makes them differ - which would drop id lookup from the inner list's hash
    /// onto a linear scan, and findAbbreviated onto a scan of scans. Remap instead.
    virtual size_t find(const std::string& id) const;
    virtual size_t findAbbreviated(const std::string& abbreviatedId, char delimiter = '.') const;

    /// True because this list removed something - create() does not build one otherwise - or
    /// because the list underneath had already done it.
    virtual bool calibrationSpectraAreOmitted() const;

    /// Nothing here serializes the inner list, and before this wrapper existed msconvert saw the
    /// reader's own list and used worker threads. Say so, or installing it silently drops
    /// conversion to a single thread: msconvert only asks this question of a SpectrumListWrapper,
    /// and the inherited answer is "no" whenever the list underneath is not itself a wrapper.
    virtual bool benefitsFromWorkerThreads() const {return true;}

    private:

    SpectrumList_IgnoreCalibrationScans(const SpectrumListPtr& inner);

    /// inner index -> our index, or size() if inner hides it or does not know it
    size_t fromInnerIndex(size_t innerIndex) const;

    std::vector<size_t> indexMap_;             // our index -> inner index
    std::vector<SpectrumIdentity> identities_; // inner identities, renumbered to be contiguous
};


} // namespace msdata
} // namespace pwiz


#endif // _SPECTRUMLIST_IGNORECALIBRATIONSCANS_HPP_
