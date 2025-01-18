// MBIMetadata.h - Wrap global and frame metadata attributes.
/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * MBI Data Access API                                             *
 * Copyright 2021 MOBILion Systems, Inc. ALL RIGHTS RESERVED       *
 * Author: Greg Van Aken                                           *
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

#include <map>
#include <string>

namespace MBISDK
{
	/*! @class MBISDK::Metadata
	*   @brief Abstract interface for metadata from an MBI file.
	*   @author Greg Van Aken
	*/
	class MBI_DLLCPP Metadata
	{
	public:
		/// @brief Initialize an empty metadata object.
		Metadata();

		/// @brief Initialize metadata from a group.
		//Metadata(H5::Group group);
		Metadata(std::map<std::string, std::string>& map);

		/// @brief Initialize metadata from another metadata object.
		void Copy(Metadata& toCopy);

	    /// @brief Determine whether the metadata table has a value at the requested key.
        /// @param key a const char* key to lookup.
		bool HasKey(const char* key);

		/// @brief Read value at the requested key as a const char* (string).
		/// @param key a const char* key to lookup.
		const char* ReadString(const char* key);

		/// @brief Read a value at the requested key as a const char*.
		/// @param key a std::string key to lookup.
		const char* ReadString(std::string key);

		/// @brief Read value at the requested key as an integer.
		/// @param key a const char* key to lookup.
		const int ReadInt(const char* key);

		/// @brief Read value at the requested key as a double float.
		/// @param key a const char* key to lookup.
		const double ReadDouble(const char* key);

		/// @brief Read all metadata key, value pairs into cache.
		//void LoadAll();

		/// @brief Read all metadata from the file
		const std::map<std::string, std::string>& ReadAll();

		/// @brief Retrieve all cached metadata.
		const std::map<std::string, std::string>& GetCache();

		/// @brief Overwrite all cached metadata.
		void SetCache(const std::map<std::string, std::string>& toCopy);

		/// @brief Close file metadata
		void Close();

		~Metadata();

	private:
		//H5::Group group;
		std::map<std::string, std::string> cache;

	};

	/// @class MBISDK::GlobalMetadata
	/// @brief Metadata global to the MBI file.
	/// @author Greg Van Aken
	class MBI_DLLCPP GlobalMetadata : public Metadata
	{
		using Metadata::Metadata;
	};

	/// @class MBISDK::FrameMetadata
	/// @brief Metadata specific to a single frame.
	/// @author Greg Van Aken
	class MBI_DLLCPP FrameMetadata : public Metadata
	{
		using Metadata::Metadata;
	};

	/// @class MBISDK::FragmentationMetadata
	/// @brief Metadata pertinent to fragmentation
	/// @author Greg Van Aken
	class MBI_DLLCPP FragmentationMetadata
	{
	public:
		/// @brief The types of fragmentation data for a single frame
		/// @author Greg Van Aken

		/// @brief Fragmentation Type enumeration.
		enum class eType
		{
			/// @brief Fragmentation Metadata type None.
			NONE,		///< No fragmentation data present
			/// @brief Fragmentation Metadata type HILO.
			HILO		///< Alternating high CE, low CE frames
		};

		/// @brief Initialize a fragmentation metadata object.
		FragmentationMetadata();

		/// @brief Type instance
		eType type; ///< Type of fragmentation used to collect the data.
		/// @brief frag energy
		double frag_energy; ///< Fragmentation energy of the frame.
	};
}
