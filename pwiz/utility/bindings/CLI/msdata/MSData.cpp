//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#include "MSData.hpp"
#include "pwiz/data/msdata/SpectrumListBase.hpp"
#include "pwiz/data/msdata/ChromatogramListBase.hpp"


using System::Exception;
using System::String;
using boost::shared_ptr;


namespace b = pwiz::msdata;


namespace pwiz {
namespace CLI {
namespace msdata {


/// <summary>
/// version information for the msdata namespace
/// </summary>
public ref class Version
{
    public:
    static int Major() {return pwiz::msdata::Version::Major();}
    static int Minor() {return pwiz::msdata::Version::Minor();}
    static int Revision() {return pwiz::msdata::Version::Revision();}
    static System::String^ LastModified() {return ToSystemString(pwiz::msdata::Version::LastModified());}
    static System::String^ ToString() {return ToSystemString(pwiz::msdata::Version::str());}
};


FileContent::FileContent()
: ParamContainer(new b::FileContent())
{owner_ = nullptr; base_ = static_cast<b::FileContent*>(ParamContainer::base_); LOG_CONSTRUCT(__FUNCTION__)}


SourceFile::SourceFile()
: ParamContainer(new b::SourceFile())
{base_ = new boost::shared_ptr<b::SourceFile>(static_cast<b::SourceFile*>(ParamContainer::base_)); LOG_CONSTRUCT(__FUNCTION__)}

SourceFile::SourceFile(System::String^ _id)
: ParamContainer(new b::SourceFile(ToStdString(_id)))
{base_ = new boost::shared_ptr<b::SourceFile>(static_cast<b::SourceFile*>(ParamContainer::base_)); LOG_CONSTRUCT(__FUNCTION__)}

SourceFile::SourceFile(System::String^ _id, System::String^ _name)
: ParamContainer(new b::SourceFile(ToStdString(_id), ToStdString(_name)))
{base_ = new boost::shared_ptr<b::SourceFile>(static_cast<b::SourceFile*>(ParamContainer::base_)); LOG_CONSTRUCT(__FUNCTION__)}

SourceFile::SourceFile(System::String^ _id, System::String^ _name, System::String^ _location)
: ParamContainer(new b::SourceFile(ToStdString(_id), ToStdString(_name), ToStdString(_location)))
{base_ = new boost::shared_ptr<b::SourceFile>(static_cast<b::SourceFile*>(ParamContainer::base_)); LOG_CONSTRUCT(__FUNCTION__)}

System::String^ SourceFile::id::get() {return ToSystemString((*base_)->id);}
void SourceFile::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

System::String^ SourceFile::name::get() {return ToSystemString((*base_)->name);}
void SourceFile::name::set(System::String^ value) {(*base_)->name = ToStdString(value);}

System::String^ SourceFile::location::get() {return ToSystemString((*base_)->location);}
void SourceFile::location::set(System::String^ value) {(*base_)->location = ToStdString(value);}

bool SourceFile::empty()
{
    return (*base_)->empty();
}


Contact::Contact()
: ParamContainer(new b::Contact())
{owner_ = nullptr; base_ = static_cast<b::Contact*>(ParamContainer::base_); LOG_CONSTRUCT(__FUNCTION__)}


FileDescription::FileDescription()
: base_(new b::FileDescription())
{owner_ = nullptr; LOG_CONSTRUCT(__FUNCTION__)}

FileContent^ FileDescription::fileContent::get() {return gcnew FileContent(&base_->fileContent, this);}
SourceFileList^ FileDescription::sourceFiles::get() {return gcnew SourceFileList(&base_->sourceFilePtrs, this);}
ContactList^ FileDescription::contacts::get() {return gcnew ContactList(&base_->contacts, this);}

bool FileDescription::empty()
{
    return base_->empty();
}


Sample::Sample()
: ParamContainer(new b::Sample())
{base_ = new boost::shared_ptr<b::Sample>(static_cast<b::Sample*>(ParamContainer::base_)); LOG_CONSTRUCT(__FUNCTION__)}

Sample::Sample(System::String^ _id)
: ParamContainer(new b::Sample(ToStdString(_id)))
{base_ = new boost::shared_ptr<b::Sample>(static_cast<b::Sample*>(ParamContainer::base_)); LOG_CONSTRUCT(__FUNCTION__)}

Sample::Sample(System::String^ _id, System::String^ _name)
: ParamContainer(new b::Sample(ToStdString(_id), ToStdString(_name)))
{base_ = new boost::shared_ptr<b::Sample>(static_cast<b::Sample*>(ParamContainer::base_)); LOG_CONSTRUCT(__FUNCTION__)}

System::String^ Sample::id::get() {return ToSystemString((*base_)->id);}
void Sample::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

System::String^ Sample::name::get() {return ToSystemString((*base_)->name);}
void Sample::name::set(System::String^ value) {(*base_)->name = ToStdString(value);}

bool Sample::empty()
{
    return (*base_)->empty();
}


Component::Component()
: ParamContainer(new b::Component())
{owner_ = nullptr; base_ = static_cast<b::Component*>(ParamContainer::base_); LOG_CONSTRUCT(__FUNCTION__)}

Component::Component(ComponentType type, int order)
: ParamContainer(new b::Component((b::ComponentType) type, order))
{owner_ = nullptr; base_ = static_cast<b::Component*>(ParamContainer::base_); LOG_CONSTRUCT(__FUNCTION__)}

Component::Component(CVID cvid, int order)
: ParamContainer(new b::Component((pwiz::cv::CVID) cvid, order))
{owner_ = nullptr; base_ = static_cast<b::Component*>(ParamContainer::base_); LOG_CONSTRUCT(__FUNCTION__)}

ComponentType Component::type::get() {return (ComponentType) base_->type;}
void Component::type::set(ComponentType value) {base_->type = (pwiz::msdata::ComponentType) value;}

int Component::order::get() {return base_->order;}
void Component::order::set(int value) {base_->order = value;}

void Component::define(CVID cvid, int order)
{
    base_->define((pwiz::cv::CVID) cvid, order);
}

bool Component::empty()
{
    return base_->empty();
}


ComponentList::ComponentList()
: ComponentBaseList(new b::ComponentList())
{owner_ = nullptr; base_ = static_cast<b::ComponentList*>(ComponentBaseList::base_); LOG_CONSTRUCT(__FUNCTION__)}

Component^ ComponentList::source(int index)
{
    return gcnew Component(&base_->source((size_t) index), this);
}

Component^ ComponentList::analyzer(int index)
{
    return gcnew Component(&base_->analyzer((size_t) index), this);
}

Component^ ComponentList::detector(int index)
{
    return gcnew Component(&base_->detector((size_t) index), this);
}


Software::Software()
: base_(new boost::shared_ptr<b::Software>(new b::Software()))
{LOG_CONSTRUCT(__FUNCTION__)}

Software::Software(System::String^ _id)
: base_(new boost::shared_ptr<b::Software>(new b::Software(ToStdString(_id))))
{LOG_CONSTRUCT(__FUNCTION__)}

Software::Software(System::String^ _id, CVParam^ _softwareParam, System::String^ _softwareParamVersion)
: base_(new boost::shared_ptr<b::Software>(new b::Software(ToStdString(_id), _softwareParam->base(), ToStdString(_softwareParamVersion))))
{LOG_CONSTRUCT(__FUNCTION__)}

System::String^ Software::id::get() {return ToSystemString((*base_)->id);}
void Software::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

System::String^ Software::version::get() {return ToSystemString((*base_)->version);}
void Software::version::set(System::String^ value) {(*base_)->version = ToStdString(value);}

bool Software::empty()
{
    return (*base_)->empty();
}


InstrumentConfiguration::InstrumentConfiguration()
: ParamContainer(new b::InstrumentConfiguration())
{base_ = new boost::shared_ptr<b::InstrumentConfiguration>(static_cast<b::InstrumentConfiguration*>(ParamContainer::base_)); LOG_CONSTRUCT(__FUNCTION__)}

InstrumentConfiguration::InstrumentConfiguration(System::String^ _id)
: ParamContainer(new b::InstrumentConfiguration(ToStdString(_id)))
{base_ = new boost::shared_ptr<b::InstrumentConfiguration>(static_cast<b::InstrumentConfiguration*>(ParamContainer::base_)); LOG_CONSTRUCT(__FUNCTION__)}

System::String^ InstrumentConfiguration::id::get() {return ToSystemString((*base_)->id);}
void InstrumentConfiguration::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

ComponentList^ InstrumentConfiguration::componentList::get() {return gcnew ComponentList(&(*base_)->componentList, this);}
Software^ InstrumentConfiguration::software::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::msdata::SoftwarePtr, Software, (*base_)->softwarePtr);}

bool InstrumentConfiguration::empty()
{
    return (*base_)->empty();
}


ProcessingMethod::ProcessingMethod()
: ParamContainer(new b::ProcessingMethod())
{owner_ = nullptr; base_ = static_cast<b::ProcessingMethod*>(ParamContainer::base_); LOG_CONSTRUCT(__FUNCTION__)}

int ProcessingMethod::order::get() {return base_->order;}
void ProcessingMethod::order::set(int value) {base_->order = value;}
Software^ ProcessingMethod::software::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::msdata::SoftwarePtr, Software, base_->softwarePtr);}

