//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#define PWIZ_SOURCE

#include "ParamTypes.hpp"
#include "diff_std.hpp"
#include <iostream>
#include <algorithm>


namespace pwiz {
namespace data {


using namespace std;


//
// CVParam
//


PWIZ_API_DECL CVParam::~CVParam() {}

PWIZ_API_DECL string CVParam::name() const
{
    return cvTermInfo(cvid).name;
}


PWIZ_API_DECL string CVParam::unitsName() const
{
    return cvTermInfo(units).name;
}


PWIZ_API_DECL double CVParam::timeInSeconds() const
{
    if (units == UO_second) 
        return valueAs<double>();
    else if (units == UO_minute)
        return valueAs<double>() * 60;
    else if (units == UO_hour)
        return valueAs<double>() * 3600;
    else if (units == MS_second_OBSOLETE) // mzML 1.0 support
        return valueAs<double>();
    else if (units == MS_minute_OBSOLETE) // mzML 1.0 support
        return valueAs<double>() * 60;
    return 0; 
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const CVParam& param)
{
    os << cvTermInfo(param.cvid).name << ": " << param.value;

    if (param.units != CVID_Unknown)
        os << " " << cvTermInfo(param.units).name << "(s)";

    return os;
}



//
// UserParam
//


PWIZ_API_DECL
UserParam::UserParam(const string& _name, 
                     const string& _value, 
                     const string& _type,
                     CVID _units)
:   name(_name), value(_value), type(_type), units(_units)
{}


PWIZ_API_DECL UserParam::~UserParam() {}


PWIZ_API_DECL UserParam::UserParam(const UserParam& other) {*this = other;}
PWIZ_API_DECL UserParam& UserParam::operator=(const UserParam& rhs)
{
    name = rhs.name;
    value = rhs.value;
    type = rhs.type;
    units = rhs.units;
    return *this;
}


PWIZ_API_DECL bool UserParam::empty() const 
{
    return name.empty() && value.empty() && type.empty() && units==CVID_Unknown;
}


PWIZ_API_DECL bool UserParam::operator==(const UserParam& that) const
{
    return (name==that.name && value==that.value && type==that.type && units==that.units);
}


PWIZ_API_DECL bool UserParam::operator!=(const UserParam& that) const
{
    return !operator==(that); 
}


//
// ParamContainer
//


PWIZ_API_DECL CVParam ParamContainer::cvParam(CVID cvid) const
{
    // first look in our own cvParams

    vector<CVParam>::const_iterator it = 
        find_if(cvParams.begin(), cvParams.end(), CVParamIs(cvid));
   
    if (it!=cvParams.end()) return *it;

    // then recurse into paramGroupPtrs

    for (vector<ParamGroupPtr>::const_iterator jt=paramGroupPtrs.begin();
         jt!=paramGroupPtrs.end(); ++jt)
    {
        CVParam result = jt->get() ? (*jt)->cvParam(cvid) : CVParam();
        if (result.cvid != CVID_Unknown)
            return result;
    }

    return CVParam();
}


PWIZ_API_DECL CVParam ParamContainer::cvParamChild(CVID cvid) const
{
    // first look in our own cvParams

    vector<CVParam>::const_iterator it = 
        find_if(cvParams.begin(), cvParams.end(), CVParamIsChildOf(cvid));
   
    if (it!=cvParams.end()) return *it;

    // then recurse into paramGroupPtrs

    for (vector<ParamGroupPtr>::const_iterator jt=paramGroupPtrs.begin();
         jt!=paramGroupPtrs.end(); ++jt)
    {
        CVParam result = jt->get() ? (*jt)->cvParamChild(cvid) : CVParam();
        if (result.cvid != CVID_Unknown)
            return result;
    }

    return CVParam();
}


PWIZ_API_DECL bool ParamContainer::hasCVParam(CVID cvid) const
{
    CVParam param = cvParam(cvid);
    return (param.cvid != CVID_Unknown);
}


PWIZ_API_DECL bool ParamContainer::hasCVParamChild(CVID cvid) const
{
    CVParam param = cvParamChild(cvid);
    return (param.cvid != CVID_Unknown);
}


namespace {
struct HasName
{
    string name_;
    HasName(const string& name) : name_(name) {}
    bool operator()(const UserParam& userParam) {return name_ == userParam.name;}
};
} // namespace


PWIZ_API_DECL UserParam ParamContainer::userParam(const string& name) const
{
    vector<UserParam>::const_iterator it = 
        find_if(userParams.begin(), userParams.end(), HasName(name));
    return it!=userParams.end() ? *it : UserParam();
}


PWIZ_API_DECL void ParamContainer::set(CVID cvid, const string& value, CVID units)
{
    vector<CVParam>::iterator it = find_if(cvParams.begin(), cvParams.end(), CVParamIs(cvid));
   
    if (it!=cvParams.end())
    {
        it->value = value;
        it->units = units;
        return;
    }

    cvParams.push_back(CVParam(cvid, value, units));
}


PWIZ_API_DECL bool ParamContainer::empty() const
{
    return paramGroupPtrs.empty() && cvParams.empty() && userParams.empty();
}


PWIZ_API_DECL void ParamContainer::clear()
{
    paramGroupPtrs.clear();
    cvParams.clear();
    userParams.clear();
}


PWIZ_API_DECL bool ParamContainer::operator==(const ParamContainer& that) const
{
    return !Diff<ParamContainer>(*this, that);
}


PWIZ_API_DECL bool ParamContainer::operator!=(const ParamContainer& that) const
{
    return !(*this == that);
}


//
// ParamGroup
//


PWIZ_API_DECL ParamGroup::ParamGroup(const string& _id)
: id(_id) 
{}


PWIZ_API_DECL bool ParamGroup::empty() const 
{
    return id.empty() && ParamContainer::empty();
}


} // namespace data
} // namespace pwiz


