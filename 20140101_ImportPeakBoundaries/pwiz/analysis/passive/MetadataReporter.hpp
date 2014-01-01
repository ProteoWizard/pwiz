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


#ifndef _METADATAREPORTER_HPP_ 
#define _METADATAREPORTER_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "MSDataAnalyzer.hpp"


namespace pwiz {
namespace analysis {


/// writes file-level metadata to a file
class PWIZ_API_DECL MetadataReporter : public MSDataAnalyzer
{
    public:

    /// \name MSDataAnalyzer interface 
    //@{
    virtual void open(const DataInfo& dataInfo);
    //@}
};


template<>
struct analyzer_strings<MetadataReporter>
{
    static const char* id() {return "metadata";}
    static const char* description() {return "write file-level metadata";}
    static const char* argsFormat() {return "";}
    static std::vector<std::string> argsUsage() {return std::vector<std::string>();}
};


} // namespace analysis 
} // namespace pwiz


#endif // _METADATAREPORTER_HPP_ 