bool ProcessingMethod::empty()
{
    return base_->empty();
}


DataProcessing::DataProcessing()
: base_(new boost::shared_ptr<b::DataProcessing>(new b::DataProcessing()))
{LOG_CONSTRUCT(__FUNCTION__)}

DataProcessing::DataProcessing(System::String^ _id)
: base_(new boost::shared_ptr<b::DataProcessing>(new b::DataProcessing(ToStdString(_id))))
{LOG_CONSTRUCT(__FUNCTION__)}

System::String^ DataProcessing::id::get() {return ToSystemString((*base_)->id);}
void DataProcessing::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

ProcessingMethodList^ DataProcessing::processingMethods::get() {return gcnew ProcessingMethodList(&(*base_)->processingMethods, this);}

bool DataProcessing::empty()
{
    return (*base_)->empty();
}


Target::Target()
: ParamContainer(new b::Target())
{owner_ = nullptr; base_ = static_cast<b::Target*>(ParamContainer::base_); LOG_CONSTRUCT(__FUNCTION__)}


ScanSettings::ScanSettings()
: base_(new boost::shared_ptr<b::ScanSettings>(new b::ScanSettings()))
{LOG_CONSTRUCT(__FUNCTION__)}

ScanSettings::ScanSettings(System::String^ _id)
: base_(new boost::shared_ptr<b::ScanSettings>(new b::ScanSettings(ToStdString(_id))))
{LOG_CONSTRUCT(__FUNCTION__)}

System::String^ ScanSettings::id::get() {return ToSystemString((*base_)->id);}
void ScanSettings::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

SourceFileList^ ScanSettings::sourceFiles::get() {return gcnew SourceFileList(&(*base_)->sourceFilePtrs, this);}

TargetList^ ScanSettings::targets::get() {return gcnew TargetList(&(*base_)->targets, this);}

bool ScanSettings::empty()
{
    return (*base_)->empty();
}


ScanWindowList^ Scan::scanWindows::get() {return gcnew ScanWindowList(&base_->scanWindows, this);}


ScanWindow::ScanWindow()
: ParamContainer(new b::ScanWindow())
{owner_ = nullptr; base_ = static_cast<b::ScanWindow*>(ParamContainer::base_); LOG_CONSTRUCT(__FUNCTION__)}

