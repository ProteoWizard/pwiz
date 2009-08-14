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


#ifndef _SPECTRUMBINARYDATA_HPP_ 
#define _SPECTRUMBINARYDATA_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "MSDataAnalyzer.hpp"
#include "MSDataCache.hpp"


namespace pwiz {
namespace analysis {


/// writes table of spectrum metadata to a file
class PWIZ_API_DECL SpectrumBinaryData : public MSDataAnalyzer
{
    public:

    struct PWIZ_API_DECL Config
    {
        size_t begin;
        size_t end;
        bool interpretAsScanNumbers;
        size_t precision; 

        Config(const std::string& args = "");
    };

    SpectrumBinaryData(const MSDataCache& cache, const Config& config);

    /// \name MSDataAnalyzer interface
    //@{
    virtual UpdateRequest updateRequested(const DataInfo& dataInfo,
                                          const SpectrumIdentity& spectrumIdentity) const;

    virtual void update(const DataInfo& dataInfo, 
                        const Spectrum& spectrum);
    //@}

    private:
    const MSDataCache& cache_;
    const Config config_;
};


template<>
struct analyzer_strings<SpectrumBinaryData>
{
    static const char* id() {return "binary";}
    static const char* description() {return "write binary data for spectra i through j";}
    static const char* argsFormat() {return "i[-][j] [sn] [precision=d]";}

    static std::vector<std::string> argsUsage() 
    {
        std::vector<std::string> result;
        result.push_back("sn: interpret as scan number, not index");
        result.push_back("precision=d: write d decimal places");
        return result;
    }
};


} // namespace analysis 
} // namespace pwiz


#endif // _SPECTRUMBINARYDATA_HPP_ 

