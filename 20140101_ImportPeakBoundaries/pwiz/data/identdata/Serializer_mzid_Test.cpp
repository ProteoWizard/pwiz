//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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

#define PWIZ_SOURCE

#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "IdentData.hpp"
#include "Serializer_mzid.hpp"
#include "examples.hpp"
#include "Diff.hpp"


using namespace pwiz::identdata;
using namespace pwiz::identdata::examples;
using namespace pwiz::util;


ostream* os_ = 0;


void testSerialize()
{
    if (os_) *os_ << "begin testSerialize\n";
    IdentData mzid;
    initializeTiny(mzid);

    Serializer_mzIdentML ser;
    ostringstream oss;
    ser.write(oss, mzid);

    if (os_) *os_ << oss.str() << endl;

    IdentData mzid2;
    boost::shared_ptr<istream> iss(new istringstream(oss.str()));
    ser.read(iss, mzid2);
    Diff<IdentData, DiffConfig> diff(mzid, mzid2);

    if (os_ && diff) *os_ << diff << endl;
    unit_assert(!diff);
}

void test()
{
    testSerialize();
}

int main(int argc, char** argv)
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}
