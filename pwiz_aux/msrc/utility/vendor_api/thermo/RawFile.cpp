//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2005 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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

#define PWIZ_SOURCE

#include "RawFile.h"

// Uncommenting the following line (assuming you have the necessary DLL) will update the tlh/tli files on the next compile
//#define UPDATE_TLH

#ifdef UPDATE_TLH
#import "MSFileReader.XRawfile2.dll" rename_namespace("XRawfile")
#else
#include "XRawFile2.tlh"
#endif

#include "RawFileValues.h"
#include "RawFileCOM.h"
#include "pwiz/utility/misc/COMInitializer.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Once.hpp"
#include <boost/bind.hpp>
#include <boost/xpressive/xpressive.hpp>


#include <windows.h> // GetModuleFileName
#include <shlwapi.h> // PathIsNetworkPath

using namespace pwiz::vendor_api::Thermo;
using namespace pwiz::util;
using namespace XRawfile;


// note:
// XRawfile seems to return 0 for success, >0 for failure;
// COM standard: HRESULT is >=0 for success, <0 for failure
#define checkResult(stmt, prefix) \
{ \
    try \
    { \
        HRESULT hr = (stmt); \
        if (hr != 0) \
            throw _com_error(hr); \
    } \
    catch (_com_error& e) \
    { \
        ostringstream temp; \
        temp << prefix << "Failed call to XRawfile: " << e.ErrorMessage(); \
        throw RawEgg(temp.str()); \
    } \
}


class RawFileImpl : public RawFile
{
    public:

    RawFileImpl(const string& filename);
    ~RawFileImpl();

    virtual string name(ValueID_Long id);
    virtual string name(ValueID_Double id);
    virtual string name(ValueID_String id);
    virtual long value(ValueID_Long id);
    virtual double value(ValueID_Double id);
    virtual string value(ValueID_String id);

    virtual std::string getFilename() {return filename_;}

    virtual blt::local_date_time getCreationDate();
    virtual auto_ptr<LabelValueArray> getSequenceRowUserInfo();

    virtual ControllerInfo getCurrentController();
    virtual void setCurrentController(ControllerType type, long controllerNumber);
    virtual long getNumberOfControllersOfType(ControllerType type);
    virtual ControllerType getControllerType(long index);

    virtual ScanEventPtr getScanEvent(long index);

    virtual long scanNumber(double rt);
    virtual double rt(long scanNumber);

    virtual MassListPtr
    getMassList(long scanNumber,
                const string& filter,
                CutoffType cutoffType,
                long cutoffValue,
                long maxPeakCount,
                bool centroidResult,
                WhichMassList which,
                const MassRangePtr massRange);

    virtual MassListPtr
    getAverageMassList(long firstAvgScanNumber, long lastAvgScanNumber,
                       long firstBkg1ScanNumber, long lastBkg1ScanNumber,
                       long firstBkg2ScanNumber, long lastBkg2ScanNumber,
                       const string& filter,
                       CutoffType cutoffType,
                       long cutoffValue,
                       long maxPeakCount,
                       bool centroidResult);

    virtual MassListPtr getMassListFromLabelData(long scanNumber);

    virtual auto_ptr<StringArray> getFilters();
    virtual ScanInfoPtr getScanInfo(long scanNumber);

    virtual MSOrder getMSOrder(long scanNumber);
    virtual double getPrecursorMass(long scanNumber, MSOrder msOrder);
    virtual ScanType getScanType(long scanNumber);
    virtual ScanFilterMassAnalyzerType getMassAnalyzerType(long scanNumber);
    virtual ActivationType getActivationType(long scanNumber);
    virtual vector<double> getIsolationWidths(long scanNumber);
    virtual double getIsolationWidth(int scanSegment, int scanEvent);
    virtual double getDefaultIsolationWidth(int scanSegment, int msLevel);

    virtual ErrorLogItem getErrorLogItem(long itemNumber);
    virtual auto_ptr<LabelValueArray> getTuneData(long segmentNumber);
    virtual auto_ptr<LabelValueArray> getInstrumentMethods();
    virtual auto_ptr<StringArray> getInstrumentChannelLabels();

    virtual InstrumentModelType getInstrumentModel();
    virtual const vector<IonizationType>& getIonSources();
    virtual const vector<MassAnalyzerType>& getMassAnalyzers();
    virtual const vector<DetectorType>& getDetectors();

    virtual ChromatogramDataPtr
    getChromatogramData(ChromatogramType type1,
                        ChromatogramOperatorType op,
                        ChromatogramType type2,
                        const string& filter,
                        const string& massRanges1,
                        const string& massRanges2,
                        double delay,
                        double startTime,
                        double endTime,
                        ChromatogramSmoothingType smoothingType,
                        long smoothingValue);

    private:
    friend class ScanInfoImpl;
    IXRawfilePtr raw_;
    int rawInterfaceVersion_; // IXRawfile=1, IXRawfile2=2, IXRawfile3=3, etc.
    string filename_;
    bool isTemporary_;
    ControllerType currentControllerType_;

    InstrumentModelType instrumentModel_;
    vector<IonizationType> ionSources_;
    vector<MassAnalyzerType> massAnalyzers_;
    vector<DetectorType> detectors_;

    map<int, map<int, double> > defaultIsolationWidthBySegmentAndMsLevel;
    map<int, map<int, double> > isolationWidthBySegmentAndScanEvent;

    once_flag_proxy instrumentMethodParsed_;
    void parseInstrumentMethod();
};


PWIZ_API_DECL RawFilePtr RawFile::create(const string& filename)
{
    return RawFilePtr(new RawFileImpl(filename));
}


