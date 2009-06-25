///
/// EharmonyAgglomerator.cpp
///

#include "EharmonyAgglomerator.hpp"
#include "AMTContainer.hpp"

using namespace pwiz;
using namespace eharmony;

namespace pwiz{
namespace eharmony{

typedef boost::shared_ptr<DataFetcherContainer> DfcPtr;

void pairwiseMatchmake(boost::shared_ptr<AMTContainer> a, boost::shared_ptr<AMTContainer> b, bool doFirst, bool doSecond, WarpFunctionEnum wfe, NormalDistributionSearch snc)
{
    DfcPtr dfc(new DataFetcherContainer(a->_pidf, b->_pidf, a->_fdf, b->_fdf));
    dfc->adjustRT(doFirst, doSecond);
    dfc->warpRT(wfe);
    a->merge(*b);

	snc.calculateTolerances(dfc);
	PeptideMatcher pm_nds(dfc->_pidf_a, dfc->_pidf_b);
	a->_pm = pm_nds;

    return;

}

boost::shared_ptr<AMTContainer> generateAMTDatabase(vector<boost::shared_ptr<AMTContainer> >& runs, vector<pair<int, int> > tree, WarpFunctionEnum& wfe, NormalDistributionSearch& snc)
{  
    vector<pair<int, int> >::iterator it = tree.begin();
    for(; it != tree.end(); ++it)
        {   	  	
            cout << "it is below" << endl;
            bool do_first = false;
            bool do_second = false;
      
            if (!(runs.at(it->first)->rtAdjusted)) { do_first = true; runs.at(it->first)->rtAdjusted = true;}
            cout << "my dignity" << endl;
            if (!(runs.at(it->second)->rtAdjusted)) { do_second = true; runs.at(it->second)->rtAdjusted = true;}
            cout << "to stoop and utilize" << endl;
    
            pairwiseMatchmake(runs.at(it->first), runs.at(it->second), do_first, do_second, wfe, snc); // changes a
            cout << "gdb" << endl;
            runs.erase(find(runs.begin(), runs.end(), runs.at(it->second))); // erases b

        }

  return *(runs.begin());

}

} // namespace eharmony
} // namespace pwiz
