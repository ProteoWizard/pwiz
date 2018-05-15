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
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "CompassData.hpp"
#include "Baf2Sql.hpp"

#ifdef _WIN64
#include "TimsData.hpp"
#endif

#pragma managed
#include "pwiz/utility/misc/cpp_cli_utilities.hpp"
using namespace pwiz::util;
using namespace pwiz::msdata::detail::Bruker;


namespace pwiz {
namespace vendor_api {
namespace Bruker {

    
const char* parameterAlternativeNames[] =
{
    "IsolationWidth:MS(n) Isol Width;Isolation Resolution FWHM",
    "ChargeState:Trigger Charge MS(2);Trigger Charge MS(3);Trigger Charge MS(4);Trigger Charge MS(5);Precursor Charge State"
};

size_t parameterAlternativeNamesSize = sizeof(parameterAlternativeNames) / sizeof(const char*);


struct ParameterCache
{
    string get(const string& parameterName, MSSpectrumParameterList& parameters);
    size_t size() { return parameterIndexByName_.size(); }

    private:
    void update(MSSpectrumParameterList& parameters);
    map<string, size_t> parameterIndexByName_;
    map<string, string> parameterAlternativeNameMap_;
};


PWIZ_API_DECL
string ParameterCache::get(const string& parameterName, MSSpectrumParameterList& parameters)
{
    map<string, size_t>::const_iterator findItr = parameterIndexByName_.find(parameterName);

    if (findItr == parameterIndexByName_.end())
    {
        update(parameters);

        // if still not found, return empty string
        findItr = parameterIndexByName_.find(parameterName);
        if (findItr == parameterIndexByName_.end())
            return string();
    }

    const MSSpectrumParameter& parameter = parameters[findItr->second];
    map<string, string>::const_iterator alternativeNameItr = parameterAlternativeNameMap_.find(parameter.name);

    if (parameter.name != parameterName && alternativeNameItr == parameterAlternativeNameMap_.end())
    {
        // if parameter name doesn't match, invalidate the cache and try again
        update(parameters);
        return get(parameterName, parameters);
    }

    return parameter.value;
}

PWIZ_API_DECL
void ParameterCache::update(MSSpectrumParameterList& parameters)
{
    parameterIndexByName_.clear();
    parameterAlternativeNameMap_.clear();

    vector<string> tokens;
    for (size_t i=0; i < parameterAlternativeNamesSize; ++i)
    {
        bal::split(tokens, parameterAlternativeNames[i], bal::is_any_of(":;"));
        for (size_t j=1; j < tokens.size(); ++j)
            parameterAlternativeNameMap_[tokens[j]] = tokens[0];
    }

    size_t i = 0;
    for(const MSSpectrumParameter& p : parameters)
    {
        map<string, string>::const_iterator findItr = parameterAlternativeNameMap_.find(p.name);
        if (findItr != parameterAlternativeNameMap_.end())
        {   
            //cout << p.name << ": " << p.value << "\n";
            parameterIndexByName_[findItr->second] = i;
        }
        else
            parameterIndexByName_[p.name] = i;

        ++i;
    }
}


#ifdef PWIZ_READER_BRUKER_WITH_COMPASSXTRACT

using System::String;
using System::Object;
using System::IntPtr;
using System::Runtime::InteropServices::Marshal;

typedef EDAL::MSAnalysis MS_Analysis;
typedef EDAL::MSSpectrumCollection MS_SpectrumCollection;

typedef BDal::CxT::Lc::IAnalysis LC_Analysis;
typedef BDal::CxT::Lc::ISpectrumSourceDeclaration LC_SpectrumSourceDeclaration;
typedef BDal::CxT::Lc::ITraceDeclaration LC_TraceDeclaration;

struct MSSpectrumParameterListImpl : public MSSpectrumParameterList
{
    MSSpectrumParameterListImpl(EDAL::MSSpectrumParameterCollection^ parameterCollection)
        : parameterCollection_(parameterCollection)
    {
    }

