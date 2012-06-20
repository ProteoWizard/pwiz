//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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


#ifndef _DIFF_STD_HPP_
#define _DIFF_STD_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include <string>
#include <vector>
#include <cmath>
#include <limits>
#include <stdexcept>
#include "ParamTypes.hpp"
#include <boost/logic/tribool.hpp>


namespace pwiz {
namespace data {

    
struct BaseDiffConfig
{
    BaseDiffConfig(double _precision = 1e-6) : precision(_precision), partialDiffOK(false), ignoreVersions(false) {}
    double precision;
    bool partialDiffOK; // if true, can stop checking at first difference found
	bool ignoreVersions; // if true, don't sweat version number mismatches
};


namespace diff_impl {


PWIZ_API_DECL
void diff(const std::string& a,
          const std::string& b,
          std::string& a_b,
          std::string& b_a,
          const BaseDiffConfig& config);

// special handling for strings which are likely
// to differ only by a trailing version number
PWIZ_API_DECL
void diff_ids(const std::string& a,
              const std::string& b,
              std::string& a_b,
              std::string& b_a,
              const BaseDiffConfig& config);

PWIZ_API_DECL
void diff(const boost::logic::tribool& a, 
          const boost::logic::tribool& b, 
          boost::logic::tribool& a_b, 
          boost::logic::tribool& b_a,
          const BaseDiffConfig& config);

PWIZ_API_DECL
void diff(const CV& a,
          const CV& b,
          CV& a_b,
          CV& b_a,
          const BaseDiffConfig& config);

PWIZ_API_DECL
void diff(CVID a,
          CVID b,
          CVID& a_b,
          CVID& b_a,
          const BaseDiffConfig& config);

PWIZ_API_DECL
void diff(const CVParam& a,
          const CVParam& b,
          CVParam& a_b,
          CVParam& b_a,
          const BaseDiffConfig& config);

PWIZ_API_DECL
void diff(const UserParam& a,
          const UserParam& b,
          UserParam& a_b,
          UserParam& b_a,
          const BaseDiffConfig& config);

PWIZ_API_DECL
void diff(const ParamContainer& a,
          const ParamContainer& b,
          ParamContainer& a_b,
          ParamContainer& b_a,
          const BaseDiffConfig& config);

PWIZ_API_DECL
void diff(const ParamGroup& a,
          const ParamGroup& b,
          ParamGroup& a_b,
          ParamGroup& b_a,
          const BaseDiffConfig& config);


} // namespace diff_impl


///     
/// Calculate diffs of objects in a ProteoWizard data model hierarchy.
///
/// A diff between two objects a and b calculates the set differences
/// a\b and b\a.
///
/// The Diff struct acts as a functor, but also stores the 
/// results of the diff calculation.  
///
/// The bool conversion operator is provided to indicate whether 
/// the two objects are different (either a\b or b\a is non-empty).
///
/// object_type requirements:
///   object_type a;
///   a.empty();
///   pwiz::data::diff::diff(const object_type& a, const object_type& b, object_result_type& a_b, object_result_type& b_a);
///
/// config_type must be pwiz::data::diff::BaseDiffConfig or derived from it
///
template <typename object_type, typename config_type = BaseDiffConfig, typename object_result_type = object_type>
struct Diff
{
    Diff(const config_type& config = config_type())
    :   config_(config)
    {}

    Diff(const object_type& a,
         const object_type& b,
         const config_type& config = config_type())
    :   config_(config)
    {
        diff_impl::diff(a, b, a_b, b_a, config_);
    }

    object_result_type a_b;
    object_result_type b_a;

    /// conversion to bool, with same semantics as *nix diff command:
    ///  true == different
    ///  false == not different
    operator bool() {return !(a_b.empty() && b_a.empty());}

    Diff& operator()(const object_type& a,
                     const object_type& b)
    {
        diff_impl::diff(a, b, a_b, b_a, config_);
        return *this;
    }