RawFileImpl::RawFileImpl(const string& filename)
:   raw_(NULL),
    filename_(filename),
    isTemporary_(false),
    instrumentModel_(InstrumentModelType_Unknown),
    instrumentMethodParsed_(init_once_flag_proxy)
{
    // XRawfile requires '.' as a decimal separator
    int decimalSeparatorLength = GetLocaleInfoA(LOCALE_USER_DEFAULT, LOCALE_SDECIMAL, 0, 0);
    char* buf = new char[decimalSeparatorLength];
    GetLocaleInfo(LOCALE_USER_DEFAULT, LOCALE_SDECIMAL, buf, decimalSeparatorLength);
    string decimalSeparator = buf;
    delete buf;
    if (decimalSeparator != ".")
        throw runtime_error("[RawFile::ctor] Reading Thermo RAW files requires the decimal separator to be '.' (yours is '" + decimalSeparator + "') - adjust regional/language settings in the Control Panel.");

    // XRawfile requires ',' as a list separator
    int listSeparatorLength = GetLocaleInfoA(LOCALE_USER_DEFAULT, LOCALE_SLIST, 0, 0);
    buf = new char[listSeparatorLength];
    GetLocaleInfo(LOCALE_USER_DEFAULT, LOCALE_SLIST, buf, listSeparatorLength);
    string listSeparator = buf;
    delete buf;
    if (listSeparator != ",")
        throw runtime_error("[RawFile::ctor] Reading Thermo RAW files requires the list separator to be ',' (yours is '" + listSeparator + "') - adjust regional/language settings in the Control Panel.");

    COMInitializer::initialize();

    // use the latest version of IXRawfile that will initialize
    IXRawfile2Ptr raw2(NULL);
    IXRawfile3Ptr raw3(NULL);
    IXRawfile4Ptr raw4(NULL);
    IXRawfile5Ptr raw5(NULL);

    if (FAILED(raw5.CreateInstance("MSFileReader.XRawfile.1")))
    {
        if (FAILED(raw4.CreateInstance("MSFileReader.XRawfile.1")))
        {
            if (FAILED(raw3.CreateInstance("MSFileReader.XRawfile.1")))
            {
                if (FAILED(raw2.CreateInstance("MSFileReader.XRawfile.1")))
                {
                    if (FAILED(raw_.CreateInstance("MSFileReader.XRawfile.1")))
                    {
                        rawInterfaceVersion_ = 0;
                        throw RawEgg("[RawFile::ctor] Unable to initialize XRawfile; is MSFileReader installed? Reading Thermo RAW files requires MSFileReader to be installed. It is available for download at:\nhttp://sjsupport.thermofinnigan.com/public/detail.asp?id=703");
                    }
                    else
                    {
                        rawInterfaceVersion_ = 1;
                    }
                }
                else
                {
                    raw_ = raw2;
                    rawInterfaceVersion_ = 2;
                }
            }
            else
            {
                raw_ = raw3;
                rawInterfaceVersion_ = 3;
            }
        }
        else
        {
            raw_ = raw4;
            rawInterfaceVersion_ = 4;
        }
    }
    else
    {
        raw_ = raw5;
        rawInterfaceVersion_ = 5;
    }

    try
    {
        // if file is on a network drive, copy it to a temporary local file
        /*if (::PathIsNetworkPath(filename.c_str()))
        {
            char* temp = ::getenv("TEMP");
            bfs::path tempFilepath = bfs::path(temp) / bfs::path(filename).filename();
            if (bfs::exists(tempFilepath))
                bfs::remove(tempFilepath);
            bfs::copy_file(filename, tempFilepath);
            filename_ = tempFilepath.string();
            isTemporary_ = true;
        }*/

        if (raw_->Open(filename_.c_str()))
            throw RawEgg("[RawFile::ctor] Unable to open file " + filename);
    }
    catch (_com_error& e)
    {
        throw RawEgg("[RawFile::ctor] Unable to open file " + filename + ": " + e.ErrorMessage());
    }
}


RawFileImpl::~RawFileImpl()
{
    raw_->Close();
    raw_ = NULL;
    
    // if applicable, delete temporary file
    if (isTemporary_)
        bfs::remove(filename_);

    COMInitializer::uninitialize();
}


string RawFileImpl::name(ValueID_Long id)
{
    return RawFileValues::descriptor(id)->name;
}


string RawFileImpl::name(ValueID_Double id)
{
    return RawFileValues::descriptor(id)->name;
}


string RawFileImpl::name(ValueID_String id)
{
    return RawFileValues::descriptor(id)->name;
}


long RawFileImpl::value(ValueID_Long id)
{
    long result = 0;
    HRESULT hr = (raw_->*RawFileValues::descriptor(id)->function)(&result);
    if (hr > 0)
        throw RawEgg((boost::format("[RawFileImpl::value()] Error getting value for \"%s\"") % name(id)).str());
    return result;
}


double RawFileImpl::value(ValueID_Double id)
{
    double result = 0;
    HRESULT hr = (raw_->*RawFileValues::descriptor(id)->function)(&result);
    if (hr > 0)
        throw RawEgg((boost::format("[RawFileImpl::value()] Error getting value for \"%s\"") % name(id)).str());
    return result;
}


string RawFileImpl::value(ValueID_String id)
{
    _bstr_t bstr;
    HRESULT hr = (raw_->*RawFileValues::descriptor(id)->function)(bstr.GetAddress());
    if (hr > 0)
        throw RawEgg((boost::format("[RawFileImpl::value()] Error getting value for \"%s\"") % name(id)).str());
    return (const char*)(bstr);
}


blt::local_date_time RawFileImpl::getCreationDate()
{
    DATE oadate;
    checkResult(raw_->GetCreationDate(&oadate), "[RawFileImpl::getCreationDate(), GetCreationDate()] ");
    bpt::ptime pt(bdt::time_from_OADATE<bpt::ptime>(oadate));
    return blt::local_date_time(pt, blt::time_zone_ptr()); // keep time as UTC
}


namespace {
class VectorLabelValueArray : public LabelValueArray
{
    public:

    virtual int size() const {return (int)labels_.size();}
    virtual string label(int index) const {checkIndex(index); return labels_[index];}
    virtual string value(int index) const {checkIndex(index); return values_[index];}

    void push_back(const string& label, const string& value)
    {
        labels_.push_back(label);
        values_.push_back(value);
    }

    private:
    vector<string> labels_;
    vector<string> values_;

    void checkIndex(int i) const
    {
        if (i<0 || i>=(int)labels_.size())
            throw RawEgg("VectorLabelValueArray: Array out of bounds.");
    }
};
} // namespace


auto_ptr<LabelValueArray> RawFileImpl::getSequenceRowUserInfo()
{
    auto_ptr<VectorLabelValueArray> info(new VectorLabelValueArray);

    for (int i=0; i<5; i++)
    {
        _bstr_t bstrLabel;
        _bstr_t bstrValue;
        checkResult(raw_->GetSeqRowUserLabel(i, bstrLabel.GetAddress()), "[RawFileImpl::getSequenceRowUserInfo(), GetSeqRowUserLabel()] ");
        checkResult(raw_->GetSeqRowUserText(i, bstrValue.GetAddress()), "[RawFileImpl::getSequenceRowUserInfo(), GetSeqRowUserText()] ");
        info->push_back((const char*)(bstrLabel), (const char*)(bstrValue));
    }

    return info;
}


ControllerInfo RawFileImpl::getCurrentController()
{
    ControllerInfo result;
    long type = 0;
    checkResult(raw_->GetCurrentController(&type, &result.controllerNumber), "[RawFileImpl::getCurrentController()] ");
    result.type = ControllerType(type);
    return result;
}


void RawFileImpl::setCurrentController(ControllerType type, long controllerNumber)
{
    checkResult(raw_->SetCurrentController(type, controllerNumber), "[RawFileImpl::setCurrentController()] ");
    currentControllerType_ = type;
}


long RawFileImpl::getNumberOfControllersOfType(ControllerType type)
{
    long result = 0;
    checkResult(raw_->GetNumberOfControllersOfType(type, &result), "[RawFileImpl::getNumberOfControllersOfType()] ");
    return result;
}


ControllerType RawFileImpl::getControllerType(long index)
{
    long result = 0;
    checkResult(raw_->GetControllerType(index, &result), "[RawFileImpl::getControllerType()] ");
    return ControllerType(result);
}


long RawFileImpl::scanNumber(double rt)
{
    long result = 0;
    checkResult(raw_->ScanNumFromRT(rt, &result), "[RawFileImpl::scanNumber()] ");
    return result;
}


double RawFileImpl::rt(long scanNumber)
{
    double result = 0;
    checkResult(raw_->RTFromScanNum(scanNumber, &result), "[RawFileImpl::rt()] ");
    return result;
}


InstrumentModelType RawFileImpl::getInstrumentModel()
{
    setCurrentController(Controller_MS, 1);

    if (instrumentModel_ == InstrumentModelType_Unknown)
    {
        string modelString = value(InstModel);
        if (modelString == "LTQ Velos") // HACK: disambiguate LTQ Velos and Orbitrap Velos
        {
            modelString = value(InstName);
        }
        else if (modelString == "LTQ Orbitrap" &&
                 value(InstName).empty()) // HACK: disambiguate LTQ Orbitrap and some broken Exactive files
        {
            // Exactive has instrument info at the end of the tune method
            auto_ptr<LabelValueArray> lvArray = getTuneData(0);
            for (int i=0; i < lvArray->size(); ++i)
                if (lvArray->label(i) == "Model")
                {
                    modelString = lvArray->value(i);
                    break;
                }
        }

        instrumentModel_ = parseInstrumentModelType(modelString);
    }
    return instrumentModel_;
}


