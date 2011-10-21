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
// Additional performance work by Brian Pratt Insilicos LLC
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
#include <string.h>


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


// optimized string->numeric conversions
//
// two versions of each:
// std::string conversion, has pedantic error checking w/ thrown exceptions
// const char *conversion, has minimal error checking for best speed
//
// note: if your code depends on thrown exceptions to decide if something
// is an integer or not (for example) you'll need to use the pedantic
// formulation, but be aware that try/catch is actually pretty expensive
// in a lot of implementations (due to setjmp/longjmp) and might not be the
// best design after all.
//
namespace boost
{
	template<>
	inline float lexical_cast( const std::string& str )
    {
		errno = 0;
		const char* stringToConvert = str.c_str();
		const char* endOfConversion = stringToConvert;
		float value = (float) STRTOD( stringToConvert, const_cast<char**>(&endOfConversion) );
		if( ( value == 0.0f && stringToConvert == endOfConversion ) || // error: conversion could not be performed
			errno != 0 ) // error: overflow or underflow
			throw bad_lexical_cast();//throw bad_lexical_cast( std::type_info( str ), std::type_info( value ) );
		return value;
	}

	template<>
	inline double lexical_cast( const std::string& str )
	{
		errno = 0;
		const char* stringToConvert = str.c_str();
		const char* endOfConversion = stringToConvert;
		double value = STRTOD( stringToConvert, const_cast<char**>(&endOfConversion) );
		if( ( value == 0.0 && stringToConvert == endOfConversion ) || // error: conversion could not be performed
			errno != 0 ) // error: overflow or underflow
			throw bad_lexical_cast();//throw bad_lexical_cast( std::type_info( str ), std::type_info( value ) );
		return value;
	}

	template<>
	inline int lexical_cast( const std::string& str )
	{
		errno = 0;
		const char* stringToConvert = str.c_str();
		const char* endOfConversion = stringToConvert;
		int value = (int) strtol( stringToConvert, const_cast<char**>(&endOfConversion), 0 );
		if( ( value == 0 && stringToConvert == endOfConversion ) || // error: conversion could not be performed
			errno != 0 ) // error: overflow or underflow
			throw bad_lexical_cast();//throw bad_lexical_cast( std::type_info( str ), std::type_info( value ) );
		return value;
	}

	template<>
	inline long lexical_cast( const std::string& str )
	{
		errno = 0;
		const char* stringToConvert = str.c_str();
		const char* endOfConversion = stringToConvert;
		long value = strtol( stringToConvert, const_cast<char**>(&endOfConversion), 0 );
		if( ( value == 0l && stringToConvert == endOfConversion ) || // error: conversion could not be performed
			errno != 0 ) // error: overflow or underflow
			throw bad_lexical_cast();//throw bad_lexical_cast( std::type_info( str ), std::type_info( value ) );
		return value;
	}

	template<>
	inline unsigned int lexical_cast( const std::string& str )
	{
		errno = 0;
		const char* stringToConvert = str.c_str();
		const char* endOfConversion = stringToConvert;
		unsigned int value = (unsigned int) strtoul( stringToConvert, const_cast<char**>(&endOfConversion), 0 );
		if( ( value == 0u && stringToConvert == endOfConversion ) || // error: conversion could not be performed
			errno != 0 ) // error: overflow or underflow
			throw bad_lexical_cast();//throw bad_lexical_cast( std::type_info( str ), std::type_info( value ) );
		return value;
	}

	template<>
	inline unsigned long lexical_cast( const std::string& str )
	{
		errno = 0;
		const char* stringToConvert = str.c_str();
		const char* endOfConversion = stringToConvert;
		unsigned long value = strtoul( stringToConvert, const_cast<char**>(&endOfConversion), 0 );
		if( ( value == 0ul && stringToConvert == endOfConversion ) || // error: conversion could not be performed
			errno != 0 ) // error: overflow or underflow
			throw bad_lexical_cast();//throw bad_lexical_cast( std::type_info( str ), std::type_info( value ) );
		return value;
	}

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

	inline void optimized_lexical_cast( const char* stringToConvert, float& value )
	{
		value = (float) ATOF( stringToConvert ) ;
	}

	inline void optimized_lexical_cast( const char* stringToConvert, double& value )
	{
		value = ATOF( stringToConvert );
	}

	inline void optimized_lexical_cast( const char* stringToConvert, int& value )
	{
        value = atoi(stringToConvert);
	}

	inline void optimized_lexical_cast( const char* stringToConvert, char& value )
	{
		value = *stringToConvert;
	}

	inline void optimized_lexical_cast( const char* stringToConvert, long& value )
	{
        value = atol(stringToConvert);
	}

	inline void optimized_lexical_cast( const char* stringToConvert, unsigned int& value )
	{
    value = (unsigned int) strtoul( stringToConvert,NULL, 10 );
	}

	inline void optimized_lexical_cast( const char* stringToConvert, unsigned long& value )
	{
    value = strtoul( stringToConvert,NULL, 10 );
	}

#ifndef _SIZE_T_DEFINED // some compilers just use a #define for size_t
    inline void optimized_lexical_cast( const char* stringToConvert, size_t& value )
	{
        value = (size_t)strtoul( stringToConvert,NULL, 10 );
	}
#endif

    inline void optimized_lexical_cast( const char* stringToConvert, bool& value )
    {
        value = (strcmp(stringToConvert, "0") && strcmp(stringToConvert,"false"));
    }

    inline void optimized_lexical_cast( const char* stringToConvert, boost::logic::tribool& value )
    {
        using namespace boost::logic;
        if (!*stringToConvert) {
            value = tribool(indeterminate);
        } else {
            bool b;
            optimized_lexical_cast(stringToConvert, b);
            value = b;
        }
    }

    inline void optimized_lexical_cast( const char* stringToConvert, std::string& value )
    {
        value = stringToConvert;
    }

} // boost

#endif // _OPTIMIZED_LEXICAL_CAST_HPP_
