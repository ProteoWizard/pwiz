//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#include "MSDataFile.hpp"
#include "pwiz/utility/misc/Exception.hpp"
#include "pwiz/utility/misc/random_access_compressed_ifstream.hpp"
#include "pwiz_tools/common/FullReaderList.hpp"

namespace b = pwiz::msdata;

namespace {

boost::shared_ptr<b::FullReaderList> readerList;
void initializeReaderList()
{
    if (!readerList.get())
        readerList.reset(new b::FullReaderList);
}

} // namespace


namespace pwiz {
namespace CLI {
namespace msdata {


MSDataFile::MSDataFile(boost::shared_ptr<b::MSDataFile>* base)
: MSData(new boost::shared_ptr<b::MSData>(base->get(), nullDelete)),
  base_(base)
{LOG_CONSTRUCT("MSDataFile")}

MSDataFile::~MSDataFile()
{
    LOG_DESTRUCT("MSDataFile", true) SAFEDELETE(base_);

    // MCC: forcing garbage collection is the best way I know of to try to clean up 
    //      reclaimable SpectrumList handles which hold on to SpectrumListPtrs
    System::GC::Collect();
    System::GC::WaitForPendingFinalizers();
}

MSDataFile::!MSDataFile() {LOG_FINALIZE("MSDataFile") delete this;}
b::MSDataFile& MSDataFile::base() {return **base_;}

MSDataFile::MSDataFile(System::String^ path)
: MSData(0)
{
    LOG_CONSTRUCT("MSDataFile")
    try
    {
        initializeReaderList();
        base_ = new boost::shared_ptr<b::MSDataFile>(new b::MSDataFile(ToStdString(path), (b::Reader*) readerList.get()));
        MSData::base_ = new boost::shared_ptr<b::MSData>(base_->get(), nullDelete);
    }
    CATCH_AND_FORWARD
}

MSDataFile::MSDataFile(System::String^ path, util::IterationListenerRegistry^ ilr)
: MSData(0)
{
    LOG_CONSTRUCT("MSDataFile")
    try
    {
        initializeReaderList();
        base_ = new boost::shared_ptr<b::MSDataFile>(new b::MSDataFile(ToStdString(path), (b::Reader*) readerList.get()/*, ilr->base()*/));
        MSData::base_ = new boost::shared_ptr<b::MSData>(base_->get(), nullDelete);
    }
    CATCH_AND_FORWARD
}


void MSDataFile::write(MSData^ msd, System::String^ filename)
{
    WriteConfig^ config = gcnew WriteConfig();
    config->format = Format::Format_mzML;
    config->precision = Precision::Precision_64;
    config->byteOrder = ByteOrder::ByteOrder_LittleEndian;
    config->compression = Compression::Compression_None;
    write(msd, filename, config);
}


void MSDataFile::write(MSData^ msd, System::String^ filename, WriteConfig^ config)
{
    write(msd, filename, config, nullptr);
}

static void translateConfig(MSDataFile::WriteConfig^ config, b::MSDataFile::WriteConfig& config2)
{
    config2.gzipped = config->gzipped;
    config2.indexed = config->indexed;
    config2.useWorkerThreads = config->useWorkerThreads;
    config2.binaryDataEncoderConfig.precision = (b::BinaryDataEncoder::Precision) config->precision;
    config2.binaryDataEncoderConfig.byteOrder = (b::BinaryDataEncoder::ByteOrder) config->byteOrder;
    config2.binaryDataEncoderConfig.compression = (b::BinaryDataEncoder::Compression) config->compression;
    if (config->numpressLinear)
    {
        config2.binaryDataEncoderConfig.numpressOverrides[b::MS_m_z_array] = b::BinaryDataEncoder::Numpress_Linear;
        config2.binaryDataEncoderConfig.numpressOverrides[b::MS_time_array] = b::BinaryDataEncoder::Numpress_Linear;
    }
    if (config->numpressPic) 
    {
        config2.binaryDataEncoderConfig.numpressOverrides[b::MS_intensity_array] = b::BinaryDataEncoder::Numpress_Pic;
    }    
    if (config->numpressSlof) 
    {
        config2.binaryDataEncoderConfig.numpressOverrides[b::MS_intensity_array] = b::BinaryDataEncoder::Numpress_Slof;
    }
    config2.binaryDataEncoderConfig.numpressLinearErrorTolerance = config->numpressLinearErrorTolerance;
    config2.binaryDataEncoderConfig.numpressLinearAbsMassAcc = config->numpressLinearAbsMassAcc;
    config2.binaryDataEncoderConfig.numpressSlofErrorTolerance = config->numpressSlofErrorTolerance;
}

void MSDataFile::write(MSData^ msd,
                       System::String^ filename,
                       WriteConfig^ config,
                       util::IterationListenerRegistry^ ilr)
{
    try
    {
        b::MSDataFile::WriteConfig config2((b::MSDataFile::Format) config->format);
        translateConfig(config,config2);
        b::MSDataFile::write(msd->base(), ToStdString(filename), config2, ilr == nullptr ? 0 : &ilr->base());
    }
    CATCH_AND_FORWARD
    GC::KeepAlive(ilr);
    GC::KeepAlive(msd);
}


void MSDataFile::write(System::String^ filename)
{
    WriteConfig^ config = gcnew WriteConfig();
    config->format = Format::Format_mzML;
    config->precision = Precision::Precision_64;
    config->byteOrder = ByteOrder::ByteOrder_LittleEndian;
    config->compression = Compression::Compression_None;
    write(filename, config);
}


void MSDataFile::write(System::String^ filename, WriteConfig^ config)
{
    write(filename, config, nullptr);
}


void MSDataFile::write(System::String^ filename,
                       WriteConfig^ config,
                       util::IterationListenerRegistry^ ilr)
{
    try
    {
        b::MSDataFile::WriteConfig config2((b::MSDataFile::Format) config->format);
        translateConfig(config,config2);
        base().write(ToStdString(filename), config2, ilr == nullptr ? 0 : &ilr->base());
        GC::KeepAlive(ilr);
    }
    CATCH_AND_FORWARD
}


void MSDataFile::calculateSHA1Checksums(MSData^ msd)
{
    try {b::calculateSHA1Checksums(msd->base());} CATCH_AND_FORWARD
    GC::KeepAlive(msd);
}


} // namespace msdata
} // namespace CLI
} // namespace pwiz
