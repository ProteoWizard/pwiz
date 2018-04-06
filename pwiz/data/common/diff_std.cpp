//
// $Id: diff_std.hpp 1656 2009-12-30 20:54:17Z chambm $
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

#define PWIZ_SOURCE


#include "diff_std.hpp"
#include "pwiz/utility/misc/String.hpp"


namespace pwiz {
namespace data {
namespace diff_impl {


PWIZ_API_DECL
void diff(const std::string& a,
          const std::string& b,
          std::string& a_b,
          std::string& b_a,
          const BaseDiffConfig& config)
{
    diff_string(a, b, a_b, b_a);
}


// special string matching for ids, which
// tend to have versions embedded at the end
PWIZ_API_DECL
void diff_ids(const std::string& a,
              const std::string& b,
              std::string& a_b,
              std::string& b_a,
              const BaseDiffConfig& config)
{
    if (config.ignoreVersions && (a != b))
    {
        // look for x.x.x at the of each string
        int aa,bb;
        int ndotsa=0;
        int ndotsb=0;
        for (aa=a.length();aa--;)
        {
            if (a[aa]=='.')
                ndotsa++;
            else if (!isdigit(a[aa]))
                break;
        }
        for (bb=b.length();bb--;)
        {
            if (b[bb]=='.')
                ndotsb++;
            else if (!isdigit(b[bb]))
                break;
        }
        if ((2==ndotsa) && (2==ndotsb) && 
            (bb > 0) && (aa > 0) &&
            (a.substr(0,aa) == b.substr(0,bb)))
            return;
    }
    diff_string(a, b, a_b, b_a);
}

PWIZ_API_DECL
void diff(const boost::logic::tribool& a, 
          const boost::logic::tribool& b, 
          boost::logic::tribool& a_b, 
          boost::logic::tribool& b_a,
          const BaseDiffConfig& config)
{
    if (a != b)
    {
        a_b = a;
        b_a = b;
    }
    else
    {
        a_b = boost::logic::indeterminate;
        b_a = boost::logic::indeterminate;
    }    

}


PWIZ_API_DECL
void diff(const CV& a, 
          const CV& b, 
          CV& a_b, 
          CV& b_a,
          const BaseDiffConfig& config)
{
    diff_string(a.URI, b.URI, a_b.URI, b_a.URI);
    diff_string(a.id, b.id, a_b.id, b_a.id);
    diff_string(a.fullName, b.fullName, a_b.fullName, b_a.fullName);
	if (!config.ignoreVersions)
        diff_string(a.version, b.version, a_b.version, b_a.version);
}


PWIZ_API_DECL
void diff(CVID a,
          CVID b,
          CVID& a_b,
          CVID& b_a,
          const BaseDiffConfig& config)
{
    if (a!=b)  
    {
        a_b = a;
        b_a = b;
    }
    else
    {
        a_b = b_a = CVID_Unknown;
    }
}


PWIZ_API_DECL
void diff(const CVParam& a, 
          const CVParam& b, 
          CVParam& a_b, 
          CVParam& b_a,
          const BaseDiffConfig& config)
{
    diff(a.cvid, b.cvid, a_b.cvid, b_a.cvid, config);

    // start with a cheap string compare
    if (a.value==b.value)
    {
        a_b.value.clear();
        b_a.value.clear();
    } 
    else
    {
        bool asString = false;
        // lexical_cast<int> is happy to read "1.1" as "1" - and "1.9" the same way
        if ((std::string::npos == a.value.find_first_of(".eE")) &&
            (std::string::npos == b.value.find_first_of(".eE")))  // any float-like chars?
        {   
            try
            {
                // compare as ints if possible
                int ia = lexical_cast<int>(a.value);
                int ib = lexical_cast<int>(b.value);
                if (ia != ib) 
                {
                    a_b.value = lexical_cast<string>(ia);
                    b_a.value = lexical_cast<string>(ib);
                }
                else // watch for something like "1a" vs "1b"
                {
                    if ((std::string::npos == a.value.find_first_not_of("0123456789")) &&
                        (std::string::npos == b.value.find_first_not_of("0123456789")))
                    { 
                        a_b.value.clear();
                        b_a.value.clear();
                    }
                    else
                    {
                        asString = true;
                    }
                }
            }
            catch (boost::bad_lexical_cast&)
            {
                asString = true;
            }
        }
        else
        {
            // use precision to compare floating point values
            try
            {
                double aValue = lexical_cast<double>(a.value);
                double bValue = lexical_cast<double>(b.value);
                double a_bValue, b_aValue;
                diff_floating<double>(aValue, bValue, a_bValue, b_aValue, config);
                if (a_bValue || b_aValue)
                {
                    a_b.value = lexical_cast<string>(a_bValue);
                    b_a.value = lexical_cast<string>(b_aValue);
                }
                else
                {
                    a_b.value.clear();
                    b_a.value.clear();
                }
            }
            catch (boost::bad_lexical_cast&)
            {
                asString = true;
            }
        }
        if (asString)
        {
             diff_string(a.value, b.value, a_b.value, b_a.value);
        }
    }
    diff(a.units, b.units, a_b.units, b_a.units, config);

    // provide names for context
    if (!a_b.empty() && a_b.cvid==CVID_Unknown) a_b.cvid = a.cvid; 
    if (!b_a.empty() && b_a.cvid==CVID_Unknown) b_a.cvid = b.cvid; 
}


PWIZ_API_DECL
void diff(const UserParam& a, 
          const UserParam& b, 
          UserParam& a_b, 
          UserParam& b_a,
          const BaseDiffConfig& config)
{
    diff_string(a.name, b.name, a_b.name, b_a.name);
    diff_string(a.value, b.value, a_b.value, b_a.value);
    diff_string(a.type, b.type, a_b.type, b_a.type);
    diff(a.units, b.units, a_b.units, b_a.units, config);

    // provide names for context
    if (!a_b.empty() && a_b.name.empty()) a_b.name = a.name; 
    if (!b_a.empty() && b_a.name.empty()) b_a.name = b.name; 
}


PWIZ_API_DECL
void diff(const ParamContainer& a, 
          const ParamContainer& b, 
          ParamContainer& a_b, 
          ParamContainer& b_a,
          const BaseDiffConfig& config)
{
    vector_diff_deep(a.paramGroupPtrs, b.paramGroupPtrs, a_b.paramGroupPtrs, b_a.paramGroupPtrs, config);
    vector_diff_diff(a.cvParams, b.cvParams, a_b.cvParams, b_a.cvParams, config);
    vector_diff_diff(a.userParams, b.userParams, a_b.userParams, b_a.userParams, config);
}


PWIZ_API_DECL
void diff(const ParamGroup& a, 
          const ParamGroup& b, 
          ParamGroup& a_b, 
          ParamGroup& b_a,
          const BaseDiffConfig& config)
{
    diff(static_cast<const ParamContainer&>(a), b, a_b, b_a, config);
    diff_ids(a.id, b.id, a_b.id, b_a.id, config);

    // provide id for context
    if (!a_b.empty() || !b_a.empty()) 
    {
        a_b.id = a.id; 
        b_a.id = b.id; 
    }
}


} // namespace diff_impl
} // namespace data
} // namespace pwiz
