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
: base_(new b::CV())
{}


UserParam::UserParam()
: base_(new b::UserParam())
{value_ = gcnew UserParamValue(base_);}

UserParam::UserParam(System::String^ _name)
: base_(new b::UserParam(ToStdString(_name)))
{value_ = gcnew UserParamValue(base_);}

UserParam::UserParam(System::String^ _name, System::String^ _value)
: base_(new b::UserParam(ToStdString(_name), ToStdString(_value)))
{value_ = gcnew UserParamValue(base_);}

UserParam::UserParam(System::String^ _name, System::String^ _value, System::String^ _type)
: base_(new b::UserParam(ToStdString(_name), ToStdString(_value), ToStdString(_type)))
{value_ = gcnew UserParamValue(base_);}

UserParam::UserParam(System::String^ _name, System::String^ _value, System::String^ _type, CVID _units)
: base_(new b::UserParam(ToStdString(_name), ToStdString(_value), ToStdString(_type), (b::CVID) _units))
{value_ = gcnew UserParamValue(base_);}


ParamGroupList^ ParamContainer::getParamGroups()
{
    return gcnew ParamGroupList(&base_->paramGroupPtrs);
}

CVParamList^ ParamContainer::getCVParams()
{
    return gcnew CVParamList(&base_->cvParams);
}

UserParamList^ ParamContainer::getUserParams()
{
    return gcnew UserParamList(&base_->userParams);
}


ParamGroup::ParamGroup()
: ParamContainer(new b::ParamGroup())
{base_ = new boost::shared_ptr<b::ParamGroup>(static_cast<b::ParamGroup*>(ParamContainer::base_));}

ParamGroup::ParamGroup(System::String^ _id)
: ParamContainer(new b::ParamGroup(ToStdString(_id)))
{base_ = new boost::shared_ptr<b::ParamGroup>(static_cast<b::ParamGroup*>(ParamContainer::base_));}


FileContent::FileContent()
: ParamContainer(new b::FileContent())
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


Contact::Contact()
: ParamContainer(new b::Contact())
{base_ = static_cast<b::Contact*>(ParamContainer::base_);}


FileDescription::FileDescription()
: base_(new b::FileDescription())
{}


Sample::Sample()
: ParamContainer(new b::Sample())
{base_ = new boost::shared_ptr<b::Sample>(static_cast<b::Sample*>(ParamContainer::base_));}

Sample::Sample(System::String^ _id)
: ParamContainer(new b::Sample(ToStdString(_id)))
{base_ = new boost::shared_ptr<b::Sample>(static_cast<b::Sample*>(ParamContainer::base_));}

Sample::Sample(System::String^ _id, System::String^ _name)
: ParamContainer(new b::Sample(ToStdString(_id), ToStdString(_name)))
{base_ = new boost::shared_ptr<b::Sample>(static_cast<b::Sample*>(ParamContainer::base_));}


Component::Component()
: ParamContainer(new b::Component())
{base_ = static_cast<b::Component*>(ParamContainer::base_);}

Component::Component(ComponentType type, int order)
: ParamContainer(new b::Component((b::ComponentType) type, order))
{base_ = static_cast<b::Component*>(ParamContainer::base_);}

Component::Component(CVID cvid, int order)
: ParamContainer(new b::Component((b::CVID) cvid, order))
{base_ = static_cast<b::Component*>(ParamContainer::base_);}


ComponentList::ComponentList()
: ComponentBaseList(new b::ComponentList())
{base_ = static_cast<b::ComponentList*>(ComponentBaseList::base_);}


Software::Software()
: base_(new boost::shared_ptr<b::Software>(new b::Software()))
{}

Software::Software(System::String^ _id)
: base_(new boost::shared_ptr<b::Software>(new b::Software(ToStdString(_id))))
{}

Software::Software(System::String^ _id, CVParam^ _softwareParam, System::String^ _softwareParamVersion)
: base_(new boost::shared_ptr<b::Software>(new b::Software(ToStdString(_id), *_softwareParam->base_, ToStdString(_softwareParamVersion))))
{}


InstrumentConfiguration::InstrumentConfiguration()
: ParamContainer(new b::InstrumentConfiguration())
{base_ = new boost::shared_ptr<b::InstrumentConfiguration>(static_cast<b::InstrumentConfiguration*>(ParamContainer::base_));}

InstrumentConfiguration::InstrumentConfiguration(System::String^ _id)
: ParamContainer(new b::InstrumentConfiguration(ToStdString(_id)))
{base_ = new boost::shared_ptr<b::InstrumentConfiguration>(static_cast<b::InstrumentConfiguration*>(ParamContainer::base_));}


