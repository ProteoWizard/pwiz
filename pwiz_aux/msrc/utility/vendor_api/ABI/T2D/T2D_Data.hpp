//
// $Id$
//
// 
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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


#ifndef _T2D_DATA_HPP_
#define _T2D_DATA_HPP_


#ifndef BOOST_DATE_TIME_NO_LIB
#define BOOST_DATE_TIME_NO_LIB // prevent MSVC auto-link
#endif

#ifdef __cplusplus_cli
// "boost/filesystem/path.hpp" uses "generic" as an identifier which is a reserved word in C++/CLI
#define generic __identifier(generic)
#endif

#include "pwiz/utility/misc/Export.hpp"
#include <string>
#include <vector>
#include <boost/shared_ptr.hpp>
#include <boost/date_time.hpp>
#include <boost/filesystem/path.hpp>


namespace pwiz {
namespace vendor_api {
namespace ABI {
namespace T2D {


enum PWIZ_API_DECL InstrumentStringParam
{
    InstrumentStringParam_SampleWell = 0,
    InstrumentStringParam_PlateID = 1,
    InstrumentStringParam_InstrumentName = 2,
    InstrumentStringParam_SerialNumber = 3,
    InstrumentStringParam_PlateTypeFilename = 4,
    InstrumentStringParam_LabName = 5
};

enum PWIZ_API_DECL InstrumentSetting
{
    InstrumentSetting_NozzlePotential = 0,
    InstrumentSetting_MinimumAnalyzerMass = 1,
    InstrumentSetting_MaximumAnalyzerMass = 2,
    InstrumentSetting_Skimmer1Potential = 3,
    InstrumentSetting_SpectrumXPosAbs = 4,
    InstrumentSetting_SpectrumYPosAbs = 5,
    InstrumentSetting_SpectrumXPosRel = 6,
    InstrumentSetting_SpectrumYPosRel = 7,
    InstrumentSetting_PulsesAccepted = 8,
    InstrumentSetting_DigitizerStartTime = 9,
    InstrumentSetting_DigitzerBinSize = 10,
    InstrumentSetting_SourcePressure = 11,
    InstrumentSetting_MirrorPressure = 12,
    InstrumentSetting_TC2Pressure = 13,
    InstrumentSetting_PreCursorIon = 14
};

enum PWIZ_API_DECL IonMode
{
    IonMode_Unknown = -1,
    IonMode_Off = 0,
    IonMode_Positive = 1,
    IonMode_Negative = 2
};

enum PWIZ_API_DECL SpectrumType
{
    SpectrumType_Unknown = 0,
    SpectrumType_Linear = 1,
    SpectrumType_Reflector = 2,
    SpectrumType_PSD = 3,
    SpectrumType_MSMS = 4
};


struct PWIZ_API_DECL Spectrum
{
    virtual SpectrumType getType() const = 0;
    virtual int getMsLevel() const = 0;
    virtual IonMode getPolarity() const = 0;

    virtual size_t getPeakDataSize() const = 0;
    virtual void getPeakData(std::vector<double>& mz, std::vector<double>& intensities) const = 0;

    virtual size_t getRawDataSize() const = 0;
    virtual void getRawData(std::vector<double>& mz, std::vector<double>& intensities) const = 0;

    virtual double getTIC() const = 0;
    virtual void getBasePeak(double& mz, double& intensity) const = 0;

    virtual std::string getInstrumentStringParam(InstrumentStringParam param) const = 0;
    virtual double getInstrumentSetting(InstrumentSetting setting) const = 0;

    virtual ~Spectrum() {}
};

typedef boost::shared_ptr<Spectrum> SpectrumPtr;


class PWIZ_API_DECL Data
{
    public:
    typedef boost::shared_ptr<Data> Ptr;
    static Ptr create(const std::string& datapath);

    virtual size_t getSpectrumCount() const = 0;
    virtual SpectrumPtr getSpectrum(size_t index) const = 0;

    virtual const std::vector<boost::filesystem::path>& getSpectrumFilenames() const = 0;

    virtual boost::local_time::local_date_time getSampleAcquisitionTime() const = 0;

    virtual ~Data() {}
};

typedef Data::Ptr DataPtr;


} // T2D
} // ABI
} // vendor_api
} // pwiz


#endif // _T2D_DATA_HPP_