    virtual size_t size() const {return parameterCollection_->Count;}
    virtual value_type operator[] (size_t index) const {return *const_iterator(*this, index);}
    virtual const_iterator begin() const {return const_iterator(*this);}
    virtual const_iterator end() const {return const_iterator();}

    gcroot<EDAL::MSSpectrumParameterCollection^> parameterCollection_;
};


struct MSSpectrumParameterIterator::Impl
{
    Impl(EDAL::MSSpectrumParameterCollection^ parameterCollection, int index)
    {
        parameterCollection_ = parameterCollection;
        index_ = index+1;
        set();
    }

    Impl(const MSSpectrumParameterIterator::Impl& p)
        : parameterCollection_(p.parameterCollection_),
          parameter_(p.parameter_),
          index_(p.index_),
          cur_(p.cur_)
    {}

    void increment() {++index_; set();}
    void decrement() {--index_; set();}
    void advance(difference_type n) {index_ += n; set();}

    gcroot<EDAL::MSSpectrumParameterCollection^> parameterCollection_;
    gcroot<EDAL::MSSpectrumParameter^> parameter_;
    int index_; // index is one-based
    MSSpectrumParameter cur_;

    private:
    void set()
    {
        if (index_ < 1 || index_ > parameterCollection_->Count) return;
        parameter_ = parameterCollection_->default[index_];
        cur_.group = ToStdString(parameter_->GroupName);
        cur_.name = ToStdString(parameter_->ParameterName);
        cur_.value = ToStdString(parameter_->ParameterValue->ToString());
    }
};

PWIZ_API_DECL MSSpectrumParameterIterator::MSSpectrumParameterIterator() {}

PWIZ_API_DECL MSSpectrumParameterIterator::MSSpectrumParameterIterator(const MSSpectrumParameterList& pl, size_t index)
{
    const MSSpectrumParameterListImpl* plImpl = dynamic_cast<const MSSpectrumParameterListImpl*>(&pl);
    if (!plImpl)
        throw std::runtime_error("[MSSpectrumParameterIterator] invalid MSSpectrumParameterList subclass");
    try {impl_.reset(new Impl(plImpl->parameterCollection_, index));} CATCH_AND_FORWARD
}

PWIZ_API_DECL MSSpectrumParameterIterator::MSSpectrumParameterIterator(const MSSpectrumParameterIterator& other)
    : impl_(other.impl_.get() ? new Impl(*other.impl_) : 0)
{}

PWIZ_API_DECL MSSpectrumParameterIterator::~MSSpectrumParameterIterator() {}

PWIZ_API_DECL void MSSpectrumParameterIterator::increment()
{
    if (!impl_.get()) return;
    impl_->increment();
}

PWIZ_API_DECL void MSSpectrumParameterIterator::decrement()
{
    if (!impl_.get()) return;
    impl_->decrement();
}
PWIZ_API_DECL void MSSpectrumParameterIterator::advance(difference_type n)
{
    if (!impl_.get()) return;
    impl_->advance(n);
}

PWIZ_API_DECL bool MSSpectrumParameterIterator::equal(const MSSpectrumParameterIterator& that) const
{
    bool gotThis = this->impl_.get() != NULL;
    bool gotThat = that.impl_.get() != NULL;

    if (gotThis && gotThat)
        return System::Object::ReferenceEquals(this->impl_->parameterCollection_, that.impl_->parameterCollection_) &&
               this->impl_->index_ == that.impl_->index_;
    else if (!gotThis && !gotThat) // end() == end()
        return true;
    else if (gotThis)
        return this->impl_->index_ >= this->impl_->parameterCollection_->Count;
    else // gotThat
        return that.impl_->index_ >= that.impl_->parameterCollection_->Count;
}

PWIZ_API_DECL const MSSpectrumParameter& MSSpectrumParameterIterator::dereference() const
{
    return impl_->cur_;
}


struct MSSpectrumImpl : public MSSpectrum
{
    MSSpectrumImpl(EDAL::IMSSpectrum^ spectrum, const shared_ptr<map<int, ParameterCache> >& parameterCacheByMsLevel, DetailLevel detailLevel)
        : spectrum_(spectrum), lineDataSize_(0), profileDataSize_(0), parameterCacheByMsLevel_(parameterCacheByMsLevel)
    {
        if (detailLevel == DetailLevel_InstantMetadata)
            return;

        try
        {
            System::Object^ massArray, ^intensityArray;
            spectrum->GetMassIntensityValues((EDAL::SpectrumTypes) SpectrumType_Line, massArray, intensityArray);
            lineDataSize_ = ((cli::array<double>^) massArray)->Length;

            if (detailLevel == DetailLevel_FullData)
            {
                lineMzArray_ = (cli::array<double>^) massArray;
                lineIntensityArray_ = (cli::array<double>^) intensityArray;
            }

            spectrum->GetMassIntensityValues((EDAL::SpectrumTypes) SpectrumType_Profile, massArray, intensityArray);
            profileDataSize_ = ((cli::array<double>^) massArray)->Length;

            if (detailLevel == DetailLevel_FullData)
            {
                profileMzArray_ = (cli::array<double>^) massArray;
                profileIntensityArray_ = (cli::array<double>^) intensityArray;
            }
        }
        CATCH_AND_FORWARD
    }

