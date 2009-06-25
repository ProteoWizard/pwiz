///
/// Matcher.hpp
///

#ifndef _MATCHER_HPP_
#define _MATCHER_HPP_


#include "DataFetcherContainer.hpp"
#include "Match_dataFetcher.hpp"
#include "SearchNeighborhoodCalculator.hpp"
#include "NeighborJoiner.hpp"
//#include "Matrix.hpp"
#include "boost/shared_ptr.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/convenience.hpp"

namespace pwiz{
namespace eharmony{

class Matcher
{
public:

    Matcher(){}
    Matcher(Config& config);

    void checkSourceFiles();
    void readSourceFiles();
    void processFiles();
    //    void msmatchmake(DataFetcherContainer& dfc, SearchNeighborhoodCalculator& snc, MSMSPipelineAnalysis& mspa, string& outputDir);


private:

    Config _config;

    std::map<std::string, PeptideID_dataFetcher> _peptideData;
    std::map<std::string, Feature_dataFetcher> _featureData;

};

} // namespace match
} // namespace pwiz

#endif
