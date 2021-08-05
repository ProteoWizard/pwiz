//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2021
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

#include "String.hpp"
#include <limits>
#include <algorithm>
#include <boost/spirit/include/karma.hpp>

using boost::spirit::karma::real_policies;
using boost::spirit::karma::real_generator;
using boost::spirit::karma::int_generator;
using boost::spirit::karma::generate;

template <typename T>
struct double12_policy : real_policies<T>
{
    //  we want to generate up to 12 fractional digits
    static unsigned int precision(T) { return 12; }
};

template <typename T>
struct float5_policy : real_policies<T>
{
    //  we want to generate up to 5 fractional digits
    static unsigned int precision(T) { return 5; }
};

template <typename T>
struct double12_policy_fixed : real_policies<T>
{
    //  we want to generate up to 12 fractional digits
    static unsigned int precision(T) { return 12; }

    //  we want the numbers always to be in fixed format
    static int floatfield(T) { return boost::spirit::karma::real_policies<T>::fmtflags::fixed; }
};

template <typename T>
struct float5_policy_fixed : real_policies<T>
{
    //  we want to generate up to 5 fractional digits
    static unsigned int precision(T) { return 5; }

    //  we want the numbers always to be in fixed format
    static int floatfield(T) { return boost::spirit::karma::real_policies<T>::fmtflags::fixed; }
};

template <typename T>
struct double12_policy_scientific : real_policies<T>
{
    //  we want to generate up to 12 fractional digits
    static unsigned int precision(T) { return 12; }

    //  we want the numbers always to be in scientific format
    static int floatfield(T) { return boost::spirit::karma::real_policies<T>::fmtflags::scientific; }
};

template <typename T>
struct float5_policy_scientific : real_policies<T>
{
    //  we want to generate up to 5 fractional digits
    static unsigned int precision(T) { return 5; }

    //  we want the numbers always to be in scientific format
    static int floatfield(T) { return boost::spirit::karma::real_policies<T>::fmtflags::scientific; }
};


template<typename PolicyT> std::string generateWithPolicy(typename PolicyT::value_type value)
{
    static const real_generator<typename PolicyT::value_type, PolicyT> policy = PolicyT();
    char buffer[256];
    char* p = buffer;
    generate(p, policy, value);
    return std::string(&buffer[0], p);

}

std::string pwiz::util::toString(double value, RealConvertPolicy policyFlags)
{
    // HACK: karma has a stack overflow on subnormal values, so we clamp to normalized values
    if (value > 0)
        value = std::max(std::numeric_limits<double>::min(), value);
    else if (value < 0)
        value = std::min(-std::numeric_limits<double>::min(), value);

    switch (policyFlags)
    {
        case RealConvertPolicy::AutoNotation: return generateWithPolicy<double12_policy<double>>(value);
        case RealConvertPolicy::FixedNotation: return generateWithPolicy<double12_policy_fixed<double>>(value);
        case RealConvertPolicy::ScientificNotation: return generateWithPolicy<double12_policy_scientific<double>>(value);
        default: throw std::runtime_error("[toString] unknown RealConvertPolicy");
    }
}

std::string pwiz::util::toString(float value, RealConvertPolicy policyFlags)
{
    // HACK: karma has a stack overflow on subnormal values, so we clamp to normalized values
    if (value > 0)
        value = std::max(std::numeric_limits<float>::min(), value);
    else if (value < 0)
        value = std::min(-std::numeric_limits<float>::min(), value);

    switch (policyFlags)
    {
        case RealConvertPolicy::AutoNotation: return generateWithPolicy<float5_policy<float>>(value);
        case RealConvertPolicy::FixedNotation: return generateWithPolicy<float5_policy_fixed<float>>(value);
        case RealConvertPolicy::ScientificNotation: return generateWithPolicy<float5_policy_scientific<float>>(value);
        default: throw std::runtime_error("[toString] unknown RealConvertPolicy");
    }
}

std::string pwiz::util::toString(int value)
{
    static const int_generator<int> intgen = int_generator<int>();
    char buffer[256];
    char* p = buffer;
    generate(p, intgen, value);
    return std::string(&buffer[0], p);
}