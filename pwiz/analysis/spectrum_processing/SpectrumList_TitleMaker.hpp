//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
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


#ifndef _SPECTRUMLIST_TITLEMAKER_HPP_ 
#define _SPECTRUMLIST_TITLEMAKER_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"


namespace pwiz {
namespace analysis {


/// SpectrumList implementation to add (or replace) a spectrum title to each spectrum
/// according to a user-specified format.
class PWIZ_API_DECL SpectrumList_TitleMaker : public msdata::SpectrumListWrapper
{
    public:

    /// The format string recognizes some keywords to replace with the appropriate field:
    /// <RunId> - Run::id
    /// <Index> - SpectrumIdentity::index
    /// <Id> - SpectrumIdentity::id (nativeID)
    /// <ScanNumber> - if the nativeID can be represented as a single number, that number, else index
    /// <ActivationType> - for the first precursor, Activation::cvParamChild("dissociation method")
    /// <IsolationMz> - for the first precursor, IsolationWindow::cvParam("isolation target m/z")
    /// <SelectedIonMz> - for the first selected ion of the first precursor, SelectedIon::cvParam("selected ion m/z")
    /// <ChargeState> - for the first selected ion of the first precursor, SelectedIon::cvParam("charge state")
    /// <SpectrumType> - Spectrum::cvParamChild("spectrum type")
    /// <ScanStartTimeInSeconds> - for the first scan, Scan::cvParam("scan start time") converted to seconds
    /// <ScanStartTimeInMinutes> - for the first scan, Scan::cvParam("scan start time") converted to minutes
    /// <BasePeakMz> - Spectrum::cvParam("base peak m/z")
    /// <BasePeakIntensity> - Spectrum::cvParam("base peak intensity")
    /// <TotalIonCurrent> - Spectrum::cvParam("total ion current")
    /// <IonMobility> - Scan::cvParam("ion mobility drift time") or Scan::cvParam("inverse reduced ion mobility")
    SpectrumList_TitleMaker(const msdata::MSData& msd, const std::string& format);

    static bool accept(const msdata::SpectrumListPtr& inner) {return true;}

    virtual msdata::SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const;

    private:
    const msdata::MSData& msd_;
    const std::string format_;
    cv::CVID nativeIdFormat_;
};


} // namespace analysis 
} // namespace pwiz


#endif // _SPECTRUMLIST_TITLEMAKER_HPP_ 
