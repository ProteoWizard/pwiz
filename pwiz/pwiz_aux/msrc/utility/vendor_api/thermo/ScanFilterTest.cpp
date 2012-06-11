//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::vendor_api::Thermo;


void testFilter(const ScanFilter& scanFilter,
                int scanSegment,
                int scanEvent,
                AccurateMassType accurateMassType,
                TriBool coronaOn,
                TriBool detectorSet,
                TriBool photoIonizationOn,
                TriBool sourceCIDOn,
                TriBool turboScanOn,
                TriBool supplementalCIDOn,
                TriBool widebandOn,
                TriBool enhancedOn,
                TriBool dependentActive,
                TriBool lockMassOn,
                TriBool faimsOn,
                double compensationVoltage,
                ScanFilterMassAnalyzerType massAnalyzerType,
                PolarityType polarityType,
                DataPointType dataPointType,
                IonizationType ionizationType,
                ActivationType activationType,
                ScanType scanType,
                bool hasMultiplePrecursors,
                int msLevel,
                const vector<double>& precursorMZs,
                const vector<double>& precursorEnergies,
                const vector<double>& scanRangeMin,
                const vector<double>& scanRangeMax )
{
    unit_assert(scanFilter.segment_ == scanSegment);
    unit_assert(scanFilter.event_ == scanEvent);

    unit_assert(scanFilter.accurateMassType_ == accurateMassType);
    unit_assert(scanFilter.coronaOn_ == coronaOn);
    unit_assert(scanFilter.detectorSet_ == detectorSet);
    unit_assert(scanFilter.photoIonizationOn_ == photoIonizationOn);
    unit_assert(scanFilter.sourceCIDOn_ == sourceCIDOn);
    unit_assert(scanFilter.turboScanOn_ == turboScanOn);
    unit_assert(scanFilter.supplementalCIDOn_ == supplementalCIDOn);
    unit_assert(scanFilter.widebandOn_ == widebandOn);
    unit_assert(scanFilter.enhancedOn_ == enhancedOn);
    unit_assert(scanFilter.dependentActive_ == dependentActive);
    unit_assert(scanFilter.lockMassOn_ == lockMassOn);

    unit_assert(scanFilter.massAnalyzerType_ == massAnalyzerType);
    unit_assert(scanFilter.polarityType_ == polarityType);
    unit_assert(scanFilter.dataPointType_ == dataPointType);
    unit_assert(scanFilter.ionizationType_ == ionizationType);
    unit_assert(scanFilter.activationType_ == activationType);
    unit_assert(scanFilter.scanType_ == scanType);
    unit_assert(scanFilter.multiplePrecursorMode_ == hasMultiplePrecursors);
    unit_assert(scanFilter.faimsOn_ == faimsOn);
    unit_assert(scanFilter.compensationVoltage_ == compensationVoltage);

    unit_assert(scanFilter.msLevel_ == msLevel);
    unit_assert(scanFilter.precursorMZs_.size() == scanFilter.precursorEnergies_.size());
    unit_assert(precursorMZs.size() == precursorEnergies.size());
    unit_assert(scanFilter.precursorEnergies_.size() == precursorMZs.size());

    if (msLevel > 1)
    {
        if (!scanFilter.multiplePrecursorMode_)
            unit_assert(precursorMZs.size() == (size_t) scanFilter.msLevel_-1);
        for (int i=0; i < scanFilter.msLevel_-1; ++i)
        {
            unit_assert(scanFilter.precursorMZs_[i] == precursorMZs[i]);
            unit_assert(scanFilter.precursorEnergies_[i] == precursorEnergies[i]);
        }
    }

    unit_assert(scanFilter.scanRangeMin_.size() == scanFilter.scanRangeMax_.size() &&
                scanRangeMin.size() == scanRangeMax.size() &&
                scanFilter.scanRangeMin_.size() == scanRangeMin.size());
    if (scanType == ScanType_Full || scanType == ScanType_Zoom) // TODO: which scan types can have more than one range?
        unit_assert(scanRangeMin.size() == 1);

    for (size_t i=0; i < scanFilter.scanRangeMin_.size(); ++i)
    {
        unit_assert(scanFilter.scanRangeMin_[i] == scanRangeMin[i]);
        unit_assert(scanFilter.scanRangeMax_[i] == scanRangeMax[i]);
    }
}


struct TestScanFilter
{
    const char* filter;

    // space-delimited doubles
	const char* precursorMZsArray; // one entry per ms level for level >= 2
	const char* precursorEnergiesArray; // relative units; one entry per ms level for level >= 2
	const char* scanRangeMinArray;
	const char* scanRangeMaxArray;
    double compensationVoltage;
    
	int msLevel;
    int scanSegment;
    int scanEvent;

