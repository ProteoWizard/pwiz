//
// CVParam.hpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


#ifndef _CVPARAM_HPP_
#define _CVPARAM_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/optimized_lexical_cast.hpp"
#include "cv.hpp"
#include <iosfwd>
#include <vector>


namespace pwiz {
namespace msdata {


/// represents a tag-value pair, where the tag comes from the controlled vocabulary
struct PWIZ_API_DECL CVParam
{
    CVID cvid;
    std::string value;
    CVID units;

    CVParam(CVID _cvid, float _value, CVID _units = CVID_Unknown)
    :   cvid(_cvid), 
        value(boost::lexical_cast<std::string>(_value)),
        units(_units)
    {}

    CVParam(CVID _cvid, double _value, CVID _units = CVID_Unknown)
    :   cvid(_cvid), 
        value(boost::lexical_cast<std::string>(_value)),
        units(_units)
    {}

    CVParam(CVID _cvid, int _value, CVID _units = CVID_Unknown)
    :   cvid(_cvid), 
        value(boost::lexical_cast<std::string>(_value)),
        units(_units)
    {}

    CVParam(CVID _cvid, long _value, CVID _units = CVID_Unknown)
    :   cvid(_cvid), 
        value(boost::lexical_cast<std::string>(_value)),
        units(_units)
    {}

    CVParam(CVID _cvid, unsigned int _value, CVID _units = CVID_Unknown)
    :   cvid(_cvid), 
        value(boost::lexical_cast<std::string>(_value)),
        units(_units)
    {}

    CVParam(CVID _cvid, unsigned long _value, CVID _units = CVID_Unknown)
    :   cvid(_cvid), 
        value(boost::lexical_cast<std::string>(_value)),
        units(_units)
    {}

    CVParam(CVID _cvid, std::string _value, CVID _units = CVID_Unknown)
    :   cvid(_cvid), 
        value(_value),
        units(_units)
    {}

    CVParam(CVID _cvid, const char* _value, CVID _units = CVID_Unknown)
    :   cvid(_cvid), 
        value(_value),
        units(_units)
    {}

    /// special case for bool (no lexical_cast)
    CVParam(CVID _cvid, bool _value, CVID _units = CVID_Unknown)
    :   cvid(_cvid), value(_value ? "true" : "false"), units(_units)
    {}

    /// constructor for non-valued CVParams
    CVParam(CVID _cvid = CVID_Unknown)
    :   cvid(_cvid), units(CVID_Unknown)
    {}

    ~CVParam();

    /// templated value access with type conversion
    template<typename value_type>
    value_type valueAs() const
    {
        return !value.empty() ? boost::lexical_cast<value_type>(value) 
                              : boost::lexical_cast<value_type>(0);
    } 

    /// convenience function to return string for the cvid 
    std::string name() const;

    /// convenience function to return string for the units 
    std::string unitsName() const;

    /// convenience function to return time in seconds (throws if units not a time unit)
    double timeInSeconds() const;

    /// equality operator
    bool operator==(const CVParam& that) const
    {
        return that.cvid==cvid && that.value==value && that.units==units;
    }

    /// inequality operator
    bool operator!=(const CVParam& that) const
    {
        return !operator==(that);
    }

    bool empty() const {return cvid==CVID_Unknown && value.empty() && units==CVID_Unknown;}
};


/// functor for finding CVParam with specified exact CVID in a collection of CVParams:
///
/// vector<CVParam>::const_iterator it =
///     find_if(params.begin(), params.end(), CVParamIs(MS_software));
///
struct PWIZ_API_DECL CVParamIs 
{
    CVParamIs(CVID cvid) : cvid_(cvid) {}
    bool operator()(const CVParam& param) const {return param.cvid == cvid_;}
    CVID cvid_;
};


/// functor for finding children of a specified CVID in a collection of CVParams:
///
/// vector<CVParam>::const_iterator it =
///     find_if(params.begin(), params.end(), CVParamIsChildOf(MS_software));
///
struct PWIZ_API_DECL CVParamIsChildOf
{
    CVParamIsChildOf(CVID cvid) : cvid_(cvid) {}
    bool operator()(const CVParam& param) const {return cvIsA(param.cvid, cvid_);}
    CVID cvid_;
};


/// special case for bool (no lexical_cast)
/// (this has to be outside the class for gcc 3.4, inline for msvc)
template<>
inline bool CVParam::valueAs<bool>() const
{
    return value == "true";
}


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const CVParam& param);


} // namespace msdata
} // namespace pwiz


#endif // _CVPARAM_HPP_


