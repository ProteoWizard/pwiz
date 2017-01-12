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
#include "MassLynxRawLockMass.h"
#include "cdtdefs.h"
#include "compresseddatacluster.h"

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

    struct CachedCompressedDataCluster : public MassLynxRawScanReader
    {
        CachedCompressedDataCluster(MassLynxRawReader& massLynxRawReader) : MassLynxRawScanReader(massLynxRawReader) {}
    };

    const string& RawFilepath() const {return rawpath_;}
    const vector<int>& FunctionIndexList() const {return functionIndexList;}
    const vector<bool>& IonMobilityByFunctionIndex() const {return ionMobilityByFunctionIndex;}

    size_t FunctionCount() const {return functionIndexList.size();}
    size_t LastFunctionIndex() const {return lastFunctionIndex_; }

    RawData(const string& rawpath)
        : Reader(rawpath),
          Info(Reader),
          ScanReader(Reader),
          ChromatogramReader(Reader),
          LockMass(Reader),
          rawpath_(rawpath),
          scanStatsInitialized(init_once_flag_proxy)
    {
        // Count the number of _FUNC[0-9]{3}.DAT files, starting with _FUNC001.DAT
        // For functions over 100, the names become _FUNC0100.DAT
        // Keep track of the maximum function number
        string functionPathmask = rawpath + "/_FUNC*.DAT";
        vector<bfs::path> functionFilepaths;
        expand_pathmask(functionPathmask, functionFilepaths);
        for (size_t i=0; i < functionFilepaths.size(); ++i)
        {
            string fileName = BFS_STRING(functionFilepaths[i].filename());
            size_t number = lexical_cast<size_t>(bal::trim_left_copy_if(fileName.substr(5, fileName.length() - 9), bal::is_any_of("0")));
            functionIndexList.push_back(number-1); // 0-based
        }
        sort(functionIndexList.begin(), functionIndexList.end()); // just in case filesystem returns them out of natural order
        lastFunctionIndex_ = functionIndexList.back();

        ionMobilityByFunctionIndex.resize(lastFunctionIndex_+1, false);
        CompressedDataCluster tmpCDC;
        for (size_t i=0; i < functionIndexList.size(); ++i)
        {
            tmpCDC.Initialise(rawpath.c_str(), functionIndexList[i] + 1); // 1-based
            ionMobilityByFunctionIndex[i] = tmpCDC.isInitialised();
            if (tmpCDC.isInitialised())
            {
                shared_ptr<CachedCompressedDataCluster>& cdc = cdcByFunction[i];
                cdc.reset(new CachedCompressedDataCluster(Reader));
            }
        }

        initHeaderProps(rawpath);
    }

    CachedCompressedDataCluster& GetCompressedDataClusterForBlock(int functionIndex, int blockIndex) const
    {
        shared_ptr<CachedCompressedDataCluster>& cdc = cdcByFunction[functionIndex];

        if (!cdc)
            throw std::runtime_error("[MassLynxRaw::GetCompressedDataClusterForBlock] function " + lexical_cast<string>(functionIndex + 1) + " does not have ion mobility data");

        //if (cdc->currentBlock != blockIndex)
        //{
        //    cdc->loadDataBlock(blockIndex);
        //    cdc->currentBlock = blockIndex;
        //}

        return *cdc;
    }

    double GetDriftTime(int functionIndex, int blockIndex, int scanIndex) const
    {
        boost::call_once(scanStatsInitialized.flag, boost::bind(&RawData::initScanStats, this));
        const ExtendedScanStatsByName& extendedScanStatsByName = extendedScanStatsByFunction[functionIndex];
        ExtendedScanStatsByName::const_iterator transportRFItr = extendedScanStatsByName.find("Transport RF");
        if (transportRFItr != extendedScanStatsByName.end())
        {
            double transportRF = boost::any_cast<short>(transportRFItr->second[std::min(transportRFItr->second.size()-1, (size_t) blockIndex)]);
            double pusherInterval = 1000 / transportRF;
            return scanIndex * pusherInterval; // 0-based, to match PLGS _final_fragment.csv bin values
        }
        return 0.0;
    }

    const MSScanStats& GetScanStats(int functionIndex, int scanIndex) const
    {
        boost::call_once(scanStatsInitialized.flag, boost::bind(&RawData::initScanStats, this));
        return scanStatsByFunction[functionIndex][scanIndex];
    }

    const vector<MSScanStats>& GetAllScanStatsForFunction(int functionIndex) const
    {
        boost::call_once(scanStatsInitialized.flag, boost::bind(&RawData::initScanStats, this));
        return scanStatsByFunction[functionIndex];
    }

    const ExtendedScanStatsByName& GetExtendedScanStats(int functionIndex) const
    {
        boost::call_once(scanStatsInitialized.flag, boost::bind(&RawData::initScanStats, this));
        return extendedScanStatsByFunction[functionIndex];
    }

    const string& GetHeaderProp(const string& name) const
    {
        map<string, string>::const_iterator findItr = headerProps.find(name);
        if (findItr == headerProps.end())
            return empty_;
        return findItr->second;
    }

    bool LockMassCanBeApplied() const
    {
        bool canBeApplied;
        LockMass.CanApplyLockMassCorrection(canBeApplied);
        return canBeApplied;
    }

    bool LockMassIsApplied() const
    {
        if (!LockMassCanBeApplied())
            return false;
        bool isApplied;
        LockMass.GetLockMassCorrectionApplied(isApplied);
        return isApplied;
    }

    bool ApplyLockMass(double mz, double tolerance)
    {
        const float MZ_EPSILON = 1e-5f;
        const float TOLERANCE_EPSILON = 1e-5f;
        float newMz = (float) mz;
        float newTolerance = (float) tolerance;

        //bool canApply;
        //LockMass.CanApplyLockMassCorrection(canApply);
        //if (!canApply)
        //    return false; // lockmass correction not available

        if (LockMassIsApplied())
        {
            float appliedMz, appliedTolerance;
            LockMass.GetLockMassValues(appliedMz, appliedTolerance);
            if (fabs(newMz - appliedMz) < MZ_EPSILON &&
                fabs(newTolerance - appliedTolerance) < TOLERANCE_EPSILON)
                return true; // existing lockmass is still applied
        }

        // apply new values
        LockMass.UpdateLockMassCorrection(newMz, newTolerance);
        return true;
    }

    void RemoveLockMass()
    {
        if (LockMassIsApplied())
        {
            LockMass.RemoveLockMassCorrection();  // This is quite expensive in time and memory if no lockmass has actually been applied
        }
    }

    private:
    MassLynxRawLockMass LockMass;

    string rawpath_, empty_;
    vector<int> functionIndexList;
    size_t lastFunctionIndex_;
    vector<bool> ionMobilityByFunctionIndex;
    map<string, string> headerProps;

    mutable map<int, shared_ptr<CachedCompressedDataCluster> > cdcByFunction;

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

            scanStatsByFunction.resize(lastFunctionIndex_+1);
            extendedScanStatsByFunction.resize(lastFunctionIndex_+1);

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
                        case Waters::CHAR: fillExtendedStatsByName<char>(statsReader, function, type, extendedScanStatsMap); break;
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
