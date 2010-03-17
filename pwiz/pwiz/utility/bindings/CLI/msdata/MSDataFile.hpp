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


#ifndef _MSDATAFILE_HPP_CLI_
#define _MSDATAFILE_HPP_CLI_


#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "MSData.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
//#include "Reader.hpp"
//#include "BinaryDataEncoder.hpp"
#pragma warning( pop )


namespace pwiz {
namespace CLI {
namespace msdata {


/// <summary>
/// MSData object plus file I/O
/// </summary>
public ref class MSDataFile : public MSData
{
    DEFINE_SHARED_DERIVED_INTERNAL_SHARED_BASE_CODE(pwiz::msdata, MSDataFile, MSData);

    public:

    /// <summary>
    /// constructs MSData object backed by file
    /// </summary>
    MSDataFile(System::String^ path);

    /// <summary>
    /// supported data formats for write()
    /// </summary>
    enum class Format {Format_Text, Format_mzML, Format_mzXML, Format_MGF, Format_MS2, Format_CMS2};

    enum class Precision {Precision_32, Precision_64};
    enum class ByteOrder {ByteOrder_LittleEndian, ByteOrder_BigEndian};
    enum class Compression {Compression_None, Compression_Zlib};

    /// <summary>
    /// configuration options for write()
    /// </summary>
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

    /// <summary>
    /// static write function for any MSData object with the default configuration (mzML, 64-bit, no compression)
    /// </summary>
    static void write(MSData^ msd, System::String^ filename);

    /// <summary>
    /// static write function for any MSData object with the specified configuration
    /// </summary>
    static void write(MSData^ msd, System::String^ filename, WriteConfig^ config);

    /// <summary>
    /// member write function with the default configuration (mzML, 64-bit, no compression)
    /// </summary>
    void write(System::String^ filename);

    /// <summary>
    /// member write function with the specified configuration
    /// </summary>
    void write(System::String^ filename, WriteConfig^ config);
};

} // namespace msdata
} // namespace CLI
} // namespace pwiz


#endif // _MSDATAFILE_HPP_CLI_
