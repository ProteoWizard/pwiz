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

///
/// DataFetcherContainerTest.cpp
///

#include "DataFetcherContainer.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "boost/shared_ptr.hpp"

using namespace pwiz::cv;
using namespace pwiz::util;
using namespace pwiz::minimxml;
using namespace pwiz::data::peakdata;
using namespace eharmony;
using boost::shared_ptr;

typedef boost::shared_ptr<DataFetcherContainer> DfcPtr;

namespace{

ostream* os_ = 0;
const double epsilon = 0.000001;

SpectrumQuery makeSpectrumQuery(double precursorNeutralMass, double rt, int charge, string sequence, double score = 0.9, int startScan = 0, int endScan = 0)
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

FeaturePtr makeFeature(double mz, double retentionTime)
{
    FeaturePtr feature(new Feature());
    feature->mz = mz;
    feature->retentionTime = retentionTime;
    feature->charge = 1;

    return feature;
}

PidfPtr makePeptideID_dataFetcher()
{
    SpectrumQuery a = makeSpectrumQuery(2, 2, 1, "KRH", 5, 6, 7);
    SpectrumQuery b = makeSpectrumQuery(2, 4, 1, "RAG", 7, 8, 9);

    vector<SpectrumQuery> sq;
    sq.push_back(a);
    sq.push_back(b);

    MSMSPipelineAnalysis mspa;
    mspa.msmsRunSummary.spectrumQueries = sq;
    PidfPtr pidf(new PeptideID_dataFetcher(mspa));

    return pidf;

}

PidfPtr makeBigPeptideID_dataFetcher()
{
    // make enough spectrum queries that we can test getAnchors()
    SpectrumQuery a = makeSpectrumQuery(3, 2, 3, "KRH", 5, 6, 7);
    SpectrumQuery b = makeSpectrumQuery(3, 4, 3, "RAG", 7, 8, 9);
    SpectrumQuery c = makeSpectrumQuery(1, 2, 3, "DW", 4, 5, 6);
    SpectrumQuery d = makeSpectrumQuery(9, 8, 3, "JK", 7, 6, 5);
    
    vector<SpectrumQuery> sq;
    sq.push_back(a);
    sq.push_back(b);
    sq.push_back(c);
    sq.push_back(d);

    MSMSPipelineAnalysis mspa;
    mspa.msmsRunSummary.spectrumQueries = sq;
    PidfPtr pidf(new PeptideID_dataFetcher(mspa));

    return pidf;

}

PidfPtr makeBigBadPeptideID_dataFetcher()
{
    // for testing to ensure that anchors that do not fit the tolerance aren't included
    SpectrumQuery a = makeSpectrumQuery(3, 2, 3, "KRH", 5, 6, 7);
    SpectrumQuery b = makeSpectrumQuery(3, 4, 3, "RAG", 7, 8, 9);
    SpectrumQuery c = makeSpectrumQuery(1, 105, 3, "DW", 4, 5, 6);
    SpectrumQuery d = makeSpectrumQuery(9, 8, 3, "JK", 7, 6, 5);

    vector<SpectrumQuery> sq;
    sq.push_back(a);
    sq.push_back(b);
    sq.push_back(c);
    sq.push_back(d);

    MSMSPipelineAnalysis mspa;
    mspa.msmsRunSummary.spectrumQueries = sq;
    PidfPtr pidf(new PeptideID_dataFetcher(mspa));

    return pidf;

}

FdfPtr makeFeature_dataFetcher()
{
    FeaturePtr ab = makeFeature(3.00727, 3.5);
    ab->id = "99";

    vector<FeaturePtr> f;
    f.push_back(ab);

    FdfPtr fdf(new Feature_dataFetcher(f));

    return fdf;

}

DfcPtr makeDataFetcherContainer()
{
    PidfPtr pidf = makePeptideID_dataFetcher();
    FdfPtr fdf = makeFeature_dataFetcher();

    DfcPtr dfc(new DataFetcherContainer(pidf, pidf, fdf, fdf));
    dfc->adjustRT();

    return dfc;

}

// Dummy PeptideID_dataFetchers and Feature_dataFetchers for testing warpRT() method

PidfPtr makeWarpTestPidfA()
{
    vector<boost::shared_ptr<SpectrumQuery> > sqs;
    
    sqs.push_back(boost::shared_ptr<SpectrumQuery>(new SpectrumQuery(makeSpectrumQuery(1, 1, 1, "F"))));
    sqs.push_back(boost::shared_ptr<SpectrumQuery>(new SpectrumQuery(makeSpectrumQuery(1,1.5, 1, "C"))));
    sqs.push_back(boost::shared_ptr<SpectrumQuery>(new SpectrumQuery(makeSpectrumQuery(1,2, 1, "A"))));

    PidfPtr pidf(new PeptideID_dataFetcher(sqs));

    return pidf;

}

PidfPtr makeWarpTestPidfB()
{
    vector<boost::shared_ptr<SpectrumQuery> > sqs;

    sqs.push_back(boost::shared_ptr<SpectrumQuery>(new SpectrumQuery(makeSpectrumQuery(1, 0.5, 1, "F"))));
    sqs.push_back(boost::shared_ptr<SpectrumQuery>(new SpectrumQuery(makeSpectrumQuery(1,2, 1, "C"))));
    sqs.push_back(boost::shared_ptr<SpectrumQuery>(new SpectrumQuery(makeSpectrumQuery(1,3, 1, "A"))));

    PidfPtr pidf(new PeptideID_dataFetcher(sqs));

    return pidf;

}

FdfPtr makeWarpTestFdfA()
{

    vector<FeaturePtr> features;

    features.push_back(makeFeature(2.00727, 1.001));
    features.push_back(makeFeature(2.00727, 1.499));
    features.push_back(makeFeature(2.00727, 2.001));

    FdfPtr fdf(new Feature_dataFetcher(features));
    
    return fdf;
}

FdfPtr makeWarpTestFdfB()
{
    vector<FeaturePtr> features;

    features.push_back(makeFeature(2.00727, 0.499));
    features.push_back(makeFeature(2.00727, 1.999));
    features.push_back(makeFeature(2.00727, 3.003));

    FdfPtr fdf(new Feature_dataFetcher(features));

    return fdf;
}

} // anonymous namespace

