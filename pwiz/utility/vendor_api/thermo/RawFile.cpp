//
// RawFile.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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

#define RAWFILE_SOURCE

#include "RawFile.h"
//#ifdef PWIZ_NO_EXCALIBUR_SDK
#include "xdk/XRawFile2.tlh" // use canned header
//#else
//#import "xdk/XRawFile2.dll" rename_namespace("XRawfile")
//#endif
#include "RawFileValues.h"
#include "RawFileCOM.h"
#include <iostream>
#include <map>
#include <sstream>
#include "boost/format.hpp"
#include "boost/shared_ptr.hpp"
#include <vector>
#include <algorithm>
#include "utility/misc/COMInitializer.hpp"


using namespace pwiz::raw;
using namespace pwiz::util;
using namespace XRawfile;
using namespace std;
using boost::shared_ptr;


namespace {
void checkResult(HRESULT hr, const string& msg = "")
{
    // note:
    // XRawfile seems to return 0 for success, >0 for failure;
    // COM standard: HRESULT is >=0 for success, <0 for failure

    if (hr == 0) // success
        return;

    ostringstream temp;
    temp << msg << "Failed HRESULT returned from COM object: " << hr;
    throw RawEgg(temp.str().c_str());
}
} // namespace


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

    virtual string getCreationDate();
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
                WhichMassList which);

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
    virtual auto_ptr<ScanInfo> getScanInfo(long scanNumber);
    virtual long getMSLevel(long scanNumber);
    virtual ErrorLogItem getErrorLogItem(long itemNumber);
    virtual auto_ptr<LabelValueArray> getTuneData(long segmentNumber);
    virtual auto_ptr<LabelValueArray> getInstrumentMethods();
    virtual auto_ptr<StringArray> getInstrumentChannelLabels();

    virtual InstrumentModelType getInstrumentModel();
    virtual const vector<IonizationType>& getIonSources();
    virtual const vector<MassAnalyzerType>& getMassAnalyzers();
    virtual const vector<DetectorType>& getDetectors();

    virtual auto_ptr<ChromatogramData>
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

    InstrumentModelType instrumentModel_;
    vector<IonizationType> ionSources_;
    vector<MassAnalyzerType> massAnalyzers_;
    vector<DetectorType> detectors_;
};


RAWFILE_API auto_ptr<RawFile> RawFile::create(const string& filename)
{
    return auto_ptr<RawFile>(new RawFileImpl(filename));
}


RawFileImpl::RawFileImpl(const string& filename)
:   raw_(NULL),
    filename_(filename),
    instrumentModel_(InstrumentModelType_Unknown)
{
    COMInitializer::initialize();

    // use the latest version of IXRawfile that will initialize
    IXRawfile2Ptr raw2(NULL);
    if (FAILED(raw2.CreateInstance("XRawfile.XRawfile.1")))
    {
        if (FAILED(raw_.CreateInstance("XRawfile.XRawfile.1")))
        {
            rawInterfaceVersion_ = 0;
			throw RawEgg("[RawFileImpl()] Unable to initialize XRawfile interface: is Thermo DLL installed?");
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

    if (raw_->Open(filename.c_str()))
        throw RawEgg("[RawFileImpl()] Unable to open file " + filename);
}


RawFileImpl::~RawFileImpl()
{
    raw_->Close();
    raw_ = NULL;
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


string RawFileImpl::getCreationDate()
{
    DATE date;
    checkResult(raw_->GetCreationDate(&date), "[RawFileImpl::getCreationDate(), GetCreationDate()] ");

    UDATE udate;
    HRESULT hr = VarUdateFromDate(date, 0, &udate);
    if (FAILED(hr))
        throw RawEgg("RawFileImpl::getCreationDate(): error converting date.\n");

    SYSTEMTIME& st = udate.st;
    ostringstream oss;
    oss << st.wMonth << "/" << st.wDay << "/" << st.wYear << " " <<
           st.wHour << ":" << st.wMinute << ":" << st.wSecond;
    return oss.str();
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
    if (instrumentModel_ == InstrumentModelType_Unknown)
    {
        string modelString = value(InstModel);
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
                                     WhichMassList which)
{
    _bstr_t bstrFilter(filter.c_str());
    double centroidPeakWidth = 0;
    VARIANT variantMassList;
    VariantInit(&variantMassList);
    VARIANT variantPeakFlags;
    VariantInit(&variantPeakFlags);
    long size = 0;

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
                           &size));

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
    VARIANT variantMassList;
    VariantInit(&variantMassList);
    VARIANT variantPeakFlags;
    VariantInit(&variantPeakFlags);
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
                                         &size));

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
	VARIANT varLabels;
	raw2->GetLabelData(&varLabels, NULL, &scanNumber);
    return MassListPtr(new MassListFromLabelDataImpl(scanNumber, varLabels));
}


