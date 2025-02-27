// Frame.h - Represents a frame (ion injection event).
/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * MBI Data Access API                                             *
 * Copyright 2021 MOBILion Systems, Inc. ALL RIGHTS RESERVED       *
 * Author: DougBodden                                             *
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

#include "MBIMetadata.h"
#include "MBICalibration.h"
#include <memory>
#include <vector>



namespace MBISDK
{
	class MBIFile; //forward declaration

    /*! @struct MBISDK::COOArray
	 *  @brief A structure holding a coordinate object array
	 */
	template<typename var>
	struct COOArray
	{
		std::vector<var> data;
		std::vector<size_t> rowIndices;
		std::vector<size_t> columnIndices;
		size_t nRows;
		size_t nColumns;
		size_t nnz;
	};

    /*! @struct MBISDK::CSRArray
	 *  @brief A structure for an aray in the Compressed Sparse Row format
	 */
	template<typename var>
	struct CSRArray
	{
		std::vector<var> data;
		std::vector<size_t> indices;
		std::vector<size_t> indptr;
		size_t nRows;
		size_t nColumns;
		size_t nnz;
	};

extern "C"
{


    /*! @struct MBIDSK::MassSpectrum
	 *  @brief A 1-D collection of intensities, together with values to assign on the "x" axis
	 */
    struct MassSpectrum
	{
		std::vector<int32_t> intensities;
		std::vector<size_t> indices;
		std::vector<double> mz;
		bool isZeroPadded = false;
		size_t nnz;
	};



	/*! @class MBISDK::Frame
	*   @brief A class allowing interface with an individual MBI Frame within a file.
	*/
	class MBI_DLLCPP Frame
	{
	public:
		/// <summary>
		/// Default constructor for creating a Frame independently of a file to laod it from.
		/// This should generally NOT be called by API users looking to use Frame/Scan data, use MBIFile::GetFrame() instead.
		/// </summary>
		Frame();

		/// <summary>
		/// Construct a frame w/ metadata only (for fast access without deep load).
		/// This should generally NOT be called by API users looking to use Frame/Scan data, use MBIFile::GetFrame() instead.
		/// </summary>
		/// <param name="metadata">The frame's metadata.</param>
		/// <param name="pFile">The frame's file pointer.</param>
		Frame(std::shared_ptr<FrameMetadata> metadata, MBISDK::MBIFile* pFile);

		/// <summary>
		/// Fully construct and load a frame from abstract metadata and a particular sparse matrix implementation.
		/// This should generally NOT be called by API users looking to use Frame/Scan data, use MBIFile::GetFrame() instead.
		/// </summary>
		/// <param name="metadata">The frame's metadata.</param>
		/// <param name="pFile">The file's pointer.</param>
		/// <param name="inputSparseSampleIntensities">The sparse vector of samples (intensities) across the entire frame.</param>
		/// <param name="inputGates">The start/stop index values per scan.</param>
		/// <param name="inputSampleOffsets">The offset into the sample intensities per scan.</param>
		/// <param name="inputGateIndexOffsets">The offset into the sample index pairs per scan.</param>
		/// <param name="inputVecAtTic">The list of AT_TIC for each frame.</param>
		/// <param name="inputVecTriggerTimeStamps">The list of trigger timestamps for each scan.</param>
		Frame(std::shared_ptr<FrameMetadata> metadata, MBISDK::MBIFile* pFile,
			std::shared_ptr<std::vector<int32_t>> inputSparseSampleIntensities,
			std::shared_ptr<std::vector<std::pair<int64_t, int64_t>>> inputGates,
			std::shared_ptr<std::vector<int64_t>> inputSampleOffsets,
			std::shared_ptr<std::vector<int64_t>> inputGateIndexOffsets,
			std::shared_ptr<std::vector<int64_t>> inputVecAtTic,
			std::shared_ptr<std::vector<double>> inputVecTriggerTimeStamps);

		/// <summary>
		/// Indicates whether a Frame's deep-data load has been completed. If not, it's usually
		/// safest to do a MBIFile::GetFrame(frameIndex).
		/// </summary>
		/// <returns>true if loaded, false if not loaded.</returns>
		bool IsLoaded();

		/// <summary>
		/// Load metadata into a Frame.
		/// This should not be used if you are reading an MBI file from disk.
		/// </summary>
		/// <param name="metadata">Key/value pairs of metadata.</param>
		void LoadMetadata(std::shared_ptr<FrameMetadata> metadata);

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
		void Load(std::shared_ptr<std::vector<int32_t>> inputSparseSampleIntensities,
			std::shared_ptr<std::vector<std::pair<int64_t, int64_t>>> inputGates,
			std::shared_ptr<std::vector<int64_t>> inputSampleOffsets,
			std::shared_ptr<std::vector<int64_t>> intputGateIndexOffsets,
			std::shared_ptr<std::vector<int64_t>> inputVecAtTic,
			std::shared_ptr<std::vector<double>> inputVecTriggerTimeStamps);

		/// <summary>
		/// Removes references to the deep-loaded Frame data and frees the associated memory.
		/// </summary>
		void Unload();

		/// <summary>
		/// Pulls the non-zero scan indices out of a Frame's deep data. This has to be done after a frame is loaded.
		/// </summary>
		/// <returns>A vector of indices (0 based) that contain samples.</returns>
		std::vector<size_t> GetNonZeroScanIndices();

		/// <summary>
		/// Return total intensity in a frame.
		/// </summary>
		/// <returns>A sum of all samples in a frame.</returns>
		int64_t GetFrameTotalIntensity();

		/// <summary>
		/// Return total intensity in a Scan.
		/// </summary>
		/// <param name="scanIndex">0-based scan index of the frame.</param>
		/// <returns>The sum of the intensities in a Scan.</returns>
		size_t GetScanTotalIntensity(size_t scanIndex);

		/// <summary>
		/// Return the number of samples per scan (dense/acquisition, not sparse/nonzero).
		/// </summary>
		/// <returns>The number of samples per scan during acquisition.</returns>
		int GetMaxPointsInScan();

		/// <summary>
		/// The Frame's calibration.
		/// </summary>
		/// <returns></returns>
		MBISDK::TofCalibration GetCalibration();

		/// <summary>
		/// Indicate whether there is fragmentation data available in the Frame.
		/// This method will be removed in a future version of MBI SDK
		/// </summary>
		/// <returns>true or falsed based on the frm-frag-op-mode value being "FragHiLo" and the energy value not being equal to 0.0</returns>
		[[deprecated]]
		bool isFragmentationData();

		/// <summary>
		/// Get the Frame's offset Time in seconds.
		/// </summary>
		/// <returns>The start time in seconds of a given frame relative the beginning of the experiment.</returns>
		double Time();

		/// <summary>
		/// return the number of scans in the frame.
		/// </summary>
		/// <returns></returns>
		size_t GetNumScans();

		/// <summary>
		/// Retrieve the frame's data as a coordinate object array.
		/// </summary>
		/// <returns> An int32 COOArray containing the frame intensities and indices.
		COOArray<int32_t> GetFrameDataAsCOOArray();

		/// <summary>
		/// Retrieve the frame's data as a compressed sparse row array.
		/// </summary>
		/// <returns> An int32 CSRArray containing the frame intensities and indices.
		CSRArray<int32_t> GetFrameDataAsCSRArray();

        /// <summary>
		/// Retrieve the frame data into pointers to vectors as the data, indices, indptr arrays of a CSR array
		/// </summary>
		/// <param name="scanIndex">0-based index of the Scan within the Frame.</param>
		/// <param name="data">A pointer to the vector of intensities to be populated.</param>
		/// <param name="indices">A pointer to the vector of indices to be populated.</param>
		/// <param name="indptr">A pointer to the indptr ("index pointer") vector to be populated.</param>
		/// <returns>true on success, false on failure</returns>
		bool GetFrameDataAsCSRComponents(std::vector<int32_t>* data, std::vector<size_t>* indices, std::vector<size_t>* indptr);

		/// <summary>
		/// Retrieve a Scan's mZ-based intensity as a pair of sparse vectors.
		/// </summary>
		/// <param name="scanIndex">0-based index of the Scan within the Frame.</param>
		/// <param name="mzIndex">A pointer to the vector of mZ values to be populated.</param>
		/// <param name="intensity">A pointer to the vector of intensities to be populated.</param>
		/// <returns>true on success, false on error (bad calibration data)</returns>
		bool GetScanDataMzIndexedSparse(size_t scanIndex, std::vector<double>* mzIndex, std::vector<size_t>* intensity);

		/// <summary>
		/// Retrieve a Scan's mZ-based intensity as a dense vector with all sample points represented, including zeroes, with the accompanying mZ indexes.
		/// </summary>
		/// <param name="scanIndex">0-based index of the Scan within the Frame.</param>
		/// <param name="mzIndex">A pointer to the vector of mZ values to be populated.</param>
		/// <param name="intensity">A pointer to the vector of intensities to be populated.</param>
		/// <returns>true on success, false on error (bad calibration data)</returns>
		bool GetScanDataMzIndexedDense(size_t scanIndex, std::vector<double>* mzIndex, std::vector<size_t>* intensity);

        /// <summary>
		/// Retrieve a mass spectrum from the frame.
		/// </summary>
		/// <param name="scanIndex">0-based index of the scan within the frame</param>
		/// <param name="zeroPaddedReturns">Set to true if zeroes are to be added (for plotting convenience) to the beginning and end of nonzero intervals.</param>
		/// <returns> An instance of the MassSpectrum structure, containing the points and indices of interest.</returns>
		MassSpectrum GetMassSpectrum(size_t scanIndex, bool zeroPaddedReturns);

		/// <summary>
		/// Retrieve a mass spectrum from the frame.
		/// </summary>
		/// <param name="scanIndex">0-based index of the scan within the frame</param>
		/// <returns> An instance of the MassSpectrum structure, containing the points and indices of interest.</returns>
		MassSpectrum GetMassSpectrum(size_t scanIndex);

        /// <summary>
		/// Retrieve a scan (row) from the frame.
		/// </summary>
		/// <param name="scanIndex">0-based index of the scan within the frame</param>
		/// <returns> A pair of vectors, for indices and intensities.</returns>
		std::pair<std::vector<size_t>, std::vector<int32_t>> GetScan(size_t scanIndex);

        /// <summary>
		/// Retrieve a scan (row) from the frame.
		/// </summary>
		/// <param name="scanIndex">0-based index of the scan within the frame</param>
		/// <param name="zeroPaddedReturns">Set to true if zeroes are to be added (for plotting convenience) to the beginning and end of nonzero intervals.</param>
		/// <returns> A pair of vectors, for indices and intensities.</returns>
        std::pair<std::vector<size_t>, std::vector<int32_t>> GetScan(size_t scanIndex, bool zeroPaddedReturns);

		/// <summary>
		/// Retrieve a Scan's ToF-based intensity as a sparse vector w/ accompanying sparse ToF indexes..
		/// </summary>
		/// <param name="scanIndex">0-based index of the Scan within the Frame.</param>
		/// <param name="tofIndex">A pointer to the vector of ToF index values to be populated.</param>
		/// <param name="intensity">A pointer to the vector of intensities to be populated.</param>
		void GetScanDataToFIndexedSparse(size_t scanIndex, std::vector<int64_t>* tofIndex, std::vector<size_t>* intensity);

		/// <summary>
		/// Retrieve a Scan's ToF-based intensity as a dense vector.
		/// </summary>
		/// <param name="scanIndex">0-based index of the Scan within the Frame.</param>
		/// <param name="intensity">THe dense vector of intensity samples.</param>
		void GetScanDataToFIndexedDense(size_t scanIndex, std::vector<size_t>* intensity);

		/// <summary>
		/// Alternative way to get metadata item.
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		std::string getFrameMetaDataItem(std::string key);

		/// <summary>
		/// Build a summed scan from all scans in the frame, and provide the minimum and maximum ToF
		/// indices and the scans that contained them.
		/// </summary>
		/// <param name="intensityOut">The vector (dense) of sample intensities summed across all scans in the frame.</param>
		/// <param name="minToFIndex">The minimum ToF index found.</param>
		/// <param name="minToFScanIndex">The scan index containing the minimum tof index.</param>
		/// <param name="maxToFIndex">The maximum ToF index found.</param>
		/// <param name="maxToFScanIndex">The scan index containing the maximum ToF index.</param>
		void GetScanSummationToFIndexedDense(std::vector<size_t>* intensityOut,
			int64_t * minToFIndex,
			size_t * minToFScanIndex,
			int64_t * maxToFIndex,
			size_t * maxToFScanIndex);

		/// <summary>
		/// Return the width of an arrival bin (scan/drift/AT).
		/// </summary>
		/// <returns>The bin width in milliseconds.</returns>
		double GetArrivalBinWidth();

		/// <summary>
		/// Give the arrival bin time offset for a given index.
		/// </summary>
		/// <param name="binIndex">The 0-based bin index.</param>
		/// <returns>The time offset, in milliseconds, of the start of the bin. -1.0 if an invalid bin index is passed in.</returns>
		double GetArrivalBinTimeOffset(size_t binIndex);

		/// <summary>
		/// Return the arrival bin index for the given time offset, or -1 if it's an invalid offset.
		/// </summary>
		/// <param name="timeOffset">Time offset into the frame, in milliseconds.</param>
		/// <returns>The bin which contains the time offset, or -1 if it's an invalid time offset.</returns>
		int GetArrivalBinIndex(double timeOffset);

		/// <summary>
		/// Return the lowest offset length statistics from all frames in the data file.
		/// </summary>
		/// <returns>The lowest value computed from all frames.</returns>
		int64_t GetToFOffset();

		/// <summary>
		/// Return the length of offset length statistics from all frames in the data file.
		/// </summary>
		/// <returns>The length of all value computed from all frames.</returns>
		int64_t GetToFLength();

		/// <summary>
		/// Return the lowest arrival bin offset statistics from all frames in the data file.
		/// </summary>
		/// <returns>The lowest value computed from all frames.</returns>
		size_t GetArrivalBinOffset();

		/// <summary>
		/// Return the length of arrival bin offset statistics from all frames in the data file.
		/// </summary>
		/// <returns>The length of all value computed from all frames.</returns>
		size_t GetArrivalBinLength();


		/// <summary>
		/// A single gathering point for all metadata reading
		/// </summary>
		/// <returns>A string of metadata</returns>
		std::string GetGenericMetaData(std::string strKey);

		/// <summary>
		/// A single gathering point for all metadata reading
		/// </summary>
		/// <returns>An int of metadata</returns>
		int GetGenericMetaDataInt(std::string strKey);

		/// <summary>
		/// A single gathering point for all metadata reading
		/// </summary>
		/// <returns>A double of metadata</returns>
		double GetGenericMetaDataDouble(std::string strKey);

		/// <summary>
		/// Return list of ATTic values for this frame
		/// </summary>
		/// <returns>None.</returns>
		std::shared_ptr<std::vector<int64_t>> GetATTicList();


		/// <summary>
		/// Return a specific value to a specific index of ATTic list for a given frame
		/// </summary>
		/// <returns>None.</returns>
		int64_t GetATTicItem(int nIndex);

		/// <summary>
		/// This method will be removed in a future version of MBI SDK.
		/// Return list of DATA_COUNT values for this frame
		/// </summary>
		/// <returns>None.</returns>
		[[deprecated("Use GetFrameDataAsCSRArray() or GetFrameDataAsCOOArray() to access frame data.")]]
		std::shared_ptr<std::vector<int32_t>> GetSampleIntensities();


		/// <summary>
		/// Return a specific value to a specific index of DATA_COUNT list for a given frame
		/// This method will be removed in a future version of MBI SDK.
		/// </summary>
		/// <returns>None.</returns>
		[[deprecated("Use GetFrameDataAsCSRArray() or GetFrameDataAsCOOArray() to access frame data.")]]
		int32_t GetDataCountItem(int nIndex);

		/// <summary>
		/// Return list of DATA_POSITIONS values for this frame
		/// This method will be removed in a future version of MBI SDK.
		/// </summary>
		/// <returns>None.</returns>
		[[deprecated("Use GetFrameDataAsCSRArray() or GetFrameDataAsCOOArray() to access frame data.")]]
		std::shared_ptr<std::vector<std::pair<int64_t, int64_t>>> GetGates();


		/// <summary>
		/// Return a specific value to a specific index of DATA_POSITIONS first list for a given frame
		/// This method will be removed in a future version of MBI SDK.
		/// </summary>
		/// <returns>None.</returns>
		[[deprecated("Use GetFrameDataAsCSRArray() or GetFrameDataAsCOOArray() to access frame data.")]]
		int64_t GetDataPositionItemFirst(int nIndex);


		/// <summary>
		/// Return a specific value to a specific index of DATA_POSITIONS second list for a given frame
		/// This method will be removed in a future version of MBI SDK.
		/// </summary>
		/// <returns>None.</returns>
		[[deprecated("Use GetFrameDataAsCSRArray() or GetFrameDataAsCOOArray() to access frame data.")]]
		int64_t GetDataPositionItemSecond(int nIndex);

		/// <summary>
		/// Return list of INDEX_COUNT values for this frame
		/// This method will be removed in a future version of MBI SDK.
		/// </summary>
		/// <returns>None.</returns>
		[[deprecated("Use GetFrameDataAsCSRArray() or GetFrameDataAsCOOArray() to access frame data.")]]
		std::shared_ptr<std::vector<int64_t>> GetSampleOffsets();


		/// <summary>
		/// Return a specific value to a specific index of INDEX_COUNT list for a given frame
		/// This method will be removed in a future version of MBI SDK.
		/// </summary>
		/// <returns>None.</returns>
		[[deprecated("Use GetFrameDataAsCSRArray() or GetFrameDataAsCOOArray() to access frame data.")]]
		int64_t GetIndexCountItem(int nIndex);

		/// <summary>
		/// Return list of INDEX_POSITION values for this frame
		/// This method will be removed in a future version of MBI SDK.
		/// </summary>
		/// <returns>None.</returns>
		[[deprecated("Use GetFrameDataAsCSRArray() or GetFrameDataAsCOOArray() to access frame data.")]]
		std::shared_ptr<std::vector<int64_t>> GetGateIndexOffsets();


		/// <summary>
		/// Return a specific value to a specific index of INDEX_POSITION list for a given frame
		/// This method will be removed in a future version of MBI SDK.
		/// </summary>
		/// <returns>None.</returns>
		[[deprecated("Use GetFrameDataAsCSRArray() or GetFrameDataAsCOOArray() to access frame data.")]]
		int64_t GetIndexPositionItem(int nIndex);

		/// <summary>
		/// Return list of trigger timestamps for this frame
		/// </summary>
		/// <returns>None.</returns>
		std::shared_ptr<std::vector<double>> GetTriggerTimeStamps();


		/// <summary>
		/// Return a specific value to a specific index of trigger timestamp list for a given frame
		/// </summary>
		/// <returns>Trigger Timestamp Item</returns>
		double GetTriggerTimeStampItem(int nIndex);

		/// <summary>
		/// retrieves local copy of frame metadata
		/// </summary>
		/// <returns>pointer to frame metadata.</returns>
		std::shared_ptr<FrameMetadata> GetFrameMetaData();

		/// @brief Examines frame metadata to determine collision energy for specific frame
		/// @returns Collision energy value
		/// @throws std::runtime_error if collision energy is not present
		double GetCollisionEnergy();

		/// <summary>
		/// Examines frame metadata to determine collision energy for specific frame is valid
		/// </summary>
		/// <returns>true if collision energy is not blank</returns>
		bool IsCollisionEnergyValid();

        /// @brief Does the frame have a fixed collision energy?
		/// @returns True if fixed, false if ramped or otherwise varied.
		bool HasFixedCE();
		
		/// @brief A non-throwing (safe) call for collision energy
		/// @returns Collision energy value
		double GetCE(int64_t scanIndex);

		///  @brief Return the details during an error to assist developers
		std::string GetErrorOutput();

	private:
		std::map<std::string, std::string> map_frame_all;
		bool is_loaded;
		double frame_dt_period;
		std::shared_ptr<FrameMetadata> frame_metadata_ptr;
		// sampleintensities is a sparse collection of all the samples in a frame
		std::shared_ptr<std::vector<int32_t>> sample_intensities;
		// This vector represents the non-zero scan samples with their
		// respective ToF indexes offset into the sample_intensities
		// int scanindex, pair<int sampleOffset, vector<int64_t> tofIndexes>
		std::shared_ptr<std::vector<std::shared_ptr<std::tuple<int64_t, int64_t, std::shared_ptr<std::vector<int64_t>>>>>> scan_table;

		// Returning to using the implementation-specific memory structures. A little
		// harder to follow the code, but this will make things faster.
		std::shared_ptr<std::vector<std::pair<int64_t, int64_t>>> gates;
		std::shared_ptr<std::vector<int64_t>> sample_offsets;
		std::shared_ptr<std::vector<int64_t>> gate_index_offsets;
		std::vector<size_t> non_zero_scan_indices;
		std::shared_ptr<std::vector<int64_t>> attic_list;
		std::shared_ptr<std::vector<double>> trigger_time_stamps_list;

		size_t numGatesInScan(size_t scanIndex);
		size_t numSamplesInScan(size_t scanIndex);

		void processOffsetLengthStats();
		bool processedOffsetLengthStats{ false };

		int64_t tof_offset;
		int64_t tof_length;
		size_t arrival_bin_offset;
		size_t arrival_bin_length;

		int hasValidCollisionEnergy;
		double fixedCE;

		MBISDK::MBIFile* pMbiFile;

		public:
		/// @brief Collision energy parsing error
		static constexpr const char* FRAME_CE_PARSE_ERROR_HEADER = "Cannot process collision energy for frame ";
		/// @brief Collision energy invalid data error
		static constexpr const char* FRAME_CE_PARSE_INVALID_DATA = ".  The frame metadata field frm-collision-energy is not a valid JSON structure in this file: ";
		/// @brief Collision energy frag data error
		static constexpr const char* FRAME_CE_PARSE_BAD_FRAG_DATA = ". The frame metadata field frm-frag-energy is not valid in this file: ";
		/// @brief Collision energy invalid data error
		static constexpr const char* FRAME_CE_PARSE_MISSING_DATA = ".  The frame metadata field frm-collision-energy is blank or missing in this file: ";

	};
}
}
