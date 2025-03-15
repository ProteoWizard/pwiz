// MBIException.h - SDK top-level exceptions
/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * MBI Data Access API                                             *
 * Copyright 2024 MOBILion Systems, Inc. ALL RIGHTS RESERVED       *
 * Author: Bennett Kalafut                                         *
 * 0.0.0.0
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
#pragma once

#ifndef MBI_DLLCPP
#ifdef SWIG_WIN
#define MBI_DLLCPP
#else
#ifdef MBI_EXPORTS
#define MBI_DLLCPP __declspec(dllexport)
#else
#define MBI_DLLCPP __declspec(dllimport)
#endif
#endif
#endif

#include <exception>

namespace MBISDK
{
namespace Exceptions
{    
    class MBISDKUnknownCaseException : public std::runtime_error
    {
        using std::runtime_error::runtime_error;
    };

    class MBISDKEmptyFile : public std::runtime_error
    {
        using std::runtime_error::runtime_error;
    };
}
}
