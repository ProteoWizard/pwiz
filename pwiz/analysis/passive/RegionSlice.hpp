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


#ifndef _REGIONSLICE_HPP_
#define _REGIONSLICE_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "MSDataAnalyzer.hpp"
#include "MSDataCache.hpp"
#include "RegionAnalyzer.hpp"


namespace pwiz {
namespace analysis {


/// writes data samples from a single rectangular region 
class PWIZ_API_DECL RegionSlice : public MSDataAnalyzer
{
    public:

    struct PWIZ_API_DECL Config : public RegionAnalyzer::Config
    {
        Config(const std::string& args); 
    };

    RegionSlice(const MSDataCache& cache, const Config& config);

    /// \name MSDataAnalyzer interface
    //@{
    virtual void open(const DataInfo& dataInfo);

    virtual UpdateRequest updateRequested(const DataInfo& dataInfo,
                                          const SpectrumIdentity& spectrumIdentity) const;

    virtual void update(const DataInfo& dataInfo, 
                        const Spectrum& spectrum);

    virtual void close(const DataInfo& dataInfo);
    //@}

    private:
    const MSDataCache& cache_;
    boost::shared_ptr<RegionAnalyzer> regionAnalyzer_;
};


template<>
struct analyzer_strings<RegionSlice>
{
    static const char* id() {return "slice";}
#define SLICE_INDEX_ARG "index"
#define SLICE_SCAN_ARG "sn"
#define SLICE_RETENTIONTIME_ARG "rt"
#define SLICE_MZRANGE_ARG "mz"
    static const char* description() {return "write data from a rectangular region";}
    static const char* argsFormat() {return "[" SLICE_MZRANGE_ARG "=<mzLow>[,<mzHigh>]] [" SLICE_RETENTIONTIME_ARG "=<rtLow>[,<rtHigh>]]] [" SLICE_INDEX_ARG "=<indexLow>[,<indexHigh>] | " SLICE_SCAN_ARG "=<scanLow>[,<scanHigh>]] [" TABULARCONFIG_DELIMITER_OPTIONS_STR "]";}
    static std::vector<std::string> argsUsage()
    {
        std::vector<std::string> result;
        result.push_back(SLICE_MZRANGE_ARG": set m/z range");
        result.push_back(SLICE_RETENTIONTIME_ARG": set retention time range");
        result.push_back(SLICE_INDEX_ARG": set spectrum index range");
        result.push_back(SLICE_SCAN_ARG": set scan number range");
        result.push_back(TABULARCONFIG_DELIMITER_USAGE_STR);
        return result;
    }
};


} // namespace analysis 
} // namespace pwiz


#endif // _REGIONSLICE_HPP_

