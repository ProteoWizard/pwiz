///
/// NeighborJoinerTest.cpp
///

#include "NeighborJoiner.hpp"
#include "pwiz/utility/misc/unit.hpp"

using namespace pwiz;
using namespace eharmony;
using namespace pwiz::util;

struct NumberOfMS2IDs : public DistanceAttribute
{
    NumberOfMS2IDs(){}    
    virtual double score(const AMTContainer& a, const AMTContainer& b);

};

double NumberOfMS2IDs::score(const AMTContainer& a, const AMTContainer& b)
{
    int a_count = a._pidf.getAllContents().size();
    int b_count = b._pidf.getAllContents().size();
    return sqrt((a_count-b_count)*(a_count-b_count)); // this is not necessarily a *good* metric, just wanted one for testing

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

void test()
{
    vector<SpectrumQuery> testSpectrumQueries;
    testSpectrumQueries.push_back(makeSpectrumQuery(550.82, 83.46, 3, "MAHTOMEDI", .95, 612, 651));
    testSpectrumQueries.push_back(makeSpectrumQuery(02.139, 02.142, 3, "CAMBRIDGE", .95, 617, 857));
    testSpectrumQueries.push_back(makeSpectrumQuery(904.04, 913.60, 3, "LOSANGELES", .95, 310, 805));
    testSpectrumQueries.push_back(makeSpectrumQuery(537.17, 53.703, 3, "MADISON", .96, 608, 715));

    PeptideID_dataFetcher four(testSpectrumQueries);
    testSpectrumQueries.erase(testSpectrumQueries.begin());
    testSpectrumQueries.erase(testSpectrumQueries.begin());
    PeptideID_dataFetcher two(testSpectrumQueries);
    testSpectrumQueries.erase(testSpectrumQueries.begin());
    PeptideID_dataFetcher one(testSpectrumQueries);

    boost::shared_ptr<AMTContainer> cuatro(new AMTContainer());
    cuatro->_pidf = four;
    boost::shared_ptr<AMTContainer> dos(new AMTContainer());
    dos->_pidf = two;
    boost::shared_ptr<AMTContainer> uno(new AMTContainer());
    uno->_pidf = one;

    vector<boost::shared_ptr<AMTContainer> > testEntries;
    testEntries.push_back(cuatro);
    testEntries.push_back(dos);
    testEntries.push_back(uno);

    NeighborJoiner neighborJoiner(testEntries);
    boost::shared_ptr<NumberOfMS2IDs> numpep(new NumberOfMS2IDs());

    neighborJoiner.addDistanceAttribute(numpep);    
    neighborJoiner.calculateDistanceMatrix();
    neighborJoiner.joinNearest();

    // make the merger that we are looking for
    dos->merge(*uno);

    unit_assert(neighborJoiner._rowEntries.back() == *dos);
    unit_assert(neighborJoiner._columnEntries.back() == *dos);
    
    neighborJoiner.calculateDistanceMatrix(); //recalculate given the recent merger
    neighborJoiner.joinNearest();

    // make the merger that we are looking for
    cuatro->merge(*dos);

    unit_assert(neighborJoiner._rowEntries.back() == *cuatro);
    unit_assert(neighborJoiner._columnEntries.back() == *cuatro);

}

int main()
{
    test();
    return 0;

}











