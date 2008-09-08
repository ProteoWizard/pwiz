//
// MSData.cpp
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
//#include "../../../data/msdata/MSData.hpp"
#include "utility/misc/Exception.hpp"
using System::Exception;
using System::String;


namespace b = pwiz::msdata;

namespace pwiz {
namespace CLI {
namespace msdata {


CV::CV()
: base_(new b::CV()), owner_(nullptr)
{}

System::String^ CV::id::get() {return gcnew System::String(base_->id.c_str());}
void CV::id::set(System::String^ value) {base_->id = ToStdString(value);}

System::String^ CV::URI::get() {return gcnew System::String(base_->URI.c_str());}
void CV::URI::set(System::String^ value) {base_->URI = ToStdString(value);}

System::String^ CV::fullName::get() {return gcnew System::String(base_->fullName.c_str());}
void CV::fullName::set(System::String^ value) {base_->fullName = ToStdString(value);}

System::String^ CV::version::get() {return gcnew System::String(base_->version.c_str());}
void CV::version::set(System::String^ value) {base_->version = ToStdString(value);}



UserParam::UserParam()
: base_(new boost::shared_ptr<b::UserParam>(new b::UserParam())), owner_(nullptr)
{value_ = gcnew UserParamValue(base_);}

UserParam::UserParam(System::String^ _name)
: base_(new boost::shared_ptr<b::UserParam>(new b::UserParam(ToStdString(_name)))), owner_(nullptr)
{value_ = gcnew UserParamValue(base_);}

UserParam::UserParam(System::String^ _name, System::String^ _value)
: base_(new boost::shared_ptr<b::UserParam>(new b::UserParam(ToStdString(_name), ToStdString(_value)))), owner_(nullptr)
{value_ = gcnew UserParamValue(base_);}

UserParam::UserParam(System::String^ _name, System::String^ _value, System::String^ _type)
: base_(new boost::shared_ptr<b::UserParam>(new b::UserParam(ToStdString(_name), ToStdString(_value), ToStdString(_type)))), owner_(nullptr)
{value_ = gcnew UserParamValue(base_);}

UserParam::UserParam(System::String^ _name, System::String^ _value, System::String^ _type, CVID _units)
: base_(new boost::shared_ptr<b::UserParam>(new b::UserParam(ToStdString(_name), ToStdString(_value), ToStdString(_type), (b::CVID) _units))), owner_(nullptr)
{value_ = gcnew UserParamValue(base_);}


System::String^ UserParam::name::get() {return gcnew System::String((*base_)->name.c_str());}
void UserParam::name::set(System::String^ value) {(*base_)->name = ToStdString(value);}

System::String^ UserParam::type::get() {return gcnew System::String((*base_)->type.c_str());}
void UserParam::type::set(System::String^ value) {(*base_)->type = ToStdString(value);}

CVID UserParam::units::get() {return (CVID) (*base_)->units;}
void UserParam::units::set(CVID value) {(*base_)->units = (pwiz::msdata::CVID) value;}

UserParamValue^ UserParam::value::get() {return value_;}	


ParamGroupList^ ParamContainer::paramGroups::get() {return gcnew ParamGroupList(&base_->paramGroupPtrs, this);}
CVParamList^ ParamContainer::cvParams::get() {return gcnew CVParamList(&base_->cvParams, this);}
UserParamList^ ParamContainer::userParams::get() {return gcnew UserParamList(&base_->userParams, this);}

void ParamContainer::set(CVID cvid) {base_->set((pwiz::msdata::CVID) cvid);}
void ParamContainer::set(CVID cvid, System::String^ value) {base_->set((pwiz::msdata::CVID) cvid, ToStdString(value));}
void ParamContainer::set(CVID cvid, System::String^ value, CVID units) {base_->set((pwiz::msdata::CVID) cvid, ToStdString(value), (pwiz::msdata::CVID) units);}

void ParamContainer::set(CVID cvid, bool value) {set(cvid, (value ? "true" : "false"));}
void ParamContainer::set(CVID cvid, System::Int32 value) {set(cvid, value.ToString());}
void ParamContainer::set(CVID cvid, System::Int64 value) {set(cvid, value.ToString());}
void ParamContainer::set(CVID cvid, System::UInt32 value) {set(cvid, value.ToString());}
void ParamContainer::set(CVID cvid, System::UInt64 value) {set(cvid, value.ToString());}
void ParamContainer::set(CVID cvid, System::Single value) {set(cvid, value.ToString());}
void ParamContainer::set(CVID cvid, System::Double value) {set(cvid, value.ToString());}

void ParamContainer::set(CVID cvid, System::Int32 value, CVID units) {set(cvid, value.ToString(), units);}
void ParamContainer::set(CVID cvid, System::Int64 value, CVID units) {set(cvid, value.ToString(), units);}
void ParamContainer::set(CVID cvid, System::UInt32 value, CVID units) {set(cvid, value.ToString(), units);}
void ParamContainer::set(CVID cvid, System::UInt64 value, CVID units) {set(cvid, value.ToString(), units);}
void ParamContainer::set(CVID cvid, System::Single value, CVID units) {set(cvid, value.ToString(), units);}
void ParamContainer::set(CVID cvid, System::Double value, CVID units) {set(cvid, value.ToString(), units);}


ParamGroup::ParamGroup()
: ParamContainer(new b::ParamGroup())
{base_ = new boost::shared_ptr<b::ParamGroup>(static_cast<b::ParamGroup*>(ParamContainer::base_));}

ParamGroup::ParamGroup(System::String^ _id)
: ParamContainer(new b::ParamGroup(ToStdString(_id)))
{base_ = new boost::shared_ptr<b::ParamGroup>(static_cast<b::ParamGroup*>(ParamContainer::base_));}

System::String^ ParamGroup::id::get() {return gcnew System::String((*base_)->id.c_str());}
void ParamGroup::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}


FileContent::FileContent()
: ParamContainer(new b::FileContent()), owner_(nullptr)
{base_ = static_cast<b::FileContent*>(ParamContainer::base_);}


SourceFile::SourceFile()
: ParamContainer(new b::SourceFile())
{base_ = new boost::shared_ptr<b::SourceFile>(static_cast<b::SourceFile*>(ParamContainer::base_));}

SourceFile::SourceFile(System::String^ _id)
: ParamContainer(new b::SourceFile(ToStdString(_id)))
{base_ = new boost::shared_ptr<b::SourceFile>(static_cast<b::SourceFile*>(ParamContainer::base_));}

SourceFile::SourceFile(System::String^ _id, System::String^ _name)
: ParamContainer(new b::SourceFile(ToStdString(_id), ToStdString(_name)))
{base_ = new boost::shared_ptr<b::SourceFile>(static_cast<b::SourceFile*>(ParamContainer::base_));}

SourceFile::SourceFile(System::String^ _id, System::String^ _name, System::String^ _location)
: ParamContainer(new b::SourceFile(ToStdString(_id), ToStdString(_name), ToStdString(_location)))
{base_ = new boost::shared_ptr<b::SourceFile>(static_cast<b::SourceFile*>(ParamContainer::base_));}

System::String^ SourceFile::id::get() {return gcnew System::String((*base_)->id.c_str());}
void SourceFile::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

System::String^ SourceFile::name::get() {return gcnew System::String((*base_)->name.c_str());}
void SourceFile::name::set(System::String^ value) {(*base_)->name = ToStdString(value);}

System::String^ SourceFile::location::get() {return gcnew System::String((*base_)->location.c_str());}
void SourceFile::location::set(System::String^ value) {(*base_)->location = ToStdString(value);}


Contact::Contact()
: ParamContainer(new b::Contact()), owner_(nullptr)
{base_ = static_cast<b::Contact*>(ParamContainer::base_);}


FileDescription::FileDescription()
: base_(new b::FileDescription()), owner_(nullptr)
{}

FileContent^ FileDescription::fileContent::get() {return gcnew FileContent(&base_->fileContent, this);}
SourceFileList^ FileDescription::sourceFiles::get() {return gcnew SourceFileList(&base_->sourceFilePtrs, this);}
ContactList^ FileDescription::contacts::get() {return gcnew ContactList(&base_->contacts, this);}


Sample::Sample()
: ParamContainer(new b::Sample())
{base_ = new boost::shared_ptr<b::Sample>(static_cast<b::Sample*>(ParamContainer::base_));}

Sample::Sample(System::String^ _id)
: ParamContainer(new b::Sample(ToStdString(_id)))
{base_ = new boost::shared_ptr<b::Sample>(static_cast<b::Sample*>(ParamContainer::base_));}

Sample::Sample(System::String^ _id, System::String^ _name)
: ParamContainer(new b::Sample(ToStdString(_id), ToStdString(_name)))
{base_ = new boost::shared_ptr<b::Sample>(static_cast<b::Sample*>(ParamContainer::base_));}

System::String^ Sample::id::get() {return gcnew System::String((*base_)->id.c_str());}
void Sample::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

System::String^ Sample::name::get() {return gcnew System::String((*base_)->name.c_str());}
void Sample::name::set(System::String^ value) {(*base_)->name = ToStdString(value);}


Component::Component()
: ParamContainer(new b::Component()), owner_(nullptr)
{base_ = static_cast<b::Component*>(ParamContainer::base_);}

Component::Component(ComponentType type, int order)
: ParamContainer(new b::Component((b::ComponentType) type, order)), owner_(nullptr)
{base_ = static_cast<b::Component*>(ParamContainer::base_);}

Component::Component(CVID cvid, int order)
: ParamContainer(new b::Component((b::CVID) cvid, order)), owner_(nullptr)
{base_ = static_cast<b::Component*>(ParamContainer::base_);}

ComponentType Component::type::get() {return (ComponentType) base_->type;}
void Component::type::set(ComponentType value) {base_->type = (pwiz::msdata::ComponentType) value;}

int Component::order::get() {return base_->order;}
void Component::order::set(int value) {base_->order = value;}


ComponentList::ComponentList()
: ComponentBaseList(new b::ComponentList()), owner_(nullptr)
{base_ = static_cast<b::ComponentList*>(ComponentBaseList::base_);}


Software::Software()
: base_(new boost::shared_ptr<b::Software>(new b::Software()))
{}

Software::Software(System::String^ _id)
: base_(new boost::shared_ptr<b::Software>(new b::Software(ToStdString(_id))))
{}

Software::Software(System::String^ _id, CVParam^ _softwareParam, System::String^ _softwareParamVersion)
: base_(new boost::shared_ptr<b::Software>(new b::Software(ToStdString(_id), **_softwareParam->base_, ToStdString(_softwareParamVersion))))
{}

System::String^ Software::id::get() {return gcnew System::String((*base_)->id.c_str());}
void Software::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

CVParam^ Software::softwareParam::get() {return gcnew CVParam(&(*base_)->softwareParam);}
void Software::softwareParam::set(CVParam^ value) {(*base_)->softwareParam = **value->base_;}

System::String^ Software::softwareParamVersion::get() {return gcnew System::String((*base_)->softwareParamVersion.c_str());}
void Software::softwareParamVersion::set(System::String^ value) {(*base_)->softwareParamVersion = ToStdString(value);}


InstrumentConfiguration::InstrumentConfiguration()
: ParamContainer(new b::InstrumentConfiguration())
{base_ = new boost::shared_ptr<b::InstrumentConfiguration>(static_cast<b::InstrumentConfiguration*>(ParamContainer::base_));}

InstrumentConfiguration::InstrumentConfiguration(System::String^ _id)
: ParamContainer(new b::InstrumentConfiguration(ToStdString(_id)))
{base_ = new boost::shared_ptr<b::InstrumentConfiguration>(static_cast<b::InstrumentConfiguration*>(ParamContainer::base_));}

System::String^ InstrumentConfiguration::id::get() {return gcnew System::String((*base_)->id.c_str());}
void InstrumentConfiguration::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

ComponentList^ InstrumentConfiguration::componentList::get() {return gcnew ComponentList(&(*base_)->componentList, this);}
Software^ InstrumentConfiguration::software::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::msdata::SoftwarePtr, Software, (*base_)->softwarePtr);}

