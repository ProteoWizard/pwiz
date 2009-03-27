///
/// Profile.cpp
///

#include "Matcher.hpp"

using namespace pwiz::eharmony;

void test()
{
    Config config;

    config.inputPath = "/stf/scratch/atrium/kate/ISB18/";
    config.filenames.push_back("20080410-A-18Mix_Data06_msprefix");
    config.filenames.push_back("20080410-A-18Mix_Data08_msprefix");

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
