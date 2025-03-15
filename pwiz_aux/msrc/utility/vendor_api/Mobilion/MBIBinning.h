/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * MBI Data Access API                                             *
 * Copyright 2022 MOBILion Systems, Inc. ALL RIGHTS RESERVED       *
 * Author: Curt Lindmark                                           *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
 /// MBIBinning.h file
/// Header file for MBIBinning classes
/// MBIBinning contains functions trim an existing MBI file and save changes to
/// a new file.
 
// MBIBinning.h - The top-level API object interface to MBI binning operations.
#pragma once
#pragma warning(disable : 4251)
#pragma warning(disable : 4996)
#ifndef MBI_DLLCPP
#ifdef MBI_EXPORTS
#define MBI_DLLCPP __declspec(dllexport)
#else
#define MBI_DLLCPP __declspec(dllimport)
#endif
#endif
