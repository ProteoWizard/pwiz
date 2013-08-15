//
// $Id$
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

#include "Pep2MzIdent.hpp"
#include "Serializer_mzid.hpp"
#include <iostream>
#include <string>

using namespace pwiz;
using namespace pwiz::identdata;
using namespace pwiz::data::pepxml;

ostream* os_ = NULL;

const char* samplePepXML =
    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
    "<msms_pipeline_analysis>\n"
    "<msms_run_summary>\n"
    "<spectrum_query start_scan=\"1\" end_scan=\"2\" precursor_neutral_mass=\"1.0\" assumed_charge=\"1\" retention_time_sec=\"2.0\">\n"
    "<search_result>\n"
    "<search_hit peptide=\"BUCKLEMYSHOE\">\n"
    "<analysis_result analysis=\"peptideprophet\">\n"
    "<peptideprophet_result probability=\"0.900\" all_ntt_prob=\"(0,0,0.900)\">\n"
    "<search_score_summary>\n"
    "</search_score_summary>\n"
    "</peptideprophet_result>\n"
    "</analysis_result>\n"
    "</search_hit>\n"
    "</search_result>\n"
    "</spectrum_query>\n"
    "<spectrum_query start_scan=\"3\" end_scan=\"4\" precursor_neutral_mass=\"3.0\" assumed_charge=\"1\" retention_time_sec=\"4.0\">\n"
    "<search_result>\n"
    "<search_hit peptide=\"SHUTTHEDOOR\">\n"
    "<analysis_result analysis=\"peptideprophet\">\n"
    "<peptideprophet_result probability=\"0.900\" all_ntt_prob=\"(0,0,0.900)\">\n"
    "<search_score_summary>\n"
    "</search_score_summary>\n"
    "</peptideprophet_result>\n"
    "</analysis_result>\n"
    "</search_hit>\n"
    "</search_result>\n"
    "</spectrum_query>\n"
    "<spectrum_query start_scan=\"5\" end_scan=\"6\" precursor_neutral_mass=\"5.0\" assumed_charge=\"1\" retention_time_sec=\"6.0\">\n"
    "<search_result>\n"
    "<search_hit peptide=\"PICKUPSTICKS\">\n"
    "<analysis_result analysis=\"peptideprophet\">\n"
    "<peptideprophet_result probability=\"0.900\" all_ntt_prob=\"(0,0,0.900)\">\n"
    "<search_score_summary>\n"
    "</search_score_summary>\n"
    "</peptideprophet_result>\n"
    "</analysis_result>\n"
    "</search_hit>\n"
    "</search_result>\n"
    "</spectrum_query>\n"
    "<spectrum_query start_scan=\"7\" end_scan=\"8\" precursor_neutral_mass=\"7.0\" assumed_charge=\"1\" retention_time_sec=\"8.0\">\n"
    "<search_result>\n"
    "<search_hit peptide=\"LAYTHEMSTRAIGHT\">\n"
    "<analysis_result analysis=\"peptideprophet\">\n"
    "<peptideprophet_result probability=\"0.900\" all_ntt_prob=\"(0,0,0.900)\">\n"
    "<search_score_summary>\n"
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
    istringstream iss(samplePepXML);
    MSMSPipelineAnalysis mspa;
    mspa.read(iss);

    Pep2MzIdent translator(mspa);
    IdentDataPtr result(translator.translate());

    Serializer_mzIdentML serializer;
    ostringstream oss;
    serializer.write(oss, *result);

    if (os_)
        *os_ << oss.str() << endl;

}

int main(int argc, char** argv)
{
    if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;

    cout << "\ntesting Pep2MzIdent ... \n" << endl;
    test();
    return 0;
}
