//
// PeakFinder.cpp
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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


#define PWIZ_SOURCE
#include "PeakFinder.hpp"
#include <algorithm>
#include <iterator>
#include <stdexcept>
#include <cmath>


namespace pwiz {
namespace analysis {


using namespace pwiz::math;
using namespace std;


PeakFinder_SNR::PeakFinder_SNR(const NoiseCalculator& noiseCalculator, const Config& config)
:   noiseCalculator_(noiseCalculator), config_(config)
{}


namespace {

struct CalculatePValue
{
    double operator()(const OrderedPair& p) {return noise_.pvalue(p.y);}
    CalculatePValue(Noise noise) : noise_(noise) {}
    Noise& noise_; 
};

vector<double> calculateRollingProducts(const vector<double>& in, size_t radius)
{
    vector<double> out;
    vector<double>::const_iterator begin = in.begin();
    vector<double>::const_iterator end = in.end();

    for (vector<double>::const_iterator it=begin; it!=end; ++it)
    {
        double product = *it;
        for (size_t i=1; i<=radius; i++)
        {
            if (it-i >= begin) product *= *(it-i);
            if (it+i < end) product *= *(it+i);
        }

        out.push_back(product); 
    }

    return out;
}

} // namespace


void PeakFinder_SNR::findPeaks(const OrderedPairContainerRef& pairs,
                               vector<size_t>& resultIndices) const
{
    Noise noise = noiseCalculator_.calculateNoise(pairs);
    
    vector<double> pvalues;
    transform(pairs.begin(), pairs.end(), back_inserter(pvalues), CalculatePValue(noise));
    
    vector<double> rollingProducts = calculateRollingProducts(pvalues, config_.windowRadius);
    if (rollingProducts.size() != pairs.size()) 
        throw runtime_error("[PeakFinder_SNR::findPeaks()] This isn't happening"); 

    double thresholdValue = noise.mean + config_.zValueThreshold * noise.standardDeviation;
    double thresholdPValue = noise.pvalue(thresholdValue);
    double threshold = pow(thresholdPValue, 1+2*config_.windowRadius);

    // report local minima above the threshold
    for (size_t i=0; i<rollingProducts.size(); i++)
    {
        if ((i==0 || rollingProducts[i]<rollingProducts[i-1]) &&
            (i+1==rollingProducts.size() || rollingProducts[i]<rollingProducts[i+1]) &&
            rollingProducts[i] < threshold)
        { 
            resultIndices.push_back(i);
        }
    }

#if 0
    cout << "noise: " << noise.mean << " " << noise.standardDeviation << endl;
    copy(pairs.begin(), pairs.end(), ostream_iterator<OrderedPair>(cout, "\n"));

    cout << "pvalues:\n";
    copy(pvalues.begin(), pvalues.end(), ostream_iterator<double>(cout, "\n"));
    cout << endl;

    cout << "rollingProducts:\n";
    copy(rollingProducts.begin(), rollingProducts .end(), ostream_iterator<double>(cout, "\n"));
    cout << endl;

    cout << "thresholdValue: " << thresholdValue << endl;
    cout << "thresholdPValue: " << thresholdPValue << endl;
    cout << "threshold: " << threshold << endl;
#endif
}


} // namespace analysis
} // namespace pwiz


