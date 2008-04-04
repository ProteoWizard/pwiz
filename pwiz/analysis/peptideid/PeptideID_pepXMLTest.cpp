//
// PeptideID_pepXMLTest.cpp
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

#include <iostream>
#include <string>

#include "PeptideID_pepXML.hpp"
#include "minimxml/SAXParser.hpp"
#include "util/unit.hpp"

using namespace std;
using namespace pwiz::util;
using namespace pwiz::peptideid;
using namespace pwiz::minimxml::SAXParser;

ostream* os_;

const char* samplePepXML =
    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
    "<msms_pipeline_analysis>\n"
    "<msms_run_summary>\n"
    "<spectrum_query start_scan=\"1\" end_scan=\"2\" retention_time_sec=\"1.0\">\n"
    "<search_result>\n"
    "<search_hit>\n"
    "<modification_info modified_peptide=\"ABC\">\n"
    "<mod_aminoacid_mass position=\"1\" mass=\"111.0320\"/>\n"
    "<mod_aminoacid_mass position=\"15\" mass=\"160.0307\"/>\n"
    "<mod_aminoacid_mass position=\"20\" mass=\"160.0307\"/>\n"
    "</modification_info>\n"
    "<search_score name=\"dotproduct\" value=\"319\"/>\n"
    "<search_score name=\"delta\" value=\"0.091\"/>\n"
    "<search_score name=\"deltastar\" value=\"0\"/>\n"
    "<search_score name=\"zscore\" value=\"0\"/>\n"
    "<search_score name=\"expect\" value=\"5.5\"/>\n"
    "<analysis_result analysis=\"peptideprophet\">\n"
    "<peptideprophet_result probability=\"0.900\">\n"
    "<search_score_summary>\n"
    "<parameter name=\"fval\" value=\"-0.7088\"/>\n"
    "<parameter name=\"ntt\" value=\"2\"/>\n"
    "<parameter name=\"nmc\" value=\"1\"/>\n"
    "<parameter name=\"massd\" value=\"-0.601\"/>\n"
    "</search_score_summary>\n"
    "</peptideprophet_result>\n"
    "</analysis_result>\n"
    "</search_hit>\n"
    "</search_result>\n"
    "</spectrum_query>\n"
    "</msms_run_summary>\n"
    "</msms_pipeline_analysis>\n";

void test()
{
    istringstream xml (samplePepXML);
    PepXMLHandler handler;
    parse(xml, handler);


    auto_vector<BriefFeature>::iterator i;
    BriefFeature* bf = *(handler.getFeatures().begin());

    unit_assert(bf->start_scan == 1);
    unit_assert(bf->end_scan == 2);
    unit_assert_equal(bf->probability, 0.9, 1e-15);
}

int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        //testDone();
        //testBadXML();
        //testNested();
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
