//
// $Id$
//
// 
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
//
// Licensed under Creative Commons 3.0 United States License, which requires:
//  - Attribution
//  - Noncommercial
//  - No Derivative Works
//
// http://creativecommons.org/licenses/by-nc-nd/3.0/us/
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#define PWIZ_SOURCE

#include "RawData.hpp"
#include "dacserver.tlh"
#include "pwiz/utility/misc/automation_vector.h"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/COMInitializer.hpp"


using namespace pwiz::util;
using namespace DACSERVERLib;


namespace pwiz {
namespace vendor_api {
namespace Waters {


struct ScanImpl : public Scan
{
    ScanImpl(const bstr_t& rawpath, short function, short process, long scan)
        :   rawpath(rawpath), function(function), process(process), scan(scan)
    {
        // each scan adds to COM's ref count in case the scan outlives RawDataImpl
        COMInitializer::initialize();
        dacSpectrumPtr = IDACSpectrumPtr(CLSID_DACSpectrum);
        dacScanStatsPtr = IDACScanStatsPtr(CLSID_DACScanStats);
        dacExScanStatsPtr = IDACExScanStatsPtr(CLSID_DACExScanStats);
        dacSpectrumPtr->GetSpectrum(rawpath, function, process, scan);
        dacScanStatsPtr->GetScanStats(rawpath, function, process, scan);
        dacExScanStatsPtr->GetExScanStats(rawpath, function, process, scan);
    }

    virtual ~ScanImpl()
    {
        dacSpectrumPtr.Release();
        dacScanStatsPtr.Release();
        dacExScanStatsPtr.Release();
        COMInitializer::uninitialize();
    }

    virtual const int getFunctionNumber() const {return function;}
    virtual const int getProcessNumber() const {return process;}
    virtual const int getScanNumber() const {return scan;}

    virtual bool getDataIsContinuous() const {return dacScanStatsPtr->Continuum != 0;}
    virtual const size_t getNumPoints() const {return dacSpectrumPtr->NumPeaks;}

    virtual const automation_vector<float>& masses() const
    {
        if (masses_.empty())
        {
            VARIANT v = dacSpectrumPtr->Masses.Detach();
            masses_.attach(v);
        }
        return masses_;
    }

    virtual const automation_vector<float>& intensities() const
    {
        if (intensities_.empty())
        {
            VARIANT v = dacSpectrumPtr->Intensities.Detach();
            intensities_.attach(v);
        }
        return intensities_;
    }

    virtual const PrecursorPtr getPrecursorInfo() const
    {
        if (!precursorPtr.get() && dacExScanStatsPtr->SetMass > 0)
        {
            precursorPtr.reset(new Precursor);
            precursorPtr->mz = dacExScanStatsPtr->SetMass;
            precursorPtr->collisionEnergy = dacExScanStatsPtr->CollisionEnergy;
            precursorPtr->collisionRF = dacExScanStatsPtr->CollisionRF;
        }
        return precursorPtr;
    }

    virtual double getStartTime() const {return dacScanStatsPtr->RetnTime;}
    virtual double getTIC() const {return dacScanStatsPtr->TIC;}
    virtual double getBasePeakMZ() const {return dacScanStatsPtr->BPM;}
    virtual double getBasePeakIntensity() const {return dacScanStatsPtr->BPI;}
    virtual double getMinMZ() const {return dacScanStatsPtr->LoMass;}
    virtual double getMaxMZ() const {return dacScanStatsPtr->HiMass;}

    private:
    const bstr_t& rawpath;
    short function, process;
    long scan;

    mutable IDACSpectrumPtr dacSpectrumPtr;
    mutable IDACScanStatsPtr dacScanStatsPtr;
    mutable IDACExScanStatsPtr dacExScanStatsPtr;

