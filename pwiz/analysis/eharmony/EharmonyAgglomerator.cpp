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
