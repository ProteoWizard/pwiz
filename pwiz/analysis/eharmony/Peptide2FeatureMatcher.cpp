///
/// Peptide2FeatureMatcher.cpp
///

#include "Peptide2FeatureMatcher.hpp"
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

  void getBestMatch(const SpectrumQuery& sq, Bin<FeatureSequenced>& featureBin, FeatureSequenced& fs, const SearchNeighborhoodCalculator& snc)
{
    pair<double,double> peptideCoords = make_pair(Ion::mz(sq.precursorNeutralMass,sq.assumedCharge), sq.retentionTimeSec);

    double bestScore = 100000000;
    FeatureSequenced* best_it = (FeatureSequenced*) NULL;

    vector<boost::shared_ptr<FeatureSequenced> > adjacentContenders;
    featureBin.getAdjacentBinContents(peptideCoords, adjacentContenders);
    vector<boost::shared_ptr<FeatureSequenced> >::iterator ac_it = adjacentContenders.begin();
           
    for(; ac_it != adjacentContenders.end(); ++ac_it)
        {
            if ( (*ac_it)->feature->charge == sq.assumedCharge )
                {
		  /* double mzDiff = ((*ac_it)->feature->mzMonoisotopic - Ion::mz(sq.precursorNeutralMass,sq.assumedCharge));
                    double rtDiff = ((*ac_it)->feature->retentionTime - sq.retentionTimeSec);
                    double score = sqrt(mzDiff*mzDiff + rtDiff*rtDiff);
		  */
		  double score = snc.score(sq, *((*ac_it)->feature));
		  if ( (1-score) < bestScore )
                        {
	   		     best_it = &(*(*ac_it));
                             best_it->ms1_5 = sq.searchResult.searchHit.peptide;
                             bestScore = score;
			     
                        }                  

                }

        }

    if (best_it) fs = *best_it;
    return;
        
}

} // anonymous namespace

Peptide2FeatureMatcher::Peptide2FeatureMatcher(PeptideID_dataFetcher& a, Feature_dataFetcher& b, const SearchNeighborhoodCalculator& snc)
{
  
    Bin<FeatureSequenced> bin = b.getBin();
    bin.rebin(snc._mzTol, snc._rtTol);

    vector<SpectrumQuery> spectrumQueries = a.getAllContents();
    vector<SpectrumQuery>::iterator sq_it = spectrumQueries.begin();
    int counter = 0;
    cout << "[eharmony] Matching peptides to features across run pair ... " << endl;

    for(; sq_it != spectrumQueries.end(); ++ sq_it)
        {
            if (counter % 100 == 0) cout << "Spectrum: " << counter << endl;
            
            FeatureSequenced fs;
            getBestMatch(*sq_it, bin, fs,snc);          

            if (fs.ms1_5.size() > 0 && snc.close(*sq_it, *fs.feature)) 
                {
  		    sq_it->searchResult.searchHit.analysisResult.xResult.probability = snc.score(*sq_it, *fs.feature);
                    Match match(*sq_it,*fs.feature);
                    match.score = snc.score(*sq_it,*fs.feature);
		    match.spectrumQuery.searchResult.searchHit.peptide += "_ms1_5";

                    _matches.push_back(match); 
                    if ((fs.ms1_5 != fs.ms2) && (fs.ms2.size() > 0))
                        {
                            _falsePositives.push_back(match);
                            
                        }
                    
                    if ((fs.ms1_5 == fs.ms2) && (fs.ms2.size() > 0))
                        {
                         
                            _truePositives.push_back(match);

                        }

                }

            else 
                {           
                    size_t i = 2;
                    bool done = false;

                    while ( !done && i < 10)
                        {            
                            FeatureSequenced gs;
                            bin.rebin(i*snc._mzTol, i*snc._rtTol);
                            getBestMatch(*sq_it, bin, gs,snc);
                            if (gs.ms1_5.size() != 0) 
                                {
                                    done = true;
                                    Match match(*sq_it, *gs.feature);
                                    match.score = snc.score(*sq_it,*gs.feature);

                                    _mismatches.push_back(match);

                                    if (gs.ms1_5 == gs.ms2 && gs.ms2.size() > 0) _falseNegatives.push_back(match);
                                    if (gs.ms1_5 != gs.ms2 && gs.ms2.size() > 0) _trueNegatives.push_back(match);
                                    
                                }

                            ++i;

                        }
                 
                     if ( !done ) cerr << "[Peptide2FeatureMatcher] Bailing out, no feature within 10*search radius." << endl;

                }            

            counter += 1;

        } 
    

}


//TODO: Hack . Fix.

Peptide2FeatureMatcher::Peptide2FeatureMatcher(PeptideID_dataFetcher& a, Feature_dataFetcher& b, const NormalDistributionSearch& snc)
{
  Bin<FeatureSequenced> bin = b.getBin();
  bin.rebin(snc._mzTol, snc._rtTol);
  
  cout << "_mzTol" << snc._mzTol << endl;
  cout << "_rtTol" << snc._rtTol << endl;

  vector<SpectrumQuery> spectrumQueries = a.getAllContents();
  vector<SpectrumQuery>::iterator sq_it = spectrumQueries.begin();
  int counter = 0;
  for(; sq_it != spectrumQueries.end(); ++ sq_it)
    {
      if (counter % 10 == 0) cout << "Spectrum: " << counter << endl;

      FeatureSequenced fs;
      getBestMatch(*sq_it, bin, fs,snc);

      if (fs.ms1_5.size() > 0 && snc.close(*sq_it, *fs.feature))
	{
	  Match match(*sq_it,*fs.feature);
	  match.score = snc.score(*sq_it,*fs.feature);

	  _matches.push_back(match);
	  if (fs.ms1_5 != fs.ms2 && fs.ms2.size() > 0)
	    {
	      _falsePositives.push_back(match);

	    }

	  if (fs.ms1_5 == fs.ms2 && fs.ms2.size() > 0)
	    {

	      _truePositives.push_back(match);

	    }

	}

      else
	{
	  size_t i = 2;
	  bool done = false;

	  while ( !done && i < 5)
	    {

	      FeatureSequenced gs;
	      bin.rebin(i*snc._mzTol, i*snc._rtTol);
	      getBestMatch(*sq_it, bin, gs,snc);
	      if (gs.ms1_5.size() != 0)
	          {
	               done = true;
		       Match match(*sq_it, *gs.feature);
		       match.score = snc.score(*sq_it,*gs.feature);

		       _mismatches.push_back(match);

		       if (gs.ms1_5 == gs.ms2 && gs.ms2.size() > 0) _falseNegatives.push_back(match);
		       if (gs.ms1_5 != gs.ms2 && gs.ms2.size() > 0) _trueNegatives.push_back(match);

		  }

	      ++i;

	    }

      // if ( !done ) cerr << "[Peptide2FeatureMatcher] No feature within 4*search radius." << endl;

    }

  counter += 1;

}

}

bool Peptide2FeatureMatcher::operator==(const Peptide2FeatureMatcher& that)
{
    return _matches == that.getMatches() &&
    _mismatches == that.getMismatches() &&
    _truePositives == that.getTruePositives() &&
    _falsePositives == that.getFalsePositives() &&
    _trueNegatives == that.getTrueNegatives() &&
    _falseNegatives == that.getFalseNegatives();
}

bool Peptide2FeatureMatcher::operator!=(const Peptide2FeatureMatcher& that)
{
    return !(*this == that);

}

