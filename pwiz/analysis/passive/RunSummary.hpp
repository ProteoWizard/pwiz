//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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


#ifndef _RUNSUMMARY_HPP_ 
#define _RUNSUMMARY_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "MSDataAnalyzer.hpp"
#include "MSDataCache.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"


namespace pwiz {
namespace analysis {


/// writes table of spectrum metadata to a file
class PWIZ_API_DECL RunSummary : public MSDataAnalyzer
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

        pwiz::util::IntegerSet msLevels;
        pwiz::util::IntegerSet charges;

        Config(const std::string& args = "");
    };

    
    RunSummary(const MSDataCache& cache, const Config& config);

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
struct analyzer_strings<RunSummary>
{
    static const char* id() {return "run_summary";}
    static const char* description() {return "print summary statistics about a run";}
    static const char* argsFormat() {return "[delimiter=fixed|space|comma|tab] [msLevels=int_set] [charges=int_set]";}
    static std::vector<std::string> argsUsage()
    {
        std::vector<std::string> result;
        result.push_back("delimiter: sets column separation; default is fixed width");
        result.push_back("msLevels: if specified, summary only operates on these MS levels; default is all MS levels");
        result.push_back("charges: if specified, summary only operates on these charge states; default is all charges");
        return result;
    }
};


} // namespace analysis 
} // namespace pwiz


#endif // _RUNSUMMARY_HPP_ 

