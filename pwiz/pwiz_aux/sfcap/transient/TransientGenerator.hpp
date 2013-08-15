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


#ifndef _TRANSIENTGENERATOR_HPP_
#define _TRANSIENTGENERATOR_HPP_


#include "Model.hpp"
#include "TransientData.hpp"
#include "pwiz/utility/proteome/IsotopeCalculator.hpp"
#include <memory>


namespace pwiz {
namespace id {


/// class for generating transient data from a model
class TransientGenerator
{
    public:

    TransientGenerator(const proteome::IsotopeCalculator& calculator);
    ~TransientGenerator();

    std::auto_ptr<pwiz::data::TransientData> 
        createTransientData(const model::ChromatographicFraction& cf);

    private:

    class Impl;
    std::auto_ptr<Impl> impl_;
};


} // namespace id 
} // namespace pwiz


#endif //  _TRANSIENTGENERATOR_HPP_ 

