/*
Copyright 2020, Michael R. Hoopmann, Institute for Systems Biology
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
    http://www.apache.org/licenses/LICENSE-2.0
Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

#ifndef _CENZYME_H
#define _CENZYME_H

#include "CEnzymeName.h"
#include "CSiteRegexp.h"
#include "mzIMLStructs.h"
#include <string>
#include <vector>

class CEnzyme {
public:

  //Constructor
  CEnzyme();

  //Data members
  std::string cTermGain;
  std::string id;
  int minDistance;
  int missedCleavages;
  std::string nTermGain;
  std::string name;
  bool semiSpecific;
  
  std::vector<CSiteRegexp> siteRegexp;
  std::vector<CEnzymeName> enzymeName;

  //Functions
  void writeOut(FILE* f, int tabs = -1);

private:
};

#endif
