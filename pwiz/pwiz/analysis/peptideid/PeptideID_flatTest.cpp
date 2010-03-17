//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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

#include <iostream>
#include <fstream>
#include <string>
#include <cstring>
#include <exception>
#include <boost/shared_ptr.hpp>

#include "PeptideID_flat.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "pwiz/utility/misc/unit.hpp"

using namespace std;
using namespace boost;
using namespace pwiz::util;
using namespace pwiz::peptideid;
using namespace pwiz::minimxml::SAXParser;

ostream* os_;

const char* sampleFlat =
    "1\t1000.0\t1000.0\t0.9\tABC\n"
    "2\t2000\t500.0\t0.7\tDEF\n";

const char* sampleMSI =
"scan	time	mz	mass	intensity	charge	chargeStates	kl	background	median	peaks	scanFirst	scanLast	scanCount\n"
"1	2.248	878.889	1755.7633	61.847733	2	1	0.05977635	0.9152653	1.0536207	5	693	721	1\n"
"1	2.248	752.86017	1503.7076	41.52021	2	1	0.10636939	1.6415321	0.8086928	5	693	715	1\n"
"1	2.248	933.4445	932.4372	33.840942	1	1	0.2521489	5.717129	2.8336976	2	695	707	1\n"
"4	7.116	801.4013	800.3538	18.389582	1	1	0.6249515	1.6089915	1.3883085	3	698	713	1\n";


void testIStream()
{
    istringstream data (sampleFlat);

    PeptideID_flat ppFlat(&data, shared_ptr<FlatRecordBuilder>(new FlatRecordBuilder));

    PeptideID::Location loc("1", 1000., 0);
    PeptideID::Record bf = ppFlat.record(loc);

    unit_assert(bf.nativeID == "1");
    unit_assert(bf.sequence == "ABC");
    unit_assert_equal(bf.normalizedScore, 0.9, 1e-14);
}

void testMSInspectIStream()
{
    istringstream data (sampleMSI);

    PeptideID_flat ppFlat(&data, shared_ptr<FlatRecordBuilder>(new MSInspectRecordBuilder()));

    PeptideID::Location loc("1", 2.248, 878.889);
    PeptideID::Record bf = ppFlat.record(loc);

    unit_assert(bf.nativeID == "1");
    unit_assert(bf.sequence == "");
    unit_assert_equal(bf.normalizedScore, 0.05977635, 1e-14);
}

//void testFilename()
//{
//    ifstream data ("test.txt");
//
//    PeptideID_flat<> ppFlat(&data);
//
//
//    PeptideID::Location loc("1", 0, 0.9);
//    PeptideID::Record bf = ppFlat.record(loc);
//
//    unit_assert(bf.nativeID == "1");
//    unit_assert(bf.sequence == "ABC");
//    unit_assert_equal(bf.normalizedScore, 0.9, 1e-14);
//}

int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        testIStream();
        testMSInspectIStream();
        //testFilename();
        //testDone();
        //testBadXML();
        //testNested();
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
     
    return 0;
}
