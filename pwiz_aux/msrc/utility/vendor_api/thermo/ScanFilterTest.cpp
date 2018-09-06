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
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
//#include <boost/chrono.hpp>

using namespace pwiz::util;
using namespace pwiz::vendor_api::Thermo;


void testFilter(const ScanInfo& scanFilter,
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
                TriBool spsOn,
                double compensationVoltage,
                MassAnalyzerType massAnalyzerType,
                PolarityType polarityType,
                DataPointType dataPointType,
                IonizationType ionizationType,
                ActivationType activationType,
                ScanType scanType,
                bool hasMultiplePrecursors,
                int msLevel,
                const vector<double>& precursorMZs,
                const vector<double>& precursorEnergies,
                ActivationType saTypes,
                double saEnergies,
                const vector<double>& scanRangeMin,
                const vector<double>& scanRangeMax,
                bool constantNeutralLoss, // true for "CNL"
                double analyzer_scan_offset)
{
    //unit_assert_operator_equal(scanSegment, scanFilter.segment_);
    //unit_assert_operator_equal(scanEvent, scanFilter.event_);

    unit_assert_operator_equal(accurateMassType, scanFilter.accurateMassType());
    unit_assert_operator_equal(coronaOn, scanFilter.isCorona() ? TriBool_True : TriBool_Unknown);
    unit_assert_operator_equal(detectorSet, scanFilter.isDetectorSet() ? TriBool_True : TriBool_Unknown);
    unit_assert_operator_equal(photoIonizationOn, scanFilter.isPhotoIonization() ? TriBool_True : TriBool_Unknown);
    unit_assert_operator_equal(sourceCIDOn, scanFilter.isSourceCID() ? TriBool_True : TriBool_Unknown);
    unit_assert_operator_equal(turboScanOn, scanFilter.isTurboScan() ? TriBool_True : TriBool_Unknown);
    unit_assert_operator_equal(supplementalCIDOn, scanFilter.supplementalActivationType() != ActivationType_Unknown ? TriBool_True : TriBool_Unknown);
    unit_assert_operator_equal(widebandOn, scanFilter.isWideband() ? TriBool_True : TriBool_Unknown);
    unit_assert_operator_equal(enhancedOn, scanFilter.isEnhanced() ? TriBool_True : TriBool_Unknown);
    unit_assert_operator_equal(dependentActive, scanFilter.isDependent() ? TriBool_True : TriBool_Unknown);
    unit_assert_operator_equal(lockMassOn, scanFilter.hasLockMass() ? TriBool_True : TriBool_Unknown);

    unit_assert_operator_equal(massAnalyzerType, scanFilter.massAnalyzerType());
    unit_assert_operator_equal(polarityType, scanFilter.polarityType());
    unit_assert_operator_equal(dataPointType, scanFilter.isCentroidScan() ? DataPointType_Centroid : DataPointType_Profile);
    unit_assert_operator_equal(ionizationType, scanFilter.ionizationType());
    unit_assert_operator_equal(activationType, scanFilter.activationType());
    unit_assert_operator_equal(scanType, scanFilter.scanType());
    unit_assert_operator_equal(hasMultiplePrecursors, scanFilter.hasMultiplePrecursors());
    unit_assert_operator_equal(faimsOn, scanFilter.FAIMSOn() ? TriBool_True : TriBool_Unknown);
    unit_assert_operator_equal(spsOn, scanFilter.isSPS() ? TriBool_True : TriBool_Unknown);
    unit_assert_operator_equal(compensationVoltage, scanFilter.compensationVoltage());

    unit_assert_operator_equal(msLevel, scanFilter.msLevel());
    unit_assert_operator_equal(precursorEnergies.size(), precursorMZs.size());
    unit_assert_operator_equal((int) precursorEnergies.size(), scanFilter.precursorCount());

    unit_assert_operator_equal(saTypes, scanFilter.supplementalActivationType());
    unit_assert_operator_equal(saEnergies, scanFilter.supplementalActivationEnergy());

    unit_assert_operator_equal(constantNeutralLoss, scanFilter.isConstantNeutralLoss()); // true for "CNL"
    unit_assert_operator_equal(analyzer_scan_offset, scanFilter.analyzerScanOffset());

    if (msLevel > 1 && !scanFilter.isConstantNeutralLoss())
    {
        if (!scanFilter.hasMultiplePrecursors())
            unit_assert_operator_equal((size_t) scanFilter.msLevel() - 1, precursorMZs.size());
        for (int i=0; i < scanFilter.msLevel()-1; ++i)
        {
            unit_assert_operator_equal(precursorMZs[i], scanFilter.precursorMZ(i));
            unit_assert_operator_equal(precursorEnergies[i], scanFilter.precursorActivationEnergy(i));
        }
    }

    unit_assert_operator_equal(scanRangeMin.size(), scanFilter.scanRangeCount());
    if (scanType == ScanType_Full) // TODO: which scan types can have more than one range?
        unit_assert_operator_equal(1, scanFilter.scanRangeCount());

    for (size_t i=0; i < scanFilter.scanRangeCount(); ++i)
    {
        unit_assert_operator_equal(scanRangeMin[i], scanFilter.scanRange(i).first);
        unit_assert_operator_equal(scanRangeMax[i], scanFilter.scanRange(i).second);
    }
}


