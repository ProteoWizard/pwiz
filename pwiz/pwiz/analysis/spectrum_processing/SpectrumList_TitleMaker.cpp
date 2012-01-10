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


#define PWIZ_SOURCE


#include "SpectrumList_TitleMaker.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace analysis {


using namespace msdata;
using namespace pwiz::util;


PWIZ_API_DECL
SpectrumList_TitleMaker::SpectrumList_TitleMaker(const msdata::MSData& msd, const string& format)
:   SpectrumListWrapper(msd.run.spectrumListPtr), msd_(msd), format_(format)
{
    nativeIdFormat_ = id::getDefaultNativeIDFormat(msd);

    // title adding isn't really worth a processingMethod, is it?
}


namespace {

template <typename value_type>
void replaceCvParam(ParamContainer& pc, CVID cvid, const value_type& value)
{
    vector<CVParam>::iterator itr;
    
    itr = std::find(pc.cvParams.begin(), pc.cvParams.end(), cvid);
    if (itr == pc.cvParams.end())
        pc.set(cvid, value);
    else
        itr->value = lexical_cast<string>(value);
}

} // namespace


PWIZ_API_DECL SpectrumPtr SpectrumList_TitleMaker::spectrum(size_t index, bool getBinaryData) const
{
    SpectrumPtr s = inner_->spectrum(index, getBinaryData);

    /// <RunId> - Run::id
    /// <Index> - SpectrumIdentity::index
    /// <Id> - SpectrumIdentity::id (nativeID)
    /// <ScanNumber> - if the nativeID can be represented as a single number, that number, else index
    /// <ActivationType> - for the first precursor, Activation::cvParamChild("dissociation method")
    /// <IsolationMz> - for the first precursor, IsolationWindow::cvParam("isolation target m/z")
    /// <SelectedIonMz> - for the first selected ion of the first precursor, SelectedIon::cvParam("selected ion m/z")
    /// <ChargeState> - for the first selected ion of the first precursor, SelectedIon::cvParam("charge state")
    /// <PrecursorSpectrumId> - for the first precursor, Precursor::spectrumID or Precursor::externalSpectrumID
    /// <SpectrumType> - Spectrum::cvParamChild("spectrum type")
    /// <MsLevel> - Spectrum::cvParam("ms level")
    /// <ScanStartTime> - for the first scan, Scan::cvParam("scan start time")
    /// <BasePeakMz> - Spectrum::cvParam("base peak m/z")
    /// <BasePeakIntensity> - Spectrum::cvParam("base peak intensity")
    /// <TotalIonCurrent> - Spectrum::cvParam("total ion current")

    string title = format_;

    bal::replace_all(title, "<RunId>", msd_.run.id);
    bal::replace_all(title, "<Index>", lexical_cast<string>(s->index));
    bal::replace_all(title, "<Id>", s->id);

    string scanNumberStr = id::translateNativeIDToScanNumber(nativeIdFormat_, s->id);
    if (scanNumberStr.empty())
        scanNumberStr = lexical_cast<string>(s->index+1); // scanNumber is a 1-based index for some nativeID formats
    bal::replace_all(title, "<ScanNumber>", scanNumberStr);

    if (!s->precursors.empty())
    {
        const Precursor& p = s->precursors[0];

        vector<string> activationTypes;
        BOOST_FOREACH(const CVParam& cvParam, p.activation.cvParamChildren(MS_dissociation_method))
            activationTypes.push_back(cv::cvTermInfo(cvParam.cvid).shortName());

        bal::replace_all(title, "<ActivationType>", bal::join(activationTypes, "/"));
        bal::replace_all(title, "<IsolationMz>", p.isolationWindow.cvParam(MS_isolation_window_target_m_z).value);

        if (!p.selectedIons.empty())
        {
            bal::replace_all(title, "<SelectedIonMz>", p.selectedIons[0].cvParam(MS_selected_ion_m_z).value);
            bal::replace_all(title, "<ChargeState>", p.selectedIons[0].cvParam(MS_charge_state).value);
        }
        else
        {
            bal::replace_all(title, "<SelectedIonMz>", "");
            bal::replace_all(title, "<ChargeState>", "");
        }

        if (!p.spectrumID.empty())
            bal::replace_all(title, "<PrecursorSpectrumId>", p.spectrumID);
        else if (!p.externalSpectrumID.empty())
            bal::replace_all(title, "<PrecursorSpectrumId>", p.externalSpectrumID);
    }
    else
    {
        bal::replace_all(title, "<ActivationType>", "");
        bal::replace_all(title, "<IsolationMz>", "");
        bal::replace_all(title, "<SelectedIonMz>", "");
        bal::replace_all(title, "<ChargeState>", "");
        bal::replace_all(title, "<PrecursorSpectrumId>", "");
    }

    double scanStartTimeInSeconds = s->scanList.scans.empty() ? 0 : s->scanList.scans[0].cvParam(MS_scan_start_time).timeInSeconds();
    bal::replace_all(title, "<ScanStartTimeInSeconds>", lexical_cast<string>(scanStartTimeInSeconds));
    bal::replace_all(title, "<ScanStartTimeInMinutes>", lexical_cast<string>(scanStartTimeInSeconds / 60));

    bal::replace_all(title, "<SpectrumType>", s->cvParamChild(MS_spectrum_type).name());
    bal::replace_all(title, "<MsLevel>", s->cvParam(MS_ms_level).value);
    bal::replace_all(title, "<BasePeakMz>", s->cvParam(MS_base_peak_m_z).value);
    bal::replace_all(title, "<BasePeakIntensity>", s->cvParam(MS_base_peak_intensity).value);
    bal::replace_all(title, "<TotalIonCurrent>", s->cvParam(MS_TIC).value);

    replaceCvParam(*s, MS_spectrum_title, title);

    return s;
}


} // namespace analysis 
} // namespace pwiz
