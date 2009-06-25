///
/// EharmonyAgglomerator.hpp
///

#ifndef _EHARMONYAGGLOMERATOR_HPP_
#define _EHARMONYAGGLOMERATOR_HPP_

#include "AMTContainer.hpp"

namespace pwiz{
namespace eharmony{

    void pairwiseMatchmake(boost::shared_ptr<AMTContainer> a, boost::shared_ptr<AMTContainer> b, bool doFirst = false, bool doSecond = false, WarpFunctionEnum wfe = Default, NormalDistributionSearch snc = NormalDistributionSearch());

    boost::shared_ptr<AMTContainer> generateAMTDatabase(vector<boost::shared_ptr<AMTContainer> >& runs, vector<pair<int, int> > tree, WarpFunctionEnum& wfe, NormalDistributionSearch& snc);

} 
}

#endif
