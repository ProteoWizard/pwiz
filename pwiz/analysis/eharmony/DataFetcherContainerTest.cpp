///
/// DataFetcherContainerTest.cpp
///

#include "DataFetcherContainer.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "boost/shared_ptr.hpp"

using namespace pwiz;
using namespace pwiz::util;
using namespace pwiz::minimxml;
using namespace pwiz::data::peakdata;
using namespace eharmony;

typedef boost::shared_ptr<DataFetcherContainer> DfcPtr;

namespace{

ostream* os_ = 0;

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

FeaturePtr makeFeature(double mz, double retentionTime)
{
    FeaturePtr feature(new Feature());
    feature->mz = mz;
    feature->retentionTime = retentionTime;

    return feature;
}

PidfPtr makePeptideID_dataFetcher()
{
    SpectrumQuery a =  makeSpectrumQuery(3, 2, 3, "KRH", 5, 6, 7);
    SpectrumQuery b =  makeSpectrumQuery(3, 4, 3, "RAG", 7, 8, 9);

    vector<SpectrumQuery> sq;
    sq.push_back(a);
    sq.push_back(b);

    MSMSPipelineAnalysis mspa;
    mspa.msmsRunSummary.spectrumQueries = sq;
    PidfPtr pidf(new PeptideID_dataFetcher(mspa));

    return pidf;

}

FdfPtr makeFeature_dataFetcher()
{
    FeaturePtr ab = makeFeature(2.00727, 3.5);
    ab->id = "99";
    ab->charge = 3;

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

} // anonymous namespace

void testAdjustRT()
{
    DfcPtr dfc = makeDataFetcherContainer();

    FeaturePtr ab = makeFeature(2.00727, 3.5);
    ab->id = "99";
    ab->charge = 3;
    FeatureSequenced fs_ab;
    fs_ab.feature = ab;
    fs_ab.ms1_5="";
    fs_ab.ms2="RAG";
        
    SpectrumQuery a =  makeSpectrumQuery(3, 2, 3, "KRH", 5, 6, 7);
    SpectrumQuery b =  makeSpectrumQuery(3, 4, 3, "RAG", 7, 8, 9);

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

}

void testWarpRT()
{

    // test getRTVals
    // test warpRTVals
    // test putRTVals

    // test invariance under default
    // test correct variance under linear


}

int main(int argc, char* argv[])
{

    try
        {
            if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
            if (os_) *os_ << "DataFetcherContainerTest ... \n";
            testAdjustRT();
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




