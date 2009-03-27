///
/// PeptideMatcherTest.cpp
///

#include "PeptideMatcher.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <utility>
#include <vector>

using namespace std;
using namespace pwiz;
using namespace pwiz::eharmony;
using namespace pwiz::util;

ostream* os_ = 0;

const char* samplePepXML_a =
    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
    "<msms_pipeline_analysis>\n"
    "<msms_run_summary>\n"
    "<spectrum_query start_scan=\"1\" end_scan=\"2\" precursor_neutral_mass=\"1.0\" assumed_charge=\"1\" retention_time_sec=\"2.0\">\n"
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
    "<spectrum_query start_scan=\"9\" end_scan=\"10\" precursor_neutral_mass=\"9.0\" assumed_charge=\"1\" retention_time_sec=\"2.0\">\n"
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
    "<spectrum_query start_scan=\"1\" end_scan=\"2\" precursor_neutral_mass=\"1.0\" assumed_charge=\"1\" retention_time_sec=\"2.0\">\n"
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
    "<spectrum_query start_scan=\"9\" end_scan=\"10\" precursor_neutral_mass=\"9.0\" assumed_charge=\"1\" retention_time_sec=\"2\">\n"
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


PeptideID_dataFetcher makePeptideID_dataFetcher(const char* samplePepXML)
{
    istringstream iss(samplePepXML);
    PeptideID_dataFetcher pidf(iss);

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

    XResult xresult;
    xresult.probability = score;
    xresult.allNttProb.push_back(0);
    xresult.allNttProb.push_back(0);
    xresult.allNttProb.push_back(score);

    analysisResult.xResult = xresult;

    searchHit.analysisResult = analysisResult;
    searchResult.searchHit = searchHit;

    spectrumQuery.searchResult = searchResult;

    return spectrumQuery;

}


Feature makeFeature(double mz, double retentionTime)
{
    Feature feature;
    feature.mzMonoisotopic = mz;
    feature.retentionTime = retentionTime;

    return feature;

}

void test()
{
    // construct test fdfs

    Feature a = makeFeature(1,2.1);
    Feature b = makeFeature(3,4.2);
    Feature c = makeFeature(5,6.4);
    Feature d = makeFeature(7,8.8);
    Feature e = makeFeature(9,1.9);

    vector<Feature> features_a;
    features_a.push_back(a);
    features_a.push_back(b);
    features_a.push_back(e);

    vector<Feature> features_b;
    features_b.push_back(c);
    features_b.push_back(d);
    features_b.push_back(e);

    Feature_dataFetcher fdf_a(features_a);
    Feature_dataFetcher fdf_b(features_b);

    // construct pidfs
    PeptideID_dataFetcher pidf_a = makePeptideID_dataFetcher(samplePepXML_a);
    PeptideID_dataFetcher pidf_b = makePeptideID_dataFetcher(samplePepXML_b);

    // construct PeptideMatcher
    DataFetcherContainer dfc(pidf_a, pidf_b, fdf_a, fdf_b);
    PeptideMatcher pm(dfc);
    PeptideMatchContainer pmc = pm.getMatches();
  
    // ensure that _matches attribute is filled in 
    unit_assert(pmc.size() != 0); 

    // and correctly:
    // construct the objects that should be in matches
    
    SpectrumQuery sq_a = makeSpectrumQuery(1,2,1,"BUCKLEMYSHOE",0.900,1,2);
    SpectrumQuery sq_b = makeSpectrumQuery(1,2,1,"BUCKLEMYSHOE",0.900,1,2);
    
    SpectrumQuery sq_c = makeSpectrumQuery(9,2,1,"ABIGFATHEN",0.900,9,10);
    SpectrumQuery sq_d = makeSpectrumQuery(9,2,1,"ABIGFATHEN",0.900,9,10);
    
    // assert that known matches are found
    unit_assert(find(pmc.begin(), pmc.end(), make_pair(sq_a, sq_b)) != pmc.end());
    unit_assert(find(pmc.begin(), pmc.end(), make_pair(sq_c, sq_d)) != pmc.end());

    // This unit test really exemplifies the fact that my private variables are insignificant since i'm allowing all my other classes to access them, I need to figure out how to encapsulate these variables in something accessible from the outside without just passing them thru, e.g. calc p val of something member function
//     //  calculate deltaRtDistribution by hand
//     double mean = 2.15;
//     double stdev = 2.15;

//     // and validate that it is correct
//     pm.calculateDeltaRTDistribution();
    
//     unit_assert_equal(pm.getDeltaRTParams().first, mean, 2 * numeric_limits<double>::epsilon());
//     unit_assert_equal(pm.getDeltaRTParams().second, stdev, 2 * numeric_limits<double>::epsilon());

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
