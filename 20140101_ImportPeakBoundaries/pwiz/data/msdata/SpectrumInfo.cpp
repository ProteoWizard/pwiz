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


#include "SpectrumInfo.hpp"
#include "pwiz/utility/misc/Exception.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace msdata {
    

PWIZ_API_DECL SpectrumInfo::SpectrumInfo()
:   index((size_t)-1), scanNumber(0), massAnalyzerType(CVID_Unknown), scanEvent(0), 
    msLevel(0), isZoomScan(false), retentionTime(0), mzLow(0), mzHigh(0), basePeakMZ(0), 
    basePeakIntensity(0), totalIonCurrent(0), thermoMonoisotopicMZ(0), ionInjectionTime(0)
{}


PWIZ_API_DECL SpectrumInfo::SpectrumInfo(const Spectrum& spectrum)
:   index((size_t)-1), scanNumber(0), massAnalyzerType(CVID_Unknown), scanEvent(0), 
    msLevel(0), isZoomScan(false), retentionTime(0), mzLow(0), mzHigh(0), basePeakMZ(0), 
    basePeakIntensity(0), totalIonCurrent(0), thermoMonoisotopicMZ(0), ionInjectionTime(0)
{
    update(spectrum);
}


PWIZ_API_DECL void SpectrumInfo::update(const Spectrum& spectrum, bool getBinaryData)
{
    *this = SpectrumInfo();
    clearBinaryData();

    id = spectrum.id;
    index = spectrum.index;
    scanNumber = id::valueAs<int>(spectrum.id, "scan");

    Scan dummy;
    const Scan& scan = spectrum.scanList.scans.empty() ? dummy : spectrum.scanList.scans[0];

    massAnalyzerType = CVID_Unknown;
    if (scan.instrumentConfigurationPtr.get())
        try
        {
            massAnalyzerType = scan.instrumentConfigurationPtr->componentList.analyzer(0)
                                        .cvParamChild(MS_mass_analyzer_type).cvid;
        }
        catch (out_of_range&)
        {
            // ignore out-of-range exception
        }

    scanEvent = scan.cvParam(MS_preset_scan_configuration).valueAs<int>(); 
    msLevel = spectrum.cvParam(MS_ms_level).valueAs<int>();
    isZoomScan = spectrum.hasCVParam(MS_zoom_scan);
    retentionTime = scan.cvParam(MS_scan_start_time).timeInSeconds();
    filterString = scan.cvParam(MS_filter_string).value;
    mzLow = spectrum.cvParam(MS_lowest_observed_m_z).valueAs<double>();        
    mzHigh = spectrum.cvParam(MS_highest_observed_m_z).valueAs<double>();        
    basePeakMZ = spectrum.cvParam(MS_base_peak_m_z).valueAs<double>();    
    basePeakIntensity = spectrum.cvParam(MS_base_peak_intensity).valueAs<double>();    
    totalIonCurrent = spectrum.cvParam(MS_total_ion_current).valueAs<double>();  
    ionInjectionTime = scan.cvParam(MS_ion_injection_time).valueAs<double>();

    UserParam userParamMonoisotopicMZ = scan.userParam("[Thermo Trailer Extra]Monoisotopic M/Z:");
    if (!userParamMonoisotopicMZ.name.empty())
        thermoMonoisotopicMZ = userParamMonoisotopicMZ.valueAs<double>();        
 
    for (vector<Precursor>::const_iterator it=spectrum.precursors.begin(); it!=spectrum.precursors.end(); ++it)
    {
        PrecursorInfo precursorInfo;
        precursorInfo.index = 0; // TODO
        if (!it->selectedIons.empty())
        {
            precursorInfo.mz = it->selectedIons[0].cvParam(MS_selected_ion_m_z).valueAs<double>();
            precursorInfo.charge = it->selectedIons[0].cvParam(MS_charge_state).valueAs<int>();
            precursorInfo.intensity = it->selectedIons[0].cvParam(MS_peak_intensity).valueAs<double>();
        }
        precursors.push_back(precursorInfo);
    }

    dataSize = spectrum.defaultArrayLength;
    if (getBinaryData && !spectrum.binaryDataArrayPtrs.empty())
        spectrum.getMZIntensityPairs(data);
}


PWIZ_API_DECL void SpectrumInfo::clearBinaryData()
{
    vector<MZIntensityPair> nothing;
    data.swap(nothing);
}


PWIZ_API_DECL string SpectrumInfo::massAnalyzerTypeAbbreviation() const
{
    string result = "Unknown";

    if (cvIsA(massAnalyzerType, MS_ion_trap))
        result = "IonTrap";
    else if (massAnalyzerType == MS_FT_ICR)
        result = "FT";
    else if (massAnalyzerType == MS_orbitrap)
        result = "Orbitrap";

    return result;
}

PWIZ_API_DECL double SpectrumInfo::mzFromFilterString() const
{
    istringstream iss(filterString);
    vector<string> tokens;
    copy(istream_iterator<string>(iss), istream_iterator<string>(), back_inserter(tokens));
    
    for (vector<string>::const_iterator it=tokens.begin(), end=tokens.end(); it!=end; ++it)
    {
        string::size_type at = it->find("@");
        if (at != string::npos)
            return lexical_cast<double>(it->substr(0,at));
    }
    
    return 0;
}


} // namespace msdata
} // namespace pwiz

