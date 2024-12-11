// MBI Data Access API                                             *
// Copyright 2024 MOBILion Systems, Inc. ALL RIGHTS RESERVED  
// MBIFileHDF5Adapter.h - Top-level object initialized and gatheing point of HDF5 operations
// Revised by B. Kalafut, August 2004
// Curt Lindmark 9/1/2021

// 0.0.0.0
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

#include "MBIConstants.h"
#include "MBIMetadata.h"
#include "MBICalibration.h"
#include "MBIFrame.h"

#include <cstdio>
#include <fstream>
#include <memory>

// Do not include H5cpp.h here.
// All methods with signatures that directly reference HDF5 classes or structures should be in H5Bridge,
// which is declared as an opaque pointer here and implemented in the source file.

namespace MBISDK
{

	/// @class MBISDK::MBIFileHDF5Adapter
	/// @brief Top level object for interfacing to the MBI data API.
	/// @author Doug Bodden */
	class MBI_DLLCPP MBIFileHDF5Adapter
	{

	public:
		/// @brief HDF5 Adapter Unexpected error 
		static const int ERR_UNEXPECTED = -1;
		/// @brief HDF5 Adapter Success code 
		static const int ERR_SUCCESS = 0;
		/// @brief HDF5 Adapter Internal error 
		static const int ERR_HDF5_INTERNAL_ERROR = 1;
		/// @brief HDF5 Adapter name size constant 
		static const int NAME_SIZE = 50;
		/// @brief HDF5 Adapter Upper limit for metadata 
		static const int HDF5_METADATA_SIZE_UPPER_LIMIT = 32 * 1024 * 1024; //This is the upper limit of writing as restricted by HDF5.
											                                //It equates to 33,554,432 bytes.

		/// <summary>
		/// Construct an MBIFile for read from a file.
		/// </summary>
		/// <param name="path">Path to the file to load.</param>
		/// <param name="parent">File being opened.</param>
		MBIFileHDF5Adapter(std::shared_ptr<std::string> path, MBIFile *parent);

        // We need to prevent the insertion of a compiler-defined destructor where the opaque pointer to H5Bridge is incomplete
		// In the implementation file MBIFileHDF5Adapter.cpp, we generate the default destructor by
		// MBIFileHDF5Adapter::~MBIFileHDF5Adapter() = default;
		~MBIFileHDF5Adapter();

		/// <summary>
		/// Initialize an existing MBI file.
		/// </summary>
		bool Init();

		/// <summary>
		/// Close the input file and free any associated resources.
		/// </summary>
		void Close();

		/// <summary>
		/// Retrieve the name of the input file (full path).
		/// </summary>
		/// <returns>std::string the path used to open the file.</returns>
		std::shared_ptr<std::string> GetFilename();

		/// <summary>
		/// Retrieve a start time for a frame metadata
		/// </summary>
		/// <param name="pFrameMD">Pointer to frame metadata.</param>
		/// <returns>A double from frame metadata
		double GetFrameStartTime(std::shared_ptr<FrameMetadata> pFrameMD);

		/// <summary>
		/// Retrieve a start time for a frame metadata
		/// </summary>
		/// <param name="pFrameMD">Pointer to frame metadata.</param>
		/// <returns>A double from frame metadata
		std::string GetCalibration(std::shared_ptr<FrameMetadata> pFrameMD);

		/// <summary>
		/// Retrieve a list of intensities for a given frame
		/// </summary>
		/// <param name="frameIndex">Frame index.</param>
		/// <returns>A list of intensities for a given frame</returns>
		std::shared_ptr<std::vector<int32_t>> loadSparseSampleIntensities(int frameIndex);

		/// <summary>
		/// Retrieve a list of samples for a given frame
		/// </summary>
		/// <param name="frameIndex">Frame index.</param>
		/// <returns>A list of samples for a given frame</returns>
		std::shared_ptr<std::vector<std::pair<int64_t, int64_t>>> loadScanSampleIndexPairs(int frameIndex);

		/// <summary>
		/// Retrieve a list of sample offsets for a given frame
		/// </summary>
		/// <param name="frameIndex">Frame index.</param>
		/// <returns>A list of sample offsets for a given frame</returns>
		std::shared_ptr<std::vector<int64_t>> loadScanSampleOffsets(int frameIndex);

		/// <summary>
		/// Retrieve a list of scan index pair offsets for a given frame
		/// </summary>
		/// <param name="frameIndex">Frame index.</param>
		/// <returns>A list of scan index pair offsets for a given frame</returns>
		std::shared_ptr<std::vector<int64_t>> loadScanIndexPairOffsets(int frameIndex);

		/// <summary>
		/// Retrieve a list of scan index pair offsets for a given frame
		/// </summary>
		/// <param name="frameIndex">Frame index.</param>
		/// <returns>A list of scan index pair offsets for a given frame</returns>
		std::shared_ptr<std::vector<double>> LoadTriggerTimeStamps(int frameIndex);

		// Frame-level internal implementation stuff
		/// <summary>
		/// Retrieve a list of frame metadata for a given frame
		/// </summary>
		/// <param name="frameIndex">Frame index.</param>
		/// <param name="metadataMap">Metadata map.</param>
		/// <returns>A list of frame metadata for a given frame</returns>
		std::shared_ptr<FrameMetadata> loadFrameMetadata(int frameIndex, std::map<std::string, std::string> &metadataMap);

		/// <summary>
		/// Retrieve a list of retention time data for a given file
		/// </summary>
		/// <param name="rtTic">Pointer to vector for rttic data.</param>
		/// <returns>A list of retention time data for a given file</returns>
		bool LoadRtTic(std::vector<int64_t> *rtTic);

		/// <summary>
		/// Retrieve a list of arrival time data for a given file
		/// </summary>
		/// <param name="nFrameIndex">Frame index.</param>
		/// <param name="nSize">Size of at_tic list.</param>
		/// <returns>A list of arrival time data for a given file</returns>
		std::shared_ptr <std::vector<int64_t>> LoadAtTic(int nFrameIndex, int nSize);

		/// <summary>
		/// Loads global metadata information to map
		/// </summary>
		void LoadGlobalMetadataToMap(std::map<std::string, std::string>& mapMetaAll);

		/// <summary>
		/// Loads frame metadata information for a given frame to map
		/// </summary>
		/// <param name="frameIndex">Frame index.</param>
		/// <param name="mapMetaAll">Metadata map.</param>
		void LoadFrameMetadataToMap(int frameIndex, std::map<std::string, std::string>& mapMetaAll);

		/// <summary>
		/// Return the error code from the last method called.
		/// </summary>
		/// <returns>The error code from the last method call.</returns>
		int GetErrorCode();

		/// <summary>
		/// Return an error message from the last method call.
		/// </summary>
		/// <returns>The error message from the last method call.</returns>
		std::string GetErrorMessage();

	private:

        // Thin HDF5 wrapper
		class H5Bridge;

        // Opaque pointer to HDF5 wrapper
	    std::unique_ptr<H5Bridge> pH5Bridge;

		MBIFile* parent_file;
		// file-oriented members
		std::shared_ptr<std::string> input_file_path;

		void setErrorCode(int local_error_code);
		std::string error_message;
		int error_code;
		bool has_rt_tic;
	};
}
