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

#ifndef _CPROTEINAMBIGUITYGROUP_H
#define _CPROTEINAMBIGUITYGROUP_H

#include "CProteinDetectionHypothesis.h"
#include "mzIMLStructs.h"
#include <string>
#include <vector>

class CProteinAmbiguityGroup{
public:

  //Constructors & Destructors
  //CProteinAmbiguityGroup();
  //CProteinAmbiguityGroup(const CProteinAmbiguityGroup& c);
  //~CProteinAmbiguityGroup();

  //Operators
  //CProteinAmbiguityGroup& operator=(const CProteinAmbiguityGroup& c);
  //bool operator==(const CProteinAmbiguityGroup& c);

  //Data members
  std::string id;
  std::string name;
  std::vector<CProteinDetectionHypothesis> proteinDetectionHypothesis;
  std::vector<sCvParam> cvParam;
  std::vector<sUserParam> userParam;

  //Functions
  void addParamValue(std::string alg, std::string scoreID, double value);
  void addParamValue(std::string alg, std::string scoreID, std::string value);
  CProteinDetectionHypothesis* addProteinDetectionHypothesis(std::string baseRef, std::string dbSequenceRef, bool passThreshold = true);
  void writeOut(FILE* f, int tabs = -1);

private:
};

#endif