    virtual ~MSSpectrumImpl() {}

    virtual bool hasLineData() const {return lineDataSize_ > 0;}
    virtual bool hasProfileData() const {return profileDataSize_ > 0;}

    virtual size_t getLineDataSize() const {return lineDataSize_;}
    virtual size_t getProfileDataSize() const {return profileDataSize_;}

    virtual void getLineData(automation_vector<double>& mz, automation_vector<double>& intensities) const
    {
        try
        {
            System::Object^ massArray, ^intensityArray;

            if ((System::Object^) lineMzArray_ == nullptr)
            {
                spectrum_->GetMassIntensityValues((EDAL::SpectrumTypes) SpectrumType_Line, massArray, intensityArray);
                lineMzArray_ = (cli::array<double>^) massArray;
                lineIntensityArray_ = (cli::array<double>^) intensityArray;
                lineDataSize_ = lineMzArray_->Length;
            }

            if (!hasLineData())
            {
                mz.clear();
                intensities.clear();
                return;
            }

            // we always get a copy of the arrays because they can be modified by the client
            ToAutomationVector((cli::array<double>^) lineMzArray_, mz);
            ToAutomationVector((cli::array<double>^) lineIntensityArray_, intensities);

            // the automation vectors now own the arrays, so nullify the cached versions
            lineMzArray_ = nullptr;
            lineIntensityArray_ = nullptr;
        }
        CATCH_AND_FORWARD
    }

    virtual void getProfileData(automation_vector<double>& mz, automation_vector<double>& intensities) const
    {
        try
        {
            System::Object^ massArray, ^intensityArray;

            if ((System::Object^) profileMzArray_ == nullptr)
            {
                spectrum_->GetMassIntensityValues((EDAL::SpectrumTypes) SpectrumType_Profile, massArray, intensityArray);
                profileMzArray_ = (cli::array<double>^) massArray;
                profileIntensityArray_ = (cli::array<double>^) intensityArray;
                profileDataSize_ = profileMzArray_->Length;
            }

            if (!hasProfileData())
            {
                mz.clear();
                intensities.clear();
                return;
            }

            // we always get a copy of the arrays because they can be modified by the client
            ToAutomationVector((cli::array<double>^) profileMzArray_, mz);
            ToAutomationVector((cli::array<double>^) profileIntensityArray_, intensities);

            // the automation vectors now own the arrays, so nullify the cached versions
            profileMzArray_ = nullptr;
            profileIntensityArray_ = nullptr;
        }
        CATCH_AND_FORWARD
    }

    virtual double getTIC() const { return 0; }
    virtual double getBPI() const { return 0; }

    virtual int getMSMSStage() const {try {return (int) spectrum_->MSMSStage;} CATCH_AND_FORWARD}

    virtual double getRetentionTime() const {try {return spectrum_->RetentionTime;} CATCH_AND_FORWARD}

    virtual void getIsolationData(std::vector<double>& isolatedMZs,
                                  std::vector<IsolationMode>& isolationModes) const
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
                                      std::vector<FragmentationMode>& fragmentationModes) const
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

