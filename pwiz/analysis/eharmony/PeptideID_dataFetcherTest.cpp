//
// PeptideID_dataFetcherTest.hpp
//

#include "pwiz/utility/misc/unit.hpp"
#include "PeptideID_dataFetcher.hpp"
#include "Feature_dataFetcher.hpp"
#include <boost/shared_ptr.hpp>
#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/proteome/Ion.hpp"
#include <iomanip>

using namespace std;
using namespace pwiz::util;
using namespace pwiz::proteome;
using namespace pwiz::data::pepxml;
using namespace pwiz::eharmony;

ostream* os_ = 0;

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

PeptideID_dataFetcher makePeptideID_dataFetcher(const char* samplePepXML)
{

    istringstream iss(samplePepXML);
    PeptideID_dataFetcher pidf(iss);

    return pidf;

}

Feature makeFeature(double mz, double retentionTime)
{
    Feature feature;
    feature.mzMonoisotopic = mz;
    feature.retentionTime = retentionTime;

    return feature;
}

void testPeptideID_dataFetcherConstructor()
{   
    if (os_)
        {
            *os_ << "\ntestPeptideID_dataFetcherConstructor() ... \n";
            *os_ << "\nSample pep.xml: \n";
            *os_ << samplePepXML << endl;

        }
                 
    PeptideID_dataFetcher pidf = makePeptideID_dataFetcher(samplePepXML);

    SpectrumQuery a = makeSpectrumQuery(1,2,1, "BUCKLEMYSHOE", 0.900, 1,2);  // mz, rt, charge, sequence, score, start scan, end scan
    SpectrumQuery b = makeSpectrumQuery(3,4,1,"SHUTTHEDOOR",0.900,3,4);
    SpectrumQuery c = makeSpectrumQuery(5,6,1,"PICKUPSTICKS",0.900,5,6);
    SpectrumQuery d = makeSpectrumQuery(7,8,1,"LAYTHEMSTRAIGHT",0.900,7,8);

    vector<SpectrumQuery> sq_a = pidf.getSpectrumQueries(Ion::mz(1,1),2);
    vector<SpectrumQuery> sq_b = pidf.getSpectrumQueries(Ion::mz(3,1),4);
    vector<SpectrumQuery> sq_c = pidf.getSpectrumQueries(Ion::mz(5,1),6);
    vector<SpectrumQuery> sq_d = pidf.getSpectrumQueries(Ion::mz(7,1),8);

    unit_assert(find(sq_a.begin(), sq_a.end(),a) != sq_a.end());
    unit_assert(find(sq_b.begin(), sq_b.end(),b) != sq_b.end());
    unit_assert(find(sq_a.begin(), sq_a.end(),a) != sq_a.end());
    unit_assert(find(sq_a.begin(), sq_a.end(),a) != sq_a.end()); 

    if (os_)
        {
            *os_ << "\nSpectrumQuery objects read from sample pep.xml: \n";
            ostringstream oss;
            XMLWriter writer(oss);
            a.write(writer);
            b.write(writer);
            c.write(writer);
            d.write(writer);
            *os_ << oss.str() << endl;
        }

}

int main(int argc, char* argv[])
{
    try
        {
            if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
            testPeptideID_dataFetcherConstructor();

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
