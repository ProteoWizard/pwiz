//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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


#ifndef _IDENTDATAFILE_HPP_
#define _IDENTDATAFILE_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "IdentData.hpp"
#include "Reader.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"


namespace pwiz {
namespace identdata {


/// IdentData object plus file I/O
struct PWIZ_API_DECL IdentDataFile : public IdentData
{
    /// constructs IdentData object backed by file;
    /// reader==0 -> use DefaultReaderList
    IdentDataFile(const std::string& filename,
                  const Reader* reader = 0,
                  const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0,
                  bool ignoreSequenceCollectionAndAnalysisData = false);

    /// data format for write()
    enum PWIZ_API_DECL Format {Format_Text, Format_MzIdentML, Format_pepXML};

    /// configuration for write()
    struct PWIZ_API_DECL WriteConfig
    {
        Format format;

        WriteConfig(Format format = Format_MzIdentML)
        :   format(format)
        {}
    };

    /// static write function for any IdentData object;
    static void write(const IdentData& mzid,
                      const std::string& filename,
                      const WriteConfig& config = WriteConfig(),
                      const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);

    /// member write function 
    void write(const std::string& filename,
               const WriteConfig& config = WriteConfig(),
               const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);

    /// static write function for any IdentData object;
    static void write(const IdentData& mzid,
                      const std::string& filename,
                      std::ostream& os,
                      const WriteConfig& config = WriteConfig(),
                      const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);

    /// member write function 
    void write(std::ostream& os,
               const std::string& filename,
               const WriteConfig& config = WriteConfig(),
               const pwiz::util::IterationListenerRegistry* iterationListenerRegistry = 0);
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, IdentDataFile::Format format);
PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const IdentDataFile::WriteConfig& config);


} // namespace identdata
} // namespace pwiz


#endif // _IDENTDATAFILE_HPP_