    ScanFilterMassAnalyzerType massAnalyzerType;
	PolarityType polarityType;
	DataPointType dataPointType;
	IonizationType ionizationType;
	AccurateMassType accurateMassType;
	ScanType scanType;
    bool hasMultiplePrecursors;
	ActivationType activationType;

    TriBool coronaOn;
	TriBool photoIonizationOn;
	TriBool sourceCIDOn;
	TriBool detectorSet;
	TriBool turboScanOn;
    TriBool enhancedOn;
	TriBool dependentActive;
    TriBool supplementalCIDOn;
	TriBool widebandOn;
    TriBool lockMassOn;
    TriBool faimsOn;
};

const TestScanFilter testScanFilters[] =
{
    {"ITMS + c NSI Full ms [400.00-2000.00]",
     "", "", "400", "2000", 0, 1, -1, -1,
     ScanFilterMassAnalyzerType_ITMS, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown},

    {"ITMS + c NSI d Full ms2 400.30@cid30.00 [80.00-1330.00]",
     "400.30", "30", "80", "1330", 0, 2, -1, -1,
     ScanFilterMassAnalyzerType_ITMS, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_CID,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown},

    {"ITMS + c NSI d Full ms3 400.30@cid30.00 329.73@cid30.00 [100.00-1615.00]",
     "400.30 329.73", "30 30", "100", "1615", 0, 3, -1, -1,
     ScanFilterMassAnalyzerType_ITMS, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_CID,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown},

    {"FTMS + p NSI Full ms [400.00-1800.00]",
     "", "", "400", "1800", 0, 1, -1, -1,
     ScanFilterMassAnalyzerType_FTMS, PolarityType_Positive, DataPointType_Profile,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown},

    {"+ c ESI Full ms [400.00-1600.00]",
     "", "", "400", "1600", 0, 1, -1, -1,
     ScanFilterMassAnalyzerType_Unknown, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_ESI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown},

    {"- c d Full ms2 400.29@cid35.00 [100.00-1215.00]",
     "400.29", "35", "100", "1215", 0, 2, -1, -1,
     ScanFilterMassAnalyzerType_Unknown, PolarityType_Negative, DataPointType_Centroid,
     IonizationType_Unknown, AccurateMass_Unknown, ScanType_Full, false, ActivationType_CID,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown},

    {"- c d Full ms2 300.26@etd60.00 [50.00-915.00]",
     "300.26", "60", "50", "915", 0, 2, -1, -1,
     ScanFilterMassAnalyzerType_Unknown, PolarityType_Negative, DataPointType_Centroid,
     IonizationType_Unknown, AccurateMass_Unknown, ScanType_Full, false, ActivationType_ETD,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown},

    {"- c NSI Q1MS [400.000-900.000]",
     "", "", "400", "900", 0, 1, -1, -1,
     ScanFilterMassAnalyzerType_Unknown, PolarityType_Negative, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown},

    {"- c NSI Q3MS [400.000-900.000]",
     "", "", "400", "900", 0, 1, -1, -1,
     ScanFilterMassAnalyzerType_Unknown, PolarityType_Negative, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown},

    {"- c NSI SRM ms2 448.711@cid19.00 [375.175-375.180, 537.265-537.270, 652.291-652.297, 749.344-749.350]",
     "448.711", "19", "375.175 537.265 652.291 749.344", "375.18 537.27 652.297 749.35", 0, 2, -1, -1,
     ScanFilterMassAnalyzerType_Unknown, PolarityType_Negative, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_SRM, false, ActivationType_CID,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown},

    {"- c SRM ms2 448.711@0 [375.175-375.180, 537.265-537.270, 652.291-652.297, 749.344-749.350]",
     "448.711", "0", "375.175 537.265 652.291 749.344", "375.18 537.27 652.297 749.35", 0, 2, -1, -1,
     ScanFilterMassAnalyzerType_Unknown, PolarityType_Negative, DataPointType_Centroid,
     IonizationType_Unknown, AccurateMass_Unknown, ScanType_SRM, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown},

    {"+ c Full pr 191.00@-35.00 [300.00-900.00]",
     "191", "-35", "300", "900", 0, -1, -1, -1,
     ScanFilterMassAnalyzerType_Unknown, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_Unknown, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown},

    {"ITMS + c NSI SIM ms [428.00-438.00, 646.50-651.50, 669.50-684.50]",
     "", "", "428 646.5 669.5", "438 651.5 684.5", 0, 1, -1, -1,
     ScanFilterMassAnalyzerType_ITMS, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_SIM, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown},

    {"ITMS + p NSI E Full ms [400.00-1800.00]",
     "", "", "400", "1800", 0, 1, -1, -1,
     ScanFilterMassAnalyzerType_ITMS, PolarityType_Positive, DataPointType_Profile,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown},

    {"ITMS + c NSI d w sa Full ms2 375.01@etd66.67 [50.00-1890.00]",
     "375.01", "66.67", "50", "1890", 0, 2, -1, -1,
     ScanFilterMassAnalyzerType_ITMS, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, static_cast<ActivationType>(ActivationType_ETD | ActivationType_CID),
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_True, TriBool_True, TriBool_Unknown, TriBool_Unknown},

    {"FTMS {0,0}  + p ESI Full ms [100.00-800.00]",
     "", "", "100", "800", 0, 1, 0, 0,
     ScanFilterMassAnalyzerType_FTMS, PolarityType_Positive, DataPointType_Profile,
     IonizationType_ESI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown},

    {"{2,3}  - p ESI Full lock ms [100.00-800.00]",
     "", "", "100", "800", 0, 1, 2, 3,
     ScanFilterMassAnalyzerType_Unknown, PolarityType_Negative, DataPointType_Profile,
     IonizationType_ESI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_True, TriBool_Unknown},

    {"FTMS + P ESI cv=-12.34 r FULL MS [300.00-2000.00]",
     "", "", "300", "2000", -12.34, 1, -1, -1,
     ScanFilterMassAnalyzerType_FTMS, PolarityType_Positive, DataPointType_Profile,
     IonizationType_ESI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_True,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_True},

    {"FTMS + p ESI d Full msx ms2 262.64@hcd35.00 1521.97@hcd35.00 [50.00-1500.00]",
     "262.64 1521.97", "35 35", "50", "1500", 0, 2, -1, -1,
     ScanFilterMassAnalyzerType_FTMS, PolarityType_Positive, DataPointType_Profile,
     IonizationType_ESI, AccurateMass_Unknown, ScanType_Full, true, ActivationType_HCD,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown},
      
    {"ITMS + p NSI r d Full ms2 400.29@cid35.00 [400.00-1800.00]",
     "400.29", "35", "400", "1800", 0, 2, -1, -1,
     ScanFilterMassAnalyzerType_ITMS, PolarityType_Positive, DataPointType_Profile,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_CID,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_True,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown},

    {"FTMS + c NSI det=3.00 d Full ms2 350.18@cid60.00 [100.00-1065.00]",
     "350.18", "60", "100", "1065", 0, 2, -1, -1,
     ScanFilterMassAnalyzerType_FTMS, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_HCD,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_True, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown},     
};

