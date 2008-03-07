//
// SpectrumInfo.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _SPECTRUMINFO_HPP_ 
#define _SPECTRUMINFO_HPP_ 


#include "MSDataAnalyzer.hpp"


namespace pwiz {
namespace analysis {


using namespace msdata;


/// simple structure for holding Spectrum info 
struct SpectrumInfo
{
    /// structure for Precursor info 
    struct PrecursorInfo
    {
        size_t index;
        double mz;
        double intensity;
        double charge;

        PrecursorInfo() : index(0), mz(0), intensity(0), charge(0) {}
    };

    size_t index;
    std::string id;
    std::string nativeID;
    int scanNumber;
    CVID massAnalyzerType;
    int scanEvent;
    int msLevel;
    double retentionTime; // seconds
    std::string filterString;
    double mzLow;
    double mzHigh;
    double basePeakMZ;
    double basePeakIntensity;
    double totalIonCurrent;
    std::vector<PrecursorInfo> precursors;
    std::vector<MZIntensityPair> data;

    SpectrumInfo()
    :   index(0), scanNumber(0), massAnalyzerType(CVID_Unknown), scanEvent(0), 
        msLevel(0), retentionTime(0), mzLow(0), mzHigh(0), basePeakMZ(0), 
        basePeakIntensity(0), totalIonCurrent(0)
    {}

    void update(const Spectrum& spectrum);
};


} // namespace analysis 
} // namespace pwiz


#endif // _SPECTRUMINFO_HPP_ 

