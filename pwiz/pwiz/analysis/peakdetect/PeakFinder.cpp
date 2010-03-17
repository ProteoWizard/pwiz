//
// $Id$
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
#include <iomanip>


namespace pwiz {
namespace analysis {


using namespace pwiz::math;
using namespace std;
using boost::shared_ptr;


PeakFinder_SNR::PeakFinder_SNR(shared_ptr<NoiseCalculator> noiseCalculator, const Config& config)
:   noiseCalculator_(noiseCalculator), config_(config)
{}


namespace {

struct ComputeLogarithm
{
    OrderedPair operator()(const OrderedPair& p)
    {
        double value = p.y>0 ? log(p.y) : 0;
        return OrderedPair(p.x, value);
    }
};

struct CalculatePValue
{
    double operator()(const OrderedPair& p) {return noise_.pvalue(p.y);}
    CalculatePValue(const Noise& noise) : noise_(noise) {}
    const Noise& noise_; 
};

vector<double> calculateRollingProducts(const vector<double>& in, size_t radius)
{
    vector<double> out;
    vector<double>::const_iterator begin = in.begin();
    vector<double>::const_iterator end = in.end();

    for (vector<double>::const_iterator it=begin; it!=end; ++it)
    {
        double product = *it;
        for (int i=1; i<=(int)radius; i++)
        {
            // Note that the non-intuitive iterator arithmetic (with result signed int)
            // is used to appease MSVC's checked iterators.
            //   (it-begin >= i) <-> (it-i >= begin) 

            if (it-begin >= i) product *= *(it-i);
            if (i < end-it) product *= *(it+i);
        }

        out.push_back(product); 
    }

    return out;
}

} // namespace


void PeakFinder_SNR::findPeaks(const OrderedPairContainerRef& pairs,
                               vector<size_t>& resultIndices) const
{
    vector<OrderedPair> preprocessedData;
    if (config_.preprocessWithLogarithm)
        transform(pairs.begin(), pairs.end(), back_inserter(preprocessedData), ComputeLogarithm());  

    const OrderedPairContainerRef& data = config_.preprocessWithLogarithm ? preprocessedData : pairs;

    Noise noise = noiseCalculator_->calculateNoise(data); // TODO: investigate calculating noise on unprocessed data

    vector<double> pvalues;
    transform(data.begin(), data.end(), back_inserter(pvalues), CalculatePValue(noise));
    
    vector<double> rollingProducts = calculateRollingProducts(pvalues, config_.windowRadius);
    if (rollingProducts.size() != data.size()) 
        throw runtime_error("[PeakFinder_SNR::findPeaks()] This isn't happening"); 

    double thresholdValue = noise.mean + config_.zValueThreshold * noise.standardDeviation;
    double thresholdPValue = noise.pvalue(thresholdValue);
    double threshold = ((double(*)(double,int))pow)(thresholdPValue, 1+2*config_.windowRadius);

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

    if (config_.log) 
    {
        ostream& log = *config_.log;

        log << "[PeakFinder_SNR::findPeaks()]\n";

        log << "# noise: " << noise.mean << " " << noise.standardDeviation << endl;
        log << "# thresholdValue: " << thresholdValue << endl;
        log << "# thresholdPValue: " << thresholdPValue << endl;
        log << "# threshold: " << threshold << endl;
        log << "#\n";

        log << "# found data pvalue rollingProduct\n";
        for (size_t i=0; i<data.size(); ++i)
        {
            bool found = (find(resultIndices.begin(), resultIndices.end(), i) != resultIndices.end());
            log << (found?"* ":"  ") << fixed << setprecision(6) << data[i] << scientific << setw(15) << pvalues[i] << setw(15) << rollingProducts[i] << endl;
        }
        log << endl;

        log << "[PeakFinder_SNR::findPeaks()] end\n";
    }
}


} // namespace analysis
} // namespace pwiz


