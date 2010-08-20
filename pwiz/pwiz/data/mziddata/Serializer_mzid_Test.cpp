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

#include "MzIdentML.hpp"
#include "Serializer_mzid.hpp"
#include "examples.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::mziddata;
using namespace pwiz::mziddata::examples;

ostream* os_ = 0;


void testSerialize()
{
    /*
    cerr << "begin testSerialize\n";
    MzIdentML mzid;
    initializeTiny(mzid);

    Serializer_mzIdentML ser;
    ostringstream oss;
    ser.write(oss, mzid);

    //cout << oss.str();
    // TODO finish adding something useful
    */
}

void test()
{
    testSerialize();
}

int main(int argc, char** argv)
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }
    
    return 1;
}
