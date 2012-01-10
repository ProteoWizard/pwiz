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


#ifndef _MSDATAANALYZERAPPLICATION_HPP_
#define _MSDATAANALYZERAPPLICATION_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/analysis/passive/MSDataAnalyzer.hpp"


namespace pwiz {
namespace analysis {


///
/// Utility class for handling command line parsing, filename wrangling, and
/// MSDataAnalyzer driving.
///
struct PWIZ_API_DECL MSDataAnalyzerApplication
{
    std::string usageOptions;
    std::string outputDirectory;
    std::vector<std::string> filenames;
    std::vector<std::string> filters;
    std::vector<std::string> commands;
    bool verbose;

    /// construct and parse command line, filling in the various structure fields
    MSDataAnalyzerApplication(int argc, const char* argv[]);

    /// iterate through file list, running analyzer on each file 
    void run(MSDataAnalyzer& analyzer, std::ostream* log = 0) const;
};


} // namespace analysis 
} // namespace pwiz


#endif // _MSDATAANALYZERAPPLICATION_HPP_