ScanWindow::ScanWindow(double low, double high, CVID unit)
: ParamContainer(new b::ScanWindow(low, high, (pwiz::cv::CVID) unit))
{owner_ = nullptr; base_ = static_cast<b::ScanWindow*>(ParamContainer::base_); LOG_CONSTRUCT(__FUNCTION__)}


Scan::Scan()
: ParamContainer(new b::Scan())
{owner_ = nullptr; base_ = static_cast<b::Scan*>(ParamContainer::base_); LOG_CONSTRUCT(__FUNCTION__)}

SourceFile^ Scan::sourceFile::get() {return NATIVE_SHARED_PTR_TO_CLI(b::SourceFilePtr, SourceFile, base_->sourceFilePtr);}
void Scan::sourceFile::set(SourceFile^ value) {base_->sourceFilePtr = CLI_TO_NATIVE_SHARED_PTR(b::SourceFilePtr, value);}

System::String^ Scan::spectrumID::get() {return ToSystemString(base_->spectrumID);}
void Scan::spectrumID::set(System::String^ value) {base_->spectrumID = ToStdString(value);}

System::String^ Scan::externalSpectrumID::get() {return ToSystemString(base_->externalSpectrumID);}
void Scan::externalSpectrumID::set(System::String^ value) {base_->externalSpectrumID = ToStdString(value);}

InstrumentConfiguration^ Scan::instrumentConfiguration::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::msdata::InstrumentConfigurationPtr, InstrumentConfiguration, base_->instrumentConfigurationPtr);}
void Scan::instrumentConfiguration::set(InstrumentConfiguration^ value) {base_->instrumentConfigurationPtr = CLI_TO_NATIVE_SHARED_PTR(b::InstrumentConfigurationPtr, value);}

bool Scan::empty()
{
    return base_->empty();
}


ScanList::ScanList()
: ParamContainer(new b::ScanList())
{owner_ = nullptr; base_ = static_cast<b::ScanList*>(ParamContainer::base_); LOG_CONSTRUCT(__FUNCTION__)}
	
Scans^ ScanList::scans::get() {return gcnew Scans(&base_->scans, this);}

bool ScanList::empty()
{
    return base_->empty();
}


IsolationWindow::IsolationWindow()
: ParamContainer(new b::IsolationWindow())
{owner_ = nullptr; base_ = static_cast<b::IsolationWindow*>(ParamContainer::base_); LOG_CONSTRUCT(__FUNCTION__)}


SelectedIon::SelectedIon()
: ParamContainer(new b::SelectedIon())
{owner_ = nullptr; base_ = static_cast<b::SelectedIon*>(ParamContainer::base_); LOG_CONSTRUCT(__FUNCTION__)}


Activation::Activation()
: ParamContainer(new b::Activation())
{owner_ = nullptr; base_ = static_cast<b::Activation*>(ParamContainer::base_); LOG_CONSTRUCT(__FUNCTION__)}


Precursor::Precursor()
: ParamContainer(new b::Precursor())
{owner_ = nullptr; base_ = static_cast<b::Precursor*>(ParamContainer::base_); LOG_CONSTRUCT(__FUNCTION__)}

SourceFile^ Precursor::sourceFile::get() {return NATIVE_SHARED_PTR_TO_CLI(b::SourceFilePtr, SourceFile, base_->sourceFilePtr);}
void Precursor::sourceFile::set(SourceFile^ value) {base_->sourceFilePtr = CLI_TO_NATIVE_SHARED_PTR(b::SourceFilePtr, value);}

System::String^ Precursor::spectrumID::get() {return ToSystemString(base_->spectrumID);}
void Precursor::spectrumID::set(System::String^ value) {base_->spectrumID = ToStdString(value);}

System::String^ Precursor::externalSpectrumID::get() {return ToSystemString(base_->externalSpectrumID);}
void Precursor::externalSpectrumID::set(System::String^ value) {base_->externalSpectrumID = ToStdString(value);}

IsolationWindow^ Precursor::isolationWindow::get() {return gcnew IsolationWindow(&base_->isolationWindow, this);}
void Precursor::isolationWindow::set(IsolationWindow^ value) {base_->isolationWindow = *value->base_;}

SelectedIonList^ Precursor::selectedIons::get() {return gcnew SelectedIonList(&base_->selectedIons, this);}

Activation^ Precursor::activation::get() {return gcnew Activation(&base_->activation, this);}
void Precursor::activation::set(Activation^ value) {base_->activation = *value->base_;}

bool Precursor::empty()
{
    return base_->empty();
}


Product::Product()
: base_(new b::Product())
{owner_ = nullptr; LOG_CONSTRUCT(__FUNCTION__)}

IsolationWindow^ Product::isolationWindow::get() {return gcnew IsolationWindow(&base_->isolationWindow, this);}
void Product::isolationWindow::set(IsolationWindow^ value) {base_->isolationWindow = *value->base_;}

bool Product::empty()
{
    return base_->empty();
}


BinaryDataArray::BinaryDataArray()
: ParamContainer(new b::BinaryDataArray())
{base_ = new boost::shared_ptr<b::BinaryDataArray>(static_cast<b::BinaryDataArray*>(ParamContainer::base_)); LOG_CONSTRUCT(__FUNCTION__)}

DataProcessing^ BinaryDataArray::dataProcessing::get() {return NATIVE_SHARED_PTR_TO_CLI(b::DataProcessingPtr, DataProcessing, (*base_)->dataProcessingPtr);}
void BinaryDataArray::dataProcessing::set(DataProcessing^ value) {(*base_)->dataProcessingPtr = CLI_TO_NATIVE_SHARED_PTR(b::DataProcessingPtr, value);}

pwiz::CLI::util::BinaryDataDouble^ BinaryDataArray::data::get() {return gcnew pwiz::CLI::util::BinaryDataDouble(&(*base_)->data, this);}
void BinaryDataArray::data::set(pwiz::CLI::util::BinaryDataDouble^ value) {(*base_)->data = value->base();}

bool BinaryDataArray::empty()
{
    return (*base_)->empty();
}