struct TestScanFilter
{
    const char* filter;

    // space-delimited doubles
	const char* precursorMZsArray; // one entry per ms level for level >= 2
	const char* precursorEnergiesArray; // relative units; one entry per ms level for level >= 2
    const char* saTypesArray;
    const char* saEnergiesArray;
	const char* scanRangeMinArray;
	const char* scanRangeMaxArray;
    double compensationVoltage;
    
	int msLevel;
    int scanSegment;
    int scanEvent;

    MassAnalyzerType massAnalyzerType;
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
    TriBool ultraOn;
    TriBool spsOn;

    bool constantNeutralLoss; // true for "CNL"
    double analyzer_scan_offset; // found with CNL
};

const TestScanFilter testScanFilters[] =
{
    {"+ p ESI Full cnl 256.200 [700.070-1500.000]",
    "", "", "", "", "700.07","1500" , 0, 2, -1, -1,
     MassAnalyzerType_Unknown, PolarityType_Positive, DataPointType_Profile,
     IonizationType_ESI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     true, 256.2 },

    {"ITMS + c NSI Full ms [400.00-2000.00]",
     "", "", "", "", "400", "2000", 0, 1, -1, -1,
     MassAnalyzerType_Linear_Ion_Trap, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"ITMS + c NSI d Full ms2 400.30@cid30.00 [80.00-1330.00]",
     "400.30", "30", "", "", "80", "1330", 0, 2, -1, -1,
     MassAnalyzerType_Linear_Ion_Trap, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_CID,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"ITMS + c NSI d Full ms3 400.30@cid30.00 329.73@cid30.00 [100.00-1615.00]",
     "400.30 329.73", "30 30", "", "", "100", "1615", 0, 3, -1, -1,
     MassAnalyzerType_Linear_Ion_Trap, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_CID,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"FTMS + p NSI Full ms [400.00-1800.00]",
     "", "", "", "", "400", "1800", 0, 1, -1, -1,
     MassAnalyzerType_FTICR, PolarityType_Positive, DataPointType_Profile,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"+ c ESI Full ms [400.00-1600.00]",
     "", "", "", "", "400", "1600", 0, 1, -1, -1,
     MassAnalyzerType_Unknown, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_ESI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"- c d Full ms2 400.29@cid35.00 [100.00-1215.00]",
     "400.29", "35", "", "", "100", "1215", 0, 2, -1, -1,
     MassAnalyzerType_Unknown, PolarityType_Negative, DataPointType_Centroid,
     IonizationType_Unknown, AccurateMass_Unknown, ScanType_Full, false, ActivationType_CID,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"- c d Full ms2 300.26@etd60.00 [50.00-915.00]",
     "300.26", "60", "", "", "50", "915", 0, 2, -1, -1,
     MassAnalyzerType_Unknown, PolarityType_Negative, DataPointType_Centroid,
     IonizationType_Unknown, AccurateMass_Unknown, ScanType_Full, false, ActivationType_ETD,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"- c NSI Q1MS [400.000-900.000]",
     "", "", "", "", "400", "900", 0, 1, -1, -1,
     MassAnalyzerType_Unknown, PolarityType_Negative, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"- c NSI Q3MS [400.000-900.000]",
     "", "", "", "", "400", "900", 0, 1, -1, -1,
     MassAnalyzerType_Unknown, PolarityType_Negative, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"- c NSI SRM ms2 448.711@cid19.00 [375.175-375.180, 537.265-537.270, 652.291-652.297, 749.344-749.350]",
     "448.711", "19", "", "", "375.175 537.265 652.291 749.344", "375.18 537.27 652.297 749.35", 0, 2, -1, -1,
     MassAnalyzerType_Unknown, PolarityType_Negative, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_SRM, false, ActivationType_CID,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"- c SRM ms2 448.711@0 [375.175-375.180, 537.265-537.270, 652.291-652.297, 749.344-749.350]",
     "448.711", "0", "", "", "375.175 537.265 652.291 749.344", "375.18 537.27 652.297 749.35", 0, 2, -1, -1,
     MassAnalyzerType_Unknown, PolarityType_Negative, DataPointType_Centroid,
     IonizationType_Unknown, AccurateMass_Unknown, ScanType_SRM, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"+ c Full pr 191.00@-35.00 [300.00-900.00]",
     "191", "-35", "", "", "300", "900", 0, -1, -1, -1,
     MassAnalyzerType_Unknown, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_Unknown, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"ITMS + c NSI SIM ms [428.00-438.00, 646.50-651.50, 669.50-684.50]",
     "", "", "", "", "428 646.5 669.5", "438 651.5 684.5", 0, 1, -1, -1,
     MassAnalyzerType_Linear_Ion_Trap, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_SIM, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"ITMS + p NSI E Full ms [400.00-1800.00]",
     "", "", "", "", "400", "1800", 0, 1, -1, -1,
     MassAnalyzerType_Linear_Ion_Trap, PolarityType_Positive, DataPointType_Profile,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"ITMS + c NSI d w sa Full ms2 375.01@etd66.67 [50.00-1890.00]",
     "375.01", "66.67", "cid", "0", "50", "1890", 0, 2, -1, -1,
     MassAnalyzerType_Linear_Ion_Trap, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, static_cast<ActivationType>(ActivationType_ETD | ActivationType_CID),
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_True, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"ITMS + c NSI d w sa Full ms2 375.01@etd66.67@hcd15.00 [50.00-1890.00]",
     "375.01", "66.67", "hcd", "15", "50", "1890", 0, 2, -1, -1,
     MassAnalyzerType_Linear_Ion_Trap, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, static_cast<ActivationType>(ActivationType_ETD | ActivationType_HCD),
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_True, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"ITMS + c NSI d w sa Full ms2 375.01@etd66.67 375.01@hcd15.00 [50.00-1890.00]",
     "375.01", "66.67", "hcd", "15", "50", "1890", 0, 2, -1, -1,
     MassAnalyzerType_Linear_Ion_Trap, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, static_cast<ActivationType>(ActivationType_ETD | ActivationType_HCD),
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_True, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"FTMS {0,0}  + p ESI Full ms [100.00-800.00]",
     "", "", "", "", "100", "800", 0, 1, 0, 0,
     MassAnalyzerType_FTICR, PolarityType_Positive, DataPointType_Profile,
     IonizationType_ESI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"{2,3}  - p ESI Full lock ms [100.00-800.00]",
     "", "", "", "", "100", "800", 0, 1, 2, 3,
     MassAnalyzerType_Unknown, PolarityType_Negative, DataPointType_Profile,
     IonizationType_ESI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"FTMS + P ESI cv=-12.34 r FULL MS [300.00-2000.00]",
     "", "", "", "", "300", "2000", -12.34, 1, -1, -1,
     MassAnalyzerType_FTICR, PolarityType_Positive, DataPointType_Profile,
     IonizationType_ESI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_True,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"FTMS + p ESI d Full msx ms2 262.64@hcd35.00 1521.97@hcd35.00 [50.00-1500.00]",
     "262.64 1521.97", "35 35", "", "", "50", "1500", 0, 2, -1, -1,
     MassAnalyzerType_FTICR, PolarityType_Positive, DataPointType_Profile,
     IonizationType_ESI, AccurateMass_Unknown, ScanType_Full, true, ActivationType_HCD,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    /*{"FTMS + p ESI k d Full ms3 262.64@hcd35.00 1521.97@hcd35.00 [50.00-1500.00]",
     "262.64 1521.97", "35 35", "", "", "50", "1500", 0, 3, -1, -1,
     MassAnalyzerType_FTICR, PolarityType_Positive, DataPointType_Profile,
     IonizationType_ESI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_HCD,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_True,
     false, 0.0 },*/
     // 
    {"FTMS + p ESI sps d Full ms3 262.64@hcd35.00 1521.97@hcd35.00 [50.00-1500.00]",
     "262.64 1521.97", "35 35", "", "", "50", "1500", 0, 3, -1, -1,
     MassAnalyzerType_FTICR, PolarityType_Positive, DataPointType_Profile,
     IonizationType_ESI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_HCD,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_True,
     false, 0.0 },

    /*{"FTMS + c NSI k d Full ms3 925.31@cid30.00 528.79@hcd50.00 [110.00-1638.00]",
     "925.31 528.79", "30 50", "", "", "110", "1638", 0, 3, -1, -1,
     MassAnalyzerType_FTICR, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_CID,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_True,
     false, 0.0 },*/

    {"FTMS + p NSI sps d Full ms3 380.1937@cid35.00 409.4627@hcd65.00 [100.0000-500.0000]",
     "380.1937 409.4627", "35 65", "", "", "100", "500", 0, 3, -1, -1,
     MassAnalyzerType_FTICR, PolarityType_Positive, DataPointType_Profile,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_CID,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_True,
     false, 0.0 },
      
    {"ITMS + p NSI r d Full ms2 400.29@cid35.00 [400.00-1800.00]",
     "400.29", "35", "", "", "400", "1800", 0, 2, -1, -1,
     MassAnalyzerType_Linear_Ion_Trap, PolarityType_Positive, DataPointType_Profile,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_CID,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_True,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    { "ITMS + p ESI d u Z ms",
     "", "", "", "", "", "", 0, 1, -1, -1,
     MassAnalyzerType_Linear_Ion_Trap, PolarityType_Positive, DataPointType_Profile,
     IonizationType_ESI, AccurateMass_Unknown, ScanType_Zoom, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_True, TriBool_Unknown,
     false, 0.0 },
     
    {"FTMS + c NSI det=3.00 d Full ms2 350.18@cid60.00 [100.00-1065.00]",
     "350.18", "60", "", "", "100", "1065", 0, 2, -1, -1,
     MassAnalyzerType_FTICR, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_HCD,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_True, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"FTMS + c NSI d Full ms2 350.18@cid60.00 [100.00-1065.00]",
     "350.18", "60", "", "", "100", "1065", 0, 2, -1, -1,
     MassAnalyzerType_FTICR, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_CID,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"FTMS + c NSI d sa Full ms3 350.18@hcd45.00 123.4@etd119.81@hcd25.00 [100.00-1065.00]",
     "350.18 123.4", "45 119.81", "", "", "100", "1065", 0, 3, -1, -1,
     MassAnalyzerType_FTICR, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Full, false, ActivationType_HCD,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_True, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"+ c NSI SRM ms2 467.244 [491.281-491.283, 619.340-619.342, 720.388-720.390]",
     "467.244", "0", "", "", "491.281 619.340 720.388", "491.283 619.342 720.390", 0, 2, -1, -1,
     MassAnalyzerType_Unknown, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_SRM, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },

    {"+ c NSI Z ms3 467.244 350.18@hcd45.00 [491.281-491.283, 619.340-619.342, 720.388-720.390]",
     "467.244 350.18", "0 45.00", "", "", "491.281 619.340 720.388", "491.283 619.342 720.390", 0, 3, -1, -1,
     MassAnalyzerType_Unknown, PolarityType_Positive, DataPointType_Centroid,
     IonizationType_NSI, AccurateMass_Unknown, ScanType_Zoom, false, ActivationType_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown, TriBool_Unknown,
     false, 0.0 },
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

ActivationType parseActivationType(const string& word)
{
    if (bal::iequals(word, "CID"))
        return ActivationType_CID;
    else if (bal::iequals(word, "MPD"))
        return ActivationType_MPD;
    else if (bal::iequals(word, "ECD"))
        return ActivationType_ECD;
    else if (bal::iequals(word, "PQD"))
        return ActivationType_PQD;
    else if (bal::iequals(word, "ETD"))
        return ActivationType_ETD;
    else if (bal::iequals(word, "HCD"))
        return ActivationType_HCD;
    else if (bal::iequals(word, "PTR"))
        return ActivationType_PTR;
    else
        return ActivationType_Unknown;
}

vector<ActivationType> parseActivationArray(const string& activationArray)
{
    vector<ActivationType> activationVector;
    vector<string> tokens;
    bal::split(tokens, activationArray, bal::is_space());
    if (!tokens.empty() && !tokens[0].empty())
        for (size_t i = 0; i < tokens.size(); ++i)
            activationVector.push_back(parseActivationType(tokens[i]));
    return activationVector;
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        int failedTests = 0;

        //auto start = boost::chrono::process_cpu_clock::now();
        // unit test static strings
        //for(int j=0; j < 1000; ++j)
        for (size_t i=0; i < testScanFiltersSize; ++i)
        {
            const TestScanFilter& f = testScanFilters[i];

            try
            {
                vector<double> precursorMZs = parseDoubleArray(f.precursorMZsArray);
                vector<double> precursorEnergies = parseDoubleArray(f.precursorEnergiesArray);
                vector<double> saEnergies = parseDoubleArray(f.saEnergiesArray);
                vector<double> scanRangeMin = parseDoubleArray(f.scanRangeMinArray);
                vector<double> scanRangeMax = parseDoubleArray(f.scanRangeMaxArray);

                ScanInfoPtr scanInfo = RawFile::getScanInfoFromFilterString(f.filter);

                testFilter(*scanInfo,
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
                            f.spsOn,
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
                            parseActivationType(f.saTypesArray),
                            saEnergies.empty() ? 0 : saEnergies.front(),
                            scanRangeMin,
                            scanRangeMax,
                            f.constantNeutralLoss,
                            f.analyzer_scan_offset);
            }
            catch (exception& e)
            {
                cout << "Unit test on filter \"" << f.filter << "\" failed:\n" << e.what() << endl;
                ++failedTests;
            }
        }
        //auto stop = boost::chrono::process_cpu_clock::now();
        //cout << "Time elapsed: " << ((stop - start).count().user / 1e9) << endl;

        unit_assert_operator_equal(0, failedTests);
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}


