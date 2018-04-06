//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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


#ifndef _TRADATAFILE_HPP_
#define _TRADATAFILE_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "TraData.hpp"
#include "Reader.hpp"


namespace pwiz {
namespace tradata {


/// TraData object plus file I/O
struct PWIZ_API_DECL TraDataFile : public TraData
{
    /// constructs TraData object backed by file;
    /// reader==0 -> use DefaultReaderList 
    TraDataFile(const std::string& filename, 
                const Reader* reader = 0);

    /// data format for write()
    enum PWIZ_API_DECL Format {Format_Text, Format_traML};

    /// configuration for write()
    struct PWIZ_API_DECL WriteConfig
    {
        Format format;
		bool gzipped; // if true, file is written as .gz

        WriteConfig(Format format = Format_traML, bool gzipped = false)
        :   format(format), gzipped(gzipped)
        {}
    };

    /// static write function for any TraData object;
    static void write(const TraData& msd,
                      const std::string& filename,
                      const WriteConfig& config = WriteConfig());

    /// member write function 
    void write(const std::string& filename,
               const WriteConfig& config = WriteConfig());
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, TraDataFile::Format format);
PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const TraDataFile::WriteConfig& config);


} // namespace tradata
} // namespace pwiz


#endif // _TRADATAFILE_HPP_
