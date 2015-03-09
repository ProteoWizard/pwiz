//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
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

#include "pwiz/utility/misc/Std.hpp"
#include <cstring>

#include "PeptideID_pepXML.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "pwiz/utility/misc/unit.hpp"

using namespace pwiz::util;
using namespace pwiz::peptideid;
using namespace pwiz::minimxml::SAXParser;

ostream* os_;

const char* samplePepXML =
    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
    "<msms_pipeline_analysis>\n"
    "<msms_run_summary>\n"
    "<spectrum_query start_scan=\"1\" end_scan=\"1\" retention_time_sec=\"1.0\">\n"
    "<search_result>\n"
    "<search_hit peptide=\"ABC\">\n"
    "<analysis_result analysis=\"peptideprophet\">\n"
    "<peptideprophet_result probability=\"0.900\">\n"
    "<search_score_summary>\n"
    "</search_score_summary>\n"
    "</peptideprophet_result>\n"
    "</analysis_result>\n"
    "</search_hit>\n"
    "</search_result>\n"
    "</spectrum_query>\n"
    "</msms_run_summary>\n"
    "</msms_pipeline_analysis>\n";

void testIStream()
{
    istringstream xml (samplePepXML);

    PeptideID_pepXml ppXml(&xml);

    PeptideID::Location loc("1", 1.0, 0.);
    PeptideID::Record bf = ppXml.record(loc);

    unit_assert(bf.nativeID == "1");
    unit_assert(bf.sequence == "ABC");
    unit_assert_equal(bf.normalizedScore, 0.9, 1e-15);
}

void testFilename()
{
    ifstream xml ("test.pep.xml");

    PeptideID_pepXml ppXml(&xml);


    PeptideID::Location loc("1", 1.0, 0.);
    PeptideID::Record bf = ppXml.record(loc);

    unit_assert(bf.nativeID == "1");
    unit_assert(bf.sequence == "ABC");
    unit_assert_equal(bf.normalizedScore, 0.9, 1e-15);
}

int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        testIStream();
        //testFilename();
        //testDone();
        //testBadXML();
        //testNested();
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
