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
    return fabs(Ion::mz(a.precursorNeutralMass, a.assumedCharge) - b.mz) < _mzTol && fabs(a.retentionTimeSec - b.retentionTime) < _rtTol;

}

double SearchNeighborhoodCalculator::score(const SpectrumQuery& a, const Feature& b) const
{
    double mzdiff = fabs(Ion::mz(a.precursorNeutralMass, a.assumedCharge) - b.mz);
    double rtdiff = fabs(a.retentionTimeSec - b.retentionTime);

    return (1 - (mzdiff/_mzTol)*(rtdiff/_rtTol));

}

void NormalDistributionSearch::calculateTolerances(const DfcPtr dfc) 
{
    PeptideMatcher pm(dfc->_pidf_a, dfc->_pidf_b);
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

bool NormalDistributionSearch::close(const SpectrumQuery& a, const Feature& b) const
{
    cout << "using close" << endl;
    return (this->score(a,b) > .006);
}

double NormalDistributionSearch::score(const SpectrumQuery& a, const Feature& b) const
{
    const double e = 2.718;
    const double pi = 3.14159;

    double mzDiff = (Ion::mz(a.precursorNeutralMass, a.assumedCharge) - b.mz);
    double rtDiff = (a.retentionTimeSec - b.retentionTime);
    double pval_rt = 0;
    //    double pval_mz= 0.5 * erfc(-(mzDiff - _mu_mz)/(sqrt(2)*_sigma_mz));
    //    double pval_rt = 0.5 * erfc(-(rtDiff - _mu_rt)/(sqrt(2)*_sigma_rt));
    if (rtDiff < _mu_rt) 
        {
            pval_rt = 1/(_sigma_rt * sqrt(2*pi));
            //            cout << " first: " << endl;
            //            pval_rt -= 1/(_sigma_rt * sqrt(2*pi))*pow(e,(-((rtDiff-_mu_rt)*(rtDiff - _mu_rt))/(2*pow(_sigma_rt,2))));
            pval_rt -= 1/(_sigma_rt * sqrt(2*pi)) * pow(e,-(rtDiff- _mu_rt)*(rtDiff - _mu_rt)/(2*_sigma_rt * _sigma_rt)) ;
            //cout << "rtDiff: " << rtDiff << endl;
            /*cout << "mu rt: " << _mu_rt << endl;
            cout << "sigma: " << _sigma_rt << endl;
            cout << "numerator: " << -(rtDiff-_mu_rt)*(rtDiff-_mu_rt) << endl;
            cout << "denominator: " << 2*_sigma_rt * _sigma_rt << endl;
            */
      }

    if (rtDiff >= _mu_rt)
        {
            pval_rt = 1/(_sigma_rt * sqrt(2*pi))*pow(e,(-pow((rtDiff - _mu_rt),2)/(2*_sigma_rt*_sigma_rt))) -1/(_sigma_rt * (sqrt(2*pi)));
            //            cout << "bigger: " << pval_rt << endl;

        }

    //    cout << "rtdiff: " << rtDiff << " pval_rt: " << pval_rt << " score: " << 1-pval_rt << endl;
    return (pval_rt); //test
    //    return ((1 - pval_mz)*(1-pval_rt)); // not a legitimate p(h_0) but a quantitative measure of how bad/good

}

