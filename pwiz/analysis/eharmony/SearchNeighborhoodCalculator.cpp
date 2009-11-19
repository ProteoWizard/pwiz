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

double calculatePVal(const double& x, const double& mu, const double& sigma)
{
    double result = 0;
    
    if (x >= mu) result = .5 * (1 + erf((-x-mu)/(sqrt(2) * sigma)));
    else result = .5 * (1 + erf((x-mu)/(sqrt(2) * sigma)));

    result *= 2;   
    return result;
}

double NormalDistributionSearch::score(const SpectrumQuery& a, const Feature& b) const
{
    double mzDiff = (Ion::mz(a.precursorNeutralMass, a.assumedCharge) - b.mz);
    double rtDiff = (a.retentionTimeSec - b.retentionTime);

    double pval_mz = calculatePVal(mzDiff, _mu_mz, _sigma_mz);
    double pval_rt = calculatePVal(rtDiff, _mu_rt, _sigma_rt);

    return (pval_mz) * (pval_rt); 

}