    private:
    config_type config_;
};


template <typename textwriter_type, typename diff_type>
std::string diff_string(const diff_type& diff)
{
    std::ostringstream os;
    textwriter_type write(os, 1);

    if (!diff.a_b.empty())
    {            
        os << "+\n";
        write(diff.a_b);
    }

    if (!diff.b_a.empty())
    {            
        os << "-\n";
        write(diff.b_a);
    }

    return os.str();
}


/// stream insertion of Diff results
template <typename textwriter_type, typename object_type, typename config_type>
std::ostream& operator<<(std::ostream& os, const Diff<object_type, config_type>& diff)
{
    textwriter_type write(os, 1);

    if (!diff.a_b.empty())
    {            
        os << "+\n";
        write(diff.a_b);
    }

    if (!diff.b_a.empty())
    {            
        os << "-\n";
        write(diff.b_a);
    }

    return os;
}


namespace diff_impl {


template <typename string_type>
void diff_string(const string_type& a,
                 const string_type& b,
                 string_type& a_b,
                 string_type& b_a)
{
    a_b.clear();
    b_a.clear();
    
    if (a != b)
    {
        a_b = a;
        b_a = b;
    }
}


template <typename char_type>
void diff_char(const char_type& a,
               const char_type& b,
               char_type& a_b,
               char_type& b_a)
{
    a_b = 0;
    b_a = 0;
    
    if (a != b)
    {
        a_b = a;
        b_a = b;
    }
}


template <typename integral_type>
void diff_integral(const integral_type& a, 
                   const integral_type& b, 
                   integral_type& a_b, 
                   integral_type& b_a,
                   const BaseDiffConfig& config)
{
    a_b = integral_type();
    b_a = integral_type();
    
    if (a != b)
    {
        a_b = static_cast<integral_type>(a);
        b_a = static_cast<integral_type>(b);
    }
}


template <typename floating_type>
void diff_floating(const floating_type& a,
                   const floating_type& b,
                   floating_type& a_b,
                   floating_type& b_a,
                   const BaseDiffConfig& config)
{
    a_b = 0;
    b_a = 0;

    if (fabs(a - b) > config.precision + std::numeric_limits<floating_type>::epsilon())
    {
        a_b = fabs(a - b);
        b_a = fabs(a - b);
    }
}


/// measure maximum relative difference between elements in the vectors
template <typename floating_type>
floating_type maxdiff(const std::vector<floating_type>& a, const std::vector<floating_type>& b)
{
    if (a.size() != b.size()) 
        throw std::runtime_error("[Diff::maxdiff()] Sizes differ.");

    typename std::vector<floating_type>::const_iterator i = a.begin(); 
    typename std::vector<floating_type>::const_iterator j = b.begin(); 

    floating_type max = 0;

    for (; i!=a.end(); ++i, ++j)
    {
        floating_type denominator = std::min(*i, *j);
        if (denominator == 0) denominator = 1;
        floating_type current = fabs(*i - *j)/denominator;
        if (max < current) max = current;

    }

    return max;
}


template <typename object_type>
void vector_diff(const std::vector<object_type>& a,
                 const std::vector<object_type>& b,
                 std::vector<object_type>& a_b,
                 std::vector<object_type>& b_a)
{
    // calculate set differences of two vectors

    a_b.clear();
    b_a.clear();

    for (typename std::vector<object_type>::const_iterator it=a.begin(); it!=a.end(); ++it)
        if (std::find(b.begin(), b.end(), *it) == b.end())
            a_b.push_back(*it);

    for (typename std::vector<object_type>::const_iterator it=b.begin(); it!=b.end(); ++it)
        if (std::find(a.begin(), a.end(), *it) == a.end())
            b_a.push_back(*it);
}


template <typename object_type>
struct HasID
{
    const std::string& id_;
    HasID(const std::string& id) : id_(id) {}
    bool operator()(const boost::shared_ptr<object_type>& objectPtr) {return objectPtr->id == id_;}
};


template <typename object_type, typename config_type>
class Same
{
    public:

