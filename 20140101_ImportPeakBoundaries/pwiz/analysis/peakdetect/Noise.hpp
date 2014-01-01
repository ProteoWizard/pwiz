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
                                                                                                     
#ifndef _NOISECALCULATOR_HPP_
#define _NOISECALCULATOR_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/math/OrderedPair.hpp"


namespace pwiz {
namespace analysis {


struct PWIZ_API_DECL Noise
{
    double mean;
    double variance;
    double standardDeviation;

    Noise(double m=0, double sd=0);
    double pvalue(double value) const; // pvalue for a value, given this noise distribution
};


class PWIZ_API_DECL NoiseCalculator
{
    public:
    
    virtual Noise calculateNoise(const math::OrderedPairContainerRef& pairs) const = 0; 
    virtual ~NoiseCalculator(){}
};


class PWIZ_API_DECL NoiseCalculator_2Pass : public NoiseCalculator
{
    public:

    struct Config
    {
        double zValueCutoff;
        Config() : zValueCutoff(1) {}
    };
    
    NoiseCalculator_2Pass(const Config& config = Config());
    virtual Noise calculateNoise(const math::OrderedPairContainerRef& pairs) const; 

    private:
    Config config_;
};


} // namespace analysis
} // namespace pwiz


#endif // _NOISECALCULATOR_HPP_ 

