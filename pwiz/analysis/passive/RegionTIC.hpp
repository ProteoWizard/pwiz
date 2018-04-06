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


#ifndef _REGIONTIC_HPP_
#define _REGIONTIC_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "MSDataAnalyzer.hpp"
#include "MSDataCache.hpp"
#include "RegionAnalyzer.hpp"
#include "TabularConfig.hpp"


namespace pwiz {
namespace analysis {


/// writes data samples from a single rectangular region 
class PWIZ_API_DECL RegionTIC : public MSDataAnalyzer
{
    public:

    struct PWIZ_API_DECL Config : TabularConfig
    {
        std::pair<double,double> mzRange;
        Config(const std::string& args); 

        bool operator==(const Config &rhs) 
        {
            return mzRange == rhs.mzRange &&
                delim_equal(rhs);
        }
    };

    RegionTIC(const MSDataCache& cache, const Config& config);

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
    Config config_;
};


template<>
struct analyzer_strings<RegionTIC>
{
    static const char* id() {return "tic";}
    static const char* description() {return "write total ion counts for an m/z range";}
#define TIC_MZRANGE_ARG "mz"
    static const char* argsFormat() {return "[" TIC_MZRANGE_ARG "=<mzLow>[,<mzHigh>]] [" TABULARCONFIG_DELIMITER_OPTIONS_STR "]";}
    static std::vector<std::string> argsUsage() 
    {
        std::vector<std::string> usage;
        usage.push_back(TIC_MZRANGE_ARG": set mz range");
        usage.push_back(TABULARCONFIG_DELIMITER_USAGE_STR);
        return usage;
    }
};


} // namespace analysis 
} // namespace pwiz


#endif // _REGIONTIC_HPP_

