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
#include "pwiz/utility/misc/Std.hpp"
#include <boost/spirit/include/karma.hpp>


namespace pwiz {
namespace data {
    

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

double timeInSecondsHelper(CVID units, double value)
{
    if (units == UO_second) 
        return value;
    else if (units == UO_minute)
        return value * 60;
    else if (units == UO_hour)
        return value * 3600;
    else if (units == UO_millisecond)
        return value * 1e-3;
    else if (units == UO_microsecond)
        return value * 1e-6;
    else if (units == UO_nanosecond)
        return value * 1e-9;
    else if (units == UO_picosecond)
        return value * 1e-12;
    else if (units == MS_second_OBSOLETE) // mzML 1.0 support
        return value;
    else if (units == MS_minute_OBSOLETE) // mzML 1.0 support
        return value * 60;
    return 0; 
}

PWIZ_API_DECL double CVParam::timeInSeconds() const
{
    return timeInSecondsHelper(units, valueAs<double>());
}

template <typename T>
struct nosci_policy : boost::spirit::karma::real_policies<T>   
{
    //  we want to generate up to 12 fractional digits
    static unsigned int precision(T) { return 12; }
    //  we want the numbers always to be in fixed format
    static int floatfield(T) { return boost::spirit::karma::real_policies<T>::fmtflags::fixed; }
};

/// convenience function to return value without scientific notation (throws if not a double)
PWIZ_API_DECL std::string CVParam::valueFixedNotation() const
{
    std::string result = value;
    if (std::string::npos != result.find_first_of("eE"))
    {
        using namespace boost::spirit::karma;
        typedef real_generator<double, nosci_policy<double> > nosci_type;
        static const nosci_type nosci = nosci_type();
        char buffer[256];
        char* p = buffer;
        double d = valueAs<double>();
        generate(p, nosci, d);
        *p = 0;
        result = buffer;
    }
    return result;
}

PWIZ_API_DECL ostream& operator<<(ostream& os, const CVParam& param)
{
    os << cvTermInfo(param.cvid).name;

    if (!param.value.empty())
        os << ": " << param.value;
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


PWIZ_API_DECL double UserParam::timeInSeconds() const
{
    return timeInSecondsHelper(units, valueAs<double>());
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


PWIZ_API_DECL vector<CVParam> ParamContainer::cvParamChildren(CVID cvid) const
{
    vector<CVParam> results;

    // first look in our own cvParams

    vector<CVParam>::const_iterator it;

    for(const CVParam& cvParam : cvParams)
    {
        if (cvIsA(cvParam.cvid, cvid))
            results.push_back(cvParam);
    }

    // then recurse into paramGroupPtrs

    for(const ParamGroupPtr& paramGroupPtr : paramGroupPtrs)
    {
        vector<CVParam> pgResults = paramGroupPtr->cvParamChildren(cvid);
        results.insert(results.end(), pgResults.begin(), pgResults.end());
    }

    return results;
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


template <typename T>
struct double12_policy : boost::spirit::karma::real_policies<T>   
{
    //  we want to generate up to 12 fractional digits
    static unsigned int precision(T) { return 12; }
};


PWIZ_API_DECL void ParamContainer::set(CVID cvid, double value, CVID units)
{
    // HACK: karma has a stack overflow on subnormal values, so we clamp to normalized values
    if (value > 0)
        value = max(numeric_limits<double>::min(), value);
    else if (value < 0)
        value = min(-numeric_limits<double>::min(), value);

    using namespace boost::spirit::karma;
    typedef real_generator<double, double12_policy<double> > double12_type;
    static const double12_type double12 = double12_type();
    char buffer[256];
    char* p = buffer;
    generate(p, double12, value);
    set(cvid, std::string(&buffer[0], p), units);
}


PWIZ_API_DECL void ParamContainer::set(CVID cvid, int value, CVID units)
{
    using namespace boost::spirit::karma;
    static const int_generator<int> intgen = int_generator<int>();
    char buffer[256];
    char* p = buffer;
    generate(p, intgen, value);
    set(cvid, std::string(&buffer[0], p), units);
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


