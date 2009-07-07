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

#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "SharedCLI.hpp"
#include "cv.hpp"
#include "../../../data/msdata/CVParam.hpp"
#pragma warning( pop )


namespace pwiz {
namespace CLI {
namespace msdata {


/// <summary>
/// A convenient variant type for casting to non-string types
/// </summary>
public ref class CVParamValue
{
    internal: CVParamValue(boost::shared_ptr<pwiz::msdata::CVParam>* base);
              virtual ~CVParamValue();
              !CVParamValue();
    internal: boost::shared_ptr<pwiz::msdata::CVParam>* base_;

    public:
    virtual System::String^ ToString() override {return (System::String^) this;}
    static operator System::String^(CVParamValue^ value) {return gcnew System::String((*value->base_)->value.c_str());};
    static explicit operator float(CVParamValue^ value) {return (*value->base_)->valueAs<float>();}
    static operator double(CVParamValue^ value) {return (*value->base_)->valueAs<double>();}
    static explicit operator int(CVParamValue^ value) {return (*value->base_)->valueAs<int>();}
    static explicit operator System::UInt64(CVParamValue^ value) {return (System::UInt64) (*value->base_)->valueAs<size_t>();}
    static explicit operator bool(CVParamValue^ value) {return (*value->base_)->value == "true";}
    CVParamValue^ operator=(System::String^ value) {(*base_)->value = ToStdString(value); return this;} 
};

/// <summary>
/// represents a tag-value pair, where the tag comes from the controlled vocabulary
/// </summary>
public ref class CVParam
{
    internal: CVParam(pwiz::msdata::CVParam* base, System::Object^ owner);
              CVParam(pwiz::msdata::CVParam* base);
              virtual ~CVParam();
              !CVParam();
    internal: boost::shared_ptr<pwiz::msdata::CVParam>* base_;
              System::Object^ owner_;
              CVParamValue^ value_;

    public:

    /// <summary>
    /// the enumerated CV term the parameter represents
    /// </summary>
    property CVID cvid
    {
        CVID get() {return (CVID) (*base_)->cvid;}
        void set(CVID value) {(*base_)->cvid = (pwiz::CVID) value;}
    }

    /// <summary>
    /// the value of the term
    /// <para>- stored as string but may represent variant types</para>
    /// <para>- must be empty for controlled value terms</para>
    /// </summary>
    property CVParamValue^ value
    {
        CVParamValue^ get() {return value_;}
    }

    /// <summary>
    /// the enumerated CV term defining the units used to represent the value
    /// </summary>
    property CVID units
    {
        CVID get() {return (CVID) (*base_)->units;}
        void set(CVID value) {(*base_)->units = (pwiz::CVID) value;}
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

    /// <summary>
    /// constructs a non-valued CVParam
    /// </summary>
    CVParam(CVID _cvid);

    /// <summary>
    /// constructs an empty/null CVParam with CVID_Unknown
    /// </summary>
    CVParam();

    /// <summary>
    /// convenience function to return string for the cvid 
    /// </summary>
    property System::String^ name { System::String^ get() {return gcnew System::String((*base_)->name().c_str());} }

    /// <summary>
    /// convenience function to return string for the units 
    /// </summary>
    property System::String^ unitsName { System::String^ get() {return gcnew System::String((*base_)->unitsName().c_str());} }

    /// <summary>
    /// convenience function to return time in seconds (throws if units not a time unit)
    /// </summary>
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