void testAdjustRT()
{
    DfcPtr dfc = makeDataFetcherContainer();

    FeaturePtr ab = makeFeature(3.00727, 3.5);
    ab->id = "99";
    ab->charge = 1;
    FeatureSequenced fs_ab;
    fs_ab.feature = ab;
    fs_ab.ms1_5="";
    fs_ab.ms2="RAG";
        
    SpectrumQuery a =  makeSpectrumQuery(2, 2, 1, "KRH", 5, 6, 7);
    SpectrumQuery b =  makeSpectrumQuery(2, 4, 1, "RAG", 7, 8, 9);

    vector<boost::shared_ptr<FeatureSequenced> > f_prime = dfc->_fdf_a->getBin().getAllContents();
    
    if (os_)
        {
            ostringstream oss;
            XMLWriter writer(oss);
            vector<boost::shared_ptr<FeatureSequenced> >::iterator it = f_prime.begin();
            for(; it != f_prime.end(); ++it) 
                {
                    (*it)->feature->write(writer);
                    oss << (*it)->ms1_5 << endl;
                    oss << (*it)->ms2 << endl;

                }

            fs_ab.feature->write(writer);
            oss << fs_ab.ms1_5 << endl;
            oss << fs_ab.ms2 << endl;
            *os_ << oss.str() << endl;

        }

    // did the feature sequence get assigned as expected?
    unit_assert(find_if(f_prime.begin(), f_prime.end(), IsObject<FeatureSequenced>(fs_ab)) != f_prime.end());

    // did the peptide retention time get assigned as expected?
    b.retentionTimeSec = 3.5;    

    vector<boost::shared_ptr<SpectrumQuery> > sq_prime = dfc->_pidf_a->getBin().getAllContents();

    if (os_)
        {
            ostringstream oss;
            XMLWriter writer(oss);
            vector<boost::shared_ptr<SpectrumQuery> >::iterator it = sq_prime.begin();
            for(; it != sq_prime.end(); ++it)
                {                   
                    (*it)->write(writer);

                }

            *os_ << oss.str() << endl;

        }

    unit_assert(find_if(sq_prime.begin(), sq_prime.end(), IsObject<SpectrumQuery>(b)) != sq_prime.end());

    // we also expect a's retention time to be set to that of feature ab, since it is the only option and therefore the best
    a.retentionTimeSec = 3.5;
    unit_assert(find_if(sq_prime.begin(), sq_prime.end(), IsObject<SpectrumQuery>(a)) != sq_prime.end());

    // were the appropriate flags set?
    unit_assert(dfc->_pidf_a->getRtAdjustedFlag());
    unit_assert(dfc->_fdf_a->getMS2LabeledFlag());

}

