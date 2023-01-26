/*
Copyright 2017, Michael R. Hoopmann, Institute for Systems Biology
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

#ifndef _CMZIDENTML_H
#define _CMZIDENTML_H

#include "CAnalysisCollection.h"
#include "CAnalysisProtocolCollection.h"
#include "CAnalysisSampleCollection.h"
#include "CAnalysisSoftwareList.h"
#include "CAuditCollection.h"
#include "CBibliographicReference.h"
#include "CCvList.h"
#include "CDataCollection.h"
#include "CProvider.h"
#include "CPSM.h"
#include "CSequenceCollection.h"
#include "expat.h"
#include "mzIMLStructs.h"
#include <ctime>
#include <string>

#define XMLCLASS		
#ifndef XML_STATIC
#define XML_STATIC	// to statically link the expat libraries
#endif

//List of versions. These should not [normally] be changed, only appended to.
#define mzIdentMLv1 "1.1.0"
#define mzIdentMLv1schema "http://psidev.info/psi/pi/mzIdentML/1.1 ../../schema/mzIdentML1.1.0.xsd"
#define mzIdentMLv1xmlns "http://psidev.info/psi/pi/mzIdentML/1.1" 
#define mzIdentMLv2 "1.2.0"
#define mzIdentMLv2schema "http://psidev.info/psi/pi/mzIdentML/1.2 http://www.psidev.info/files/mzIdentML1.2.0.xsd"
#define mzIdentMLv2xmlns "http://psidev.info/psi/pi/mzIdentML/1.2" 

class CMzIdentML {
public:

  //Constructor & Destructor
  CMzIdentML();
  ~CMzIdentML();

  //Data members
  CCvList cvList;
  CAnalysisSoftwareList analysisSoftwareList;
  CSequenceCollection sequenceCollection;
  CAnalysisCollection analysisCollection;
  CAnalysisProtocolCollection analysisProtocolCollection;
  std::vector<CAnalysisSampleCollection> analysisSampleCollection;
  CDataCollection dataCollection;
  CProvider provider;
  std::vector<CAuditCollection> auditCollection;
  std::vector<CBibliographicReference> bibliographicReference;

  sMzIDDateTime creationDate;
  std::string id;
  std::string name;
  std::string versionStr;
  std::string xmlns;
  std::string schema;

  std::string fileBase;
  std::string fileFull;
  std::string filePath;

  //Functions
  std::string addAnalysisSoftware(std::string software, std::string version);
  std::string addDBSequence(std::string acc, std::string sdbRef, std::string desc = "");
  std::string addPeptide(std::string seq, std::vector<CModification>& mods);
  sPeptideEvidenceRef addPeptideEvidence(std::string dbRef, std::string pepRef, int start=0, int end=0, char pre='?', char post='?', bool isDecoy="false");
  CProteinAmbiguityGroup* addProteinAmbiguityGroup();
  CProteinDetection* addProteinDetection(std::vector<std::string>& specIdentListRef, std::string protDetectProtRef, CProteinDetectionList*& pdl);
  CSpectrumIdentification* addSpectrumIdentification(std::string spectraDataRef, std::string searchDatabaseRef, std::string specIdentProtRef, CSpectrumIdentificationList*& sil);
  bool addXLPeptides(std::string seq1, std::vector<CModification>& mods1, std::string& ref1, std::string seq2, std::vector<CModification>& mods2, std::string& ref2, std::string& value);
  //void consolidateSpectrumIdentificationProtocol();
  
  CDBSequence       getDBSequence(std::string& dBSequence_ref);
  CDBSequence       getDBSequenceByAcc(std::string acc);
  void              getDBSequenceByAcc(std::string acc, std::vector<CDBSequence>& v);
  CPeptide          getPeptide(std::string peptide_ref);
  bool              getPeptide(std::string peptide_ref, CPeptide& p);
  bool              getPeptide(std::string peptide_ref, CPeptide*& p);
  CPeptideEvidence  getPeptideEvidence(std::string& peptideEvidence_ref);
  CPSM              getPSM(int index, int rank=1);
  int               getPSMCount();

  CSpectraData                      getSpectraData(std::string& spectraData_ref);
  CSpectrumIdentificationList*      getSpectrumIdentificationList(std::string& spectrumIdentificationList_ref);
  CSpectrumIdentificationProtocol*  getSpectrumIdentificationProtocol(std::string& spectrumIdentificationProtocol_ref);
  CSpectrumIdentificationResult&    getSpectrumIdentificationResult(std::string& spectrumIdentificationResult_ref);
  CSpectrumIdentificationResult*    getSpectrumIdentificationResultBySpectrumID(std::string& spectrumIdentificationList_ref, std::string& spectrumIdentificationResult_spectrumID);

  std::string getMzIMLToolsVersion();
  int getVersion();
  bool readFile(const char* fn);
  void setVersion(int ver);
  bool writeFile(const char* fn);
  
  //Functions for XML Parsing
  void characters(const XML_Char *s, int len);
  void endElement(const XML_Char *el);
  void startElement(const XML_Char *el, const XML_Char **attr);

protected:
  bool                killRead;
  XML_Parser				  parser;
  std::vector<mzidElement> activeEl;
  int version;  //1=1.1.0, 2=1.2.0

  //Functions
  void processCvParam(sCvParam& cv);
  void processUserParam(sUserParam& u);

  //Functions for XML Parsing
  inline const char* getAttrValue(const char* name, const XML_Char **attr) {
    for (int i = 0; attr[i]; i += 2) {
      if (isAttr(name, attr[i])) return attr[i + 1];
    }
    return "";
  }
  inline bool isAttr(const char *n1, const XML_Char *n2) { return (strcmp(n1, n2) == 0); }
  inline bool isElement(const char *n1, const XML_Char *n2)	{ return (strcmp(n1, n2) == 0); }

private:
};

#endif