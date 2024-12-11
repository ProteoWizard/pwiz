// MBIWrapper.h - The top-level API object interface to MBI data.
// Curt Lindmark 2/2/2024
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

#include "MBIFile.h"
#include "MBIFrame.h"
#include "MBICalibration.h"
#include "MBIBinning.h"
#include "MBIStitch.h"
#include "MBITrim.h"
#include "MBIMetadata.h"
#include "MBIMerge.h"

using namespace MBISDK;

extern "C"
{
	static std::string global_error_message;
	/// <summary>
	/// Return the error message for the caller to ask "what went wrong" when something goes wrong.
	/// </summary>
	/// <returns>none</returns>
	MBI_DLLCPP void GetWrapperErrorMessage(char* str, int strlen);

	// begining of MBIFile functions
	/// <summary>
	/// Construct an MBIFile for read from a file.
	/// </summary>
	MBI_DLLCPP MBISDK::MBIFile* Make_MBIFile();

	/// <summary>
	/// Construct an MBIFile for read from a file.
	/// </summary>
	MBI_DLLCPP MBISDK::MBIFile* Make_MBIFile_Path(const char* path);

	/// <summary>
	/// Free an MBIFile memory space.
	/// </summary>
	/// <param name="path">Path to the file to load.</param>
	MBI_DLLCPP void Free_MBIFile(MBISDK::MBIFile* ptr);

	/// <summary>
	/// Construct an MBIFile for read from a file.
	/// </summary>
	/// <param name="path">Path to the file to load.</param>
	MBI_DLLCPP bool Init_MBIFile(MBISDK::MBIFile* ptr);

	/// <summary>
	/// Construct an MBIFile for read from a file.
	/// </summary>
	/// <param name="path">Path to the file to load.</param>
	MBI_DLLCPP void Close_MBIFile(MBISDK::MBIFile* ptr);

	/// <summary>
	/// Return the number of frames for the given file
	/// </summary>
	/// <param name="path">Path to the file to load.</param>
	MBI_DLLCPP int Get_NumFrames(MBISDK::MBIFile* ptr);

	/// <summary>
	/// Retrieve the number of scans per Frame for the experiment.
	/// </summary>
	/// <param name="nFrame">The frame index.</param>
	/// <returns>The number of scans in the Frame., or 0 if nFrame is invalid</returns>
	MBI_DLLCPP int GetMaxScansInFrame(MBISDK::MBIFile* ptr, int nFrame);

	/// <summary>
	/// The MBIFile's calibration.
	/// </summary>
	/// <returns></returns>
	MBI_DLLCPP MBISDK::TofCalibration* GetTofCalibration(MBISDK::MBIFile* ptr);

	/// <summary>
	/// The MBIFile's calibration.
	/// </summary>
	/// <returns></returns>
	MBI_DLLCPP MBISDK::EyeOnCcsCalibration* GetEyeOnCCSCalibration(MBISDK::MBIFile* ptr);

	/// <summary>
	/// The MBIFile's calibration.
	/// </summary>
	/// <returns></returns>
	MBI_DLLCPP void GetCCSCalibration(MBISDK::MBIFile* ptr, char* str, int strlen, char* str_coefficients, int strlen_coefficients);

	/// <summary>
	/// Load all Frames and return a shared pointer to the collection.
	/// </summary>
	/// <returns>A shared pointer to a vector of Frame pointers, all loaded, or a NULL ptr is frameIndex is invalid</returns>
	MBI_DLLCPP bool GetFrames(MBISDK::MBIFile* ptr);

	/// <summary>
	/// Load a Frame (if unloaded) and return a shared_ptr to it.
	/// </summary>
	/// <param name="frameIndex">1-based index into the Frame collection.</param>
	/// <returns>shared_ptr to a loaded Frame w/ the specified index.</returns>
	MBI_DLLCPP MBISDK::Frame* GetFrame(MBISDK::MBIFile* ptr, int frameIndex);

	/// <summary>
	/// Return the metadata pointer for a given Frame.
	/// </summary>
	/// <param name="frameIndex">1-based index into the frame collection.</param>
	/// <returns>shared_ptr to the frame's metadata object, or NULL if frameIndex is invalid.</returns>
	MBI_DLLCPP void GetFrameMetadata(MBISDK::MBIFile* ptr, int frameIndex, char* str, int strlen);

	/// <summary>
	/// Deep-load a frame's data. If you only want to deepload frames based on metadata criteria, you may use this to avoid calling GetFrames() to load them all.
	/// </summary>
	/// <param name="frameIndex">The 1-based index into the frame collection.</param>
	MBI_DLLCPP void LoadFrameData(MBISDK::MBIFile* ptr, int frameIndex);

	/// <summary>
	/// Unload the data sets and memory associated w/ the Frame, and set it to unloaded state.
	/// </summary>
	MBI_DLLCPP void UnloadFrame(MBISDK::MBIFile* ptr, int frameIndex);

	/// <summary>
	/// Return the max scan samples for the experiment.
	/// </summary>
	/// <returns>The max number of samples in a scan in the file.</returns>
	MBI_DLLCPP int GetMaxPointsInScan(MBISDK::MBIFile* ptr);

	/// <summary>
	/// Retrieve the name of the input file (full path).
	/// </summary>
	/// <returns>std::string the path used to open the file.</returns>
	MBI_DLLCPP void GetFilename(MBISDK::MBIFile* ptr, char* str, int strlen);

	/// <summary>
	/// Set the name of the input file (full path).
	/// </summary>
	/// <param name="path">Path to the file to load.</param>
	/// <returns>None</returns>
	MBI_DLLCPP void SetFilename(MBISDK::MBIFile* ptr, const char* path);

	/// <summary>
	/// Retrieve the RT-TIC for a frame.
	/// </summary>
	/// <returns>A pair, first being time since beginning of experiment, the second being the TIC, or empty std::pair if frameIndex is invalid.</returns>.
	MBI_DLLCPP int64_t GetRtTicValue(MBISDK::MBIFile* ptr, int frameIndex);

	/// <summary>
	/// Evaluate the frame index being passed in to ensure it is valid
	/// </summary>
	/// <returns>A bool checking it is safe to use as an array index
	MBI_DLLCPP bool CheckFrameIndex(MBISDK::MBIFile* ptr, int nFrameIndex);

	/// <summary>
	/// load the arrival time intensity counts for arrival bins in a frame.
	/// </summary>
	/// <param name="frameIndex">The 1-based index of the frame desired.</param>
	/// <returns>A vector of arrival time intensity counts per bin in the frame.</returns>
	MBI_DLLCPP void loadAtTic(MBISDK::MBIFile* ptr, int frameIndex, char* str, int strlen);

	/// <summary>
	/// Return the error code from the last method called.
	/// </summary>
	/// <returns>The error code from the last method call.</returns>
	MBI_DLLCPP int GetErrorCode(MBISDK::MBIFile* ptr);

	/// <summary>
	/// Return an error message from the last method call.
	/// </summary>
	/// <returns>The error message from the last method call.</returns>
	MBI_DLLCPP void File_GetErrorMessage(MBISDK::MBIFile* ptr, char* str, int strlen);

	/// <summary>
	/// Retrieve the sample rate from global meta data
	/// </summary>
	/// <returns>A double, the sample rate from the file's global metadata
	MBI_DLLCPP double GetSampleRate(MBISDK::MBIFile* ptr);

	/// <summary>
	/// Return the version string of the library.
	/// </summary>
	/// <returns></returns>
	MBI_DLLCPP void File_GetVersion(MBISDK::MBIFile* ptr, char* str, int strlen);

	/// <summary>
	/// Return a copy of the global metadata for the file.
	/// </summary>
	/// <returns></returns>
	MBI_DLLCPP void GetGlobalMetaData(MBISDK::MBIFile* ptr, char* str, int strlen);

	/// <summary>
	/// Return the member variable value representing the initialzation status of the class.
	/// Many functions of the class rely on data being properly initialized.  By checking this status first, the library
	/// can be protected against attempted usage in an uninitialized condition
	/// </summary>
	/// <returns>The value stored in the member variable, true or false.</returns>
	MBI_DLLCPP bool IsInitialized(MBISDK::MBIFile* ptr);

	/// <summary>
	/// Return the lowest offset length statistics from all frames in the data file.
	/// </summary>
	/// <returns>The lowest value computed from all frames.</returns>
	MBI_DLLCPP int64_t GetToFOffset(MBISDK::MBIFile* ptr);

	/// <summary>
	/// Return the length of offset length statistics from all frames in the data file.
	/// </summary>
	/// <returns>The length of all value computed from all frames.</returns>
	MBI_DLLCPP int64_t GetToFLength(MBISDK::MBIFile* ptr);

	/// <summary>
	/// Return the lowest arrival bin offset statistics from all frames in the data file.
	/// </summary>
	/// <returns>The lowest value computed from all frames.</returns>
	MBI_DLLCPP size_t GetArrivalBinOffset(MBISDK::MBIFile* ptr);

	/// <summary>
	/// Return the length of arrival bin offset statistics from all frames in the data file.
	/// </summary>
	/// <returns>The length of all value computed from all frames.</returns>
	MBI_DLLCPP size_t GetArrivalBinLength(MBISDK::MBIFile* ptr);


	/// <summary>
	/// Return specific RT_TIC data from a specific frame
	/// </summary>
	/// <returns>None</returns>
	MBI_DLLCPP int64_t GetSpecificRtTicValue(MBISDK::MBIFile* ptr, int nFrameIndex);

	/// <summary>
	/// List of RT Tic values
	/// </summary>
	/// <returns>List of int64_t values</returns>
	MBI_DLLCPP void GetRtTicList(MBISDK::MBIFile* ptr, char* str, int strlen);

	/// <summary>
	/// Retrieves single metadata item
	/// </summary>
	/// <returns>String of metadata item</returns>
	MBI_DLLCPP void getMetaDataItem(MBISDK::MBIFile* ptr, char* key, char* str, int strlen);

	/// <summary>
	/// Retrieves single metadata item
	/// </summary>
	/// <returns>Int of metadata item</returns>
	MBI_DLLCPP int getMetaDataItemInt(MBISDK::MBIFile* ptr, char* key);

	/// <summary>
	/// Retrieves single metadata item
	/// </summary>
	/// <returns>Double of metadata item</returns>
	MBI_DLLCPP double getMetaDataItemDouble(MBISDK::MBIFile* ptr, char* key);

	/// <summary>
	/// Set the external error message
	/// </summary>
	MBI_DLLCPP void File_setExternalErrorMessage(MBISDK::MBIFile* ptr, std::string input_str);

	/// <summary>
	/// Retrieves external error message
	/// </summary>
	/// <returns>external error message</returns>
	MBI_DLLCPP void File_getExternalErrorMessage(MBISDK::MBIFile* ptr, char* str, int strlen);

	// end of MBIFile functions
	
	// beginning of MBIFrame functions
	/// <summary>
	/// Default constructor for creating a Frame independently of a file to load it from.
	/// This should generally NOT be called by API users looking to use Frame/Scan data, use MBIFile::GetFrame() instead.
	/// </summary>
	MBI_DLLCPP MBISDK::Frame* Make_MBIFrame();

	/// <summary>
	/// Indicates whether a Frame's deep-data load has been completed. If not, it's usually
	/// safest to do a MBIFile::GetFrame(frameIndex).
	/// </summary>
	/// <returns>true if loaded, false if not loaded.</returns>
	MBI_DLLCPP bool IsLoaded(MBISDK::Frame* ptr);

	/// <summary>
	/// Load the abstract sparse cube data to the Frame for later use.
	/// This should generally NOT be called by API users looking to use Frame/Scan data, use MBIFile::GetFrame() instead.
	/// </summary>
	/// <param name="inputSparseSampleIntensities">An implementation-specific approach to storing sparse intinsity data.</param>
	/// <param name="inputGates">An implementation-specific approach to storing sparse intinsity data.</param>
	/// <param name="inputSampleOffsets">An implementation-specific approach to storing sparse intinsity data.</param>
	/// <param name="intputGateIndexOffsets">An implementation-specific approach to storing sparse intinsity data.</param>
	/// <param name="inputVecAtTic">The list of AT_TIC for each frame.</param>
	/// <param name="inputVecTriggerTimeStamps">The list of trigger timestamps for each scan.</param>
	MBI_DLLCPP void Load(MBISDK::Frame* ptr, char* str_data_count_list, char* str_data_position_list, char* str_index_count_list, char* str_index_position_list, char* str_attic_list, char* str_trigger_timestamp_list);

	/// <summary>
	/// Removes references to the deep-loaded Frame data and frees the associated memory.
	/// </summary>
	MBI_DLLCPP void Unload(MBISDK::Frame* ptr);

	/// <summary>
	/// Pulls the non-zero scan indices out of a Frame's deep data. This has to be done after a frame is loaded.
	/// </summary>
	/// <returns>A vector of indices (0 based) that contain samples.</returns>
	MBI_DLLCPP void GetNonZeroScanIndices(MBISDK::Frame* ptr, char* str, int strlen);

	/// <summary>
	/// Return total intensity in a frame.
	/// </summary>
	/// <returns>A sum of all samples in a frame.</returns>
	MBI_DLLCPP int64_t GetFrameTotalIntensity(MBISDK::Frame* ptr);

	/// <summary>
	/// Return total intensity in a Scan.
	/// </summary>
	/// <param name="scanIndex">0-based scan index of the frame.</param>
	/// <returns>The sum of the intensities in a Scan.</returns>
	MBI_DLLCPP size_t GetScanTotalIntensity(MBISDK::Frame* ptr, size_t scanIndex);

	/// <summary>
	/// Return the number of samples per scan (dense/acquisition, not sparse/nonzero).
	/// </summary>
	/// <returns>The number of samples per scan during acquisition.</returns>
	MBI_DLLCPP int Frame_GetMaxPointsInScan(MBISDK::Frame* ptr);

	/// <summary>
	/// Indicate whether there is fragmentation data available in the Frame.
	/// </summary>
	/// <returns>true or falsed based on the frm-frag-op-mode value being "FragHiLo" and the energy value not being equal to 0.0</returns>
	MBI_DLLCPP bool isFragmentationData(MBISDK::Frame* ptr);

	/// <summary>
	/// Get the Frame's offset Time in seconds.
	/// </summary>
	/// <returns>The start time in seconds of a given frame relative the beginning of the experiment.</returns>
	MBI_DLLCPP double Time(MBISDK::Frame* ptr);

	/// <summary>
	/// return the number of scans in the frame.
	/// </summary>
	/// <returns></returns>
	MBI_DLLCPP size_t GetNumScans(MBISDK::Frame* ptr);

	/// <summary>
	/// Retrieve a Scan's mZ-based intensity as a pair of sparse vectors.
	/// </summary>
	/// <param name="scanIndex">0-based index of the Scan within the Frame.</param>
	/// <param name="mzIndex">A pointer to the vector of mZ values to be populated.</param>
	/// <param name="intensity">A pointer to the vector of intensities to be populated.</param>
	/// <returns>true on success, false on error (bad calibration data)</returns>
	MBI_DLLCPP bool GetScanDataMzIndexedSparse(MBISDK::Frame* ptr, size_t scanIndex, char* output_mz_list, int mz_len, char* output_intensity_list, int intensity_len);

	/// <summary>
	/// Retrieve a Scan's mZ-based intensity as a dense vector with all sample points represented, including zeroes, with the accompanying mZ indexes.
	/// </summary>
	/// <param name="scanIndex">0-based index of the Scan within the Frame.</param>
	/// <param name="mzIndex">A pointer to the vector of mZ values to be populated.</param>
	/// <param name="intensity">A pointer to the vector of intensities to be populated.</param>
	/// <returns>true on success, false on error (bad calibration data)</returns>
	MBI_DLLCPP bool GetScanDataMzIndexedDense(MBISDK::Frame* ptr, size_t scanIndex, char* output_mz_list, int mz_len, char* output_intensity_list, int intensity_len);

	/// <summary>
	/// Retrieve a Scan's ToF-based intensity as a sparse vector w/ accompanying sparse ToF indexes..
	/// </summary>
	/// <param name="scanIndex">0-based index of the Scan within the Frame.</param>
	/// <param name="tofIndex">A pointer to the vector of ToF index values to be populated.</param>
	/// <param name="intensity">A pointer to the vector of intensities to be populated.</param>
	MBI_DLLCPP void GetScanDataToFIndexedSparse(MBISDK::Frame* ptr, size_t scanIndex, char* output_tof_list, int tof_len, char* output_int_list, int int_len);

	/// <summary>
	/// Retrieve a Scan's ToF-based intensity as a dense vector.
	/// </summary>
	/// <param name="scanIndex">0-based index of the Scan within the Frame.</param>
	/// <param name="intensity">THe dense vector of intensity samples.</param>
	MBI_DLLCPP void GetScanDataToFIndexedDense(MBISDK::Frame* ptr, size_t scanIndex, char* output_int_list, int int_len);

	/// <summary>
	/// Alternative way to get metadata item.
	/// </summary>
	/// <param name="key"></param>
	/// <returns></returns>
	MBI_DLLCPP void getFrameMetaDataItem(MBISDK::Frame* ptr, char* key, char* str, int strlen);

	/// <summary>
	/// Build a summed scan from all scans in the frame, and provide the minimum and maximum ToF
	/// indices and the scans that contained them.
	/// </summary>
	/// <param name="intensityOut">The vector (dense) of sample intensities summed across all scans in the frame.</param>
	/// <param name="minToFIndex">The minimum ToF index found.</param>
	/// <param name="minToFScanIndex">The scan index containing the minimum tof index.</param>
	/// <param name="maxToFIndex">The maximum ToF index found.</param>
	/// <param name="maxToFScanIndex">The scan index containing the maximum ToF index.</param>
	MBI_DLLCPP void GetScanSummationToFIndexedDense(MBISDK::Frame* ptr, 
													char* str_size_t_intensity, int str_size_t_intensity_len,
													int64_t* minToFIndex, size_t* minToFScanIndex,
													int64_t* maxToFIndex, size_t* maxToFScanIndex);

	/// <summary>
	/// Return the width of an arrival bin (scan/drift/AT).
	/// </summary>
	/// <returns>The bin width in milliseconds.</returns>
	MBI_DLLCPP double GetArrivalBinWidth(MBISDK::Frame* ptr);

	/// <summary>
	/// Give the arrival bin time offset for a given index.
	/// </summary>
	/// <param name="binIndex">The 0-based bin index.</param>
	/// <returns>The time offset, in milliseconds, of the start of the bin. -1.0 if an invalid bin index is passed in.</returns>
	MBI_DLLCPP double GetArrivalBinTimeOffset(MBISDK::Frame* ptr, size_t binIndex);

	/// <summary>
	/// Return the arrival bin index for the given time offset, or -1 if it's an invalid offset.
	/// </summary>
	/// <param name="timeOffset">Time offset into the frame, in milliseconds.</param>
	/// <returns>The bin which contains the time offset, or -1 if it's an invalid time offset.</returns>
	MBI_DLLCPP int GetArrivalBinIndex(MBISDK::Frame* ptr, double timeOffset);


	/// <summary>
	/// A single gathering point for all metadata reading
	/// </summary>
	/// <returns>A string of metadata</returns>
	MBI_DLLCPP void GetGenericMetaData(MBISDK::Frame* ptr, char* strKey, char* str, int strlen);

	/// <summary>
	/// A single gathering point for all metadata reading
	/// </summary>
	/// <returns>An int of metadata</returns>
	MBI_DLLCPP int GetGenericMetaDataInt(MBISDK::Frame* ptr, char* strKey);

	/// <summary>
	/// A single gathering point for all metadata reading
	/// </summary>
	/// <returns>A double of metadata</returns>
	MBI_DLLCPP double GetGenericMetaDataDouble(MBISDK::Frame* ptr, char* strKey);

	/// <summary>
	/// Return list of ATTic values for this frame
	/// </summary>
	/// <returns>None.</returns>
	MBI_DLLCPP void GetATTicList(MBISDK::Frame* ptr, char* str, int strlen);
	                                               


	/// <summary>
	/// Return a specific value to a specific index of ATTic list for a given frame
	/// </summary>
	/// <returns>None.</returns>
	MBI_DLLCPP int64_t GetATTicItem(MBISDK::Frame* ptr, int nIndex);

	/// <summary>
	/// Return list of DATA_COUNT values for this frame
	/// </summary>
	/// <returns>None.</returns>
	MBI_DLLCPP void GetSampleIntensities(MBISDK::Frame* ptr, char* str, int strlen);


	/// <summary>
	/// Return a specific value to a specific index of DATA_COUNT list for a given frame
	/// </summary>
	/// <returns>None.</returns>
	MBI_DLLCPP int32_t GetDataCountItem(MBISDK::Frame* ptr, int nIndex);

	/// <summary>
	/// Return list of DATA_POSITIONS values for this frame
	/// </summary>
	/// <returns>None.</returns>
	MBI_DLLCPP void GetGates(MBISDK::Frame* ptr, char* str, int strlen);


	/// <summary>
	/// Return a specific value to a specific index of DATA_POSITIONS first list for a given frame
	/// </summary>
	/// <returns>None.</returns>
	MBI_DLLCPP int64_t GetDataPositionItemFirst(MBISDK::Frame* ptr, int nIndex);


	/// <summary>
	/// Return a specific value to a specific index of DATA_POSITIONS second list for a given frame
	/// </summary>
	/// <returns>None.</returns>
	MBI_DLLCPP int64_t GetDataPositionItemSecond(MBISDK::Frame* ptr, int nIndex);

	/// <summary>
	/// Return list of INDEX_COUNT values for this frame
	/// </summary>
	/// <returns>None.</returns>
	MBI_DLLCPP void GetSampleOffsets(MBISDK::Frame* ptr, char* str, int strlen);
	

	/// <summary>
	/// Return a specific value to a specific index of INDEX_COUNT list for a given frame
	/// </summary>
	/// <returns>None.</returns>
	MBI_DLLCPP int64_t GetIndexCountItem(MBISDK::Frame* ptr, int nIndex);

	/// <summary>
	/// Return list of INDEX_POSITION values for this frame
	/// </summary>
	/// <returns>None.</returns>
	MBI_DLLCPP void GetGateIndexOffsets(MBISDK::Frame* ptr, char* str, int strlen);


	/// <summary>
	/// Return a specific value to a specific index of INDEX_POSITION list for a given frame
	/// </summary>
	/// <returns>None.</returns>
	MBI_DLLCPP int64_t GetIndexPositionItem(MBISDK::Frame* ptr, int nIndex);

	/// <summary>
	/// Return a list of trigger time stamps for a given frame
	/// </summary>
	/// <returns>None.</returns>
	MBI_DLLCPP void GetTriggerTimeStamps(MBISDK::Frame* ptr, char* str, int strlen);


	/// <summary>
	/// Return a specific value to a specific index of INDEX_POSITION list for a given frame
	/// </summary>
	/// <returns>Trigger Timestamp Item</returns>
	MBI_DLLCPP double GetTriggerTimeStampItem(MBISDK::Frame* ptr, int nIndex);

	/// <summary>
	/// retrieves local copy of frame metadata
	/// </summary>
	/// <returns>pointer to frame metadata.</returns>
	MBI_DLLCPP void GetFrameMetaData(MBISDK::Frame* ptr, char* str, int strlen);

	/// <summary>
	/// Examines frame metadata to determine collision energy for specific frame
	/// </summary>
	/// <returns>Collision energy value</returns>
	MBI_DLLCPP double GetCollisionEnergy(MBISDK::Frame* ptr);

	/// <summary>
	/// Examines frame metadata to determine collision energy for specific frame is valid
	/// </summary>
	/// <returns>true if collision energy is not blank</returns>
	MBI_DLLCPP bool IsCollisionEnergyValid(MBISDK::Frame* ptr);

	///  @brief Return the details during an error to assist developers
	MBI_DLLCPP void Frame_GetErrorOutput(MBISDK::Frame* ptr, char* str, int strlen);

	//end of Frame functions

	//beginning of Calibration functions
	/// @brief Initialize a calibration object
	MBI_DLLCPP MBISDK::TofCalibration* Make_TofCalibration();

	/// @brief De-initialize a calibration object
	MBI_DLLCPP void Free_TofCalibration(MBISDK::TofCalibration* ptr);

	/// @brief Compute mass error from polynomial residual fit.
	MBI_DLLCPP double TofError(MBISDK::TofCalibration* ptr, double uSecTOF);

	/// @brief Convert tof bin index to time-of-flight
	MBI_DLLCPP double IndexToMicroseconds(MBISDK::TofCalibration* ptr, int64_t index);

	/// @brief Convert time-of-flight to m/z using this calibration.
	MBI_DLLCPP double MicrosecondsToMz(MBISDK::TofCalibration* ptr, double uSec);

	/// @brief Convert tof bin index to m/z using this calibration.
	MBI_DLLCPP double IndexToMz(MBISDK::TofCalibration* ptr, int64_t index);

	/// @brief Convert time-of-flight to tof bin index.
	MBI_DLLCPP size_t MicrosecondsToIndex(MBISDK::TofCalibration* ptr, double uSec);

	/// @brief Convert m/z to time-of-flight using this calibration.
	MBI_DLLCPP double MzToMicroseconds(MBISDK::TofCalibration* ptr, double mz);

	/// @brief Convert m/z to tof bin index using this calibration.
	MBI_DLLCPP size_t MzToIndex(MBISDK::TofCalibration* ptr, double mz);

	/// @brief Retrieve the slope of the calibration.
	MBI_DLLCPP double Slope(MBISDK::TofCalibration* ptr);

	/// @brief Retrieve the intercept of the calibration.
	MBI_DLLCPP double Intercept(MBISDK::TofCalibration* ptr);

	/// @brief return status of failure
	MBI_DLLCPP int getFailureStatus(MBISDK::TofCalibration* ptr);

	/// @brief return status of StatusText
	MBI_DLLCPP void TOF_getStatusText(MBISDK::TofCalibration* ptr, char* str, int strlen);

	/// @brief Initialize a calibration object from json representation.
	MBI_DLLCPP MBISDK::CcsCalibration* Make_CcsCalibration(double sampleRate, const char* jsonString);

	/// @brief De-initialize a calibration object from json representation.
	MBI_DLLCPP void Free_CcsCalibration(MBISDK::CcsCalibration* ptr);

	/// @brief Get the type of calibration
	MBI_DLLCPP int CCS_Type(MBISDK::CcsCalibration* ptr);

	/// @brief Get the coefficients
	MBI_DLLCPP void CCS_CAL_Coefficients(MBISDK::CcsCalibration* ptr, char* str, int strlen);

	/// @brief Initialize a GlobalCcsCalibration object from json representation.
	MBI_DLLCPP MBISDK::GlobalCcsCalibration* Make_GlobalCcsCalibration(const char* jsonString);

	/// @brief De-initialize a GlobalCcsCalibration object from json representation.
	MBI_DLLCPP void Free_GlobalCcsCalibration(MBISDK::GlobalCcsCalibration* ptr);

	/// @brief Get the type of calibration
	MBI_DLLCPP int GlobalCcs_Type(MBISDK::GlobalCcsCalibration* ptr);

	/// @brief Initialize a t object from json representation.
	MBI_DLLCPP EyeOnCcsCalibration* Make_EyeOnCcsCalibration(MBISDK::MBIFile* input_mbi_file_ptr);

	/// @brief De-initialize a EyeOnCcsCalibration object from json representation.
	MBI_DLLCPP void Free_EyeOnCcsCalibration(MBISDK::EyeOnCcsCalibration* ptr);

	/// @brief Parse a JSON string for EyeOnCcsCalibration.
	MBI_DLLCPP void ParseCCSCal(MBISDK::EyeOnCcsCalibration* ptr, std::string strCCSData);

	/// @brief Get CCS Minimum value
	MBI_DLLCPP double GetCCSMinimum(MBISDK::EyeOnCcsCalibration* ptr);

	/// @brief Get CCS Maximum value
	MBI_DLLCPP double GetCCSMaximum(MBISDK::EyeOnCcsCalibration* ptr);

	/// @brief Get Degree value
	MBI_DLLCPP int GetDegree(MBISDK::EyeOnCcsCalibration* ptr);

	/// @brief Get AT Surfing value, measured in milliseconds
	MBI_DLLCPP double GetAtSurf(MBISDK::EyeOnCcsCalibration* ptr);

	/// @brief Get CCS Coefficient for a given file
	MBI_DLLCPP void GetEyeOnCCSCoefficients(MBISDK::EyeOnCcsCalibration* ptr, char* str, int strlen);

	/// @brief Get CCS Coefficient for a given file
	MBI_DLLCPP void SetEyeOnCCSCoefficients(MBISDK::EyeOnCcsCalibration* ptr, char* str);

	/// @brief Calculate CCS value for given AT
	MBI_DLLCPP double ArrivalTimeToCCS(MBISDK::EyeOnCcsCalibration* ptr, double scan_arrival_time, double ion_mass);

	/// @brief Calculate CCS value for set of AT
	MBI_DLLCPP void ArrivalTimeToCCS2(MBISDK::EyeOnCcsCalibration* ptr, char* input_str, char* output_str, int strlen);

	/// @brief Calculate CCS value for the AT of a single frame of intensities
	MBI_DLLCPP void ArrivalTimeToCCS3(MBISDK::EyeOnCcsCalibration* ptr, int frame_index, char* str, int strlen);

	/// @brief Return the details during an error to assist developers
	MBI_DLLCPP void EyeOnCcs_GetErrorOutput(MBISDK::EyeOnCcsCalibration* ptr, char* str, int strlen);

	///  @brief Return the value of the gas mass for the experiment
	MBI_DLLCPP double ComputeGasMass(MBISDK::EyeOnCcsCalibration* ptr, std::string gas_string);

	///  @brief Calculate adjusted CCS value based on arrival time
	MBI_DLLCPP double AdjustCCSValue(MBISDK::EyeOnCcsCalibration* ptr, double unadjusted_ccs, double dbArrivalTime, double Mz_ion);

	///  @brief Return the value of the gas mass for the experiment
	MBI_DLLCPP double ComputeGasMassDefault(MBISDK::EyeOnCcsCalibration* ptr);

	///  @brief Generate a message to throw an error
	MBI_DLLCPP void EyeOn_CCS_GenerateThrownErrorMessage(MBISDK::EyeOnCcsCalibration* ptr, int error_type, char* str, int strlen, int frame_index = 0, double arrival_time = 0.0);

	///  @brief evaluate pointer to ensure it is not null
	MBI_DLLCPP bool IsValid(MBISDK::EyeOnCcsCalibration* calptr, void* ptr);

	///  @brief override the gas mass value
	MBI_DLLCPP void SetGasMass(MBISDK::EyeOnCcsCalibration* ptr, double gas_mass_input);
	//end of Calibration functions

}