const vector<IonizationType>& RawFileImpl::getIonSources()
{
    if (ionSources_.empty())
        ionSources_ = getIonSourcesForInstrumentModel(getInstrumentModel());
    return ionSources_;
}


const vector<MassAnalyzerType>& RawFileImpl::getMassAnalyzers()
{
    if (massAnalyzers_.empty())
        massAnalyzers_ = getMassAnalyzersForInstrumentModel(getInstrumentModel());
    return massAnalyzers_;
}


const vector<DetectorType>& RawFileImpl::getDetectors()
{
    if (detectors_.empty())
        detectors_ = getDetectorsForInstrumentModel(getInstrumentModel());
    return detectors_;
}


namespace{
class MassListImpl : public MassList
{
    public:

    MassListImpl(long scanNumber, VARIANT& v, long size, double centroidPeakWidth,
                 long firstAvgScanNumber = 0, long lastAvgScanNumber = 0,
                 long firstBkg1ScanNumber = 0, long lastBkg1ScanNumber = 0,
                 long firstBkg2ScanNumber = 0, long lastBkg2ScanNumber = 0)
    :   scanNumber_(scanNumber),
        msa_(v, size),
        centroidPeakWidth_(centroidPeakWidth),
        firstAvgScanNumber_(firstAvgScanNumber),
        lastAvgScanNumber_(lastAvgScanNumber),
        firstBkg1ScanNumber_(firstBkg1ScanNumber),
        lastBkg1ScanNumber_(lastBkg1ScanNumber),
        firstBkg2ScanNumber_(firstBkg2ScanNumber),
        lastBkg2ScanNumber_(lastBkg2ScanNumber)
    {
        if (v.vt != (VT_ARRAY | VT_R8))
            throw RawEgg("MassListImpl(): VARIANT error.");
    }

    virtual long scanNumber() const {return scanNumber_;}
    virtual long size() const {return msa_.size();}
    virtual MassIntensityPair* data() const {return (MassIntensityPair*)msa_.data();}
    virtual double centroidPeakWidth() const {return centroidPeakWidth_;}
    virtual long firstAvgScanNumber() const {return firstAvgScanNumber_;}
    virtual long lastAvgScanNumber() const {return lastAvgScanNumber_;}
    virtual long firstBkg1ScanNumber() const {return firstBkg1ScanNumber_;}
    virtual long lastBkg1ScanNumber() const {return lastBkg1ScanNumber_;}
    virtual long firstBkg2ScanNumber() const {return firstBkg2ScanNumber_;}
    virtual long lastBkg2ScanNumber() const {return lastBkg2ScanNumber_;}

    private:
    long scanNumber_;
    ManagedSafeArray msa_;
    double centroidPeakWidth_;
    long firstAvgScanNumber_;
    long lastAvgScanNumber_;
    long firstBkg1ScanNumber_;
    long lastBkg1ScanNumber_;
    long firstBkg2ScanNumber_;
    long lastBkg2ScanNumber_;
};

class MassListFromLabelDataImpl : public MassList
{
    public:
    MassListFromLabelDataImpl(long scanNumber, VARIANT& labels)
    :   scanNumber_(scanNumber)
    {
        if (labels.vt != (VT_ARRAY | VT_R8))
            throw RawEgg("MassListFromLabelDataImpl(): VARIANT error.");

        _variant_t labels2(labels, false);
        size_ = (long) labels2.parray->rgsabound[0].cElements;
        data_ = new MassIntensityPair[size_];

        double* pdval = (double*) labels2.parray->pvData;
        for(long i=0; i < size_; ++i)
        {
	        data_[i].mass = (double) pdval[(i*6)+0];
	        data_[i].intensity = (double) pdval[(i*6)+1];
        }
        // labels is freed when labels2 goes out of scope
    }

    ~MassListFromLabelDataImpl()
    {
        delete data_;
    }

    virtual long scanNumber() const {return scanNumber_;}
    virtual long size() const {return size_;}
    virtual MassIntensityPair* data() const {return data_;}

    virtual double centroidPeakWidth() const {return 0;}
    virtual long firstAvgScanNumber() const {return 0;}
    virtual long lastAvgScanNumber() const {return 0;}
    virtual long firstBkg1ScanNumber() const {return 0;}
    virtual long lastBkg1ScanNumber() const {return 0;}
    virtual long firstBkg2ScanNumber() const {return 0;}
    virtual long lastBkg2ScanNumber() const {return 0;}

    private:
    long scanNumber_;
    MassIntensityPair* data_;
    long size_;
};
} // namespace


MassListPtr RawFileImpl::getMassList(long scanNumber,
                                     const string& filter,
                                     CutoffType cutoffType,
                                     long cutoffValue,
                                     long maxPeakCount,
                                     bool centroidResult,
                                     WhichMassList which,
                                     const MassRangePtr massRange)
{
    _bstr_t bstrFilter(filter.c_str());
    double centroidPeakWidth = 0;
    _variant_t variantMassList;
    _variant_t variantPeakFlags;
    long size = 0;


    if (!massRange.get())
    {
        HRESULT (IXRawfile::*f)(long*, _bstr_t, long, long, long, long, double*, VARIANT*, VARIANT*, long*);
        switch(which)
        {
            case MassList_Current:
                f = &IXRawfile::GetMassListFromScanNum;
                break;
            case MassList_Previous:
                f = &IXRawfile::GetPrevMassListFromScanNum;
                break;
            case MassList_Next:
                f = &IXRawfile::GetNextMassListFromScanNum;
                break;
            default:
                throw RawEgg("RawFileImpl::getMassList(): bad WhichMassList.\n");
                break;
        }

        checkResult((raw_->*f)(&scanNumber,
                               bstrFilter,
                               cutoffType,
                               cutoffValue,
                               maxPeakCount,
                               centroidResult,
                               &centroidPeakWidth,
                               &variantMassList,
                               &variantPeakFlags,
                               &size),
                               "[RawFileImpl::getMassList(), GetMassListFromScanNum()] ");
    }
    else
    {
        if (rawInterfaceVersion_ < 4)
            throw RawEgg("[RawFileImpl::getMassList()] Retrieving list by mass ranges requires the IXRawfile4 interface.");

        IXRawfile4Ptr raw4 = (IXRawfile2Ptr) raw_;

        HRESULT (IXRawfile4::*f)(long*, _bstr_t, long, long, long, long, double*, VARIANT*, VARIANT*, LPWSTR, long*);
        switch(which)
        {
            case MassList_Current:
                f = &IXRawfile4::GetMassListRangeFromScanNum;
                break;
            case MassList_Previous:
                f = &IXRawfile4::GetPrevMassListRangeFromScanNum;
                break;
            case MassList_Next:
                f = &IXRawfile4::GetNextMassListRangeFromScanNum;
                break;
            default:
                throw RawEgg("RawFileImpl::getMassList(): bad WhichMassList.\n");
                break;
        }

        wostringstream woss;
        woss << setprecision(7) << massRange->low << '-' << massRange->high;
        checkResult((raw4->*f)(&scanNumber,
                               bstrFilter,
                               cutoffType,
                               cutoffValue,
                               maxPeakCount,
                               centroidResult,
                               &centroidPeakWidth,
                               &variantMassList,
                               &variantPeakFlags,
                               const_cast<wchar_t*>(woss.str().c_str()),
                               &size),
                               "[RawFileImpl::getMassList(), GetMassListRangeFromScanNum()] ");
    }

    return MassListPtr(new MassListImpl(scanNumber,
                                        variantMassList,
                                        size,
                                        centroidPeakWidth));
}


