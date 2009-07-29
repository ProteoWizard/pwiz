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

double square(const double& d){ return d*d;}

bool SearchNeighborhoodCalculator::close(const SpectrumQuery& a, const Feature& b) const
{
    return fabs(Ion::mz(a.precursorNeutralMass, a.assumedCharge) - b.mz) < _mzTol && fabs(a.retentionTimeSec - b.retentionTime) < _rtTol;

}

double SearchNeighborhoodCalculator::score(const SpectrumQuery& a, const Feature& b) const
{
    double mzdiff = fabs(Ion::mz(a.precursorNeutralMass, a.assumedCharge) - b.mz);
    double rtdiff = fabs(a.retentionTimeSec - b.retentionTime);

    return (1 - (mzdiff/_mzTol)*(rtdiff/_rtTol));

}

bool SearchNeighborhoodCalculator::operator==(const SearchNeighborhoodCalculator& that) const
{
    return _id == that._id &&
        _mzTol == that._mzTol &&
        _rtTol == that._rtTol;

}

bool SearchNeighborhoodCalculator::operator!=(const SearchNeighborhoodCalculator& that) const
{
    return (*this) != that;

}   

void NormalDistributionSearch::calculateTolerances(const DfcPtr dfc) 
{
    PeptideMatcher pm(dfc->_pidf_a, dfc->_pidf_b);
    pm.calculateDeltaRTDistribution();
    pm.calculateDeltaMZDistribution();

    // calculate normal distribution
    pair<double,double> mz_params = pm.getDeltaMZParams();
    pair<double,double> rt_params = pm.getDeltaRTParams();
    
    _mu_rt = rt_params.first;
    _sigma_rt = rt_params.second;

    _mu_mz = mz_params.first;
    _sigma_mz = mz_params.second;

    _mzTol = 100;
    _rtTol = 6000;

}

bool NormalDistributionSearch::close(const SpectrumQuery& a, const Feature& b) const
{
    return this->score(a, b) > _threshold;

}

double calculateFoldedNormalPval(const double& x, const double& mu, const double& sigma)
{ 
    double firstterm = -.5 * (1 + erf((-x-mu)/(sqrt(2) * sigma))) + .5 * (1 + erf((-mu)/(sqrt(2) * sigma))); 
    double secondterm = .5 * (1 + erf((x-mu)/(sqrt(2) * sigma))) - .5 * (1 + erf((-mu)/(sqrt(2) * sigma)));
    
    if (firstterm + secondterm < 0) 
        throw runtime_error(("[SearchNeighborhoodCalculator] Negative pval! x = " 
                             + boost::lexical_cast<string>(x) ).c_str());
    
    return ( firstterm + secondterm);

}

double NormalDistributionSearch::score(const SpectrumQuery& a, const Feature& b) const
{
    double mzDiff = fabs(Ion::mz(a.precursorNeutralMass, a.assumedCharge) - b.mz);
    double rtDiff = fabs(a.retentionTimeSec - b.retentionTime);

    double pval_mz = calculateFoldedNormalPval(mzDiff, _mu_mz, _sigma_mz);    
    double pval_rt = calculateFoldedNormalPval(rtDiff, _mu_rt, _sigma_rt);
    
    return (1-pval_mz)*(1-pval_rt); // not a legitimate p(h_0) but a quantitative measure of how bad/good

}

