//
// $Id: WatersRawFile.hpp 11611 2017-12-05 17:00:21Z chambm $
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
#include "pwiz/utility/misc/IterationListener.hpp"
#include <boost/shared_ptr.hpp>
#include <boost/core/null_deleter.hpp>
#include <boost/any.hpp>
#include <boost/range/adaptor/map.hpp>

#include <boost/bind.hpp>
#include <vector>
#include <map>
#include <iostream>
#include <fstream>

//#include "MassLynxRawDataFile.h"
#include "MassLynxRawBase.hpp"
#include "MassLynxRawScanReader.hpp"
#include "MassLynxRawChromatogramReader.hpp"
#include "MassLynxRawInfoReader.hpp"
//#include "MassLynxRawScanStatsReader.h"
#include "MassLynxLockMassProcessor.hpp"
#include "MassLynxRawProcessor.hpp"
#include "MassLynxParameters.hpp"
//#include "cdtdefs.h"
//#include "compresseddatacluster.h"

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


class MassLynxRawProcessorWithProgress : public MassLynxRawProcessor
{
    IterationListenerRegistry* ilr_;
    int numSpectra_;
    int lastSpectrum_;

    public:
    MassLynxRawProcessorWithProgress(const string& rawpath, IterationListenerRegistry* ilr = nullptr) : ilr_(ilr), numSpectra_(100), lastSpectrum_(0)
    {
        SetRawPath(rawpath);
    }

    void SetNumSpectra(int numSpectra) { numSpectra_ = numSpectra; }

    virtual void Progress(const int& percent) override
    {
        if (ilr_ != nullptr)
        {
            // must loop through every spectrum because listeners might only respond to every nth iteration (e.g. 100, 200, 300)
            int currentSpectrum_ = (int) floor(percent / 100.0 * numSpectra_);
            for (int i = lastSpectrum_; i <= currentSpectrum_; ++i)
                ilr_->broadcastUpdateMessage(IterationListener::UpdateMessage(i, numSpectra_, "running MassLynx centroider for all spectra"));
            lastSpectrum_ = currentSpectrum_;
        }
    }
};


struct PWIZ_API_DECL RawData
{
    mutable Extended::MassLynxRawScanReader Reader;
    Extended::MassLynxRawInfo Info;
    MassLynxRawChromatogramReader ChromatogramReader;

    struct CachedCompressedDataCluster : public MassLynxRawScanReader
    {
        CachedCompressedDataCluster(Extended::MassLynxRawScanReader& massLynxRawReader) : MassLynxRawScanReader(massLynxRawReader) {}
    };

    const string& RawFilepath() const {return rawpath_;}
    const vector<int>& FunctionIndexList() const {return functionIndexList;}
    const vector<bool>& IonMobilityByFunctionIndex() const {return ionMobilityByFunctionIndex;}
    const vector<bool>& SonarEnabledByFunctionIndex() const {return sonarEnabledByFunctionIndex;}

    bool HasIonMobility() {return hasIonMobility_;}
    bool HasSONAR() {return hasSONAR_;}

    const vector<vector<float>>& TimesByFunctionIndex() const {return timesByFunctionIndex;}
    const vector<vector<float>>& TicByFunctionIndex() const {return ticByFunctionIndex;}

    size_t FunctionCount() const {return functionIndexList.size();}
    size_t LastFunctionIndex() const {return lastFunctionIndex_; }

