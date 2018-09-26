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


#ifndef _PARAMTYPES_HPP_
#define _PARAMTYPES_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/optimized_lexical_cast.hpp"
#include "cv.hpp"
#include <iosfwd>
#include <vector>
#include <boost/shared_ptr.hpp>


namespace pwiz {
namespace data {


using namespace pwiz::cv;


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

    /// convenience function to return value without scientific notation (throws if not a double)
    std::string valueFixedNotation() const;

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


/// Uncontrolled user parameters (essentially allowing free text). Before using these, one should verify whether there is an appropriate CV term available, and if so, use the CV term instead
struct PWIZ_API_DECL UserParam
{
    /// the name for the parameter.
    std::string name;

    /// the value for the parameter, where appropriate.
    std::string value;

    /// the datatype of the parameter, where appropriate (e.g.: xsd:float).
    std::string type;

    /// an optional CV parameter for the unit term associated with the value, if any (e.g. MS_electron_volt).
    CVID units;

    UserParam(const std::string& _name = "", 
              const std::string& _value = "", 
              const std::string& _type = "",
              CVID _units = CVID_Unknown);

    ~UserParam();

    UserParam(const UserParam& other);
    UserParam& operator=(const UserParam& rhs);

    /// convenience function to return time in seconds (throws if units not a time unit)
    double timeInSeconds() const;

    /// Templated value access with type conversion
    template<typename value_type>
    value_type valueAs() const
    {
        return !value.empty() ? boost::lexical_cast<value_type>(value) 
                              : boost::lexical_cast<value_type>(0);
    }

    /// returns true iff name, value, type, and units are all empty
    bool empty() const;

    /// returns true iff name, value, type, and units are all pairwise equal
    bool operator==(const UserParam& that) const;

    /// returns !(this==that)
    bool operator!=(const UserParam& that) const;
};


// Special case for bool (outside the class for gcc 3.4, and inline for msvc)
template<>
inline bool UserParam::valueAs<bool>() const
{
    return value == "true";
}


struct ParamGroup;
typedef boost::shared_ptr<ParamGroup> ParamGroupPtr;


/// The base class for elements that may contain cvParams, userParams, or paramGroup references
struct PWIZ_API_DECL ParamContainer
{
    /// a collection of references to ParamGroups
    std::vector<ParamGroupPtr> paramGroupPtrs;

    /// a collection of controlled vocabulary terms
    std::vector<CVParam> cvParams;

    /// a collection of uncontrolled user terms
    std::vector<UserParam> userParams;
    
    /// finds cvid in the container:
    /// - returns first CVParam result such that (result.cvid == cvid); 
    /// - if not found, returns CVParam(CVID_Unknown)
    /// - recursive: looks into paramGroupPtrs
    CVParam cvParam(CVID cvid) const;

    /// finds child of cvid in the container:
    /// - returns first CVParam result such that (result.cvid is_a cvid); 
    /// - if not found, CVParam(CVID_Unknown)
    /// - recursive: looks into paramGroupPtrs
    CVParam cvParamChild(CVID cvid) const;

    /// finds cvid in the container:
    /// - returns first CVParam result's value such that (result.cvid == cvid); 
    /// - if not found, returns the given default value
    /// - recursive: looks into paramGroupPtrs
    template<typename ValueT>
    ValueT cvParamValueOrDefault(CVID cvid, ValueT defaultValue) const
    {
        CVParam p = cvParam(cvid);
        return p.empty() ? defaultValue : p.valueAs<ValueT>();
    }

    /// finds child of cvid in the container:
    /// - returns first CVParam result's value such that (result.cvid is_a cvid); 
    /// - if not found, returns the given default value
    /// - recursive: looks into paramGroupPtrs
    template<typename ValueT>
    ValueT cvParamChildValueOrDefault(CVID cvid, ValueT defaultValue) const
    {
        CVParam p = cvParamChild(cvid);
        return p.empty() ? defaultValue : p.valueAs<ValueT>();
    }

    /// finds all children of cvid in the container:
    /// - returns all CVParam results such that (result.cvid is_a cvid); 
    /// - if not found, empty vector
    /// - recursive: looks into paramGroupPtrs
    std::vector<CVParam> cvParamChildren(CVID cvid) const;

    /// returns true iff cvParams contains exact cvid (recursive)
    bool hasCVParam(CVID cvid) const;

    /// returns true iff cvParams contains a child (is_a) of cvid (recursive)
    bool hasCVParamChild(CVID cvid) const;

    /// finds UserParam with specified name 
    /// - returns UserParam() if name not found 
    /// - not recursive: looks only at local userParams
    UserParam userParam(const std::string&) const; 

    /// set/add a CVParam (not recursive)
    void set(CVID cvid, const std::string& value = "", CVID units = CVID_Unknown);

    /// set/add a CVParam (not recursive)
    void set(CVID cvid, double value, CVID units = CVID_Unknown);

    /// set/add a CVParam (not recursive)
    void set(CVID cvid, int value, CVID units = CVID_Unknown);

    /// set/add a CVParam (not recursive)
    template <typename value_type>
    void set(CVID cvid, value_type value, CVID units = CVID_Unknown)
    {
        set(cvid, boost::lexical_cast<std::string>(value), units);
    }

    /// returns true iff the element contains no params or param groups
    bool empty() const;

    /// clears the collections
    void clear();

    /// returns true iff this and that have the exact same cvParams and userParams
    /// - recursive: looks into paramGroupPtrs
    bool operator==(const ParamContainer& that) const;

    /// returns !(this==that)
    bool operator!=(const ParamContainer& that) const;
};


/// special case for bool (outside the class for gcc 3.4, and inline for msvc)
template<>
inline void ParamContainer::set<bool>(CVID cvid, bool value, CVID units)
{
    set(cvid, (value ? "true" : "false"), units);
}


/// A collection of CVParam and UserParam elements that can be referenced from elsewhere in this mzML document by using the 'paramGroupRef' element in that location to reference the 'id' attribute value of this element. 
struct PWIZ_API_DECL ParamGroup : public ParamContainer
{
    /// the identifier with which to reference this ReferenceableParamGroup.
    std::string id;

    ParamGroup(const std::string& _id = "");

    /// returns true iff the element contains no params or param groups
    bool empty() const;
};


} // namespace data
} // namespace pwiz


#endif // _PARAMTYPES_HPP_
