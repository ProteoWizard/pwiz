///
/// MatcherTest.cpp
///

#include "Matcher.hpp"
using namespace pwiz::eharmony;

void test()
{
    Config config;

    config.inputPath = "./fractionData/";
    config.batchFileName = "fractions_63-90.txt";
    //    config.searchNeighborhoodCalculator = "naive[.01,500]";
    config.normalDistributionSearch = "normalDistribution[3]";
    config.warpFunctionCalculator = "piecewiseLinear";
    config.generateAMTDatabase = true;

//     config.inputPath = "/stf/scratch/atrium/kate/20090112/";
//     config.filenames.push_back("20090112-B-JW-BOB2IgY14depleted_Data06_msprefix");
//     config.filenames.push_back("20090112-B-JW-BOB2IgY14depleted_Data08_msprefix");

    Matcher matcher(config);

}

int main()
{
    test();
    return 0;

}
