//
// RegionSlice.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _REGIONSLICE_HPP_
#define _REGIONSLICE_HPP_


#include "MSDataAnalyzer.hpp"
#include "MSDataCache.hpp"
#include "RegionAnalyzer.hpp"


namespace pwiz {
namespace analysis {


/// writes data samples from a single rectangular region 
class RegionSlice : public MSDataAnalyzer
{
    public:

    struct Config : public RegionAnalyzer::Config
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
    static const char* description() {return "write data from a rectangular region";}
    static const char* argsFormat() {return "[mz=[a,b]] [rt=[a,b]] [index=[a,b]] [sn=[a,b]]";}
    static std::vector<std::string> argsUsage()
    {
        std::vector<std::string> result;
        result.push_back("mz=[a,b] (set m/z range)");
        result.push_back("rt=[a,b] (set retention time range)");
        result.push_back("index=[a,b] (set spectrum index range)");
        result.push_back("sn=[a,b] (set scan number range)");
        return result;
    }
};


} // namespace analysis 
} // namespace pwiz


#endif // _REGIONSLICE_HPP_

