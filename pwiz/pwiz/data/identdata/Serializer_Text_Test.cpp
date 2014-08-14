//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2010 Spielberg Family Center for Applied Proteomics
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
#include "IdentData.hpp"
#include "Serializer_Text.hpp"
#include "TextWriter.hpp"
#include "examples.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::identdata;
using namespace pwiz::identdata::examples;

ostream* os_ = 0;

void testSerializeRead()
{
    if (os_)
        (*os_) << "*** Beginning testSerializeRead() ***\n";
    
    string testStr = "scan\trt\tmz\tscore\tscoretype\tpeptide\tprotein\n"
        "1\t1.01\t100.1\t0.5\tmascot\tVAGWE\tvague protein\n"
        "2\t2.02\t200.0\t0.9\tmascot\tCERTAIN\tcertain protein\n";

    shared_ptr<istringstream> iss(new istringstream(testStr));
    
    IdentData mzid;
    Serializer_Text serializer;
    serializer.read(iss, mzid);

    
    if (os_)
    {
        (*os_) << "mzIdentML output:\n";
        TextWriter tw(*os_);
        tw(mzid);
    }
    
    if (os_)
        (*os_) << "*** Ending testSerializeRead() ***\n";
}

void testSerializeWrite()
{
    if (os_)
        (*os_) << "*** Beginning testSerializeWrite() ***\n";

    ostringstream oss;
    IdentData mzid;
    initializeBasicSpectrumIdentification(mzid);

    Serializer_Text serializer;
    serializer.write(oss, mzid);

    if (os_)
        (*os_) << oss.str() << endl;

    if (os_)
        (*os_) << "*** Ending testSerializeRead() ***\n";
}

void test()
{
    testSerializeRead();
    testSerializeWrite();
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