MassListPtr
RawFileImpl::getAverageMassList(long firstAvgScanNumber, long lastAvgScanNumber,
                                long firstBkg1ScanNumber, long lastBkg1ScanNumber,
                                long firstBkg2ScanNumber, long lastBkg2ScanNumber,
                                const string& filter,
                                CutoffType cutoffType,
                                long cutoffValue,
                                long maxPeakCount,
                                bool centroidResult)
{
    _bstr_t bstrFilter(filter.c_str());
    double centroidPeakWidth = 0;
    _variant_t variantMassList;
    _variant_t variantPeakFlags;
    long size = 0;

    checkResult(raw_->GetAverageMassList(&firstAvgScanNumber, &lastAvgScanNumber,
                                         &firstBkg1ScanNumber, &lastBkg1ScanNumber,
                                         &firstBkg2ScanNumber, &lastBkg2ScanNumber,
                                         bstrFilter,
                                         cutoffType,
                                         cutoffValue,
                                         maxPeakCount,
                                         centroidResult,
                                         &centroidPeakWidth,
                                         &variantMassList,
                                         &variantPeakFlags,
                                         &size),
                                         "[RawFileImpl::getMassList(), GetAverageMassList()] ");

    return MassListPtr(new MassListImpl(0,
                                        variantMassList,
                                        size,
                                        centroidPeakWidth,
                                        firstAvgScanNumber, lastAvgScanNumber,
                                        firstBkg1ScanNumber, lastBkg1ScanNumber,
                                        firstBkg2ScanNumber, lastBkg2ScanNumber));
}


MassListPtr RawFileImpl::getMassListFromLabelData(long scanNumber)
{
    if (rawInterfaceVersion_ < 2)
        throw RawEgg("[RawFileImpl::getMassListFromLabelData()] GetLabelData requires the IXRawfile2 interface.");

    IXRawfile2Ptr raw2 = (IXRawfile2Ptr) raw_;
	_variant_t varLabels;
    _variant_t varFlags;
	raw2->GetLabelData(&varLabels, &varFlags, &scanNumber);
    return MassListPtr(new MassListFromLabelDataImpl(scanNumber, varLabels));
}


auto_ptr<StringArray> RawFileImpl::getFilters()
{
    _variant_t v;
    long size = 0;

    checkResult(raw_->GetFilters(&v, &size), "[RawFileImpl::getFilters()] ");

    VariantStringArray* vsa = new VariantStringArray(v, size);
    return auto_ptr<StringArray>(vsa);
}


class ScanInfoImpl : public ScanInfo
{
    public:

    ScanInfoImpl(long scanNumber, RawFileImpl* raw);
    virtual long scanNumber() const {return scanNumber_;}

    virtual std::string filter() const {return filter_;}
    virtual MassAnalyzerType massAnalyzerType() const {return massAnalyzerType_;}
    virtual IonizationType ionizationType() const {return ionizationType_;}
    virtual ActivationType activationType() const {return activationType_;}
    virtual long msLevel() const {return msLevel_;}
    virtual ScanType scanType() const {return scanType_;}
    virtual PolarityType polarityType() const {return polarityType_;}
    virtual bool isEnhanced() const {return isEnhanced_;}
    virtual bool isDependent() const {return isDependent_;}
    virtual bool hasMultiplePrecursors() const {return hasMultiplePrecursors_; }

    virtual std::vector<PrecursorInfo> precursorInfo() const;
    virtual long precursorCount() const {return precursorMZs_.size();}
    virtual long precursorCharge() const;
    virtual double precursorMZ(long index, bool preferMonoisotope) const;
    virtual double precursorActivationEnergy(long index) const {return precursorActivationEnergies_[index];}

    virtual long parentCount() const {return precursorCount();}
    virtual long parentCharge() const {return precursorCharge();}
    virtual double parentMass(long index, bool preferMonoisotope) const {return precursorMZ(index, preferMonoisotope);}
    virtual double parentEnergy(long index) const {return precursorActivationEnergy(index);}

    virtual size_t scanRangeCount() const {return scanRanges_.size();}
    virtual const pair<double, double>& scanRange(size_t index) const {return scanRanges_[index];}

    virtual bool isProfileScan() const {return isProfileScan_;}
    virtual bool isCentroidScan() const {return isCentroidScan_;}
    virtual long packetCount() const {return packetCount_;}
    virtual double startTime() const {return startTime_;}
    virtual double lowMass() const {return lowMass_;}
    virtual double highMass() const {return highMass_;}
    virtual double totalIonCurrent() const {return totalIonCurrent_;}
    virtual double basePeakMass() const {return basePeakMZ_;}
    virtual double basePeakMZ() const {return basePeakMZ_;}
    virtual double basePeakIntensity() const {return basePeakIntensity_;}
    virtual long channelCount() const {return channelCount_;}
    virtual bool isUniformTime() const {return isUniformTime_;}
    virtual double frequency() const {return frequency_;}
	virtual bool FAIMSOn() const {return faimsOn_;}
	virtual double CompensationVoltage() const {return compensationVoltage_;}

    virtual long statusLogSize() const {initStatusLog(); return statusLogSize_;}
    virtual double statusLogRT() const {initStatusLog(); return statusLogRT_;}
    virtual std::string statusLogLabel(long index) const {initStatusLog(); return statusLogLabels_->item(index);}
    virtual std::string statusLogValue(long index) const {initStatusLog(); return statusLogValues_->item(index);}

    virtual long trailerExtraSize() const {initTrailerExtra(); return trailerExtraSize_;}
    virtual std::string trailerExtraLabel(long index) const {initTrailerExtra(); return trailerExtraLabels_->item(index);}
    virtual std::string trailerExtraValue(long index) const {initTrailerExtra(); return trailerExtraValues_->item(index);}
    virtual std::string trailerExtraValue(const string& name) const {initTrailerExtra(); return trailerExtraMap_[name];}
    virtual double trailerExtraValueDouble(const string& name) const;
    virtual long trailerExtraValueLong(const string& name) const;

    private:

    long scanNumber_;
    RawFileImpl* rawfile_;
    string filter_;
    MassAnalyzerType massAnalyzerType_;
    IonizationType ionizationType_;
    ActivationType activationType_;
    long msLevel_;
    ScanType scanType_;
    PolarityType polarityType_;
    bool isEnhanced_;
    bool isDependent_;
    bool hasMultiplePrecursors_; // true for "MSX" mode
    vector<double> precursorMZs_;
    vector<double> precursorActivationEnergies_;
    vector<pair<double, double> > scanRanges_;
    bool isProfileScan_;
    bool isCentroidScan_;
    long packetCount_;
    double startTime_;
    double lowMass_;
    double highMass_;
    double totalIonCurrent_;
    double basePeakMZ_;
    double basePeakIntensity_;
    long channelCount_;
    bool isUniformTime_;
    double frequency_;
	bool faimsOn_;
	double compensationVoltage_;

    mutable once_flag_proxy statusLogInitialized_;
    mutable long statusLogSize_;
    mutable double statusLogRT_;
    mutable auto_ptr<VariantStringArray> statusLogLabels_;
    mutable auto_ptr<VariantStringArray> statusLogValues_;