ProcessingMethod::ProcessingMethod()
: ParamContainer(new b::ProcessingMethod()), owner_(nullptr)
{base_ = static_cast<b::ProcessingMethod*>(ParamContainer::base_);}

int ProcessingMethod::order::get() {return base_->order;}
void ProcessingMethod::order::set(int value) {base_->order = value;}


DataProcessing::DataProcessing()
: base_(new boost::shared_ptr<b::DataProcessing>(new b::DataProcessing()))
{}

DataProcessing::DataProcessing(System::String^ _id)
: base_(new boost::shared_ptr<b::DataProcessing>(new b::DataProcessing(ToStdString(_id))))
{}

System::String^ DataProcessing::id::get() {return gcnew System::String((*base_)->id.c_str());}
void DataProcessing::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

Software^ DataProcessing::software::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::msdata::SoftwarePtr, Software, (*base_)->softwarePtr);}
ProcessingMethodList^ DataProcessing::processingMethods::get() {return gcnew ProcessingMethodList(&(*base_)->processingMethods, this);}


Target::Target()
: ParamContainer(new b::Target()), owner_(nullptr)
{base_ = static_cast<b::Target*>(ParamContainer::base_);}


AcquisitionSettings::AcquisitionSettings()
: base_(new boost::shared_ptr<b::AcquisitionSettings>(new b::AcquisitionSettings()))
{}

