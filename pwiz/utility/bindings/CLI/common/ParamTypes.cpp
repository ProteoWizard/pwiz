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

#include "ParamTypes.hpp"


namespace pwiz {
namespace CLI {
namespace cv {


namespace b = pwiz::data;


CV::CV()
: base_(new b::CV()), owner_(nullptr)
{}

System::String^ CV::id::get() {return ToSystemString(base_->id);}
void CV::id::set(System::String^ value) {base_->id = ToStdString(value);}

System::String^ CV::URI::get() {return ToSystemString(base_->URI);}
void CV::URI::set(System::String^ value) {base_->URI = ToStdString(value);}

System::String^ CV::fullName::get() {return ToSystemString(base_->fullName);}
void CV::fullName::set(System::String^ value) {base_->fullName = ToStdString(value);}

System::String^ CV::version::get() {return ToSystemString(base_->version);}
void CV::version::set(System::String^ value) {base_->version = ToStdString(value);}

bool CV::empty()
{
    return base_->empty();
}


CVTermInfo^ CV::cvTermInfo(CVID cvid)
{
    return gcnew CVTermInfo(cvid);
}

CVTermInfo^ CV::cvTermInfo(System::String^ id)
{
    return gcnew CVTermInfo(id);
}

bool CV::cvIsA(CVID child, CVID parent)
{
    return b::cvIsA((b::CVID) child, (b::CVID) parent);
}

System::Collections::Generic::IList<CVID>^ CV::cvids()
{
    return gcnew CVIDList(const_cast<std::vector<b::CVID>*>(&b::cvids()), gcnew System::Object());
}


} // namespace cv




namespace data {


namespace b = pwiz::data;


CVParamValue::CVParamValue(boost::shared_ptr<b::CVParam>* base)
: base_(new boost::shared_ptr<b::CVParam>(*base))
{LOG_CONSTRUCT(BOOST_PP_STRINGIZE(CVParamValue))}

CVParamValue::~CVParamValue()
{LOG_DESTRUCT(BOOST_PP_STRINGIZE(CVParamValue), true) SAFEDELETE(base_);}

CVParamValue::!CVParamValue()
{delete this;}

CVParam::CVParam(void* base, System::Object^ owner)
: base_(new boost::shared_ptr<b::CVParam>(static_cast<b::CVParam*>(base))), owner_(owner), value_(gcnew CVParamValue(base_))
{LOG_CONSTRUCT(BOOST_PP_STRINGIZE(CVParam))}

CVParam::CVParam(void* base)
: base_(new boost::shared_ptr<b::CVParam>(static_cast<b::CVParam*>(base))), owner_(nullptr), value_(gcnew CVParamValue(base_))
{LOG_CONSTRUCT(BOOST_PP_STRINGIZE(CVParam))}

CVParam::~CVParam()
{LOG_DESTRUCT(BOOST_PP_STRINGIZE(CVParam), (owner_ == nullptr)) if (owner_ == nullptr) SAFEDELETE(base_);}

CVParam::!CVParam()
{delete this;}

CVParam::CVParam(CVID _cvid, bool _value)
: base_(new boost::shared_ptr<b::CVParam>(new b::CVParam((pwiz::cv::CVID) _cvid, _value)))
{value_ = gcnew CVParamValue(base_);}

CVParam::CVParam(CVID _cvid, float _value)
: base_(new boost::shared_ptr<b::CVParam>(new b::CVParam((pwiz::cv::CVID) _cvid, _value)))
{value_ = gcnew CVParamValue(base_);}

CVParam::CVParam(CVID _cvid, double _value)
: base_(new boost::shared_ptr<b::CVParam>(new b::CVParam((pwiz::cv::CVID) _cvid, _value)))
{value_ = gcnew CVParamValue(base_);}

CVParam::CVParam(CVID _cvid, System::Int32 _value)
: base_(new boost::shared_ptr<b::CVParam>(new b::CVParam((pwiz::cv::CVID) _cvid, _value)))
{value_ = gcnew CVParamValue(base_);}

//CVParam::CVParam(CVID _cvid, System::Int64 _value)
//{base_ = new b::CVParam((pwiz::cv::CVID) _cvid, _value);}

CVParam::CVParam(CVID _cvid, System::UInt32 _value)
: base_(new boost::shared_ptr<b::CVParam>(new b::CVParam((pwiz::cv::CVID) _cvid, _value)))
{value_ = gcnew CVParamValue(base_);}

//CVParam::CVParam(CVID _cvid, System::UInt64 _value)
//{base_ = new b::CVParam((pwiz::cv::CVID) _cvid, _value);}

CVParam::CVParam(CVID _cvid, System::String^ _value)
: base_(new boost::shared_ptr<b::CVParam>(new b::CVParam((pwiz::cv::CVID) _cvid, ToStdString(_value))))
{value_ = gcnew CVParamValue(base_);}

CVParam::CVParam(CVID _cvid, float _value, CVID _units)
: base_(new boost::shared_ptr<b::CVParam>(new b::CVParam((pwiz::cv::CVID) _cvid, _value, (pwiz::cv::CVID) _units)))
{value_ = gcnew CVParamValue(base_);}

CVParam::CVParam(CVID _cvid, double _value, CVID _units)
: base_(new boost::shared_ptr<b::CVParam>(new b::CVParam((pwiz::cv::CVID) _cvid, _value, (pwiz::cv::CVID) _units)))
{value_ = gcnew CVParamValue(base_);}

CVParam::CVParam(CVID _cvid, System::Int32 _value, CVID _units)
: base_(new boost::shared_ptr<b::CVParam>(new b::CVParam((pwiz::cv::CVID) _cvid, _value, (pwiz::cv::CVID) _units)))
{value_ = gcnew CVParamValue(base_);}

//CVParam::CVParam(CVID _cvid, System::Int64 _value, CVID _units)
//{base_ = new b::CVParam((pwiz::cv::CVID) _cvid, _value, (pwiz::cv::CVID) _units);}

CVParam::CVParam(CVID _cvid, System::UInt32 _value, CVID _units)
: base_(new boost::shared_ptr<b::CVParam>(new b::CVParam((pwiz::cv::CVID) _cvid, _value, (pwiz::cv::CVID) _units)))
{value_ = gcnew CVParamValue(base_);}

//CVParam::CVParam(CVID _cvid, System::UInt64 _value, CVID _units)
//{base_ = new b::CVParam((pwiz::cv::CVID) _cvid, _value, (pwiz::cv::CVID) _units);}

CVParam::CVParam(CVID _cvid, System::String^ _value, CVID _units)
: base_(new boost::shared_ptr<b::CVParam>(new b::CVParam((pwiz::cv::CVID) _cvid, ToStdString(_value), (pwiz::cv::CVID) _units)))
{value_ = gcnew CVParamValue(base_);}

CVParam::CVParam(CVID _cvid)
: base_(new boost::shared_ptr<b::CVParam>(new b::CVParam((pwiz::cv::CVID) _cvid)))
{value_ = gcnew CVParamValue(base_);}

CVParam::CVParam()
: base_(new boost::shared_ptr<b::CVParam>(new b::CVParam()))
{value_ = gcnew CVParamValue(base_);}

double CVParam::timeInSeconds()
{
    try { return (*base_)->timeInSeconds(); }
    CATCH_AND_FORWARD_CAST(value, "CVParam", "double")
}
bool CVParam::operator==(CVParam^ that) {return **base_ == **that->base_;}
bool CVParam::operator!=(CVParam^ that) {return **base_ != **that->base_;}
bool CVParam::operator==(CVID that) {return cvid == that;}
bool CVParam::operator!=(CVID that) {return cvid != that;}
bool CVParam::empty() {return (*base_)->empty();}




UserParamValue::UserParamValue(boost::shared_ptr<b::UserParam>* base)
: base_(new boost::shared_ptr<b::UserParam>(*base))
{LOG_CONSTRUCT(BOOST_PP_STRINGIZE(UserParamValue))}

UserParamValue::~UserParamValue()
{LOG_DESTRUCT(BOOST_PP_STRINGIZE(UserParamValue), true) SAFEDELETE(base_);}

UserParamValue::!UserParamValue()
{delete this;}

UserParam::UserParam(void* base, System::Object^ owner)
: base_(new boost::shared_ptr<b::UserParam>(static_cast<b::UserParam*>(base))), owner_(owner), value_(gcnew UserParamValue(base_))
{LOG_CONSTRUCT(BOOST_PP_STRINGIZE(UserParam))}

UserParam::UserParam(void* base)
: base_(new boost::shared_ptr<b::UserParam>(static_cast<b::UserParam*>(base))), owner_(nullptr), value_(gcnew UserParamValue(base_))
{LOG_CONSTRUCT(BOOST_PP_STRINGIZE(UserParam))}

UserParam::~UserParam()
{LOG_DESTRUCT(BOOST_PP_STRINGIZE(UserParam), (owner_ == nullptr)) if (owner_ == nullptr) SAFEDELETE(base_);}

UserParam::!UserParam()
{delete this;}

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
: base_(new boost::shared_ptr<b::UserParam>(new b::UserParam(ToStdString(_name), ToStdString(_value), ToStdString(_type), (pwiz::cv::CVID) _units))), owner_(nullptr)
{value_ = gcnew UserParamValue(base_);}

System::String^ UserParam::name::get() {return ToSystemString((*base_)->name);}
void UserParam::name::set(System::String^ value) {(*base_)->name = ToStdString(value);}

System::String^ UserParam::type::get() {return ToSystemString((*base_)->type);}
void UserParam::type::set(System::String^ value) {(*base_)->type = ToStdString(value);}

CVID UserParam::units::get() {return (CVID) (*base_)->units;}
void UserParam::units::set(CVID value) {(*base_)->units = (pwiz::cv::CVID) value;}

UserParamValue^ UserParam::value::get() {return value_;}

bool UserParam::empty()
{
    return (*base_)->empty();
}

double UserParam::timeInSeconds()
{
    try { return (*base_)->timeInSeconds(); }
    CATCH_AND_FORWARD_CAST(value, "UserParam", "double")
}

bool UserParam::operator==(UserParam^ that) {return (*base_) == *that->base_;}
bool UserParam::operator!=(UserParam^ that) {return (*base_) != *that->base_;}




ParamContainer::ParamContainer(b::ParamContainer* base)
: base_(base), owner_(nullptr)
{LOG_CONSTRUCT(BOOST_PP_STRINGIZE(ParamContainer))}

ParamContainer::~ParamContainer()
{LOG_DESTRUCT(BOOST_PP_STRINGIZE(ParamContainer), (owner_ == nullptr)) if (owner_ == nullptr) SAFEDELETE(base_);}

ParamContainer::ParamContainer() : base_(new b::ParamContainer()) {}
ParamGroupList^ ParamContainer::paramGroups::get() {return gcnew ParamGroupList(&base_->paramGroupPtrs, this);}
CVParamList^ ParamContainer::cvParams::get() {return gcnew CVParamList(&base_->cvParams, this);}
UserParamList^ ParamContainer::userParams::get() {return gcnew UserParamList(&base_->userParams, this);}

CVParam^ ParamContainer::cvParam(CVID cvid)
{
    return gcnew CVParam(new b::CVParam(base_->cvParam((pwiz::cv::CVID) cvid)));
}

CVParam^ ParamContainer::cvParamChild(CVID cvid)
{
    return gcnew CVParam(new b::CVParam(base_->cvParamChild((pwiz::cv::CVID) cvid)));
}

CVParamList^ ParamContainer::cvParamChildren(CVID cvid)
{
    return gcnew CVParamList(new std::vector<b::CVParam>(base_->cvParamChildren((pwiz::cv::CVID) cvid)));
}

bool ParamContainer::hasCVParam(CVID cvid)
{
    return base_->hasCVParam((pwiz::cv::CVID) cvid);
}

bool ParamContainer::hasCVParamChild(CVID cvid)
{
    return base_->hasCVParamChild((pwiz::cv::CVID) cvid);
}

UserParam^ ParamContainer::userParam(System::String^ name)
{
    return gcnew UserParam(new b::UserParam(base_->userParam(ToStdString(name))));
}

bool ParamContainer::empty()
{
    return base_->empty();
}

void ParamContainer::set(CVID cvid) {base_->set((pwiz::cv::CVID) cvid);}
void ParamContainer::set(CVID cvid, System::String^ value) {base_->set((pwiz::cv::CVID) cvid, ToStdString(value));}
void ParamContainer::set(CVID cvid, System::String^ value, CVID units) {base_->set((pwiz::cv::CVID) cvid, ToStdString(value), (pwiz::cv::CVID) units);}

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

System::String^ ParamGroup::id::get() {return ToSystemString((*base_)->id);}
void ParamGroup::id::set(System::String^ value) {(*base_)->id = ToStdString(value);}

bool ParamGroup::empty()
{
    return (*base_)->empty();
}


} // namespace data
} // namespace CLI
} // namespace pwiz