    mutable once_flag_proxy trailerExtraInitialized_;
    mutable long trailerExtraSize_;
    mutable auto_ptr<VariantStringArray> trailerExtraLabels_;
    mutable auto_ptr<VariantStringArray> trailerExtraValues_;
    mutable map<string,string> trailerExtraMap_;

    void initialize();
    void initStatusLog() const;
    void initStatusLogHelper() const;
    void initTrailerExtra() const;
    void initTrailerExtraHelper() const;
    void parseFilterString();
};

ScanInfoImpl::ScanInfoImpl(long scanNumber, RawFileImpl* raw)
:   scanNumber_(scanNumber),
    rawfile_(raw),
    massAnalyzerType_(MassAnalyzerType_Unknown),
    ionizationType_(IonizationType_Unknown),
    activationType_(ActivationType_Unknown),
    msLevel_(1),
    scanType_(ScanType_Unknown),
    polarityType_(PolarityType_Unknown),
    isEnhanced_(false),
    isDependent_(false),
    hasMultiplePrecursors_(false),
    isProfileScan_(false),
    isCentroidScan_(false),
    packetCount_(0),
    startTime_(0),
    lowMass_(0),
    highMass_(0),
    totalIonCurrent_(0),
    basePeakMZ_(0),
    basePeakIntensity_(0),
    channelCount_(0),
    isUniformTime_(false),
    frequency_(0),
    statusLogInitialized_(init_once_flag_proxy),
    statusLogSize_(0),
    statusLogRT_(0),
    statusLogLabels_(0),
    statusLogValues_(0),
    trailerExtraInitialized_(init_once_flag_proxy),
    trailerExtraSize_(0),
    trailerExtraLabels_(0),
    trailerExtraValues_(0)
{
    initialize();
}

void ScanInfoImpl::initialize()
{
    IXRawfilePtr& raw_ = (*rawfile_).raw_;

    // TODO: figure out which controllers have filters, PDA/UV does not!
    if (rawfile_->currentControllerType_ == Controller_MS)
    {
        _bstr_t bstrFilter;
        checkResult(raw_->GetFilterForScanNum(scanNumber_, bstrFilter.GetAddress()), "[ScanInfoImpl::initialize(), GetFilterForScanNum()] ");
        filter_ = (const char*)(bstrFilter);
        parseFilterString();
    }

    long isUniformTime = 0;
    HRESULT hr = raw_->GetScanHeaderInfoForScanNum(scanNumber_,
                                                  &packetCount_,
                                                  &startTime_,
                                                  &lowMass_,
                                                  &highMass_,
                                                  &totalIonCurrent_,
                                                  &basePeakMZ_,
                                                  &basePeakIntensity_,
                                                  &channelCount_,
                                                  &isUniformTime,
                                                  &frequency_);
    if (hr != 0) 
    {
        checkResult(raw_->GetStartTime(&startTime_), "[ScanInfoImpl::initialize(), GetStartTime()]");
    }
    isUniformTime_ = (isUniformTime!=0);
}

void ScanInfoImpl::initStatusLog() const
{
    boost::call_once(statusLogInitialized_.flag, boost::bind(&ScanInfoImpl::initStatusLogHelper, this));
}

void ScanInfoImpl::initStatusLogHelper() const
{
    _variant_t variantStatusLogLabels;
    _variant_t variantStatusLogValues;

    checkResult(rawfile_->raw_->GetStatusLogForScanNum(scanNumber_,
                                             &statusLogRT_,
                                             &variantStatusLogLabels,
                                             &variantStatusLogValues,
                                             &statusLogSize_),
                                             "[ScanInfoImpl::initStatusLog(), GetStatusLogForScanNum()] ");
    statusLogLabels_ = auto_ptr<VariantStringArray>(new VariantStringArray(variantStatusLogLabels, statusLogSize_));
    statusLogValues_ = auto_ptr<VariantStringArray>(new VariantStringArray(variantStatusLogValues, statusLogSize_));
}

void ScanInfoImpl::initTrailerExtra() const
{
    boost::call_once(trailerExtraInitialized_.flag, boost::bind(&ScanInfoImpl::initTrailerExtraHelper, this));
}

void ScanInfoImpl::initTrailerExtraHelper() const
{
    _variant_t variantTrailerExtraLabels;
    _variant_t variantTrailerExtraValues;

    checkResult(rawfile_->raw_->GetTrailerExtraForScanNum(scanNumber_,
                                                &variantTrailerExtraLabels,
                                                &variantTrailerExtraValues,
                                                &trailerExtraSize_),
                                                "[ScanInfoImpl::initTrailerExtra(), GetTrailerExtraForScanNum()] ");
    trailerExtraLabels_ = auto_ptr<VariantStringArray>(new VariantStringArray(variantTrailerExtraLabels, trailerExtraSize_));
    trailerExtraValues_ = auto_ptr<VariantStringArray>(new VariantStringArray(variantTrailerExtraValues, trailerExtraSize_));

    if (trailerExtraLabels_->size() != trailerExtraValues_->size())
        throw RawEgg("[ScanInfoImpl::initTrailerExtra()] Trailer Extra sizes do not match."); 

    for (int i=0; i<trailerExtraLabels_->size(); i++)
        trailerExtraMap_[trailerExtraLabels_->item(i)] = trailerExtraValues_->item(i);
}

void ScanInfoImpl::parseFilterString()
{
    ScanFilter filterParser;
    try
    {
        filterParser.parse(filter_);
    }
    catch (exception& e)
    {
        throw RawEgg("[ScanInfoImpl::parseFilterString()] error parsing filter \"" + filter_ + "\": " + e.what());
    }

    msLevel_ = filterParser.msLevel_;
    massAnalyzerType_ = convertScanFilterMassAnalyzer(filterParser.massAnalyzerType_, rawfile_->getInstrumentModel());
    ionizationType_ = filterParser.ionizationType_;
    polarityType_ = filterParser.polarityType_;
    scanType_ = filterParser.scanType_;
    activationType_ = filterParser.activationType_;
    isEnhanced_ = filterParser.enhancedOn_ == TriBool_True;
    isDependent_ = filterParser.dependentActive_ == TriBool_True;
    hasMultiplePrecursors_ = filterParser.multiplePrecursorMode_;
    precursorMZs_.insert(precursorMZs_.end(), filterParser.precursorMZs_.begin(), filterParser.precursorMZs_.end());
    precursorActivationEnergies_.insert(precursorActivationEnergies_.end(), filterParser.precursorEnergies_.begin(), filterParser.precursorEnergies_.end());
    isProfileScan_ = filterParser.dataPointType_ == DataPointType_Profile;
    isCentroidScan_ = filterParser.dataPointType_ == DataPointType_Centroid;
	faimsOn_ = filterParser.faimsOn_ == TriBool_True;
	compensationVoltage_ = filterParser.compensationVoltage_;

    for (size_t i=0; i < filterParser.scanRangeMin_.size(); ++i)
        scanRanges_.push_back(make_pair(filterParser.scanRangeMin_[i], filterParser.scanRangeMax_[i]));
}

vector<PrecursorInfo> ScanInfoImpl::precursorInfo() const
{
    if (rawfile_->rawInterfaceVersion_ < 3)
        throw RawEgg("[RawFileImpl::getMassListFromLabelData()] GetPrecursorInfoFromScanNum requires the IXRawfile3 interface.");

    IXRawfile3Ptr raw3 = (IXRawfile3Ptr) rawfile_->raw_;
    vector<PrecursorInfo> precursorInfo;
	_variant_t varInfo;
    long precursorCount;
    raw3->GetPrecursorInfoFromScanNum(scanNumber_, &varInfo, &precursorCount);
    cout << precursorCount << " " << varInfo.vt << endl;
    return precursorInfo;
}

