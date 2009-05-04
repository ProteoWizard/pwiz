///
/// Matcher.hpp
///

#ifndef _MATCHER_HPP_
#define _MATCHER_HPP_

#include "DataFetcherContainer.hpp"
#include "Match_dataFetcher.hpp"
#include "SearchNeighborhoodCalculator.hpp"

#include "boost/shared_ptr.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/convenience.hpp"

namespace pwiz{
namespace eharmony{

struct Config
{
    std::vector<std::string> filenames; 
    std::string inputPath;
    std::string outputPath;
    std::string batchFileName;

    bool generateAMTDatabase;

    bool rtCalibrate;

    std::string warpFunctionCalculator;
    std::string searchNeighborhoodCalculator;
    std::string normalDistributionSearch;
 
    SearchNeighborhoodCalculator parsedSNC;
    NormalDistributionSearch parsedNDS;
    WarpFunctionEnum warpFunction;

    Config() : inputPath("."), outputPath(".") {}

};

class Matcher
{
public:

    Matcher(){}
    Matcher(Config& config);

    void checkSourceFiles();
    void readSourceFiles();
    void processFiles();
    void msmatchmake(DataFetcherContainer& dfc, SearchNeighborhoodCalculator& snc, MSMSPipelineAnalysis& mspa, string& outputDir);
    void msmatchmake(DataFetcherContainer& dfc, NormalDistributionSearch& nds, MSMSPipelineAnalysis& mspa, string& outputDir);

private:

    Config _config;

    std::map<std::string, PeptideID_dataFetcher> _peptideData;
    std::map<std::string, Feature_dataFetcher> _featureData;

};

} // namespace match
} // namespace pwiz

#endif
