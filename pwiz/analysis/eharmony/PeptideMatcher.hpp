///
/// PeptideMatcher.hpp
///

#ifndef _PEPTIDEMATCHER_HPP_
#define _PEPTIDEMATCHER_HPP_

// my stuff
#include "DataFetcherContainer.hpp"

using namespace pwiz::data::pepxml;

namespace pwiz{
namespace eharmony{

typedef std::vector<std::pair<SpectrumQuery, SpectrumQuery> > PeptideMatchContainer;

class PeptideMatcher
{

public:

    PeptideMatcher(){} 
    PeptideMatcher(const DataFetcherContainer& dfc);

    PeptideMatchContainer getMatches() const { return _matches;}
    void calculateDeltaRTDistribution(); // find mean and stdev deltaRT 
    pair<double,double> getDeltaRTParams() const { return make_pair(_meanDeltaRT, _stdevDeltaRT); }

private:

    PeptideMatchContainer _matches;

    double _meanDeltaRT;
    double _stdevDeltaRT;

};

} // namespace eharmony
} // namespace pwiz

#endif //_PEPTIDEMATCHER_HPP_
