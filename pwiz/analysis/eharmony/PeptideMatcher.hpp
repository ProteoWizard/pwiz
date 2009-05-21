///
/// PeptideMatcher.hpp
///

#ifndef _PEPTIDEMATCHER_HPP_
#define _PEPTIDEMATCHER_HPP_

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
    void calculateDeltaMZDistribution();

    pair<double,double> getDeltaRTParams() const { return make_pair(_meanDeltaRT, _stdevDeltaRT); }
    pair<double,double> getDeltaMZParams() const { return make_pair(_meanDeltaMZ, _stdevDeltaMZ); }

    bool operator==(const PeptideMatcher& that);
    bool operator!=(const PeptideMatcher& that);

private:

    PeptideMatchContainer _matches;

    double _meanDeltaRT;
    double _stdevDeltaRT;

    double _meanDeltaMZ;
    double _stdevDeltaMZ;

};

} // namespace eharmony
} // namespace pwiz

#endif //_PEPTIDEMATCHER_HPP_
