//
// CVParam.cpp
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

#include "CVParam.hpp"

namespace pwiz {
namespace CLI {
namespace msdata {


CVParamValue::CVParamValue(boost::shared_ptr<pwiz::msdata::CVParam>* base)
: base_(new boost::shared_ptr<pwiz::msdata::CVParam>(*base))
{LOG_CONSTRUCT(BOOST_PP_STRINGIZE(CVParamValue))}

CVParamValue::~CVParamValue()
{LOG_DESTRUCT(BOOST_PP_STRINGIZE(CVParamValue)) SAFEDELETE(base_);}

CVParamValue::!CVParamValue()
{delete this;}

CVParam::CVParam(pwiz::msdata::CVParam* base, System::Object^ owner)
: base_(new boost::shared_ptr<pwiz::msdata::CVParam>(base)), owner_(owner), value_(gcnew CVParamValue(base_))
{LOG_CONSTRUCT(BOOST_PP_STRINGIZE(CVParam))}

CVParam::CVParam(pwiz::msdata::CVParam* base)
: base_(new boost::shared_ptr<pwiz::msdata::CVParam>(base)), owner_(nullptr), value_(gcnew CVParamValue(base_))
{LOG_CONSTRUCT(BOOST_PP_STRINGIZE(CVParam))}

CVParam::~CVParam()
{LOG_DESTRUCT(BOOST_PP_STRINGIZE(CVParam)) if (owner_ == nullptr) SAFEDELETE(base_);}

CVParam::!CVParam()
{delete this;}

CVParam::CVParam(CVID _cvid, float _value)
: base_(new boost::shared_ptr<pwiz::msdata::CVParam>(new pwiz::msdata::CVParam((pwiz::msdata::CVID) _cvid, _value)))
{value_ = gcnew CVParamValue(base_);}

CVParam::CVParam(CVID _cvid, double _value)
: base_(new boost::shared_ptr<pwiz::msdata::CVParam>(new pwiz::msdata::CVParam((pwiz::msdata::CVID) _cvid, _value)))
{value_ = gcnew CVParamValue(base_);}

CVParam::CVParam(CVID _cvid, System::Int32 _value)
: base_(new boost::shared_ptr<pwiz::msdata::CVParam>(new pwiz::msdata::CVParam((pwiz::msdata::CVID) _cvid, _value)))
{value_ = gcnew CVParamValue(base_);}

//CVParam::CVParam(CVID _cvid, System::Int64 _value)
//{base_ = new pwiz::msdata::CVParam((pwiz::msdata::CVID) _cvid, _value);}

CVParam::CVParam(CVID _cvid, System::UInt32 _value)
: base_(new boost::shared_ptr<pwiz::msdata::CVParam>(new pwiz::msdata::CVParam((pwiz::msdata::CVID) _cvid, _value)))
{value_ = gcnew CVParamValue(base_);}

//CVParam::CVParam(CVID _cvid, System::UInt64 _value)
//{base_ = new pwiz::msdata::CVParam((pwiz::msdata::CVID) _cvid, _value);}

CVParam::CVParam(CVID _cvid, System::String^ _value)
: base_(new boost::shared_ptr<pwiz::msdata::CVParam>(new pwiz::msdata::CVParam((pwiz::msdata::CVID) _cvid, ToStdString(_value))))
{value_ = gcnew CVParamValue(base_);}

CVParam::CVParam(CVID _cvid, float _value, CVID _units)
: base_(new boost::shared_ptr<pwiz::msdata::CVParam>(new pwiz::msdata::CVParam((pwiz::msdata::CVID) _cvid, _value, (pwiz::msdata::CVID) _units)))
{value_ = gcnew CVParamValue(base_);}

CVParam::CVParam(CVID _cvid, double _value, CVID _units)
: base_(new boost::shared_ptr<pwiz::msdata::CVParam>(new pwiz::msdata::CVParam((pwiz::msdata::CVID) _cvid, _value, (pwiz::msdata::CVID) _units)))
{value_ = gcnew CVParamValue(base_);}

CVParam::CVParam(CVID _cvid, System::Int32 _value, CVID _units)
: base_(new boost::shared_ptr<pwiz::msdata::CVParam>(new pwiz::msdata::CVParam((pwiz::msdata::CVID) _cvid, _value, (pwiz::msdata::CVID) _units)))
{value_ = gcnew CVParamValue(base_);}

//CVParam::CVParam(CVID _cvid, System::Int64 _value, CVID _units)
//{base_ = new pwiz::msdata::CVParam((pwiz::msdata::CVID) _cvid, _value, (pwiz::msdata::CVID) _units);}

CVParam::CVParam(CVID _cvid, System::UInt32 _value, CVID _units)
: base_(new boost::shared_ptr<pwiz::msdata::CVParam>(new pwiz::msdata::CVParam((pwiz::msdata::CVID) _cvid, _value, (pwiz::msdata::CVID) _units)))
{value_ = gcnew CVParamValue(base_);}

//CVParam::CVParam(CVID _cvid, System::UInt64 _value, CVID _units)
//{base_ = new pwiz::msdata::CVParam((pwiz::msdata::CVID) _cvid, _value, (pwiz::msdata::CVID) _units);}

CVParam::CVParam(CVID _cvid, System::String^ _value, CVID _units)
: base_(new boost::shared_ptr<pwiz::msdata::CVParam>(new pwiz::msdata::CVParam((pwiz::msdata::CVID) _cvid, ToStdString(_value), (pwiz::msdata::CVID) _units)))
{value_ = gcnew CVParamValue(base_);}

CVParam::CVParam(CVID _cvid)
: base_(new boost::shared_ptr<pwiz::msdata::CVParam>(new pwiz::msdata::CVParam((pwiz::msdata::CVID) _cvid)))
{value_ = gcnew CVParamValue(base_);}

CVParam::CVParam()
: base_(new boost::shared_ptr<pwiz::msdata::CVParam>(new pwiz::msdata::CVParam()))
{value_ = gcnew CVParamValue(base_);}

double CVParam::timeInSeconds() {return (*base_)->timeInSeconds();}
bool CVParam::operator==(CVParam^ that) {return **base_ == **that->base_;}
bool CVParam::operator!=(CVParam^ that) {return **base_ != **that->base_;}
bool CVParam::operator==(CVID that) {return cvid == that;}
bool CVParam::operator!=(CVID that) {return cvid != that;}
bool CVParam::empty() {return (*base_)->empty();}

} // namespace msdata
} // namespace CLI
} // namespace pwiz