auto_ptr<StringArray> RawFileImpl::getFilters()
{
    VARIANT v;
    VariantInit(&v);
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

    virtual long precursorCount() const {return precursorMZs_.size();}
    virtual long precursorCharge() const;
    virtual double precursorMZ(long index, bool preferMonoisotope) const;
    virtual double precursorActivationEnergy(long index) const {return precursorActivationEnergies_[index];}

    virtual long parentCount() const {return precursorCount();}
    virtual long parentCharge() const {return precursorCharge();}
    virtual double parentMass(long index, bool preferMonoisotope) const {return precursorMZ(index, preferMonoisotope);}
    virtual double parentEnergy(long index) const {return precursorActivationEnergy(index);}

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

    virtual long statusLogSize() const {return statusLogSize_;}
    virtual double statusLogRT() const {return statusLogRT_;}
    virtual std::string statusLogLabel(long index) const {return statusLogLabels_->item(index);}
    virtual std::string statusLogValue(long index) const {return statusLogValues_->item(index);}

    virtual long trailerExtraSize() const {return trailerExtraSize_;}
    virtual std::string trailerExtraLabel(long index) const {return trailerExtraLabels_->item(index);}
    virtual std::string trailerExtraValue(long index) const {return trailerExtraValues_->item(index);}
    virtual std::string trailerExtraValue(const string& name) const {return trailerExtraMap_[name];}
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
    vector<double> precursorMZs_;
    vector<double> precursorActivationEnergies_;
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

    long statusLogSize_;
    double statusLogRT_;
    auto_ptr<VariantStringArray> statusLogLabels_;
    auto_ptr<VariantStringArray> statusLogValues_;

    long trailerExtraSize_;
    auto_ptr<VariantStringArray> trailerExtraLabels_;
    auto_ptr<VariantStringArray> trailerExtraValues_;
    mutable map<string,string> trailerExtraMap_;

    void initialize();
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
    statusLogSize_(0),
    statusLogRT_(0),
    statusLogLabels_(0),
    statusLogValues_(0),
    trailerExtraSize_(0),
    trailerExtraLabels_(0),
    trailerExtraValues_(0)
{
    initialize();
    parseFilterString();
}

void ScanInfoImpl::initialize()
{
    IXRawfilePtr& raw_ = (*rawfile_).raw_;

    _bstr_t bstrFilter;
    checkResult(raw_->GetFilterForScanNum(scanNumber_, bstrFilter.GetAddress()), "[ScanInfoImpl::initialize(), GetFilterForScanNum()] ");
    filter_ = (const char*)(bstrFilter);

    long isProfileScan = 0;
    checkResult(raw_->IsProfileScanForScanNum(scanNumber_, &isProfileScan), "[ScanInfoImpl::initialize(), IsProfileScanForScanNum()] ");
    isProfileScan_ = (isProfileScan!=0);

    long isCentroidScan = 0;
    checkResult(raw_->IsCentroidScanForScanNum(scanNumber_, &isCentroidScan), "[ScanInfoImpl::initialize(), IsCentroidScanForScanNum()] ");
    isCentroidScan_ = (isCentroidScan!=0);

    long isUniformTime = 0;
    checkResult(raw_->GetScanHeaderInfoForScanNum(scanNumber_,
                                                  &packetCount_,
                                                  &startTime_,
                                                  &lowMass_,
                                                  &highMass_,
                                                  &totalIonCurrent_,
                                                  &basePeakMZ_,
                                                  &basePeakIntensity_,
                                                  &channelCount_,
                                                  &isUniformTime,
                                                  &frequency_),
                                                  "[ScanInfoImpl::initialize(), GetScanHeaderInfoForScanNum()] ");
    isUniformTime_ = (isUniformTime!=0);

    VARIANT variantStatusLogLabels;
    VariantInit(&variantStatusLogLabels);
    VARIANT variantStatusLogValues;
    VariantInit(&variantStatusLogValues);
    long statusLogSize = 0;

    checkResult(raw_->GetStatusLogForScanNum(scanNumber_,
                                             &statusLogRT_,
                                             &variantStatusLogLabels,
                                             &variantStatusLogValues,
                                             &statusLogSize),
                                             "[ScanInfoImpl::initialize(), GetStatusLogForScanNum()] ");
    statusLogSize_ = statusLogSize;
    statusLogLabels_ = auto_ptr<VariantStringArray>(new VariantStringArray(variantStatusLogLabels, statusLogSize));
    statusLogValues_ = auto_ptr<VariantStringArray>(new VariantStringArray(variantStatusLogValues, statusLogSize));

    VARIANT variantTrailerExtraLabels;
    VariantInit(&variantTrailerExtraLabels);
    VARIANT variantTrailerExtraValues;
    VariantInit(&variantTrailerExtraValues);
    long trailerExtraSize = 0;

    checkResult(raw_->GetTrailerExtraForScanNum(scanNumber_,
                                                &variantTrailerExtraLabels,
                                                &variantTrailerExtraValues,
                                                &trailerExtraSize),
                                                "[]");
    trailerExtraSize_ = trailerExtraSize;
    trailerExtraLabels_ = auto_ptr<VariantStringArray>(new VariantStringArray(variantTrailerExtraLabels, trailerExtraSize));
    trailerExtraValues_ = auto_ptr<VariantStringArray>(new VariantStringArray(variantTrailerExtraValues, trailerExtraSize));

    if (trailerExtraLabels_->size() != trailerExtraValues_->size())
        throw RawEgg("[ScanInfoImpl::initialize()]  Trailer Extra sizes do not match."); 

    for (int i=0; i<trailerExtraLabels_->size(); i++)
        trailerExtraMap_[trailerExtraLabels_->item(i)] = trailerExtraValues_->item(i);
}

void ScanInfoImpl::parseFilterString()
{
    ScanFilter filterParser;
    filterParser.parse(filter_);

    msLevel_ = filterParser.msLevel_;
    massAnalyzerType_ = convertScanFilterMassAnalyzer(filterParser.massAnalyzerType_, rawfile_->getInstrumentModel());
    ionizationType_ = filterParser.ionizationType_;
    polarityType_ = filterParser.polarityType_;
    scanType_ = filterParser.scanType_;
    activationType_ = filterParser.activationType_;
    precursorMZs_.insert(precursorMZs_.end(), filterParser.cidParentMass_.begin(), filterParser.cidParentMass_.end());
    precursorActivationEnergies_.insert(precursorActivationEnergies_.end(), filterParser.cidEnergy_.begin(), filterParser.cidEnergy_.end());
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
        double monoisotopicMZ;
        try
        {
            monoisotopicMZ = trailerExtraValueDouble("Monoisotopic M/Z:");
        }
        catch (RawEgg& )
        {
            monoisotopicMZ = 0;
        }
        if (monoisotopicMZ > 0)
            return monoisotopicMZ;
    }
    return precursorMZs_[index];
}

