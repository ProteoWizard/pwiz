//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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


#define PWIZ_SOURCE

#include "PeakFamilyDetector.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace analysis {
    

PWIZ_API_DECL
void PeakFamilyDetector::detect(const vector<MZIntensityPair>& data,
                                vector<PeakFamily>& result)
{
    if (data.empty()) return;
    detect(&data[0], &data[0] + data.size(), result);
}


} // namespace analysis 
} // namespace pwiz

