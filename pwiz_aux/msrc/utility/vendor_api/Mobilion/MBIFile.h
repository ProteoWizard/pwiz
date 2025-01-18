// MBIFile.h - The top-level API object interface to MBI data.
// dbodden 8/18/2021
// 0.0.0.0
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

#include "MBIConstants.h"
#include "MBIMetadata.h"
#include "MBICalibration.h"
#include "MBIFrame.h"

#include <cstdio>
#include <fstream>
#include "MBIFileHDF5Adapter.h"
#include <set>

namespace MBISDK
{
extern "C"
{
	/*! @class MBISDK::MBIFile
	*	@brief Top level object for interfacing to the MBI data API.
	*	@author Doug Bodden */
	class MBI_DLLCPP MBIFile
	{

	public:
		///<summary>
		///Experiment types represented by a MBI file.
		///</summary>
		enum class ExperimentType
		{
			/// @brief An MS1 experiment.  Note that if collected on MOBIE with a MassHunter method with fragmentation turned on this can be AIF
			MS1,
			/// @brief AIF data collected through EYEON, as e.g. the high-energy file for stiteched (MIFF) MAF
			AIF,
			/// @brief MAF with a single input file
			SIFF_MAF,
			/// @brief MAF stitched from two input files.
			MIFF_MAF
		};


		/// @brief MBIFile unexpected error code
		static const int ERR_UNEXPECTED = -1;
		/// @brief MBIFile Success code
		static const int ERR_SUCCESS = 0;
		/// @brief MBIFile file not found error code
		static const int ERR_FILE_NOT_FOUND = 1;
		/// @brief MBIFile HDF5 file error code
		static const int ERR_HDF5_FILE_ERROR = 2;
		/// @brief MBIFile file not initialized error code
		static const int ERR_FILE_NOT_INITIALIZED = 3;
		/// @brief MBIFile metadata not loaded error code
		static const int ERR_METADATA_NOT_LOADED = 101;
		/// @brief MBIFile frame not loaded error code
		static const int ERR_FRAME_NOT_LOADED = 102;
		/// @brief MBIFile bad frame index error code
		static const int ERR_BAD_FRAME_INDEX = 201;
		/// @brief MBIFile bad scan index error code
		static const int ERR_BAD_SCAN_INDEX = 202;
		/// @brief MBIFile item missing error code
		static const int ERR_ITEM_MISSING = 301;
		/// @brief MBIFile operation not supported error code
		static const int ERR_OPERATION_NOT_SUPPORTED = 401;

		// MBISDK-45 - compiler flagged an uninitialized memory use warning for processOffsetLengthStats().  Indeed, if the number of frames retrieved is actually zero,
		// then some of the function variables used to set the MBI object members would be uninitialized.  Created this new error to represent that condition.
		/// @brief MBIFile operation zero frames return error code
		static const int ERR_ZERO_FRAMES = 501;

		/// <summary>
		/// Construct an MBIFile for read from a file.
		/// </summary>
		MBIFile();

		/// <summary>
		/// Construct an MBIFile for read from a file.
		/// </summary>
		/// <param name="path">Path to the file to load.</param>
		MBIFile(const char* path);

		/// <summary>
		/// Close the input file and free any associated resources.
		/// </summary>
		void Close();

		/// <summary>
		/// Retrieve the number of scans per Frame for the experiment.
		/// </summary>
		/// <param name="nFrame">The frame index.</param>
		/// <returns>The number of scans in the Frame., or 0 if nFrame is invalid</returns>
		int GetMaxScansInFrame(int nFrame);

		/// <summary>
		/// set the global metadata item for the file.
		/// </summary>
		/// <param name="globalMetadata"></param>
		void SetGlobalMetaData(std::shared_ptr<GlobalMetadata> globalMetadata);

		/// <summary>
		/// The MBIFile's mass calibration.
		/// This method will be removed in a future version of MBI SDK.
		/// </summary>
		/// <returns></returns>
		[[deprecated("Access mass calibrations through Frame objects.")]]
		MBISDK::TofCalibration GetCalibration();

		/// <summary>
		/// The MBIFile's calibration.
		/// </summary>
		/// <returns></returns>
		MBISDK::EyeOnCcsCalibration GetEyeOnCCSCalibration();

		/// <summary>
		/// The MBIFile's calibration.
		/// </summary>
		/// <returns></returns>
		std::string GetCCSCalibration(std::vector<double>* coefficients);

		/// @brief
		/// The type of experiment (e.g. MS1, SIFF-MAF) represented by the MBI File.
		/// @return 
		/// An instance of the enum MBIFile::ExperimentType
		ExperimentType GetExperimentType();

		/// <summary>
		/// THe number of Frames in the MBIFile.
		/// </summary>
		/// <returns>THe number of Frames in the MBIFile.</returns>
		int NumFrames();

		/// <summary>
		/// Load all Frames and return a shared pointer to the collection.
		/// </summary>
		/// <returns>A shared pointer to a vector of Frame pointers, all loaded, or a NULL ptr is frameIndex is invalid</returns>
		std::shared_ptr<std::vector<std::shared_ptr<Frame>>> GetFrames();

		/// <summary>
		/// Load a Frame (if unloaded) and return a shared_ptr to it.
		/// </summary>
		/// <param name="frameIndex">1-based index into the Frame collection.</param>
		/// <returns>shared_ptr to a loaded Frame w/ the specified index.</returns>
		std::shared_ptr<Frame> GetFrame(int frameIndex);

		/// <summary>
		/// Return the metadata pointer for a given Frame.
		/// </summary>
		/// <param name="frameIndex">1-based index into the frame collection.</param>
		/// <returns>shared_ptr to the frame's metadata object, or NULL if frameIndex is invalid.</returns>
		std::shared_ptr<FrameMetadata> GetFrameMetadata(int frameIndex);

		/// <summary>
		/// Deep-load a frame's data. If you only want to deepload frames based on metadata criteria, you may use this to avoid calling GetFrames() to load them all.
		/// </summary>
		/// <param name="frameIndex">The 1-based index into the frame collection.</param>
		void LoadFrameData(int frameIndex);

		/// <summary>
		/// Unload the data sets and memory associated w/ the Frame, and set it to unloaded state.
		/// </summary>
		void UnloadFrame(int frameIndex);

		/// <summary>
		/// Return the max scan samples for the experiment.
		[[deprecated]]
		/// </summary>
		/// <returns>The max number of samples in a scan in the file.</returns>
		/// This method will be removed in a future version of MBI SDK.
		int GetMaxPointsInScan();

		/// <summary>
		/// Retrieve the name of the input file (full path).
		/// </summary>
		/// <returns>std::string the path used to open the file.</returns>
		std::shared_ptr<std::string> GetFilename();

		/// <summary>
		/// Set the name of the input file (full path).
		/// </summary>
		/// <param name="path">Path to the file to load.</param>
		void SetFilename(const char* path);

		/// <summary>
		/// Retrieve the RT-TIC for a frame.
		/// </summary>
		/// <returns>A pair, first being time since beginning of experiment, the second being the TIC, or empty std::pair if frameIndex is invalid.</returns>.
		std::pair<double, int64_t> GetRtTic(int frameIndex);

		/// <summary>
		/// Evaluate the frame index being passed in to ensure it is valid
		/// </summary>
		/// <returns>A bool checking it is safe to use as an array index
		bool CheckFrameIndex(int nFrameIndex);

		/// <summary>
		/// Retrieve a start time for a frame metadata
		/// </summary>
		/// <returns>A double from frame metadata
		double GetFrameStartTime(std::shared_ptr<FrameMetadata> pFrameMD);

		/// <summary>
		/// load the arrival time intensity counts for arrival bins in a frame.
		/// </summary>
		/// <param name="frameIndex">The 1-based index of the frame desired.</param>
		/// <returns>A vector of arrival time intensity counts per bin in the frame.</returns>
		std::shared_ptr <std::vector<int64_t>> loadAtTic(int frameIndex);

		/// <summary>
		/// Initialize the MBIFile object.
		/// </summary>
		bool Init();

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

		/// <summary>
		/// Retrieve the sample rate from global meta data
		/// </summary>
		/// <returns>A double, the sample rate from the file's global metadata
		double GetSampleRate();

		/// <summary>
		/// Retrieve the number of frames global meta data
		/// </summary>
		/// <returns>An int, the number of frames from the file's global metadata
		int GetNumFrames();

		/// <summary>
		/// Return the version string of the library.
		/// </summary>
		/// <returns></returns>
		std::string GetVersion();

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		std::shared_ptr<GlobalMetadata> GetGlobalMetaData();

		/// <summary>
		/// Return the member variable value representing the initialzation status of the class.
		/// Many functions of the class rely on data being properly initialized.  By checking this status first, the library
		/// can be protected against attempted usage in an uninitialized condition
		/// </summary>
		/// <returns>The value stored in the member variable, true or false.</returns>
		bool IsInitialized();

		/// <summary>
		/// Return the lowest offset length statistics from all frames in the data file.
		/// This method will be removed from a future version of MBI SDK
		/// </summary>
		/// <returns>The lowest value computed from all frames.</returns>
		[[deprecated]]
		int64_t GetToFOffset();

		/// <summary>
		/// Return the length of offset length statistics from all frames in the data file.
		/// This method will be removed in a future version of MBI SDK.
		/// </summary>
		/// <returns>The length of all value computed from all frames.</returns>
		[[deprecated]]
		int64_t GetToFLength();

		/// <summary>
		/// Return the lowest arrival bin offset statistics from all frames in the data file.
		/// This method will be removed from a future version of MBI SDK
		/// </summary>
		/// <returns>The lowest value computed from all frames.</returns>
		[[deprecated]]
		size_t GetArrivalBinOffset();

		/// <summary>
		/// Return the length of arrival bin offset statistics from all frames in the data file.
		/// This method will be removed from a future version of MBI SDK
		/// </summary>
		/// <returns>The length of all value computed from all frames.</returns>
		[[deprecated]]
		size_t GetArrivalBinLength();


		/// <summary>
		/// Return specific RT_TIC data from a specific frame
		/// </summary>
		/// <returns>int specific rt value</returns>
		int64_t GetSpecificRtTicValue(int nFrameIndex);

		/// <summary>
		/// List of RT Tic values
		/// </summary>
		/// <returns>List of int64_t values</returns>
		std::shared_ptr<std::vector<std::pair<double, int64_t>>> GetRtTicList();

		/// <summary>
		/// Retrieves single metadata item
		/// </summary>
		/// <returns>String of metadata item</returns>
		std::string getMetaDataItem(std::string key);

		/// <summary>
		/// Retrieves single metadata item
		/// </summary>
		/// <returns>Int of metadata item</returns>
		int getMetaDataItemInt(std::string key);

		/// <summary>
		/// Retrieves single metadata item
		/// </summary>
		/// <returns>Double of metadata item</returns>
		double getMetaDataItemDouble(std::string key);

		/// <summary>
		/// Set the external error message
		/// </summary>
		void setExternalErrorMessage(std::string input_str);

		/// <summary>
		/// Retrieves external error message
		/// </summary>
		/// <returns>external error message</returns>
		std::string getExternalErrorMessage();

	private:
		enum class CollectionMode
		{
            UNKNOWN,
			NONE,
			SIFF,
			MIFF
		};
		
		void setErrorCode(int error_code);
		MBIFileHDF5Adapter * getFileAbstract();
		void loadAllFrameMetadata();
		std::shared_ptr<FrameMetadata> loadFrameMetadata(int frameIndex);
		void loadRtTic();
		void processOffsetLengthStats();

		int MSLevel();

		CollectionMode AcqCollectionMode();
		ExperimentType MIFFFileCollectionMode();


	private:
		bool is_initialized;
		bool processed_offset_length_stats;
		int error_code;
		std::string external_error_message;
		std::shared_ptr < std::map<std::string, std::string>> map_meta_all;
		// file-oriented members
		std::shared_ptr<std::string> input_file_path;

		// Abstract data members
		// Frame collection:
		std::shared_ptr <std::vector<std::shared_ptr<Frame>>> frame_collection;
		std::shared_ptr <std::vector<std::shared_ptr<FrameMetadata>>> frame_metadata_collection;

		int num_frames; ///< Number of frames present in the file.
		int total_scans; ///< Number of scans in the file.
		int max_scans; ///< Maximum number of scans in a frame in the file.
		int max_samples; ///< Maximum number of samples in a frame in the file.
		int total_samples; ///< Number of samples in the file.

		std::shared_ptr<GlobalMetadata> global_metadata;

	    // Acquisition metadata
		int ms_level; /// MS1 or MS2
		CollectionMode collection_mode; // SIFF or MIFF; corresponds to acq-collection-mode in metadata

		// Frame-level internal implementation stuff

		std::shared_ptr<std::vector<std::pair<double, int64_t>>> rt_tic_collection;
		std::string oob_error_message;
		bool has_rt_tic;

		int64_t tof_offset;
		int64_t tof_length;
		size_t scan_offset;
		size_t scan_length;

		MBIFileHDF5Adapter * mbifile_abstract;
	};
}
}
