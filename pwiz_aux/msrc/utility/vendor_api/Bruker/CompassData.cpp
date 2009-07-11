#define PWIZ_SOURCE

#pragma unmanaged
#include "CompassData.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/String.hpp"


#pragma managed
#include <gcroot.h>

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


#include <vcclr.h>
namespace {

std::string ToStdString(System::String^ source)
{
	int len = (( source->Length+1) * 2);
	char *ch = new char[ len ];
	bool result ;
	{
		pin_ptr<const wchar_t> wch = PtrToStringChars( source );
		result = wcstombs( ch, wch, len ) != -1;
	}
	std::string target = ch;
	delete ch;
	if(!result)
        throw gcnew System::Exception("error converting System::String to std::string");
	return target;
}

System::String^ ToSystemString(const std::string& source)
{
    return gcnew System::String(source.c_str());
}

template<typename managed_value_type, typename native_value_type>
void ToStdVector(cli::array<managed_value_type>^ managedArray, std::vector<native_value_type>& stdVector)
{
    stdVector.resize(managedArray->Length);
    for (int i=0; i < managedArray->Length; ++i)
        stdVector[i] = static_cast<native_value_type>(managedArray[i]);
}

template<typename managed_value_type, typename native_value_type>
void ToAutomationVector(cli::array<managed_value_type>^ managedArray, automation_vector<native_value_type>& automationArray)
{
    VARIANT v;
    ::VariantInit(&v);
    IntPtr vPtr = (IntPtr) &v;
    Marshal::GetNativeVariantForObject((Object^) managedArray, vPtr);
    automationArray.attach(v);
}

} // namespace


namespace pwiz {
namespace vendor_api {
namespace Bruker {


struct MSSpectrumImpl : public MSSpectrum
{
    MSSpectrumImpl(EDAL::MSSpectrum^ spectrum) : spectrum_(spectrum)
    {
        System::Object^ massArray, ^intensityArray;
        spectrum->GetMassIntensityValues((EDAL::SpectrumTypes) SpectrumType_Line, massArray, intensityArray);
        lineDataSize_ = ((cli::array<double>^) massArray)->Length;

        spectrum->GetMassIntensityValues((EDAL::SpectrumTypes) SpectrumType_Profile, massArray, intensityArray);
        profileDataSize_ = ((cli::array<double>^) massArray)->Length;
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

        // we always get a copy of the arrays because they can be modified by the client
        System::Object^ massArray, ^intensityArray;
        spectrum_->GetMassIntensityValues((EDAL::SpectrumTypes) SpectrumType_Line, massArray, intensityArray);
        ToAutomationVector((cli::array<double>^) massArray, mz);
        ToAutomationVector((cli::array<double>^) intensityArray, intensities);
    }

    virtual void getProfileData(automation_vector<double>& mz, automation_vector<double>& intensities) const
    {
        if (!hasProfileData())
        {
            mz.clear();
            intensities.clear();
            return;
        }

        // we always get a copy of the arrays because they can be modified by the client
        System::Object^ massArray, ^intensityArray;
        spectrum_->GetMassIntensityValues((EDAL::SpectrumTypes) SpectrumType_Profile, massArray, intensityArray);
        ToAutomationVector((cli::array<double>^) massArray, mz);
        ToAutomationVector((cli::array<double>^) intensityArray, intensities);
    }

    virtual int getMSMSStage() {return (int) spectrum_->MSMSStage;}

    //IMSSpectrumParameterCollectionPtr GetMSSpectrumParameterCollection ( );

    virtual double getRetentionTime() {return spectrum_->RetentionTime;}

    virtual void getIsolationData(std::vector<double>& isolatedMZs,
                                  std::vector<IsolationMode>& isolationModes)
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

    virtual void getFragmentationData(std::vector<double>& fragmentedMZs,
                                      std::vector<FragmentationMode>& fragmentationModes)
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

    virtual IonPolarity getPolarity() {return (IonPolarity) spectrum_->Polarity;}

    private:
    gcroot<EDAL::MSSpectrum^> spectrum_;
    size_t lineDataSize_, profileDataSize_;
};


struct CompassDataImpl : public CompassData
{
    CompassDataImpl(const string& rawpath)
    {
        msAnalysis_ = gcnew EDAL::MSAnalysisClass();
        msAnalysis_->Open(ToSystemString(rawpath));
        msSpectrumCollection_ = msAnalysis_->MSSpectrumCollection;
        hasMSData_ = msSpectrumCollection_->Count > 0;
    }

    virtual bool hasMSData() {return hasMSData_;}
    virtual bool hasLCData() {return false;}

    virtual size_t getMSSpectrumCount()
    {
        if (!hasMSData_) return 0;
        return msSpectrumCollection_->Count;
    }

    virtual MSSpectrumPtr getMSSpectrum(int scan)
    {
        if (scan < 1 || scan > (int) getMSSpectrumCount())
            throw std::out_of_range("[CompassData::getMSSpectrum] Scan number " + lexical_cast<string>(scan) + " is out of range.");

        return MSSpectrumPtr(new MSSpectrumImpl(msSpectrumCollection_->default[scan]));
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
        return ToStdString(msAnalysis_->OperatorName);
    }

    virtual std::string getAnalysisName()
    {
        if (!hasMSData_) return "";
        return ToStdString(msAnalysis_->AnalysisName);
    }

    virtual boost::local_time::local_date_time getAnalysisDateTime()
    {
        using bpt::ptime;
        using blt::local_date_time;
        if (!hasMSData_) return local_date_time(bdt::not_a_date_time);
        ptime pt(bdt::time_from_OADATE<ptime>(msAnalysis_->AnalysisDateTime.ToUniversalTime().ToOADate()));
        return local_date_time(pt, blt::time_zone_ptr());
    }

    virtual std::string getSampleName()
    {
        if (!hasMSData_) return "";
        return ToStdString(msAnalysis_->SampleName);
    }

    virtual std::string getMethodName()
    {
        if (!hasMSData_) return "";
        return ToStdString(msAnalysis_->MethodName);
    }

    virtual InstrumentFamily getInstrumentFamily()
    {
        if (!hasMSData_) return InstrumentFamily_Unknown;
        return (InstrumentFamily) msAnalysis_->InstrumentFamily;
    }

    virtual std::string getInstrumentDescription()
    {
        if (!hasMSData_) return "";
        return ToStdString(msAnalysis_->InstrumentDescription);
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