    RawData(const string& rawpath, IterationListenerRegistry* ilr = nullptr)
        : Reader(rawpath),
          Info(Reader),
          ChromatogramReader(Reader),
          PeakPicker(rawpath, ilr),
          rawpath_(rawpath),
          numSpectra_(0),
          hasProfile_(false),
          hasIonMobility_(false),
          hasSONAR_(false)
    {
        LockMass.SetRawReader(Reader);

        // Count the number of _FUNC[0-9]{3}.DAT files, starting with _FUNC001.DAT
        // For functions over 100, the names become _FUNC0100.DAT
        // Keep track of the maximum function number
        string functionPathmask = rawpath + "/_FUNC*.DAT";
        vector<bfs::path> functionFilepaths;
        expand_pathmask(functionPathmask, functionFilepaths);
        map<int, bfs::path> functionFilepathByNumber;
        for (size_t i=0; i < functionFilepaths.size(); ++i)
        {
            string fileName = BFS_STRING(functionFilepaths[i].filename());
            size_t number = lexical_cast<size_t>(bal::trim_left_copy_if(fileName.substr(5, fileName.length() - 9), bal::is_any_of("0")));
            functionIndexList.push_back(number-1); // 0-based
            functionFilepathByNumber[number-1] = functionFilepaths[i];
            numSpectra_ += Info.GetScansInFunction(number-1);
        }
        sort(functionIndexList.begin(), functionIndexList.end()); // just in case filesystem returns them out of natural order
        lastFunctionIndex_ = functionIndexList.back();
        PeakPicker.SetNumSpectra(numSpectra_);

        ionMobilityByFunctionIndex.resize(lastFunctionIndex_ + 1, false);
        sonarEnabledByFunctionIndex.resize(lastFunctionIndex_ + 1, false);
        timesByFunctionIndex.resize(lastFunctionIndex_ + 1);
        ticByFunctionIndex.resize(lastFunctionIndex_ + 1);
        for (auto& itr : functionFilepathByNumber)
        {
            ionMobilityByFunctionIndex[itr.first] = bfs::exists(itr.second.replace_extension(".cdt")) && Info.GetDriftScanCount(itr.first) > 0;
            if (ionMobilityByFunctionIndex[itr.first])
            {
                hasIonMobility_ = true;

                shared_ptr<CachedCompressedDataCluster>& cdc = cdcByFunction[itr.first];
                cdc.reset(new CachedCompressedDataCluster(Reader));

                // only IMS functions could have SONAR enabled, right?
                sonarEnabledByFunctionIndex[itr.first] = lexical_cast<bool>(Info.GetScanItem(itr.first, 0, MassLynxScanItem::SONAR_ENABLED));
                hasSONAR_ = hasSONAR_ || sonarEnabledByFunctionIndex[itr.first];
            }

            ChromatogramReader.ReadTICChromatogram(itr.first, timesByFunctionIndex[itr.first], ticByFunctionIndex[itr.first]);

            if (!hasProfile_)
                hasProfile_ = hasProfile_ || Info.IsContinuum(itr.first);
        }

        initHeaderProps(rawpath);
    }

    CachedCompressedDataCluster& GetCompressedDataClusterForBlock(int functionIndex, int blockIndex) const
    {
        shared_ptr<CachedCompressedDataCluster>& cdc = cdcByFunction[functionIndex];

        if (!cdc)
            throw std::runtime_error("[MassLynxRaw::GetCompressedDataClusterForBlock] function " + lexical_cast<string>(functionIndex + 1) + " does not have ion mobility data");

        return *cdc;
    }

    double GetDriftTime(int functionIndex, int driftBin) const
    {
        return Info.GetDriftTime(functionIndex, driftBin);
    }

    bool HasCcsCalibration() const
    {
        return bfs::exists(rawpath_ + "/mob_cal.csv");
    }

    float DriftTimeToCCS(float driftTime, float mass, int charge) const
    {
        return Info.GetCollisionalCrossSection(driftTime, mass, charge);
    }

    float CcsToDriftTime(float ccs, float mass, int charge) const
    {
        return Info.GetDriftTime(ccs, mass, charge);
    }
    
    /* e.g.
    Accurate Mass Diagnostic Flags:
    Accurate Mass Flag: 0
    Collision Energy: 0
    Course Laser Control: 0
    FAIMS Compensation Voltage: 0
    Fine Laser Control: 0
    Laser Aim X Position: 0
    Laser Aim Y Position: 0
    Laser Repetition Rate: 0
    Linear Detector Voltage: 0
    Linear Sensitivity: 0
    LockMass Correction: 0
    Number Shots Performed: 0
    Number Shots Summed: 0
    PSD Factor: 0
    PSD Major Step: 0
    PSD Minor Step: 0
    Reference Scan: 0
    Reflectron Detector Voltage: 0
    Reflectron Length: 0
    Reflectron Length Alt: 0
    Reflectron Lens Voltage: 0
    Reflectron Path Length: 0
    Reflectron Path Length Alt: 0
    Reflectron Sensitivity: 0
    Reflectron Voltage: 0
    Sample Plate Voltage: 0
    Sampling Cone Voltage: 0
    Segment Number: 0
    Segment Type: 0
    Set Mass: 0
    Source Region 1: 0
    Source Region 2: 0
    TFM Well: 0
    TIC A Trace: 0
    TIC B Trace: 0
    Temperature Coefficient: 0
    Temperature Correction: 0
    Transport RF: 0
    Use LockMass Correction: 0
    Use Temperature Correction: 0
    */
    string GetScanStat(int functionIndex, int scanIndex, MassLynxScanItem statIndex) const
    {
        try
        {
            return Info.GetScanItem(functionIndex, scanIndex, statIndex);
        }
        catch (MassLynxRawException&)
        {
            return "";
        }
    }

