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

#ifndef CONFIGURATION_MZ5_HPP_
#define CONFIGURATION_MZ5_HPP_

#include "Datastructures_mz5.hpp"
#include "../MSDataFile.hpp"
#include <string>

namespace pwiz {
namespace msdata {
namespace mz5 {

/**
 * Configuration class for mz5 im- and export.
 * This class is holding several different configuration options, such as dataset names, dataset types and different buffering and chunking values.
 */
class Configuration_mz5
{
public:

    static unsigned short MZ5_FILE_MAJOR_VERSION;
    static unsigned short MZ5_FILE_MINOR_VERSION;
    static bool PRINT_HDF5_EXCEPTIONS;

    /**
     * Enumeration for different load strategies for spectra.
     */
    enum SpectrumLoadPolicy
    {
        /**
         * Initializes all meta information of all spectra at the creation a an spectrum list obeject.
         */
        SLP_InitializeAllOnCreation,
        /**
         * Initialzes all meta information of all spectra at the first getSpectrum() call.
         */
        SLP_InitializeAllOnFirstCall
    //SLP_PreemptionMode not implemented yet
    //SLP_OnDemand not implemented yet
    //SLP_CachedOnDemand not implemented yet
    };

    enum ChromatogramLoadPolicy {
        /**
         * Initialize and keep all chromatograms when initializing the chromatogram list.
         */
        CLP_InitializeAllOnCreation,
        /**
         * Initialize and keep all chromatograms when requesting the first chromatogram.
         */
        CLP_InitializeAllOnFirstCall
    //CLP_PreemptionMode not implemented yet
    //CLP_OnDemand not implemented yet
    //CLP_CachedOnDemand not implemented yet
    };

    /**
     * Enumeration to simplify the use of datasets. These values are used to determine dataset specific parameters, such as chunk size, buffer size, name and type.
     */
    enum MZ5DataSets
    {
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
         * Dataset containing all mz values for all spectra.
         */
        SpectrumMZ,
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
         * Index dataset. kth element points to the end of the kth chromatogram in Time and CIntensity.
         */
        ChromatogramIndex,
        /**
         * Dataset containing all time values.
         */
        ChomatogramTime,
        /**
         * Dataset containing all chromatogram intensities.
         */
        ChromatogramIntensity,
        /**
         * Dataset containing information about the file and specific dataset configurations.
         */
        FileInformation
    };

    /**
     * Default constructor.
     * Initializes default parameters.
     */
    Configuration_mz5();

    /**
     * Copy constructor.
     */
    Configuration_mz5(const Configuration_mz5&);

    /**
     * Conversion constructor for WriteConfig objects.
     * Uses values in config to set up specific options such as compression or precision.
     * @param config a pwiz config object
     */
    Configuration_mz5(const pwiz::msdata::MSDataFile::WriteConfig& config);

    /**
     * Assign operator.
     * @param rhs right hand side of assign operator.
     * @return the altered object
     */
    Configuration_mz5& operator=(const Configuration_mz5& rhs);

    /**
     * Returns dataset name for a requested dataset.
     * @param v dataset
     * @return dataset name
     */
    const std::string& getNameFor(const MZ5DataSets v);

    /**
     * Returns dataset enumeration value for a given string.
     * Returns out_of_range if string does not exist.
     * @param name dataset name
     * @return dataset enumeration value
     */
    MZ5DataSets getVariableFor(const std::string& name);

    /**
     * Returns dataset type.
     * @param v dataset
     * @return mz5 data type reference
     */
    const H5::DataType& getDataTypeFor(const MZ5DataSets v);

    /**
     * Returns chunk size for a dataset.
     * @param v dataset
     * @return chunk size. EMPTY_CHUNK_SIZE if no chunking should be used.
     */
    const hsize_t& getChunkSizeFor(const MZ5DataSets v);

