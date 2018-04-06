//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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


#include "pwiz/utility/misc/Export.hpp"
#include "MSData.hpp"
#include "Reader.hpp"
#include "BinaryDataEncoder.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"


namespace pwiz {
namespace msdata {


/// MSData object plus file I/O
struct PWIZ_API_DECL MSDataFile : public MSData
{
    /// constructs MSData object backed by file;
    /// reader==0 -> use DefaultReaderList 
    MSDataFile(const std::string& filename, 
               const Reader* reader = 0,
               bool calculateSourceFileChecksum = false);

    /// data format for write()
    enum PWIZ_API_DECL Format {Format_Text, Format_mzML, Format_mzXML, Format_MGF, Format_MS1, Format_CMS1, Format_MS2, Format_CMS2, Format_MZ5};

    /// configuration for write()
    struct PWIZ_API_DECL WriteConfig
    {
        Format format;
        BinaryDataEncoder::Config binaryDataEncoderConfig;
        bool indexed;
		bool gzipped; // if true, file is written as .gz

        WriteConfig(Format _format = Format_mzML,bool _gzipped = false)
        :   format(_format), indexed(true), gzipped(_gzipped)
        {}
    };

    /// static write function for any MSData object;
    /// iterationListenerRegistry may be used for progress updates
    static void write(const MSData& msd,
                      const std::string& filename,
                      const WriteConfig& config = WriteConfig(),
                      const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);

    /// member write function 
    void write(const std::string& filename,
               const WriteConfig& config = WriteConfig(),
               const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);

    /// static write function for any MSData object;
    /// iterationListenerRegistry may be used for progress updates
    static void write(const MSData& msd,
                      std::ostream& os,
                      const WriteConfig& config = WriteConfig(),
                      const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);

    /// member write function 
    void write(std::ostream& os,
               const WriteConfig& config = WriteConfig(),
               const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);
};


/// calculates and adds a CV term for the SHA1 checksum of a source file element
PWIZ_API_DECL void calculateSourceFileSHA1(SourceFile& sourceFile);

/// Iterate and calculate SHA-1 for all source files
PWIZ_API_DECL void calculateSHA1Checksums(const MSData& msd);

PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, MSDataFile::Format format);
PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const MSDataFile::WriteConfig& config);


} // namespace msdata
} // namespace pwiz


#endif // _MSDATAFILE_HPP_

