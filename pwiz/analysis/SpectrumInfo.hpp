//
// SpectrumInfo.hpp
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

        PrecursorInfo() : index(-1ul), mz(0), intensity(0), charge(0) {}
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
    :   index(-1ul), scanNumber(0), massAnalyzerType(CVID_Unknown), scanEvent(0), 
        msLevel(0), retentionTime(0), mzLow(0), mzHigh(0), basePeakMZ(0), 
        basePeakIntensity(0), totalIonCurrent(0)
    {}

    void update(const Spectrum& spectrum);
    void clearBinaryData();
};


} // namespace analysis 
} // namespace pwiz


#endif // _SPECTRUMINFO_HPP_ 

