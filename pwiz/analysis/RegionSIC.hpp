//
// RegionSIC.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _REGIONSIC_HPP_
#define _REGIONSIC_HPP_


#include "MSDataAnalyzer.hpp"
#include "MSDataCache.hpp"
#include "RegionAnalyzer.hpp"


namespace pwiz {
namespace analysis {


/// writes data samples from a single rectangular region 
class RegionSIC : public MSDataAnalyzer
{
    public:

    struct Config
    {
        double mzCenter;
        double radius; 
        enum RadiusUnits {RadiusUnits_Unknown, RadiusUnits_amu, RadiusUnits_ppm};
        RadiusUnits radiusUnits;

        Config(const std::string& args); 
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
    static const char* argsFormat() {return "mzCenter radius (\"amu\"|\"ppm\")";}
    static std::vector<std::string> argsUsage() {return std::vector<std::string>();}
};


} // namespace analysis 
} // namespace pwiz


#endif // _REGIONSIC_HPP_

