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


#ifndef _PROTEOMEDATAFILE_HPP_CLI_
#define _PROTEOMEDATAFILE_HPP_CLI_


#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "ProteomeData.hpp"
#include "pwiz/data/proteome/ProteomeDataFile.hpp"
#include "Reader.hpp"
#pragma warning( pop )


namespace pwiz {
namespace CLI {
namespace proteome {


/// <summary>
/// ProteomeData object plus file I/O
/// </summary>
public ref class ProteomeDataFile : public ProteomeData
{
    DEFINE_SHARED_DERIVED_INTERNAL_SHARED_BASE_CODE(pwiz::proteome, ProteomeDataFile, ProteomeData);

    public:

    /// <summary>
    /// constructs ProteomeData object backed by file using the default reader list (without indexing)
    /// </summary>
    ProteomeDataFile(System::String^ path);

    /// <summary>
    /// constructs ProteomeData object backed by file using the default reader list
    /// </summary>
    ProteomeDataFile(System::String^ path, bool indexed);

    /// <summary>
    /// constructs ProteomeData object backed by file using the specified reader
    /// </summary>
    ProteomeDataFile(System::String^ path, Reader^ reader);

    /// <summary>
    /// supported data formats for write()
    /// </summary>
    enum class Format {FASTA, Text};

    /// <summary>
    /// configuration options for write()
    /// </summary>
    ref class WriteConfig
    {
        public:
        Format format;
        bool indexed;
		bool gzipped; // if true, file is written as .gz

        WriteConfig()
        :   format(Format::FASTA), indexed(true), gzipped(false)
        {}

        WriteConfig(Format _format)
        :   format(_format), indexed(true), gzipped(false)
        {}

        WriteConfig(Format _format, bool _gzipped)
        :   format(_format), indexed(true), gzipped(_gzipped)
        {}
    };

    /// <summary>
    /// static write function for any ProteomeData object with the default configuration (mzML, 64-bit, no compression)
    /// </summary>
    static void write(ProteomeData^ pd, System::String^ filename);

    /// <summary>
    /// static write function for any ProteomeData object with the specified configuration
    /// </summary>
    static void write(ProteomeData^ pd, System::String^ filename, WriteConfig^ config);

    /// <summary>
    /// member write function with the default configuration (mzML, 64-bit, no compression)
    /// </summary>
    void write(System::String^ filename);

    /// <summary>
    /// member write function with the specified configuration
    /// </summary>
    void write(System::String^ filename, WriteConfig^ config);
};

} // namespace proteome
} // namespace CLI
} // namespace pwiz


#endif // _PROTEOMEDATAFILE_HPP_CLI_