//local utility functions
/// <summary>
/// Convert shared pointer to a vector of int64s to a JSON structure for interop.
/// </summary>
/// <returns>JSON string</returns>
std::string MarshalIntoJSON(std::string strType, std::shared_ptr <std::vector<int64_t>> input_list);

/// <summary>
/// Convert shared pointer to a vector of int32s to a JSON structure for interop.
/// </summary>
/// <returns>JSON string</returns>
std::string MarshalIntoJSON32(std::string strType, std::shared_ptr <std::vector<int32_t>> input_list);

/// <summary>
/// Convert shared pointer to a vector of pairs of int32s to a JSON structure for interop.
/// </summary>
/// <returns>JSON string</returns>
std::string MarshalIntoJSONPairs(std::string strType, std::shared_ptr<std::vector<std::pair<int64_t, int64_t>>> input_list);

/// <summary>
/// Convert shared pointer to a vector of pairs of doubles and int32s to a JSON structure for interop.
/// </summary>
/// <returns>JSON string</returns>
std::string MarshalIntoJSONDoubleInt(std::string strType, std::shared_ptr<std::vector<std::pair<double, int64_t>>> input_list);

/// <summary>
/// Convert shared pointer to a metadata map to a JSON structure for interop.
/// </summary>
/// <returns>JSON string</returns>
std::string MarshalIntoJSONMetaData(std::string strType, std::shared_ptr<Metadata> input_list);

