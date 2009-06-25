///
/// Feature2PeptideMatcherTest.cpp
///

#include "Feature2PeptideMatcher.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"

using namespace std;
using namespace pwiz;
using namespace pwiz::eharmony;
using namespace pwiz::data::pepxml;
using namespace pwiz::util;

ostream* os_ = 0;

struct PointsToSame
{
    PointsToSame(MatchPtr& mp) : _mp(mp){}
    bool operator()(const MatchPtr& pm){return (*_mp) == (*pm);}
    
    MatchPtr _mp;

};

const char* firstTestPepXML =     
    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
    "<msms_pipeline_analysis>\n"
    "<msms_run_summary>\n"
    "<spectrum_query start_scan=\"1\" end_scan=\"2\" precursor_neutral_mass=\"32.975191529999996\" assumed_charge=\"1\" retention_time_sec=\"118.441016\">\n"
    "<search_result>\n"
    "<search_hit peptide=\"MARINA\">\n"
    "<analysis_result analysis=\"peptideprophet\">\n"
    "<peptideprophet_result probability=\"0.99998663029333357\" all_ntt_prob=\"(0,0,0.99998663029333357)\">\n"
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

const char* secondTestPepXML =
    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
    "<msms_pipeline_analysis>\n"
    "<msms_run_summary>\n"
    "<spectrum_query start_scan=\"1\" end_scan=\"2\" precursor_neutral_mass=\"32.97512353\" assumed_charge=\"1\" retention_time_sec=\"118.5\">\n"
    "<search_result>\n"
    "<search_hit peptide=\"MARINA\">\n"
    "<analysis_result analysis=\"peptideprophet\">\n"
    "<peptideprophet_result probability=\"0.99998663029333357\" all_ntt_prob=\"(0,0,0.99998663029333357)\">\n"
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
    "<peptideprophet_result probability=\"0.99998663029333357\" all_ntt_prob=\"(0,0,0.99998663029333357)\">\n"
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

    PeptideProphetResult ppresult;
    ppresult.probability = score;
    ppresult.allNttProb.push_back(0);
    ppresult.allNttProb.push_back(0);
    ppresult.allNttProb.push_back(score);

    analysisResult.peptideProphetResult = ppresult;

    searchHit.analysisResult = analysisResult;
    searchResult.searchHit = searchHit;

    spectrumQuery.searchResult = searchResult;

    return spectrumQuery;

}


FeaturePtr makeFeature(double mz, double retentionTime, string ms1_5, int charge = 0)
{
    FeaturePtr feature(new Feature());
    feature->mz = mz;
    feature->retentionTime = retentionTime;
    feature->charge = charge;

    return feature;

}


void testNaive()
{
    if (os_) *os_ << "\ntestNaive() ... \n";

    // first test: test that matches that should be found are, and correctly
 
    // read pepxml
    PidfPtr pidf_a = makePeptideID_dataFetcher(firstTestPepXML);
    PidfPtr pidf_a2 = makePeptideID_dataFetcher(secondTestPepXML);
    PidfPtr pidf_a3 = makePeptideID_dataFetcher(thirdTestPepXML);

    // make candidate features for all tests
    FeaturePtr feat_b1  = makeFeature(33.98246, 117.9, "MARINA", 1); // matches but with large rt diff
    FeaturePtr feat_b2 = makeFeature(33.9829, 118.5, "MARINA", 1); // matches with small rt diff and small mz diff (should get picked)
    FeaturePtr feat_b3 = makeFeature(33.988, 118.5, "MARINA", 1); // outside of tolerances, shouldn't get picked

    // make Feature_dataFetcher for first test
    vector<FeaturePtr> feats_b;
    feats_b.push_back(feat_b1);
    feats_b.push_back(feat_b2);
    FdfPtr fdf_b(new Feature_dataFetcher(feats_b));

    // make Feature_dataFetcher for second and third test
    vector<FeaturePtr> feats_b2;
    feats_b2.push_back(feat_b3);
    FdfPtr fdf_b2(new Feature_dataFetcher(feats_b2));

    // make spectrum queries that we want to find / not find
    SpectrumQuery sq_1 = makeSpectrumQuery(32.975191529999996,118.441016,1,"MARINA",0.99998663029333357,1,2);
    SpectrumQuery sq_2 = makeSpectrumQuery(32.97512353, 118.5, 1, "MARINA", 0.99998663029333357,1,2);
    SpectrumQuery sq_3 = makeSpectrumQuery(32.975191529999996, 18.441016, 1, "MARINA", 0.99998663029333357, 1, 2);  

    SearchNeighborhoodCalculator snc;

    // construct F2PMs for testing
    Feature2PeptideMatcher f2pm(fdf_b, pidf_a, snc);
    Feature2PeptideMatcher f2pm2(fdf_b2, pidf_a2, snc);
    Feature2PeptideMatcher f2pm3(fdf_b2, pidf_a3, snc);

    // test
    vector<MatchPtr> matches = f2pm.getMatches();
  
    MatchPtr match(new Match(sq_1, feat_b2));
    match->score = snc.score(sq_1, *feat_b2);

    if (os_)
        {
            os_ = &cout;
            XMLWriter writer(*os_);
            *os_ << "\nTesting that potential matches that fit the mz and rt tolerance are found ... \n";
            *os_ << "\nLooking for: \n";
            match->write(writer);
            *os_ << "\nFound: \n";
            vector<MatchPtr>::iterator it = matches.begin();
            for(; it != matches.end(); ++it)
                {
                    (*it)->write(writer);

                }

        }

    PointsToSame pts(match);
    unit_assert(find_if(matches.begin(), matches.end(), pts) !=  matches.end());

    // second test: test that potential matches that don't fit the mz tolerance fail

    // get the resulting matches
    vector<MatchPtr> matches2 = f2pm2.getMatches();
        
    // assert that the incorrect one was not found
    MatchPtr match2(new Match(sq_2, feat_b3));
    match2->score = snc.score(sq_2, *feat_b3);
    
    if (os_)
        {
            *os_ << "\nTesting that potential matches that don't fit the mz tolerance are not found ... \n";
            XMLWriter writer(*os_);
            *os_ << "\nLooking for the absence of: \n";
            match2->write(writer);
            *os_ << "\nFound: \n";
            vector<MatchPtr>::iterator it = matches2.begin();
            for(; it != matches2.end(); ++it)
                {
                    (*it)->write(writer);

                }

        }

    PointsToSame pts2(match2);
    unit_assert(find_if(matches2.begin(), matches2.end(), pts2) ==  matches2.end());

    // third test: test that potential matches that don't fit the rt tolerance aren't found
   
    // get the resulting matches
    vector<MatchPtr> matches3 = f2pm3.getMatches();

    // assert that the incorrect one was not found
    MatchPtr match3(new Match(sq_3, feat_b3));
    match3->score = snc.score(sq_3, *feat_b3);

    if (os_)
        {
            *os_ << "\nTesting that potential matches that don't fit the rt tolerance are not found ... \n";
            XMLWriter writer(*os_);
            *os_ << "\nLooking for the absence of: \n";
            match3->write(writer);
            *os_ << "\nFound: \n";
            vector<MatchPtr>::iterator it = matches3.begin();
            for(; it != matches3.end(); ++it)
                {
                    (*it)->write(writer);

                }

        }
   
    PointsToSame pts3(match3); 
    unit_assert(find_if(matches3.begin(), matches3.end(), pts3) == matches3.end());

}

int main(int argc, char* argv[])
{
    try
        {
            if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
            if (os_) *os_ << "\nFeature2PeptideMatcherTest ... \n";
            testNaive();           

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
