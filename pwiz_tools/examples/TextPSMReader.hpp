//
// $Id$ 
//
// Original author: Nathan Edwards <nje5@georgetown.edu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#ifndef _TEXTPSMREADER_HPP_
#define _TEXTSPMREADER_HPP_

#include <string>
#include <iostream>
#include <map>
#include <list>

#include "pwiz/data/identdata/IdentData.hpp"
#include "pwiz/data/common/CVTranslator.hpp"

using namespace pwiz::data;
using namespace pwiz::identdata;

namespace pwiz {
namespace examples {

class TextPSMMod {
public:
  TextPSMMod(): location(-1), residue('$'), delta(0.0), name("") {}
  std::string instantiate(std::string const & s); 
  int location;
  char residue;
  float delta;
  std::string name;
  void error(std::string const & msg, std::string const & line="") const;
};

class TextPSMScore {
public:
  TextPSMScore(): name(""), value(0.0), units("") {}
  bool instantiate(std::string const & s);
  std::string name;
  double value;
  std::string units;
};

class TextPSMParam {
public:
  TextPSMParam(): name(""), value(""), units("") {}
  bool instantiate(std::string const & s);
  std::string name;
  std::string value;
  std::string units;
};

class TextPSMProtein {
public:
  TextPSMProtein(): pracc(""), laa('$'), raa('$'), start(-1), end(-1), len(-1), prdef("") {}
  bool instantiate(std::string const & s);
  std::string pracc;
  char laa;
  char raa;
  int  start;
  int  end;
  int  len;
  std::string prdef;
};

class TextPSMContainer {
public:
  TextPSMContainer() {}
};

class TextPSMRecord: public TextPSMContainer {
public:
  std::string SpectrumFile;
  std::string SpectrumID;
  std::string Location;
  std::string Scan;
  int    Rank;
  int    ChargeState;
  double ExperimentalMassToCharge;
  std::string Peptide;
  std::list<TextPSMMod> Modifications;
  std::list<TextPSMScore> Scores;
  std::list<TextPSMParam> Params;
  std::list<TextPSMParam> SpecParams;
  std::list<TextPSMProtein> Proteins;
  bool passThreshold;
  std::string peptideID() const;
};

class TextPSMSoftware {
public:
  TextPSMSoftware(): name(""), version("") {}
  bool instantiate(std::string const & s);
  std::string name;
  std::string version;
};

class TextPSMSeqDB: public TextPSMContainer {
public:
  TextPSMSeqDB() {}
  // bool instantiate(std::string const & s);
  std::string name;
  std::string organism;
  std::string release;
  std::string ref;
  std::string uri;
  std::string source;
  std::string location;
  std::string decoyPrefix;
};

class TextPSMMetaData: public TextPSMContainer {
public:
  TextPSMMetaData(): OutputFormat(NULL), threshold(NULL), enzymesemi(false) {};
  std::string SpectrumIDFormat;
  std::string FileFormat;
  std::list<TextPSMSoftware> Software;
  TextPSMSoftware *OutputFormat;
  std::list<TextPSMSeqDB> SequenceDatabases;
  TextPSMParam *threshold;
  std::string AnalysisSoftware;
  std::string enzyme;
  bool enzymesemi;
};

class TextPSMGroupMetaData: public TextPSMContainer {
public:
  TextPSMGroupMetaData(): threshold(NULL) {};
  std::string AnalysisSoftware;
  TextPSMParam *threshold;
  std::list<TextPSMParam> AnalysisParams;
};

class TextPSMProtGroup: public TextPSMContainer {
public:
  TextPSMProtGroup() {};
  std::string name;
  std::list<std::string> praccs;
  std::multimap<std::string,TextPSMScore> Scores;
  std::multimap<std::string,TextPSMParam> Params;  
};

class TextPSMReader {
private:
  std::map<std::string,PeptidePtr> peptides;
  std::map<std::string,DBSequencePtr> proteins;
  std::map<std::string,SearchDatabasePtr> databases;
  std::map<std::string,PeptideEvidencePtr> peptideEvidence;
  std::map<std::string,SearchModificationPtr> modifications;
  std::map<std::string,AnalysisSoftwarePtr> software;

  std::multimap<DBSequencePtr,PeptideEvidencePtr> dbseq2pe;
  std::multimap<PeptideEvidencePtr,SpectrumIdentificationItemPtr> pe2sii;
  CVTranslator translator;
public:
  TextPSMReader() {};
  void initialize(IdentData& mzid) const;
  int getNextRecord(std::istream & is, TextPSMContainer * & c) const;
  bool valid(TextPSMRecord const & r, const std::string & block) const;
  void readTextStream(std::istream & is, IdentData& mzid);
  PeptidePtr getPeptide(TextPSMRecord const & r, IdentData& mzid);
  bool testProtein(const std::string & acc) const;
  DBSequencePtr findProtein(const std::string & acc) const;
  DBSequencePtr getProtein(const TextPSMProtein & p, IdentData& mzid);
  SearchDatabasePtr getSearchDatabase(const std::string & db, IdentData &mzid);
  PeptideEvidencePtr getPeptideEvidence(PeptidePtr const & pep,
					DBSequencePtr const & pr,
					TextPSMProtein const & psmpr,
					IdentData& mzid);
  void addMod(IdentData& mzid, ModificationPtr mod, int peplen);
  CVID getCVID(std::string const & term) const;
  CVID getCVID_UNIMOD(std::string const & term) const;
};

}} // namespace scope

#endif // _TEXTPSMREADER_HPP_