/// <summary>
/// Convert vector of doubles to a JSON structure for interop.
/// </summary>
/// <returns>JSON string</returns>
std::string MarshalIntoJsonDoubleVector(std::string strType, std::vector<double> input_list);

/// <summary>
/// Convert vector of size_t to a JSON structure for interop.
/// </summary>
/// <returns>JSON string</returns>
std::string MarshalIntoJSONSizeT(std::string strType, std::vector<size_t> input_list);

/// <summary>
/// Convert vector of size_t to a JSON structure for interop.
/// </summary>
/// <returns>JSON string</returns>
std::vector<std::string> MarshalIntoJSONStringVector(std::string input_list);

/// <summary>
/// Convert vector of tuples to a JSON structure for interop.
/// </summary>
/// <returns>JSON string</returns>
std::string MarshalIntoJsonTupleIntDoubleDouble(std::string strType, std::vector<std::tuple<int64_t, double, double>> input_list);

/// <summary>
/// Convert vector of long long to a JSON structure for interop.
/// </summary>
/// <returns>JSON string</returns>
std::string MarshalIntoJSONLongLong(std::string strType, std::vector<long long> input_list);

/// <summary>
/// Convert vector of doubles to a JSON structure for interop.
/// </summary>
/// <returns>vector of doubles</returns>
std::vector<double> MarshalJSONIntoJsonDoubleVector(std::string input);