AcquisitionSettings::AcquisitionSettings(System::String^ _id)
: base_(new boost::shared_ptr<b::AcquisitionSettings>(new b::AcquisitionSettings(ToStdString(_id))))
{}

System::String^ AcquisitionSettings::id::get() {return gcnew System::String((*base_)->id.c_str());}
void AcquisitionSettings::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

InstrumentConfiguration^ AcquisitionSettings::instrumentConfiguration::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::msdata::InstrumentConfigurationPtr, InstrumentConfiguration, (*base_)->instrumentConfigurationPtr);}
void AcquisitionSettings::instrumentConfiguration::set(InstrumentConfiguration^ value) {(*base_)->instrumentConfigurationPtr = *value->base_;}

SourceFileList^ AcquisitionSettings::sourceFiles::get() {return gcnew SourceFileList(&(*base_)->sourceFilePtrs, this);}

TargetList^ AcquisitionSettings::targets::get() {return gcnew TargetList(&(*base_)->targets, this);}


Acquisition::Acquisition()
: ParamContainer(new b::Acquisition()), owner_(nullptr)
{base_ = static_cast<b::Acquisition*>(ParamContainer::base_);}
int Acquisition::number::get() {return base_->number;}
void Acquisition::number::set(int value) {base_->number = value;}

