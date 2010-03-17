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

vector<SQBinPair> getCoordinates(const vector<SpectrumQuery>& sq, const double& threshold = .9)
{
    vector<SQBinPair> result;
    vector<SpectrumQuery>::const_iterator sq_it = sq.begin();
    for(; sq_it != sq.end(); ++sq_it) 
        {
            if (sq_it->searchResult.searchHit.analysisResult.peptideProphetResult.probability >= threshold) 
                {
                    SpectrumQuery curr = *sq_it;
                                   const double mz = Ion::mz(curr.precursorNeutralMass, curr.assumedCharge);
                                   //const double mass = curr.precursorNeutralMass;
                    const double rt = curr.retentionTimeSec;
                    result.push_back(make_pair(make_pair(mz, rt), curr));
                }
        }

    return result;

}

vector<SQBinPair> getCoordinates(const vector<boost::shared_ptr<SpectrumQuery> >& sq, const double& threshold = .9)
{
    vector<SQBinPair> result;
    vector<boost::shared_ptr<SpectrumQuery> >::const_iterator sq_it = sq.begin();
    for(; sq_it != sq.end(); ++sq_it) 
        {
            if ((*sq_it)->searchResult.searchHit.analysisResult.peptideProphetResult.probability >= threshold) 
                {
                    SpectrumQuery curr = **sq_it;
     const double mz = Ion::mz(curr.precursorNeutralMass, curr.assumedCharge);
     //                    const double mass = curr.precursorNeutralMass;
                    const double rt = curr.retentionTimeSec;
                    result.push_back(make_pair(make_pair(mz, rt), curr));
                }

        }

    return result;

}

PeptideID_dataFetcher::PeptideID_dataFetcher(istream& is, const double& threshold) : _rtAdjusted(false)
{
    MSMSPipelineAnalysis mspa;
    mspa.read(is);
    id = mspa.summaryXML;
 
    vector<SpectrumQuery> sq = mspa.msmsRunSummary.spectrumQueries;
    vector<SQBinPair> spectrumQueries = getCoordinates(sq, threshold);
    _bin = Bin<SpectrumQuery>(spectrumQueries,.005,60);

}

PeptideID_dataFetcher::PeptideID_dataFetcher(const vector<boost::shared_ptr<SpectrumQuery> >& sqs) : _rtAdjusted(false)
{
    vector<SQBinPair> spectrumQueries = getCoordinates(sqs);
    _bin = Bin<SpectrumQuery>(spectrumQueries, .005,60);

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
    //    double mass = sq.precursorNeutralMass;
    double rt = sq.retentionTimeSec;
    _bin.erase(sq, make_pair(mz,rt));

}

void PeptideID_dataFetcher::update(const SpectrumQuery& sq)
{
        double mz = Ion::mz(sq.precursorNeutralMass, sq.assumedCharge);
        //double mass = sq.precursorNeutralMass;
    double rt = sq.retentionTimeSec;
    _bin.update(sq, make_pair(mz,rt));

}

struct SortBySequence
{
  SortBySequence(){}
  bool operator()(boost::shared_ptr<const SpectrumQuery> a, boost::shared_ptr<const SpectrumQuery> b)
  {
    return a->searchResult.searchHit.peptide < b->searchResult.searchHit.peptide;

  }

};

void PeptideID_dataFetcher::merge(const PeptideID_dataFetcher& that)
{
    Bin<SpectrumQuery> bin = that.getBin();
    vector<boost::shared_ptr<SpectrumQuery> > b = bin.getAllContents();
    vector<boost::shared_ptr<SpectrumQuery> > a = _bin.getAllContents();
    vector<boost::shared_ptr<SpectrumQuery> >::iterator it = b.begin();

    vector<boost::shared_ptr<SpectrumQuery> > result;

    sort(a.begin(), a.end(), SortBySequence());
    sort(b.begin(), b.end(), SortBySequence());

    vector<boost::shared_ptr<SpectrumQuery> >::iterator itt = a.begin();
    vector<boost::shared_ptr<SpectrumQuery> >::iterator jt = b.begin();

    while( itt != a.end() && jt != b.end())
      {
	if ((*itt)->searchResult.searchHit.peptide == (*jt)->searchResult.searchHit.peptide)
	  {

	    result.push_back(*itt);
	    result.push_back(*jt);
	    
	    // filtering predicate
	    if (fabs((*itt)->retentionTimeSec - (*jt)->retentionTimeSec) < 100) { update(**itt); update(**jt);}

	    ++itt;
	    ++jt;

	  }

	else if ((*itt)->searchResult.searchHit.peptide < (*jt)->searchResult.searchHit.peptide) ++itt;
	else ++jt;

      }

    for(; it!= b.end(); ++it)
      {
	if (find(result.begin(), result.end(), *it) == result.end()) update(**it);

      }

}

vector<boost::shared_ptr<SpectrumQuery> > PeptideID_dataFetcher::getAllContents() const
{
    vector<boost::shared_ptr<SpectrumQuery> > result = _bin.getAllContents();
    return result;

}

vector<boost::shared_ptr<SpectrumQuery> > PeptideID_dataFetcher::getSpectrumQueries(double mz, double rt)
{
    pair<double,double> coords = make_pair(mz,rt);
    vector<boost::shared_ptr<SpectrumQuery> > result;
    _bin.getAdjacentBinContents(coords,result);

    return result;

}

bool PeptideID_dataFetcher::operator==(const PeptideID_dataFetcher& that)
{
  return getAllContents() == that.getAllContents();

}

bool PeptideID_dataFetcher::operator!=(const PeptideID_dataFetcher& that)
{
    return !(*this == that);

}
