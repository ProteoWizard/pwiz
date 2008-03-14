//
// MSDataFile.hpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _MSDATAFILE_HPP_
#define _MSDATAFILE_HPP_


#include "MSData.hpp"
#include "BinaryDataEncoder.hpp"

#if (!defined(_MSC_VER) && !defined(PWIZ_NO_READER_RAW))
// Xcalibur DLL usage is msvc only - mingw doesn't provide com support
#define PWIZ_NO_READER_RAW
#endif

namespace pwiz {
namespace msdata {


/// MSData object plus file I/O
struct MSDataFile : public MSData
{
    /// interface for file readers
    class Reader
    {
        public:

        /// return true iff Reader can handle the file; Reader may filter based on
        /// filename or on the buffer holding the head of the file
        virtual bool accept(const std::string& filename, const std::string& head) const = 0;

        /// fill in the MSData structure -- generally this will amount to: 
        /// 1) fill in the file-level metadata
        /// 2) instantiate a SpectrumList (held by MSData::run.spectrumListPtr)
        virtual void read(const std::string& filename, MSData& result) const = 0;

        virtual ~Reader(){}
    };

    /// runtime Reader registration (replaces default Readers)
    static void registerReader(const Reader& reader);

    /// clear registered Reader (use default Readers)
    static void clearReader();

    /// constructs MSData object backed by file
    MSDataFile(const std::string& filename);

    /// data format for write()
    enum Format {Format_Text, Format_mzML, Format_mzXML};

    /// configuration for write()
    struct WriteConfig
    {
        Format format;
        BinaryDataEncoder::Config binaryDataEncoderConfig;
        bool indexed;

        WriteConfig(Format _format = Format_mzML)
        :   format(_format), indexed(true)
        {}
    };

    /// static write function for any MSData object
    static void write(const MSData& msd,
                      const std::string& filename,
                      const WriteConfig& config = WriteConfig());

    /// member write function 
    void write(const std::string& filename,
               const WriteConfig& config = WriteConfig());
};


std::ostream& operator<<(std::ostream& os, MSDataFile::Format format);
std::ostream& operator<<(std::ostream& os, const MSDataFile::WriteConfig& config);


} // namespace msdata
} // namespace pwiz


#endif // _MSDATAFILE_HPP_

