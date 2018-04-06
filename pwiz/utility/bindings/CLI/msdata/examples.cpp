//
// $Id$ 
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#include "examples.hpp"

#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "pwiz/data/msdata/examples.hpp"
#pragma warning( pop )

namespace pwiz {
namespace CLI {
namespace msdata {


void examples::initializeTiny(MSData^ msd)
{
    pwiz::msdata::examples::initializeTiny(**msd->base_);
}


void examples::addMIAPEExampleMetadata(MSData^ msd)
{
    pwiz::msdata::examples::addMIAPEExampleMetadata(**msd->base_);
}


} // namespace msdata
} // namespace CLI
} // namespace pwiz