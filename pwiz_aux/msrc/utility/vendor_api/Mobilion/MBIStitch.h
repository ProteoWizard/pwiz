/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * MBI Data Access API                                             *
 * Copyright 2022 MOBILion Systems, Inc. ALL RIGHTS RESERVED       *
 * Author: Curt Lindmark                                           *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
 /// MBIStitch.h file
/// Header file for MBIStitch classes
/// MBIStitch contains functions trim an existing MBI file and save changes to
/// a new file.
 
// MBIStitch.h - The top-level API object interface to MBI trim operations.
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

#include "MBIFile.h"
#include "MBIMetadata.h"
#include "MBICalibration.h"
#include "MBIFrame.h"

#include <cstdio>
#include <fstream>