IntegerDataArray::IntegerDataArray()
: ParamContainer(new b::IntegerDataArray())
{base_ = new boost::shared_ptr<b::IntegerDataArray>(static_cast<b::IntegerDataArray*>(ParamContainer::base_)); LOG_CONSTRUCT(__FUNCTION__)}

DataProcessing^ IntegerDataArray::dataProcessing::get() {return NATIVE_SHARED_PTR_TO_CLI(b::DataProcessingPtr, DataProcessing, (*base_)->dataProcessingPtr);}
void IntegerDataArray::dataProcessing::set(DataProcessing^ value) {(*base_)->dataProcessingPtr = CLI_TO_NATIVE_SHARED_PTR(b::DataProcessingPtr, value);}

pwiz::CLI::util::BinaryDataInteger^ IntegerDataArray::data::get() {return gcnew pwiz::CLI::util::BinaryDataInteger(&(*base_)->data, this);}
void IntegerDataArray::data::set(pwiz::CLI::util::BinaryDataInteger^ value) {(*base_)->data = value->base();}

bool IntegerDataArray::empty()
{
    return (*base_)->empty();
}


MZIntensityPair::MZIntensityPair()
: base_(new pwiz::msdata::MZIntensityPair()) {LOG_CONSTRUCT(__FUNCTION__)}

MZIntensityPair::MZIntensityPair(double mz, double intensity)
: base_(new pwiz::msdata::MZIntensityPair(mz, intensity)) {LOG_CONSTRUCT(__FUNCTION__)}

double MZIntensityPair::mz::get() {return base_->mz;}
void MZIntensityPair::mz::set(double value) {base_->mz = value;}

double MZIntensityPair::intensity::get() {return base_->intensity;}
void MZIntensityPair::intensity::set(double value) {base_->intensity = value;}


TimeIntensityPair::TimeIntensityPair()
: base_(new pwiz::msdata::TimeIntensityPair()) {LOG_CONSTRUCT(__FUNCTION__)}

TimeIntensityPair::TimeIntensityPair(double mz, double intensity)
: base_(new pwiz::msdata::TimeIntensityPair(mz, intensity)) {LOG_CONSTRUCT(__FUNCTION__)}

double TimeIntensityPair::time::get() {return base_->time;}
void TimeIntensityPair::time::set(double value) {base_->time = value;}

double TimeIntensityPair::intensity::get() {return base_->intensity;}
void TimeIntensityPair::intensity::set(double value) {base_->intensity = value;}


SpectrumIdentity::SpectrumIdentity()
: base_(new pwiz::msdata::SpectrumIdentity()) {LOG_CONSTRUCT(__FUNCTION__)}

int SpectrumIdentity::index::get() {return (int) base_->index;}
void SpectrumIdentity::index::set(int value) {base_->index = (size_t) value;}

System::String^ SpectrumIdentity::id::get() {return ToSystemString(base_->id);}
void SpectrumIdentity::id::set(System::String^ value) {base_->id = ToStdString(value);}

System::String^ SpectrumIdentity::spotID::get() {return ToSystemString(base_->spotID);}
void SpectrumIdentity::spotID::set(System::String^ value) {base_->spotID = ToStdString(value);}

System::UInt64 SpectrumIdentity::sourceFilePosition::get() {return (System::UInt64) base_->sourceFilePosition;}
void SpectrumIdentity::sourceFilePosition::set(System::UInt64 value) {base_->sourceFilePosition = (size_t) value;}


ChromatogramIdentity::ChromatogramIdentity()
: base_(new pwiz::msdata::ChromatogramIdentity()) {LOG_CONSTRUCT(__FUNCTION__)}

int ChromatogramIdentity::index::get() {return (int) base_->index;}
void ChromatogramIdentity::index::set(int value) {base_->index = (size_t) value;}

System::String^ ChromatogramIdentity::id::get() {return ToSystemString(base_->id);}
void ChromatogramIdentity::id::set(System::String^ value) {base_->id = ToStdString(value);}

System::UInt64 ChromatogramIdentity::sourceFilePosition::get() {return (System::UInt64) base_->sourceFilePosition;}
void ChromatogramIdentity::sourceFilePosition::set(System::UInt64 value) {base_->sourceFilePosition = (size_t) value;}


Spectrum::Spectrum()
: ParamContainer(new b::Spectrum())
{base_ = new boost::shared_ptr<b::Spectrum>(static_cast<b::Spectrum*>(ParamContainer::base_)); LOG_CONSTRUCT(__FUNCTION__)}

int Spectrum::index::get() {return (int) (*base_)->index;}
void Spectrum::index::set(int value) {(*base_)->index = (size_t) value;}

System::String^ Spectrum::id::get() {return ToSystemString((*base_)->id);}
void Spectrum::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

System::String^ Spectrum::spotID::get() {return ToSystemString((*base_)->spotID);}
void Spectrum::spotID::set(System::String^ value) {(*base_)->spotID = ToStdString(value);}

System::UInt64 Spectrum::sourceFilePosition::get() {return (System::UInt64) (*base_)->sourceFilePosition;}
void Spectrum::sourceFilePosition::set(System::UInt64 value) {(*base_)->sourceFilePosition = (size_t) value;}

System::UInt64 Spectrum::defaultArrayLength::get() {return (System::UInt64) (*base_)->defaultArrayLength;}
void Spectrum::defaultArrayLength::set(System::UInt64 value) {(*base_)->defaultArrayLength = (size_t) value;}
 
DataProcessing^ Spectrum::dataProcessing::get() {return NATIVE_SHARED_PTR_TO_CLI(b::DataProcessingPtr, DataProcessing, (*base_)->dataProcessingPtr);}
void Spectrum::dataProcessing::set(DataProcessing^ value) {(*base_)->dataProcessingPtr = CLI_TO_NATIVE_SHARED_PTR(b::DataProcessingPtr, value);}

