//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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


#ifndef _SPECTRUMTABLE_HPP_ 
#define _SPECTRUMTABLE_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "MSDataAnalyzer.hpp"
#include "MSDataCache.hpp"


namespace pwiz {
namespace analysis {


/// writes table of spectrum metadata to a file
class PWIZ_API_DECL SpectrumTable : public MSDataAnalyzer
{
    public:

    struct PWIZ_API_DECL Config
    {
        enum Delimiter
        {
            Delimiter_FixedWidth,
            Delimiter_Space,
            Delimiter_Comma,
            Delimiter_Tab
        };

        /// delimiter between columns (unless set to Delimiter_FixedWidth)
        Delimiter delimiter;

        Config(const std::string& args = "");
    };

    
    SpectrumTable(const MSDataCache& cache, const Config& config);

    /// \name MSDataAnalyzer interface
    //@{
    virtual UpdateRequest updateRequested(const DataInfo& dataInfo,
                                          const SpectrumIdentity& spectrumIdentity) const;

    virtual void close(const DataInfo& dataInfo);
    //@}

    private:
    const MSDataCache& cache_;
    const Config config_;
};


template<>
struct analyzer_strings<SpectrumTable>
{
    static const char* id() {return "spectrum_table";}
    static const char* description() {return "write spectrum metadata in a table format";}
    static const char* argsFormat() {return "[delimiter=fixed|space|comma|tab]";}
    static std::vector<std::string> argsUsage()
    {
        return std::vector<std::string>(1, "delimiter: sets column separation; default is fixed width");
    }
};


} // namespace analysis 
} // namespace pwiz


#endif // _SPECTRUMTABLE_HPP_ 

