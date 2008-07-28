//
// RawFileTest.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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

#include "RawFile.h"
#include "ScanFilter.h"
#include "utility/misc/unit.hpp"
#include <iostream>
#include <vector>

using namespace std;
using namespace pwiz::util;
using namespace pwiz::raw;


void testFilter(const ScanFilter& scanFilter,
                TriBool accurateMassType,
                TriBool coronaOn,
                TriBool detectorSet,
                TriBool photoIonizationOn,
                TriBool sourceCIDOn,
                TriBool turboScanOn,
                TriBool widebandOn,
                TriBool dependentActive,
                ScanFilterMassAnalyzerType massAnalyzerType,
                PolarityType polarityType,
                DataPointType dataPointType,
                IonizationType ionizationType,
                ScanType scanType,
                int msLevel,
                const vector<double>& cidParentMass,
                const vector<double>& cidParentEnergy,
                const vector<double>& scanRangeMin,
                const vector<double>& scanRangeMax )
{
    unit_assert(scanFilter.accurateMassType_ == accurateMassType);
    unit_assert(scanFilter.coronaOn_ == coronaOn);
    unit_assert(scanFilter.detectorSet_ == detectorSet);
    unit_assert(scanFilter.photoIonizationOn_ == photoIonizationOn);
    unit_assert(scanFilter.sourceCIDOn_ == sourceCIDOn);
    unit_assert(scanFilter.turboScanOn_ == turboScanOn);
    unit_assert(scanFilter.widebandOn_ == widebandOn);
    unit_assert(scanFilter.dependentActive_ == dependentActive);

    unit_assert(scanFilter.massAnalyzerType_ == massAnalyzerType);
    unit_assert(scanFilter.polarityType_ == polarityType);
    unit_assert(scanFilter.dataPointType_ == dataPointType);
    unit_assert(scanFilter.ionizationType_ == ionizationType);
    unit_assert(scanFilter.scanType_ == scanType);

    unit_assert(scanFilter.msLevel_ == msLevel);
    unit_assert(scanFilter.cidParentMass_.size() == scanFilter.cidEnergy_.size() &&
                cidParentMass.size() == cidParentEnergy.size() &&
                scanFilter.cidEnergy_.size() == cidParentMass.size());
    unit_assert(cidParentMass.size() == (size_t) scanFilter.msLevel_-1);
    for (int i=0; i < scanFilter.msLevel_-1; ++i)
    {
        unit_assert(scanFilter.cidParentMass_[i] == cidParentMass[i] && scanFilter.cidEnergy_[i] == cidParentEnergy[i]);
    }

    unit_assert(scanFilter.scanRangeMin_.size() == scanFilter.scanRangeMax_.size() &&
                scanRangeMin.size() == scanRangeMax.size() &&
                scanFilter.scanRangeMin_.size() == scanRangeMin.size());
    if (scanType == ScanType_Full || scanType == ScanType_Zoom) // TODO: which scan types can have more than one range?
        unit_assert(scanRangeMin.size() == 1);

    for (size_t i=0; i < scanFilter.scanRangeMin_.size(); ++i)
    {
        unit_assert(scanFilter.scanRangeMin_[i] == scanRangeMin[i] &&
                    scanFilter.scanRangeMax_[i] == scanRangeMax[i]);
    }
}


const char* scanFilterStrings[] =
{
    "ITMS + c NSI Full ms [400.00-2000.00]",
    "ITMS + c NSI d Full ms2 400.30@cid30.00 [80.00-1330.00]",
    "ITMS + c NSI d Full ms3 400.30@cid30.00 329.73@cid30.00 [100.00-1615.00]",
    "FTMS + p NSI Full ms [400.00-1800.00]",
    "+ c ESI Full ms [400.00-1600.00]",
    "+ c d Full ms2 400.29@cid35.00 [100.00-1215.00]",
    "+ c NSI Q1MS [400.000-900.000]",
    "+ c NSI SRM ms2 448.711@cid19.00 [375.175-375.180, 537.265-537.270, 652.291-652.297, 749.344-749.350]"
};

