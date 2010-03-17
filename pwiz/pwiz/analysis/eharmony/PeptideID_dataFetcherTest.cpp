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

//
// $Id$
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

typedef boost::shared_ptr<PeptideID_dataFetcher> PidfPtr;

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

    PeptideProphetResult xresult;
    xresult.probability = score;
    xresult.allNttProb.push_back(0);
    xresult.allNttProb.push_back(0);
    xresult.allNttProb.push_back(score);

    analysisResult.peptideProphetResult = xresult;

    searchHit.analysisResult = analysisResult;
    searchResult.searchHit = searchHit;

    spectrumQuery.searchResult = searchResult;
    
    return spectrumQuery;

}

PidfPtr makePeptideID_dataFetcher(const char* samplePepXML)
{

    istringstream iss(samplePepXML);
    PidfPtr pidf(new PeptideID_dataFetcher(iss));

    return pidf;

}
/*
boost::shared_ptr<Feature> makeFeature(double mz, double retentionTime)
{
    boost::shared_ptr<Feature> feature;
    feature->mz = mz;
    feature->retentionTime = retentionTime;

    return feature;
    }*/

struct IsSQ
{
    IsSQ(const SpectrumQuery& sq) : _p(sq){}
    SpectrumQuery _p;
    bool operator()(boost::shared_ptr<SpectrumQuery> sq){ return *sq == _p;}

};

void testPeptideID_dataFetcherConstructor()
{   
    if (os_)
        {
            *os_ << "\ntestPeptideID_dataFetcherConstructor() ... \n";
            *os_ << "\nSample pep.xml: \n";
            *os_ << samplePepXML << endl;

        }
    
    // make the PeptideID_dataFetcher from input pep.xml
    PidfPtr pidf = makePeptideID_dataFetcher(samplePepXML);

    // make the SpectrumQuery objects that we expect to be read into the PeptideID_dataFetcher 
    SpectrumQuery a = makeSpectrumQuery(1,2,1, "BUCKLEMYSHOE", 0.900, 1,2);  // mz, rt, charge, sequence, score, start scan, end scan
    SpectrumQuery b = makeSpectrumQuery(3,4,1,"SHUTTHEDOOR",0.900,3,4);
    SpectrumQuery c = makeSpectrumQuery(5,6,1,"PICKUPSTICKS",0.900,5,6);
    SpectrumQuery d = makeSpectrumQuery(7,8,1,"LAYTHEMSTRAIGHT",0.900,7,8);
    
    // Access SpectrumQuery objects that are in the PeptideID_dataFetcher at the coordinates we expect
    vector<boost::shared_ptr<SpectrumQuery> > sq_a = pidf->getSpectrumQueries(Ion::mz(1,1),2);
    vector<boost::shared_ptr<SpectrumQuery> > sq_b = pidf->getSpectrumQueries(Ion::mz(3,1),4);
    vector<boost::shared_ptr<SpectrumQuery> > sq_c = pidf->getSpectrumQueries(Ion::mz(5,1),6);
    vector<boost::shared_ptr<SpectrumQuery> > sq_d = pidf->getSpectrumQueries(Ion::mz(7,1),8);

    // Assert that all SpectrumQuery objects were found at expected coordinates
    unit_assert(find_if(sq_a.begin(), sq_a.end(),IsSQ(a)) != sq_a.end());
    unit_assert(find_if(sq_b.begin(), sq_b.end(),IsSQ(b)) != sq_b.end());
    unit_assert(find_if(sq_c.begin(), sq_c.end(),IsSQ(c)) != sq_c.end());
    unit_assert(find_if(sq_d.begin(), sq_d.end(),IsSQ(d)) != sq_d.end()); 

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

void testMerge()
{
    if (os_) *os_ << "\ntestMerge() ... \n" << endl;

    SpectrumQuery b = makeSpectrumQuery(1,2,1, "BUCKLEMYSHOE", 0.900, 1,2);
    SpectrumQuery c = makeSpectrumQuery(3,4,1, "SHUTTHEDOOR", 0.900, 3,4);

    vector<SpectrumQuery> v;
    v.push_back(b);

    vector<SpectrumQuery> v2;
    SpectrumQuery b2(b);
    b2.retentionTimeSec = 4000;
    v2.push_back(b2);
    v2.push_back(c);
        
    MSMSPipelineAnalysis mspa_fiat;
    mspa_fiat.msmsRunSummary.spectrumQueries = v;

    MSMSPipelineAnalysis mspa_chrysler;
    mspa_chrysler.msmsRunSummary.spectrumQueries = v2;

    PeptideID_dataFetcher fiat(mspa_fiat);
    PeptideID_dataFetcher chrysler(mspa_chrysler);
    
    fiat.merge(chrysler);
 
    // test that the merger correctly concatenated all SpectrumQuery objects
    //    vector<boost::shared_ptr<SpectrumQuery> > contents = fiat.getSpectrumQueries(Ion::mz(b.precursorNeutralMass, b.assumedCharge), b.retentionTimeSec);
    vector<boost::shared_ptr<SpectrumQuery> > contents = fiat.getBin().getAllContents();
    unit_assert(contents.size() == 2);
    unit_assert(**contents.begin() == b);
    unit_assert(*contents.back() == c);
    
    

}

int main(int argc, char* argv[])
{
    try
        {
            if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;

            testPeptideID_dataFetcherConstructor();
	    testMerge();

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
