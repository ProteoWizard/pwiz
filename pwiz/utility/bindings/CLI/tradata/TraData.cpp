//
// $Id$
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
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

#include "TraData.hpp"
//#include "../../../data/msdata/TraData.hpp"


using System::Exception;
using System::String;
using boost::shared_ptr;


namespace b = pwiz::tradata;


namespace pwiz {
namespace CLI {
namespace tradata {

/// <summary>
/// version information for the msdata namespace
/// </summary>
public ref class Version
{
    public:
    static int Major() {return pwiz::tradata::Version::Major();}
    static int Minor() {return pwiz::tradata::Version::Minor();}
    static int Revision() {return pwiz::tradata::Version::Revision();}
    static System::String^ LastModified() {return ToSystemString(pwiz::tradata::Version::LastModified());}
    static System::String^ ToString() {return ToSystemString(pwiz::tradata::Version::str());}
};

Contact::Contact()
: ParamContainer(new b::Contact())
{base_ = new boost::shared_ptr<b::Contact>(static_cast<b::Contact*>(ParamContainer::base_));}

Contact::Contact(System::String^ id)
: ParamContainer(new b::Contact(ToStdString(id)))
{base_ = new boost::shared_ptr<b::Contact>(static_cast<b::Contact*>(ParamContainer::base_));}

System::String^ Contact::id::get() {return ToSystemString((*base_)->id);}
void Contact::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

bool Contact::empty()
{
    return (*base_)->empty();
}

Publication::Publication()
: ParamContainer(new b::Publication())
{owner_ = nullptr; base_ = static_cast<b::Publication*>(ParamContainer::base_);}

System::String^ Publication::id::get() {return ToSystemString(base_->id);}
void Publication::id::set(System::String^ value) {base_->id = ToStdString(value);}

bool Publication::empty()
{
    return base_->empty();
}

Software::Software()
: ParamContainer(new b::Software())
{base_ = new boost::shared_ptr<b::Software>(static_cast<b::Software*>(ParamContainer::base_));}

Software::Software(System::String^ _id)
: ParamContainer(new b::Software(ToStdString(_id)))
{base_ = new boost::shared_ptr<b::Software>(static_cast<b::Software*>(ParamContainer::base_));}

Software::Software(System::String^ _id, CVParam^ param, System::String^ version)
: ParamContainer(new b::Software(ToStdString(_id), param->base(), ToStdString(version)))
{base_ = new boost::shared_ptr<b::Software>(static_cast<b::Software*>(ParamContainer::base_));}

System::String^ Software::id::get() {return ToSystemString((*base_)->id);}
void Software::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

System::String^ Software::version::get() {return ToSystemString((*base_)->version);}
void Software::version::set(System::String^ value) {(*base_)->version = ToStdString(value);}

bool Software::empty()
{
    return (*base_)->empty();
}

Software^ RetentionTime::software::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::tradata::SoftwarePtr, Software, base_->softwarePtr);}

RetentionTime::RetentionTime()
: ParamContainer(new b::RetentionTime())
{owner_ = nullptr; base_ = static_cast<b::RetentionTime*>(ParamContainer::base_);}

bool RetentionTime::empty()
{
    return base_->empty();
}

Software^ Prediction::software::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::tradata::SoftwarePtr, Software, base_->softwarePtr);}
Contact^ Prediction::contact::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::tradata::ContactPtr, Contact, base_->contactPtr);}

Prediction::Prediction()
: ParamContainer(new b::Prediction())
{owner_ = nullptr; base_ = static_cast<b::Prediction*>(ParamContainer::base_);}

bool Prediction::empty()
{
    return base_->empty();
}

Evidence::Evidence()
: ParamContainer(new b::Evidence())
{owner_ = nullptr; base_ = static_cast<b::Evidence*>(ParamContainer::base_);}

bool Evidence::empty()
{
    return base_->empty();
}

Validation::Validation()
: ParamContainer(new b::Validation())
{owner_ = nullptr; base_ = static_cast<b::Validation*>(ParamContainer::base_);}

bool Validation::empty()
{
    return base_->empty();
}

Instrument::Instrument()
: ParamContainer(new b::Instrument())
{base_ = new boost::shared_ptr<b::Instrument>(static_cast<b::Instrument*>(ParamContainer::base_));}

Instrument::Instrument(System::String^ id)
: ParamContainer(new b::Instrument(ToStdString(id)))
{base_ = new boost::shared_ptr<b::Instrument>(static_cast<b::Instrument*>(ParamContainer::base_));}

System::String^ Instrument::id::get() {return ToSystemString((*base_)->id);}
void Instrument::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

bool Instrument::empty()
{
    return (*base_)->empty();
}

