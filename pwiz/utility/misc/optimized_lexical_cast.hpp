//
// $Id$
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

#ifndef _OPTIMIZED_LEXICAL_CAST_HPP_
#define _OPTIMIZED_LEXICAL_CAST_HPP_

#include <cstdlib>
#include <cerrno>
#include <boost/lexical_cast.hpp>
#include <boost/logic/tribool.hpp>


// HACK: Darwin strtod isn't threadsafe so strtod_l must be used
#ifdef __APPLE__
#include <xlocale.h>
#include "pwiz/utility/misc/Singleton.hpp"

namespace {

class ThreadSafeCLocale : public boost::singleton<ThreadSafeCLocale>
{
    public:
    ThreadSafeCLocale(boost::restricted) : c_locale(::newlocale(LC_ALL_MASK, "C", 0)) {}
    ~ThreadSafeCLocale() {::freelocale(c_locale);}
    ::locale_t c_locale;
};

} // namespace
#define STRTOD(x, y) strtod_l((x), (y), ThreadSafeCLocale::instance->c_locale)
#define ATOF(x) STRTOD(x,NULL)

#else // __APPLE__
#define STRTOD(x, y) strtod((x), (y))
#define ATOF(x) atof(x)
#endif // __APPLE__

#define OPTIMIZED_LEXICAL_CAST(toType) \
    template<> inline toType lexical_cast(const std::string& str) { \
        bool success; \
        toType value = lexical_cast<toType>(str, success); \
        if (!success) \
            throw bad_lexical_cast(); \
        return value; \
    }

// optimized string->numeric conversions
namespace boost
{
    template <typename toType>
    inline toType lexical_cast(const std::string& str, bool& success)
    {
        // error: new overload needed below
        throw std::logic_error("BUG: new overload needed");
    }

    template <>
    inline float lexical_cast( const std::string& str, bool& success )
    {
        errno = 0;
        success = true;
        const char* stringToConvert = str.c_str();
        const char* endOfConversion = stringToConvert;
        float value = (float) STRTOD( stringToConvert, const_cast<char**>(&endOfConversion) );
        if( value == 0.0f && stringToConvert == endOfConversion ) // error: conversion could not be performed
            success = false;
        return value;
    }

    OPTIMIZED_LEXICAL_CAST(float)

    template <>
    inline double lexical_cast( const std::string& str, bool& success )
    {
        errno = 0;
        success = true;
        const char* stringToConvert = str.c_str();
        const char* endOfConversion = stringToConvert;
        double value = STRTOD( stringToConvert, const_cast<char**>(&endOfConversion) );
        if( value == 0.0 && stringToConvert == endOfConversion ) // error: conversion could not be performed
            success = false;
        return value;
    }

    OPTIMIZED_LEXICAL_CAST(double)

    template <>
    inline int lexical_cast( const std::string& str, bool& success )
    {
        errno = 0;
        success = true;
        const char* stringToConvert = str.c_str();
        const char* endOfConversion = stringToConvert;
        int value = (int) strtol( stringToConvert, const_cast<char**>(&endOfConversion), 10 );
        if( ( value == 0 && stringToConvert == endOfConversion ) || // error: conversion could not be performed
            errno != 0 ) // error: overflow or underflow
            success = false;
        return value;
    }

    OPTIMIZED_LEXICAL_CAST(int)

    template <>
    inline long lexical_cast( const std::string& str, bool& success )
    {
        errno = 0;
        success = true;
        const char* stringToConvert = str.c_str();
        const char* endOfConversion = stringToConvert;
        long value = strtol( stringToConvert, const_cast<char**>(&endOfConversion), 10 );
        if( ( value == 0l && stringToConvert == endOfConversion ) || // error: conversion could not be performed
            errno != 0 ) // error: overflow or underflow
            success = false;
        return value;
    }

    OPTIMIZED_LEXICAL_CAST(long)

    template <>
    inline unsigned int lexical_cast( const std::string& str, bool& success )
    {
        errno = 0;
        success = true;
        const char* stringToConvert = str.c_str();
        const char* endOfConversion = stringToConvert;
        unsigned int value = (unsigned int) strtoul( stringToConvert, const_cast<char**>(&endOfConversion), 10 );
        if( ( value == 0u && stringToConvert == endOfConversion ) || // error: conversion could not be performed
            errno != 0 ) // error: overflow or underflow
            success = false;
        return value;
    }

    OPTIMIZED_LEXICAL_CAST(unsigned int)

    template <>
    inline unsigned long lexical_cast( const std::string& str, bool& success )
    {
        errno = 0;
        success = true;
        const char* stringToConvert = str.c_str();
        const char* endOfConversion = stringToConvert;
        unsigned long value = strtoul( stringToConvert, const_cast<char**>(&endOfConversion), 10 );
        if( ( value == 0ul && stringToConvert == endOfConversion ) || // error: conversion could not be performed
            errno != 0 ) // error: overflow or underflow
            success = false;
        return value;
    }