    virtual IonPolarity getPolarity() const {try {return (IonPolarity) spectrum_->Polarity;} CATCH_AND_FORWARD}

    virtual pair<double, double> getScanRange() const
    {
        // cache parameter indexes for this msLevel if they aren't already cached
        ParameterCache& parameterCache = (*parameterCacheByMsLevel_)[(int)spectrum_->MSMSStage];
        MSSpectrumParameterListImpl parameters(spectrum_->MSSpectrumParameterCollection);

        string scanBegin = parameterCache.get("Scan Begin", parameters);
        string scanEnd = parameterCache.get("Scan End", parameters);

        if (!scanBegin.empty() && !scanEnd.empty())
            return make_pair(lexical_cast<double>(scanBegin), lexical_cast<double>(scanEnd));
        return make_pair(0.0, 0.0);
    }

    virtual int getChargeState() const
    {
        // cache parameter indexes for this msLevel if they aren't already cached
        ParameterCache& parameterCache = (*parameterCacheByMsLevel_)[(int)spectrum_->MSMSStage];
        MSSpectrumParameterListImpl parameters(spectrum_->MSSpectrumParameterCollection);

        string chargeState = parameterCache.get("ChargeState", parameters);

        if (!chargeState.empty())
        {
            if (chargeState == "single") 1;
            else if (chargeState == "double") 2;
            else if (chargeState == "triple") 3;
            else if (chargeState == "quad") 4;
            else
            {
                try
                {
                    int charge = lexical_cast<int>(chargeState);
                    if (charge > 0)
                        return charge;
                }
                catch (bad_lexical_cast&) {}
            }
        }
        return 0;
    }

    virtual double getIsolationWidth() const
    {
        // cache parameter indexes for this msLevel if they aren't already cached
        ParameterCache& parameterCache = (*parameterCacheByMsLevel_)[(int)spectrum_->MSMSStage];
        MSSpectrumParameterListImpl parameters(spectrum_->MSSpectrumParameterCollection);

        string isolationWidthString = parameterCache.get("IsolationWidth", parameters);

        if (!isolationWidthString.empty())
            try
            {
                int isolationWidth = lexical_cast<double>(isolationWidthString);
                if (isolationWidth > 0.0)
                    return isolationWidth;
            }
            catch (bad_lexical_cast&) {}

        return 0.0;
    }

    virtual MSSpectrumParameterListPtr parameters() const
    {
        try
        {
            MSSpectrumParameterListPtr result(new MSSpectrumParameterListImpl(spectrum_->MSSpectrumParameterCollection));
            return result;
        }
        CATCH_AND_FORWARD
    }

    private:
    gcroot<EDAL::IMSSpectrum^> spectrum_;
    mutable size_t lineDataSize_, profileDataSize_;
    mutable gcroot<cli::array<double>^> lineMzArray_, lineIntensityArray_;
    mutable gcroot<cli::array<double>^> profileMzArray_, profileIntensityArray_;
    mutable shared_ptr<map<int, ParameterCache> > parameterCacheByMsLevel_;
};


struct LCSpectrumSourceImpl : public LCSpectrumSource
{
    LCSpectrumSourceImpl(BDal::CxT::Lc::ISpectrumSourceDeclaration^ ssd) : spectrumSourceDeclaration_(ssd) {}
    virtual ~LCSpectrumSourceImpl() {}

    virtual int getCollectionId() const {try {return spectrumSourceDeclaration_->SpectrumCollectionId;} CATCH_AND_FORWARD}
    virtual std::string getInstrument() const {try {return ToStdString(spectrumSourceDeclaration_->Instrument);} CATCH_AND_FORWARD}
    virtual std::string getInstrumentId() const {try {return ToStdString(spectrumSourceDeclaration_->InstrumentId);} CATCH_AND_FORWARD}
    virtual double getTimeOffset() const {try {return spectrumSourceDeclaration_->TimeOffset;} CATCH_AND_FORWARD}
    virtual void getXAxis(vector<double>& xAxis) const {try {ToStdVector((cli::array<double>^) spectrumSourceDeclaration_->XAxis, xAxis);} CATCH_AND_FORWARD}
    virtual LCUnit getXAxisUnit() const {try {return (LCUnit) spectrumSourceDeclaration_->XAxisUnit;} CATCH_AND_FORWARD}

