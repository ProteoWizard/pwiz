///
/// Feature2PeptideMatcher.cpp
///

#include "Feature2PeptideMatcher.hpp"
#include "PeptideMatcher.hpp"
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


namespace{

    void getBestMatch(vector<FeatureSequenced>::iterator fs, Bin<SpectrumQuery>& sqBin, SpectrumQuery& sq, const SearchNeighborhoodCalculator& snc)
{
  //    pair<double,double> peptideCoords = make_pair(Ion::mz(sq.precursorNeutralMass,sq.assumedCharge), sq.retentionTimeSec);
    pair<double,double> featureCoords = make_pair(fs->feature->mz, fs->feature->retentionTime);

    double bestScore = 0;
    SpectrumQuery* best_it = (SpectrumQuery*) NULL;

    vector<boost::shared_ptr<SpectrumQuery> > adjacentContenders;
    sqBin.getAdjacentBinContents(featureCoords, adjacentContenders);
    vector<boost::shared_ptr<SpectrumQuery> >::iterator ac_it = adjacentContenders.begin();
           
    for(; ac_it != adjacentContenders.end(); ++ac_it)
        {
            if (true) // consider filtering by charge state here
                {		
                    double score = snc.score(**ac_it, *(fs->feature));
                    if ( score > bestScore )
                        {
                            //                            best_it = &(*(*ac_it));
                            sq = **ac_it;
                            fs->ms1_5 = (*ac_it)->searchResult.searchHit.peptide;
                            bestScore = score;
			     
                        }                  

                }

        }

    //    if (best_it) sq = *best_it;
    return;
        
}

} // anonymous namespace

Feature2PeptideMatcher::Feature2PeptideMatcher(FdfPtr a, PidfPtr b, const SearchNeighborhoodCalculator& snc)
{
  Bin<SpectrumQuery> bin = b->getBin();
  bin.rebin(snc._mzTol, snc._rtTol);
  
  cout << "_mzTol" << snc._mzTol << endl;
  cout << "_rtTol" << snc._rtTol << endl;

  vector<FeatureSequenced> featureSequenceds = a->getAllContents();
  vector<FeatureSequenced>::iterator fs_it = featureSequenceds.begin();
  int counter = 0;
  for(; fs_it != featureSequenceds.end(); ++ fs_it)
    {
      if (counter % 100 == 0) cout << "Feature: " << counter << endl;

      SpectrumQuery sq;
      getBestMatch(fs_it, bin, sq, snc); // if there exists a peptide close enough, call it a match and assign the ms1.5

      if (fs_it->ms1_5.size() > 0 && snc.close(sq, *(fs_it->feature)))
          {
              MatchPtr match(new Match(sq,(fs_it->feature)));
              match->score = snc.score(sq,*(fs_it->feature));           
              _matches.push_back(match);

              if (fs_it->ms1_5 != fs_it->ms2 && fs_it->ms2.size() > 0)
                  {
                      _falsePositives.push_back(match);
	      
                  }

              if (fs_it->ms1_5 == fs_it->ms2 && fs_it->ms2.size() > 0)
                  {

                      _truePositives.push_back(match);

                  }
	  
              if (fs_it->ms2.size() == 0) _unknownPositives.push_back(match);

          }

      else
          {
              bool done = false;
              if (fs_it->ms1_5.size() > 0)
                  {
                      done = true;
                      MatchPtr match(new Match(sq, (fs_it->feature)));
                      match->score = snc.score(sq,*(fs_it->feature));

                      _mismatches.push_back(match);
                      if (fs_it->ms1_5 == fs_it->ms2 && fs_it->ms2.size() > 0) _falseNegatives.push_back(match);
                      if (fs_it->ms1_5 != fs_it->ms2 && fs_it->ms2.size() > 0) _trueNegatives.push_back(match);
                      if (fs_it->ms2.size() == 0) _unknownNegatives.push_back(match);

                  }

              size_t i = 1;

	  // look in really big neighborhood for the next closest match.  Entire thing for ROC purposes, once done with ROC, change to smaller neighborhood and grow with "while" loop as big as desired

              double local_mzTol = 2000/3;
              double local_rtTol = 2000;

              while ( !done && i < 2) 
                  {	    
                      SpectrumQuery gs;
                      bin.rebin(local_mzTol, local_rtTol);
                      getBestMatch(fs_it, bin, gs,snc);
                      if (fs_it->ms1_5.size() != 0)
                          {
                              done = true;
                              MatchPtr match(new Match(gs, fs_it->feature));
                              match->score = snc.score(gs, *(fs_it->feature));
                              _mismatches.push_back(match);
		   
                              if (fs_it->ms1_5 == fs_it->ms2 && fs_it->ms2.size() > 0) _falseNegatives.push_back(match);
                              if (fs_it->ms1_5 != fs_it->ms2 && fs_it->ms2.size() > 0) _trueNegatives.push_back(match);
                              if (fs_it->ms2.size() == 0 ) _unknownNegatives.push_back(match);
                          }

                      ++i;

                  }

      // if ( !done ) cerr << "[Feature2PeptideMatcher] No feature within 4*search radius." << endl;

    }

  counter += 1;

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

