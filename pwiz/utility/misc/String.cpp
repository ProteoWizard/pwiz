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
#include <iomanip>
#include <locale>
#include <sstream>
#include <boost/spirit/include/karma.hpp>

using boost::spirit::karma::real_policies;
using boost::spirit::karma::real_generator;
using boost::spirit::karma::int_generator;
using boost::spirit::karma::uint_generator;
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


namespace {

/// True when text reloads to exactly value, read independently of the global
/// locale. karma generates locale-independently, so the round-trip check has to
/// read that way too; strtod would follow a comma-decimal-point locale and
/// reject text karma had just written with a period.
///
/// The failbit test is NOT redundant with the equality test. C++11 requires an
/// extraction that goes out of range to set failbit AND store the largest
/// representable value, so a decimal just above DBL_MAX such as
/// 1.79769313486232e+308 reads back as exactly DBL_MAX and compares equal
/// without being the same number. A consumer reading that text with strtod
/// gets inf instead.
bool reloadsExactly(const std::string& text, double value)
{
    std::istringstream iss(text);
    iss.imbue(std::locale::classic());
    double reloaded = 0;
    iss >> reloaded;
    return !iss.fail() && reloaded == value;
}

/// The shortest decimal form of value that reloads bit-exact, found by trying
/// 15, 16 and 17 significant digits in turn (17 = max_digits10, which always
/// round-trips a double). Default float formatting is %g semantics, i.e.
/// SIGNIFICANT digits, which is what round-tripping needs at every magnitude;
/// karma's precision() counts FRACTIONAL digits and so cannot express this.
///
/// Deliberately not std::to_chars: floating-point to_chars is C++17 but its
/// library support is not universal across the toolsets pwiz builds with
/// (libstdc++ needs GCC 11, libc++ a recent LLVM), and this file is compiled
/// everywhere. Deliberately not snprintf either: that follows the global
/// locale, and would emit a comma decimal point into XML under e.g. de_DE.
std::string toRoundTripString(double value)
{
    std::string widest;
    for (int significantDigits = 15; significantDigits <= 17; ++significantDigits)
    {
        std::ostringstream oss;
        oss.imbue(std::locale::classic());
        oss << std::setprecision(significantDigits) << value;
        widest = oss.str();
        if (reloadsExactly(widest, value))
            return widest;
    }
    return widest; // 17 significant digits always round-trips a double
}

} // namespace


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
        case RealConvertPolicy::AutoNotation:
        {
            // 12 fractional digits is enough for almost every value pwiz writes,
            // and it is what keeps output free of lexical_cast noise like
            // 123.00000000007. But it is a SILENT truncation when a value needs
            // more: a Thermo scan start time of 1.8119944333330003 minutes was
            // written as 1.811994433333 and reloaded one ULP away, so an mzML
            // could not reproduce the value read from the raw file.
            //
            // Keep the historical text whenever it already reloads bit-exact
            // (the overwhelmingly common case, so existing output and file sizes
            // are unchanged), and fall back to the shortest round-tripping form
            // only where the fast path would lose information.
            std::string result = generateWithPolicy<double12_policy<double>>(value);
            if (reloadsExactly(result, value))
                return result;
            return toRoundTripString(value);
        }
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

std::string pwiz::util::toString(size_t value)
{
    static const uint_generator<size_t> intgen = uint_generator<size_t>();
    char buffer[256];
    char* p = buffer;
    generate(p, intgen, value);
    return std::string(&buffer[0], p);
}