    gcroot<BDal::CxT::Lc::ISpectrumSourceDeclaration^> spectrumSourceDeclaration_;
};

struct LCSpectrumImpl : public LCSpectrum
{
    LCSpectrumImpl(BDal::CxT::Lc::ISpectrum^ spectrum) : spectrum_(spectrum) {}
    virtual ~LCSpectrumImpl() {}

    virtual void getData(vector<double>& intensities) const {try {ToStdVector((cli::array<double>^) spectrum_->Intensity, intensities);} CATCH_AND_FORWARD}
    virtual double getTime() const {try {return spectrum_->Time;} CATCH_AND_FORWARD}

    gcroot<BDal::CxT::Lc::ISpectrum^> spectrum_;
};


struct CompassDataImpl : public CompassData
{
    CompassDataImpl(const string& rawpath, Reader_Bruker_Format format_) : parameterCacheByMsLevel_(new map<int, ParameterCache>())
    {
        try
        {
            if (format_ == Reader_Bruker_Format_Unknown)
                format_ = pwiz::msdata::detail::Bruker::format(rawpath);
            if (format_ == Reader_Bruker_Format_Unknown)
                throw runtime_error("[CompassData::ctor] unknown file format");

            if (format_ != Reader_Bruker_Format_U2)
            {
                msAnalysis_ = gcnew EDAL::MSAnalysisClass();
                msAnalysis_->Open(ToSystemString(rawpath));
                msSpectrumCollection_ = msAnalysis_->MSSpectrumCollection;
                hasMSData_ = msSpectrumCollection_->Count > 0;
            }
            else
                hasMSData_ = false;
            
            /*if (format_ == Reader_Bruker_Format_U2 ||
                format_ == Reader_Bruker_Format_BAF_and_U2)
            {
                BDal::CxT::Lc::AnalysisFactory^ factory = gcnew BDal::CxT::Lc::AnalysisFactory();
                lcAnalysis_ = factory->Open(ToSystemString(rawpath));
                lcSources_ = lcAnalysis_->GetSpectrumSourceDeclarations();
                hasLCData_ = lcSources_->Length > 0;
            }
            else*/
                hasLCData_ = false;
        }
        CATCH_AND_FORWARD
    }

    virtual ~CompassDataImpl()
    {
        if ((MS_Analysis^) msAnalysis_ != nullptr) delete msAnalysis_;
        if ((LC_Analysis^) lcAnalysis_ != nullptr) lcAnalysis_->Close();
    }

    virtual bool hasMSData() const {return hasMSData_;}
    virtual bool hasLCData() const {return hasLCData_;}

    virtual size_t getMSSpectrumCount() const
    {
        if (!hasMSData_) return 0;
        try {return msSpectrumCollection_->Count;} CATCH_AND_FORWARD
    }

    virtual MSSpectrumPtr getMSSpectrum(int scan, DetailLevel detailLevel) const
    {
        if (!hasMSData_) throw runtime_error("[CompassData::getMSSpectrum] No MS data.");
        if (scan < 1 || scan > (int) getMSSpectrumCount())
            throw out_of_range("[CompassData::getMSSpectrum] Scan number " + lexical_cast<string>(scan) + " is out of range.");

        try {return MSSpectrumPtr(new MSSpectrumImpl(msSpectrumCollection_->default[scan], parameterCacheByMsLevel_, detailLevel));} CATCH_AND_FORWARD
    }

    virtual size_t getLCSourceCount() const
    {
        if (!hasLCData_) return 0;
        try {return (size_t) lcSources_->Length;} CATCH_AND_FORWARD
    }

