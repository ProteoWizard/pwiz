//
// AgilentDataReader.cpp
//
//
// Original author: Brendan MacLean <brendanx .@. u.washington.edu>
//
// Copyright 2009 University of Washington - Seattle, WA
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


#define AGILENTFILEREADER_SOURCE

#pragma unmanaged
#include "AgilentDataReader.hpp"
#include <iostream>
using namespace std;

//#ifdef _MANAGED
#pragma managed
#include <gcroot.h>
#define GCHANDLE(T) gcroot<T>
#using "BaseCommon.dll"
#using "BaseDataAccess.dll"
#using "MassSpecDataReader.dll"

using System::String;
using System::Math;
using System::Console;
using namespace System;
//using namespace System::Xml;
using namespace Agilent::MassSpectrometry::DataAnalysis;
//#else
//#define GCHANDLE(T) intptr_t
//#endif


// forwards managed exception to unmanaged code
#define CATCH_AND_FORWARD(x) \
    try \
    { x } \
    catch (System::ApplicationException^ e) \
    { throw std::runtime_error(ToStdString(e->Message)); }

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

template<typename value_type>
std::vector<value_type> ToStdVector(cli::array<value_type>^ valueArray)
{
    if (valueArray->Length == 0)
        return std::vector<value_type>();
    pin_ptr<value_type> pin = &valueArray[0];
    value_type* begin = (value_type*) pin;
    return std::vector<value_type>(begin, begin + valueArray->Length);
}

}

namespace pwiz {
namespace agilent {

class AgilentDataReaderImpl : public AgilentDataReader
{
    public:
    AgilentDataReaderImpl(const std::string& path);
    ~AgilentDataReaderImpl();

    virtual std::string getVersion() const;
    virtual DeviceType getDeviceType() const;
    virtual std::string getDeviceName(DeviceType deviceType) const;
    virtual double getAcquisitionTime() const;
    virtual IonizationMode getIonModes() const;
    virtual MSScanType getScanTypes() const;
    virtual MSStorageMode getSpectraFormat() const;
    virtual long getTotalScansPresent() const;

    virtual vector<Transition> getMRMTransitions() const;
    virtual vector<double> getSIMIons() const;

    virtual std::vector<double> getTicTimes() const;
    virtual std::vector<double> getBpcTimes() const;
    virtual std::vector<float> getTicIntensities() const;
    virtual std::vector<float> getBpcIntensities() const;

    virtual ChromatogramPtr getChromatogram(int index, ChromType type) const;
    virtual SpectrumPtr getSpectrum(int index, bool centroid = false) const;

    private:
    GCHANDLE(IMsdrDataReader^) reader_;
    GCHANDLE(IBDAMSScanFileInformation^) scanFileInfo_;
    vector<double> ticTimes_, bpcTimes_;
    vector<float> ticIntensities_, bpcIntensities_;
};

typedef boost::shared_ptr<AgilentDataReaderImpl> AgilentDataReaderImplPtr;

struct SpectrumImpl : public Spectrum
{
    SpectrumImpl(IBDASpecData^ specData);
    ~SpectrumImpl();

    virtual MSScanType getMSScanType() const;
    virtual MSStorageMode getMSStorageMode() const;
    virtual IonPolarity getIonPolarity() const;
    virtual DeviceType getDeviceType() const;
    virtual MassRange getMeasuredMassRange() const;
    virtual long getParentScanId() const;
    virtual std::vector<double> getPrecursorIon(int& precursorCount) const;
    virtual bool getPrecursorCharge(int& charge) const;
    virtual bool getPrecursorIntensity(double& precursorIntensity) const;
    virtual double getCollisionEnergy() const;
    virtual int getScanId() const;
    virtual int getTotalDataPoints() const;
    virtual std::vector<double> getXArray() const;
    virtual std::vector<float> getYArray() const;

    private:
    GCHANDLE(IBDASpecData^) specData_;
};

typedef boost::shared_ptr<SpectrumImpl> SpectrumImplPtr;

struct ChromatogramImpl : public Chromatogram
{
    ChromatogramImpl(IBDAChromData^ chromData);
    ~ChromatogramImpl();

    virtual double getCollisionEnergy() const;
    virtual int getTotalDataPoints() const;
    virtual vector<double> getXArray() const;
    virtual vector<float> getYArray() const;

