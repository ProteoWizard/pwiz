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
    pm.calculateDeltaMZDistribution();

    pair<double,double> mz_params = pm.getDeltaMZParams();
    pair<double,double> rt_params = pm.getDeltaRTParams();
    
    _mu_rt = rt_params.first;
    _sigma_rt = rt_params.second;

    _mu_mz = mz_params.first;
    _sigma_mz = mz_params.second;

    _mzTol = _Z*_sigma_mz;
    _rtTol = _Z*_sigma_rt;

}

double NormalDistributionSearch::score(const SpectrumQuery& a, const Feature& b) const
{
    double mzDiff = fabs(Ion::mz(a.precursorNeutralMass, a.assumedCharge) - b.mzMonoisotopic);
    double rtDiff = fabs(a.retentionTimeSec - b.retentionTime);
    double pval_mz= 0.5 * erfc(-(mzDiff - _mu_mz)/(sqrt(2)*_sigma_mz));
    double pval_rt = 0.5 * erfc(-(rtDiff - _mu_rt)/(sqrt(2)*_sigma_rt));
    
    return (1 - (pval_mz*pval_rt)); // not a legitimate p(h_0) but a quantitative measure of how bad/good

}

