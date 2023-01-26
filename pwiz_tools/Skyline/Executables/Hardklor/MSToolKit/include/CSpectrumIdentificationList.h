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

#ifndef _CSPECTRUMIDENTIFICATIONLIST_H
#define _CSPECTRUMIDENTIFICATIONLIST_H

#include "CFragmentationTable.h"
#include "CSpectrumIdentificationResult.h"
#include "mzIMLStructs.h"
#include <string>
#include <vector>

class CSpectrumIdentificationList {
public:

  //Constructors & Destructor
  CSpectrumIdentificationList();
  //CSpectrumIdentificationList(const CSpectrumIdentificationList& s);
  //~CSpectrumIdentificationList();

  //Data members
  std::string id;
  std::string name;
  int numSequencesSearched;
  std::vector<CFragmentationTable> fragmentationTable;
  std::vector<CSpectrumIdentificationResult> spectrumIdentificationResult;
  std::vector<sCvParam> cvParam;
  std::vector<sUserParam> userParam;

  //operators
  //CSpectrumIdentificationList& operator=(const CSpectrumIdentificationList& s);

  //Functions
  //CSpectrumIdentificationResult* addSpectrumIdentificationResult(std::string specID, std::string& sdRef);
  //CSpectrumIdentificationResult* addSpectrumIdentificationResult(CSpectrumIdentificationResult& c);
  void writeOut(FILE* f, int tabs = -1);

private:
};

#endif