SourceFile^ Acquisition::sourceFile::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::msdata::SourceFilePtr, SourceFile, base_->sourceFilePtr);}
void Acquisition::sourceFile::set(SourceFile^ value) {base_->sourceFilePtr = *value->base_;}

System::String^ Acquisition::spectrumID::get() {return gcnew System::String(base_->spectrumID.c_str());}
void Acquisition::spectrumID::set(System::String^ value) {base_->spectrumID = ToStdString(value);}

System::String^ Acquisition::externalSpectrumID::get() {return gcnew System::String(base_->externalSpectrumID.c_str());}
void Acquisition::externalSpectrumID::set(System::String^ value) {base_->externalSpectrumID = ToStdString(value);}

System::String^ Acquisition::externalNativeID::get() {return gcnew System::String(base_->externalNativeID.c_str());}
void Acquisition::externalNativeID::set(System::String^ value) {base_->externalNativeID = ToStdString(value);}


AcquisitionList::AcquisitionList()
: ParamContainer(new b::AcquisitionList()), owner_(nullptr)
{base_ = static_cast<b::AcquisitionList*>(ParamContainer::base_);}
	
Acquisitions^ AcquisitionList::acquisitions::get() {return gcnew Acquisitions(&base_->acquisitions, this);}


IsolationWindow::IsolationWindow()
: ParamContainer(new b::IsolationWindow()), owner_(nullptr)
{base_ = static_cast<b::IsolationWindow*>(ParamContainer::base_);}


SelectedIon::SelectedIon()
: ParamContainer(new b::SelectedIon()), owner_(nullptr)
{base_ = static_cast<b::SelectedIon*>(ParamContainer::base_);}


Activation::Activation()
: ParamContainer(new b::Activation()), owner_(nullptr)
{base_ = static_cast<b::Activation*>(ParamContainer::base_);}


Precursor::Precursor()
: ParamContainer(new b::Precursor()), owner_(nullptr)
{base_ = static_cast<b::Precursor*>(ParamContainer::base_);}

SourceFile^ Precursor::sourceFile::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::msdata::SourceFilePtr, SourceFile, base_->sourceFilePtr);}
void Precursor::sourceFile::set(SourceFile^ value) {base_->sourceFilePtr = *value->base_;}

System::String^ Precursor::spectrumID::get() {return gcnew System::String(base_->spectrumID.c_str());}
void Precursor::spectrumID::set(System::String^ value) {base_->spectrumID = ToStdString(value);}

System::String^ Precursor::externalSpectrumID::get() {return gcnew System::String(base_->externalSpectrumID.c_str());}
void Precursor::externalSpectrumID::set(System::String^ value) {base_->externalSpectrumID = ToStdString(value);}

System::String^ Precursor::externalNativeID::get() {return gcnew System::String(base_->externalNativeID.c_str());}
void Precursor::externalNativeID::set(System::String^ value) {base_->externalNativeID = ToStdString(value);}

IsolationWindow^ Precursor::isolationWindow::get() {return gcnew IsolationWindow(&base_->isolationWindow, this);}
void Precursor::isolationWindow::set(IsolationWindow^ value) {base_->isolationWindow = *value->base_;}

SelectedIonList^ Precursor::selectedIons::get() {return gcnew SelectedIonList(&base_->selectedIons, this);}

Activation^ Precursor::activation::get() {return gcnew Activation(&base_->activation, this);}
void Precursor::activation::set(Activation^ value) {base_->activation = *value->base_;}


Scan::Scan()
: ParamContainer(new b::Scan()), owner_(nullptr)
{base_ = static_cast<b::Scan*>(ParamContainer::base_);}

InstrumentConfiguration^ Scan::instrumentConfiguration::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::msdata::InstrumentConfigurationPtr, InstrumentConfiguration, base_->instrumentConfigurationPtr);}
void Scan::instrumentConfiguration::set(InstrumentConfiguration^ value) {base_->instrumentConfigurationPtr = *value->base_;}

ScanWindowList^ Scan::scanWindows::get() {return gcnew ScanWindowList(&base_->scanWindows, this);}


ScanWindow::ScanWindow()
: ParamContainer(new b::ScanWindow()), owner_(nullptr)
{base_ = static_cast<b::ScanWindow*>(ParamContainer::base_);}

ScanWindow::ScanWindow(double mzLow, double mzHigh)
: ParamContainer(new b::ScanWindow(mzLow, mzHigh)), owner_(nullptr)
{base_ = static_cast<b::ScanWindow*>(ParamContainer::base_);}


SpectrumDescription::SpectrumDescription()
: ParamContainer(new b::SpectrumDescription()), owner_(nullptr)
{base_ = static_cast<b::SpectrumDescription*>(ParamContainer::base_);}