ValidationList^ Configuration::validations::get() {return gcnew ValidationList(&base_->validations, this);}
Contact^ Configuration::contact::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::tradata::ContactPtr, Contact, base_->contactPtr);}
Instrument^ Configuration::instrument::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::tradata::InstrumentPtr, Instrument, base_->instrumentPtr);}

Configuration::Configuration()
: ParamContainer(new b::Configuration())
{owner_ = nullptr; base_ = static_cast<b::Configuration*>(ParamContainer::base_);}

bool Configuration::empty()
{
    return base_->empty();
}

Interpretation::Interpretation()
: ParamContainer(new b::Interpretation())
{owner_ = nullptr; base_ = static_cast<b::Interpretation*>(ParamContainer::base_);}

bool Interpretation::empty()
{
    return base_->empty();
}

Protein::Protein()
: ParamContainer(new b::Protein())
{base_ = new boost::shared_ptr<b::Protein>(static_cast<b::Protein*>(ParamContainer::base_));}

Protein::Protein(System::String^ id)
: ParamContainer(new b::Protein(ToStdString(id)))
{base_ = new boost::shared_ptr<b::Protein>(static_cast<b::Protein*>(ParamContainer::base_));}

System::String^ Protein::id::get() {return ToSystemString((*base_)->id);}
void Protein::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

System::String^ Protein::sequence::get() {return ToSystemString((*base_)->sequence);}
void Protein::sequence::set(System::String^ value) {(*base_)->sequence = ToStdString(value);}

bool Protein::empty()
{
    return (*base_)->empty();
}

Modification::Modification()
: ParamContainer(new b::Modification())
{owner_ = nullptr; base_ = static_cast<b::Modification*>(ParamContainer::base_);}

int Modification::location::get() {return base_->location;}
void Modification::location::set(int value) {base_->location = value;}

double Modification::monoisotopicMassDelta::get() {return base_->monoisotopicMassDelta;}
void Modification::monoisotopicMassDelta::set(double value) {base_->monoisotopicMassDelta = value;}

double Modification::averageMassDelta::get() {return base_->averageMassDelta;}
void Modification::averageMassDelta::set(double value) {base_->averageMassDelta = value;}

bool Modification::empty()
{
    return base_->empty();
}

Peptide::Peptide()
: ParamContainer(new b::Peptide())
{base_ = new boost::shared_ptr<b::Peptide>(static_cast<b::Peptide*>(ParamContainer::base_));}

Peptide::Peptide(System::String^ id)
: ParamContainer(new b::Peptide(ToStdString(id)))
{base_ = new boost::shared_ptr<b::Peptide>(static_cast<b::Peptide*>(ParamContainer::base_));}

System::String^ Peptide::id::get() {return ToSystemString((*base_)->id);}
void Peptide::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

System::String^ Peptide::sequence::get() {return ToSystemString((*base_)->sequence);}
void Peptide::sequence::set(System::String^ value) {(*base_)->sequence = ToStdString(value);}

ModificationList^ Peptide::modifications::get() {return gcnew ModificationList(&(*base_)->modifications, this);}
ProteinList^ Peptide::proteins::get() {return gcnew ProteinList(&(*base_)->proteinPtrs, this);}
RetentionTimeList^ Peptide::retentionTimes::get() {return gcnew RetentionTimeList(&(*base_)->retentionTimes, this);}
Evidence^ Peptide::evidence::get() {return gcnew Evidence(&(*base_)->evidence, this);}

bool Peptide::empty()
{
    return (*base_)->empty();
}

Compound::Compound()
: ParamContainer(new b::Compound())
{base_ = new boost::shared_ptr<b::Compound>(static_cast<b::Compound*>(ParamContainer::base_));}

Compound::Compound(System::String^ id)
: ParamContainer(new b::Compound(ToStdString(id)))
{base_ = new boost::shared_ptr<b::Compound>(static_cast<b::Compound*>(ParamContainer::base_));}

System::String^ Compound::id::get() {return ToSystemString((*base_)->id);}
void Compound::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

RetentionTimeList^ Compound::retentionTimes::get() {return gcnew RetentionTimeList(&(*base_)->retentionTimes, this);}

bool Compound::empty()
{
    return (*base_)->empty();
}

Precursor::Precursor()
: ParamContainer(new b::Precursor())
{owner_ = nullptr; base_ = static_cast<b::Precursor*>(ParamContainer::base_);}

bool Precursor::empty()
{
    return base_->empty();
}

Product::Product()
: ParamContainer(new b::Product())
{owner_ = nullptr; base_ = static_cast<b::Product*>(ParamContainer::base_);}

bool Product::empty()
{
    return base_->empty();
}

Transition::Transition()
: ParamContainer(new b::Transition())
{owner_ = nullptr; base_ = static_cast<b::Transition*>(ParamContainer::base_);}

