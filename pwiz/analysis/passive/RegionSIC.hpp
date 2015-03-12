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


#ifndef _REGIONSIC_HPP_
#define _REGIONSIC_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "MSDataAnalyzer.hpp"
#include "MSDataCache.hpp"
#include "RegionAnalyzer.hpp"
#include "TabularConfig.hpp"


namespace pwiz {
namespace analysis {


/// writes data samples from a single rectangular region 
class PWIZ_API_DECL RegionSIC : public MSDataAnalyzer
{
    public:

    struct PWIZ_API_DECL Config : TabularConfig
    {
        double mzCenter;
        double radius; 
        enum RadiusUnits {RadiusUnits_Unknown, RadiusUnits_amu, RadiusUnits_ppm};
        RadiusUnits radiusUnits;

        Config(const std::string& args); 

        bool operator==(const Config &rhs) 
        {
            return mzCenter==rhs.mzCenter &&
                radius == rhs.radius &&
                radiusUnits == rhs.radiusUnits &&
                delim_equal(rhs);
        }
    };

    RegionSIC(const MSDataCache& cache, const Config& config);

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
struct analyzer_strings<RegionSIC>
{
    static const char* id() {return "sic";}
    static const char* description() {return "write selected ion chromatogram for an m/z and radius";}
#define SIC_MZCENTER_ARG "mzCenter"
#define SIC_RADIUS_ARG "radius"
#define SIC_RADIUSUNITS_ARG "radiusUnits"
    static const char* argsFormat() {return SIC_MZCENTER_ARG "=<mz> " SIC_RADIUS_ARG"=<radius> " SIC_RADIUSUNITS_ARG "=<amu|ppm> [" TABULARCONFIG_DELIMITER_OPTIONS_STR "]";}
    static std::vector<std::string> argsUsage() 
    {
        std::vector<std::string> usage;
        usage.push_back(SIC_MZCENTER_ARG": set mz value");
        usage.push_back(SIC_RADIUS_ARG": set radius value");
        usage.push_back(SIC_RADIUSUNITS_ARG": set units for radius value (must be amu or ppm)");
        usage.push_back(TABULARCONFIG_DELIMITER_USAGE_STR);
        return usage;
    }
};


} // namespace analysis 
} // namespace pwiz


#endif // _REGIONSIC_HPP_

