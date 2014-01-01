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

#include "Feature2PeptideMatcher.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::cv;
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

void test()
{

// TODO
// Goal is to test that we can catch all of the six ways in which we can be wrong or right in identifying a match:
 
// Ways to be right:
// A) Only one peptide within search radius of feature; peptide is correct and we call it correct; TP
// B) No peptides within search radius of feature; next nearest peptide is incorrect and we call it incorrect; TN
// C) More than one peptide within search radius of feature; one peptide is correct and we call it correct; TP; the rest are incorrect and we've called them incorrect; TN

// Ways to be wrong:
// D) Only one peptide within search radius of feature; peptide is incorrect and we call it correct; FP
// E) No peptides within search radius of feature; next nearest peptide is correct and we call it incorrect; FN;
// F) More than one peptide within search radius of feature; one peptide is correct and we call it incorrect; FN; the rest are incorrect and we've called one of them correct; FP

}

int main(int argc, char* argv[])
{
    try
        {
            if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
            if (os_) *os_ << "\nFeature2PeptideMatcherTest ... \n";
            test();

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
