//
// $Id$
//
// 
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#pragma unmanaged
#include "CompassData.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/String.hpp"


#pragma managed
#include "pwiz/utility/misc/cpp_cli_utilities.hpp"
using namespace pwiz::util;


using System::String;
using System::Object;
using System::IntPtr;
using System::Runtime::InteropServices::Marshal;

typedef EDAL::MSAnalysis MS_Analysis;
typedef EDAL::MSSpectrumCollection MS_SpectrumCollection;

typedef BDal::CxT::Lc::IAnalysis LC_Analysis;
typedef BDal::CxT::Lc::ISpectrumSourceDeclaration LC_SpectrumSourceDeclaration;
typedef BDal::CxT::Lc::ITraceDeclaration LC_TraceDeclaration;

typedef automation_vector<LC_SpectrumSourceDeclaration> LC_SpectrumSourceDeclarationList;
typedef automation_vector<LC_TraceDeclaration> LC_TraceDeclarationList;


namespace pwiz {
namespace vendor_api {
namespace Bruker {


struct MSSpectrumImpl : public MSSpectrum
{
    MSSpectrumImpl(EDAL::MSSpectrum^ spectrum) : spectrum_(spectrum)
    {
        try
        {
            System::Object^ massArray, ^intensityArray;
            spectrum->GetMassIntensityValues((EDAL::SpectrumTypes) SpectrumType_Line, massArray, intensityArray);
            lineDataSize_ = ((cli::array<double>^) massArray)->Length;

            spectrum->GetMassIntensityValues((EDAL::SpectrumTypes) SpectrumType_Profile, massArray, intensityArray);
            profileDataSize_ = ((cli::array<double>^) massArray)->Length;
        }
        CATCH_AND_FORWARD
    }

    virtual bool hasLineData() const {return lineDataSize_ > 0;}
    virtual bool hasProfileData() const {return profileDataSize_ > 0;}

    virtual size_t getLineDataSize() const {return lineDataSize_;}
    virtual size_t getProfileDataSize() const {return profileDataSize_;}

    virtual void getLineData(automation_vector<double>& mz, automation_vector<double>& intensities) const
    {
        if (!hasLineData())
        {
            mz.clear();
            intensities.clear();
            return;
        }

        try
        {
            // we always get a copy of the arrays because they can be modified by the client
            System::Object^ massArray, ^intensityArray;
            spectrum_->GetMassIntensityValues((EDAL::SpectrumTypes) SpectrumType_Line, massArray, intensityArray);
            ToAutomationVector((cli::array<double>^) massArray, mz);
            ToAutomationVector((cli::array<double>^) intensityArray, intensities);
        }
        CATCH_AND_FORWARD
    }

    virtual void getProfileData(automation_vector<double>& mz, automation_vector<double>& intensities) const
    {
        if (!hasProfileData())
        {
            mz.clear();
            intensities.clear();
            return;
        }

        try
        {
            // we always get a copy of the arrays because they can be modified by the client
            System::Object^ massArray, ^intensityArray;
            spectrum_->GetMassIntensityValues((EDAL::SpectrumTypes) SpectrumType_Profile, massArray, intensityArray);
            ToAutomationVector((cli::array<double>^) massArray, mz);
            ToAutomationVector((cli::array<double>^) intensityArray, intensities);
        }
        CATCH_AND_FORWARD
    }

    virtual int getMSMSStage() {try {return (int) spectrum_->MSMSStage;} CATCH_AND_FORWARD}

    //IMSSpectrumParameterCollectionPtr GetMSSpectrumParameterCollection ( );

    virtual double getRetentionTime() {try {return spectrum_->RetentionTime;} CATCH_AND_FORWARD}

    virtual void getIsolationData(std::vector<double>& isolatedMZs,
                                  std::vector<IsolationMode>& isolationModes)
    {
        try
        {
            System::Object^ mzArrayObject;
            System::Array^ modeArray;
            spectrum_->GetIsolationData(mzArrayObject, modeArray);
            cli::array<double,2>^ mzArray = (cli::array<double,2>^) mzArrayObject;
            isolatedMZs.resize(mzArray->Length);
            for (int i=0; i < mzArray->Length; ++i)
                isolatedMZs[i] = mzArray[i,0];
            ToStdVector((cli::array<EDAL::IsolationModes>^) modeArray, isolationModes);
        }
        CATCH_AND_FORWARD
    }

