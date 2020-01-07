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

#ifndef _Connection_triMS5_HPP_
#define _Connection_triMS5_HPP_

#include "../MSData.hpp"
#include "../mz5/Connection_mz5.hpp"
#include "Configuration_triMS5.hpp"
#include <string>
#include <vector>
#include <memory>
#include "boost/thread/mutex.hpp"
namespace { boost::mutex connectionReadMutex_, connectionWriteMutex_; }
namespace pwiz {
namespace msdata {
namespace triMS5 {

//useful typdefs for handling presetScanConfigIndices with associated H5::DataSets
using DataSetIdPair = std::pair<DataSetType_triMS5, int>;
using DataSetIdPairToSizeMap = std::map<DataSetIdPair, size_t>;

/**
 * This class is used for reading and writing information to a triMS5 file.
 * On destruction it will automatically close all existing datasets and flush the file.
 */
class Connection_triMS5 final : public pwiz::msdata::mz5::Connection_HDF5
{
public:
	/**
	*triMS5's file open policy (same as mz5).
	*/
	enum class OpenPolicy
	{
		/**
		* Fails if the file already exists.
		*/
		FailIfFileExists,
		/**
		* If file exists, it will be deleted and created empty.
		*/
		RemoveAndCreate,
		/**
		* Open file with read and write support.
		*/
		ReadWrite,
		/**
		* Open file with read only support.
		*/
		ReadOnly
	};

	
      /**
     * Default constructor.
     *
     * When opening a file, which is a hdf5 file but no mz5 file, a runtime_error is thrown.
     *
     * @param filename file name
     * @param op open policy
     * @param configuration configuration used to determine chunk and buffer sizes
     */
	Connection_triMS5(const std::string filename, const OpenPolicy op = OpenPolicy::ReadOnly, const Configuration_triMS5 config = Configuration_triMS5());
	

	H5::Group openGroup(const GroupType_triMS5& v, int presetScanConfiguration = -1);

	template <typename T>
	void writeAttributesToGroup(GroupType_triMS5 v, int presetScanConfiguration, const H5::DataType& datatype, const std::string& attName, T data)
	{
		H5::Group group = openGroup(v, presetScanConfiguration);

		hsize_t dims[1] = { 1 };
		H5::DataSpace attr_dataspace = H5::DataSpace(1, dims);

		// Create a dataset attribute. 
		H5::Attribute att = group.createAttribute(attName, datatype, attr_dataspace);
		// Write the attribute data. 
		att.write(datatype, &data);
		att.close();

		group.close();
	}

	/**
	* writes all groups and subgroups according to the number of preset scan configuration indices 
	* @param numberOfPresetScanConfigurations the number of distinct scans
	*/
	void writeFileStructure(int numberOfPresetScanConfigurations);
    /**
     * Closes all open datasets and flushes file to filesystem.
     */
    ~ Connection_triMS5();


    /**
     * Creates and write a one dimensional dataset.
     * @param size size of the dataset
     * @param data void pointer to the dataset beginning
     * @param v dataset enumeration value
     */
    void createAndWrite1DDataSet(hsize_t size, void* data, const DataSetType_triMS5& v, int presetScanConfiguration);




    /**
     * Extends data to a dataset. This method automatically uses an internal buffer to write the data and calls extendsAndWrite1DDataSet()
     * @param d1 data
     * @param v dataset enumeration value
     */
	void extendData(const std::vector<double>& d1, const DataSetType_triMS5& v, int presetScanConfigurationIndex);



	/**
	 * Returns a map of all existing datasets in this file.
	 */
	const DataSetIdPairToSizeMap& getAvailableDataSets() { return availDataSets_;}

	/**
	* Reading from a data set.
	* @param v the dataset identifiert
	* @param dsend size of data set
	* @param presetScanConfigurationIndex index of the scan to be read
	* @param ptr buffer to be filled with data
	*/
	void * readDataSet(const DataSetType_triMS5 v, size_t& dsend, int presetScanConfigurationIndex, void* ptr = 0);

	/**
	* Cleans buffers (of variable length data sets)
	* @param v closes data set
	* @param buffer buffer to be cleaned
	* @param dsend size of buffer
	*/
	void clean(const DataSetType_triMS5 v, void * buffer, const size_t dsend);

