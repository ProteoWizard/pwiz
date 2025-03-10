// MBIChem.h - Chemistry-related codes
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

#include <string>
#include <map>

namespace MBISDK
{

    namespace MBIChem
    {
    extern "C" 
    {
	
    /// @fn GasStringToMassMapping
    /// @brief A mapping of gas identity strings to masses
    /// @author B. Kalafut
    MBI_DLLCPP std::map<std::string, const double>& GasStringToMassMapping();
    
    }
    }

};