    OPTIMIZED_LEXICAL_CAST(unsigned long)

    template <>
    inline long long lexical_cast( const std::string& str, bool& success )
    {
        errno = 0;
        success = true;
        const char* stringToConvert = str.c_str();
        const char* endOfConversion = stringToConvert;
        long long value = strtoll( stringToConvert, const_cast<char**>(&endOfConversion), 10 );
        if ((value == 0ll && stringToConvert == endOfConversion) || // error: conversion could not be performed
            errno != 0 ) // error: overflow or underflow
            success = false;
        return value;
    }

    OPTIMIZED_LEXICAL_CAST(long long)

    template <>
    inline unsigned long long lexical_cast( const std::string& str, bool& success )
    {
        errno = 0;
        success = true;
        const char* stringToConvert = str.c_str();
        const char* endOfConversion = stringToConvert;
        unsigned long long value = strtoull( stringToConvert, const_cast<char**>(&endOfConversion), 10 );
        if( ( value == 0ull && stringToConvert == endOfConversion ) || // error: conversion could not be performed
            errno != 0 ) // error: overflow or underflow
            success = false;
        return value;
    }

    OPTIMIZED_LEXICAL_CAST(unsigned long long)

    template<>
    inline bool lexical_cast( const std::string& str )
    {
        if (str == "0" || str == "false")
            return false;
        return true;
    }

    template<>
    inline boost::logic::tribool lexical_cast( const std::string& str )
    {
        using namespace boost::logic;
        if (str.empty())
            return tribool(indeterminate);
        if (str == "0" || str == "false")
            return false;
        return true;
    }

    /*template<>
    inline float lexical_cast( const char*& str )
    {
        errno = 0;
        const char* endOfConversion = str;
        float value = (float) STRTOD( str, const_cast<char**>(&endOfConversion) );
        if( ( value == 0.0f && str == endOfConversion ) || // error: conversion could not be performed
            errno != 0 ) // error: overflow or underflow
            throw bad_lexical_cast();//throw bad_lexical_cast( std::type_info( str ), std::type_info( value ) );
        return value;
    }

    template<>
    inline double lexical_cast( const char*& str )
    {
        errno = 0;
        const char* endOfConversion = str;
        double value = STRTOD( str, const_cast<char**>(&endOfConversion) );
        if( ( value == 0.0 && str == endOfConversion ) || // error: conversion could not be performed
            errno != 0 ) // error: overflow or underflow
            throw bad_lexical_cast();//throw bad_lexical_cast( std::type_info( str ), std::type_info( value ) );
        return value;
    }

    template<>
    inline int lexical_cast( const char*& str )
    {
        errno = 0;
        const char* endOfConversion = str;
        int value = (int) strtol( str, const_cast<char**>(&endOfConversion), 0 );
        if( ( value == 0 && str == endOfConversion ) || // error: conversion could not be performed
            errno != 0 ) // error: overflow or underflow
            throw bad_lexical_cast();//throw bad_lexical_cast( std::type_info( str ), std::type_info( value ) );
        return value;
    }

    template<>
    inline long lexical_cast( const char*& str )
    {
        errno = 0;
        const char* endOfConversion = str;
        long value = strtol( str, const_cast<char**>(&endOfConversion), 0 );
        if( ( value == 0l && str == endOfConversion ) || // error: conversion could not be performed
            errno != 0 ) // error: overflow or underflow
            throw bad_lexical_cast();//throw bad_lexical_cast( std::type_info( str ), std::type_info( value ) );
        return value;
    }

    template<>
    inline unsigned int lexical_cast( const char*& str )
    {
        errno = 0;
        const char* endOfConversion = str;
        unsigned int value = (unsigned int) strtoul( str, const_cast<char**>(&endOfConversion), 0 );
        if( ( value == 0u && str == endOfConversion ) || // error: conversion could not be performed
            errno != 0 ) // error: overflow or underflow
            throw bad_lexical_cast();//throw bad_lexical_cast( std::type_info( str ), std::type_info( value ) );
        return value;
    }

    template<>
    inline unsigned long lexical_cast( const char*& str )
    {
        errno = 0;
        const char* endOfConversion = str;
        unsigned long value = strtoul( str, const_cast<char**>(&endOfConversion), 0 );
        if( ( value == 0ul && stringToConvert == endOfConversion ) || // error: conversion could not be performed
            errno != 0 ) // error: overflow or underflow
            throw bad_lexical_cast();//throw bad_lexical_cast( std::type_info( str ), std::type_info( value ) );
        return value;
    }
    */
} // boost

#endif // _OPTIMIZED_LEXICAL_CAST_HPP_