    mutable automation_vector<float> masses_;
    mutable automation_vector<float> intensities_;
    mutable PrecursorPtr precursorPtr;
};


namespace {

PwizFunctionType translateFunctionType(const string& funcType)
{
    if (funcType == "Scan")                                 return FunctionType_Scan;
    else if (funcType == "SIR")                             return FunctionType_SIR;
    else if (funcType == "Delay")                           return FunctionType_Delay;
    else if (funcType == "Concatenated")                    return FunctionType_Concatenated;
    else if (funcType == "Off")                             return FunctionType_Off;
    else if (funcType == "Parents")                         return FunctionType_Parents;  
    else if (funcType == "Daughters")                       return FunctionType_Daughters;
    else if (funcType == "Neutral Loss")                    return FunctionType_Neutral_Loss;
    else if (funcType == "Neutral Gain")                    return FunctionType_Neutral_Gain;
    else if (funcType == "MRM")                             return FunctionType_MRM;
    else if (funcType == "Q1F")                             return FunctionType_Q1F;
    else if (funcType == "MS2")                             return FunctionType_MS2;
    else if (funcType == "Diode Array")                     return FunctionType_Diode_Array;
    else if (funcType == "TOF")                             return FunctionType_TOF;
    else if (funcType == "TOF PSD")                         return FunctionType_TOF_PSD;
    else if (funcType == "TOF Survey")                      return FunctionType_TOF_Survey;
    else if (funcType == "TOF Daughter")                    return FunctionType_TOF_Daughter;
    else if (funcType == "Maldi TOF")                       return FunctionType_MALDI_TOF;
    else if (funcType == "TOF MS")                          return FunctionType_TOF_MS;
    else if (funcType == "TOF Parent")                      return FunctionType_TOF_Parent;
    else if (funcType == "Voltage Scan")                    return FunctionType_Voltage_Scan;
    else if (funcType == "Magnetic Scan")                   return FunctionType_Magnetic_Scan;
    else if (funcType == "Voltage SIR")                     return FunctionType_Voltage_SIR;
    else if (funcType == "Magnetic SIR")                    return FunctionType_Magnetic_SIR;
    else if (funcType == "Auto Daughters")                  return FunctionType_Auto_Daughters;
    else if (funcType == "AutoSpec B/E Scan")               return FunctionType_AutoSpec_B_E_Scan;
    else if (funcType == "AutoSpec B^2/E Scan")             return FunctionType_AutoSpec_B2_E_Scan;
    else if (funcType == "AutoSpec CNL Scan")               return FunctionType_AutoSpec_CNL_Scan;
    else if (funcType == "AutoSpec MIKES Scan")             return FunctionType_AutoSpec_MIKES_Scan;
    else if (funcType == "AutoSpec MRM")                    return FunctionType_AutoSpec_MRM;
    else if (funcType == "AutoSpec NRMS Scan")              return FunctionType_AutoSpec_NRMS_Scan;
    else if (funcType == "AutoSpec-Q MRM Quad")             return FunctionType_AutoSpec_Q_MRM_Quad;
    else if (funcType.find("MSMSMS") != string::npos)       return FunctionType_MSMSMS;
    else if (funcType.find("MSMS") != string::npos)         return FunctionType_MSMS;
    else
        throw runtime_error("[translateFunctionType] Unknown Waters function type.");
}

} // namespace


struct FunctionImpl : public Function
{
    FunctionImpl(const bstr_t& rawpath, short function)
        :   rawpath(rawpath), number(function), type(FunctionType_Unknown)            
    {
        // each function adds to COM's ref count in case the function outlives RawDataImpl
        COMInitializer::initialize();
        dacFunctionInfoPtr = IDACFunctionInfoPtr(CLSID_DACFunctionInfo);
        dacFunctionInfoPtr->GetFunctionInfo(rawpath, function);

        VARIANT v = dacFunctionInfoPtr->MRMParents.Detach();
        mrmParents.attach(v);
        v = dacFunctionInfoPtr->MRMDaughters.Detach();
        mrmDaughters.attach(v);
    }

    virtual ~FunctionImpl()
    {
        dacFunctionInfoPtr.Release();
        COMInitializer::uninitialize();
    }

    virtual int getFunctionNumber() const {return number;}

    virtual PwizFunctionType getFunctionType() const
    {
        if (type == FunctionType_Unknown)
            type = translateFunctionType((const char*) dacFunctionInfoPtr->FunctionType);
        return type;
    }

    virtual size_t getScanCount() const {return dacFunctionInfoPtr->NumScans;}

    virtual ScanPtr getScan(int process, int scan) const
    {
        return ScanPtr(new ScanImpl(rawpath, number, process, scan));
    }

    virtual double getSetMass() const {return dacFunctionInfoPtr->FunctionSetMass;}

    virtual size_t getSRMSize() const {return mrmDaughters.size();}
    virtual void getSRM(size_t index, SRMTarget& target) const
    {
        target.Q1 = mrmParents[index];
        target.Q3 = mrmDaughters[index];
    }

