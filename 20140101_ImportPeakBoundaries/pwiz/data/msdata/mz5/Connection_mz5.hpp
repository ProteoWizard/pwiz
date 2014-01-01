//
// $Id$
//
//
// Original authors: Mathias Wilhelm <mw@wilhelmonline.com>
//                   Marc Kirchner <mail@marc-kirchner.de>
//
// Copyright 2011 Proteomics Center
//                Children's Hospital Boston, Boston, MA 02135
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

#ifndef CONNECTION_MZ5_HPP_
#define CONNECTION_MZ5_HPP_

#include "../MSData.hpp"
#include "Configuration_mz5.hpp"
#include <string>
#include <vector>

namespace pwiz {
namespace msdata {
namespace mz5 {

/**
 * This class is used for reading and writing information to a mz5 file.
 * On destruction it will automatically close all existing datasets and flush the file.
 */
class Connection_mz5
{
public:
    /**
     * mz5 file open policy.
     */
    enum OpenPolicy
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
    Connection_mz5(const std::string filename, const OpenPolicy op =
            ReadOnly, const Configuration_mz5 config =
            Configuration_mz5());

    /**
     * Closes all open datasets and flushes file to filesystem.
     */
    ~Connection_mz5();

    /**
     * Creates and write a one dimensional dataset.
     * @param size size of the dataset
     * @param data void pointer to the dataset beginning
     * @param v dataset enumeration value
     */
    void createAndWrite1DDataSet(hsize_t size, void* data,
            const Configuration_mz5::MZ5DataSets v);

    /**
     * Extends data of to a dataset. This method automatically uses an internal buffer to write the data and calls extendsAndWrite1DDataSet()
     * @param d1 data
     * @param v dataset enumeration value
     */
    void extendData(const std::vector<double>& d1,
            const Configuration_mz5::MZ5DataSets v);

    /**
     * Returns a list of all existing datasets in this file.
     */
    const std::map<Configuration_mz5::MZ5DataSets, size_t>& getFields();

    /**
     * Reads a dataset.
     * If no pointer is set, this method will allocate the needed mememory with calloc. To regain the memory call clean().
     *
     * @param v dataset enumeration value
     * @param dsend dataset size
     * @param ptr start pointer where the data should be written to.
     * @return pointer to the data
     */
    void* readDataSet(Configuration_mz5::MZ5DataSets v, size_t& dsend,
            void* ptr = 0);

    /**
     * Clean up of open datasets and destruction of corresponding data elements.
     * This method calls vlenReclaim, free and close.
     * @param v dataset enumeration value
     * @param data pointer to the beginning of the data
     * @param dsend length of the data.
     */
    void clean(const Configuration_mz5::MZ5DataSets v, void* data,
            const size_t dsend);

    /**
     * Gets data from a numerical dataset and writes it to the vector. This method is used to get mz,intensity and time.
     * @param data data vector
     * @param v dataset enumeration value
     * @param start start index
     * @param end end index
     */
    void getData(std::vector<double>& data,
            const Configuration_mz5::MZ5DataSets v, const hsize_t start,
            const hsize_t end);

    /**
     * Getter.
     * @return current configuration
     */
    const Configuration_mz5& getConfiguration();

private:

    /**
     * Creates a DSetCreatPropList for a dataset.
     * @param rank dimensionality of dataset
     * @param dataset dataset used to determine the name
     * @param datadim data dimensions
     * @return DSetCreatPropList
     */
    H5::DSetCreatPropList getCParm(int rank,
            const Configuration_mz5::MZ5DataSets& v, const hsize_t& datadim);

    /**
     * Creates a dataset.
     * @param rank dimensionality of the dataset
     * @param dim size of each dimension
     * @param maxdim maximal size of each dimension
     * @param v dataset enumeration value
     * @return dataset
     */
    H5::DataSet getDataSet(int rank, hsize_t* dim, hsize_t* maxdim,
            const Configuration_mz5::MZ5DataSets v);

    /**
     * Initializes a file and read internal mz5 information.
     *
     * Will throw a runtime_error when the file is not a mz5 file.
     */
    void readFile();

    /**
     * Extends and appends data to an existing dataset with no buffer.
     * @param dataset dataset
     * @param d1 data
     */
    void extendAndWrite1DDataSet(const H5::DataSet& dataset, const std::vector<
            double>& d1);

    /**
     * Internal method to add data to a buffer.
     * @param b buffer
     * @param d1 data
     * @param bs buffer size
     * @param dataset dataset
     */
    void addToBuffer(std::vector<double>& b, const std::vector<double>& d1,
            const size_t bs, const H5::DataSet& dataset);

    /**
     * Flushes all data to the hard drive.
     * @param v dataset enumeration value
     */
    void flush(const Configuration_mz5::MZ5DataSets v);

    /**
     * Closes the file and flushes all open buffers/datasets.
     */
    void close();

    /**
     * Existing field in file.
     */
    std::map<Configuration_mz5::MZ5DataSets, size_t> fields_;
    /**
     * MZ5 file reference.
     */
    H5::H5File* file_;
    /**
     * mz5 configuration object.
     */
    mutable Configuration_mz5 config_;
    /**
     * Mapping from a dataset enumeration value to the dataset.
     */
    std::map<Configuration_mz5::MZ5DataSets, H5::DataSet> bufferMap_;
    /**
     * Mapping from a dataset enumeration value to a buffer.
     */
    std::map<Configuration_mz5::MZ5DataSets, std::vector<double> > buffers_;
    /**
     * Flag whether file is closed or not.
     */
    bool closed_;
};

}
}
}

#endif /* CONNECTION_MZ5_HPP_ */
