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
#include "PeptideMatcher.hpp"
#include "DatabaseQuery.hpp"
#include "pwiz/utility/proteome/Ion.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cmath>
#include <cctype>

using namespace pwiz::eharmony;
using namespace pwiz::minimxml;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;
using namespace pwiz::proteome;


struct SortByScore
{
    SortByScore(){}
    bool operator()(MatchPtr a, MatchPtr b) { return a->score > b->score;}

};

void normalizeMatches(vector<MatchPtr>& v, const double& maxScore)
{
    vector<MatchPtr>::iterator it = v.begin();
    for(; it != v.end(); ++it) (*it)->score /= maxScore;
}

ofstream matches("matchSequences.txt");

Feature2PeptideMatcher::Feature2PeptideMatcher(FdfPtr a, PidfPtr b, const NormalDistributionSearch& nds, const int& rocStats, const double& threshold)
{
    DatabaseQuery db(b);

    vector<FeatureSequencedPtr> featureSequenceds = a->getAllContents();
    vector<FeatureSequencedPtr>::iterator fs_it = featureSequenceds.begin();
    int counter = 0;
    int known = 0;
    ofstream all("all.txt");

    for(; fs_it != featureSequenceds.end(); ++fs_it)
        {
            if (counter % 100 == 0) cout << "Feature: " << counter << endl;
            if (!((*fs_it)->ms2.size() > 0))
                {
                    counter++;
                    continue;

                }

            else { counter++; known++;}

            vector<MatchPtr> matches = db.query(*fs_it, nds, threshold);
            sort(matches.begin(), matches.end(), SortByScore());            

            const string& feature_ms2 = (*fs_it)->ms2;
            string peptide_ms2 = "";
            string next_peptide_ms2 = "";

            if (matches.size() > 0)
                {
                     _matches.push_back(*matches.begin());
                     peptide_ms2 = _matches.back()->spectrumQuery.searchResult.searchHit.peptide;

                     if (feature_ms2 == peptide_ms2) _truePositives.push_back(*matches.begin());
                     if (feature_ms2 != peptide_ms2) _falsePositives.push_back(*matches.begin());
                    
                }

            else  // look for the nearest thing if we are trying to generate ROC stats
                {
                    if (rocStats)
                        {
                            vector<MatchPtr> nextMatches = db.query(*fs_it, nds, 0);
                            sort(nextMatches.begin(), nextMatches.end(), SortByScore());
                            if (nextMatches.size() == 0 ) 
                                {
                                    cerr << "Error: What the heck, .6 not big enough ... " << endl;             
                                    if (matches.size() == 0) cerr << "Error: matches also of size 0 ! " << endl;
                                }

                            else 
                                {
                                    _mismatches.push_back(*nextMatches.begin());
                                    next_peptide_ms2 = _mismatches.back()->spectrumQuery.searchResult.searchHit.peptide;
                            
                                    if (feature_ms2 == next_peptide_ms2) _falseNegatives.push_back(*nextMatches.begin());
                                    if (feature_ms2 != next_peptide_ms2) _trueNegatives.push_back(*nextMatches.begin());
                                }             
                        }
                }
        }
   
}

bool Feature2PeptideMatcher::operator==(const Feature2PeptideMatcher& that)
{
    return _matches == that.getMatches() &&
    _mismatches == that.getMismatches() &&
    _truePositives == that.getTruePositives() &&
    _falsePositives == that.getFalsePositives() &&
    _trueNegatives == that.getTrueNegatives() &&
    _falseNegatives == that.getFalseNegatives();

}

bool Feature2PeptideMatcher::operator!=(const Feature2PeptideMatcher& that)
{
    return !(*this == that);

}

