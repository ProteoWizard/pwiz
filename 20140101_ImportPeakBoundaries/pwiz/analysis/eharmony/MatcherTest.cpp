//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

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
