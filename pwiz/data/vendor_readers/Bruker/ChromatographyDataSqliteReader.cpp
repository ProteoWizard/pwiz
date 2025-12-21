//
// $Id$
//
//
// Original author: Brian Pratt <bspratt .at. proteinms.net>
//
// Copyright 2025 University of Washington - Seattle, WA
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


//
// Implementation of helper functions for  LC traces in Bruker chromatography-data.sqlite files
// See accompanying file "pwiz\data\vendor_readers\Bruker\hystar_enum_definitions.txt" for details provided by Bruker
//

#define PWIZ_SOURCE

#include "ChromatogramList_Bruker.hpp"

#ifdef PWIZ_READER_BRUKER
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/automation_vector.h"
#include "pwiz/data/common/cv.hpp"

using namespace pwiz::util;
using namespace pwiz::vendor_api::Bruker;
using namespace pwiz::cv;


#include "sqlite3pp.h"
#include <sstream>


namespace pwiz {
namespace msdata {
namespace detail {

enum class TraceType
{
    NoneTrace = 0,
    ChromMS = 1,
    // No 2?
    ChromUV = 3,
    ChromPressure = 4,
    ChromSolventMix = 5,
    ChromFlow = 6,
    ChromTemperature = 7,
    ChromUserDefined = 9999
};

enum class TraceUnit
{
    NoneUnit = 0,
    Length_nm = 1,
    Flow_mul_min = 2,
    Pressure_bar = 3,
    Percent = 4,
    Temperature_C = 5,
    Intensity = 6,
    UnknownUnit = 7,
    Absorbance_AU = 8,
    Absorbance_mAU = 9,
    Counts = 10,
    Current_A = 11,
    Current_mA = 12,
    Current_muA = 13,
    Flow_ml_min = 14,
    Flow_nl_min = 15,
    Length_cm = 16,
    Length_mm = 17,
    Length_mum = 18,
    // No 19?
    Luminescence = 20,
    Molarity_mM = 21,
    Power_W = 22,
    Power_mW = 23,
    Pressure_mbar = 24,
    Pressure_kPa = 25,
    Pressure_MPa = 26,
    Pressure_psi = 27,
    RefractiveIndex = 28,
    Temperature_F = 30,
    Time_h = 31,
    Time_min = 32,
    Time_s = 33,
    Time_ms = 34,
    Time_mus = 35,
    Viscosity_cP = 36,
    Voltage_kV = 37,
    Voltage_V = 38,
    Voltage_mV = 39,
    Volume_l = 40,
    Volume_ml = 41,
    Volume_mul = 42,
    Energy_J = 43,
    Energy_mJ = 44,
    Energy_muJ = 45,
    Length_Angstrom = 46,
};


using pwiz::vendor_api::Bruker::Chromatogram;
using pwiz::vendor_api::Bruker::ChromatogramPtr;

// Forward declarations for local helpers used below
static CVID traceTypeToCVID(int type, int unit, const std::string description);
static CVID traceUnitToCVID(int unit, double& value);
static TraceUnit getDefaultUnitForTraceType(CVID chromatogramType);

std::vector<vendor_api::Bruker::ChromatogramPtr> readChromatographyDataSqlite(const std::string rootpath)
{
    std::vector<vendor_api::Bruker::ChromatogramPtr> results;
    try
    {
        bfs::path chromFilepath = bfs::path(rootpath) / "chromatography-data.sqlite";
        if (!bfs::exists(chromFilepath))
            return results;

        sqlite3pp::database db(chromFilepath.string());
        sqlite3pp::query q(db, "SELECT Id FROM TraceSources ORDER BY Id");

        for (sqlite3pp::query::iterator itr = q.begin(); itr != q.end(); ++itr)
        {
            int traceId = itr->get<int>(0);

            ChromatogramPtr chrom(new Chromatogram);

            int unitValue = 0;
            double timeOffset = 0.0;

            // Get trace metadata from TraceSources
            std::ostringstream sourceQuery;
            sourceQuery << "SELECT Type, Unit, Description, Instrument, TimeOffset FROM TraceSources WHERE Id=" << traceId;
            sqlite3pp::query sq(db, sourceQuery.str().c_str());
            sqlite3pp::query::iterator sitr = sq.begin();
            if (sitr != sq.end())
            {
                chrom->description = sitr->get<std::string>(2);
                chrom->instrument = sitr->get<std::string>(3);
                timeOffset = sitr->get<double>(4);
                unitValue = sitr->get<int>(1);
                chrom->type = traceTypeToCVID(sitr->get<int>(0), unitValue, chrom->description);
                if (unitValue == static_cast<int>(TraceUnit::NoneUnit))
                {
                    unitValue = static_cast<int>(TraceUnit::Intensity); // default to Intensity if no unit specified
                }
                double dummyValue = 0.0;
                chrom->units = traceUnitToCVID(unitValue, dummyValue);
            }

            sqlite3_stmt* chunksStmt = nullptr;
            std::ostringstream chunksQuery;
            chunksQuery << "SELECT Times, Intensities FROM TraceChunks WHERE Trace=? ORDER BY ROWID";

            if (sqlite3_prepare_v2(db.connected(), chunksQuery.str().c_str(), -1, &chunksStmt, nullptr) == SQLITE_OK)
            {
                sqlite3_bind_int(chunksStmt, 1, traceId);

                while (sqlite3_step(chunksStmt) == SQLITE_ROW)
                {
                    const void* timesBlob = sqlite3_column_blob(chunksStmt, 0);
                    int timesBlobSize = sqlite3_column_bytes(chunksStmt, 0);
                    const void* intensitiesBlob = sqlite3_column_blob(chunksStmt, 1);
                    int intensitiesBlobSize = sqlite3_column_bytes(chunksStmt, 1);

                    if (!timesBlob || !intensitiesBlob)
                        continue;

                    const double* timesData = reinterpret_cast<const double*>(timesBlob);
                    int numTimes = timesBlobSize / static_cast<int>(sizeof(double));
                        chrom->times.insert(chrom->times.end(), timesData, timesData + numTimes);

                    const float* intensitiesData = reinterpret_cast<const float*>(intensitiesBlob);
                    int numIntensities = intensitiesBlobSize / static_cast<int>(sizeof(float));
                    for (int i = 0; i < numIntensities; ++i)
                        chrom->intensities.push_back(static_cast<double>(intensitiesData[i]));
                }
            }

            if (chunksStmt)
                sqlite3_finalize(chunksStmt);


            for (double& intensity : chrom->intensities)
                traceUnitToCVID(unitValue, intensity); // Scale values as needed to fit available standard unit types

            if (timeOffset != 0.0)
            {
                for (double& time : chrom->times)
                    time += timeOffset;
            }
            results.push_back(chrom);
        }
    }
    catch (...)
    {
        // ignore errors, return empty list
    }

    return results;
}

static CVID traceTypeToCVID(int type, int unit, const std::string description)
{
    TraceType traceTypeEnum = static_cast<TraceType>(type);
    switch (traceTypeEnum)
    {
        case TraceType::NoneTrace:
            return CVID_Unknown;
        case TraceType::ChromMS:
            if (description.find("BPC") == 0)
                return MS_basepeak_chromatogram;
            return MS_TIC_chromatogram;
        case TraceType::ChromUV:
            return MS_absorption_chromatogram;
        case TraceType::ChromPressure:
            return MS_pressure_chromatogram;
        case TraceType::ChromSolventMix:
            return MS_chromatogram; // Does not appear to be a suitably differentiated CVID for this
        case TraceType::ChromFlow:
            return MS_flow_rate_chromatogram;
        case TraceType::ChromTemperature:
            return MS_temperature_chromatogram;
        case TraceType::ChromUserDefined:
            if (unit == static_cast<int>(TraceUnit::Temperature_C) ||
                unit == static_cast<int>(TraceUnit::Temperature_F))
            {
                return MS_temperature_chromatogram;
            }
            return MS_chromatogram;
    }
    return CVID_Unknown;
}

static CVID traceUnitToCVID(int unit, double& value)
{
    TraceUnit traceUnitEnum = static_cast<TraceUnit>(unit);
    switch (traceUnitEnum)
    {
        case TraceUnit::NoneUnit:
            return CVID_Unknown;
        case TraceUnit::Length_nm:
            return UO_nanometer;
        case TraceUnit::Flow_mul_min:
            return UO_microliters_per_minute;
        case TraceUnit::Pressure_bar:
            value *= 100000.0; // convert bar to Pa (1 bar = 100000 Pa)
            return UO_pascal;
        case TraceUnit::Percent:
            return UO_percent;
        case TraceUnit::Temperature_C:
            return UO_degree_Celsius;
        case TraceUnit::Intensity:
            return MS_number_of_detector_counts;
        case TraceUnit::UnknownUnit:
            return CVID_Unknown;
        case TraceUnit::Absorbance_AU:
            return UO_absorbance_unit;
        case TraceUnit::Absorbance_mAU:
            value /= 1000.0; // convert mAU to AU
            return UO_absorbance_unit;
        case TraceUnit::Counts:
            return MS_number_of_detector_counts;
        case TraceUnit::Current_A:
            return UO_ampere;
        case TraceUnit::Current_mA:
            return UO_milliampere;
        case TraceUnit::Current_muA:
            return UO_microampere;
        case TraceUnit::Flow_ml_min:
            value *= 1000.0; // convert mL/min to µL/min
            return UO_microliters_per_minute;
        case TraceUnit::Flow_nl_min:
            value /= 1000.0; // convert nL/min to µL/min
            return UO_microliters_per_minute;
        case TraceUnit::Length_cm:
            return UO_centimeter;
        case TraceUnit::Length_mm:
            return UO_millimeter;
        case TraceUnit::Length_mum:
            return UO_micrometer;
        case TraceUnit::Luminescence:
            return CVID_Unknown;
        case TraceUnit::Molarity_mM:
            return UO_millimolar;
        case TraceUnit::Power_W:
            return UO_watt;
        case TraceUnit::Power_mW:
            value /= 1000.0; // convert mW to W
            return UO_watt;
        case TraceUnit::Pressure_mbar:
            value *= 100.0; // convert mbar to Pa (1 mbar = 100 Pa)
            return UO_pascal;
        case TraceUnit::Pressure_kPa:
            value *= 1000.0; // convert kPa to Pa
            return UO_pascal;
        case TraceUnit::Pressure_MPa:
            value *= 1000000.0; // convert MPa to Pa
            return UO_pascal;
        case TraceUnit::Pressure_psi:
            return UO_pounds_per_square_inch;
        case TraceUnit::RefractiveIndex:
            return CVID_Unknown;
        case TraceUnit::Temperature_F:
            return UO_degree_Fahrenheit;
        case TraceUnit::Time_h:
            return UO_hour;
        case TraceUnit::Time_min:
            return UO_minute;
        case TraceUnit::Time_s:
            return UO_second;
        case TraceUnit::Time_ms:
            return UO_millisecond;
        case TraceUnit::Time_mus:
            return UO_microsecond;
        case TraceUnit::Viscosity_cP:
            value /= 100.0; // convert cP to P (1 P = 100 cP)
            return UO_poise;
        case TraceUnit::Voltage_kV:
            return UO_kilovolt;
        case TraceUnit::Voltage_V:
            return UO_volt;
        case TraceUnit::Voltage_mV:
            return UO_millivolt;
        case TraceUnit::Volume_l:
            return UO_liter;
        case TraceUnit::Volume_ml:
            return UO_milliliter;
        case TraceUnit::Volume_mul:
            return UO_microliter;
        case TraceUnit::Energy_J:
            return UO_joule;
        case TraceUnit::Energy_mJ:
            value /= 1000.0; // convert mJ to J
            return UO_joule;
        case TraceUnit::Energy_muJ:
            value /= 1000000.0; // convert µJ to J
            return UO_joule;
        case TraceUnit::Length_Angstrom:
            return UO_angstrom;
    }
    return CVID_Unknown;
}
} // namespace detail
} // namespace msdata
} // namespace pwiz

#endif // PWIZ_READER_BRUKER

