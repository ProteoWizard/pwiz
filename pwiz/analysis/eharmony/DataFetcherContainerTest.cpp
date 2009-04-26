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

// template <typename T>
// struct IsObject
// {
//     IsObject(const T& t) : _t(t) {}
//     bool operator()(boost::shared_ptr<T> entry) { return *entry == _t;}

//     T _t;

// };

} // anonymous namespace

void test()
{
    //    SpectrumQuery makeSpectrumQuery(double precursorNeutralMass, double rt, int charge, string sequence, double score, int startScan, int endScan)
    SpectrumQuery a =  makeSpectrumQuery(3, 2, 3, "four", 5, 6, 7); 
    SpectrumQuery b =  makeSpectrumQuery(3, 4, 3, "six", 7, 8, 9); 

    vector<SpectrumQuery> sq;
    sq.push_back(a);
    sq.push_back(b);

    MSMSPipelineAnalysis mspa;
    mspa.msmsRunSummary.spectrumQueries = sq;
    PeptideID_dataFetcher pidf(mspa);
    
    // we want a feature that lies in between a and b to find the closest one.
    Feature ab = makeFeature(2.00727, 3.5);
    ab.id = "99";
    ab.charge = 3;

    FeatureSequenced fs_ab;
    fs_ab.feature = ab;
    fs_ab.ms1_5="";
    fs_ab.ms2="six";

    vector<Feature> f;
    f.push_back(ab);

    Feature_dataFetcher fdf(f);

    DataFetcherContainer dfc(pidf, pidf, fdf, fdf);
    dfc.adjustRT();
    
    vector<boost::shared_ptr<FeatureSequenced> > f_prime = dfc._fdf_a.getBin().getAllContents();
    
    if (os_)
        {
            ostringstream oss;
            XMLWriter writer(oss);
            vector<boost::shared_ptr<FeatureSequenced> >::iterator it = f_prime.begin();
            for(; it != f_prime.end(); ++it) 
	      {
		  (*it)->feature.write(writer);
		  cout << (*it)->ms1_5 << endl;
		  cout << (*it)->ms2 << endl;

	      }

            *os_ << oss.str() << endl;

        }

    unit_assert(find_if(f_prime.begin(), f_prime.end(), IsObject<FeatureSequenced>(fs_ab)) != f_prime.end());

    // did the spectrum query change as expected?
    b.retentionTimeSec = 3.5;    

    vector<boost::shared_ptr<SpectrumQuery> > sq_prime = dfc._pidf_a.getBin().getAllContents();

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
    unit_assert(dfc._pidf_a.getRtAdjustedFlag());
    unit_assert(dfc._fdf_a.getMS2LabeledFlag());

}

int main(int argc, char* argv[])
{

    try
        {
            if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
            if (os_) *os_ << "DataFetcherContainerTest ... \n";
            test();
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




