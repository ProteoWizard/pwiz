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

#include "PeptideMatcher.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::cv;
using namespace pwiz::eharmony;
using namespace pwiz::util;

ostream* os_ = 0;

const char* samplePepXML_a =
    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
    "<msms_pipeline_analysis>\n"
    "<msms_run_summary>\n"
    "<spectrum_query start_scan=\"1\" end_scan=\"2\" precursor_neutral_mass=\"2.0\" assumed_charge=\"1\" retention_time_sec=\"2.0\">\n"
    "<search_result>\n"
    "<search_hit peptide=\"BUCKLEMYSHOE\">\n"
    "<analysis_result analysis=\"peptideprophet\">\n"
    "<peptideprophet_result probability =\"0.900\" all_ntt_prob=\"(0,0,0.900)\">\n"
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
    "<peptideprophet_result probability =\"0.900\" all_ntt_prob=\"(0,0,0.900)\">\n"
    "<search_score_summary>\n"
    "</search_score_summary>\n"
    "</peptideprophet_result>\n"
    "</analysis_result>\n"
    "</search_hit>\n"
    "</search_result>\n"
    "</spectrum_query>\n"
    "<spectrum_query start_scan=\"9\" end_scan=\"10\" precursor_neutral_mass=\"2.0\" assumed_charge=\"1\" retention_time_sec=\"4.0\">\n"
    "<search_result>\n"
    "<search_hit peptide=\"ABIGFATHEN\">\n"
    "<analysis_result analysis=\"peptideprophet\">\n"
    "<peptideprophet_result probability =\"0.900\" all_ntt_prob=\"(0,0,0.900)\">\n"
    "<search_score_summary>\n"
    "</search_score_summary>\n"
    "</peptideprophet_result>\n"
    "</analysis_result>\n"
    "</search_hit>\n"
    "</search_result>\n"
    "</spectrum_query>\n"
    "</msms_run_summary>\n"
    "</msms_pipeline_analysis>\n";

const char* samplePepXML_b =
    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
    "<msms_pipeline_analysis>\n"
    "<msms_run_summary>\n"
    "<spectrum_query start_scan=\"1\" end_scan=\"2\" precursor_neutral_mass=\"2.1\" assumed_charge=\"1\" retention_time_sec=\"3.0\">\n"
    "<search_result>\n"
    "<search_hit peptide=\"BUCKLEMYSHOE\">\n"
    "<analysis_result analysis=\"peptideprophet\">\n"
    "<peptideprophet_result probability =\"0.900\" all_ntt_prob=\"(0,0,0.900)\">\n"
    "<search_score_summary>\n"
    "</search_score_summary>\n"
    "</peptideprophet_result>\n"
    "</analysis_result>\n"
    "</search_hit>\n"
    "</search_result>\n"
    "</spectrum_query>\n"
    "<spectrum_query start_scan=\"7\" end_scan=\"8\" precursor_neutral_mass=\"3.0\" assumed_charge=\"1\" retention_time_sec=\"4.0\">\n"
    "<search_result>\n"
    "<search_hit peptide=\"LAYTHEMSTRAIGHT\">\n"
    "<analysis_result analysis=\"peptideprophet\">\n"
    "<peptideprophet_result probability =\"0.900\" all_ntt_prob=\"(0,0,0.900)\">\n"
    "<search_score_summary>\n"
    "</search_score_summary>\n"
    "</peptideprophet_result>\n"
    "</analysis_result>\n"
    "</search_hit>\n"
    "</search_result>\n"
    "</spectrum_query>\n"
    "<spectrum_query start_scan=\"9\" end_scan=\"10\" precursor_neutral_mass=\"2.2\" assumed_charge=\"1\" retention_time_sec=\"2.0\">\n"
    "<search_result>\n"
    "<search_hit peptide=\"ABIGFATHEN\">\n"
    "<analysis_result analysis=\"peptideprophet\">\n"
    "<peptideprophet_result probability =\"0.900\" all_ntt_prob=\"(0,0,0.900)\">\n"
    "<search_score_summary>\n"
    "</search_score_summary>\n"
    "</peptideprophet_result>\n"
    "</analysis_result>\n"
    "</search_hit>\n"
    "</search_result>\n"
    "</spectrum_query>\n"
    "</msms_run_summary>\n"
"</msms_pipeline_analysis>\n";


PidfPtr makePeptideID_dataFetcher(const char* samplePepXML)
{
    istringstream iss(samplePepXML);
    PidfPtr pidf(new PeptideID_dataFetcher(iss));

    return pidf;

}