	/**
	* Gets data from a numerical dataset and writes it to the vector. This method is used to get mz,intensity and time.
	* @param data data vector
	* @param v dataset enumeration value
	* @param start start index
	* @param end end index
	*/
	template <typename T>
	void getData(std::vector<T>& buffer, const DataSetType_triMS5 v, int presetScanConfigurationIndex, const hsize_t start, const hsize_t end)
	{
		boost::mutex::scoped_lock lock(connectionReadMutex_);

		hsize_t scount = end - start;

		if (scount > 0)
		{
			//first check if there is any dataset with the correct name
			auto avail_it = availDataSets_.find({ v, presetScanConfigurationIndex });
			// if it does not exist in the file or if there is a dataset with this name but it is not the correct scan
			if (avail_it == availDataSets_.end())
			{
				throw std::out_of_range("[Connection_triMS5]::getData() : Data Set not found");
			}

			//if the correct dataset was found in the file, check if it is already opened in memory

			auto it = dsets_.find({ v, presetScanConfigurationIndex });
			//if it is not open, open it up and put it in dsets_ list 
			if (it == dsets_.end())
			{
				H5::Group group = openGroup(config_.getGroupTypeFor(v), presetScanConfigurationIndex);
				H5::DataSet ds = group.openDataSet(config_.getNameFor(v));

				dsets_[{ v, presetScanConfigurationIndex }] = ds;
				it = dsets_.find({ v, presetScanConfigurationIndex });
				group.close();
			}

			//now the dataset is in the list
			if (it != dsets_.end())
			{
				buffer.resize(scount);
				H5::DataSet dataset = it->second;
				H5::DataSpace dataspace = dataset.getSpace();
				hsize_t offset[1];
				offset[0] = start;
				hsize_t count[1];
				count[0] = scount;
				dataspace.selectHyperslab(H5S_SELECT_SET, count, offset);

				hsize_t dimsm[1];
				dimsm[0] = scount;
				H5::DataSpace memspace(1, dimsm);

				dataset.read(buffer.data(), config_.getDataTypeFor(v), memspace, dataspace);

				memspace.close();
				dataspace.close();
			}
		}
	}

    /**
     * Getter.
     * @return current configuration
     */
     const Configuration_triMS5& getConfiguration(){ return config_;  }

	 /**
	 * Getter inherited from Connection_HDF5; 
	 * @return file version of mz5  
	 */
	 const FileInformationMZ5& getFileInformation() const;

private:
	/**
	* Method creating a group
	* @param v Group to be created
	* @param name name of the group
	* @param presetScanConfiguration index of the scan if group is scan specific
	*/
	void createAndWriteGroup(const GroupType_triMS5& v, const std::string& name, int presetScanConfiguration);

	/**
	* Reads all elements of the group
	* @param g group to be read
	* @param group_type type of the group to be read
	* @param level regarding preset scan indices
	*/
	void readElementsInGroup(const H5::Group& g, const GroupType_triMS5& group_type, int level);

    /**
     * Creates a DSetCreatPropList for a dataset.
     * @param rank dimensionality of dataset
     * @param dataset dataset used to determine the name
     * @param datadim data dimensions
     * @return DSetCreatPropList
     */
    H5::DSetCreatPropList getCParm(int rank, const DataSetType_triMS5& v, const hsize_t& datadim);

    /**
     * Creates a dataset.
     * @param rank dimensionality of the dataset
     * @param dim size of each dimension
     * @param maxdim maximal size of each dimension
     * @param v dataset enumeration value
	 * @param presetScanConfiguration the one-based scan index 
     * @return dataset
     */
    H5::DataSet createDataSet(H5::Group father, int rank, hsize_t* dim, hsize_t* maxdim, const DataSetType_triMS5& v, int presetScanConfiguration);

    /**
     * Initializes a file and read internal triMS5 information.
     * Will throw a runtime_error when the file is not a triMS5 file.
     */
    void readFile();

    /**
     * Extends and appends data to an existing dataset with no buffer.
     * @param dataset dataset
     * @param d1 data
     */
	void extendAndWrite1DDataSet(const H5::DataSet& dataset, const std::vector<double>& d1);
    /**
     * Internal method to add data to a buffer.
     * @param b buffer
     * @param d1 data
     * @param bs buffer size
     * @param dataset dataset
     */
    void addToBuffer(std::vector<double>& b, const std::vector<double>& d1, const size_t bs, const H5::DataSet& dataset);

    /**
     * Flushes all data to the hard drive.
     * @param id dataset id value
     */
    void flush(const DataSetType_triMS5& v, int presetScanConfigurationIndex);

    /**
     * Closes the file and flushes all open buffers/datasets.
     */
    void close();
    /**
     * triMS5  configuration object.
     */
    mutable Configuration_triMS5 config_;

	/**
	* triMS5 file reference.
	*/
	std::unique_ptr<H5::H5File> file_;

	/**
	* Mapping from an 
	*/
	DataSetIdPairToSizeMap availDataSets_;

	/**
	* Mapping from an identifier to the corresponding DataSet, which may already be open.
	*/
	std::map<DataSetIdPair, H5::DataSet> dsets_;

	/**
	* Mapping from an identifier of a DataSet to the corresponding buffer (used during writing).
	*/
	std::map<DataSetIdPair, std::vector<double> > buffers_;

    /**
     * Flag whether file is closed or not.
     */
	bool closed_;

	/**
	* File information required for creation of spectra and chromatogram
	*/
	FileInformationMZ5 fileInfo_;

};

}
}
}

#endif /* _Connection_triMS5_HPP_ */
