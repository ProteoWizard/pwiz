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
/// NeighborJoinerTest.cpp
///

#include "NeighborJoiner.hpp"
#include "pwiz/utility/misc/unit.hpp"

using namespace pwiz;
using namespace eharmony;
using namespace pwiz::util;

boost::shared_ptr<SpectrumQuery> makeSpectrumQuery(double precursorNeutralMass, double rt, int charge, string sequence, double score, int startScan, int endScan)
{
    boost::shared_ptr<SpectrumQuery> spectrumQuery(new SpectrumQuery());
    spectrumQuery->startScan = startScan;
    spectrumQuery->endScan = endScan;
    spectrumQuery->precursorNeutralMass = precursorNeutralMass;
    spectrumQuery->assumedCharge = charge;
    spectrumQuery->retentionTimeSec = rt;

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

    spectrumQuery->searchResult = searchResult;

    return spectrumQuery;

}

void test()
{
    vector<boost::shared_ptr<SpectrumQuery> > testSpectrumQueries;
    testSpectrumQueries.push_back(makeSpectrumQuery(550.82, 83.46, 3, "MAHTOMEDI", .95, 612, 651));
    testSpectrumQueries.push_back(makeSpectrumQuery(02.139, 02.142, 3, "CAMBRIDGE", .95, 617, 857));
    testSpectrumQueries.push_back(makeSpectrumQuery(904.04, 913.60, 3, "MAHTOMEDI", .95, 310, 805));
    testSpectrumQueries.push_back(makeSpectrumQuery(537.17, 53.703, 3, "MAHTOMEDI", .96, 608, 715));

    PidfPtr four(new PeptideID_dataFetcher(testSpectrumQueries));
    testSpectrumQueries.erase(testSpectrumQueries.begin());
    testSpectrumQueries.erase(testSpectrumQueries.begin());
    PidfPtr two(new PeptideID_dataFetcher(testSpectrumQueries));
    testSpectrumQueries.erase(testSpectrumQueries.begin());
    PidfPtr one(new PeptideID_dataFetcher(testSpectrumQueries));

    boost::shared_ptr<Feature_dataFetcher> dummy_fdf(new Feature_dataFetcher());

    boost::shared_ptr<AMTContainer> cuatro(new AMTContainer());
    cuatro->_pidf = four;
    cuatro->_fdf = dummy_fdf;
    boost::shared_ptr<AMTContainer> dos(new AMTContainer());
    dos->_pidf = two;
    dos->_fdf = dummy_fdf;
    boost::shared_ptr<AMTContainer> uno(new AMTContainer());
    uno->_pidf = one;
    uno->_fdf = dummy_fdf;

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