SpectrumQuery makeSpectrumQuery(double precursorNeutralMass, double rt, int charge, string sequence, double score, int startScan, int endScan)
{
    SpectrumQuery spectrumQuery;
    spectrumQuery.startScan = startScan;
    spectrumQuery.endScan = endScan;
    spectrumQuery.precursorNeutralMass = precursorNeutralMass;
    spectrumQuery.assumedCharge = charge;
    spectrumQuery.retentionTimeSec = rt;

    SearchResult searchResult;

    SearchHit searchHit;
    searchHit.peptide = sequence;


    AnalysisResult analysisResult;
    analysisResult.analysis = "peptideprophet";

    PeptideProphetResult peptideProphetResult;
    peptideProphetResult.probability = score;
    peptideProphetResult.allNttProb.push_back(0);
    peptideProphetResult.allNttProb.push_back(0);
    peptideProphetResult.allNttProb.push_back(score);

    analysisResult.peptideProphetResult = peptideProphetResult;

    searchHit.analysisResult = analysisResult;
    searchResult.searchHit = searchHit;

    spectrumQuery.searchResult = searchResult;

    return spectrumQuery;

}


FeaturePtr makeFeature(double mz, double retentionTime)
{
    FeaturePtr feature(new Feature());
    feature->mz = mz;
    feature->retentionTime = retentionTime;

    return feature;

}

void test()
{
    // construct test fdfs
    FeaturePtr a = makeFeature(1,2.1);
    FeaturePtr b = makeFeature(3,4.2);
    FeaturePtr c = makeFeature(5,6.4);
    FeaturePtr d = makeFeature(7,8.8);
    FeaturePtr e = makeFeature(9,1.9);

    vector<FeaturePtr> features_a;
    features_a.push_back(a);
    features_a.push_back(b);
    features_a.push_back(e);

    vector<FeaturePtr> features_b;
    features_b.push_back(c);
    features_b.push_back(d);
    features_b.push_back(e);

    Feature_dataFetcher fdf_a(features_a);
    Feature_dataFetcher fdf_b(features_b);

    // construct pidfs
    PidfPtr pidf_a = makePeptideID_dataFetcher(samplePepXML_a);
    PidfPtr pidf_b = makePeptideID_dataFetcher(samplePepXML_b);

    // construct PeptideMatcher
    //    DataFetcherContainer dfc(pidf_a, pidf_b, fdf_a, fdf_b);
    PeptideMatcher pm(pidf_a, pidf_b);
    PeptideMatchContainer pmc = pm.getMatches();
  
    // ensure that _matches attribute is filled in 
    unit_assert(pmc.size() != 0); 

    // and correctly:
    // construct the objects that should be in matches
    
    SpectrumQuery sq_a = makeSpectrumQuery(2.0,2.0,1,"BUCKLEMYSHOE",0.900,1,2);
    SpectrumQuery sq_b = makeSpectrumQuery(2.1,3.0,1,"BUCKLEMYSHOE",0.900,1,2);
    
    SpectrumQuery sq_c = makeSpectrumQuery(2.0,4.0,1,"ABIGFATHEN",0.900,9,10);
    SpectrumQuery sq_d = makeSpectrumQuery(2.2,2.0,1,"ABIGFATHEN",0.900,9,10);
    
    PeptideMatchContainer::iterator it = pmc.begin();
    if (os_)
        {
	    *os_ << "\n[PeptideMatcherTest] Matches found:\n " << endl;
	    ostringstream oss;
	    XMLWriter writer(oss);
	    PeptideMatchContainer::iterator it = pmc.begin();
            for(; it != pmc.end(); ++it)
                {
		    it->first->write(writer);
		    it->second->write(writer);
		  
		}	    

	    oss << "\n[PeptideMatcherTest] Looking for:\n " << endl;
	    sq_a.write(writer);
	    sq_b.write(writer);
	    
	    *os_ << oss.str() << endl;
        }

    // assert that known matches are found TODO fix
    /*
    unit_assert(find(pmc.begin(), pmc.end(), make_pair(sq_a, sq_b)) != pmc.end());
    unit_assert(find(pmc.begin(), pmc.end(), make_pair(sq_c, sq_d)) != pmc.end());
    */

     //  calculate deltaRtDistribution by hand
     double mean = 0.5;
     double stdev = 1.5;
 
     // and verify that it is correct
     pm.calculateDeltaRTDistribution();
    
     unit_assert_equal(pm.getDeltaRTParams().first, mean, 2 * numeric_limits<double>::epsilon());
     unit_assert_equal(pm.getDeltaRTParams().second, stdev, 2 * numeric_limits<double>::epsilon());

     // calculate deltaMZDistribution by  hand
     double mz_mean = -0.15;
     double mz_stdev = 0.05;

     // and verify that it is correct
     pm.calculateDeltaMZDistribution();

     unit_assert_equal(pm.getDeltaMZParams().first, mz_mean, 2 * numeric_limits<double>::epsilon());
     unit_assert_equal(pm.getDeltaMZParams().second, mz_stdev, 2 * numeric_limits<double>::epsilon());

}

int main(int argc, char* argv[])
{
    try
        {
            if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
            test();

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