AcquisitionList^ SpectrumDescription::acquisitionList::get() {return gcnew AcquisitionList(&base_->acquisitionList, this);}
PrecursorList^ SpectrumDescription::precursors::get() {return gcnew PrecursorList(&base_->precursors, this);}

Scan^ SpectrumDescription::scan::get() {return gcnew Scan(&base_->scan, this);}
void SpectrumDescription::scan::set(Scan^ value) {base_->scan = *value->base_;}


BinaryDataArray::BinaryDataArray()
: ParamContainer(new b::BinaryDataArray())
{base_ = new boost::shared_ptr<b::BinaryDataArray>(static_cast<b::BinaryDataArray*>(ParamContainer::base_));}

DataProcessing^ BinaryDataArray::dataProcessing::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::msdata::DataProcessingPtr, DataProcessing, (*base_)->dataProcessingPtr);}
void BinaryDataArray::dataProcessing::set(DataProcessing^ value) {(*base_)->dataProcessingPtr = *value->base_;}

BinaryData^ BinaryDataArray::data::get() {return gcnew BinaryData(&(*base_)->data, this);}
void BinaryDataArray::data::set(BinaryData^ value) {(*base_)->data = *value->base_;}


MZIntensityPair::MZIntensityPair()
: base_(new pwiz::msdata::MZIntensityPair()) {}

MZIntensityPair::MZIntensityPair(double mz, double intensity)
: base_(new pwiz::msdata::MZIntensityPair(mz, intensity)) {}

double MZIntensityPair::mz::get() {return base_->mz;}
void MZIntensityPair::mz::set(double value) {base_->mz = value;}

double MZIntensityPair::intensity::get() {return base_->intensity;}
void MZIntensityPair::intensity::set(double value) {base_->intensity = value;}


TimeIntensityPair::TimeIntensityPair()
: base_(new pwiz::msdata::TimeIntensityPair()) {}

TimeIntensityPair::TimeIntensityPair(double mz, double intensity)
: base_(new pwiz::msdata::TimeIntensityPair(mz, intensity)) {}

double TimeIntensityPair::time::get() {return base_->time;}
void TimeIntensityPair::time::set(double value) {base_->time = value;}

double TimeIntensityPair::intensity::get() {return base_->intensity;}
void TimeIntensityPair::intensity::set(double value) {base_->intensity = value;}


SpectrumIdentity::SpectrumIdentity()
: base_(new pwiz::msdata::SpectrumIdentity()) {}

int SpectrumIdentity::index::get() {return (int) base_->index;}
void SpectrumIdentity::index::set(int value) {base_->index = (size_t) value;}

System::String^ SpectrumIdentity::id::get() {return gcnew System::String(base_->id.c_str());}
void SpectrumIdentity::id::set(System::String^ value) {base_->id = ToStdString(value);}

System::String^ SpectrumIdentity::nativeID::get() {return gcnew System::String(base_->nativeID.c_str());}
void SpectrumIdentity::nativeID::set(System::String^ value) {base_->nativeID = ToStdString(value);}

System::String^ SpectrumIdentity::spotID::get() {return gcnew System::String(base_->spotID.c_str());}
void SpectrumIdentity::spotID::set(System::String^ value) {base_->spotID = ToStdString(value);}

System::UInt64 SpectrumIdentity::sourceFilePosition::get() {return (System::UInt64) base_->sourceFilePosition;}
void SpectrumIdentity::sourceFilePosition::set(System::UInt64 value) {base_->sourceFilePosition = (size_t) value;}


ChromatogramIdentity::ChromatogramIdentity()
: base_(new pwiz::msdata::ChromatogramIdentity()) {}

int ChromatogramIdentity::index::get() {return (int) base_->index;}
void ChromatogramIdentity::index::set(int value) {base_->index = (size_t) value;}

System::String^ ChromatogramIdentity::id::get() {return gcnew System::String(base_->id.c_str());}
void ChromatogramIdentity::id::set(System::String^ value) {base_->id = ToStdString(value);}

System::String^ ChromatogramIdentity::nativeID::get() {return gcnew System::String(base_->nativeID.c_str());}
void ChromatogramIdentity::nativeID::set(System::String^ value) {base_->nativeID = ToStdString(value);}

System::UInt64 ChromatogramIdentity::sourceFilePosition::get() {return (System::UInt64) base_->sourceFilePosition;}
void ChromatogramIdentity::sourceFilePosition::set(System::UInt64 value) {base_->sourceFilePosition = (size_t) value;}


Spectrum::Spectrum()
: ParamContainer(new b::Spectrum())
{base_ = new boost::shared_ptr<b::Spectrum>(static_cast<b::Spectrum*>(ParamContainer::base_));}

int Spectrum::index::get() {return (int) (*base_)->index;}
void Spectrum::index::set(int value) {(*base_)->index = (size_t) value;}

System::String^ Spectrum::id::get() {return gcnew System::String((*base_)->id.c_str());}
void Spectrum::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

System::String^ Spectrum::nativeID::get() {return gcnew System::String((*base_)->nativeID.c_str());}
void Spectrum::nativeID::set(System::String^ value) {(*base_)->nativeID = ToStdString(value);}