long ScanInfoImpl::precursorCharge() const
{
    try
    {
        return trailerExtraValueLong("Charge State:");
    }
    catch (RawEgg&)
    {
        // almost certainly means that the label was not present
        return 0;
    }
}

double ScanInfoImpl::precursorMZ(long index, bool preferMonoisotope) const
{
    if (preferMonoisotope)
    {
        try
        {
            double mz = trailerExtraValueDouble("Monoisotopic M/Z:");
            if (mz > 0)
                return mz;
        }
        catch (RawEgg&)
        {
            // almost certainly means that the label was not present
        }
    }
    return precursorMZs_[index];
}

double ScanInfoImpl::trailerExtraValueDouble(const string& name) const
{
    IXRawfilePtr& raw_ = rawfile_->raw_;

    _variant_t v;

    checkResult(raw_->GetTrailerExtraValueForScanNum(scanNumber_,
                                                     name.c_str(), 
                                                     &v),
                                                     "[ScanInfoImpl::trailerExtraValueDouble()] ");
    switch (v.vt)
    {
        case VT_R4: return v.fltVal;
        case VT_R8: return v.dblVal;
        default:
            throw RawEgg("[ScanInfoImpl::trailerExtraValueDouble()] Unknown type.");
    }
}


long ScanInfoImpl::trailerExtraValueLong(const string& name) const
{
    IXRawfilePtr& raw_ = rawfile_->raw_;

    _variant_t v;

    checkResult(raw_->GetTrailerExtraValueForScanNum(scanNumber_,
                                                     name.c_str(), 
                                                     &v),
                                                     "[ScanInfoImpl::trailerExtraValueLong()] ");
    switch (v.vt)
    {
        case VT_I1: return v.cVal;
        case VT_UI1: return v.bVal;
        case VT_I2: return v.iVal;
        case VT_UI2: return v.uiVal;
        case VT_I4: return v.lVal;
        case VT_UI4: return v.ulVal;
        case VT_INT: return v.intVal;
        case VT_UINT: return v.uintVal;
        default:
            throw RawEgg("[ScanInfoImpl::trailerExtraValueLong()] Unknown type.");
    }
}


ScanInfoPtr RawFileImpl::getScanInfo(long scanNumber)
{
    ScanInfoPtr scanInfo(new ScanInfoImpl(scanNumber, this));
    return scanInfo;
}


MSOrder RawFileImpl::getMSOrder(long scanNumber)
{
    if (rawInterfaceVersion_ < 4)
        throw RawEgg("[RawFileImpl::getMSOrder()] GetMSOrderForScanNum requires the IXRawfile4 interface.");

    IXRawfile4Ptr raw4 = (IXRawfile4Ptr) raw_;

    long result;
    checkResult(raw4->GetMSOrderForScanNum(scanNumber, &result), "[RawFileImpl::getMSOrder()] ");
    return (MSOrder) result;
}


double RawFileImpl::getPrecursorMass(long scanNumber, MSOrder msOrder)
{
    if (rawInterfaceVersion_ < 4)
        throw RawEgg("[RawFileImpl::getPrecursorMass()] GetPrecursorMassForScanNum requires the IXRawfile4 interface.");

    IXRawfile4Ptr raw4 = (IXRawfile4Ptr) raw_;

    if (msOrder < MSOrder_MS2)
        msOrder = getMSOrder(scanNumber);

    double result;
    checkResult(raw4->GetPrecursorMassForScanNum(scanNumber, msOrder, &result), "[RawFileImpl::GetPrecursorMassForScanNum()] ");
    return floor(result * 100 + 0.5) / 100; // round to 2 decimal places (like the filter line)
}


ScanType RawFileImpl::getScanType(long scanNumber)
{
    if (rawInterfaceVersion_ < 4)
        throw RawEgg("[RawFileImpl::getScanType()] GetScanTypeForScanNum requires the IXRawfile4 interface.");

    IXRawfile4Ptr raw4 = (IXRawfile4Ptr) raw_;

    long result;
    checkResult(raw4->GetScanTypeForScanNum(scanNumber, &result), "[RawFileImpl::GetScanTypeForScanNum()] ");
    return (ScanType) result;
}


ScanFilterMassAnalyzerType RawFileImpl::getMassAnalyzerType(long scanNumber)
{
    if (rawInterfaceVersion_ < 4)
        throw RawEgg("[RawFileImpl::getMassAnalyzerType()] GetMassAnalyzerTypeForScanNum requires the IXRawfile4 interface.");

    IXRawfile4Ptr raw4 = (IXRawfile4Ptr) raw_;

    long result;
    checkResult(raw4->GetMassAnalyzerTypeForScanNum(scanNumber, &result), "[RawFileImpl::GetMassAnalyzerTypeForScanNum()] ");
    return (ScanFilterMassAnalyzerType) result;
}


ActivationType RawFileImpl::getActivationType(long scanNumber)
{
    if (rawInterfaceVersion_ < 4)
        throw RawEgg("[RawFileImpl::getActivationType()] GetActivationTypeForScanNum requires the IXRawfile4 interface.");

    IXRawfile4Ptr raw4 = (IXRawfile4Ptr) raw_;

    long result;
    checkResult(raw4->GetActivationTypeForScanNum(scanNumber, MSOrder_Any, &result), "[RawFileImpl::GetActivationTypeForScanNum()] ");
    return (ActivationType) result;
}


void RawFileImpl::parseInstrumentMethod()
{
    using namespace boost::xpressive;

    string instrumentMethods;

    auto_ptr<LabelValueArray> labelValueArray = getInstrumentMethods();
    if (!labelValueArray.get())
        return;

    for (int i=0; i < labelValueArray->size(); ++i)
        instrumentMethods += labelValueArray->value(i) + "\n";

    int scanSegment = 1, scanEvent;
    bool scanEventDetails = false;
    bool dataDependentSettings = false;

    sregex scanSegmentRegex = sregex::compile("\\s*Segment (\\d+) Information\\s*");
    sregex scanEventRegex = sregex::compile("\\s*(\\d+):.*");
    sregex scanEventIsolationWidthRegex = sregex::compile("\\s*Isolation Width:\\s*(\\S+)\\s*");
	sregex scanEventIsoWRegex = sregex::compile("\\s*MS.*:.*\\s+IsoW\\s+(\\S+)\\s*");
    sregex repeatedEventRegex = sregex::compile("\\s*Scan Event (\\d+) repeated for top (\\d+)\\s*");
    sregex defaultIsolationWidthRegex = sregex::compile("\\s*MS(\\d+) Isolation Width:\\s*(\\S+)\\s*");

    smatch what;
    string line;
    istringstream instrumentMethodStream(instrumentMethods);
    while (std::getline(instrumentMethodStream, line))
    {
        string::const_iterator start = line.begin(), end = line.end();

        // Segment 1 Information
        if (regex_match(line, what, scanSegmentRegex))
        {
            scanSegment = lexical_cast<int>(what[1]);
            continue;
        }

        // Scan Event Details:
        if (bal::icontains(line, "Scan Event Details"))
        {
            scanEventDetails = true;
            continue;
        }

        if (scanEventDetails)
        {
            // 2:  ITMS + c norm Dep MS/MS Most intense ion from (1)
            //       ...
            //       Isolation Width:         2.5
            //       ...
            if (regex_match(line, what, scanEventRegex))
            {
                scanEvent = lexical_cast<int>(what[1]);
                continue;
            }

            if (regex_match(line, what, scanEventIsolationWidthRegex) || regex_match(line, what, scanEventIsoWRegex))
            {
                isolationWidthBySegmentAndScanEvent[scanSegment][scanEvent] = lexical_cast<double>(what[1]);
                continue;
            }

            // Scan Event M repeated for top N peaks
            if (regex_match(line, what, repeatedEventRegex))
            {
                int repeatedEvent = lexical_cast<int>(what[1]);
                int repeatCount = lexical_cast<int>(what[2]);
                double repeatedIsolationWidth = isolationWidthBySegmentAndScanEvent[scanSegment][repeatedEvent];
                for (int i = repeatedEvent + 1; i < repeatedEvent + repeatCount; ++i)
                    isolationWidthBySegmentAndScanEvent[scanSegment][i] = repeatedIsolationWidth;
                continue;
            }

            if (bal::all(line, bal::is_space()))
                scanEventDetails = false;
        }

        // Data Dependent Settings:
        if (bal::icontains(line, "Data Dependent Settings"))
        {
            dataDependentSettings = true;
            continue;
        }

        if (dataDependentSettings)
        {
            // MSn Isolation Width:       2.5
            if (regex_match(line, what, defaultIsolationWidthRegex))
            {
                int msLevel = lexical_cast<int>(what[1]);
                double isolationWidth = lexical_cast<double>(what[2]);
                defaultIsolationWidthBySegmentAndMsLevel[scanSegment][msLevel] = isolationWidth;
                continue;
            }

            if (bal::all(line, bal::is_space()))
                dataDependentSettings = false;
        }
    }
}

