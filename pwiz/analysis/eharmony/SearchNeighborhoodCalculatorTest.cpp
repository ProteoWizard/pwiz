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
/// SearchNeighborhoodCalculatorTest.cpp
///

#include "SearchNeighborhoodCalculator.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"

using namespace pwiz::eharmony;
using namespace pwiz::util;

ostream* os_ = 0;

void testNormalDistribution()
{
    // TODO
    // Test that folded normal distribution is correctly calculated from normal distribution parameters
    // Test that scoring function is correct
}

void test()
{
    if (os_) *os_ << "test() ..." << endl;

    SearchNeighborhoodCalculator snc;

    SpectrumQuery sq;
    sq.precursorNeutralMass = 1;
    sq.assumedCharge = 2;
    sq.retentionTimeSec = 40;

    Feature f;
    f.mz = 1.510;
    f.retentionTime = 98;

    unit_assert(snc.close(sq,f));
    if (os_)
        {
            XMLWriter writer(*os_);
            sq.write(writer);
            f.write(writer);

        }
}

int main(int argc, char* argv[])
{
    try
        {
            if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
            if (os_) *os_ << "SearchNeighborhoodCalculatorTest: " << endl;
            test();
            testNormalDistribution();

        }

    catch (std::exception& e)
        {
            cerr << e.what() << endl;
            return 1;

        }

    catch (...)
        {
            cerr << "Caught unknown exception.\n";
            return 1;

        }

}

