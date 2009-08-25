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

double calculatePVal(const double& x, const double& mu, const double& sigma)
{
    double result = 0;
    
    if (x >= mu) result = .5 * (1 + erf((-x-mu)/(sqrt(2) * sigma)));
    else result = .5 * (1 + erf((x-mu)/(sqrt(2) * sigma)));

    result *= 2;   
    return result;
}

const double epsilon = 2 * numeric_limits<double>::epsilon();
const double arbitrarilyLarge = 100000000;

const double null_mu_mz = 0;
const double null_sigma_mz = 0.001;

const double null_mu_rt = 0;
const double null_sigma_rt = 10;

double calculateMzPVal(const double& x, const double& mu, const double& sigma)
{
    double result = 0;
    if (x >= mu) result = .5 * (1 + erf((-x - (null_mu_mz + mu))/(sqrt(2) * (sqrt(square(null_sigma_mz) + square(sigma))))));
    else result = .5 * (1 + erf((x - (null_mu_mz + mu))/(sqrt(2) * (sqrt(square(null_sigma_mz) + square(sigma))))));
   
    return result;
}

double calculateRtPVal(const double& x, const double& mu, const double& sigma)
{
    double result = 0;
    if (x >= mu) result = .5 * (1 + erf((-x - (null_mu_rt + mu))/(sqrt(2) * (sqrt(square(null_sigma_rt) + square(sigma))))));
    else result = .5 * (1 + erf((x - (null_mu_rt + mu))/(sqrt(2) * (sqrt(square(null_sigma_rt) + square(sigma))))));

    return result;
}
double NormalDistributionSearch::score(const SpectrumQuery& a, const Feature& b) const
{
    double mzDiff = (Ion::mz(a.precursorNeutralMass, a.assumedCharge) - b.mz);
    double rtDiff = (a.retentionTimeSec - b.retentionTime);

    //    double pval_mz = calculateFoldedNormalPval(mzDiff, _mu_mz, _sigma_mz);    
    //    double pval_rt = calculateFoldedNormalPval(rtDiff, _mu_rt, _sigma_rt);
    
    double pval_mz = 0;
    double pval_rt = 0;

    // Weighting by mzDiff and rtDiff as well as scales on which we expect this diff to occur
    /* if (fabs(mzDiff) < epsilon) pval_mz = arbitrarilyLarge;
    else pval_mz = calculatePVal(mzDiff, _mu_mz, _sigma_mz) / mzDiff * 1000;

    if (fabs(rtDiff) < epsilon) pval_rt = arbitrarilyLarge;
    else pval_rt = calculatePVal(rtDiff, _mu_rt, _sigma_rt) / (rtDiff * 100);
    */
    pval_mz = calculatePVal(mzDiff, _mu_mz, _sigma_mz);
    pval_rt = calculatePVal(rtDiff, _mu_rt, _sigma_rt);

    //    cout.precision(16);
    //    cout << pval_mz << "\t" << mzDiff << "\t" << pval_rt << "\t" << rtDiff << endl;

    return (pval_mz) * (pval_rt); // scores will be crazy numbers for now

}

