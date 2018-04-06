//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
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


#ifndef _TRUNCATEDLORENTZIANESTIMATOR_HPP_
#define _TRUNCATEDLORENTZIANESTIMATOR_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "TruncatedLorentzianParameters.hpp"
#include "pwiz/data/misc/FrequencyData.hpp"
#include <memory>
#include <iosfwd>


namespace pwiz {
namespace frequency {


class PWIZ_API_DECL TruncatedLorentzianEstimator
{
    public:

    static std::auto_ptr<TruncatedLorentzianEstimator> create();

    virtual TruncatedLorentzianParameters initialEstimate(const pwiz::data::FrequencyData& fd) const = 0;

    virtual TruncatedLorentzianParameters iteratedEstimate(const pwiz::data::FrequencyData& fd,
                                                           const TruncatedLorentzianParameters& tlp,
                                                           int iterationCount) const = 0;

    virtual double error(const pwiz::data::FrequencyData& fd, const TruncatedLorentzianParameters& tlp) const = 0;
    virtual double normalizedError(const pwiz::data::FrequencyData& fd, const TruncatedLorentzianParameters& tlp) const = 0;
    virtual double sumSquaresModel(const pwiz::data::FrequencyData& fd, const TruncatedLorentzianParameters& tlp) const = 0;

    virtual void log(std::ostream* os) = 0; // set log stream [default == &cout] 
    virtual void outputDirectory(const std::string& name) = 0; // set intermediate output [default=="" (none)]  

    virtual ~TruncatedLorentzianEstimator(){}
};


} // namespace frequency
} // namespace pwiz


#endif // _TRUNCATEDLORENTZIANESTIMATOR_HPP_ 