    /**
     * Returns buffer size for a dataset.
     * Buffers are mainly used to speed up writing of mz and spectrum intensities.
     * @param v dataset
     * @return buffer size. NO_BUFFER_SIZE if no buffer will be used.
     */
    const size_t& getBufferSizeFor(const MZ5DataSets v);

    /**
     * Returns mz5 cache in Mb.
     * The mz5 cache is used for chunked datasets. This effects the random read time.
     * @return mz5 cache size
     */
    const size_t& getBufferInMb();

    /**
     * Returns mz5 cache in byte.
     * See getBufferInMb()
     * @return mz5 cache in byte
     */
    const size_t getBufferInB();

    /**
     * Returns number of used rdcc slots.
     * This is currently constant 41957L, but should be the the next prime after 10-100 times the number of chunks fitting into the cache.
     * @return number of rdcc slots
     */
    const size_t& getRdccSlots();

    /**
     * Getter for spectrum load policy.
     * @return spectrum load policy
     */
    const SpectrumLoadPolicy& getSpectrumLoadPolicy() const;

    /**
     * Getter for chromatogram load policy.
     * @return spectrum load policy
     */
    const ChromatogramLoadPolicy& getChromatogramLoadPolicy() const;

    /**
     * Getter for translation flag.
     * If this flag is set, mz values of mass spectra are saved as delta mz's. This greatly improves compression rate and significantly reduces file size.
     * @return true of tranlating is enabled, otherwise false.
     */
    const bool doTranslating() const;

    /**
     * Setter for translation flag.
     * @param flag true of translation of mz and intensity values is be enabled;
     */
    void setTranslating(const bool flag) const;

    /**
     * Getter for compression level. Default is 1 if compression is enabled, since the gain in compression rate of >2 is negligible.
     * @return value between 0-9(0=no compression, 1=fast compression, 9=high compression)
     */
    const int getDeflateLvl();

    /**
     * Getter for shuffle flag.
     * Shuffle greatly increases compression rate.
     * @return true if shuffel is enabled, otherwise false.
     */
    const bool doShuffel();

    /**
     * Value for empty chunk size. Should be 0.
     */
    static hsize_t EMPTY_CHUNK_SIZE;
    /**
     * Default value to use no buffer. Should be 0.
     */
    static size_t NO_BUFFER_SIZE;

private:
    /**
     * Initializes configuration object.
     */
    void init(const bool deltamz, const bool translateinten);

    /**
     * Internal copy of pwiz configuration object.
     */
    mutable pwiz::msdata::MSDataFile::WriteConfig config_;
    /**
     * Map which holds the translation of datasets to dataset names.
     */
    std::map<MZ5DataSets, std::string> variableNames_;
    /**
     * Map which holds the translation of dataset names to datasets.
     */
    std::map<std::string, MZ5DataSets> variableVariables_;
    /**
     * Map which holds the data types for a dataset.
     */
    std::map<MZ5DataSets, H5::DataType> variableTypes_;
    /**
     * Map which holds the chunk size for a dataset.
     */
    std::map<MZ5DataSets, hsize_t> variableChunkSizes_;
    /**
     * Map which holds the buffer size for a dataset.
     */
    std::map<MZ5DataSets, size_t> variableBufferSizes_;
    /**
     * MZ5 cache in MB
     */
    size_t bufferInMB_;
    /**
     * Number of rdcc slots.
     */
    size_t rdccSolts_;
    /**
     * Spectrum load policy
     */
    SpectrumLoadPolicy spectrumLoadPolicy_;
    /**
     * Chromaogram load policy
     */
    ChromatogramLoadPolicy chromatogramLoadPolicy_;
    /**
     * flag for translation.
     */
    mutable bool doTranslating_;
    /**
     * Compression level. If compression level is > 0, shuffle is enable by default.
     */
    int deflateLvl_;
};

}
}
}

#endif /* CONFIGURATION_MZ5_HPP_ */
