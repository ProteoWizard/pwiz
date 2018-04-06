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


#ifndef _PROTEOMEDATAFILE_HPP_
#define _PROTEOMEDATAFILE_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "ProteomeData.hpp"
#include "Reader.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"


namespace pwiz {
namespace proteome {


/// ProteomeData object plus file I/O
struct PWIZ_API_DECL ProteomeDataFile : public ProteomeData
{
    /// constructs ProteomeData object backed by file;
    /// indexed==true -> uses DefaultReaderList with indexing
    ProteomeDataFile(const std::string& uri, bool indexed = false);

    /// constructs ProteomeData object backed by file using the specified reader
    ProteomeDataFile(const std::string& uri, const Reader& reader);

    /// data format for write()
    enum PWIZ_API_DECL Format {Format_FASTA, Format_Text};

    /// configuration for write()
    struct PWIZ_API_DECL WriteConfig
    {
        Format format;
        bool indexed;
		bool gzipped; // if true, file is written as .gz

        WriteConfig(Format _format = Format_FASTA, bool _gzipped = false)
        :   format(_format), indexed(true), gzipped(_gzipped)
        {}
    };

    /// static write function for any ProteomeData object;
    /// iterationListenerRegistry may be used for progress updates
    static void write(const ProteomeData& pd,
                      const std::string& uri,
                      const WriteConfig& config = WriteConfig(),
                      const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);

    /// member write function 
    void write(const std::string& uri,
               const WriteConfig& config = WriteConfig(),
               const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, ProteomeDataFile::Format format);
PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const ProteomeDataFile::WriteConfig& config);


} // namespace proteome
} // namespace pwiz


#endif // _PROTEOMEDATAFILE_HPP_

