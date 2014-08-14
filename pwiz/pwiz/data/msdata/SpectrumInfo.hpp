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


#ifndef _SPECTRUMINFO_HPP_ 
#define _SPECTRUMINFO_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "MSData.hpp"


namespace pwiz {
namespace msdata {


/// simple structure for holding Spectrum info 
struct PWIZ_API_DECL SpectrumInfo
{
    /// structure for Precursor info 
    struct PWIZ_API_DECL PrecursorInfo
    {
        size_t index;
        double mz;
        double intensity;
        double charge;

        PrecursorInfo() : index((size_t)-1), mz(0), intensity(0), charge(0) {}
    };

    size_t index;
    std::string id;
    int scanNumber;
    CVID massAnalyzerType;
    int scanEvent;
    int msLevel;
    bool isZoomScan;
    double retentionTime; // seconds
    std::string filterString;
    double mzLow;
    double mzHigh;
    double basePeakMZ;
    double basePeakIntensity;
    double totalIonCurrent;
    double thermoMonoisotopicMZ;
    double ionInjectionTime;
    std::vector<PrecursorInfo> precursors;
    size_t dataSize;
    std::vector<MZIntensityPair> data;

    SpectrumInfo();
    SpectrumInfo(const Spectrum& spectrum);

    void update(const Spectrum& spectrum, bool getBinaryData = false);
    void clearBinaryData();

    // some helper functions
    std::string massAnalyzerTypeAbbreviation() const;
    double mzFromFilterString() const;
};


} // namespace msdata 
} // namespace pwiz


#endif // _SPECTRUMINFO_HPP_ 

