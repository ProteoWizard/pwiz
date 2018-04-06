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


#ifndef _FREQUENCYESTIMATORPHYSICALMODEL_HPP_ 
#define _FREQUENCYESTIMATORPHYSICALMODEL_HPP_ 


#include "pwiz/utility/misc/Export.hpp"
#include "FrequencyEstimator.hpp" 
#include <string>
#include <memory>


namespace pwiz {
namespace frequency {


/// Physical model implementation of the FrequencyEstimator interface. 
class PWIZ_API_DECL FrequencyEstimatorPhysicalModel : public FrequencyEstimator
{
    public:

    struct PWIZ_API_DECL Config
    {
        unsigned int windowRadius;
        unsigned int iterationCount;
        std::string outputDirectory; // ("" == no logging output)

        Config() : windowRadius(10), iterationCount(20) {}
    };

    /// create an instance
    static std::auto_ptr<FrequencyEstimatorPhysicalModel> create(const Config& config);

    /// \name FrequencyEstimator interface
    //@{
    virtual Peak estimate(const FrequencyData& fd, 
                          const Peak& initialEstimate) const = 0; 

    virtual ~FrequencyEstimatorPhysicalModel(){}
    //@}
};


} // namespace frequency
} // namespace pwiz


#endif // _FREQUENCYESTIMATORPHYSICALMODEL_HPP_ 


