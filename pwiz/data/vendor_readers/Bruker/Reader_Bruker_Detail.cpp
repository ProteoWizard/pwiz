//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
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


#include "Reader_Bruker_Detail.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/data/msdata/Reader.hpp"

using namespace pwiz::vendor_api::Bruker;

namespace pwiz {
namespace msdata {
namespace detail {
namespace Bruker {


using namespace pwiz::util;

Reader_Bruker_Format format(const string& path)
{
    bfs::path sourcePath(path);

    // Make sure target "path" is actually a directory since
    // all Bruker formats are directory-based
    if (!bfs::is_directory(sourcePath))
    {
        // Special cases for identifying direct paths to fid/Analysis.yep/Analysis.baf/.U2
        // Note that direct paths to baf or u2 will fail to find a baf/u2 hybrid source
        std::string leaf = BFS_STRING(sourcePath.filename());
        bal::to_lower(leaf);
        if (leaf == "fid" && !bfs::exists(sourcePath.parent_path() / "analysis.baf"))
            return Reader_Bruker_Format_FID;
        else if(extension(sourcePath) == ".u2")
            return Reader_Bruker_Format_U2;
        else if(leaf == "analysis.yep")
            return Reader_Bruker_Format_YEP;
        else if(leaf == "analysis.baf")
            return Reader_Bruker_Format_BAF;
        else if(leaf == "analysis.tdf" ||
                leaf == "analysis.tdf_bin")
            return Reader_Bruker_Format_TDF;
        else if(leaf == "analysis.tsf" ||
                leaf == "analysis.tsf_bin")
            return Reader_Bruker_Format_TSF;
        else
            return Reader_Bruker_Format_Unknown;
    }

    // Check for tdf-based data;
    // The directory should have a file named "Analysis.tdf"
    if (bfs::exists(sourcePath / "Analysis.tdf") || bfs::exists(sourcePath / "analysis.tdf"))
        return Reader_Bruker_Format_TDF;
    if (bfs::exists(sourcePath / "Analysis.tsf") || bfs::exists(sourcePath / "analysis.tsf"))
        return Reader_Bruker_Format_TSF;

    // TODO: 1SRef is not the only possible substring below, get more examples!

    // Check for fid-based data;
    // Every directory within the queried directory should have a "1/1SRef"
    // subdirectory with a fid file in it, but we check only the first non-dotted
    // directory for efficiency. This can fail, but those failures are acceptable.
    // Alternatively, a directory closer to the fid file can be identified.
    // Caveat: BAF files may be accompanied by a fid, skip these cases! (?)
    const static bfs::directory_iterator endItr;
    bfs::directory_iterator itr(sourcePath);
    for (; itr != endItr; ++itr)
        if (bfs::is_directory(itr->status()))
        {
            if (BFS_STRING(itr->path().filename())[0] == '.') // HACK: skip ".svn"
                continue;
            else if (bfs::exists(itr->path() / "1/1SRef/fid") ||
                     bfs::exists(itr->path() / "1SRef/fid") ||
                     bfs::exists(itr->path() / "1/1SLin/fid") ||
                     bfs::exists(itr->path() / "1SLin/fid") ||
                     bfs::exists(itr->path() / "1/1Ref/fid") ||
                     bfs::exists(itr->path() / "1Ref/fid") ||
                     bfs::exists(itr->path() / "1/1Lin/fid") ||
                     bfs::exists(itr->path() / "1Lin/fid") ||
                     (bfs::exists(itr->path() / "fid") && !bfs::exists(itr->path() / "Analysis.baf") && !bfs::exists(itr->path() / "analysis.baf")) ||
                     (bfs::exists(sourcePath / "fid") && !bfs::exists(sourcePath / "Analysis.baf") && !bfs::exists(sourcePath / "analysis.baf")))
                    return Reader_Bruker_Format_FID;
            else
                break;
        }

    // Check for yep-based data;
    // The directory should have a file named "Analysis.yep"
    if (bfs::exists(sourcePath / "Analysis.yep") || bfs::exists(sourcePath / "analysis.yep"))
        return Reader_Bruker_Format_YEP;

    bfs::path sourceDirectory = *(--sourcePath.end());

    // Check for baf-based data;
    // The directory should have a file named "Analysis.baf"
    if (bfs::exists(sourcePath / "Analysis.baf") || bfs::exists(sourcePath / "analysis.baf"))
    {
        // Check for baf/u2 hybrid data
        if (bfs::exists(sourcePath / sourceDirectory.replace_extension(".u2")))
            return Reader_Bruker_Format_BAF_and_U2;
        else
            return Reader_Bruker_Format_BAF;
    }

    // Check for u2-based data;
    // The directory should have a file named "<directory-name - ".d">.u2"
    if (bfs::exists(sourcePath / sourceDirectory.replace_extension(".u2")))
        return Reader_Bruker_Format_U2;

    return Reader_Bruker_Format_Unknown;
}


#ifdef PWIZ_READER_BRUKER

std::vector<InstrumentConfiguration> createInstrumentConfigurations(CompassDataPtr rawfile)
{
    vector<InstrumentConfiguration> configurations;
    map<string, string> parameterMap;
    
    if (rawfile->getMSSpectrumCount() > 0)
    {
        MSSpectrumPtr firstSpectrum = rawfile->getMSSpectrum(1);
        MSSpectrumParameterListPtr parametersPtr = firstSpectrum->parameters();
        const MSSpectrumParameterList& parameters = *parametersPtr;

        BOOST_FOREACH(const MSSpectrumParameter& p, parameters)
            parameterMap[p.name] = p.value;
    }

    switch (rawfile->getInstrumentSource())
    {
        case InstrumentSource_AP_MALDI:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(Component(MS_AP_MALDI, 1));
            break;

        case InstrumentSource_MALDI:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(Component(MS_MALDI, 1));
            break;

        case InstrumentSource_ESI:
        case InstrumentSource_MULTI_MODE:
        case InstrumentSource_Ultraspray:
        case InstrumentSource_VIP_HESI:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(Component(MS_ESI, 1));
            configurations.back().componentList.back().set(MS_electrospray_inlet);
            break;

        case InstrumentSource_NANO_ESI_OFFLINE:
        case InstrumentSource_NANO_ESI_ONLINE:
        case InstrumentSource_NANO_FLOW_ESI:
        case InstrumentSource_CaptiveSpray:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(Component(MS_nanoelectrospray, 1));
            configurations.back().componentList.back().set(MS_nanospray_inlet);
            break;

        case InstrumentSource_APCI:
        case InstrumentSource_GC_APCI:
        case InstrumentSource_VIP_APCI:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(Component(MS_atmospheric_pressure_chemical_ionization, 1));
            break;

        case InstrumentSource_APPI:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(Component(MS_atmospheric_pressure_photoionization, 1));
            break;

        case InstrumentSource_EI:
            configurations.push_back(InstrumentConfiguration());
            configurations.back().componentList.push_back(Component(MS_electron_ionization, 1));
            break;
            
        case InstrumentSource_AlsoUnknown:
        case InstrumentSource_Unknown:
        {
            switch (rawfile->getInstrumentFamily())
            {
                case InstrumentFamily_Trap:
                case InstrumentFamily_OTOF:
                case InstrumentFamily_OTOFQ:
                case InstrumentFamily_maXis:
                case InstrumentFamily_compact:
                case InstrumentFamily_impact:
                    configurations.push_back(InstrumentConfiguration());
                    configurations.back().componentList.push_back(Component(MS_ESI, 1));
                    configurations.back().componentList.back().set(MS_electrospray_inlet);
                    break;

                case InstrumentFamily_MaldiTOF:
                case InstrumentFamily_BioTOF:
                case InstrumentFamily_BioTOFQ:
                    configurations.push_back(InstrumentConfiguration());
                    configurations.back().componentList.push_back(Component(MS_MALDI, 1));
                    break;

                case InstrumentFamily_FTMS:
                case InstrumentFamily_solariX:
                    configurations.push_back(InstrumentConfiguration());
                    if (parameterMap["Mobile Hexapole Position"] == "MALDI") // HACK: I haven't seen enough data to know whether this is robust.
                        configurations.back().componentList.push_back(Component(MS_MALDI, 1));
                    else
                    {
                        configurations.back().componentList.push_back(Component(MS_ESI, 1));
                        configurations.back().componentList.back().set(MS_electrospray_inlet);
                    }
                    break;

                case InstrumentFamily_Unknown:
                    break; // unknown configuration

                default:
                    throw runtime_error("[Reader_Bruker::createInstrumentConfigurations] no case for InstrumentFamily " + lexical_cast<string>(rawfile->getInstrumentFamily()));
            }
            break;
        } // InstrumentSource_Unknown

        default:
            throw runtime_error("[Reader_Bruker::createInstrumentConfigurations] no case for InstrumentSource " + lexical_cast<string>(rawfile->getInstrumentSource()));
    }

    switch (rawfile->getInstrumentFamily())
    {
        case InstrumentFamily_Trap:
            configurations.back().componentList.push_back(Component(MS_radial_ejection_linear_ion_trap, 2));
            configurations.back().componentList.push_back(Component(MS_electron_multiplier, 3));
            break;

        case InstrumentFamily_OTOF:
        case InstrumentFamily_MaldiTOF:
            configurations.back().componentList.push_back(Component(MS_time_of_flight, 2));
            configurations.back().componentList.push_back(Component(MS_multichannel_plate, 3));
            configurations.back().componentList.push_back(Component(MS_photomultiplier, 4));
            break;

        case InstrumentFamily_OTOFQ:
        case InstrumentFamily_BioTOFQ:
        case InstrumentFamily_maXis:
        case InstrumentFamily_impact:
        case InstrumentFamily_compact:
        case InstrumentFamily_timsTOF:
            configurations.back().componentList.push_back(Component(MS_quadrupole, 2));
            configurations.back().componentList.push_back(Component(MS_time_of_flight, 3));
            configurations.back().componentList.push_back(Component(MS_multichannel_plate, 4));
            configurations.back().componentList.push_back(Component(MS_photomultiplier, 5));
            break;

        case InstrumentFamily_FTMS:
        case InstrumentFamily_solariX:
            configurations.back().componentList.push_back(Component(MS_FT_ICR, 2));
            configurations.back().componentList.push_back(Component(MS_inductive_detector, 3));
            break;

        default:
        case InstrumentFamily_Unknown:
            break; // unknown configuration
    }

    return configurations;
}


PWIZ_API_DECL cv::CVID translateAsInstrumentSeries(CompassDataPtr rawfile)
{
    switch (rawfile->getInstrumentFamily())
    {
        case InstrumentFamily_Trap: return MS_Bruker_Daltonics_HCT_Series; // or amazon
        case InstrumentFamily_OTOF: return MS_Bruker_Daltonics_micrOTOF_series;
        case InstrumentFamily_OTOFQ: return MS_Bruker_Daltonics_micrOTOF_series; // or ultroTOF
        case InstrumentFamily_BioTOF: return MS_Bruker_Daltonics_BioTOF_series;
        case InstrumentFamily_BioTOFQ: return MS_Bruker_Daltonics_BioTOF_series;
        case InstrumentFamily_MaldiTOF: return MS_Bruker_Daltonics_flex_series;
        case InstrumentFamily_FTMS: return MS_Bruker_Daltonics_apex_series;
        case InstrumentFamily_solariX: return MS_Bruker_Daltonics_solarix_series;
        case InstrumentFamily_timsTOF: return MS_Bruker_Daltonics_timsTOF_series;

        case InstrumentFamily_maXis:
        case InstrumentFamily_compact:
        case InstrumentFamily_impact:
            return MS_Bruker_Daltonics_maXis_series;

        default:
        case InstrumentFamily_Unknown:
            return MS_Bruker_Daltonics_instrument_model;
    }
}

PWIZ_API_DECL cv::CVID translateAsAcquisitionSoftware(CompassDataPtr rawfile)
{
    string name = rawfile->getAcquisitionSoftware();

    if (name.empty()) // fall back on instrument family
        switch (rawfile->getInstrumentFamily())
        {
            case InstrumentFamily_Trap: return MS_HCTcontrol;
            case InstrumentFamily_OTOF: return MS_micrOTOFcontrol;
            case InstrumentFamily_OTOFQ: return MS_micrOTOFcontrol;
            case InstrumentFamily_BioTOF: return MS_Compass;
            case InstrumentFamily_BioTOFQ: return MS_Compass;
            case InstrumentFamily_MaldiTOF: return MS_FlexControl;

            case InstrumentFamily_FTMS:
            case InstrumentFamily_solariX:
                return MS_apexControl;

            case InstrumentFamily_maXis:
            case InstrumentFamily_compact:
            case InstrumentFamily_impact:
                return MS_Compass;

            default:
            case InstrumentFamily_Unknown:
                return MS_Compass;
        }

    if (bal::icontains(name, "HCT")) return MS_HCTcontrol;
    if (bal::icontains(name, "oTOFcontrol")) return MS_micrOTOFcontrol;
    if (bal::icontains(name, "Compass")) return MS_Compass;
    if (bal::icontains(name, "Apex")) return MS_apexControl;
    if (bal::icontains(name, "Flex")) return MS_FlexControl;

    return MS_Compass; // default to Compass
}


// Implementations for trace type/unit -> CVID and scaling helpers
PWIZ_API_DECL pwiz::cv::CVID traceTypeToCVID(TraceType type, TraceUnit unit, const std::string& description)
{
    switch (type)
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
            if (unit == TraceUnit::Temperature_C ||
                unit == TraceUnit::Temperature_F)
            {
                return MS_temperature_chromatogram;
            }
            return MS_chromatogram;
    }
    return CVID_Unknown;
}

PWIZ_API_DECL pwiz::cv::CVID traceUnitToCVID(TraceUnit unit, double& value)
{
    switch (unit)
    {
        case TraceUnit::NoneUnit:
            return CVID_Unknown;
        case TraceUnit::Length_nm:
            return UO_nanometer;
        case TraceUnit::Flow_mul_min:
            return UO_microliters_per_minute;
        case TraceUnit::Pressure_bar:
            return UO_bar;
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
            value /= 1000.0; // convert mbar to bar (1 bar = 1000 mbar)
            return UO_bar;
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

#else

PWIZ_API_DECL std::vector<InstrumentConfiguration> createInstrumentConfigurations(CompassDataPtr rawfile) { throw runtime_error("Reader_Bruker not implemented"); }
PWIZ_API_DECL cv::CVID translateAsInstrumentSeries(CompassDataPtr rawfile) { throw runtime_error("Reader_Bruker not implemented"); }
PWIZ_API_DECL cv::CVID translateAsAcquisitionSoftware(CompassDataPtr rawfile) { throw runtime_error("Reader_Bruker not implemented"); }

#endif


} // namespace Bruker
} // namespace detail
} // namespace msdata
} // namespace pwiz
