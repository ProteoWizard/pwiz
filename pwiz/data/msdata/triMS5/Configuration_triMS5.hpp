//
// $Id$
//
//
// Original author: Jennifer Leclaire <leclaire@uni-mainz.de>
//
// Copyright 2019 Institute of Computer Science, Johannes Gutenberg-Universität Mainz
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
#ifndef _Configuration_triMS5_HPP_
#define _Configuration_triMS5_HPP_

#include "../mz5/Configuration_mz5.hpp"


#include "../MSDataFile.hpp"
#include <string>

namespace pwiz {
namespace msdata {
namespace triMS5 {

	using namespace pwiz::msdata::mz5;


	enum class GroupType_triMS5
	{
		Root,

		MetaData,

		RawData,

		Cluster,

		Chromatogram,

		Spectrum

	};


	//  /**
	//   * Enumeration to simplify the use of datasets. These values are used to determine dataset specific parameters, such as chunk size, buffer size, name and type.
	//  */
	enum class DataSetType_triMS5
	{
		//contains all mz5 data sets:

		/**
		* Dataset for the controlled vocabulary sets.
		*/
		ControlledVocabulary,
		/**
		* File content dataset.
		*/
		FileContent,
		/**
		* Dataset containing contact infomation.
		*/
		Contact,
		/**
		* Dataset containing all used controlled vocabulary accessions (prefix, accession, definition).
		*/
		CVReference,
		/**
		* Dataset containing all controlled vocabulary parameters.
		*/
		CVParam,
		/**
		* Dataset containing all user parameters.
		*/
		UserParam,
		/**
		* Dataset containing all referenced parameter groups.
		*/
		RefParam,
		/**
		* Dataset for parameter groups.
		*/
		ParamGroups,
		/**
		* Source file dataset.
		*/
		SourceFiles,
		/**
		* Sample datatset.
		*/
		Samples,
		/**
		* Software dataset.
		*/
		Software,
		/**
		* Scan setting datatset.
		*/
		ScanSetting,
		/**
		* Instrument configuration datatset.
		*/
		InstrumentConfiguration,
		/**
		* Data processing dataset.
		*/
		DataProcessing,
		/**
		* Dataset containing all meta information for all runs.
		*/
		Run,
		/**
		* Dataset containing all meta information of all spectra.
		*/
		SpectrumMetaData,
		/**
		* Dataset containing all meta information of all binary data elements for spectra.
		*/
		SpectrumBinaryMetaData,
		/**
		* Index dataset. kth element points to the end of the kth spectrum in MZ and SIntensity.
		*/
		SpectrumIndex,

		/**
		* Dataset containing all intensity values for all spectra.
		*/
		SpectrumIntensity,

	
		/**
		* Dataset containing all meta information of all chromatograms.
		*/
		ChromatogramMetaData,
		/**
		* Dataset containing all meta information of all binary data elements for chromatograms.
		*/
		ChromatogramBinaryMetaData,
		/**
		* Index dataset. kth element points to the end of the kth chromatogram in Time and Intensity.
		*/
		ChromatogramIndex,
		/**
		* Dataset containing all time values.
		*/
		ChromatogramTime,
		/**
		* Dataset containing all chromatogram intensities.
		*/
		ChromatogramIntensity,
		/**
		* Dataset containing information about the file and specific dataset configurations.
		*/
		FileInformation,

		//triMS5  specific datesets

		/**
		* Dataset holding the computed mass axis (with all mass values of the spectra).
		*/
		SpectrumMassAxis,

		/**
		* Dataset holding the mass indices for one cluster.
		*/
		SpectrumMassIndices,

		/**
		*Dataset mapping global spectrum indices to cluster specific indices.
		*/
		SpectrumListIndices

	};

/**
 * Configuration class for triMS5 im- and export.
 * This class is derived from Configuration_mz5 and holds just the additioanl dsets and groups used in triMS5
 */
class Configuration_triMS5 final
{
public:
	static unsigned short triMS5_FILE_MAJOR_VERSION;
	static unsigned short triMS5_FILE_MINOR_VERSION;
    /**
     * Default constructor.
     * Initializes default parameters.
     */
    Configuration_triMS5();

    /**
     * Copy constructor.
     */
	Configuration_triMS5(const Configuration_triMS5&);

    /**
     * Conversion constructor for WriteConfig objects.
     * Uses values in config to set up specific options such as compression or precision.
     * @param config a pwiz config object
     */
	Configuration_triMS5(const pwiz::msdata::MSDataFile::WriteConfig& config);

