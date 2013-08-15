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

#include "KwCVMap.hpp"
#include "examples.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::util;
using namespace pwiz::data;
using namespace pwiz::identdata;

ostream* os_ = 0;
//const double epsilon = numeric_limits<double>::epsilon();

void testCVMap()
{
    if (os_) (*os_) << "\ntestCVMap()\n";

    CVMap map("sample name", MS_sample_name,
              "/mzIdentML/AnalysisSampleCollection/Sample/cvParam");

    unit_assert(map.keyword == "sample name");
    unit_assert(map.cvid == MS_sample_name);
    unit_assert(map("sample name"));
    unit_assert(!map("potato"));
}

void testRegexCVMap()
{
    if (os_) (*os_) << "\ntestRegexCVMap()\n";
    
    RegexCVMap rx("[Ss]ample[ ]+[Nn]ame", MS_sample_name,
                  "/mzIdentML/AnalysisSampleCollection/Sample/cvParam");

    //unit_assert(map.keyword == "sample name");
    unit_assert(rx.cvid == MS_sample_name);
    unit_assert(rx("sample name"));
    unit_assert(rx("Sample name"));
    unit_assert(rx("sample Name"));
    unit_assert(rx("Sample    Name"));
    unit_assert(!rx("turnip"));
}

void testCVMapIO()
{
    if (os_) (*os_) << "\ntestCVMapIO()\n";

    CVMap map("sample name", MS_sample_name,
              "/mzIdentML/AnalysisSampleCollection/Sample/cvParam");
    stringstream ss;

    ss << map;

    if (os_) (*os_) << "insertion operator:\n" << ss.str();
    unit_assert(ss.str() == "plain\tsample name\tMS:1000002"
                "\t/mzIdentML/AnalysisSampleCollection/Sample/cvParam\n");

    // Test CVMapPtr extraction
    CVMapPtr cvmPtr;
    ss >>  cvmPtr;

    if (os_) (*os_) << "NULL pointer returned?"
                    << (cvmPtr.get() == NULL) << endl;
    unit_assert(cvmPtr.get());
    
    if (os_) (*os_) << typeid(cvmPtr.get()).name() << endl;
    unit_assert(typeid(cvmPtr.get()).name() == typeid(CVMap*).name());
    
    if (os_) (*os_) << "keyword: " << cvmPtr->keyword << endl;
    if (os_) (*os_) << "cvid: " << cvmPtr->cvid << endl;
    unit_assert(cvmPtr->keyword == "sample name");
    unit_assert(cvmPtr->cvid == MS_sample_name);
}

void testRegexCVMapIO()
{
    if (os_) (*os_) << "\ntestRegexCVMapIO()\n";
    
    RegexCVMap map("[Ss]ample [Nn]ame", MS_sample_name,
              "/mzIdentML/AnalysisSampleCollection/Sample/cvParam");
    stringstream ss;

    ss << map;

    if (os_) (*os_) << "insertion operator:\n" << ss.str();
    unit_assert(ss.str() == "regex\t[Ss]ample [Nn]ame\tMS:1000002\t"
                "/mzIdentML/AnalysisSampleCollection/Sample/cvParam\n");

    // Test CVMapPtr extraction
    CVMapPtr cvmPtr;
    ss >>  cvmPtr;

    if (os_) (*os_) << "NULL pointer returned?"
                    << (cvmPtr.get() == NULL) << endl;
    unit_assert(cvmPtr.get());
    
    if (os_) (*os_) << typeid(cvmPtr.get()).name() << endl;
    unit_assert(typeid(cvmPtr.get()).name() == typeid(CVMap*).name());
    
    if (os_) (*os_) << "keyword: " << cvmPtr->keyword << endl;
    if (os_) (*os_) << "cvid: " << cvmPtr->cvid << endl;
    unit_assert(cvmPtr->keyword == "[Ss]ample [Nn]ame");
    unit_assert(cvmPtr->cvid == MS_sample_name);
}

void testVectorIO()
{
    if (os_) (*os_) << "\ntestVectorIO()\n";
    
    const char* file =
        "plain\tsample name\tMS:1000002\t/mzIdentML/AnalysisSampleCollection/Sample/cvParam\n"
        "regex\t[Aa]ccuracy[ ]*\tMS:1000014\t/mzIdentML\n"
        "regex\t[Ss]can start time[\\.]?\tMS:1000016\t/mzIdentML\n";

    if (os_) (*os_) << "file used:\n" << file << endl;
    istringstream iss(file);

    vector<CVMapPtr> mappings;
    iss >> mappings;

    if (os_) (*os_) << "Records read in:\n";
    for (vector<CVMapPtr>::iterator i=mappings.begin(); i!=mappings.end(); i++)
    {
        if (os_) (*os_) << *i;
    }

    // TODO add some record specific checking here.
    
    ostringstream oss;

    oss << mappings;

    if (os_) (*os_) << "\nResulting vector output:\n";
    if (os_) (*os_) << oss.str() << endl;
    unit_assert(oss.str() == file);
}

void test()
{
    testCVMap();
    testRegexCVMap();
    testCVMapIO();
    testRegexCVMapIO();
    testVectorIO();
}

int main(int argc, char* argv[])
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

