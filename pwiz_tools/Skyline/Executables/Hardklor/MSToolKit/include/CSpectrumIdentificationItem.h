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

#ifndef _CSPECTRUMIDENTIFICATIONITEM_H
#define _CSPECTRUMIDENTIFICATIONITEM_H

#include "CFragmentation.h"
#include "mzIMLStructs.h"
#include <string>
#include <vector>

class CSpectrumIdentificationItem {
public:

  //Constructors & Destructor
  CSpectrumIdentificationItem();
  //CSpectrumIdentificationItem(const CSpectrumIdentificationItem& s);
  //~CSpectrumIdentificationItem();

  //Data members
  double calculatedMassToCharge;
  float calculatedPI;
  int chargeState;
  double experimentalMassToCharge;
  std::string id;
  std::string massTableRef;
  std::string name;
  bool passThreshold;
  std::string peptideRef;
  int rank;
  std::string sampleRef;
  std::vector<sPeptideEvidenceRef> peptideEvidenceRef;
  std::vector<CFragmentation> fragmentation;
  std::vector<sCvParam> cvParam;
  std::vector<sUserParam> userParam;

  //operators
  //CSpectrumIdentificationItem& operator=(const CSpectrumIdentificationItem& s);

  //Functions
  void addCvParam(sCvParam& s);
  void addCvParam(std::string accession, std::string cvRef, std::string name, std::string unitAccession = "", std::string unitCvRef = "", std::string unitName = "", std::string value = "");
  void addPeptideEvidenceRef(sPeptideEvidenceRef& s);
  void addPSMValue(std::string alg, std::string scoreID, int value, std::string prefix = "");
  void addPSMValue(std::string alg, std::string scoreID, double value, std::string prefix = "");
  void addPSMValue(std::string alg, std::string scoreID, std::string value, std::string prefix = "");
  void writeOut(FILE* f, int tabs = -1);

private:
};

#endif
