///
/// Peptide2FeatureMatcherTest.cpp
///

#include "Peptide2FeatureMatcher.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"

using namespace std;
using namespace pwiz;
using namespace pwiz::eharmony;
using namespace pwiz::data::pepxml;
using namespace pwiz::util;

ostream* os_ = 0;

const char* firstTestPepXML =     
    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
    "<msms_pipeline_analysis>\n"
    "<msms_run_summary>\n"
    "<spectrum_query start_scan=\"1\" end_scan=\"2\" precursor_neutral_mass=\"32.975191529999996\" assumed_charge=\"1\" retention_time_sec=\"118.441016\">\n"
    "<search_result>\n"
    "<search_hit peptide=\"MARINA\">\n"
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


PeptideID_dataFetcher makePeptideID_dataFetcher(const char* samplePepXML)
{
    istringstream iss(samplePepXML);
    PeptideID_dataFetcher pidf(iss);

    return pidf;

}

const char* secondTestPepXML =
    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
    "<msms_pipeline_analysis>\n"
    "<msms_run_summary>\n"
    "<spectrum_query start_scan=\"1\" end_scan=\"2\" precursor_neutral_mass=\"32.97512353\" assumed_charge=\"1\" retention_time_sec=\"118.5\">\n"
    "<search_result>\n"
    "<search_hit peptide=\"MARINA\">\n"
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

const char* thirdTestPepXML =
    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
    "<msms_pipeline_analysis>\n"
    "<msms_run_summary>\n"
    "<spectrum_query start_scan=\"1\" end_scan=\"2\" precursor_neutral_mass=\"32.975191529999996\" assumed_charge=\"1\" retention_time_sec=\"18.441016\">\n"
    "<search_result>\n"
    "<search_hit peptide=\"MARINA\">\n"
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


Feature makeFeature(double mz, double retentionTime, string ms1_5, int charge = 0)
{
    Feature feature;
    feature.mzMonoisotopic = mz;
    feature.retentionTime = retentionTime;
    feature.charge = charge;

    return feature;

}


void testNaive()
{
    if (os_) *os_ << "\ntestNaive() ... \n";

    // first test: test that matches that should be found are, and correctly
 
    // read pepxml
    PeptideID_dataFetcher pidf_a = makePeptideID_dataFetcher(firstTestPepXML);
    PeptideID_dataFetcher pidf_a2 = makePeptideID_dataFetcher(secondTestPepXML);
    PeptideID_dataFetcher pidf_a3 = makePeptideID_dataFetcher(thirdTestPepXML);

    // make candidate features for all tests
    Feature feat_b1  = makeFeature(33.98246, 117.9, "MARINA", 1); // matches but with large rt dif
    Feature feat_b2 = makeFeature(33.9824, 118.5, "MARINA", 1); // matches with small rt diff and mz dif (should get picked)
    Feature feat_b3 = makeFeature(33.988, 118.5, "MARINA", 1);

    // make Feature_dataFetcher for first test
    vector<Feature> feats_b;
    feats_b.push_back(feat_b1);
    feats_b.push_back(feat_b2);
    Feature_dataFetcher fdf_b(feats_b);

    // make Feature_dataFetcher for second and third test
    vector<Feature> feats_b2;
    feats_b2.push_back(feat_b3);
    Feature_dataFetcher fdf_b2(feats_b2);

    // make peptide records that we want to find / not find
    SpectrumQuery sq_1 = makeSpectrumQuery(32.975191529999996,118.441016,1,"MARINA_ms1_5",0.900,1,2);
    SpectrumQuery sq_2 = makeSpectrumQuery(32.97512353, 118.5, 1, "MARINA_ms1_5", 0.900,1,2);
    SpectrumQuery sq_3 = makeSpectrumQuery(32.975191529999996, 18.441016, 1, "MARINA_ms1_5", 0.900, 1, 2);  

    SearchNeighborhoodCalculator snc;

    // construct P2FMs for testing
    Peptide2FeatureMatcher p2fm(pidf_a, fdf_b, snc);
    Peptide2FeatureMatcher p2fm2(pidf_a2, fdf_b2, snc);
    Peptide2FeatureMatcher p2fm3(pidf_a3, fdf_b2, snc);

    // do the tests (make separate functions?)
    vector<Match> matches = p2fm.getMatches();
  
    Match match(sq_1, feat_b2);
    match.score = snc.score(sq_1, feat_b2);

    if (os_)
        {
            os_ = &cout;
            XMLWriter writer(*os_);
            *os_ << "\nTesting that potential matches that fit the mz and rt tolerance are found ... \n";
            *os_ << "\nLooking for: \n";
            match.write(writer);
            *os_ << "\nFound: \n";
            vector<Match>::iterator it = matches.begin();
            for(; it != matches.end(); ++it)
                {
                    it->write(writer);

                }

        }

    unit_assert(find(matches.begin(), matches.end(), match) !=  matches.end());

    // second test: test that potential matches that don't fit the mz tolerance fail

    // get the resulting matches
    vector<Match> matches2 = p2fm2.getMatches();
        
    // assert that the incorrect one was not found
    Match match2(sq_2, feat_b3);
    match2.score = snc.score(sq_2, feat_b3);
    
    if (os_)
        {
            *os_ << "\nTesting that potential matches that don't fit the mz tolerance are not found ... \n";
            XMLWriter writer(*os_);
            *os_ << "\nLooking for the absence of: \n";
            match2.write(writer);
            *os_ << "\nFound: \n";
            vector<Match>::iterator it = matches2.begin();
            for(; it != matches2.end(); ++it)
                {
                    it->write(writer);

                }

        }

    unit_assert(find(matches2.begin(), matches2.end(), match2) ==  matches2.end());

    // third test: test that potential matches that don't fit the rt tolerance aren't found
   
    // get the resulting matches
    vector<Match> matches3 = p2fm3.getMatches();

    // assert that the incorrect one was not found
    Match match3(sq_3, feat_b3);
    match3.score = snc.score(sq_3, feat_b3);

    if (os_)
        {
            *os_ << "\nTesting that potential matches that don't fit the rt tolerance are not found ... \n";
            XMLWriter writer(*os_);
            *os_ << "\nLooking for the absence of: \n";
            match3.write(writer);
            *os_ << "\nFound: \n";
            vector<Match>::iterator it = matches3.begin();
            for(; it != matches3.end(); ++it)
                {
                    it->write(writer);

                }

        }
      
    unit_assert(find(matches3.begin(), matches3.end(), match3) == matches3.end());

}

int main(int argc, char* argv[])
{
    try
        {
            if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
            if (os_) *os_ << "\nPeptide2FeatureMatcherTest ... \n";
            testNaive();
            // because the test depends only on finding things that are in the mz and rt tolerance, and the setting of these tolerances is tested elsewhere, we only need to test Naive...

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
