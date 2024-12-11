/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * MBI Data Access API                                             *
 * Copyright 2022 MOBILion Systems, Inc. ALL RIGHTS RESERVED       *
 * Author: Curt Lindmark                                           *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
 /// MBITrim.h file
/// Header file for MBITrim classes
/// MBITrim contains functions trim an existing MBI file and save changes to
/// a new file.

// MBITrim.h - The top-level API object interface to MBI trim operations.
#pragma once
#pragma warning(disable : 4996)
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
