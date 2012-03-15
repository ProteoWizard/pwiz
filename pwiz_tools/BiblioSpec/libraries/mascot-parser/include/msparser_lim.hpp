/*
##############################################################################
# file: msparser_lim.hpp                                                     #
# 'msparser' toolkit                                                         #
# Contains definitions used in all projects                                  #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/msparser_lim.hpp           $ #
#     $Author: davidc $ #
#       $Date: 2008-03-18 17:15:03 $ #
#   $Revision: 1.5 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MSPARSER_LIM_HPP
#define MSPARSER_LIM_HPP

/* Define some other values depending on the compiler / platform */
#ifdef _WIN32
#ifdef __GNUC__
typedef long long INT64;
typedef unsigned long long UINT64;
#define FORMAT_STRING_FOR_I64 "%lld" // checked with man pages
#define FORMAT_STRING_FOR_UI64 "%llu"
#else
typedef __int64          INT64;
typedef unsigned __int64 UINT64;
#define FORMAT_STRING_FOR_I64 "%I64d" // checked with MSDN
#define FORMAT_STRING_FOR_UI64 "%I64u"
#endif
#endif

// IRIX - only Origin 200 supported now. Used to support R4000
// On the SGI with O3 optimisation, it is better to let it choose what to inline */
// on second attempt, this was OK - just don't go too deep See bug 144
#ifdef __IRIX__
typedef long long INT64;
typedef unsigned long long UINT64;
#define FORMAT_STRING_FOR_I64 "%lld" // checked with man pages
#define FORMAT_STRING_FOR_UI64 "%llu"
#endif

#ifdef __ALPHA_UNIX__
typedef long INT64;
typedef unsigned long UINT64;
#define FORMAT_STRING_FOR_I64 "%ld"
#define FORMAT_STRING_FOR_UI64 "%lu"
#endif

//AIX Only support 64 bit now
#ifdef __AIX__
typedef long long INT64;
typedef unsigned long long  UINT64;
#define FORMAT_STRING_FOR_I64 "%lld"
#define FORMAT_STRING_FOR_UI64 "%llu"
#endif

#ifdef __SUNOS__
typedef long long INT64;
typedef unsigned long long UINT64;
#define FORMAT_STRING_FOR_I64 "%lld" // checked with man pages
#define FORMAT_STRING_FOR_UI64 "%llu"
#endif

#ifdef __LINUX__
typedef long long INT64;
typedef unsigned long long UINT64;
#define FORMAT_STRING_FOR_I64 "%lld" // checked with man pages
#define FORMAT_STRING_FOR_UI64 "%llu"
#endif

#ifdef __LINUX64__
typedef long INT64;
typedef unsigned long UINT64;
#define FORMAT_STRING_FOR_I64 "%ld" // checked with man pages
#define FORMAT_STRING_FOR_UI64 "%lu"
#endif

#ifdef __ITANIUM_LINUX__
typedef long long INT64;
typedef unsigned long long UINT64;
#define FORMAT_STRING_FOR_I64 "%ld"
#define FORMAT_STRING_FOR_UI64 "%lu"
#endif

#ifdef __HPUX__
typedef long long INT64;
typedef unsigned long long UINT64;
#define FORMAT_STRING_FOR_I64 "%ld" // checked with man pages
#define FORMAT_STRING_FOR_UI64 "%lu"
#endif

#ifdef __FREEBSD__
typedef long long INT64;
typedef unsigned long long UINT64;
#define FORMAT_STRING_FOR_I64 "%lld" // checked in man pages
#define FORMAT_STRING_FOR_UI64 "%llu"
#endif

#endif // MSPARSER_LIM_HPP
