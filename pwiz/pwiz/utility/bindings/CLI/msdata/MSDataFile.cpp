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
//#include "boost/system/error_code.hpp"
#include <WinError.h>

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


MSDataFile::MSDataFile(System::String^ path)
: MSData(0)
{
    try
    {
        initializeReaderList();
        base_ = new boost::shared_ptr<b::MSDataFile>(new b::MSDataFile(ToStdString(path), (b::Reader*) readerList.get()));
        MSData::base_ = reinterpret_cast<boost::shared_ptr<b::MSData>*>(base_);
    }
    catch(exception& e)
    {
        throw gcnew System::Exception(gcnew System::String(e.what()));
    }
    catch(...)
    {
        throw gcnew System::Exception("[MSDataFile::MSDataFile()] Unhandled exception");
    }
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
    try
    {
        b::MSDataFile::WriteConfig config2((b::MSDataFile::Format) config->format);
        config2.gzipped = config->gzipped;
        config2.binaryDataEncoderConfig.precision = (b::BinaryDataEncoder::Precision) config->precision;
        config2.binaryDataEncoderConfig.byteOrder = (b::BinaryDataEncoder::ByteOrder) config->byteOrder;
        config2.binaryDataEncoderConfig.compression = (b::BinaryDataEncoder::Compression) config->compression;
        b::MSDataFile::write(**msd->base_, ToStdString(filename), config2);
    }
    catch(exception& e)
    {
        throw gcnew System::Exception(gcnew System::String(e.what()));
    }
    catch(...)
    {
        throw gcnew System::Exception(gcnew System::String("[MSDataFile::write()] Unhandled exception"));
    }
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
    try
    {
        b::MSDataFile::WriteConfig config2((b::MSDataFile::Format) config->format);
        config2.gzipped = config->gzipped;
        config2.binaryDataEncoderConfig.precision = (b::BinaryDataEncoder::Precision) config->precision;
        config2.binaryDataEncoderConfig.byteOrder = (b::BinaryDataEncoder::ByteOrder) config->byteOrder;
        config2.binaryDataEncoderConfig.compression = (b::BinaryDataEncoder::Compression) config->compression;
        (*base_)->write(ToStdString(filename), config2);
    }
    catch(exception& e)
    {
        throw gcnew System::Exception(gcnew System::String(e.what()));
    }
    catch(...)
    {
        throw gcnew System::Exception(gcnew System::String("[MSDataFile::write()] Unhandled exception"));
    }
}


} // namespace msdata
} // namespace CLI
} // namespace pwiz
