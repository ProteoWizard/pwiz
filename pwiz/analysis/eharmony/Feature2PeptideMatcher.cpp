///
/// Feature2PeptideMatcher.cpp
///

#include "Feature2PeptideMatcher.hpp"
#include "PeptideMatcher.hpp"
#include "DatabaseQuery.hpp"
#include "pwiz/utility/proteome/Ion.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include <math.h>
#include <string>
#include <algorithm>
#include <cctype>

using namespace std;
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


ofstream matches("matchSequences.txt");

Feature2PeptideMatcher::Feature2PeptideMatcher(FdfPtr a, PidfPtr b, const NormalDistributionSearch& nds)
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
            vector<MatchPtr> matches = db.query(*fs_it, nds, .95);

            sort(matches.begin(), matches.end(), SortByScore());            

            const string& feature_ms2 = (*fs_it)->ms2;
            string peptide_ms2 = "";
            string next_peptide_ms2 = "";

            if (matches.size() > 0)
                {
                     _matches.push_back(*matches.begin());
                     peptide_ms2 = _matches.back()->spectrumQuery.searchResult.searchHit.peptide;
                    
                }
            
            vector<MatchPtr> nextMatches = db.query(*fs_it, nds, -.1);
            sort(nextMatches.begin(), nextMatches.end(), SortByScore());
            if (matches.size() > 0) nextMatches.erase(nextMatches.begin()); // b/c we already got it in matches
            if (nextMatches.size() == 0 ) 
                {
                    cerr << "Error: What the heck, .6 not big enough ... " << endl;             
                    if (matches.size() == 0) cerr << "Error: matches also of size 0 ! " << endl;
                }

             else 
                {
                    _mismatches.push_back(*nextMatches.begin());
                     next_peptide_ms2 = _mismatches.back()->spectrumQuery.searchResult.searchHit.peptide;

                }

            // In the case that something was within the original search radius, analyze the positives
            if (matches.size() > 0)
                {
                    if (feature_ms2 == peptide_ms2) _truePositives.push_back(*matches.begin());
                    if (feature_ms2 != peptide_ms2) _falsePositives.push_back(*matches.begin());

                }

            // In the case that nothing was within the original search radius, analyze the negatives
            if ( matches.size() == 0 && nextMatches.size() > 0)
                {
                    if (feature_ms2 == next_peptide_ms2) _falseNegatives.push_back(*nextMatches.begin());
                    if (feature_ms2 != next_peptide_ms2) _trueNegatives.push_back(*nextMatches.begin());

                }

            else cerr << "Nothing nearby." << endl;
            
        }

  cout << "KNOWN: " << known << endl;

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

