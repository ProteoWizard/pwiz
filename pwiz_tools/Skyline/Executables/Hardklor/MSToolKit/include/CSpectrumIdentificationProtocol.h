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

#ifndef _CSPECTRUMIDENTIFICATIONPROTOCOL_H
#define _CSPECTRUMIDENTIFICATIONPROTOCOL_H

#include "CAdditionalSearchParams.h"
#include "CDatabaseFilters.h"
#include "CEnzymes.h"
#include "CFragmentTolerance.h"
#include "CMassTable.h"
#include "CModificationParams.h"
#include "CParentTolerance.h"
#include "CSearchType.h"
#include "CThreshold.h"
#include <string>
#include <vector>

class CSpectrumIdentificationProtocol{
public:
  //Data members
  std::string analysisSoftwareRef;
  std::string id;
  std::string name;
  CSearchType searchType;
  std::vector<CAdditionalSearchParams> additionalSearchParams;
  std::vector<CModificationParams> modificationParams;
  std::vector<CEnzymes> enzymes;
  std::vector<CMassTable> massTable;
  std::vector<CFragmentTolerance> fragmentTolerance;
  std::vector<CParentTolerance> parentTolerance;
  CThreshold threshold;
  std::vector<CDatabaseFilters> databaseFilters;
  //CDatabaseTranslation databaseTranslation;

  //operators
  //CSpectrumIdentificationProtocol& operator=(const CSpectrumIdentificationProtocol& c);
  //bool operator==(const CSpectrumIdentificationProtocol& c);

  //Functions
  void writeOut(FILE* f, int tabs = -1);

private:
};

#endif
