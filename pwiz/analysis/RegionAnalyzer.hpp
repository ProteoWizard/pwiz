//
// RegionAnalyzer.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _REGIONANANALYZER_HPP_ 
#define _REGIONANANALYZER_HPP_ 


#include "MSDataAnalyzer.hpp"
#include "MSDataCache.hpp"


namespace pwiz {
namespace analysis {


/// analyzes a rectangular region of m/z-time space 
class RegionAnalyzer : public MSDataAnalyzer
{
    public:

    struct Config
    {
        std::pair<double,double> mzRange;
        std::pair<size_t,size_t> indexRange;
        std::pair<int,int> scanNumberRange;
        std::pair<double,double> rtRange;
        bool dumpRegionData;
        std::string filenameSuffix;

        Config();
    };

    RegionAnalyzer(const Config& config, const MSDataCache& cache);

    struct SpectrumStats
    {
        double sumIntensity;
        MZIntensityPair max;  // sample point with highest intensity
        MZIntensityPair peak; // interpolated peak

        SpectrumStats();
    };

    const std::vector<SpectrumStats>& spectrumStats() const;

    struct Stats
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