SourceFile^ Spectrum::sourceFile::get() {return NATIVE_SHARED_PTR_TO_CLI(b::SourceFilePtr, SourceFile, (*base_)->sourceFilePtr);}
void Spectrum::sourceFile::set(SourceFile^ value) {(*base_)->sourceFilePtr = CLI_TO_NATIVE_SHARED_PTR(b::SourceFilePtr, value);}

ScanList^ Spectrum::scanList::get() {return gcnew ScanList(&(*base_)->scanList, this);}
PrecursorList^ Spectrum::precursors::get() {return gcnew PrecursorList(&(*base_)->precursors, this);}
ProductList^ Spectrum::products::get() {return gcnew ProductList(&(*base_)->products, this);}

BinaryDataArrayList^ Spectrum::binaryDataArrays::get() {return gcnew BinaryDataArrayList(&(*base_)->binaryDataArrayPtrs, this);}
void Spectrum::binaryDataArrays::set(BinaryDataArrayList^ value) {(*base_)->binaryDataArrayPtrs = *value->base_;}

IntegerDataArrayList^ Spectrum::integerDataArrays::get() { return gcnew IntegerDataArrayList(&(*base_)->integerDataArrayPtrs, this); }
void Spectrum::integerDataArrays::set(IntegerDataArrayList^ value) { (*base_)->integerDataArrayPtrs = *value->base_; }

void Spectrum::getMZIntensityPairs(MZIntensityPairList^% output)
{
    try
    {
        std::vector<b::MZIntensityPair>* p = new std::vector<b::MZIntensityPair>();
        (*base_)->getMZIntensityPairs(*p);
        output = gcnew MZIntensityPairList(p);
    }
    CATCH_AND_FORWARD
}

BinaryDataArray^ Spectrum::getMZArray()
{
    try
    {
        auto arrayPtr = (*base_)->getMZArray();
        return arrayPtr ? gcnew BinaryDataArray(new b::BinaryDataArrayPtr(arrayPtr)) : nullptr;
    }
    CATCH_AND_FORWARD
}

BinaryDataArray^ Spectrum::getIntensityArray()
{
    try
    {
        auto arrayPtr = (*base_)->getIntensityArray();
        return arrayPtr ? gcnew BinaryDataArray(new b::BinaryDataArrayPtr(arrayPtr)) : nullptr;
    }
    CATCH_AND_FORWARD
}

BinaryDataArray^ Spectrum::getArrayByCVID(CVID arrayType)
{
    try
    {
        auto arrayPtr = (*base_)->getArrayByCVID((pwiz::cv::CVID) arrayType);
        return arrayPtr ? gcnew BinaryDataArray(new b::BinaryDataArrayPtr(arrayPtr)) : nullptr;
    }
    CATCH_AND_FORWARD
}

void Spectrum::setMZIntensityPairs(MZIntensityPairList^ input)
{
    (*base_)->setMZIntensityPairs(*input->base_, (pwiz::cv::CVID) CVID::CVID_Unknown);
}

void Spectrum::setMZIntensityPairs(MZIntensityPairList^ input, CVID intensityUnits)
{
    (*base_)->setMZIntensityPairs(*input->base_, (pwiz::cv::CVID) intensityUnits);
}

void Spectrum::setMZIntensityArrays(System::Collections::Generic::List<double>^ mzArray,
                                    System::Collections::Generic::List<double>^ intensityArray)
{
    setMZIntensityArrays(mzArray, intensityArray, CVID::CVID_Unknown);
}

void Spectrum::setMZIntensityArrays(System::Collections::Generic::List<double>^ mzArray,
                                    System::Collections::Generic::List<double>^ intensityArray,
                                    CVID intensityUnits)
{
    std::vector<double> mzVector;
    if (mzArray->Count > 0)
    {
        cli::array<double>^ mzArray2 = mzArray->ToArray();
        pin_ptr<double> mzArrayPinPtr = &mzArray2[0];
        double* mzArrayBegin = (double*) mzArrayPinPtr;
        mzVector.assign(mzArrayBegin, mzArrayBegin + mzArray2->Length);
    }

    std::vector<double> intensityVector;
    if (intensityArray->Count > 0)
    {
        cli::array<double>^ intensityArray2 = intensityArray->ToArray();
        pin_ptr<double> intensityArrayPinPtr = &intensityArray2[0];
        double* intensityArrayBegin = (double*) intensityArrayPinPtr;
        intensityVector.assign(intensityArrayBegin, intensityArrayBegin + intensityArray2->Length);
    }

    (*base_)->setMZIntensityArrays(mzVector, intensityVector, (pwiz::cv::CVID) intensityUnits);
}

bool Spectrum::empty()
{
    return (*base_)->empty();
}


Chromatogram::Chromatogram()
: ParamContainer(new b::Chromatogram())
{base_ = new boost::shared_ptr<b::Chromatogram>(static_cast<b::Chromatogram*>(ParamContainer::base_)); LOG_CONSTRUCT(__FUNCTION__)}

int Chromatogram::index::get() {return (int) (*base_)->index;}
void Chromatogram::index::set(int value) {(*base_)->index = (size_t) value;}

System::String^ Chromatogram::id::get() {return ToSystemString((*base_)->id);}
void Chromatogram::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

System::UInt64 Chromatogram::sourceFilePosition::get() {return (System::UInt64) (*base_)->sourceFilePosition;}
void Chromatogram::sourceFilePosition::set(System::UInt64 value) {(*base_)->sourceFilePosition = (size_t) value;}

System::UInt64 Chromatogram::defaultArrayLength::get() {return (*base_)->defaultArrayLength;}
void Chromatogram::defaultArrayLength::set(System::UInt64 value) {(*base_)->defaultArrayLength = (size_t) value;}
 