    template <typename T>
    T GetScanStat(int functionIndex, int scanIndex, MassLynxScanItem statIndex) const
    {
        string value = GetScanStat(functionIndex, scanIndex, statIndex);
        if (value.empty())
            throw runtime_error("[MassLynxRaw::GetScanStat] scan stat " + Info.GetScanItemString(statIndex) +
                                " for spectrum " + lexical_cast<string>(functionIndex+1) + ".0." + lexical_cast<string>(scanIndex+1) +
                                " does not have a value but is assumed to have one (report this as a bug)");

        return lexical_cast<T>(value);
    }

    const string& GetHeaderProp(const string& name) const
    {
        map<string, string>::const_iterator findItr = headerProps.find(name);
        if (findItr == headerProps.end())
            return empty_;
        return findItr->second;
    }

    void Centroid() const
    {
        if (!hasProfile_)
        {
            centroidRaw_.reset(const_cast<RawData*>(this), boost::null_deleter());
            return;
        }

        string centroidPath = rawpath_ + "\\centroid.raw";
        if (!bfs::exists(centroidPath))
            PeakPicker.Centroid(centroidPath);

        if (!centroidRaw_)
        {
            try
            {
                centroidRaw_.reset(new RawData(centroidPath));
            }
            catch (MassLynxRawException&)
            {
                bfs::remove_all(centroidPath);
                PeakPicker.Centroid(centroidPath);
                centroidRaw_.reset(new RawData(centroidPath));
            }
        }
    }

    boost::shared_ptr<RawData> CentroidRawDataFile() const
    {
        return centroidRaw_;
    }

    bool LockMassCanBeApplied() const
    {
        return Info.CanLockMassCorrect();
    }

    bool LockMassIsApplied() const
    {
        if (!LockMassCanBeApplied())
            return false;
        return Info.IsLockMassCorrected();
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
        
        MassLynxParameters parms;
        parms.Set(LockMassParameter::MASS, newMz);
        parms.Set(LockMassParameter::TOLERANCE, newTolerance);
        LockMass.LockMassCorrect(parms);
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
    MassLynxLockMassProcessor LockMass;
    mutable MassLynxRawProcessorWithProgress PeakPicker;
    mutable boost::shared_ptr<RawData> centroidRaw_;

    string rawpath_, empty_;
    vector<int> functionIndexList;
    size_t lastFunctionIndex_;
    vector<bool> ionMobilityByFunctionIndex;
    vector<bool> sonarEnabledByFunctionIndex;
    vector<vector<float>> timesByFunctionIndex;
    vector<vector<float>> ticByFunctionIndex;
    map<string, string> headerProps;
    int numSpectra_; // not separated by ion mobility
    bool hasProfile_; // can only centroid if at least one function is profile mode
    bool hasIonMobility_;
    bool hasSONAR_;

    mutable map<int, shared_ptr<CachedCompressedDataCluster> > cdcByFunction;

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


inline PwizFunctionType WatersToPwizFunctionType(MassLynxFunctionType functionType)
{
    return (PwizFunctionType) ((int) functionType - FUNCTION_TYPE_BASE);
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

enum IonMode {
    IM_EIP = 0,
    IM_EIM,
    IM_CIP,
    IM_CIM,
    IM_FBP,
    IM_FBM,
    IM_TSP,
    IM_TSM,
    IM_ESP,
    IM_ESM,
    IM_AIP,
    IM_AIM,
    IM_LDP,
    IM_LDM,
    IM_FIP,
    IM_FIM,
    IM_GENERIC
};


inline PwizIonizationType WatersToPwizIonizationType(MassLynxIonMode ionMode)
{
    switch ((IonMode) ((int) ionMode - ION_MODE_BASE))
    {
        case IM_GENERIC: return IonizationType_Generic;
        default: return (PwizIonizationType) (((int)ionMode - ION_MODE_BASE) / 2);
    }
}


enum PWIZ_API_DECL PwizPolarityType
{
    PolarityType_Unknown = -1,
    PolarityType_Positive = 0,
    PolarityType_Negative,
    PolarityType_Count
};


inline PwizPolarityType WatersToPwizPolarityType(MassLynxIonMode ionMode)
{
    switch ((IonMode)((int)ionMode - ION_MODE_BASE))
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
