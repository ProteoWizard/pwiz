///
/// Matcher.hpp
///

#ifndef _MATCHER_HPP_
#define _MATCHER_HPP_

#include "DataFetcherContainer.hpp"
#include "SearchNeighborhoodCalculator.hpp"

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
    bool rtCalibrate;
    
    std::vector<std::string> warpFunctionCalculators;
    std::vector<std::string> searchNbhdCalculators;

    std::vector<SearchNeighborhoodCalculator> parsedSNCs;

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

private:

    Config _config;

    std::map<std::string, PeptideID_dataFetcher> _peptideData;
    std::map<std::string, Feature_dataFetcher> _featureData;

};

} // namespace match
} // namespace pwiz

#endif
