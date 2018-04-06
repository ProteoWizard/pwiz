//
// $Id$ 
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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

#include "examples.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace proteome {
namespace examples {




PWIZ_API_DECL void initializeTiny(ProteomeData& pd)
{
    pd.id = "tiny";

    shared_ptr<ProteinListSimple> proteinListPtr(new ProteinListSimple);
    pd.proteinListPtr = proteinListPtr;

    proteinListPtr->proteins.push_back(ProteinPtr(new Protein("ABC123", 0, "One two three.", "ELVISLIVES")));
    proteinListPtr->proteins.push_back(ProteinPtr(new Protein("ZEBRA", 1, "Has stripes:", "BLACKANDWHITE")));
    proteinListPtr->proteins.push_back(ProteinPtr(new Protein("DEFCON42", 2, "", "DNTPANIC")));

} // initializeTiny()


//PWIZ_API_DECL void addMIAPEExampleMetadata(ProteomeData& pd) {} // addMIAPEExampleMetadata()


} // namespace examples
} // namespace tdata
} // namespace pwiz
