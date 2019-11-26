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

#include "Configuration_mz5.hpp"
#include "../BinaryDataEncoder.hpp"

namespace pwiz {
namespace msdata {
namespace mz5 {

using namespace H5;

hsize_t Configuration_mz5::EMPTY_CHUNK_SIZE = 0;
size_t Configuration_mz5::NO_BUFFER_SIZE = 0;
unsigned short Configuration_mz5::MZ5_FILE_MAJOR_VERSION = 0;
unsigned short Configuration_mz5::MZ5_FILE_MINOR_VERSION = 10;

bool Configuration_mz5::PRINT_HDF5_EXCEPTIONS = false;

Configuration_mz5::Configuration_mz5()
{
    config_.binaryDataEncoderConfig.precision
            = pwiz::msdata::BinaryDataEncoder::Precision_64;
    config_.binaryDataEncoderConfig.compression
            = pwiz::msdata::BinaryDataEncoder::Compression_Zlib;
    init(true, true);
}

Configuration_mz5::Configuration_mz5(const Configuration_mz5& config)
{
    config_ = config.config_;
    init(config.doTranslating(), config.doTranslating());
}

Configuration_mz5::Configuration_mz5(
        const pwiz::msdata::MSDataFile::WriteConfig& config)
{
    config_ = config;
    init(true, true);
}

Configuration_mz5& Configuration_mz5::operator=(const Configuration_mz5& rhs)
{
    if (this != &rhs)
    {
        this->config_ = rhs.config_;
        init(rhs.doTranslating(), rhs.doTranslating());
    }
    return *this;
}

void Configuration_mz5::init(const bool deltamz,
        const bool translateinten)
{
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(
            ControlledVocabulary, "ControlledVocabulary"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(CVReference,
            "CVReference"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(CVParam,
            "CVParam"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(UserParam,
            "UserParam"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(RefParam,
            "RefParam"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(FileContent,
            "FileContent"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(Contact,
            "Contact"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(ParamGroups,
            "ParamGroups"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(SourceFiles,
            "SourceFiles"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(Samples,
            "Samples"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(Software,
            "Software"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(ScanSetting,
            "ScanSetting"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(
            InstrumentConfiguration, "InstrumentConfiguration"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(DataProcessing,
            "DataProcessing"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(Run, "Run"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(SpectrumMetaData,
            "SpectrumMetaData"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(
            SpectrumBinaryMetaData, "SpectrumListBinaryData"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(
            ChromatogramMetaData, "ChromatogramList"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(
            ChromatogramBinaryMetaData, "ChromatogramListBinaryData"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(
            ChromatogramIndex, "ChromatogramIndex"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(SpectrumIndex,
            "SpectrumIndex"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(SpectrumMZ,
            "SpectrumMZ"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(
            SpectrumIntensity, "SpectrumIntensity"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(ChomatogramTime,
            "ChomatogramTime"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(
            ChromatogramIntensity, "ChromatogramIntensity"));
    variableNames_.insert(std::pair<MZ5DataSets, std::string>(FileInformation,
            "FileInformation"));

    for (std::map<MZ5DataSets, std::string>::iterator it =
            variableNames_.begin(); it != variableNames_.end(); ++it)
    {
        variableVariables_.insert(std::pair<std::string, MZ5DataSets>(
                it->second, it->first));
    }

    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(
            ControlledVocabulary, ContVocabMZ5::getType()));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(FileContent,
            ParamListMZ5::getType()));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(Contact,
            ParamListMZ5::getType()));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(CVReference,
            CVRefMZ5::getType()));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(ParamGroups,
            ParamGroupMZ5::getType()));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(SourceFiles,
            SourceFileMZ5::getType()));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(Samples,
            SampleMZ5::getType()));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(Software,
            SoftwareMZ5::getType()));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(ScanSetting,
            ScanSettingMZ5::getType()));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(
            InstrumentConfiguration, InstrumentConfigurationMZ5::getType()));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(DataProcessing,
            DataProcessingMZ5::getType()));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(Run,
            RunMZ5::getType()));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(SpectrumMetaData,
            SpectrumMZ5::getType()));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(
            SpectrumBinaryMetaData, BinaryDataMZ5::getType()));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(
            ChromatogramMetaData, ChromatogramMZ5::getType()));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(
            ChromatogramBinaryMetaData, BinaryDataMZ5::getType()));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(ChromatogramIndex,
            PredType::NATIVE_ULONG));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(SpectrumIndex,
            PredType::NATIVE_ULONG));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(FileInformation,
            FileInformationMZ5::getType()));

    pwiz::msdata::BinaryDataEncoder::Precision mzprec =
            config_.binaryDataEncoderConfig.precision;
    std::map<CVID, pwiz::msdata::BinaryDataEncoder::Precision>::const_iterator
            overrideItrmz =
                    config_.binaryDataEncoderConfig.precisionOverrides.find(
                            pwiz::msdata::MS_m_z_array);
    if (overrideItrmz
            != config_.binaryDataEncoderConfig.precisionOverrides.end())
        mzprec = overrideItrmz->second;

    pwiz::msdata::BinaryDataEncoder::Precision intenprec =
            config_.binaryDataEncoderConfig.precision;
    std::map<CVID, pwiz::msdata::BinaryDataEncoder::Precision>::const_iterator
            overrideItrinten =
                    config_.binaryDataEncoderConfig.precisionOverrides.find(
                            pwiz::msdata::MS_intensity_array);
    if (overrideItrinten
            != config_.binaryDataEncoderConfig.precisionOverrides.end())
        intenprec = overrideItrinten->second;

    if (mzprec == pwiz::msdata::BinaryDataEncoder::Precision_64)
    {
        variableTypes_.insert(std::pair<MZ5DataSets, DataType>(SpectrumMZ,
                PredType::NATIVE_DOUBLE));
    }
    else if (mzprec == pwiz::msdata::BinaryDataEncoder::Precision_32)
    {
        variableTypes_.insert(std::pair<MZ5DataSets, DataType>(SpectrumMZ,
                PredType::NATIVE_FLOAT));
    }
    else
    {
        throw std::runtime_error(
                "[Configuration_mz5::init()] Unknown mz precision flag.");
    }
    if (intenprec == pwiz::msdata::BinaryDataEncoder::Precision_64)
    {
        variableTypes_.insert(std::pair<MZ5DataSets, DataType>(
                SpectrumIntensity, PredType::NATIVE_DOUBLE));
        variableTypes_.insert(std::pair<MZ5DataSets, DataType>(
                ChromatogramIntensity, PredType::NATIVE_DOUBLE));
    }
    else if (intenprec == pwiz::msdata::BinaryDataEncoder::Precision_32)
    {
        variableTypes_.insert(std::pair<MZ5DataSets, DataType>(
                SpectrumIntensity, PredType::NATIVE_FLOAT));
        variableTypes_.insert(std::pair<MZ5DataSets, DataType>(
                ChromatogramIntensity, PredType::NATIVE_FLOAT));
    }
    else
    {
        throw std::runtime_error(
                "[Configuration_mz5::init()] Unknown intensity precision flag.");
    }

    pwiz::msdata::BinaryDataEncoder::Precision timeprec =
            config_.binaryDataEncoderConfig.precision;
    std::map<CVID, pwiz::msdata::BinaryDataEncoder::Precision>::const_iterator
            overrideItrtime =
                    config_.binaryDataEncoderConfig.precisionOverrides.find(
                            pwiz::msdata::MS_time_array);
    if (overrideItrtime
            != config_.binaryDataEncoderConfig.precisionOverrides.end())
        timeprec = overrideItrtime->second;

    if (timeprec == pwiz::msdata::BinaryDataEncoder::Precision_64)
    {
        variableTypes_.insert(std::pair<MZ5DataSets, DataType>(ChomatogramTime,
                PredType::NATIVE_DOUBLE));
    }
    else if (timeprec == pwiz::msdata::BinaryDataEncoder::Precision_32)
    {
        variableTypes_.insert(std::pair<MZ5DataSets, DataType>(ChomatogramTime,
                PredType::NATIVE_FLOAT));
    }
    else
    {
        throw std::runtime_error(
                "[Configuration_mz5::init()] Unknown chromatogram time precision flag.");
    }

    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(CVParam,
            CVParamMZ5::getType()));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(UserParam,
            UserParamMZ5::getType()));
    variableTypes_.insert(std::pair<MZ5DataSets, DataType>(RefParam,
            RefMZ5::getType()));

    hsize_t spectrumChunkSize = 5000L; // 1000=faster random read, 10000=better compression
    hsize_t chromatogramChunkSize = 1000L;
    hsize_t spectrumMetaChunkSize = 2000L; // should be modified in case of on demand access
    // hsize_t chromatogramMetaChunkSize = 10L; // usually one experiment does not contain a lot of chromatograms, so this chunk size is small in order to save storage space
    hsize_t cvparamChunkSize = 5000L;
    hsize_t userparamChunkSize = 100L;

    variableChunkSizes_.insert(std::pair<MZ5DataSets, hsize_t>(SpectrumMZ,
            spectrumChunkSize));
    variableChunkSizes_.insert(std::pair<MZ5DataSets, hsize_t>(
            SpectrumIntensity, spectrumChunkSize));
    variableChunkSizes_.insert(std::pair<MZ5DataSets, hsize_t>(ChomatogramTime,
            chromatogramChunkSize));
    variableChunkSizes_.insert(std::pair<MZ5DataSets, hsize_t>(
            ChromatogramIntensity, chromatogramChunkSize));

    bufferInMB_ = 8L; // this affects all datasets.
    size_t sizeOfDouble = static_cast<size_t> (sizeof(double));
    size_t bufferInByte = bufferInMB_ * 1024L * 1024L;
    size_t numberOfChunksInBuffer = (bufferInByte / sizeOfDouble)
            / spectrumChunkSize;
    rdccSolts_ = 41957L; // for 32 mb, 10000 chunk size

    hsize_t spectrumBufferSize = spectrumChunkSize * (numberOfChunksInBuffer
            / 4L);
    hsize_t chromatogramBufferSize = chromatogramChunkSize * 10L;

    variableBufferSizes_.insert(std::pair<MZ5DataSets, size_t>(SpectrumMZ,
            spectrumBufferSize));
    variableBufferSizes_.insert(std::pair<MZ5DataSets, size_t>(
            SpectrumIntensity, spectrumBufferSize));
    variableBufferSizes_.insert(std::pair<MZ5DataSets, size_t>(ChomatogramTime,
            chromatogramBufferSize));
    variableBufferSizes_.insert(std::pair<MZ5DataSets, size_t>(
            ChromatogramIntensity, chromatogramBufferSize));

    if (config_.binaryDataEncoderConfig.compression
            == pwiz::msdata::BinaryDataEncoder::Compression_Zlib)
    {
        doTranslating_ = deltamz && translateinten;
        deflateLvl_ = 1;

        variableChunkSizes_.insert(std::pair<MZ5DataSets, hsize_t>(
                SpectrumMetaData, spectrumMetaChunkSize));
        variableChunkSizes_.insert(std::pair<MZ5DataSets, hsize_t>(
                SpectrumBinaryMetaData, spectrumMetaChunkSize));
        variableChunkSizes_.insert(std::pair<MZ5DataSets, hsize_t>(
                SpectrumIndex, spectrumMetaChunkSize));
        // should not affect file size to much, if chromatogram information are not compressed
        //  variableChunkSizes_.insert(std::pair<MZ5DataSets, hsize_t>(ChromatogramMetaData, chromatogramMetaChunkSize));
        //  variableChunkSizes_.insert(std::pair<MZ5DataSets, hsize_t>(ChromatogramBinaryMetaData,
        //   chromatogramMetaChunkSize));
        //   variableChunkSizes_.insert(std::pair<MZ5DataSets, hsize_t>(ChromatogramIndex, chromatogramMetaChunkSize));
        variableChunkSizes_.insert(std::pair<MZ5DataSets, hsize_t>(CVParam,
                cvparamChunkSize));
        variableChunkSizes_.insert(std::pair<MZ5DataSets, hsize_t>(UserParam,
                userparamChunkSize));

    }
    else
    {
        deflateLvl_ = 0;
    }

    spectrumLoadPolicy_ = SLP_InitializeAllOnFirstCall;
    chromatogramLoadPolicy_ = CLP_InitializeAllOnFirstCall;
}

const std::string& Configuration_mz5::getNameFor(const MZ5DataSets v)
{
    if (variableNames_.find(v) != variableNames_.end())
    {
        return variableNames_.find(v)->second;
    }
    throw std::out_of_range("[Configurator_mz5::getNameFor]: out of range");
}

Configuration_mz5::MZ5DataSets Configuration_mz5::getVariableFor(
        const std::string& name)
{
    if (variableVariables_.find(name) != variableVariables_.end())
    {
        return variableVariables_.find(name)->second;
    }
    throw std::out_of_range("[Configurator_mz5::getVariableFor]: out of range");
}

const DataType& Configuration_mz5::getDataTypeFor(const MZ5DataSets v)
{
    if (variableTypes_.find(v) != variableTypes_.end())
    {
        return variableTypes_.find(v)->second;
    }
    throw std::out_of_range("[Configurator_mz5::getDataTypeFor]: out of range");
}

const hsize_t& Configuration_mz5::getChunkSizeFor(const MZ5DataSets v)
{
    std::map<MZ5DataSets, hsize_t>::iterator it = variableChunkSizes_.find(v);
    if (it != variableChunkSizes_.end())
    {
        return it->second;
    }
    return EMPTY_CHUNK_SIZE;
}

const size_t& Configuration_mz5::getBufferSizeFor(const MZ5DataSets v)
{
    std::map<MZ5DataSets, size_t>::iterator it = variableBufferSizes_.find(v);
    if (it != variableBufferSizes_.end())
    {
        return it->second;
    }
    return NO_BUFFER_SIZE;
}

const size_t& Configuration_mz5::getBufferInMb()
{
    return bufferInMB_;
}

const size_t Configuration_mz5::getBufferInB()
{
    return (bufferInMB_ * 1024L * 1024L);
}

const size_t& Configuration_mz5::getRdccSlots()
{
    return rdccSolts_;
}

const Configuration_mz5::SpectrumLoadPolicy& Configuration_mz5::getSpectrumLoadPolicy() const
{
    return spectrumLoadPolicy_;
}

const Configuration_mz5::ChromatogramLoadPolicy& Configuration_mz5::getChromatogramLoadPolicy() const
{
    return chromatogramLoadPolicy_;
}

const bool Configuration_mz5::doTranslating() const
{
    return doTranslating_;
}

void Configuration_mz5::setTranslating(const bool flag) const
{
    doTranslating_ = flag;
}

const int Configuration_mz5::getDeflateLvl()
{
    return deflateLvl_;
}

const bool Configuration_mz5::doShuffel()
{
    return deflateLvl_ > 0;
}

}
}
}