vector<double> RawFileImpl::getIsolationWidths(long scanNumber)
{
    if (rawInterfaceVersion_ < 5)
        throw RawEgg("[RawFileImpl::getIsolationWidths()] GetIsolationWidthForScanNum requires the IXRawfile5 interface.");

    IXRawfile5Ptr raw5 = (IXRawfile5Ptr) raw_;

    vector<double> isolationWidths;
    long nNumMSOrders;
    checkResult(raw5->GetNumberOfMSOrdersFromScanNum(scanNumber, &nNumMSOrders), "[RawFileImpl::GetNumberOfMSOrdersFromScanNum()] ");
    for (long i = 0; i < nNumMSOrders; i++)
    {
        double dIsolationWidth;
        checkResult(raw5->GetIsolationWidthForScanNum(scanNumber, i, &dIsolationWidth), "[RawFileImpl::GetIsolationWidthForScanNum()] ");
        isolationWidths.push_back(dIsolationWidth);
    }
    return isolationWidths;
}

double RawFileImpl::getIsolationWidth(int scanSegment, int scanEvent)
{
    boost::call_once(instrumentMethodParsed_.flag, boost::bind(&RawFileImpl::parseInstrumentMethod, this));

    if (isolationWidthBySegmentAndScanEvent.count(scanSegment) > 0 &&
        isolationWidthBySegmentAndScanEvent[scanSegment].count(scanEvent) > 0)
        return isolationWidthBySegmentAndScanEvent[scanSegment][scanEvent];
    return 0.0;
}


double RawFileImpl::getDefaultIsolationWidth(int scanSegment, int msLevel)
{
    boost::call_once(instrumentMethodParsed_.flag, boost::bind(&RawFileImpl::parseInstrumentMethod, this));

    if (defaultIsolationWidthBySegmentAndMsLevel.count(scanSegment) > 0 &&
        defaultIsolationWidthBySegmentAndMsLevel[scanSegment].count(msLevel) > 0)
        return defaultIsolationWidthBySegmentAndMsLevel[scanSegment][msLevel];
    return 0.0;
}


ErrorLogItem RawFileImpl::getErrorLogItem(long itemNumber)
{
    ErrorLogItem result;
    _bstr_t bstr;
    checkResult(raw_->GetErrorLogItem(itemNumber, &result.rt, bstr.GetAddress()), "[RawFileImpl::GetErrorLogItem()] ");
    result.errorMessage = (const char*)(bstr);
    return result;
}


namespace {
class TuneDataLabelValueArray : public LabelValueArray
{
    public:

    TuneDataLabelValueArray(VARIANT& variantLabels, long size,
                            IXRawfilePtr& raw, long segmentNumber)
    :   labels_(variantLabels, size),
        raw_(raw),
        segmentNumber_(segmentNumber)
    {}

    virtual int size() const {return labels_.size();}
    virtual string label(int index) const {return labels_.item(index);}

    virtual string value(int index) const
    {
        // lazy evaluation via GetTuneDataValue() because
        // GetTuneData() was returning unexpected error

        _variant_t v;
        _bstr_t bstrLabel(labels_.item(index).c_str());
        HRESULT hr = raw_->GetTuneDataValue(segmentNumber_, bstrLabel, &v);
        if (hr)
            return string(); // ignore errors

        ostringstream oss;

        switch(v.vt)
        {
            case VT_EMPTY:
                break;
            case VT_R8:
                oss << v.dblVal;
                break;
            case VT_BOOL:
                oss << v.boolVal;
                break;
            case VT_I2:
                oss << v.iVal;
                break;
            case VT_BSTR:
            {
                _bstr_t temp(v.bstrVal);
                oss << (const char*)(temp);
                break;
            }
            default:
                oss << "UNHANDLED VT: " << v.vt;
                break;
        }

        return oss.str();
    }

    private:

    VariantStringArray labels_;
    IXRawfilePtr& raw_;
    long segmentNumber_;

    TuneDataLabelValueArray(TuneDataLabelValueArray&);
    TuneDataLabelValueArray& operator=(TuneDataLabelValueArray&);
};
} // namespace


auto_ptr<LabelValueArray> RawFileImpl::getTuneData(long segmentNumber)
{
    _variant_t variantLabels;
    long size = 0;

    checkResult(raw_->GetTuneDataLabels(segmentNumber, &variantLabels, &size), "[RawFileImpl::GetTuneDataLabels()] ");

    auto_ptr<TuneDataLabelValueArray> a(
        new TuneDataLabelValueArray(variantLabels, size, raw_, segmentNumber));
    return a;
}


namespace {
class InstrumentMethodLabelValueArray : public LabelValueArray
{
    public:

    InstrumentMethodLabelValueArray(VARIANT& variantLabels, long size,
                            IXRawfilePtr& raw)
    :   labels_(variantLabels, size),
        raw_(raw)
    {}

    virtual int size() const {return labels_.size();}
    virtual string label(int index) const {return labels_.item(index);}

    virtual string value(int index) const
    {
        // lazy evaluation: non-VARIANT interface to get the values
        _bstr_t bstr;
        HRESULT hr = raw_->GetInstMethod(index, bstr.GetAddress());
        if (hr)
            return string(); // empty value
        return (const char*)(bstr);
    }

    private:

    VariantStringArray labels_;
    IXRawfilePtr& raw_;

    InstrumentMethodLabelValueArray(InstrumentMethodLabelValueArray&);
    InstrumentMethodLabelValueArray& operator=(InstrumentMethodLabelValueArray&);
};
} // namespace


auto_ptr<LabelValueArray> RawFileImpl::getInstrumentMethods()
{
    _variant_t variantLabels;
    long size = 0;

    try
    {
        checkResult(raw_->GetInstMethodNames(&size, &variantLabels), "[RawFileImpl::GetInstMethodNames()] ");
    }
    catch (exception&)
    {
        // TODO: log warning?
        return auto_ptr<LabelValueArray>();
    }

    auto_ptr<InstrumentMethodLabelValueArray> a(
        new InstrumentMethodLabelValueArray(variantLabels, size, raw_));

    return a;
}