const size_t testScanFiltersSize = sizeof(testScanFilters) / sizeof(TestScanFilter);

vector<double> parseDoubleArray(const string& doubleArray)
{
    vector<double> doubleVector;
    vector<string> tokens;
    bal::split(tokens, doubleArray, bal::is_space());
    if (!tokens.empty() && !tokens[0].empty())
        for (size_t i=0; i < tokens.size(); ++i)
            doubleVector.push_back(lexical_cast<double>(tokens[i]));
    return doubleVector;
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc > 2)
            throw runtime_error("Usage: ScanFilterTest [Thermo RAW filename]");
        else if (argc == 1)
        {
            // unit test static strings
            for (size_t i=0; i < testScanFiltersSize; ++i)
            {
                const TestScanFilter& f = testScanFilters[i];
                ScanFilter scanFilter;

                try
                {
                    vector<double> precursorMZs = parseDoubleArray(f.precursorMZsArray);
                    vector<double> precursorEnergies = parseDoubleArray(f.precursorEnergiesArray);
                    vector<double> scanRangeMin = parseDoubleArray(f.scanRangeMinArray);
                    vector<double> scanRangeMax = parseDoubleArray(f.scanRangeMaxArray);

                    scanFilter = ScanFilter();
                    scanFilter.parse(f.filter);

                    testFilter(scanFilter,
                               f.scanSegment,
                               f.scanEvent,
                               f.accurateMassType,
                               f.coronaOn,
                               f.detectorSet,
                               f.photoIonizationOn,
                               f.sourceCIDOn,
                               f.turboScanOn,
                               f.supplementalCIDOn,
                               f.widebandOn,
                               f.enhancedOn,
                               f.dependentActive,
                               f.lockMassOn,
                               f.faimsOn,
                               f.compensationVoltage,
                               f.massAnalyzerType,
                               f.polarityType,
                               f.dataPointType,
                               f.ionizationType,
                               f.activationType,
                               f.scanType,
                               f.hasMultiplePrecursors,
                               f.msLevel,
                               precursorMZs,
                               precursorEnergies,
                               scanRangeMin,
                               scanRangeMax);
                }
                catch (exception& e)
                {
                    cout << "Unit test on filter \"" << f.filter << "\" failed:\n" << e.what() << endl;
                    scanFilter.print();
                }
            }
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