DataProcessing^ Chromatogram::dataProcessing::get()  {return NATIVE_SHARED_PTR_TO_CLI(pwiz::msdata::DataProcessingPtr, DataProcessing, (*base_)->dataProcessingPtr);}
//void set(DataProcessing^ value) {(*base_)->dataProcessingPtr = *value->base_;}

Precursor^ Chromatogram::precursor::get() {return gcnew Precursor(&(*base_)->precursor, this);}
void Chromatogram::precursor::set(Precursor^ value) {(*base_)->precursor = *value->base_;}

Product^ Chromatogram::product::get() {return gcnew Product(&(*base_)->product, this);}
void Chromatogram::product::set(Product^ value) {(*base_)->product = *value->base_;}

BinaryDataArrayList^ Chromatogram::binaryDataArrays::get() {return gcnew BinaryDataArrayList(&(*base_)->binaryDataArrayPtrs, this);}
void Chromatogram::binaryDataArrays::set(BinaryDataArrayList^ value) {(*base_)->binaryDataArrayPtrs = *value->base_;}

IntegerDataArrayList^ Chromatogram::integerDataArrays::get() { return gcnew IntegerDataArrayList(&(*base_)->integerDataArrayPtrs, this); }
void Chromatogram::integerDataArrays::set(IntegerDataArrayList^ value) { (*base_)->integerDataArrayPtrs = *value->base_; }

void Chromatogram::getTimeIntensityPairs(TimeIntensityPairList^% output)
{
    try
    {
        std::vector<b::TimeIntensityPair>* p = new std::vector<b::TimeIntensityPair>();
        (*base_)->getTimeIntensityPairs(*p);
        output = gcnew TimeIntensityPairList(p);
    }
    CATCH_AND_FORWARD
}

void Chromatogram::setTimeIntensityPairs(TimeIntensityPairList^ input, CVID timeUnits, CVID intensityUnits)
{
    try {(*base_)->setTimeIntensityPairs(*input->base_, (pwiz::cv::CVID) timeUnits, (pwiz::cv::CVID) intensityUnits);} CATCH_AND_FORWARD
}

BinaryDataArray^ Chromatogram::getTimeArray()
{
    try
    {
        auto arrayPtr = (*base_)->getTimeArray();
        return arrayPtr ? gcnew BinaryDataArray(new b::BinaryDataArrayPtr(arrayPtr)) : nullptr;
    }
    CATCH_AND_FORWARD
}

BinaryDataArray^ Chromatogram::getIntensityArray()
{
    try
    {
        auto arrayPtr = (*base_)->getIntensityArray();
        return arrayPtr ? gcnew BinaryDataArray(new b::BinaryDataArrayPtr(arrayPtr)) : nullptr;
    }
    CATCH_AND_FORWARD
}

bool Chromatogram::empty()
{
    return (*base_)->empty();
}


int SpectrumList::size()
{
    try{return (int) (*base_)->size();} CATCH_AND_FORWARD
}

bool SpectrumList::empty()
{
    try{return (*base_)->empty();} CATCH_AND_FORWARD
}

SpectrumIdentity^ SpectrumList::spectrumIdentity(int index)
{
    try {return gcnew SpectrumIdentity(&const_cast<b::SpectrumIdentity&>((*base_)->spectrumIdentity((size_t) index)), this);} CATCH_AND_FORWARD
}

int SpectrumList::find(System::String^ id)
{
    try {return (int) (*base_)->find(ToStdString(id));} CATCH_AND_FORWARD
}

int SpectrumList::findAbbreviated(System::String^ abbreviatedId)
{
    return findAbbreviated(abbreviatedId, '.');
}

int SpectrumList::findAbbreviated(System::String^ abbreviatedId, char delimiter)
{
    try {return (int) (*base_)->findAbbreviated(ToStdString(abbreviatedId), delimiter);} CATCH_AND_FORWARD
}

IndexList^ SpectrumList::findNameValue(System::String^ name, System::String^ value)
{
    try
    {
        b::IndexList indexList = (*base_)->findNameValue(ToStdString(name), ToStdString(value));
        std::vector<size_t>* ownedIndexListPtr = new std::vector<size_t>();
        ownedIndexListPtr->swap(indexList);
        return gcnew IndexList(ownedIndexListPtr);
    }
    CATCH_AND_FORWARD
}


Spectrum^ SpectrumList::spectrum(int index)
{
    return spectrum(index, false);
}

Spectrum^ SpectrumList::spectrum(int index, bool getBinaryData)
{
    try {return gcnew Spectrum(new b::SpectrumPtr((*base_)->spectrum((size_t) index, getBinaryData)));} CATCH_AND_FORWARD
}

Spectrum^ SpectrumList::spectrum(int index, DetailLevel detailLevel)
{
	try {return gcnew Spectrum(new b::SpectrumPtr((*base_)->spectrum((size_t) index, (b::DetailLevel)detailLevel)));} CATCH_AND_FORWARD
}

DataProcessing^ SpectrumList::dataProcessing()
{
    const shared_ptr<const b::DataProcessing> cdp = (*base_)->dataProcessingPtr();
    if (!cdp.get())
        return nullptr;
    b::DataProcessingPtr dp = boost::const_pointer_cast<b::DataProcessing>(cdp);
    return NATIVE_SHARED_PTR_TO_CLI(b::DataProcessingPtr, DataProcessing, dp);
}

void SpectrumList::setDataProcessing(DataProcessing^ dp)
{
    b::SpectrumListBase* sl = dynamic_cast<b::SpectrumListBase*>((*base_).get());
    if (sl) sl->setDataProcessingPtr(CLI_TO_NATIVE_SHARED_PTR(b::DataProcessingPtr, dp));
}


SpectrumListSimple::SpectrumListSimple()
: SpectrumList(new boost::shared_ptr<b::SpectrumList>(new b::SpectrumListSimple()))
{base_ = reinterpret_cast<boost::shared_ptr<b::SpectrumListSimple>*>(SpectrumList::base_); LOG_CONSTRUCT(__FUNCTION__)}