System::String^ Spectrum::spotID::get() {return gcnew System::String((*base_)->spotID.c_str());}
void Spectrum::spotID::set(System::String^ value) {(*base_)->spotID = ToStdString(value);}

System::UInt64 Spectrum::sourceFilePosition::get() {return (System::UInt64) (*base_)->sourceFilePosition;}
void Spectrum::sourceFilePosition::set(System::UInt64 value) {(*base_)->sourceFilePosition = (size_t) value;}

System::UInt64 Spectrum::defaultArrayLength::get() {return (System::UInt64) (*base_)->defaultArrayLength;}
void Spectrum::defaultArrayLength::set(System::UInt64 value) {(*base_)->defaultArrayLength = (size_t) value;}
 
DataProcessing^ Spectrum::dataProcessing::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::msdata::DataProcessingPtr, DataProcessing, (*base_)->dataProcessingPtr);}
void Spectrum::dataProcessing::set(DataProcessing^ value) {(*base_)->dataProcessingPtr = *value->base_;}

SourceFile^ Spectrum::sourceFile::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::msdata::SourceFilePtr, SourceFile, (*base_)->sourceFilePtr);}
void Spectrum::sourceFile::set(SourceFile^ value) {(*base_)->sourceFilePtr = *value->base_;}

SpectrumDescription^ Spectrum::spectrumDescription::get() {return gcnew SpectrumDescription(&(*base_)->spectrumDescription, this);}
void Spectrum::spectrumDescription::set(SpectrumDescription^ value) {(*base_)->spectrumDescription = *value->base_;}

BinaryDataArrayList^ Spectrum::binaryDataArrays::get() {return gcnew BinaryDataArrayList(&(*base_)->binaryDataArrayPtrs, this);}
void Spectrum::binaryDataArrays::set(BinaryDataArrayList^ value) {(*base_)->binaryDataArrayPtrs = *value->base_;}

void Spectrum::getMZIntensityPairs(MZIntensityPairList^% output)
{
    std::vector<b::MZIntensityPair>* p = new std::vector<b::MZIntensityPair>();
    (*base_)->getMZIntensityPairs(*p);
    output = gcnew MZIntensityPairList(p);
}

BinaryDataArray^ Spectrum::getMZArray()
{
    return gcnew BinaryDataArray(new b::BinaryDataArrayPtr((*base_)->getMZArray()));
}

BinaryDataArray^ Spectrum::getIntensityArray()
{
    return gcnew BinaryDataArray(new b::BinaryDataArrayPtr((*base_)->getIntensityArray()));
}

void Spectrum::setMZIntensityPairs(MZIntensityPairList^ input)
{
    (*base_)->setMZIntensityPairs(*input->base_, (b::CVID) CVID::CVID_Unknown);
}

void Spectrum::setMZIntensityPairs(MZIntensityPairList^ input, CVID intensityUnits)
{
    (*base_)->setMZIntensityPairs(*input->base_, (b::CVID) intensityUnits);
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

    (*base_)->setMZIntensityArrays(mzVector, intensityVector, (b::CVID) intensityUnits);
}


Chromatogram::Chromatogram()
: ParamContainer(new b::Chromatogram())
{base_ = new boost::shared_ptr<b::Chromatogram>(static_cast<b::Chromatogram*>(ParamContainer::base_));}

int Chromatogram::index::get() {return (int) (*base_)->index;}
void Chromatogram::index::set(int value) {(*base_)->index = (size_t) value;}

System::String^ Chromatogram::id::get() {return gcnew System::String((*base_)->id.c_str());}
void Chromatogram::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

System::String^ Chromatogram::nativeID::get() {return gcnew System::String((*base_)->nativeID.c_str());}
void Chromatogram::nativeID::set(System::String^ value) {(*base_)->nativeID = ToStdString(value);}

System::UInt64 Chromatogram::sourceFilePosition::get() {return (System::UInt64) (*base_)->sourceFilePosition;}
void Chromatogram::sourceFilePosition::set(System::UInt64 value) {(*base_)->sourceFilePosition = (size_t) value;}

System::UInt64 Chromatogram::defaultArrayLength::get() {return (*base_)->defaultArrayLength;}
void Chromatogram::defaultArrayLength::set(System::UInt64 value) {(*base_)->defaultArrayLength = (size_t) value;}
 
DataProcessing^ Chromatogram::dataProcessing::get()  {return NATIVE_SHARED_PTR_TO_CLI(pwiz::msdata::DataProcessingPtr, DataProcessing, (*base_)->dataProcessingPtr);}
//void set(DataProcessing^ value) {(*base_)->dataProcessingPtr = *value->base_;}

BinaryDataArrayList^ Chromatogram::binaryDataArrays::get() {return gcnew BinaryDataArrayList(&(*base_)->binaryDataArrayPtrs, this);}
void Chromatogram::binaryDataArrays::set(BinaryDataArrayList^ value) {(*base_)->binaryDataArrayPtrs = *value->base_;}