System::String^ Transition::id::get() {return ToSystemString(base_->id);}
void Transition::id::set(System::String^ value) {base_->id = ToStdString(value);}

Peptide^ Transition::peptide::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::tradata::PeptidePtr, Peptide, base_->peptidePtr);}
Compound^ Transition::compound::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::tradata::CompoundPtr, Compound, base_->compoundPtr);}
Precursor^ Transition::precursor::get() {return gcnew Precursor(&base_->precursor, this);}
Product^ Transition::product::get() {return gcnew Product(&base_->product, this);}
Prediction^ Transition::prediction::get() {return gcnew Prediction(&base_->prediction, this);}
RetentionTime^ Transition::retentionTime::get() {return gcnew RetentionTime(&base_->retentionTime, this);}
InterpretationList^ Transition::interpretations::get() {return gcnew InterpretationList(&base_->interpretationList, this);}
ConfigurationList^ Transition::configurations::get() {return gcnew ConfigurationList(&base_->configurationList, this);}

bool Transition::empty()
{
    return base_->empty();
}

Target::Target()
: ParamContainer(new b::Target())
{owner_ = nullptr; base_ = static_cast<b::Target*>(ParamContainer::base_);}

System::String^ Target::id::get() {return ToSystemString(base_->id);}
void Target::id::set(System::String^ value) {base_->id = ToStdString(value);}

Peptide^ Target::peptide::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::tradata::PeptidePtr, Peptide, base_->peptidePtr);}
Compound^ Target::compound::get() {return NATIVE_SHARED_PTR_TO_CLI(pwiz::tradata::CompoundPtr, Compound, base_->compoundPtr);}
Precursor^ Target::precursor::get() {return gcnew Precursor(&base_->precursor, this);}
RetentionTime^ Target::retentionTime::get() {return gcnew RetentionTime(&base_->retentionTime, this);}
ConfigurationList^ Target::configurations::get() {return gcnew ConfigurationList(&base_->configurationList, this);}

bool Target::empty()
{
    return base_->empty();
}

TargetList::TargetList()
: ParamContainer(new b::TargetList())
{owner_ = nullptr; base_ = static_cast<b::TargetList*>(ParamContainer::base_);}

TargetListList^ TargetList::targetExcludeList::get() {return gcnew TargetListList(&base_->targetExcludeList, this);}
TargetListList^ TargetList::targetIncludeList::get() {return gcnew TargetListList(&base_->targetIncludeList, this);}

bool TargetList::empty()
{
    return base_->empty();
}

TraData::TraData()
: base_(new boost::shared_ptr<b::TraData>(new b::TraData())), owner_(nullptr)
{
}

System::String^ TraData::id::get() {return ToSystemString(base().id);}
void TraData::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

CVList^ TraData::cvs::get() {return gcnew CVList(&base().cvs, this);}
void TraData::cvs::set(CVList^ value) {cvs->assign(value);}

ContactList^ TraData::contacts::get() {return gcnew ContactList(&base().contactPtrs, this);}
void TraData::contacts::set(ContactList^ value) {contacts->assign(value);}

PublicationList^ TraData::publications::get() {return gcnew PublicationList(&base().publications, this);}
void TraData::publications::set(PublicationList^ value) {publications->assign(value);}

InstrumentList^ TraData::instruments::get() {return gcnew InstrumentList(&base().instrumentPtrs, this);}
void TraData::instruments::set(InstrumentList^ value) {instruments->assign(value);}

SoftwareList^ TraData::softwareList::get() {return gcnew SoftwareList(&base().softwarePtrs, this);}
void TraData::softwareList::set(SoftwareList^ value) {softwareList->assign(value);}

ProteinList^ TraData::proteins::get() {return gcnew ProteinList(&base().proteinPtrs, this);}
void TraData::proteins::set(ProteinList^ value) {proteins->assign(value);}

PeptideList^ TraData::peptides::get() {return gcnew PeptideList(&base().peptidePtrs, this);}
void TraData::peptides::set(PeptideList^ value) {peptides->assign(value);}

CompoundList^ TraData::compounds::get() {return gcnew CompoundList(&base().compoundPtrs, this);}
void TraData::compounds::set(CompoundList^ value) {compounds->assign(value);}

TransitionList^ TraData::transitions::get() {return gcnew TransitionList(&base().transitions, this);}
void TraData::transitions::set(TransitionList^ value) {transitions->assign(value);}

TargetList^ TraData::targets::get() {return gcnew TargetList(&base().targets, this);}
void TraData::targets::set(TargetList^ value) {base().targets = *value->base_;}



bool TraData::empty() {return base().empty();}
System::String^ TraData::version() {return ToSystemString(base().version());}

} // namespace tradata
} // namespace CLI
} // namespace pwiz