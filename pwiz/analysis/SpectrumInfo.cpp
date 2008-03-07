//
// SpectrumInfo.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "SpectrumInfo.hpp"


namespace pwiz {
namespace analysis {


using namespace std;
using boost::lexical_cast;
using boost::bad_lexical_cast;


namespace {
int nativeIDToScanNumber(const string& nativeID)
{
    try
    {
        return lexical_cast<int>(nativeID);
    }
    catch (bad_lexical_cast&)
    {
        return 0;
    }
}
} // namespace


void SpectrumInfo::update(const Spectrum& spectrum)
{
    const SpectrumDescription& sd = spectrum.spectrumDescription;

    id = spectrum.id;
    nativeID = spectrum.nativeID;
    index = spectrum.index;
    scanNumber = nativeIDToScanNumber(spectrum.nativeID);

    massAnalyzerType = sd.scan.cvParamChild(MS_mass_analyzer_type).cvid; // TODO: wait on spec
    if (massAnalyzerType == CVID_Unknown)
        massAnalyzerType = sd.scan.instrumentPtr.get() ? 
                                sd.scan.instrumentPtr->componentList.analyzer.cvParamChild(MS_mass_analyzer_type).cvid :
                                CVID_Unknown;

    scanEvent = sd.scan.cvParam(MS_preset_scan_configuration).valueAs<int>(); 
    msLevel = spectrum.cvParam(MS_ms_level).valueAs<int>();
    retentionTime = sd.scan.cvParam(MS_scan_time).timeInSeconds();
    filterString = sd.scan.cvParam(MS_filter_string).value;
    mzLow = sd.cvParam(MS_lowest_m_z_value).valueAs<double>();        
    mzHigh = sd.cvParam(MS_highest_m_z_value).valueAs<double>();        
    basePeakMZ = sd.cvParam(MS_base_peak_m_z).valueAs<double>();    
    basePeakIntensity = sd.cvParam(MS_base_peak_intensity).valueAs<double>();    
    totalIonCurrent = sd.cvParam(MS_total_ion_current).valueAs<double>();
 
    for (vector<Precursor>::const_iterator it=sd.precursors.begin(); it!=sd.precursors.end(); ++it)
    {
        PrecursorInfo precursorInfo;
        precursorInfo.index = 0; // TODO
        precursorInfo.mz = it->ionSelection.cvParam(MS_m_z).valueAs<double>();
        precursorInfo.charge = it->ionSelection.cvParam(MS_charge_state).valueAs<int>();
        precursorInfo.intensity = it->ionSelection.cvParam(MS_intensity).valueAs<double>();
        precursors.push_back(precursorInfo);
    }

    if (!spectrum.binaryDataArrayPtrs.empty())
        spectrum.getMZIntensityPairs(data);
}


} // namespace analysis 
} // namespace pwiz

