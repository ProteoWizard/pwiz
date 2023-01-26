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

#ifndef _CSPECTRUMIDENTIFICATIONRESULT_H
#define _CSPECTRUMIDENTIFICATIONRESULT_H

#include "CSpectrumIdentificationItem.h"
#include "mzIMLStructs.h"
#include <string>
#include <vector>

class CSpectrumIdentificationResult {
public:

  //Constructors & Destructor
  //CSpectrumIdentificationResult();
  //CSpectrumIdentificationResult(const CSpectrumIdentificationResult& s);
  //~CSpectrumIdentificationResult();

  //Data members
  std::string id;
  std::string name;
  std::string spectraDataRef;
  std::string spectrumID;
  std::vector<CSpectrumIdentificationItem> spectrumIdentificationItem;
  std::vector<sCvParam> cvParam;
  std::vector<sUserParam> userParam;

  //operators
  //CSpectrumIdentificationResult& operator=(const CSpectrumIdentificationResult& s);

  //Functions
  void addCvParam(std::string id, int value);
  void addCvParam(std::string id, double value);
  void addCvParam(std::string id, std::string value);
  //CSpectrumIdentificationItem* addSpectrumIdentificationItem(int z, double expMZ, int rnk, std::vector<sPeptideEvidenceRef>& peRef, bool pass = true, std::string pRef = "");
  //CSpectrumIdentificationItem* addSpectrumIdentificationItem(CSpectrumIdentificationItem& s);
  void writeOut(FILE* f, int tabs = -1);

private:
};

#endif
