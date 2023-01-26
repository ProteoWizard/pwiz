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

#ifndef _CSAMPLE_H
#define _CSAMPLE_H

#include "CContactRole.h"
#include "CSubSample.h"
#include "mzIMLStructs.h"
#include <string>
#include <vector>
#include <iostream>

class CSample {
public:

  //Data members
  std::string id;
  std::string name;
  
  std::vector<CContactRole> contactRole;
  std::vector<CSubSample> subSample;
  std::vector<sCvParam> cvParam;
  std::vector<sUserParam> userParam;

  //Functions
  void writeOut(FILE* f, int tabs = -1);

private:
};

#endif