double ScanInfoImpl::trailerExtraValueDouble(const string& name) const
{
    IXRawfilePtr& raw_ = rawfile_->raw_;

    VARIANT v;
    VariantInit(&v);

    checkResult(raw_->GetTrailerExtraValueForScanNum(scanNumber_,
                                                     name.c_str(), 
                                                     &v),
                                                     "[ScanInfoImpl::trailerExtraValueDouble()] ");
    if (v.vt == VT_R4) return v.fltVal;
	else if (v.vt == VT_R8) return v.dblVal;

    throw RawEgg("[ScanInfoImpl::trailerExtraValueDouble()] Unknown type.");
}


long ScanInfoImpl::trailerExtraValueLong(const string& name) const
{
    IXRawfilePtr& raw_ = rawfile_->raw_;

    VARIANT v;
    VariantInit(&v);

    checkResult(raw_->GetTrailerExtraValueForScanNum(scanNumber_,
                                                     name.c_str(), 
                                                     &v),
                                                     "[ScanInfoImpl::trailerExtraValueLong()] ");
    if (v.vt == VT_I4) return v.lVal;
	else if (v.vt == VT_I2) return v.iVal;
	else if (v.vt == VT_INT) return v.intVal;

    throw RawEgg("[ScanInfoImpl::trailerExtraValueLong()] Unknown type.");
}


auto_ptr<ScanInfo> RawFileImpl::getScanInfo(long scanNumber)
{
    auto_ptr<ScanInfo> scanInfo(new ScanInfoImpl(scanNumber, this));
    return scanInfo;
}


long RawFileImpl::getMSLevel(long scanNumber)
{
    long result = 1;
    _bstr_t bstrFilter;
    checkResult(raw_->GetFilterForScanNum(scanNumber, bstrFilter.GetAddress()));
    const char* ms = strstr((const char*)(bstrFilter), "ms");
    if (ms && strlen(ms)>2 && ms[2]!=' ')
        result = atol(ms+2);
    return result;
}


ErrorLogItem RawFileImpl::getErrorLogItem(long itemNumber)
{
    ErrorLogItem result;
    _bstr_t bstr;
    checkResult(raw_->GetErrorLogItem(itemNumber, &result.rt, bstr.GetAddress()));
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

        VARIANT v;
        VariantInit(&v);
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
    VARIANT variantLabels;
    VariantInit(&variantLabels);
    long size = 0;

    checkResult(raw_->GetTuneDataLabels(segmentNumber, &variantLabels, &size));

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
            throw RawEgg("InstrumentMethodLabelValueArray: error");
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
    VARIANT variantLabels;
    VariantInit(&variantLabels);
    long size = 0;

    checkResult(raw_->GetInstMethodNames(&size, &variantLabels));

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


auto_ptr<ChromatogramData>
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
    VARIANT variantChromatogramData;
    VariantInit(&variantChromatogramData);
    VARIANT variantPeakFlags;
    VariantInit(&variantPeakFlags);
    long size = 0;

    checkResult(raw_->GetChroData(type1, op, type2,
                                  bstrFilter, bstrMassRanges1, bstrMassRanges2,
                                  delay, &startTime, &endTime,
                                  smoothingType, smoothingValue,
                                  &variantChromatogramData, &variantPeakFlags, &size));

    return auto_ptr<ChromatogramData>
        (new ChromatogramDataImpl(variantChromatogramData, size, startTime, endTime));
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