    virtual void getFragmentationData(std::vector<double>& fragmentedMZs,
                                      std::vector<FragmentationMode>& fragmentationModes)
    {
        try
        {
            System::Object^ mzArrayObject;
            System::Array^ modeArray;
            spectrum_->GetFragmentationData(mzArrayObject, modeArray);
            cli::array<double,2>^ mzArray = (cli::array<double,2>^) mzArrayObject;
            fragmentedMZs.resize(mzArray->Length);
            for (int i=0; i < mzArray->Length; ++i)
                fragmentedMZs[i] = mzArray[i,0];
            ToStdVector((cli::array<EDAL::FragmentationModes>^) modeArray, fragmentationModes);
        }
        CATCH_AND_FORWARD
    }

    virtual IonPolarity getPolarity() {try {return (IonPolarity) spectrum_->Polarity;} CATCH_AND_FORWARD}

    private:
    gcroot<EDAL::MSSpectrum^> spectrum_;
    size_t lineDataSize_, profileDataSize_;
};


struct CompassDataImpl : public CompassData
{
    CompassDataImpl(const string& rawpath)
    {
        try
        {
            msAnalysis_ = gcnew EDAL::MSAnalysisClass();
            msAnalysis_->Open(ToSystemString(rawpath));
            msSpectrumCollection_ = msAnalysis_->MSSpectrumCollection;
            hasMSData_ = msSpectrumCollection_->Count > 0;
        }
        CATCH_AND_FORWARD
    }

    virtual bool hasMSData() {return hasMSData_;}
    virtual bool hasLCData() {return false;}

    virtual size_t getMSSpectrumCount()
    {
        if (!hasMSData_) return 0;
        try {return msSpectrumCollection_->Count;} CATCH_AND_FORWARD
    }

    virtual MSSpectrumPtr getMSSpectrum(int scan)
    {
        if (scan < 1 || scan > (int) getMSSpectrumCount())
            throw std::out_of_range("[CompassData::getMSSpectrum] Scan number " + lexical_cast<string>(scan) + " is out of range.");

        try {return MSSpectrumPtr(new MSSpectrumImpl(msSpectrumCollection_->default[scan]));} CATCH_AND_FORWARD
    }

    /*virtual size_t getLCSpectrumCount()
    {
        return 0;
    }

    virtual LCSpectrumPtr getLCSpectrum(int declaration, int collection, int scan)
    {
        return LCSpectrumPtr();
    }*/

    virtual std::string getOperatorName()
    {
        if (!hasMSData_) return "";
        try {return ToStdString(msAnalysis_->OperatorName);} CATCH_AND_FORWARD
    }

    virtual std::string getAnalysisName()
    {
        if (!hasMSData_) return "";
        try {return ToStdString(msAnalysis_->AnalysisName);} CATCH_AND_FORWARD
    }

    virtual boost::local_time::local_date_time getAnalysisDateTime()
    {
        using bpt::ptime;
        using blt::local_date_time;
        if (!hasMSData_) return local_date_time(bdt::not_a_date_time);

        try
        {
            ptime pt(bdt::time_from_OADATE<ptime>(msAnalysis_->AnalysisDateTime.ToOADate()));
            return local_date_time(pt, blt::time_zone_ptr());
        }
        CATCH_AND_FORWARD
    }

    virtual std::string getSampleName()
    {
        if (!hasMSData_) return "";
        try {return ToStdString(msAnalysis_->SampleName);} CATCH_AND_FORWARD
    }

    virtual std::string getMethodName()
    {
        if (!hasMSData_) return "";
        try {return ToStdString(msAnalysis_->MethodName);} CATCH_AND_FORWARD
    }

    virtual InstrumentFamily getInstrumentFamily()
    {
        if (!hasMSData_) return InstrumentFamily_Unknown;
        try {return (InstrumentFamily) msAnalysis_->InstrumentFamily;} CATCH_AND_FORWARD
    }

    virtual std::string getInstrumentDescription()
    {
        if (!hasMSData_) return "";
        try {return ToStdString(msAnalysis_->InstrumentDescription);} CATCH_AND_FORWARD
    }

    private:
    bool hasMSData_;
    gcroot<MS_Analysis^> msAnalysis_;
    gcroot<MS_SpectrumCollection^> msSpectrumCollection_;
};


PWIZ_API_DECL CompassDataPtr CompassData::create(const string& rawpath)
{
    return CompassDataPtr(new CompassDataImpl(rawpath));
}

} // namespace Bruker
} // namespace vendor_api
} // namespace pwiz