    /**
     * Assign operator.
     * @param rhs right hand side of assign operator.
     * @return the altered object
     */
	Configuration_triMS5& operator=(const Configuration_triMS5& rhs);

    /**
     * Returns dataset name for a requested dataset.
     * @param v dataset
     * @return dataset name
     */
    const std::string& getNameFor(const DataSetType_triMS5 v);


    /**
     * Returns dataset type.
     * @param v dataset
     * @return mz5 data type reference
     */
    const H5::DataType& getDataTypeFor(const DataSetType_triMS5 v);

    /**
     * Returns chunk size for a dataset.
     * @param v dataset
     * @return chunk size. EMPTY_CHUNK_SIZE if no chunking should be used.
     */
    const hsize_t& getChunkSizeFor(const DataSetType_triMS5 v);


	/**
	* Returns chunk size for a dataset.
	* @param v dataset
	* @return chunk size. EMPTY_CHUNK_SIZE if no chunking should be used.
	*/
	const hsize_t& getBufferSizeFor(const DataSetType_triMS5 v);


	/**
	* Returns the group in which the dataset is located
	* @param v dataset
	* @return grouptype. 
	*/
	GroupType_triMS5 getGroupTypeFor(const DataSetType_triMS5 v);

	/**
	*Returns the group in which the group is located
	* @param v group to query
	* @return grouptype. GroupType_triMS5::Root is located at GroupType_triMS5::Root
	*/
	GroupType_triMS5 getGroupTypeFor(const GroupType_triMS5 v);

	/**
	* Returns dataset name for a requested dataset.
	* @param v dataset
	* @return dataset name
	*/
	const std::string& getNameFor(const GroupType_triMS5 v);

	/**
	* Returns the group type for a given group name.
	* @param v dataset
	* @return dataset name
	*/
	GroupType_triMS5 getGroupTypeFor(const std::string& name);

	/**
	* Returns DataSetType_triMS5 for a given dataset name
	* @param name name of the dataset
	* @return DataSetType_triMS5 the type
	*/
	DataSetType_triMS5 getDataSetTypeFor(const std::string& name);


	/**
	* Getter.
	* @return current configuration
	*/
	mz5::Configuration_mz5& getMZ5Configuration() { return config_mz5_; }

	/**
	* Returns mz5 cache in Mb.
	* The mz5 cache is used for chunked datasets. This effects the random read time.
	* @return mz5 cache size
	*/
	const size_t& getBufferInMB(){	return config_mz5_.getBufferInMb();	}

	/**
	* Returns mz5 cache in byte.
	* See getBufferInMb()
	* @return mz5 cache in byte
	*/
	const size_t getBufferInB() { return config_mz5_.getBufferInB(); }

	/**
	* Returns number of used rdcc slots.
	* This is currently constant 41957L, but should be the the next prime after 10-100 times the number of chunks fitting into the cache.
	* @return number of rdcc slots
	*/
	const size_t& getRdccSlots() { return config_mz5_.getRdccSlots(); }

private:
    /**
     * Initializes configuration object.
     */
    void init();

	/**
	* Mapping from DataSetType to name
	*/
	std::map<DataSetType_triMS5, std::string> variableNames_;

	/**
	* Mapping from name to DataSetType
	*/
	std::map<std::string, DataSetType_triMS5> namesVariable_;


	/**
	* Map which holds the data types for a dataset.
	*/
	std::map<DataSetType_triMS5, H5::DataType> variableTypes_;

	/**
	* Map which holds the chunk size for a dataset.
	*/
	std::map<DataSetType_triMS5, hsize_t> variableChunkSizes_;
	/**
	* Map which holds the buffer size for a dataset.
	*/
	std::map<DataSetType_triMS5, size_t> variableBufferSizes_;

	std::map<DataSetType_triMS5, GroupType_triMS5> variableToGroup_;

	/**
	* Map which holds the names for each Group.
	*/
	std::map<GroupType_triMS5, std::string> groupsNames_;

	/**
	* Mapping from the name to the group
	*/
	std::map<std::string, GroupType_triMS5> namesGroups_;


	/**
	*Map which holds the localization of each group.
	*/
	std::map<GroupType_triMS5, GroupType_triMS5> groupsToGroups_;


	mz5::Configuration_mz5 config_mz5_;
};



}
}
}

#endif /*_Configuration_triMS5_HPP_*/