    Same(const object_type& object,
         const config_type& config)
    :   mine_(object), config_(config)
    {}

    bool operator()(const object_type& yours)
    {
        // true iff yours is the same as mine
        return !Diff<object_type, config_type>(mine_, yours, config_);
    }

    private:
    const object_type& mine_;
    const config_type& config_;
};


template <typename object_type, typename config_type>
void vector_diff_diff(const std::vector<object_type>& a,
                      const std::vector<object_type>& b,
                      std::vector<object_type>& a_b,
                      std::vector<object_type>& b_a,
                      const config_type& config)
{
    // calculate set differences of two vectors, using diff on each object

    a_b.clear();
    b_a.clear();

    for (typename std::vector<object_type>::const_iterator it=a.begin(); it!=a.end(); ++it)
        if (std::find_if(b.begin(), b.end(), Same<object_type, config_type>(*it, config)) == b.end())
            a_b.push_back(*it);

    for (typename std::vector<object_type>::const_iterator it=b.begin(); it!=b.end(); ++it)
        if (std::find_if(a.begin(), a.end(), Same<object_type, config_type>(*it, config)) == a.end())
            b_a.push_back(*it);
}


template <typename object_type, typename config_type>
class SameDeep
{
    public:

    SameDeep(const object_type& object,
             const config_type& config)
    :   mine_(object), config_(config)
    {}

    bool operator()(const boost::shared_ptr<object_type>& yours)
    {
        // true iff yours is the same as mine
        return !Diff<object_type, config_type>(mine_, *yours, config_);
    }

    private:
    const object_type& mine_;
    const config_type& config_;
};


template <typename object_type, typename config_type>
void vector_diff_deep(const std::vector< boost::shared_ptr<object_type> >& a,
                      const std::vector< boost::shared_ptr<object_type> >& b,
                      std::vector< boost::shared_ptr<object_type> >& a_b,
                      std::vector< boost::shared_ptr<object_type> >& b_a,
                      const config_type& config)
{
    // calculate set differences of two vectors of ObjectPtrs (deep compare using diff)

    a_b.clear();
    b_a.clear();

    config_type quick_config(config);
    quick_config.partialDiffOK = true; // for fastest check in SameDeep

    for (typename std::vector< boost::shared_ptr<object_type> >::const_iterator it=a.begin(); it!=a.end(); ++it)
        if (std::find_if(b.begin(), b.end(), SameDeep<object_type, config_type>(**it, quick_config)) == b.end())
            a_b.push_back(*it);

    for (typename std::vector< boost::shared_ptr<object_type> >::const_iterator it=b.begin(); it!=b.end(); ++it)
        if (std::find_if(a.begin(), a.end(), SameDeep<object_type, config_type>(**it, quick_config)) == a.end())
            b_a.push_back(*it);
}


template <typename object_type, typename config_type>
void ptr_diff(const boost::shared_ptr<object_type>& a,
              const boost::shared_ptr<object_type>& b,
              boost::shared_ptr<object_type>& a_b,
              boost::shared_ptr<object_type>& b_a,
              const config_type& config)
{
    if (!a.get() && !b.get()) return;

    boost::shared_ptr<object_type> a_temp = a.get() ? a : boost::shared_ptr<object_type>(new object_type);
    boost::shared_ptr<object_type> b_temp = b.get() ? b : boost::shared_ptr<object_type>(new object_type);

    if (!a_b.get()) a_b = boost::shared_ptr<object_type>(new object_type);
    if (!b_a.get()) b_a = boost::shared_ptr<object_type>(new object_type);
    diff(*a_temp, *b_temp, *a_b, *b_a, config);

    if (a_b->empty()) a_b = boost::shared_ptr<object_type>();
    if (b_a->empty()) b_a = boost::shared_ptr<object_type>();
}


} // namespace diff_impl
} // namespace data
} // namespace pwiz


#endif // _DIFF_STD_HPP_
