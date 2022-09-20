
#ifndef _TEXTPSMREADER_HPP_
#define _TEXTSPMREADER_HPP_

#include "pwiz/data/identdata/IdentData.hpp"
#include "pwiz/data/common/CVTranslator.hpp"

using namespace pwiz::data;
using namespace pwiz::identdata;

#include <string>
#include <iostream>
#include <map>
#include <list>

using namespace std;

class TextPSMMod {
public:
  TextPSMMod(): location(-1), residue('$'), delta(0.0), name("") {}
  string instantiate(string const & s); 
  int location;
  char residue;
  float delta;
  string name;
  void error(string const & msg, string const & line="") const;
};

class TextPSMScore {
public:
  TextPSMScore(): name(""), value(0.0), units("") {}
  bool instantiate(string const & s);
  string name;
  double value;
  string units;
};

class TextPSMParam {
public:
  TextPSMParam(): name(""), value(""), units("") {}
  bool instantiate(string const & s);
  string name;
  string value;
  string units;
};

class TextPSMProtein {
public:
  TextPSMProtein(): pracc(""), laa('$'), raa('$'), start(-1), end(-1), len(-1), prdef("") {}
  bool instantiate(string const & s);
  string pracc;
  char laa;
  char raa;
  int  start;
  int  end;
  int  len;
  string prdef;
};

class TextPSMContainer {
public:
  TextPSMContainer() {}
};

class TextPSMRecord: public TextPSMContainer {
public:
  string SpectrumFile;
  string SpectrumID;
  string Location;
  string Scan;
  int    Rank;
  int    ChargeState;
  double ExperimentalMassToCharge;
  string Peptide;
  list<TextPSMMod> Modifications;
  list<TextPSMScore> Scores;
  list<TextPSMParam> Params;
  list<TextPSMParam> SpecParams;
  list<TextPSMProtein> Proteins;
  bool passThreshold;
  string peptideID() const;
};

class TextPSMSoftware {
public:
  TextPSMSoftware(): name(""), version("") {}
  bool instantiate(string const & s);
  string name;
  string version;
};

class TextPSMSeqDB: public TextPSMContainer {
public:
  TextPSMSeqDB() {}
  // bool instantiate(string const & s);
  string name;
  string organism;
  string release;
  string ref;
  string uri;
  string source;
  string location;
  string decoyPrefix;
};

class TextPSMMetaData: public TextPSMContainer {
public:
  TextPSMMetaData(): OutputFormat(NULL), threshold(NULL), enzymesemi(false) {};
  string SpectrumIDFormat;
  string FileFormat;
  list<TextPSMSoftware> Software;
  TextPSMSoftware *OutputFormat;
  list<TextPSMSeqDB> SequenceDatabases;
  TextPSMParam *threshold;
  string AnalysisSoftware;
  string enzyme;
  bool enzymesemi;
};

class TextPSMGroupMetaData: public TextPSMContainer {
public:
  TextPSMGroupMetaData(): threshold(NULL) {};
  string AnalysisSoftware;
  TextPSMParam *threshold;
  list<TextPSMParam> AnalysisParams;
};

class TextPSMProtGroup: public TextPSMContainer {
public:
  TextPSMProtGroup() {};
  string name;
  list<string> praccs;
  multimap<string,TextPSMScore> Scores;
  multimap<string,TextPSMParam> Params;  
};

class TextPSMReader {
private:
  map<string,PeptidePtr> peptides;
  map<string,DBSequencePtr> proteins;
  map<string,SearchDatabasePtr> databases;
  map<string,PeptideEvidencePtr> peptideEvidence;
  map<string,SearchModificationPtr> modifications;
  map<string,AnalysisSoftwarePtr> software;

  multimap<DBSequencePtr,PeptideEvidencePtr> dbseq2pe;
  multimap<PeptideEvidencePtr,SpectrumIdentificationItemPtr> pe2sii;
  CVTranslator translator;
public:
  TextPSMReader() {};
  void initialize(IdentData& mzid) const;
  int getNextRecord(istream & is, TextPSMContainer * & c) const;
  bool valid(TextPSMRecord const & r, const string & block) const;
  void readTextStream(istream & is, IdentData& mzid);
  PeptidePtr getPeptide(TextPSMRecord const & r, IdentData& mzid);
  bool testProtein(const string & acc) const;
  DBSequencePtr findProtein(const string & acc) const;
  DBSequencePtr getProtein(const TextPSMProtein & p, IdentData& mzid);
  SearchDatabasePtr getSearchDatabase(const string & db, IdentData &mzid);
  PeptideEvidencePtr getPeptideEvidence(PeptidePtr const & pep,
					DBSequencePtr const & pr,
					TextPSMProtein const & psmpr,
					IdentData& mzid);
  void addMod(IdentData& mzid, ModificationPtr mod, int peplen);
  CVID getCVID(string const & term) const;
  CVID getCVID_UNIMOD(string const & term) const;
};

#endif // _TEXTPSMREADER_HPP_
