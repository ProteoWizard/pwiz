//
// MSDataFile.hpp
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


#ifndef _MSDATAFILE_HPP_CLI_
#define _MSDATAFILE_HPP_CLI_


#include "MSData.hpp"
#include "../../../data/msdata/MSDataFile.hpp"
//#include "Reader.hpp"
//#include "BinaryDataEncoder.hpp"


namespace pwiz {
namespace CLI {
namespace msdata {


/// MSData object plus file I/O
public ref class MSDataFile : public MSData
{
    internal: MSDataFile(pwiz::msdata::MSDataFile* base) : MSData(base), base_(base) {}
              virtual ~MSDataFile() {}
              pwiz::msdata::MSDataFile* base_;

    public:
    /// constructs MSData object backed by file;
    /// reader==0 -> use DefaultReaderList 
    MSDataFile(System::String^ filename);

    /// data format for write()
    enum class Format {Format_Text, Format_mzML, Format_mzXML};
    enum class Precision {Precision_32, Precision_64};
    enum class ByteOrder {ByteOrder_LittleEndian, ByteOrder_BigEndian};
    enum class Compression {Compression_None, Compression_Zlib};

    /// configuration for write()
    ref class WriteConfig
    {
        public:
        Format format;
        Precision precision;
        ByteOrder byteOrder;
        Compression compression;
        bool indexed;

        WriteConfig(Format _format)
        :   format(_format), indexed(true)
        {}
    };

    /// static write function for any MSData object
    static void write(MSData^ msd, System::String^ filename);
    static void write(MSData^ msd, System::String^ filename, WriteConfig^ config);

    /// member write function 
    void write(System::String^ filename);
    void write(System::String^ filename, WriteConfig^ config);
};

} // namespace msdata
} // namespace CLI
} // namespace pwiz


#endif // _MSDATAFILE_HPP_CLI_
