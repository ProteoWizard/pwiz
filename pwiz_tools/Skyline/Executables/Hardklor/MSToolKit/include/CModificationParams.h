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

#ifndef _CMODIFICATIONPARAMS_H
#define _CMODIFICATIONPARAMS_H

#include "CSearchModification.h"
#include <cmath>
#include <vector>

class CModificationParams{
public:
  //Constructors & Destructor
  //CModificationParams();
  //CModificationParams(const CModificationParams& c);
  //~CModificationParams();

  //Data members
  std::vector<CSearchModification> searchModification;

  //operators
  //CModificationParams& operator=(const CModificationParams& c);
  bool operator==(const CModificationParams& c);
  bool operator!=(const CModificationParams& c);

  //Functions
  void addSearchModification(bool fixed, double mass, std::string residues, bool protTerm = false);
  //void addSearchModification(CSearchModification& c);
  void addSearchModificationXL(double mass, std::string residues, std::string residues2);
  sCvParam findCvParam(double mass, std::string residues);
  sCvParam getModificationCvParam(double monoisotopicMassDelta, std::string residues, bool nTerm = false, bool cTerm = false);
  void writeOut(FILE* f, int tabs = -1);

private:
};

#endif
