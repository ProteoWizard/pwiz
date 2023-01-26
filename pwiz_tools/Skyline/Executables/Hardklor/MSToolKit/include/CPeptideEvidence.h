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

#ifndef _CPEPTIDEEVIDENCE_H
#define _CPEPTIDEEVIDENCE_H

#include "mzIMLStructs.h"
#include <string>
#include <vector>

class CPeptideEvidence{
public:

  //Constructors & Destructors
  CPeptideEvidence();
  //CPeptideEvidence(const CPeptideEvidence& p);
  //~CPeptideEvidence();

  //Data members
  std::string dbSequenceRef;
  int end;
  //string frame; //not clear from the docs what this data structure really is
  std::string id;
  bool isDecoy;
  std::string name;
  std::string peptideRef;
  char post;
  char pre;
  int start;
  std::string translationTableRef;
  std::vector<sCvParam> cvParam;
  std::vector<sUserParam> userParam;

  //operators
  //CPeptideEvidence& operator=(const CPeptideEvidence& p);
  bool operator==(const CPeptideEvidence& p);

  //Functions
  void writeOut(FILE* f, int tabs = -1);

private:
};

#endif