int main(int argc, char* argv[])
{
    try
    {
        if (argc > 2)
            throw runtime_error("Usage: ScanFilterTest [Thermo RAW filename]");
        else if (argc == 1)
        {
            // unit test static strings
            size_t scanFilterIndex = 0;
            ScanFilter scanFilter;
            vector<double> cidParentMass, cidParentEnergy, scanRangeMin, scanRangeMax;

            scanFilter.parse(scanFilterStrings[scanFilterIndex++]);
            cidParentMass.clear(); cidParentEnergy.clear(); scanRangeMin.clear(); scanRangeMax.clear();
            scanRangeMin.push_back(400.00); scanRangeMax.push_back(2000.00);
            testFilter (scanFilter,
                        TriBool_Unknown, // accurate mass?
                        TriBool_Unknown, // corona?
                        TriBool_Unknown, // detector?
                        TriBool_Unknown, // photo ionization?
                        TriBool_Unknown, // source CID?
                        TriBool_Unknown, // turbo scan?
                        TriBool_Unknown, // wideband?
                        TriBool_Unknown, // data-dependent?
                        ScanFilterMassAnalyzerType_ITMS,
                        PolarityType_Positive,
                        DataPointType_Centroid,
                        IonizationType_NSI,
                        ScanType_Full,
                        1, cidParentMass, cidParentEnergy, scanRangeMin, scanRangeMax);

            scanFilter.parse(scanFilterStrings[scanFilterIndex++]);
            cidParentMass.clear(); cidParentEnergy.clear(); scanRangeMin.clear(); scanRangeMax.clear();
            cidParentMass.push_back(400.30); cidParentEnergy.push_back(30.00);
            scanRangeMin.push_back(80.00); scanRangeMax.push_back(1330.00);
            testFilter (scanFilter,
                        TriBool_Unknown, // accurate mass?
                        TriBool_Unknown, // corona?
                        TriBool_Unknown, // detector?
                        TriBool_Unknown, // photo ionization?
                        TriBool_Unknown, // source CID?
                        TriBool_Unknown, // turbo scan?
                        TriBool_Unknown, // wideband?
                        TriBool_True, // data-dependent?
                        ScanFilterMassAnalyzerType_ITMS,
                        PolarityType_Positive,
                        DataPointType_Centroid,
                        IonizationType_NSI,
                        ScanType_Full,
                        2, cidParentMass, cidParentEnergy, scanRangeMin, scanRangeMax);

            scanFilter.parse(scanFilterStrings[scanFilterIndex++]);
            cidParentMass.clear(); cidParentEnergy.clear(); scanRangeMin.clear(); scanRangeMax.clear();
            cidParentMass.push_back(400.30); cidParentEnergy.push_back(30.00);
            cidParentMass.push_back(329.73); cidParentEnergy.push_back(30.00);
            scanRangeMin.push_back(100.00); scanRangeMax.push_back(1615.00);
            testFilter (scanFilter,
                        TriBool_Unknown, // accurate mass?
                        TriBool_Unknown, // corona?
                        TriBool_Unknown, // detector?
                        TriBool_Unknown, // photo ionization?
                        TriBool_Unknown, // source CID?
                        TriBool_Unknown, // turbo scan?
                        TriBool_Unknown, // wideband?
                        TriBool_True, // data-dependent?
                        ScanFilterMassAnalyzerType_ITMS,
                        PolarityType_Positive,
                        DataPointType_Centroid,
                        IonizationType_NSI,
                        ScanType_Full,
                        3, cidParentMass, cidParentEnergy, scanRangeMin, scanRangeMax);

            scanFilter.parse(scanFilterStrings[scanFilterIndex++]);
            cidParentMass.clear(); cidParentEnergy.clear(); scanRangeMin.clear(); scanRangeMax.clear();
            scanRangeMin.push_back(400.00); scanRangeMax.push_back(1800.00);
            testFilter (scanFilter,
                        TriBool_Unknown, // accurate mass?
                        TriBool_Unknown, // corona?
                        TriBool_Unknown, // detector?
                        TriBool_Unknown, // photo ionization?
                        TriBool_Unknown, // source CID?
                        TriBool_Unknown, // turbo scan?
                        TriBool_Unknown, // wideband?
                        TriBool_Unknown, // data-dependent?
                        ScanFilterMassAnalyzerType_FTMS,
                        PolarityType_Positive,
                        DataPointType_Profile,
                        IonizationType_NSI,
                        ScanType_Full,
                        1, cidParentMass, cidParentEnergy, scanRangeMin, scanRangeMax);

            scanFilter.parse(scanFilterStrings[scanFilterIndex++]);
            cidParentMass.clear(); cidParentEnergy.clear(); scanRangeMin.clear(); scanRangeMax.clear();
            scanRangeMin.push_back(400.00); scanRangeMax.push_back(1600.00);
            testFilter (scanFilter,
                        TriBool_Unknown, // accurate mass?
                        TriBool_Unknown, // corona?
                        TriBool_Unknown, // detector?
                        TriBool_Unknown, // photo ionization?
                        TriBool_Unknown, // source CID?
                        TriBool_Unknown, // turbo scan?
                        TriBool_Unknown, // wideband?
                        TriBool_Unknown, // data-dependent?
                        ScanFilterMassAnalyzerType_Unknown,
                        PolarityType_Positive,
                        DataPointType_Centroid,
                        IonizationType_ESI,
                        ScanType_Full,
                        1, cidParentMass, cidParentEnergy, scanRangeMin, scanRangeMax);

            scanFilter.parse(scanFilterStrings[scanFilterIndex++]);
            cidParentMass.clear(); cidParentEnergy.clear(); scanRangeMin.clear(); scanRangeMax.clear();
            cidParentMass.push_back(400.29); cidParentEnergy.push_back(35.00);
            scanRangeMin.push_back(100.00); scanRangeMax.push_back(1215.00);
            testFilter (scanFilter,
                        TriBool_Unknown, // accurate mass?
                        TriBool_Unknown, // corona?
                        TriBool_Unknown, // detector?
                        TriBool_Unknown, // photo ionization?
                        TriBool_Unknown, // source CID?
                        TriBool_Unknown, // turbo scan?
                        TriBool_Unknown, // wideband?
                        TriBool_True, // data-dependent?
                        ScanFilterMassAnalyzerType_Unknown,
                        PolarityType_Positive,
                        DataPointType_Centroid,
                        IonizationType_Unknown,
                        ScanType_Full,
                        2, cidParentMass, cidParentEnergy, scanRangeMin, scanRangeMax);

            scanFilter.parse(scanFilterStrings[scanFilterIndex++]);
            cidParentMass.clear(); cidParentEnergy.clear(); scanRangeMin.clear(); scanRangeMax.clear();
            scanRangeMin.push_back(400.00); scanRangeMax.push_back(900.00);
            testFilter (scanFilter,
                        TriBool_Unknown, // accurate mass?
                        TriBool_Unknown, // corona?
                        TriBool_Unknown, // detector?
                        TriBool_Unknown, // photo ionization?
                        TriBool_Unknown, // source CID?
                        TriBool_Unknown, // turbo scan?
                        TriBool_Unknown, // wideband?
                        TriBool_Unknown, // data-dependent?
                        ScanFilterMassAnalyzerType_Unknown,
                        PolarityType_Positive,
                        DataPointType_Centroid,
                        IonizationType_NSI,
                        ScanType_Full,
                        1, cidParentMass, cidParentEnergy, scanRangeMin, scanRangeMax);

            scanFilter.parse(scanFilterStrings[scanFilterIndex++]);
            cidParentMass.clear(); cidParentEnergy.clear(); scanRangeMin.clear(); scanRangeMax.clear();
            cidParentMass.push_back(448.711); cidParentEnergy.push_back(19.00);
            scanRangeMin.push_back(375.175); scanRangeMax.push_back(375.180);
            scanRangeMin.push_back(537.265); scanRangeMax.push_back(537.270);
            scanRangeMin.push_back(652.291); scanRangeMax.push_back(652.297);
            scanRangeMin.push_back(749.344); scanRangeMax.push_back(749.350);
            testFilter (scanFilter,
                        TriBool_Unknown, // accurate mass?
                        TriBool_Unknown, // corona?
                        TriBool_Unknown, // detector?
                        TriBool_Unknown, // photo ionization?
                        TriBool_Unknown, // source CID?
                        TriBool_Unknown, // turbo scan?
                        TriBool_Unknown, // wideband?
                        TriBool_Unknown, // data-dependent?
                        ScanFilterMassAnalyzerType_Unknown,
                        PolarityType_Positive,
                        DataPointType_Centroid,
                        IonizationType_NSI,
                        ScanType_SRM,
                        2, cidParentMass, cidParentEnergy, scanRangeMin, scanRangeMax);
        }
        return 0;
    }
    catch (exception& e)
    {
        cout << "Caught exception: " << e.what() << endl;
    }
    catch (...)
    {
        cout << "Caught unknown exception.\n";
    }

    return 1;
}


