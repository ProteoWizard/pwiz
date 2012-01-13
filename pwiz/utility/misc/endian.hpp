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


#ifndef _ENDIAN_HPP_
#define _ENDIAN_HPP_


#include "boost/static_assert.hpp"


namespace pwiz {
namespace util {


#if defined(__GLIBC__) || defined(__GLIBCXX__)
#define PWIZ_GCC
#endif


#if defined(_MSC_VER)
#define PWIZ_MSVC
#endif


#if (defined(PWIZ_GCC) && defined(__BYTE_ORDER) && __BYTE_ORDER==__LITTLE_ENDIAN) || \
    (defined(__DARWIN_BYTE_ORDER) && __DARWIN_BYTE_ORDER==__DARWIN_LITTLE_ENDIAN) || \
    (defined(__LITTLE_ENDIAN__)) || \
    (defined(__MINGW32__)) || \
    (defined(__i386__)) || \
    (defined(PWIZ_MSVC))
#define PWIZ_LITTLE_ENDIAN
#endif


#if (defined(PWIZ_GCC) && defined(__BYTE_ORDER) && __BYTE_ORDER==__BIG_ENDIAN)
#define PWIZ_BIG_ENDIAN
#endif


#if defined(PWIZ_LITTLE_ENDIAN) && defined(PWIZ_BIG_ENDIAN)
#error "This isn't happening."
#endif


#if !defined(PWIZ_LITTLE_ENDIAN) && !defined(PWIZ_BIG_ENDIAN)
#error "Unsupported platform: probably need a platform-specific define above."
#endif


BOOST_STATIC_ASSERT(sizeof(unsigned int) == 4); // 32 bits
BOOST_STATIC_ASSERT(sizeof(unsigned long long) == 8); // 64 bits


inline unsigned int endianize32(unsigned int n)
{
    return ((n&0xff)<<24) | ((n&0xff00)<<8) | ((n&0xff0000)>>8) | ((n&0xff000000)>>24);
}


inline unsigned long long endianize64(unsigned long long n)
{
    return ((n&0x00000000000000ffll)<<56) | 
           ((n&0x000000000000ff00ll)<<40) | 
           ((n&0x0000000000ff0000ll)<<24) | 
           ((n&0x00000000ff000000ll)<<8)  |
           ((n&0x000000ff00000000ll)>>8)  | 
           ((n&0x0000ff0000000000ll)>>24) |
           ((n&0x00ff000000000000ll)>>40) | 
           ((n&0xff00000000000000ll)>>56);
}


//
// notes:
//
// To dump gcc's defined macros:
//   gcc -dM -E source.cpp
//
// glibc defines __BYTE_ORDER in <endian.h>
//


} // namespace util
} // namespace pwiz 


#endif // _ENDIAN_HPP_


