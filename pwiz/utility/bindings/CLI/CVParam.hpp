//
// CVParam.hpp
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

#ifndef _CVPARAM_HPP_CLI_
#define _CVPARAM_HPP_CLI_

#include <stdlib.h>
#include <vcclr.h>
#include <string>

#include "cv.hpp"
#include "../../../data/msdata/CVParam.hpp"
#include <boost/shared_ptr.hpp>

namespace pwiz {
namespace CLI {
namespace msdata {

inline std::string ToStdString( System::String^ source )
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
		throw std::runtime_error("error converting System::String to std::string");
	return target;
}

public ref class CVParamValue
{
    internal: CVParamValue(pwiz::msdata::CVParam* base);
    internal: pwiz::msdata::CVParam* base_;

    public:
    virtual System::String^ ToString() override {return (System::String^) this;}
    static operator System::String^(CVParamValue^ value) {return gcnew System::String(value->base_->value.c_str());};
    static explicit operator float(CVParamValue^ value) {return value->base_->valueAs<float>();}
    static operator double(CVParamValue^ value) {return value->base_->valueAs<double>();}
    static explicit operator int(CVParamValue^ value) {return value->base_->valueAs<int>();}
    static explicit operator System::UInt64(CVParamValue^ value) {return (System::UInt64) value->base_->valueAs<size_t>();}
    static explicit operator bool(CVParamValue^ value) {return value->base_->value == "true";}
    CVParamValue^ operator=(System::String^ value) {base_->value = ToStdString(value); return this;} 
};

/// represents a tag-value pair, where the tag comes from the controlled vocabulary
public ref class CVParam
{
    internal: CVParam(pwiz::msdata::CVParam* base);
	internal: pwiz::msdata::CVParam* base_;
              CVParamValue^ value_;

    public:
    property CVID cvid
    {
        CVID get() {return (CVID) base_->cvid;}
        void set(CVID value) {base_->cvid = (pwiz::msdata::CVID) value;}
    }

    property CVParamValue^ value
    {
        CVParamValue^ get() {return value_;}
    }

    property CVID units
    {
        CVID get() {return (CVID) base_->units;}
        void set(CVID value) {base_->units = (pwiz::msdata::CVID) value;}
    }

    CVParam(CVID _cvid, float _value);
    CVParam(CVID _cvid, double _value);
    CVParam(CVID _cvid, System::Int32 _value);
    //CVParam(CVID _cvid, System::Int64 _value);
    CVParam(CVID _cvid, System::UInt32 _value);
    //CVParam(CVID _cvid, System::UInt64 _value);
    CVParam(CVID _cvid, System::String^ _value);

    CVParam(CVID _cvid, float _value, CVID _units);
    CVParam(CVID _cvid, double _value, CVID _units);
    CVParam(CVID _cvid, System::Int32 _value, CVID _units);
    //CVParam(CVID _cvid, System::Int64 _value, CVID _units);
    CVParam(CVID _cvid, System::UInt32 _value, CVID _units);
    //CVParam(CVID _cvid, System::UInt64 _value, CVID _units);
    CVParam(CVID _cvid, System::String^ _value, CVID _units);

    /// constructor for non-valued CVParams
    CVParam(CVID _cvid);
    CVParam();

    /// convenience function to return string for the cvid 
    property System::String^ name { System::String^ get() {return gcnew System::String(base_->name().c_str());} }

    /// convenience function to return string for the units 
    property System::String^ unitsName { System::String^ get() {return gcnew System::String(base_->unitsName().c_str());} }

    /// convenience function to return time in seconds (throws if units not a time unit)
    double timeInSeconds();

    bool operator==(CVParam^ that);
    bool operator!=(CVParam^ that);
    bool operator==(CVID that);
    bool operator!=(CVID that);
    bool empty();
};

} // namespace msdata
} // namespace CLI
} // namespace pwiz

#endif // _CVPARAM_HPP_CLI_
