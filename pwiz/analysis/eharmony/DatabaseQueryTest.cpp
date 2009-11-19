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
/// DatabaseQueryTest.cpp
///

#include "DatabaseQuery.hpp"
#include "pwiz/utility/misc/unit.hpp"

using namespace pwiz;
using namespace eharmony;
using namespace pwiz::util;

void test()
{
    double mu1 = 1;
    double mu2 = 2;
    double sigma1 = 2;
    double sigma2 = 4;
    double threshold = 0.7;
    
    DatabaseQuery dbQuery(PidfPtr(new PeptideID_dataFetcher()));

    // Given a normal distribution fit to mz and rt differences, calculate the folded normal distribution correspoding to the parameters of the original distribution.  Using this distribution and an explicit approximation to the error function, calculate the region of mz x rt space that it is necessary to search in order to find all the matches that would score higher than the given threshold.

    pair<double,double> radii = dbQuery.calculateSearchRegion(mu1, mu2, sigma1, sigma2, threshold);    
    unit_assert_equal(radii.first, 10.047046209584696, .000001);

}

int main()
{
    test();
    return 0;
}
