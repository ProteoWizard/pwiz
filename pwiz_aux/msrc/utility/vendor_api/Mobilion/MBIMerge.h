/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * MBI Data Access API                                             *
 * Copyright 2022 MOBILion Systems, Inc. ALL RIGHTS RESERVED       *
 * Author: Curt Lindmark                                           *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
 /// MBIMerge.h file
/// Header file for MBIMerge classes
/// MBIMerge contains functions trim an existing MBI file and save changes to
/// a new file.
 
// MBIMerge.h - The top-level API object interface to MBI trim operations.
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


namespace MBISDK
{
	/*! @class MBISDK::MBIMerge
	*	@brief Top level object for interfacing to the MBI data API.
	*	@author Curt Lindmark */
extern "C"
{
	class MBIMerge
	{
		std::map<int, std::string> MBIMerge_error_codes =
		{
			{ ERR_UNEXPECTED, "Unexpected error."},
			{ ERR_SUCCESS, "Success"},
			{ ERR_FILE_NOT_FOUND, "File not found."},
			{ ERR_HDF5_FILE_ERROR, "HDF5 file error."},
			{ ERR_METADATA_NOT_LOADED, "Metadata not loaded yet."},
			{ ERR_BAD_FRAME_INDEX, "Frame index out of bounds."},
			{ ERR_BAD_SCAN_INDEX, "Scan index out of bounds."},
			{ ERR_ITEM_MISSING, "Item missing."},
			{ ERR_OPERATION_NOT_SUPPORTED, "Operation is not supported."},
			{ ERR_INVALID_FRAME_SELECTION, "The frames selected are not valid."},
			{ ERR_INPUT_FILE_LOW_PATH_FAIL, "The low energy input path was not found."},
			{ ERR_INPUT_FILE_HIGH_PATH_FAIL, "The high energy input path was not found."},
			{ ERR_OUTPUT_FILE_PATH_FAIL, "The output path was not found."},
			{ ERR_INPUT_FILE_LOW_OPEN_FAIL, "The low energy input file could not be opened"},
			{ ERR_INPUT_FILE_HIGH_OPEN_FAIL, "The high energy input file could not be opened"},
			{ ERR_OUTPUT_FILE_CREATE_FAIL, "The output file could not be created"},
			{ERR_INPUT_LOW_FRAME_COUNT_MUST_MATCH_INPUT_HIGH_FRAME_COUNT, "The frame count of the low energy and high energy file must match."},
		};

	public:
		static const int ERR_UNEXPECTED = -1;
		static const int ERR_SUCCESS = 0;
		static const int ERR_FILE_NOT_FOUND = 1;
		static const int ERR_HDF5_FILE_ERROR = 2;
		static const int ERR_FILE_NOT_INITIALIZED = 3;
		static const int ERR_INVALID_FRAME_SELECTION = 4;
		static const int ERR_INPUT_FILE_LOW_PATH_FAIL = 5;
		static const int ERR_INPUT_FILE_HIGH_PATH_FAIL = 6;
		static const int ERR_OUTPUT_FILE_PATH_FAIL = 7;
		static const int ERR_INPUT_FILE_LOW_OPEN_FAIL = 8;
		static const int ERR_INPUT_FILE_HIGH_OPEN_FAIL = 9;
		static const int ERR_OUTPUT_FILE_CREATE_FAIL = 10;
		static const int ERR_INPUT_LOW_FRAME_COUNT_MUST_MATCH_INPUT_HIGH_FRAME_COUNT = 11;

		static const int ERR_METADATA_NOT_LOADED = 101;

		static const int ERR_BAD_FRAME_INDEX = 201;
		static const int ERR_BAD_SCAN_INDEX = 202;

		static const int ERR_ITEM_MISSING = 301;

		static const int ERR_OPERATION_NOT_SUPPORTED = 401;

		/// <summary>
		/// Construct an MBIMerge for read from a file.
		/// </summary>
		MBI_DLLCPP MBIMerge();

	private:
		void setErrorCode(int local_error_code);

		MBIFile* first_input_file_ptr;
		MBIFile* second_input_file_ptr;
		MBIFile* output_file_ptr;
		int error_code;
		std::string first_input_file_path_str;
		std::string second_input_file_path_str;
		std::string output_file_path_str;
		std::string oob_error_message;
		int frame_count;
		double optional_low_energy;
		double optional_high_energy;
	};
}
}
