///
/// SearchNeighborhoodCalculator.cpp
///

#include "SearchNeighborhoodCalculator.hpp"
#include "PeptideMatcher.hpp"
#include "pwiz/utility/proteome/Ion.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"

using namespace pwiz::eharmony;
using namespace pwiz::proteome;
using namespace pwiz::minimxml;

// namespace {

// double zscore(double value, double mean, double stdev)
// {
//     return (fabs(value - mean) / stdev);

// }

// } // anonymous namespace

bool SearchNeighborhoodCalculator::close(const SpectrumQuery& a, const Feature& b) const
{
    return fabs(Ion::mz(a.precursorNeutralMass, a.assumedCharge) - b.mzMonoisotopic) < _mzTol && fabs(a.retentionTimeSec - b.retentionTime) < _rtTol;

}

double SearchNeighborhoodCalculator::score(const SpectrumQuery& a, const Feature& b) const
{
    double mzdiff = fabs(Ion::mz(a.precursorNeutralMass, a.assumedCharge) - b.mzMonoisotopic);
    double rtdiff = fabs(a.retentionTimeSec - b.retentionTime);

    return (1 - (mzdiff/_mzTol)*(rtdiff/_rtTol));

}

void NormalDistributionSearch::calculateTolerances(const DataFetcherContainer& dfc) 
{
    PeptideMatcher pm(dfc);
    pm.calculateDeltaRTDistribution();
    pair<double,double> params = pm.getDeltaRTParams();
    
    _mu = params.first;
    _sigma = params.second;

    _mzTol = .005; // until we also do a distribution for mz
    _rtTol = _Z*_sigma + _mu; 

}

double NormalDistributionSearch::score(const SpectrumQuery& a, const Feature& b) const
{
    double rtDiff = fabs(a.retentionTimeSec - b.retentionTime);
    double pval = 0.5 * erfc(-(rtDiff - _mu)/(sqrt(2)*_sigma));
    
    return (1 - pval); // not a legitimate p(h_0) but a quantitative measure of how bad/good

}

