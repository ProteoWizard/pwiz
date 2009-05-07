///
/// EharmonyAgglomerator.cpp
///

#include "AMTContainer.hpp"
#include "Peptide2FeatureMatcher.hpp"

using namespace pwiz;
using namespace eharmony;

vector<AMTContainer> pairwiseMatchmake(vector<AMTContainer> runs)
{
    vector<AMTContainer> result;
    vector<AMTContainer>::iterator it = runs.begin();

    for(; it < runs.end()-1; ++it)
        { 
	   
	    AMTContainer& one = *it;
	    it++;
	    AMTContainer& two = *it;

  	    DataFetcherContainer dfc(one._pidf, two._pidf, one._fdf, two._fdf);
	    dfc.adjustRT();
	    one.merge(two);
	    
            WarpFunctionEnum wfe = one._config.warpFunction; // use the even numbers settings for now
	    dfc.warpRT(wfe);
	    
	    Match_dataFetcher mdf;

            if (one._config.searchNeighborhoodCalculator.size() > 0) 
	        {
		    SearchNeighborhoodCalculator snc = one._config.parsedSNC;
		    PeptideMatcher pm_snc(dfc);
		    Peptide2FeatureMatcher p2fm_snc(dfc._pidf_a, dfc._fdf_b, snc);
		    one._pm = pm_snc;
		    one._p2fm = p2fm_snc;
		    
                    Match_dataFetcher mdf_snc(p2fm_snc.getMatches());
		    mdf = mdf_snc;
		   
		}

            else if (one._config.normalDistributionSearch.size() > 0) 
	        {
		    NormalDistributionSearch nds = one._config.parsedNDS;
		    nds.calculateTolerances(dfc);
		    PeptideMatcher pm_nds(dfc);
                    Peptide2FeatureMatcher p2fm_nds(dfc._pidf_a, dfc._fdf_b, nds);
		    one._pm = pm_nds;
		    one._p2fm = p2fm_nds;

                    Match_dataFetcher mdf_nds(p2fm_nds.getMatches());
		    mdf = mdf_nds;

		}

	    
            one._mdf.merge(mdf); // add the new MS1.5s to the merged set. ie we have all the MS1.5s from last time, and now a new set from these two together.	    
	    result.push_back(one);

        }

    return result;

}

AMTContainer generateAMTDatabase(vector<AMTContainer>& runs)
{
  // msmatchmake each pair of containers
  // repeat log runs.size() times
 
    vector<AMTContainer> result = runs;
    cout << "result.size()" << result.size() << endl;
    while (result.size() > 1) 
      {
	  result = pairwiseMatchmake(result);
	

      }

    return *result.begin();

}
