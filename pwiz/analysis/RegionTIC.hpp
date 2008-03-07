//
// RegionTIC.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _REGIONTIC_HPP_
#define _REGIONTIC_HPP_


#include "MSDataAnalyzer.hpp"
#include "MSDataCache.hpp"
#include "RegionAnalyzer.hpp"


namespace pwiz {
namespace analysis {


/// writes data samples from a single rectangular region 
class RegionTIC : public MSDataAnalyzer
{
    public:

    struct Config
    {
        std::pair<double,double> mzRange;
        Config(const std::string& args); 
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
    static const char* argsFormat() {return "[mzLow [mzHigh]]";}
    static std::vector<std::string> argsUsage() {return std::vector<std::string>();}
};


} // namespace analysis 
} // namespace pwiz


#endif // _REGIONTIC_HPP_

