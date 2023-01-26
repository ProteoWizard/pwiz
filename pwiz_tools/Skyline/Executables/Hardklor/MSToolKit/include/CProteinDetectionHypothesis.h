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

#ifndef _CPROTEINDETECTIONHYPOTHESIS_H
#define _CPROTEINDETECTIONHYPOTHESIS_H

#include "CPeptideHypothesis.h"
#include "mzIMLStructs.h"
#include <string>
#include <vector>

class CProteinDetectionHypothesis{
public:

  //Constructors & Destructors
  CProteinDetectionHypothesis();
  //CProteinDetectionHypothesis(const CProteinDetectionHypothesis& c);
  //~CProteinDetectionHypothesis();

  //Operators
  //CProteinDetectionHypothesis& operator=(const CProteinDetectionHypothesis& c);
  //bool operator==(const CProteinAmbiguityGroup& c);

  //Data members
  std::string dbSequenceRef;
  std::string id;
  std::string name;
  bool passThreshold;
  std::vector<CPeptideHypothesis> peptideHypothesis;
  std::vector<sCvParam> cvParam;
  std::vector<sUserParam> userParam;

  //Functions
  void addParamValue(std::string alg, std::string scoreID, double value);
  void addParamValue(std::string alg, std::string scoreID, std::string value);
  CPeptideHypothesis* addPeptideHypothesis(std::string& pe);
  void writeOut(FILE* f, int tabs = -1);

private:
};

#endif