void Chromatogram::getTimeIntensityPairs(TimeIntensityPairList^% output)
{
    std::vector<b::TimeIntensityPair>* p = new std::vector<b::TimeIntensityPair>();
    (*base_)->getTimeIntensityPairs(*p);
    output = gcnew TimeIntensityPairList(p);
}

void Chromatogram::setTimeIntensityPairs(TimeIntensityPairList^ input)
{
    (*base_)->setTimeIntensityPairs(*input->base_);
}


int SpectrumList::size()
{
    return (int) (*base_)->size();
}

bool SpectrumList::empty()
{
    return (*base_)->empty();
}

SpectrumIdentity^ SpectrumList::spectrumIdentity(int index)
{
    return gcnew SpectrumIdentity(&const_cast<b::SpectrumIdentity&>((*base_)->spectrumIdentity((size_t) index)));
}

int SpectrumList::find(System::String^ id)
{
    return (int) (*base_)->find(ToStdString(id));
}

int SpectrumList::findNative(System::String^ nativeID)
{
    return (int) (*base_)->findNative(ToStdString(nativeID));
}

Spectrum^ SpectrumList::spectrum(int index)
{
    return spectrum(index, false);
}

Spectrum^ SpectrumList::spectrum(int index, bool getBinaryData)
{
    try { return gcnew Spectrum(new b::SpectrumPtr((*base_)->spectrum((size_t) index, getBinaryData))); }
    catch (exception& e) { throw gcnew Exception(gcnew String(e.what())); }
}


SpectrumListSimple::SpectrumListSimple()
: SpectrumList(new boost::shared_ptr<b::SpectrumList>(new b::SpectrumListSimple()))
{base_ = reinterpret_cast<boost::shared_ptr<b::SpectrumListSimple>*>(SpectrumList::base_);}

Spectra^ SpectrumListSimple::spectra::get() {return gcnew Spectra(&(*base_)->spectra, this);}
void SpectrumListSimple::spectra::set(Spectra^ value) {(*base_)->spectra = *value->base_;}

SpectrumIdentity^ SpectrumListSimple::spectrumIdentity(int index)
{
    return gcnew SpectrumIdentity(&const_cast<b::SpectrumIdentity&>((*base_)->spectrumIdentity((size_t) index)));
}

Spectrum^ SpectrumListSimple::spectrum(int index)
{
    return gcnew Spectrum(new b::SpectrumPtr((*base_)->spectrum((size_t) index, false)));
}

Spectrum^ SpectrumListSimple::spectrum(int index, bool getBinaryData)
{
    return gcnew Spectrum(new b::SpectrumPtr((*base_)->spectrum((size_t) index, getBinaryData)));
}


int ChromatogramList::size()
{
    return (int) (*base_)->size();
}

bool ChromatogramList::empty()
{
    return (*base_)->empty();
}

ChromatogramIdentity^ ChromatogramList::chromatogramIdentity(int index)
{
    return gcnew ChromatogramIdentity(&const_cast<b::ChromatogramIdentity&>((*base_)->chromatogramIdentity((size_t) index)));
}

int ChromatogramList::find(System::String^ id)
{
    return (int) (*base_)->find(ToStdString(id));
}

int ChromatogramList::findNative(System::String^ nativeID)
{
    return (int) (*base_)->findNative(ToStdString(nativeID));
}

Chromatogram^ ChromatogramList::chromatogram(int index)
{
    return gcnew Chromatogram(new b::ChromatogramPtr((*base_)->chromatogram((size_t) index, false)));
}

Chromatogram^ ChromatogramList::chromatogram(int index, bool getBinaryData)
{
    return gcnew Chromatogram(new b::ChromatogramPtr((*base_)->chromatogram((size_t) index, getBinaryData)));
}


ChromatogramListSimple::ChromatogramListSimple()
: ChromatogramList(new boost::shared_ptr<b::ChromatogramList>(new b::ChromatogramListSimple()))
{base_ = reinterpret_cast<boost::shared_ptr<b::ChromatogramListSimple>*>(ChromatogramList::base_);}

Chromatograms^ ChromatogramListSimple::chromatograms::get() {return gcnew Chromatograms(&(*base_)->chromatograms, this);}
void ChromatogramListSimple::chromatograms::set(Chromatograms^ value) {(*base_)->chromatograms = *value->base_;}

ChromatogramIdentity^ ChromatogramListSimple::chromatogramIdentity(int index)
{
    return gcnew ChromatogramIdentity(&const_cast<b::ChromatogramIdentity&>((*base_)->chromatogramIdentity((size_t) index)));
}

Chromatogram^ ChromatogramListSimple::chromatogram(int index)
{
    return gcnew Chromatogram(new b::ChromatogramPtr((*base_)->chromatogram((size_t) index, false)));
}