    private:
    GCHANDLE(IBDAChromData^) chromData_;
};

typedef boost::shared_ptr<ChromatogramImpl> ChromatogramImplPtr;

Agilent::MassSpectrometry::DataAnalysis::DeviceType ToAssemblyDeviceType(DeviceType deviceType)
{
    return static_cast<Agilent::MassSpectrometry::DataAnalysis::DeviceType>(deviceType);
}

Agilent::MassSpectrometry::DataAnalysis::ChromType ToAssemblyChromType(ChromType chromType)
{
    return static_cast<Agilent::MassSpectrometry::DataAnalysis::ChromType>(chromType);
}

DeviceType ToDeviceType(Agilent::MassSpectrometry::DataAnalysis::DeviceType deviceType)
{
    return static_cast<DeviceType>(deviceType);
}

IonizationMode ToIonModes(Agilent::MassSpectrometry::DataAnalysis::IonizationMode ionModes)
{
    return static_cast<IonizationMode>(ionModes);
}

MSScanType ToScanTypes(Agilent::MassSpectrometry::DataAnalysis::MSScanType scanTypes)
{
    return static_cast<MSScanType>(scanTypes);
}

MSStorageMode ToStorageMode(Agilent::MassSpectrometry::DataAnalysis::MSStorageMode storageMode)
{
    return static_cast<MSStorageMode>(storageMode);
}

IonPolarity ToIonPolarity(Agilent::MassSpectrometry::DataAnalysis::IonPolarity ionPolarity)
{
    return static_cast<IonPolarity>(ionPolarity);
}

ChromType ToChromType(Agilent::MassSpectrometry::DataAnalysis::ChromType chromType)
{
    return static_cast<ChromType>(chromType);
}

AGILENTDATAREADER_API
AgilentDataReaderPtr AgilentDataReader::create(const string& path)
{
    AgilentDataReaderImplPtr dataReader(new AgilentDataReaderImpl(path));
    return boost::static_pointer_cast<AgilentDataReader>(dataReader);
}

AgilentDataReaderImpl::AgilentDataReaderImpl(const std::string& path)
{
    CATCH_AND_FORWARD
    (
        reader_ = gcnew MassSpecDataReader();
        if (!reader_->OpenDataFile(gcnew String(path.c_str())))
            throw std::runtime_error("[AgilentDataReaderImpl::ctor()] Error opening source path.");

        scanFileInfo_ = reader_->MSScanFileInformation;

        // cycle summing can make the full file chromatograms have the wrong number of points
        IBDAChromFilter^ filter = gcnew BDAChromFilter();
        filter->DoCycleSum = false;

        // set filter for TIC
        filter->ChromatogramType = ToAssemblyChromType(ChromType_TotalIon);
        IBDAChromData^ tic = reader_->GetChromatogram(filter)[0];
        ticTimes_ = ToStdVector(tic->XArray);
        ticIntensities_ = ToStdVector(tic->YArray);

        // set filter for BPC
        filter->ChromatogramType = ToAssemblyChromType(ChromType_BasePeak);
        IBDAChromData^ bpc = reader_->GetChromatogram(filter)[0];
        bpcTimes_ = ToStdVector(bpc->XArray);
        bpcIntensities_ = ToStdVector(bpc->YArray);
    )
}

AgilentDataReaderImpl::~AgilentDataReaderImpl()
{
    CATCH_AND_FORWARD
    (
        reader_->CloseDataFile();
    )
}

std::string AgilentDataReaderImpl::getVersion() const
{
    CATCH_AND_FORWARD( return ToStdString(reader_->Version); )
}

DeviceType AgilentDataReaderImpl::getDeviceType() const
{
    CATCH_AND_FORWARD( return ToDeviceType(scanFileInfo_->DeviceType); )
}

std::string AgilentDataReaderImpl::getDeviceName(DeviceType deviceType) const
{
    CATCH_AND_FORWARD
    (
        return ToStdString(reader_->FileInformation->GetDeviceName(ToAssemblyDeviceType(deviceType)));
    )
}

double AgilentDataReaderImpl::getAcquisitionTime() const
{
    CATCH_AND_FORWARD( return reader_->FileInformation->AcquisitionTime.ToOADate(); )
}

IonizationMode AgilentDataReaderImpl::getIonModes() const
{
    CATCH_AND_FORWARD( return ToIonModes(scanFileInfo_->IonModes); )
}

MSScanType AgilentDataReaderImpl::getScanTypes() const
{
    CATCH_AND_FORWARD( return ToScanTypes(scanFileInfo_->ScanTypes); )
}

MSStorageMode AgilentDataReaderImpl::getSpectraFormat() const
{
    CATCH_AND_FORWARD( return ToStorageMode(scanFileInfo_->SpectraFormat); )
}

long AgilentDataReaderImpl::getTotalScansPresent() const
{
    CATCH_AND_FORWARD( return scanFileInfo_->TotalScansPresent; )
}

vector<Transition> AgilentDataReaderImpl::getMRMTransitions() const
{
    CATCH_AND_FORWARD
    (
        cli::array<IRange^>^ transitions = scanFileInfo_->MRMTransitions;
        vector<Transition> trans;
        trans.reserve(transitions->Length);
        for each (IRange^ transition in transitions)
        {
            Transition tran;
            tran.precursor = transition->Start;
            tran.product = transition->End;
            trans.push_back(tran);
        }
        return trans;
    )
}

vector<double> AgilentDataReaderImpl::getSIMIons() const
{
    CATCH_AND_FORWARD( return ToStdVector(scanFileInfo_->SIMIons); )
}

std::vector<double> AgilentDataReaderImpl::getTicTimes() const
{
    return ticTimes_;
}

std::vector<double> AgilentDataReaderImpl::getBpcTimes() const
{
    return bpcTimes_;
}

std::vector<float> AgilentDataReaderImpl::getTicIntensities() const
{
    return ticIntensities_;
}

std::vector<float> AgilentDataReaderImpl::getBpcIntensities() const
{
    return bpcIntensities_;
}

ChromatogramPtr AgilentDataReaderImpl::getChromatogram(int index, ChromType type) const
{
//    CATCH_AND_FORWARD
//    (
        IBDAChromFilter^ filter = gcnew BDAChromFilter();
        filter->ChromatogramType = ToAssemblyChromType(type);
        filter->SingleChromatogramForAllMasses = false;
        filter->ExtractOneChromatogramPerScanSegment = true;

        ChromatogramImplPtr chromatogramPtr(new ChromatogramImpl(reader_->GetChromatogram(filter)[index-1]));
        return chromatogramPtr;
//    )
}

SpectrumPtr AgilentDataReaderImpl::getSpectrum(int index, bool centroid /*= false*/) const
{
    CATCH_AND_FORWARD
    (
        SpectrumImplPtr spectrumPtr(new SpectrumImpl(reader_->GetSpectrum(index, nullptr, nullptr)));
        return spectrumPtr;
    )
}

SpectrumImpl::SpectrumImpl(IBDASpecData^ specData)
{
    specData_ = specData;
}

SpectrumImpl::~SpectrumImpl()
{
}

MSScanType SpectrumImpl::getMSScanType() const
{
    CATCH_AND_FORWARD( return ToScanTypes(specData_->MSScanType); )
}

MSStorageMode SpectrumImpl::getMSStorageMode() const
{
    CATCH_AND_FORWARD( return ToStorageMode(specData_->MSStorageMode); )
}

IonPolarity SpectrumImpl::getIonPolarity() const
{
    CATCH_AND_FORWARD( return ToIonPolarity(specData_->IonPolarity); )
}

DeviceType SpectrumImpl::getDeviceType() const
{
    CATCH_AND_FORWARD( return ToDeviceType(specData_->DeviceType); )
}

MassRange SpectrumImpl::getMeasuredMassRange() const
{
    CATCH_AND_FORWARD
    ( 
        IRange^ massRange = specData_->MeasuredMassRange;
        MassRange mr;
        mr.start = massRange->Start;
        mr.end = massRange->End;
        return mr;
    )
}

long SpectrumImpl::getParentScanId() const
{
    CATCH_AND_FORWARD( return specData_->ParentScanId; )
}

std::vector<double> SpectrumImpl::getPrecursorIon(int& precursorCount) const
{
    CATCH_AND_FORWARD( return ToStdVector(specData_->GetPrecursorIon(precursorCount)); )
}

bool SpectrumImpl::getPrecursorCharge(int& charge) const
{
    CATCH_AND_FORWARD( return specData_->GetPrecursorCharge(charge); )
}

bool SpectrumImpl::getPrecursorIntensity(double& precursorIntensity) const
{
    CATCH_AND_FORWARD( return specData_->GetPrecursorIntensity(precursorIntensity); )
}

double SpectrumImpl::getCollisionEnergy() const
{
    CATCH_AND_FORWARD( return specData_->CollisionEnergy; )
}

int SpectrumImpl::getScanId() const
{
    CATCH_AND_FORWARD( return specData_->ScanId; )
}

int SpectrumImpl::getTotalDataPoints() const
{
    CATCH_AND_FORWARD( return specData_->TotalDataPoints; )
}

std::vector<double> SpectrumImpl::getXArray() const
{
    CATCH_AND_FORWARD( return ToStdVector(specData_->XArray); )
}

std::vector<float> SpectrumImpl::getYArray() const
{
    CATCH_AND_FORWARD( return ToStdVector(specData_->YArray); )
}

ChromatogramImpl::ChromatogramImpl(IBDAChromData^ chromData)
{
    chromData_ = chromData;
}

ChromatogramImpl::~ChromatogramImpl()
{
}

double ChromatogramImpl::getCollisionEnergy() const
{
    CATCH_AND_FORWARD( return chromData_->CollisionEnergy; )
}

int ChromatogramImpl::getTotalDataPoints() const
{
    CATCH_AND_FORWARD( return chromData_->TotalDataPoints; )
}

std::vector<double> ChromatogramImpl::getXArray() const
{
    CATCH_AND_FORWARD( return ToStdVector(chromData_->XArray); )
}

std::vector<float> ChromatogramImpl::getYArray() const
{
    CATCH_AND_FORWARD( return ToStdVector(chromData_->YArray); )
}

} // wiff
} // pwiz
