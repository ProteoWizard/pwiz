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


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/Exception.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Once.hpp"
#include <boost/shared_ptr.hpp>
#include <boost/any.hpp>
#include <boost/foreach.hpp>
#include <boost/bind.hpp>
#include <vector>
#include <map>
#include <iostream>
#include <fstream>

#include "MassLynxRawDataFile.h"
#include "MassLynxRawReader.h"
#include "MassLynxRawScanReader.h"
#include "MassLynxRawChromatogramReader.h"
#include "MassLynxRawInfo.h"
#include "MassLynxRawScanStatsReader.h"

namespace pwiz {
namespace vendor_api {
namespace Waters {


using boost::shared_ptr;
using std::vector;
using std::map;
using std::pair;
using std::string;
using std::ifstream;
using namespace pwiz::util;
using namespace ::Waters::Lib::MassLynxRaw;


struct PWIZ_API_DECL RawData
{
    mutable MassLynxRawReader Reader;
    MassLynxRawInfo Info;
    MassLynxRawScanReader ScanReader;
    MassLynxRawChromatogramReader ChromatogramReader;

    typedef map<string, vector<boost::any> > ExtendedScanStatsByName;

    const vector<int>& FunctionIndexList() const {return functionIndexList;}
    size_t FunctionCount() const {return functionIndexList.back() + 1;}

    RawData(const string& rawpath)
        : Reader(rawpath),
          Info(Reader),
          ScanReader(Reader),
          ChromatogramReader(Reader),
          scanStatsInitialized(init_once_flag_proxy)
    {
        // Count the number of _FUNC[0-9]{3}.DAT files, starting with _FUNC001.DAT
		// For functions over 100, the names become _FUNC0100.DAT
        // Keep track of the maximum function number
        string functionPathmask = rawpath + "/_FUNC*.DAT";
        vector<bfs::path> functionFilepaths;
        expand_pathmask(functionPathmask, functionFilepaths);
        int functionCount = 0;
        for (size_t i=0; i < functionFilepaths.size(); ++i)
        {
			string fileName = functionFilepaths[i].filename();
            int number = lexical_cast<int>(bal::trim_left_copy_if(fileName.substr(5, fileName.length() - 9), bal::is_any_of("0")));
            functionIndexList.push_back(number-1); // 0-based
            functionCount = std::max(functionCount, number);
        }

        initHeaderProps(rawpath);
	}

	const MSScanStats& GetScanStats(int functionIndex, int scanIndex) const
	{
		boost::call_once(scanStatsInitialized.flag, boost::bind(&RawData::initScanStats, this));
		return scanStatsByFunction[functionIndex][scanIndex];
	}

	const ExtendedScanStatsByName& GetExtendedScanStats(int functionIndex) const
	{
		boost::call_once(scanStatsInitialized.flag, boost::bind(&RawData::initScanStats, this));
		return extendedScanStatsByFunction[functionIndex];
	}

    const string GetHeaderProp(string name) const
    {
        if (headerProps.count(name) == 0)
            return "";
        return headerProps.find(name)->second;
    }

    private:
    vector<int> functionIndexList;
    map<string, string> headerProps;

	mutable once_flag_proxy scanStatsInitialized;
    mutable vector<vector<MSScanStats>> scanStatsByFunction;
    mutable vector<ExtendedScanStatsByName> extendedScanStatsByFunction;

    void initHeaderProps(const string& rawpath)
    {
        string headerTextPath = rawpath + "/_HEADER.TXT";
        ifstream in(headerTextPath.c_str());

        if (!in.is_open())
            return;

        string line;
        while(getline(in, line))
        {
            size_t c_pos = line.find(": ");
            if (line.find("$$ ") != 0 || c_pos == string::npos)
                continue;

            string name = line.substr(3, c_pos - 3);
            string value = line.substr(c_pos + 2, line.size() - (c_pos + 2));
            headerProps[name] = value;
            //std::cout << name << " = " << value << std::endl;
        }
    }

	void initScanStats() const
	{
        try
        {
            MassLynxRawScanStatsReader statsReader(Reader);

            scanStatsByFunction.resize(FunctionCount());
            extendedScanStatsByFunction.resize(FunctionCount());

            BOOST_FOREACH(int function, functionIndexList)
            {
                statsReader.readScanStats(function, scanStatsByFunction[function]);

                ExtendedScanStatsByName& extendedScanStatsMap = extendedScanStatsByFunction[function];

                vector<ExtendedStatsType> extendedStatsTypes;
                statsReader.getExtendedStatsTypes(function, extendedStatsTypes);
                for (size_t i=0; i < extendedStatsTypes.size(); ++i)
                {
                    ExtendedStatsType& type = extendedStatsTypes[i];
                    if(type.name.empty())
                        continue;

                    //std::cout << extendedStatsTypes[i].name << " " << extendedStatsTypes[i].typeCode << std::endl;
                    switch (type.typeCode)
                    {
                        case CHAR: fillExtendedStatsByName<char>(statsReader, function, type, extendedScanStatsMap); break;
                        case SHORT_INT: fillExtendedStatsByName<short>(statsReader, function, type, extendedScanStatsMap); break;
                        case LONG_INT: fillExtendedStatsByName<int>(statsReader, function, type, extendedScanStatsMap); break;
                        case SINGLE_FLOAT: fillExtendedStatsByName<float>(statsReader, function, type, extendedScanStatsMap); break;
                        case DOUBLE_FLOAT: fillExtendedStatsByName<double>(statsReader, function, type, extendedScanStatsMap); break;

                        case STRING:
                        default:
                            throw std::runtime_error("cannot handle string extended stats");
                    }
                }
            }
        }
        catch (std::exception& e)
        {
            // TODO: log error (can't propogate from inside call_once)
            std::cerr << "[MassLynxRaw::initScanStats] " << e.what() << std::endl;
        }
        catch (...)
        {
            // TODO: log error (can't propogate from inside call_once)
            std::cerr << "[MassLynxRaw::initScanStats] caught unknown exception" << std::endl;
        }
    }

