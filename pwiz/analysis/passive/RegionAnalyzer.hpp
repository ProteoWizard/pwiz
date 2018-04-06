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


#ifndef _REGIONANANALYZER_HPP_ 
#define _REGIONANANALYZER_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "MSDataAnalyzer.hpp"
#include "MSDataCache.hpp"
#include "TabularConfig.hpp"
#include <iostream>


namespace pwiz {
namespace analysis {


/// analyzes a rectangular region of m/z-time space 
class PWIZ_API_DECL RegionAnalyzer : public MSDataAnalyzer
{
    public:

    struct PWIZ_API_DECL Config : TabularConfig
    {
        std::pair<double,double> mzRange;
        std::pair<size_t,size_t> indexRange;
        std::pair<int,int> scanNumberRange;
        std::pair<double,double> rtRange;
        bool dumpRegionData; // if true, dump info to a stream or file
        std::ostream* osDump; // if non-null, dump to this stream, else open a file
        std::string filenameSuffix;

        Config();
    };

    RegionAnalyzer(const Config& config, const MSDataCache& cache);

    struct PWIZ_API_DECL SpectrumStats
    {
        double sumIntensity;
        MZIntensityPair max;  // sample point with highest intensity
        MZIntensityPair peak; // interpolated peak

        SpectrumStats();
    };

    const std::vector<SpectrumStats>& spectrumStats() const;

    struct PWIZ_API_DECL Stats
    {
        size_t nonzeroCount; // # spectra with sumIntensity > 0
        double sum_sumIntensity;
        double sum_peak_intensity;

        // intensity-weighted peak statistics
        double mean_peak_mz;
        double variance_peak_mz;
        double sd_peak_mz; // standard deviation

        // index of peak with highest intensity
        size_t indexApex;

        Stats();
    };

    const Stats& stats() const;


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
    struct Impl;
    boost::shared_ptr<Impl> impl_;
    RegionAnalyzer(RegionAnalyzer&);
    RegionAnalyzer& operator=(RegionAnalyzer&);
};


} // namespace analysis 
} // namespace pwiz


#endif //_REGIONANANALYZER_HPP_ 

