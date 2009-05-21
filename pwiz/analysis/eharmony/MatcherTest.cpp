///
/// MatcherTest.cpp
///

#include "Matcher.hpp"
using namespace pwiz::eharmony;

void test()
{
    Config config;

    config.inputPath = "./2007/";
    config.batchFileName = "18mix_runs.txt";
    config.searchNeighborhoodCalculator = "naive[.01,500]";
    config.warpFunctionCalculator = "linear";
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