    virtual size_t getLCSpectrumCount(int source) const
    {
        if (!hasLCData_) return 0;
        if (source < 0 || source >= lcSources_->Length)
            throw out_of_range("[CompassData::getLCSpectrumCount] Source index out of range.");
        try {return lcAnalysis_->GetSpectrumCollection(lcSources_[source]->SpectrumCollectionId)->NumberOfSpectra;} CATCH_AND_FORWARD
    }

    virtual LCSpectrumSourcePtr getLCSource(int source) const
    {
        if (!hasLCData_) throw runtime_error("[CompassData::getLCSource] No LC data.");
        if (source < 0 || source >= lcSources_->Length)
            throw out_of_range("[CompassData::getLCSpectrum] Source index out of range.");
        try {return LCSpectrumSourcePtr(new LCSpectrumSourceImpl(lcSources_[source]));} CATCH_AND_FORWARD
    }

    virtual LCSpectrumPtr getLCSpectrum(int source, int scan) const
    {
        if (!hasLCData_) throw runtime_error("[CompassData::getLCSpectrum] No LC data.");
        if (source < 0 || source >= lcSources_->Length)
            throw out_of_range("[CompassData::getLCSpectrum] Source index out of range.");
        try
        {
            BDal::CxT::Lc::ISpectrumCollection^ sc = lcAnalysis_->GetSpectrumCollection(lcSources_[source]->SpectrumCollectionId);
            if (scan < 0 || scan >= sc->NumberOfSpectra)
                throw out_of_range("[CompassData::getLCSpectrum] Scan number " + lexical_cast<string>(scan) + " is out of range.");
            return LCSpectrumPtr(new LCSpectrumImpl(sc->default[scan]));
        }
        CATCH_AND_FORWARD
    }

    virtual std::string getOperatorName() const
    {
        if (!hasMSData_) return "";
        try {return ToStdString(msAnalysis_->OperatorName);} CATCH_AND_FORWARD
    }

    virtual std::string getAnalysisName() const
    {
        if (!hasMSData_) return "";
        try {return ToStdString(msAnalysis_->AnalysisName);} CATCH_AND_FORWARD
    }

    virtual boost::local_time::local_date_time getAnalysisDateTime() const
    {
        using bpt::ptime;
        using blt::local_date_time;
        if (!hasMSData_) return local_date_time(bdt::not_a_date_time);

        try
        {
            System::DateTime acquisitionTime = msAnalysis_->AnalysisDateTime;
            bpt::ptime pt(boost::gregorian::date(acquisitionTime.Year, boost::gregorian::greg_month(acquisitionTime.Month), acquisitionTime.Day),
                bpt::time_duration(acquisitionTime.Hour, acquisitionTime.Minute, acquisitionTime.Second, bpt::millisec(acquisitionTime.Millisecond).fractional_seconds()));
            return local_date_time(pt, blt::time_zone_ptr());
        }
        CATCH_AND_FORWARD
    }

    virtual std::string getSampleName() const
    {
        if (!hasMSData_) return "";
        try {return ToStdString(msAnalysis_->SampleName);} CATCH_AND_FORWARD
    }

    virtual std::string getMethodName() const
    {
        if (!hasMSData_) return "";
        try {return ToStdString(msAnalysis_->MethodName);} CATCH_AND_FORWARD
    }

    virtual InstrumentFamily getInstrumentFamily() const
    {
        if (!hasMSData_) return InstrumentFamily_Unknown;
        try {return (InstrumentFamily) msAnalysis_->InstrumentFamily;} CATCH_AND_FORWARD
    }

    virtual std::string getInstrumentDescription() const
    {
        if (!hasMSData_) return "";
        try {return ToStdString(msAnalysis_->InstrumentDescription);} CATCH_AND_FORWARD
    }

    virtual InstrumentSource getInstrumentSource() const { return InstrumentSource_Unknown; }
    virtual std::string getAcquisitionSoftware() const { return ""; }
    virtual std::string getAcquisitionSoftwareVersion() const { return "unknown"; }

    private:
    mutable shared_ptr<map<int, ParameterCache> > parameterCacheByMsLevel_;

    bool hasMSData_;
    gcroot<MS_Analysis^> msAnalysis_;
    gcroot<MS_SpectrumCollection^> msSpectrumCollection_;