    template <typename T>
    inline void fillExtendedStatsByName(const MassLynxRawScanStatsReader& statsReader, int function, const ExtendedStatsType& type, ExtendedScanStatsByName& statsMap) const
    {
        std::pair<ExtendedScanStatsByName::iterator, bool> insertResult =
            statsMap.insert(std::make_pair(type.name, vector<boost::any>()));
        vector<boost::any>& statsVector = insertResult.first->second;

        vector<T> stats;
        statsReader.getExtendedStatsField<T>(function, type, stats);
        for (size_t i=0; i < stats.size(); ++i)
            statsVector.push_back(stats[i]);
    }
};

typedef shared_ptr<RawData> RawDataPtr;


enum PWIZ_API_DECL PwizFunctionType
{
    FunctionType_Scan,                  /// Standard MS scanning function
    FunctionType_SIR,                   /// Selected ion recording
    FunctionType_Delay,                 /// No longer supported
    FunctionType_Concatenated,          /// No longer supported
    FunctionType_Off,                   /// No longer supported
    FunctionType_Parents,               /// MSMS Parent scan
    FunctionType_Daughters,             /// MSMS Daughter scan
    FunctionType_Neutral_Loss,          /// MSMS Neutral Loss
    FunctionType_Neutral_Gain,          /// MSMS Neutral Gain
    FunctionType_MRM,                   /// Multiple Reaction Monitoring
    FunctionType_Q1F,                   /// Special function used on Quattro IIs for scanning MS1 (Q1) but uses the final detector
    FunctionType_MS2,                   /// Special function used on triple quads for scanning MS2. Used for calibration experiments.
    FunctionType_Diode_Array,           /// Diode array type function
    FunctionType_TOF,                   /// TOF
    FunctionType_TOF_PSD,               /// TOF Post Source Decay type function
    FunctionType_TOF_Survey,            /// QTOF MS Survey scan
    FunctionType_TOF_Daughter,          /// QTOF MSMS scan
    FunctionType_MALDI_TOF,             /// Maldi-Tof function
    FunctionType_TOF_MS,                /// QTOF MS scan
    FunctionType_TOF_Parent,            /// QTOF Parent scan
    FunctionType_Voltage_Scan,          /// AutoSpec Voltage Scan
    FunctionType_Magnetic_Scan,         /// AutoSpec Magnet Scan
    FunctionType_Voltage_SIR,           /// AutoSpec Voltage SIR
    FunctionType_Magnetic_SIR,          /// AutoSpec Magnet SIR
    FunctionType_Auto_Daughters,        /// Quad Automated daughter scanning
    FunctionType_AutoSpec_B_E_Scan,     /// AutoSpec_B_E_Scan
    FunctionType_AutoSpec_B2_E_Scan,    /// AutoSpec_B2_E_Scan
    FunctionType_AutoSpec_CNL_Scan,     /// AutoSpec_CNL_Scan
    FunctionType_AutoSpec_MIKES_Scan,   /// AutoSpec_MIKES_Scan
    FunctionType_AutoSpec_MRM,          /// AutoSpec_MRM
    FunctionType_AutoSpec_NRMS_Scan,    /// AutoSpec_NRMS_Scan
    FunctionType_AutoSpec_Q_MRM_Quad,   /// AutoSpec_Q_MRM_Quad
};


inline PwizFunctionType WatersToPwizFunctionType(FunctionType functionType)
{
    return (PwizFunctionType) functionType;
}


enum PWIZ_API_DECL PwizIonizationType
{
    IonizationType_Unknown = -1,
    IonizationType_EI = 0,       // Electron Ionization
    IonizationType_CI,           // Chemical Ionization
    IonizationType_FB,           // Fast Atom Bombardment
    IonizationType_TS,           // Thermospray
    IonizationType_ES,           // Electrospray Ionization
    IonizationType_AI,           // Atmospheric Ionization
    IonizationType_LD,           // Laser Desorption Ionization
    IonizationType_FI,           // ?
    IonizationType_Generic,
    IonizationType_Count
};


inline PwizIonizationType WatersToPwizIonizationType(IonMode ionMode)
{
    switch (ionMode)
    {
        case IM_GENERIC: return IonizationType_Generic;
        default: return (PwizIonizationType) (int(ionMode) / 2);
    }
}


enum PWIZ_API_DECL PwizPolarityType
{
    PolarityType_Unknown = -1,
    PolarityType_Positive = 0,
    PolarityType_Negative,
    PolarityType_Count
};


inline PwizPolarityType WatersToPwizPolarityType(IonMode ionMode)
{
    switch (ionMode)
    {
        case IM_EIP: case IM_CIP: case IM_FBP: case IM_TSP:
        case IM_ESP: case IM_AIP: case IM_LDP: case IM_FIP:
            return PolarityType_Positive;

        case IM_EIM: case IM_CIM: case IM_FBM: case IM_TSM:
        case IM_ESM: case IM_AIM: case IM_LDM: case IM_FIM:
            return PolarityType_Negative;

        default: return PolarityType_Unknown;
    }
}


} // namespace Waters
} // namespace vendor_api
} // namespace pwiz