/// <summary>
/// Convert shared pointer to a vector of pairs of doubles and int32s to a JSON structure for interop.
/// </summary>
/// <returns>vector of pairs</returns>
std::shared_ptr<std::vector<std::pair<double, int64_t>>> MarshalFromJSONDoubleInt(std::string input);

/// <summary>
/// Convert string to a shared pointer to a vector of int32s for interop.
/// </summary>
/// <returns>vector of pairs</returns>
std::shared_ptr<std::vector<int32_t>> MarshalFromJSonInt32(std::string input);

/// <summary>
/// Convert string to a shared pointer to a vector of int64s for interop.
/// </summary>
/// <returns>vector of int64s</returns>
std::shared_ptr<std::vector<int64_t>> MarshalFromJSonInt64(std::string input);

/// <summary>
/// Convert string to a shared pointer to a vector of doubles for interop.
/// </summary>
/// <returns>vector of doubles</returns>
std::shared_ptr<std::vector<double>> MarshalFromJsonDouble(std::string input);

/// <summary>
/// Convert string to a shared pointer to a vector of pairs of ints for interop.
/// </summary>
/// <returns>vector of pairs of ints</returns>
std::shared_ptr<std::vector<std::pair<int64_t, int64_t>>> MarshalFromJsonPairInts(std::string input);

/// <summary>
/// Convert string to a shared pointer to a vector of pairs of ints for interop.
/// </summary>
/// <returns>vector of pairs of ints</returns>
std::vector<std::tuple<int64_t, double, double>> MarshalFromJsonTupleIntDoubleDouble(std::string input);

/// <summary>
/// Convert string with token to a vector of sub strings
/// </summary>
/// <returns>vector of strings</returns>
std::vector<std::string> SplitStringByToken(std::string input, std::string token);

/// <summary>
/// Convert string to a shared pointer to a vector of size_t's for interop.
/// </summary>
/// <returns>vector of size_t's</returns>
std::vector<size_t> MarshalIntoJSONSizeT(std::string input);

/// <summary>
/// Set the error message for the caller to ask "what went wrong" when something goes wrong.
/// </summary>
/// <returns>none</returns>
void SetWrapperErrorMessage(std::string function_name, std::string input);
