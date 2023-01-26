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

#ifndef _CPEPTIDE_H
#define _CPEPTIDE_H

#include "CModification.h"
#include "mzIMLStructs.h"
#include <cmath>
#include <string>
#include <vector>

class CPeptide{
public:

  //Constructors & Destructors
  //CPeptide();
  //CPeptide(const CPeptide& p);
  //~CPeptide();

  //Operators
  //CPeptide& operator=(const CPeptide& p);
  bool operator==(const CPeptide& p);

  //Data members
  std::string id;
  std::string name;
  sPeptideSequence peptideSequence;
  std::vector<CModification> modification;
  std::vector<sSubstitutionModification> substitutionModification;
  std::vector<sCvParam> cvParam;
  std::vector<sUserParam> userParam;

  //Functions
  bool compareModsSoft(CPeptide& p);
  void writeOut(FILE* f, int tabs = -1);

private:
};

#endif