Chromatogram^ ChromatogramListSimple::chromatogram(int index, bool getBinaryData)
{
    return gcnew Chromatogram(new b::ChromatogramPtr((*base_)->chromatogram((size_t) index, getBinaryData)));
}


Run::Run()
: ParamContainer(new b::Run()), owner_(nullptr)
{base_ = static_cast<b::Run*>(ParamContainer::base_);}

System::String^ Run::id::get() {return gcnew System::String(base_->id.c_str());}
void Run::id::set(System::String^ value) {base_->id = ToStdString(value);}

InstrumentConfiguration^ Run::defaultInstrumentConfiguration::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::msdata::InstrumentConfigurationPtr, InstrumentConfiguration, base_->defaultInstrumentConfigurationPtr);}
void Run::defaultInstrumentConfiguration::set(InstrumentConfiguration^ value) {base_->defaultInstrumentConfigurationPtr = *value->base_;}

Sample^ Run::sample::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::msdata::SamplePtr, Sample, base_->samplePtr);}
void Run::sample::set(Sample^ value) {base_->samplePtr = *value->base_;}

System::String^ Run::startTimeStamp::get() {return gcnew System::String(base_->startTimeStamp.c_str());}
void Run::startTimeStamp::set(System::String^ value) {base_->startTimeStamp = ToStdString(value);}

SourceFileList^ Run::sourceFiles::get() {return gcnew SourceFileList(&base_->sourceFilePtrs, this);}
void Run::sourceFiles::set(SourceFileList^ value) {base_->sourceFilePtrs = *value->base_;}

SpectrumList^ Run::spectrumList::get() {return NATIVE_OWNED_SHARED_PTR_TO_CLI(pwiz::msdata::SpectrumListPtr, SpectrumList, base_->spectrumListPtr, this);}
void Run::spectrumList::set(SpectrumList^ value) {base_->spectrumListPtr = *value->base_;}

ChromatogramList^ Run::chromatogramList::get() {return NATIVE_OWNED_SHARED_PTR_TO_CLI(pwiz::msdata::ChromatogramListPtr, ChromatogramList, base_->chromatogramListPtr, this);}
void Run::chromatogramList::set(ChromatogramList^ value) {base_->chromatogramListPtr = *value->base_;}


MSData::MSData()
: base_(new b::MSData()), owner_(nullptr)
{}

System::String^ MSData::accession::get() {return gcnew System::String(base_->accession.c_str());}
void MSData::accession::set(System::String^ value) {base_->accession = ToStdString(value);}

System::String^ MSData::id::get() {return gcnew System::String(base_->id.c_str());}
void MSData::id::set(System::String^ value) {base_->id = ToStdString(value);}

System::String^ MSData::version::get() {return gcnew System::String(base_->version.c_str());}
void MSData::version::set(System::String^ value) {base_->version = ToStdString(value);}

CVList^ MSData::cvs::get() {return gcnew CVList(&base_->cvs, this);}
void MSData::cvs::set(CVList^ value) {base_->cvs = *value->base_;}

FileDescription^ MSData::fileDescription::get() {return gcnew FileDescription(&base_->fileDescription, this);}
void MSData::fileDescription::set(FileDescription^ value) {base_->fileDescription = *value->base_;}

ParamGroupList^ MSData::paramGroups::get() {return gcnew ParamGroupList(&base_->paramGroupPtrs, this);}
void MSData::paramGroups::set(ParamGroupList^ value) {base_->paramGroupPtrs = *value->base_;}

SampleList^ MSData::samples::get() {return gcnew SampleList(&base_->samplePtrs, this);}
void MSData::samples::set(SampleList^ value) {base_->samplePtrs = *value->base_;}

InstrumentConfigurationList^ MSData::instrumentConfigurationList::get() {return gcnew InstrumentConfigurationList(&base_->instrumentConfigurationPtrs, this);}
void MSData::instrumentConfigurationList::set(InstrumentConfigurationList^ value) {base_->instrumentConfigurationPtrs = *value->base_;}

SoftwareList^ MSData::softwareList::get() {return gcnew SoftwareList(&base_->softwarePtrs, this);}
void MSData::softwareList::set(SoftwareList^ value) {base_->softwarePtrs = *value->base_;}

DataProcessingList^ MSData::dataProcessingList::get() {return gcnew DataProcessingList(&base_->dataProcessingPtrs, this);}
void MSData::dataProcessingList::set(DataProcessingList^ value) {base_->dataProcessingPtrs = *value->base_;}

AcquisitionSettingsList^ MSData::acquisitionSettingList::get() {return gcnew AcquisitionSettingsList(&base_->acquisitionSettingsPtrs, this);}
void MSData::acquisitionSettingList::set(AcquisitionSettingsList^ value) {base_->acquisitionSettingsPtrs = *value->base_;}

Run^ MSData::run::get()  {return gcnew Run(&base_->run, this);}
//void set(Run^ value) {base_->run = *value->base_;}


} // namespace msdata
} // namespace CLI
} // namespace pwiz
