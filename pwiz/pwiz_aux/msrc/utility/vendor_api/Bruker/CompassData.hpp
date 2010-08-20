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


#ifndef _COMPASSDATA_HPP_
#define _COMPASSDATA_HPP_


#ifndef BOOST_DATE_TIME_NO_LIB
#define BOOST_DATE_TIME_NO_LIB // prevent MSVC auto-link
#endif


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/automation_vector.h"
#include <string>
#include <vector>
#include <boost/shared_ptr.hpp>
#include <boost/date_time.hpp>


namespace pwiz {
namespace vendor_api {
namespace Bruker {


PWIZ_API_DECL enum SpectrumType
{
    SpectrumType_Line = 0,
    SpectrumType_Profile = 1
};

PWIZ_API_DECL enum IonPolarity
{
    IonPolarity_Positive = 0,
    IonPolarity_Negative = 1,
    IonPolarity_Unknown = 255
};

PWIZ_API_DECL enum FragmentationMode
{
    FragmentationMode_Off = 0,
    FragmentationMode_CID = 1,
    FragmentationMode_ETD = 2,
    FragmentationMode_CIDETD_CID = 3,
    FragmentationMode_CIDETD_ETD = 4,
    FragmentationMode_ISCID = 5,
    FragmentationMode_ECD = 6,
    FragmentationMode_IRMPD = 7,
    FragmentationMode_PTR = 8,
    FragmentationMode_Unknown = 255
};

PWIZ_API_DECL enum InstrumentFamily
{
    InstrumentFamily_Trap = 0,
    InstrumentFamily_OTOF = 1,
    InstrumentFamily_OTOFQ = 2,
    InstrumentFamily_BioTOF = 3,
    InstrumentFamily_BioTOFQ = 4,
    InstrumentFamily_MaldiTOF = 5,
    InstrumentFamily_FTMS = 6,
    InstrumentFamily_Unknown = 255
};

PWIZ_API_DECL enum IsolationMode
{
    IsolationMode_Off = 0,
    IsolationMode_On = 1,
    IsolationMode_Unknown = 255
};


struct PWIZ_API_DECL MSSpectrum
{
    virtual bool hasLineData() const = 0;
    virtual bool hasProfileData() const = 0;
    virtual size_t getLineDataSize() const = 0;
    virtual size_t getProfileDataSize() const = 0;
    virtual void getLineData(automation_vector<double>& mz, automation_vector<double>& intensities) const = 0;
    virtual void getProfileData(automation_vector<double>& mz, automation_vector<double>& intensities) const = 0;

    virtual int getMSMSStage() = 0;
    //IMSSpectrumParameterCollectionPtr GetMSSpectrumParameterCollection ( );
    virtual double getRetentionTime() = 0;
    virtual void getIsolationData(std::vector<double>& isolatedMZs,
                                  std::vector<IsolationMode>& isolationModes) = 0;
    virtual void getFragmentationData(std::vector<double>& fragmentedMZs,
                                      std::vector<FragmentationMode>& fragmentationModes) = 0;
    virtual IonPolarity getPolarity() = 0;
};

typedef boost::shared_ptr<MSSpectrum> MSSpectrumPtr;


/*struct PWIZ_API_DECL LCSpectrum
{
    virtual bool hasLineData() const = 0;
    virtual bool hasProfileData() const = 0;
    virtual size_t getLineDataSize() const = 0;
    virtual size_t getProfileDataSize() const = 0;
    virtual void getLineData(automation_vector<double>& mz, automation_vector<double>& intensities) const = 0;
    virtual void getProfileData(automation_vector<double>& mz, automation_vector<double>& intensities) const = 0;

    virtual long getMSMSStage() = 0;
    //IMSSpectrumParameterCollectionPtr GetMSSpectrumParameterCollection ( );
    virtual double getRetentionTime() = 0;
    //virtual long GetIsolationData() = 0;
    //virtual long GetFragmentationData() = 0;
    virtual IonPolarity getPolarity() = 0;
};*/

typedef boost::shared_ptr<MSSpectrum> MSSpectrumPtr;


struct PWIZ_API_DECL CompassData
{
    typedef boost::shared_ptr<CompassData> Ptr;
    static Ptr create(const std::string& rawpath);

    /// returns true if the source has MS spectra
    virtual bool hasMSData() = 0;

    /// returns true if the source has LC spectra or traces
    virtual bool hasLCData() = 0;

    /// returns the number of spectra available from the MS source
    virtual size_t getMSSpectrumCount() = 0;

    /// returns the number of spectra available from the LC source
    //virtual size_t getLCSpectrumCount() = 0;

    /// returns a spectrum from the MS source
    virtual MSSpectrumPtr getMSSpectrum(int scan) = 0;

    /// returns a spectrum from the LC source
    //virtual LCSpectrumPtr getLCSpectrum(int declaration, int collection, int scan) = 0;

    virtual std::string getOperatorName() = 0;
    virtual std::string getAnalysisName() = 0;
    virtual boost::local_time::local_date_time getAnalysisDateTime() = 0;
    virtual std::string getSampleName() = 0;
    virtual std::string getMethodName() = 0;
    virtual InstrumentFamily getInstrumentFamily() = 0;
    virtual std::string getInstrumentDescription() = 0;
};

typedef CompassData::Ptr CompassDataPtr;


} // namespace Bruker
} // namespace vendor_api
} // namespace pwiz


#endif // _COMPASSDATA_HPP_
