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

#include "DatabaseQuery.hpp"
#include "PeptideID_dataFetcher.hpp"
#include "pwiz/utility/misc/Std.hpp"

using namespace pwiz::cv;
using namespace pwiz::eharmony;

const double pi = 3.1415926;

pair<double,double> DatabaseQuery::calculateSearchRegion(const double& mu1, const double& mu2, const double& sigma1, const double& sigma2, const double& threshold)
{
    double k1 = 1 + 2/sqrt(pi) * ( -2*mu2/(sigma2*sqrt(2)) + 2*pow(mu2,3)/(3*pow(sigma2*sqrt(2), 3)));
    double k2 = 1 + erf(-mu1/(sigma1*sqrt(2)));

    double mzDiameter = sqrt((sqrt(pi)/2*(threshold/k1 + k2 - 1) + 2*mu1/(sigma1*sqrt(2))) * 3*pow(sigma1*sqrt(2),3)/2 - pow(mu1,3));

    k1 = 1 + 2/sqrt(pi) * ( -2*mu1/(sigma1*sqrt(2)) + 2*pow(mu1,3)/(3*pow(sigma1*sqrt(2), 3)));
    k2 = 1 + erf(-mu2/(sigma2*sqrt(2)));

    double rtDiameter = sqrt((sqrt(pi)/2*(threshold/k1 + k2 - 1) + 2*mu2/(sigma2*sqrt(2))) * 3*pow(sigma2*sqrt(2),3)/2 - pow(mu2,3));

    return make_pair(mzDiameter, rtDiameter);

}

pair<double,double> DatabaseQuery::calculateNormalSearchRegion(const double& mu1, const double& mu2, double& sigma1, double& sigma2, const double& threshold)
{

    // Non weighted
    double mzDiameter = fabs((threshold - 1) * (sqrt(pi)/2)*sqrt(2)*sigma1);
    double rtDiameter = fabs((threshold - 1) * (sqrt(pi)/2)*sqrt(2)*sigma2);
    
    mzDiameter *= 2;
    rtDiameter *= 2;

    //    cout << mzDiameter << "\t" << rtDiameter << endl;
    return make_pair(mzDiameter, rtDiameter);

}

vector<MatchPtr> DatabaseQuery::query(FeatureSequencedPtr fs, NormalDistributionSearch nds, double threshold)
{
    Bin<SpectrumQuery> bin = _database->getBin();
    pair<double,double> tolerances = calculateNormalSearchRegion(nds._mu_mz, nds._mu_rt,  nds._sigma_mz, nds._sigma_rt, threshold);

    bin.rebin(tolerances.first, tolerances.second);
    
    vector<SpectrumQueryPtr> candidates;
    pair<double,double> fsCoords = make_pair(fs->feature->mz, fs->feature->retentionTime);

    bin.getAdjacentBinContents(fsCoords, candidates);
    vector<MatchPtr> resultingMatches;

    vector<SpectrumQueryPtr>::iterator it = candidates.begin();
    for( ; it != candidates.end(); ++it) 
        {
            MatchPtr match(new Match(**it, fs->feature));
            match->score = nds.score(**it, *(fs->feature));
            if (match->feature->charge == match->spectrumQuery.assumedCharge && match->score > threshold)
                {
                    resultingMatches.push_back(match);

                }

        }

    return resultingMatches;

}

