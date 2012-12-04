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

        bool operator == (const Config &rhs) const
        {
            return begin==rhs.begin &&
                end == rhs.end &&
                interpretAsScanNumbers == rhs.interpretAsScanNumbers &&
                precision == rhs.precision;
        }
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
#define BINARY_INDEX_ARG "index"
#define BINARY_SCAN_ARG "sn"
#define BINARY_PRECISION_ARG "precision"
    static const char* argsFormat() {return BINARY_INDEX_ARG"=<spectrumIndexLow>[,<spectrumIndexHigh>] | "BINARY_SCAN_ARG"=<scanNumberLow>[,<scanNumberHigh>] ["BINARY_PRECISION_ARG"=<precision>]";}

    static const char* description() {return "write binary data for selected spectra";}

    static std::vector<std::string> argsUsage() 
    {
        std::vector<std::string> result;
        result.push_back(BINARY_INDEX_ARG": write data for spectra in this index range");
        result.push_back(BINARY_SCAN_ARG": write data for spectra in this scan number range");
        result.push_back(BINARY_PRECISION_ARG": write d decimal places");
        return result;
    }
};


} // namespace analysis 
} // namespace pwiz


#endif // _SPECTRUMBINARYDATA_HPP_ 