    bool hasLCData_;
    gcroot<LC_Analysis^> lcAnalysis_;
    gcroot<cli::array<LC_SpectrumSourceDeclaration^>^> lcSources_;
};


PWIZ_API_DECL CompassDataPtr CompassData::create(const string& rawpath, bool combineIonMobilitySpectra,
                                                 Reader_Bruker_Format format,
                                                 int preferOnlyMsLevel) // when nonzero, caller only wants spectra at this ms level
{
    if (format == Reader_Bruker_Format_BAF || format == Reader_Bruker_Format_BAF_and_U2)
        return CompassDataPtr(new Baf2SqlImpl(rawpath));
#ifdef _WIN64
    else if (format == Reader_Bruker_Format_TDF)
        return CompassDataPtr(new TimsDataImpl(rawpath, combineIonMobilitySpectra, preferOnlyMsLevel));
#endif

    try {return CompassDataPtr(new CompassDataImpl(rawpath, format));} CATCH_AND_FORWARD
}


#else


struct MSSpectrumParameterListImpl : public MSSpectrumParameterList
{
    MSSpectrumParameterListImpl()
    {
    }

    virtual size_t size() const {return 0;}
    virtual value_type operator[] (size_t index) const {return *const_iterator(*this, index);}
    virtual const_iterator begin() const {return const_iterator(*this);}
    virtual const_iterator end() const {return const_iterator();}
};


struct MSSpectrumParameterIterator::Impl
{
    MSSpectrumParameter dummy;
};

PWIZ_API_DECL MSSpectrumParameterIterator::MSSpectrumParameterIterator()
    : impl_(new Impl)
{
}

PWIZ_API_DECL MSSpectrumParameterIterator::MSSpectrumParameterIterator(const MSSpectrumParameterList& pl, size_t index)
    : impl_(new Impl)
{
}

PWIZ_API_DECL MSSpectrumParameterIterator::MSSpectrumParameterIterator(const MSSpectrumParameterIterator& other)
    : impl_(new Impl)
{
}

PWIZ_API_DECL MSSpectrumParameterIterator::~MSSpectrumParameterIterator() {}

PWIZ_API_DECL void MSSpectrumParameterIterator::increment()
{
}

PWIZ_API_DECL void MSSpectrumParameterIterator::decrement()
{
}
PWIZ_API_DECL void MSSpectrumParameterIterator::advance(difference_type n)
{
}

PWIZ_API_DECL bool MSSpectrumParameterIterator::equal(const MSSpectrumParameterIterator& that) const
{
    return true;
}

PWIZ_API_DECL const MSSpectrumParameter& MSSpectrumParameterIterator::dereference() const
{
    return impl_->dummy;
}

PWIZ_API_DECL CompassDataPtr CompassData::create(const string& rawpath, bool combineIonMobilitySpectra,
                                                 Reader_Bruker_Format format,
                                                 int preferOnlyMsLevel) // when nonzero, caller only wants spectra at this ms level
{
    if (format == Reader_Bruker_Format_BAF || format == Reader_Bruker_Format_BAF_and_U2)
        return CompassDataPtr(new Baf2SqlImpl(rawpath));
#ifdef _WIN64
    else if (format == Reader_Bruker_Format_TDF)
        return CompassDataPtr(new TimsDataImpl(rawpath, combineIonMobilitySpectra, preferOnlyMsLevel));
#endif
    else
        throw runtime_error("[CompassData::create] Bruker API was built with only BAF and TDF support; YEP and FID files not supported in this build");
}


#endif // PWIZ_READER_BRUKER_WITH_COMPASSXTRACT


PWIZ_API_DECL pair<size_t, size_t> CompassData::getFrameScanPair(int scanIndex) const
{
    throw runtime_error("[getFrameScanPair()] only supported for TDF data");
}

PWIZ_API_DECL size_t CompassData::getSpectrumIndex(int frame, int scan) const
{
    throw runtime_error("[getSpectrumIndex()] only supported for TDF data");
}

} // namespace Bruker
} // namespace vendor_api
} // namespace pwiz
