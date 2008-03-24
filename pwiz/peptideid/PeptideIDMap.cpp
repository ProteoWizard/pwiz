//
// PeptideIDMap.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


#include "PeptideIDMap.hpp"


namespace pwiz {
namespace peptideid {


using namespace std;


PeptideID::Record PeptideIDMap::record(const string& nativeID) const
{
    map<string,PeptideID::Record>::const_iterator it = this->find(nativeID);
    if (it != this->end()) return it->second;
    return PeptideID::Record();
}


} // namespace peptideid
} // namespace pwiz