Spectra^ SpectrumListSimple::spectra::get() {return gcnew Spectra(&(*base_)->spectra, this);}
void SpectrumListSimple::spectra::set(Spectra^ value) {(*base_)->spectra = *value->base_;}

int SpectrumListSimple::size()
{
    try {return (*base_)->size();} CATCH_AND_FORWARD
}

bool SpectrumListSimple::empty()
{
    try {return (*base_)->empty();} CATCH_AND_FORWARD
}

SpectrumIdentity^ SpectrumListSimple::spectrumIdentity(int index)
{
    try {return gcnew SpectrumIdentity(&const_cast<b::SpectrumIdentity&>((*base_)->spectrumIdentity((size_t) index)), this);} CATCH_AND_FORWARD
}

Spectrum^ SpectrumListSimple::spectrum(int index)
{
    return spectrum(index, false);
}

Spectrum^ SpectrumListSimple::spectrum(int index, bool getBinaryData)
{
    try {return gcnew Spectrum(new b::SpectrumPtr((*base_)->spectrum((size_t) index, getBinaryData)));} CATCH_AND_FORWARD
}


int ChromatogramList::size()
{
    try {return (int) (*base_)->size();} CATCH_AND_FORWARD
}

bool ChromatogramList::empty()
{
    try {return (*base_)->empty();} CATCH_AND_FORWARD
}

ChromatogramIdentity^ ChromatogramList::chromatogramIdentity(int index)
{
    try {return gcnew ChromatogramIdentity(&const_cast<b::ChromatogramIdentity&>((*base_)->chromatogramIdentity((size_t) index)), this);} CATCH_AND_FORWARD
}

int ChromatogramList::find(System::String^ id)
{
    try {return (int) (*base_)->find(ToStdString(id));} CATCH_AND_FORWARD
}

Chromatogram^ ChromatogramList::chromatogram(int index)
{
    return chromatogram(index, false);
}

Chromatogram^ ChromatogramList::chromatogram(int index, bool getBinaryData)
{
    try {return gcnew Chromatogram(new b::ChromatogramPtr((*base_)->chromatogram((size_t) index, getBinaryData)));} CATCH_AND_FORWARD
}

Chromatogram^ ChromatogramList::chromatogram(int index, DetailLevel detailLevel)
{
    try { return gcnew Chromatogram(new b::ChromatogramPtr((*base_)->chromatogram((size_t)index, (b::DetailLevel) detailLevel))); } CATCH_AND_FORWARD
}

DataProcessing^ ChromatogramList::dataProcessing()
{
    const shared_ptr<const b::DataProcessing> cdp = (*base_)->dataProcessingPtr();
    if (!cdp.get())
        return nullptr;
    b::DataProcessingPtr dp = boost::const_pointer_cast<b::DataProcessing>(cdp);
    return NATIVE_SHARED_PTR_TO_CLI(b::DataProcessingPtr, DataProcessing, dp);
}

void ChromatogramList::setDataProcessing(DataProcessing^ dp)
{
    b::ChromatogramListBase* cl = dynamic_cast<b::ChromatogramListBase*>((*base_).get());
    if (cl) cl->setDataProcessingPtr(CLI_TO_NATIVE_SHARED_PTR(b::DataProcessingPtr, dp));
}


ChromatogramListSimple::ChromatogramListSimple()
: ChromatogramList(new boost::shared_ptr<b::ChromatogramList>(new b::ChromatogramListSimple()))
{base_ = reinterpret_cast<boost::shared_ptr<b::ChromatogramListSimple>*>(ChromatogramList::base_); LOG_CONSTRUCT(__FUNCTION__)}

Chromatograms^ ChromatogramListSimple::chromatograms::get() {return gcnew Chromatograms(&(*base_)->chromatograms, this);}
void ChromatogramListSimple::chromatograms::set(Chromatograms^ value) {(*base_)->chromatograms = *value->base_;}

int ChromatogramListSimple::size()
{
    try {return (*base_)->size();} CATCH_AND_FORWARD
}

bool ChromatogramListSimple::empty()
{
    try {return (*base_)->empty();} CATCH_AND_FORWARD
}

ChromatogramIdentity^ ChromatogramListSimple::chromatogramIdentity(int index)
{
    try {return gcnew ChromatogramIdentity(&const_cast<b::ChromatogramIdentity&>((*base_)->chromatogramIdentity((size_t) index)), this);} CATCH_AND_FORWARD
}

Chromatogram^ ChromatogramListSimple::chromatogram(int index)
{
    return chromatogram(index, false);
}

Chromatogram^ ChromatogramListSimple::chromatogram(int index, bool getBinaryData)
{
    try {return gcnew Chromatogram(new b::ChromatogramPtr((*base_)->chromatogram((size_t) index, getBinaryData)));} CATCH_AND_FORWARD
}


Run::Run()
: ParamContainer(new b::Run())
{owner_ = nullptr; base_ = static_cast<b::Run*>(ParamContainer::base_); LOG_CONSTRUCT(__FUNCTION__)}

System::String^ Run::id::get() {return ToSystemString(base_->id);}
void Run::id::set(System::String^ value) {base_->id = ToStdString(value);}

InstrumentConfiguration^ Run::defaultInstrumentConfiguration::get() {return NATIVE_SHARED_PTR_TO_CLI(b::InstrumentConfigurationPtr, InstrumentConfiguration, base_->defaultInstrumentConfigurationPtr);}
void Run::defaultInstrumentConfiguration::set(InstrumentConfiguration^ value) {base_->defaultInstrumentConfigurationPtr = CLI_TO_NATIVE_SHARED_PTR(b::InstrumentConfigurationPtr, value);}

Sample^ Run::sample::get() {return NATIVE_SHARED_PTR_TO_CLI(b::SamplePtr, Sample, base_->samplePtr);}
void Run::sample::set(Sample^ value) {base_->samplePtr = CLI_TO_NATIVE_SHARED_PTR(b::SamplePtr, value);}

