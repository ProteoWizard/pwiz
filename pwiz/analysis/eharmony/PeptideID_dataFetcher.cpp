///
/// PeptideID_dataFetcher.cpp
///

#include "PeptideID_dataFetcher.hpp"
#include "Feature_dataFetcher.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include "pwiz/utility/proteome/Ion.hpp"

using namespace std;
using namespace pwiz::eharmony;
using namespace pwiz::proteome;

typedef pair<pair<double,double>, SpectrumQuery> SQBinPair;

vector<SQBinPair> getCoordinates(const vector<SpectrumQuery>& sq)
{
    vector<SQBinPair> result;
    vector<SpectrumQuery>::const_iterator sq_it = sq.begin();
    if (true)
        {
            for(; sq_it != sq.end(); ++sq_it) 
                {
                    if (sq_it->searchResult.searchHit.analysisResult.xResult.probability >= .9) 
                        {
                            result.push_back(make_pair(make_pair(Ion::mz(sq_it->precursorNeutralMass,sq_it->assumedCharge), sq_it->retentionTimeSec),*sq_it));

                        }

                }

        }

    return result;

}

PeptideID_dataFetcher::PeptideID_dataFetcher(istream& is) : _rtAdjusted(false)
{
    MSMSPipelineAnalysis mspa;
    mspa.read(is);
 
    vector<SpectrumQuery> sq = mspa.msmsRunSummary.spectrumQueries;
    vector<SQBinPair> spectrumQueries = getCoordinates(sq);
    _bin = Bin<SpectrumQuery>(spectrumQueries,.005,60);
   
}

PeptideID_dataFetcher::PeptideID_dataFetcher(const MSMSPipelineAnalysis& mspa) : _rtAdjusted(false)
{
    vector<SpectrumQuery> sq = mspa.msmsRunSummary.spectrumQueries;
    vector<SQBinPair> spectrumQueries = getCoordinates(sq);
    _bin = Bin<SpectrumQuery>(spectrumQueries,.005,60);

}

void PeptideID_dataFetcher::erase(const SpectrumQuery& sq)
{
    double mz = Ion::mz(sq.precursorNeutralMass,sq.assumedCharge);
    double rt = sq.retentionTimeSec;
    _bin.erase(sq, make_pair(mz,rt));

}

void PeptideID_dataFetcher::update(const SpectrumQuery& sq)
{
    double mz = Ion::mz(sq.precursorNeutralMass, sq.assumedCharge);
    double rt = sq.retentionTimeSec;
    _bin.update(sq, make_pair(mz,rt));

}

vector<SpectrumQuery> PeptideID_dataFetcher::getAllContents() const
{
    vector<boost::shared_ptr<SpectrumQuery> > hack = _bin.getAllContents();
    vector<boost::shared_ptr<SpectrumQuery> >::iterator it = hack.begin();
    vector<SpectrumQuery> result;
    for(; it != hack.end(); ++it) result.push_back(**it);

    return result;

}

vector<SpectrumQuery> PeptideID_dataFetcher::getSpectrumQueries(double mz, double rt)
{
    pair<double,double> coords = make_pair(mz,rt);
    vector<SpectrumQuery> result;
    _bin.getBinContents(coords,result);

    return result;

}