ProcessingMethod::ProcessingMethod()
: ParamContainer(new b::ProcessingMethod())
{base_ = static_cast<b::ProcessingMethod*>(ParamContainer::base_);}


DataProcessing::DataProcessing()
: base_(new boost::shared_ptr<b::DataProcessing>(new b::DataProcessing()))
{}

DataProcessing::DataProcessing(System::String^ _id)
: base_(new boost::shared_ptr<b::DataProcessing>(new b::DataProcessing(ToStdString(_id))))
{}


Target::Target()
: ParamContainer(new b::Target())
{base_ = static_cast<b::Target*>(ParamContainer::base_);}


AcquisitionSettings::AcquisitionSettings()
: base_(new boost::shared_ptr<b::AcquisitionSettings>(new b::AcquisitionSettings()))
{}

AcquisitionSettings::AcquisitionSettings(System::String^ _id)
: base_(new boost::shared_ptr<b::AcquisitionSettings>(new b::AcquisitionSettings(ToStdString(_id))))
{}


Acquisition::Acquisition()
: ParamContainer(new b::Acquisition())
{base_ = static_cast<b::Acquisition*>(ParamContainer::base_);}


AcquisitionList::AcquisitionList()
: ParamContainer(new b::AcquisitionList())
{base_ = static_cast<b::AcquisitionList*>(ParamContainer::base_);}


IsolationWindow::IsolationWindow()
: ParamContainer(new b::IsolationWindow())
{base_ = static_cast<b::IsolationWindow*>(ParamContainer::base_);}


SelectedIon::SelectedIon()
: ParamContainer(new b::SelectedIon())
{base_ = static_cast<b::SelectedIon*>(ParamContainer::base_);}


Activation::Activation()
: ParamContainer(new b::Activation())
{base_ = static_cast<b::Activation*>(ParamContainer::base_);}


Precursor::Precursor()
: ParamContainer(new b::Precursor())
{base_ = static_cast<b::Precursor*>(ParamContainer::base_);}


Scan::Scan()
: ParamContainer(new b::Scan())
{base_ = static_cast<b::Scan*>(ParamContainer::base_);}

ScanWindow::ScanWindow()
: ParamContainer(new b::ScanWindow())
{base_ = static_cast<b::ScanWindow*>(ParamContainer::base_);}

ScanWindow::ScanWindow(double mzLow, double mzHigh)
: ParamContainer(new b::ScanWindow(mzLow, mzHigh))
{base_ = static_cast<b::ScanWindow*>(ParamContainer::base_);}


SpectrumDescription::SpectrumDescription()
: ParamContainer(new b::SpectrumDescription())
{base_ = static_cast<b::SpectrumDescription*>(ParamContainer::base_);}


BinaryDataArray::BinaryDataArray()
: ParamContainer(new b::BinaryDataArray())
{base_ = new boost::shared_ptr<b::BinaryDataArray>(static_cast<b::BinaryDataArray*>(ParamContainer::base_));}


Spectrum::Spectrum()
: ParamContainer(new b::Spectrum())
{base_ = new boost::shared_ptr<b::Spectrum>(static_cast<b::Spectrum*>(ParamContainer::base_));}

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
    cli::array<double>^ mzArray2 = mzArray->ToArray();
    pin_ptr<double> mzArrayPinPtr = &mzArray2[0];
    double* mzArrayBegin = (double*) mzArrayPinPtr;
    cli::array<double>^ intensityArray2 = intensityArray->ToArray();
    pin_ptr<double> intensityArrayPinPtr = &intensityArray2[0];
    double* intensityArrayBegin = (double*) intensityArrayPinPtr;
    (*base_)->setMZIntensityArrays(std::vector<double>(mzArrayBegin, mzArrayBegin + mzArray2->Length),
                                   std::vector<double>(intensityArrayBegin, intensityArrayBegin + intensityArray2->Length),
                                   (b::CVID) intensityUnits);
}


Chromatogram::Chromatogram()
: ParamContainer(new b::Chromatogram())
{base_ = new boost::shared_ptr<b::Chromatogram>(static_cast<b::Chromatogram*>(ParamContainer::base_));}

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
: ParamContainer(new b::Run())
{base_ = static_cast<b::Run*>(ParamContainer::base_);}


MSData::MSData()
: base_(new b::MSData())
{}


} // namespace msdata
} // namespace CLI
} // namespace pwiz