System::String^ Run::startTimeStamp::get() {return ToSystemString(base_->startTimeStamp);}
void Run::startTimeStamp::set(System::String^ value) {base_->startTimeStamp = ToStdString(value);}

SourceFile^ Run::defaultSourceFile::get() {return NATIVE_SHARED_PTR_TO_CLI(b::SourceFilePtr, SourceFile, base_->defaultSourceFilePtr);}
void Run::defaultSourceFile::set(SourceFile^ value) {base_->defaultSourceFilePtr = CLI_TO_NATIVE_SHARED_PTR(b::SourceFilePtr, value);}

SpectrumList^ Run::spectrumList::get() {return NATIVE_OWNED_SHARED_PTR_TO_CLI(b::SpectrumListPtr, SpectrumList, base_->spectrumListPtr, this);}
void Run::spectrumList::set(SpectrumList^ value) {base_->spectrumListPtr = *value->base_;}

ChromatogramList^ Run::chromatogramList::get() {return NATIVE_OWNED_SHARED_PTR_TO_CLI(b::ChromatogramListPtr, ChromatogramList, base_->chromatogramListPtr, this);}
void Run::chromatogramList::set(ChromatogramList^ value) {base_->chromatogramListPtr = *value->base_;}

bool Run::empty()
{
    return base_->empty();
}

MSData::MSData(boost::shared_ptr<b::MSData>* base)
: base_(base)
{LOG_CONSTRUCT("MSData")}

MSData::~MSData()
{
    LOG_DESTRUCT("MSData", true) SAFEDELETE(base_);

    // MCC: forcing garbage collection is the best way I know of to try to clean up 
    //      reclaimable SpectrumList handles which hold on to SpectrumListPtrs
    System::GC::Collect();
    System::GC::WaitForPendingFinalizers();
}

MSData::!MSData() {LOG_FINALIZE("MSData") delete this;}
b::MSData& MSData::base() {return **base_;}

MSData::MSData()
: base_(new boost::shared_ptr<b::MSData>(new b::MSData))
{LOG_CONSTRUCT(__FUNCTION__)}

System::String^ MSData::accession::get() {return ToSystemString(base().accession);}
void MSData::accession::set(System::String^ value) {base().accession = ToStdString(value);}

System::String^ MSData::id::get() {return ToSystemString(base().id);}
void MSData::id::set(System::String^ value) {base().id = ToStdString(value);}

CVList^ MSData::cvs::get() {return gcnew CVList(&base().cvs, this);}
void MSData::cvs::set(CVList^ value) {cvs->assign(value);}

FileDescription^ MSData::fileDescription::get() {return gcnew FileDescription(&base().fileDescription, this);}
void MSData::fileDescription::set(FileDescription^ value) {base().fileDescription = *value->base_;}

ParamGroupList^ MSData::paramGroups::get() {return gcnew ParamGroupList(&base().paramGroupPtrs, this);}
void MSData::paramGroups::set(ParamGroupList^ value) {paramGroups->assign(value);}

SampleList^ MSData::samples::get() {return gcnew SampleList(&base().samplePtrs, this);}
void MSData::samples::set(SampleList^ value) {samples->assign(value);}

InstrumentConfigurationList^ MSData::instrumentConfigurationList::get() {return gcnew InstrumentConfigurationList(&base().instrumentConfigurationPtrs, this);}
void MSData::instrumentConfigurationList::set(InstrumentConfigurationList^ value) {instrumentConfigurationList->assign(value);}

SoftwareList^ MSData::softwareList::get() {return gcnew SoftwareList(&base().softwarePtrs, this);}
void MSData::softwareList::set(SoftwareList^ value) {softwareList->assign(value);}

DataProcessingList^ MSData::dataProcessingList::get() {return gcnew DataProcessingList(&base().dataProcessingPtrs, this);}
void MSData::dataProcessingList::set(DataProcessingList^ value) {dataProcessingList->assign(value);}

ScanSettingsList^ MSData::scanSettingsList::get() {return gcnew ScanSettingsList(&base().scanSettingsPtrs, this);}
void MSData::scanSettingsList::set(ScanSettingsList^ value) {scanSettingsList->assign(value);}

Run^ MSData::run::get()  {return gcnew Run(&base().run, this);}
//void set(Run^ value) {(*base_)->run = *value->base_;}

bool MSData::empty() {return base().empty();}
System::String^ MSData::version() {return ToSystemString(base().version());}




System::String^ id::value(System::String^ id, System::String^ name)
{
    try {return ToSystemString(b::id::value(ToStdString(id), ToStdString(name)));} CATCH_AND_FORWARD
}

CVID id::getDefaultNativeIDFormat(MSData^ msd)
{
    try {return (CVID) b::id::getDefaultNativeIDFormat(msd->base());} CATCH_AND_FORWARD
}

System::String^ id::translateScanNumberToNativeID(CVID nativeIDFormat, System::String^ scanNumber)
{
    try {return ToSystemString(b::id::translateScanNumberToNativeID((b::CVID) nativeIDFormat, ToStdString(scanNumber)));} CATCH_AND_FORWARD
}

System::String^ id::translateNativeIDToScanNumber(CVID nativeIDFormat, System::String^ id)
{
    try {return ToSystemString(b::id::translateNativeIDToScanNumber((b::CVID) nativeIDFormat, ToStdString(id)));} CATCH_AND_FORWARD
}

System::String^ id::abbreviate(System::String^ id)
{
    try {return ToSystemString(b::id::abbreviate(ToStdString(id)));} CATCH_AND_FORWARD
}

System::String^ id::abbreviate(System::String^ id, char delimiter)
{
    try {return ToSystemString(b::id::abbreviate(ToStdString(id), delimiter));} CATCH_AND_FORWARD
}


} // namespace msdata
} // namespace CLI
} // namespace pwiz