namespace {
class InstrumentChannelStringArray : public StringArray
{
    public:

    InstrumentChannelStringArray(RawFileImpl* rawFile, IXRawfilePtr& raw)
    :   rawFile_(rawFile),
        raw_(raw)
    {
        size_ = rawFile_->value(InstNumChannelLabels);
    }

    virtual int size() const {return size_;}

    virtual string item(int index) const
    {
        _bstr_t bstr;
        HRESULT hr = raw_->GetInstChannelLabel(index, bstr.GetAddress());
        if (hr)
            throw RawEgg("InstrumentChannelStringArray: error");
        return (const char*)(bstr);
    }

    private:
    RawFileImpl* rawFile_;
    IXRawfilePtr& raw_;
    int size_;
};
} // namespace


auto_ptr<StringArray> RawFileImpl::getInstrumentChannelLabels()
{
    auto_ptr<InstrumentChannelStringArray> a(new InstrumentChannelStringArray(this, raw_));
    return a;
}


namespace{
class ChromatogramDataImpl : public ChromatogramData
{
    public:

    ChromatogramDataImpl(VARIANT& v, long size, double startTime, double endTime)
    :   msa_(v, size),
        startTime_(startTime),
        endTime_(endTime)
    {
        if (v.vt != (VT_ARRAY | VT_R8))
            throw RawEgg("ChromatogramDataImpl(): VARIANT error.");
    }

    virtual double startTime() const {return startTime_;}
    virtual double endTime() const {return endTime_;}
    virtual long size() const {return msa_.size();}
    virtual TimeIntensityPair* data() const {return (TimeIntensityPair*)msa_.data();}

    private:
    ManagedSafeArray msa_;
    double startTime_;
    double endTime_;
};
} // namespace


ChromatogramDataPtr
RawFileImpl::getChromatogramData(ChromatogramType type1,
                                 ChromatogramOperatorType op,
                                 ChromatogramType type2,
                                 const string& filter,
                                 const string& massRanges1,
                                 const string& massRanges2,
                                 double delay,
                                 double startTime,
                                 double endTime,
                                 ChromatogramSmoothingType smoothingType,
                                 long smoothingValue)
{
    _bstr_t bstrFilter(filter.c_str());
    _bstr_t bstrMassRanges1(massRanges1.c_str());
    _bstr_t bstrMassRanges2(massRanges2.c_str());
    _variant_t variantChromatogramData;
    _variant_t variantPeakFlags;
    long size = 0;

    checkResult(raw_->GetChroData(type1, op, type2,
                                  bstrFilter, bstrMassRanges1, bstrMassRanges2,
                                  delay, &startTime, &endTime,
                                  smoothingType, smoothingValue,
                                  &variantChromatogramData, &variantPeakFlags, &size),
                                  "[RawFileImpl::GetChroData]");

    return ChromatogramDataPtr(new ChromatogramDataImpl(variantChromatogramData, size, startTime, endTime));
}


namespace {
class ScanEventImpl : public ScanEvent
{
    public:

    ScanEventImpl(MS_ScanEvent* rawScanEvent);

    //virtual MassAnalyzerType massAnalyzerType() const;
    virtual IonizationType ionizationType() const;
    //virtual ActivationType activationType() const;
    virtual ScanType scanType() const;
    virtual PolarityType polarityType() const;

    virtual const std::vector<MassRange>& massRanges() const;

    virtual ~ScanEventImpl() {delete rawScanEvent_;}

    private:
    MS_ScanEvent* rawScanEvent_;
    mutable std::vector<MassRange> massRanges_;
};

ScanEventImpl::ScanEventImpl(MS_ScanEvent* rawScanEvent)
: rawScanEvent_(rawScanEvent)
{}

IonizationType ScanEventImpl::ionizationType() const
{
    switch(rawScanEvent_->eIonizationMode)
    {
        case MS_ElectronImpact:                             return IonizationType_EI;
        case MS_ChemicalIonization:                         return IonizationType_CI;
        case MS_FastAtomBombardment:                        return IonizationType_FAB;
        case MS_Electrospray:                               return IonizationType_ESI;
        case MS_AtmosphericPressureChemicalIonization:      return IonizationType_APCI;
        case MS_Nanospray:                                  return IonizationType_NSI;
        case MS_Thermospray:                                return IonizationType_TSP;
        case MS_FieldDesorption:                            return IonizationType_FD;
        case MS_MatrixAssistedLaserDesorptionIonization:    return IonizationType_MALDI;
        case MS_GlowDischarge:                              return IonizationType_GD;

        case MS_AcceptAnyIonizationMode:
        default:
            return IonizationType_Unknown;
    }
}

ScanType ScanEventImpl::scanType() const
{
    switch(rawScanEvent_->eScanType)
    {
        case MS_Fullsc:     return ScanType_Full;
        case MS_Zoomsc:     return ScanType_Zoom;
        case MS_SIMsc:      return ScanType_SIM;
        case MS_SRMsc:      return ScanType_SRM;
        case MS_CRMsc:      return ScanType_CRM;
        case MS_Q1MSsc:     return ScanType_Q1MS;
        case MS_Q3MSsc:     return ScanType_Q3MS;

        case MS_AcceptAnyScanType:
        default:
            return ScanType_Unknown;
    }
}

PolarityType ScanEventImpl::polarityType() const
{
    switch (rawScanEvent_->ePolarity)
    {
        case MS_Negative:   return PolarityType_Negative;
        case MS_Positive:   return PolarityType_Positive;

        case MS_AnyPolarity:
        default:
            return PolarityType_Unknown;
    }
}

const vector<MassRange>& ScanEventImpl::massRanges() const
{
    if (massRanges_.empty())
    {
        MassRange* massRangeArray = reinterpret_cast<MassRange*>(rawScanEvent_->arrMassRanges);
        massRanges_.assign(massRangeArray, massRangeArray+rawScanEvent_->nNumMassRanges);
    }
    return massRanges_;
}

} // namespace

ScanEventPtr RawFileImpl::getScanEvent(long index)
{
    /*::XRawfile::IXVirMSPtr virms("XRawfile2.XVirMS.1");
    wchar_t* wfilename = new wchar_t[filename_.length()+1];
    for(size_t i=0; i < filename_.length(); ++i)
        wfilename[i] = filename_[i];
    wfilename[filename_.length()] = 0;
    checkResult(virms->Create(wfilename), "create XVirMS");
    checkResult(virms->InitMethodScanEvents(), "init method scan events");
    MS_ScanEvent* rawScanEvent = new MS_ScanEvent;
    checkResult(virms->InitializeScanEvent(rawScanEvent), "init scan event");
    checkResult(virms->SetMethodScanEvent(0, index, rawScanEvent), "set method scan event");
    checkResult(virms->WriteMethodScanEvents(), "write method scan events");
    virms->InitializeScanIndex(0, MS_PacketTypes_MS_ANALOG_TYPE);
    MS_ScanIndex rawScanIndex;
    virms->WriteScanIndex2(&rawScanIndex);
    checkResult(virms->Close(), "close XVirMS");*/
    /*auto_ptr<LabelValueArray> foo = getInstrumentMethods();
    for(size_t i=0, end=foo->size(); i < end; ++i)
        cout << foo->label(i) << ": " << foo->value(i) << endl;*/
    return ScanEventPtr();//new ScanEventImpl(0));
}

