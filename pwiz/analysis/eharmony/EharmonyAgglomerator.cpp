
///
/// EharmonyAgglomerator.cpp
///

#include "AMTContainer.hpp"
#include "Peptide2FeatureMatcher.hpp"

using namespace pwiz;
using namespace eharmony;


void pairwiseMatchmake(boost::shared_ptr<AMTContainer> a, boost::shared_ptr<AMTContainer> b, bool doFirst = false, bool doSecond = false)
{
    DataFetcherContainer dfc(a->_pidf, b->_pidf, a->_fdf, b->_fdf);
    dfc.adjustRT(doFirst, doSecond);
    a->merge(*b);

    WarpFunctionEnum wfe = a->_config.warpFunction;
    dfc.warpRT(wfe);

    Match_dataFetcher mdf;

    if (a->_config.searchNeighborhoodCalculator.size() > 0)
      {
	SearchNeighborhoodCalculator snc = a->_config.parsedSNC;
	PeptideMatcher pm_snc(dfc);
	Peptide2FeatureMatcher p2fm_snc(dfc._pidf_a, dfc._fdf_b, snc);
	a->_pm = pm_snc;
	a->_p2fm = p2fm_snc;

	Match_dataFetcher mdf_snc(p2fm_snc.getMatches());
	mdf = mdf_snc;

      }

    else if (a->_config.normalDistributionSearch.size() > 0)
      {
	NormalDistributionSearch nds = a->_config.parsedNDS;
	nds.calculateTolerances(dfc);
	PeptideMatcher pm_nds(dfc);
	Peptide2FeatureMatcher p2fm_nds(dfc._pidf_a, dfc._fdf_b, nds);
	a->_pm = pm_nds;
	a->_p2fm = p2fm_nds;

	Match_dataFetcher mdf_nds(p2fm_nds.getMatches());
	mdf = mdf_nds;

      }

    a->_mdf.merge(mdf); 
    return;

}

vector<boost::shared_ptr<AMTContainer> > pairwiseMatchmakeVector(vector<boost::shared_ptr<AMTContainer> > runs, bool firstPass = false)
{
    vector<boost::shared_ptr<AMTContainer> > result;
    vector<boost::shared_ptr<AMTContainer> >::iterator it = runs.begin();
   
    for(; it < runs.end()-1; ++it)
        { 
	  
	 
  	    boost::shared_ptr<AMTContainer> one = *it;
	    it++;
	    boost::shared_ptr<AMTContainer> two = *it;
	    
  	    DataFetcherContainer dfc(one->_pidf, two->_pidf, one->_fdf, two->_fdf);
	    if (firstPass) dfc.adjustRT();
	    one->merge(*two);
	    
            WarpFunctionEnum wfe = one->_config.warpFunction; // use the even numbers settings for now
	    dfc.warpRT(wfe);
	    
	    Match_dataFetcher mdf;

            if (one->_config.searchNeighborhoodCalculator.size() > 0) 
	        {		  
		    SearchNeighborhoodCalculator snc = one->_config.parsedSNC;
		    PeptideMatcher pm_snc(dfc);
		    Peptide2FeatureMatcher p2fm_snc(dfc._pidf_a, dfc._fdf_b, snc);
		    one->_pm = pm_snc;
		    one->_p2fm = p2fm_snc;
		    
                    Match_dataFetcher mdf_snc(p2fm_snc.getMatches());
		    mdf = mdf_snc;
		   
		}

            else if (one->_config.normalDistributionSearch.size() > 0) 
	        {		 
		    NormalDistributionSearch nds = one->_config.parsedNDS;
		    nds.calculateTolerances(dfc);
		    PeptideMatcher pm_nds(dfc);
                    Peptide2FeatureMatcher p2fm_nds(dfc._pidf_a, dfc._fdf_b, nds);
		    one->_pm = pm_nds;
		    one->_p2fm = p2fm_nds;
		    
                    Match_dataFetcher mdf_nds(p2fm_nds.getMatches());
		    mdf = mdf_nds;

		}

	    
            one->_mdf.merge(mdf); // add the new MS1.5s to the merged set. ie we have all the MS1.5s from last time, and now a new set from these two together.	    
	    result.push_back(one);	   

        }
    
    return result;

}

boost::shared_ptr<AMTContainer> generateAMTDatabase(vector<boost::shared_ptr<AMTContainer> >& runs)
{
  // msmatchmake each pair of containers
  // repeat log runs.size() times
 
    vector<boost::shared_ptr<AMTContainer> > result = runs;
    result = pairwiseMatchmakeVector(result, true); // adjust RT on the first pass
    while (result.size() > 1) 
      {
 	  vector<boost::shared_ptr<AMTContainer> > newResult = pairwiseMatchmakeVector(result);
	  result = newResult;

      }

    return *(result.begin());

}

boost::shared_ptr<AMTContainer> generateAMTDatabase(vector<boost::shared_ptr<AMTContainer> >& runs, vector<pair<int, int> > tree)
{  
  vector<pair<int, int> >::iterator it = tree.begin();
  for(; it != tree.end(); ++it)
      {   	  	
 	  bool do_first = false;
	  bool do_second = false;

  	  if (!(runs.at(it->first)->rtAdjusted)) do_first = true;
	  if (!(runs.at(it->second)->rtAdjusted)) do_second = true;

	  pairwiseMatchmake(runs.at(it->first), runs.at(it->second), do_first, do_second); // changes a
	  runs.erase(find(runs.begin(), runs.end(), runs.at(it->second))); // erases b

      }

  return *(runs.begin());

}
