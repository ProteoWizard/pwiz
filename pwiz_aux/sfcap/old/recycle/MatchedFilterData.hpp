//
// $Id$
//
//
// Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _MATCHEDFILTERDATA_HPP_
#define _MATCHEDFILTERDATA_HPP_


#include "data/FrequencyData.hpp"
#include <memory>
#include <complex>
#include <vector>
#include <iostream>


namespace pwiz {
namespace peaks {


/// class for calculation of a matched filter applied to FrequencyData
class MatchedFilterData
{
    public:

    /// correlation kernel interface, to be implemented by the client
    class Kernel
    {
        public:
        virtual std::complex<double> operator()(double frequency) const = 0;
        virtual ~Kernel(){} 
    };

    /// instantiation
    /// fd is assumed to exist for the lifetime of the MatchedFilterData object.
    /// kernel is implemented by the client; existence is only assumed during the construction call.
    /// sampleFactor is the number of filter shift increments per frequency increment. 
    /// sampleRadius is the number of filter samples taken on either side of 0
    MatchedFilterData(const pwiz::data::FrequencyData& fd, const Kernel& kernel, int sampleFactor, int sampleRadius);
    ~MatchedFilterData();

    /// apply the filter to the entire FrequencyData set and cache results
    void compute();

    /// fills in list of peaks (frequencies) such that: 
    ///   abs(correlation) >= minMagnitude and
    ///   angle(correlation) <= maxAngle (degrees)
    void findPeaks(double minMagnitude, double maxAngle, std::vector<double>& result) const;

    /// returns correlation value at sample closest to target frequency 
    std::complex<double> correlationValue(double targetFrequency) const;

    /// returns correlation angle (degrees) at sample closest to target frequency 
    double correlationAngle(double targetFrequency) const;

    /// outputs filters (sampled kernel) 
    void printFilters(std::ostream& os = std::cout) const; 

    /// outputs full correlation matrix
    void printCorrelationMatrix(std::ostream& os = std::cout) const; 
    

    private:
    class Impl;
    std::auto_ptr<Impl> impl_;
};


} // namespace peaks
} // namespace pwiz


#endif // _MATCHEDFILTERDATA_HPP_