void testGetAnchors()
{
    PidfPtr pidf_a(makeBigPeptideID_dataFetcher());
    PidfPtr pidf_b(makeBigBadPeptideID_dataFetcher());
    
    DataFetcherContainer dfc(pidf_a, pidf_b, FdfPtr(new Feature_dataFetcher()), FdfPtr(new Feature_dataFetcher()));
    dfc.warpRT(Default, 2); // get every 2nd anchor, using default tolerance of 100 seconds   
    
    vector<pair<double, double> > trueAnchors;
    trueAnchors.push_back(make_pair(2,2));

    unit_assert(trueAnchors == dfc.anchors());
    
    trueAnchors.clear();

    dfc.warpRT(Default,2, 110); // get every 2nd anchor, using higher tolerance of 110 seconds
    trueAnchors.push_back(make_pair(2, 105));
    trueAnchors.push_back(make_pair(2, 2));

    unit_assert(trueAnchors == dfc.anchors());
                            
}

// helper functions  
namespace{
                                                                                                                 
vector<double> getRTs(PidfPtr pidf)
{
    vector<double> result;
    vector<boost::shared_ptr<SpectrumQuery> > sqs = pidf->getAllContents();
    vector<boost::shared_ptr<SpectrumQuery> >::iterator it = sqs.begin();
    for(; it != sqs.end(); ++it) result.push_back((*it)->retentionTimeSec);

    return result;

}

vector<double> getRTs(FdfPtr fdf)
{
    vector<double> result;
    vector<FeatureSequencedPtr> fss = fdf->getAllContents();
    vector<FeatureSequencedPtr>::iterator it = fss.begin();
    for(; it != fss.end(); ++it) result.push_back((*it)->feature->retentionTime);

    return result;

}

} // anonymous namespace

void testWarpRT()
{
    DataFetcherContainer dfc(makeWarpTestPidfA(), makeWarpTestPidfB(), makeWarpTestFdfA(), makeWarpTestFdfB());
    dfc.adjustRT();
    dfc.warpRT(Linear, 2);

    vector<double> pidf_a_rts = getRTs(dfc._pidf_a);
    vector<double> pidf_b_rts = getRTs(dfc._pidf_b);
    vector<double> fdf_a_rts = getRTs(dfc._fdf_a);
    vector<double> fdf_b_rts = getRTs(dfc._fdf_b);
    
    vector<double> a_truth;
    a_truth.push_back(0.49899999999999);
    a_truth.push_back(1.74599200000000);
    a_truth.push_back(3.00300000000000);
    
    vector<double> b_truth;
    b_truth.push_back(-0.7580079999999);
    b_truth.push_back(2.99799200000000);
    b_truth.push_back(5.51200800000000);

    vector<double>::iterator par = pidf_a_rts.begin();
    vector<double>::iterator far = fdf_a_rts.begin();
    vector<double>::iterator at = a_truth.begin();

    for(; at != a_truth.end(); ++at, ++far, ++par) 
        {
            //            unit_assert_equal(*par, *at, epsilon);
            //unit_assert_equal(*far, *at, epsilon);

        }

    vector<double>::iterator pbr = pidf_b_rts.begin();
    vector<double>::iterator fbr = fdf_b_rts.begin();
    vector<double>::iterator bt = b_truth.begin();

    for(; bt != b_truth.end(); ++bt, ++fbr, ++pbr)
        {
            unit_assert_equal(*pbr, *bt, epsilon);
            unit_assert_equal(*fbr, *bt, epsilon);

        }
    
}

int main(int argc, char* argv[])
{

    try
        {
            if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
            if (os_) *os_ << "DataFetcherContainerTest ... \n";
            testAdjustRT();
            testGetAnchors();
            testWarpRT();

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