    virtual void getSIC(size_t index, automation_vector<float>& times, automation_vector<float>& intensities) const
    {
        IDACChromatogramPtr dacChromatogramPtr(CLSID_DACChromatogram);
        dacChromatogramPtr->GetChromatogram(rawpath,
                                            number,
                                            0,
                                            dacFunctionInfoPtr->StartRT,
                                            dacFunctionInfoPtr->EndRT,
                                            1,
                                            ("Ch" + lexical_cast<string>(index+1)).c_str());

        VARIANT v = dacChromatogramPtr->Times.Detach();
        times.attach(v);
        v = dacChromatogramPtr->Intensities.Detach();
        intensities.attach(v);
    }

    virtual double getStartTime() const {return dacFunctionInfoPtr->StartRT;}
    virtual double getEndTime() const {return dacFunctionInfoPtr->EndRT;}

    virtual void getTIC(automation_vector<float>& times, automation_vector<float>& intensities) const
    {
        IDACChromatogramPtr dacChromatogramPtr(CLSID_DACChromatogram);
        dacChromatogramPtr->GetChromatogram(rawpath,
                                            number,
                                            0,
                                            dacFunctionInfoPtr->StartRT,
                                            dacFunctionInfoPtr->EndRT,
                                            1,
                                            "TIC");

        VARIANT v = dacChromatogramPtr->Times.Detach();
        times.attach(v);
        v = dacChromatogramPtr->Intensities.Detach();
        intensities.attach(v);
    }

    private:
    const bstr_t& rawpath;
    short number;
    IDACFunctionInfoPtr dacFunctionInfoPtr;

    mutable PwizFunctionType type;
    mutable automation_vector<float> mrmParents;
    mutable automation_vector<float> mrmDaughters;
};

class RawDataImpl : public RawData
{
    public:
    RawDataImpl(const string& rawpath)
    :   rawpath_(rawpath.c_str())
    {
        COMInitializer::initialize();
        dacHeaderPtr = IDACHeaderPtr(CLSID_DACHeader);
        dacHeaderPtr->GetHeader(rawpath_);
    }

    virtual ~RawDataImpl()
    {
        dacHeaderPtr.Release();
        COMInitializer::uninitialize();
    }

    virtual int getVersionMajor() const {return dacHeaderPtr->VersionMajor;}
    virtual int getVersionMinor() const {return dacHeaderPtr->VersionMinor;}

    virtual std::string getAcquisitionName() const {return (const char*) dacHeaderPtr->AcquName;}
    virtual std::string getAcquisitionDate() const {return (const char*) dacHeaderPtr->AcquDate;}
    virtual std::string getAcquisitionTime() const {return (const char*) dacHeaderPtr->AcquTime;}

    virtual InstrumentType getInstrument() const {return InstrumentType_Xevo;}

    /// returns an array of FunctionPtrs, but each function is instantiated on-demand;
    /// some of the FunctionPtrs may be null (if the corresponding _FUNC0xx.DAT is missing)
    virtual const FunctionList& functions() const
    {
        if (functions_.empty())
        {
            // Count the number of _FUNC[0-9]{3}.DAT files, starting with _FUNC001.DAT
            string functionPathmask = (const char*) rawpath_;
            functionPathmask += "/_FUNC???.DAT";
            vector<bfs::path> functionFilepaths;
            expand_pathmask(functionPathmask, functionFilepaths);
            //sort(functionFilepaths.begin(), functionFilepaths.end());
            functions_.resize(functionFilepaths.size());
            for (size_t i=0; i < functionFilepaths.size(); ++i)
            {
                short number = lexical_cast<short>(functionFilepaths[i].filename().substr(5, 3));
                functions_[i].reset(new FunctionImpl(rawpath_, number));
            }
        }
        return functions_;
    }

    //virtual ScanPtr getScan(int function, int process, int scan) const = 0;

    private:
    bstr_t rawpath_;
    IDACHeaderPtr dacHeaderPtr;

    mutable FunctionList functions_;
};


PWIZ_API_DECL RawDataPtr RawData::create(const string& rawpath)
{
    return RawDataPtr(new RawDataImpl(rawpath));
}


} // namespace Waters
} // namespace vendor_api
} // namespace pwiz
