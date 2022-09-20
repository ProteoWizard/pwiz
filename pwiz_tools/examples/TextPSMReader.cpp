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

#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/data/identdata/References.hpp"
#include "pwiz/data/identdata/IdentData.hpp"
#include "pwiz/data/common/CVTranslator.hpp"
#include "pwiz/utility/chemistry/Ion.hpp"
#include "pwiz/data/proteome/Digestion.hpp"
#include "pwiz/data/proteome/AminoAcid.hpp"
#include "pwiz/data/common/Unimod.hpp"
#include <boost/assign.hpp>

#include "TextPSMReader.hpp"

using namespace pwiz::examples;

// Convenience utility functions - no visibility outside of this
// file. There are probably better boost equivalents, but I don't have
// time to learn a new API.

// STL FTW!

// Remove leading and trailing whitespace, replace one or more
// whitespace characters with a single space.
std::string clean(std::string const & s) {
  istringstream iss(s);
  std::string clean;
  std::string tok;
  bool first = true;
  while (iss) {
    tok = "";
    iss >> tok;
    if (tok == "") {
      break;
    }
    if (!first) {
      clean += " ";
    }
    clean += tok;
    first = false;
  }
  return clean;
}

// Split on whitespace, resulting in up to (max_split+1) pieces. 
// Remainder of string is in the last position of the vector.
// The number of tokens appended to the vector v is returned.
int split(std::string const & s, std::vector<std::string> & v, int max_split=0) {
  // cerr << "Split: " << s << endl;
  std::istringstream iss(s);
  int cnt = 0;
  std::string tok;
  if (max_split <= 0) {
    // Overestimate...
    max_split = s.length()+1;
  }
  bool append=false;
  for (int i=0;cnt<max_split;i++) {
    tok = "";
    iss >> tok;
    if (tok == "") {
      break;
    }
    int tlen = tok.length();
    bool hasbs = false;
    if (tok[tlen-1] == '\\') {
      hasbs = true;
      tok = tok.substr(0,tlen-1);
      --cnt;
    }
    if (append) {
      v[v.size()-1] += " ";
      v[v.size()-1] += tok;
    } else {
      v.push_back(tok);
    }
    ++cnt;
    append = hasbs;
  }
  tok = "";
  getline(iss,tok,'\0');
  if (tok != "") {
    v.push_back(tok.substr(1));
    ++cnt;
  }
  // cerr << cnt;
  // for (int i=0;i<cnt;i++) {
  //   cerr << " \"" << v[i] << "\"";
  // }
  // cerr << endl;
  return cnt;
}

double toReal(std::string s) {
  std::istringstream iss(s);
  double value;
  iss >> value;
  return value;
}

long int toInteger(std::string s) {
  std::istringstream iss(s);
  long int value;
  iss >> value;
  return value;
}

bool toBoolean(std::string s) {
  return (toInteger(s) == 0);
}

std::string TextPSMMod::instantiate(std::string const & s) {
  int ntok;
  std::vector<std::string> tok;
  ntok = split(s, tok, 3);
  if (ntok < 3) {
    return "too few values";
  }
  location = toInteger(tok[0]);
  residue = tok[1][0];
  delta = toReal(tok[2]);
  if (ntok > 3) {
    name = tok[3];
  }
  if (name == "" && delta == 0.0) {
    return "delta must be non-zero if name is missing";
  }
  return "";
}

void TextPSMMod::error(std::string const & msg, std::string const & line) const {
  if (msg != "") {
    cerr << "Bad Modification: " << msg << " - aborting!!!!" << endl;
  } 
  else {
    cerr << "Bad Modification - aborting!!!!" << endl;
  }
  if (line != "") {
    cerr << line << endl;
  } else {
    cerr << "Modification " << location << " " << residue << " " << delta;
    if (name != "") {
      cerr << " " << name;
    }
    cerr << endl;
  }
  exit(1);
}

bool TextPSMScore::instantiate(std::string const & s) {
  int ntok;
  std::vector<std::string> tok;
  ntok = split(s, tok, 3);
  if (ntok < 2) {
    return false;
  }
  name = tok[0];
  value = toReal(tok[1]);
  if (ntok >= 3) {
    units = tok[2];
  }
  return true;
}

bool TextPSMParam::instantiate(std::string const & s) {
  int ntok;
  std::vector<std::string> tok;
  ntok = split(s, tok, 3);
  if (ntok < 1) {
    return false;
  }
  name = tok[0];
  if (ntok >= 2) {
    value = tok[1];
  }
  if (ntok >= 3) {
    units = tok[2];
  }
  return true;
}

bool TextPSMProtein::instantiate(std::string const & s) {
  int ntok;
  std::vector<std::string> tok;
  size_t i;
  i = s.find(">");
  if (i != std::string::npos) {
    prdef = clean(s.substr(i+1));
    ntok = split(clean(s.substr(0,i)),tok,6);
  } else {
    ntok = split(s, tok, 6);
  }
  if (ntok < 1 || ntok == 2 || ntok == 4) {
    return false;
  }
  pracc = tok[0];
  if (ntok >= 3) {
    laa = tok[1][0];
    raa = tok[2][0];
  }
  if (ntok >= 5) {
    start = toInteger(tok[3]);
    end = toInteger(tok[4]);
  }
  if (ntok >= 6) {
    len = toInteger(tok[5]);
  }
  return true;
}

// bool TextPSMSeqDB::instantiate(std::string const & s) {
//   int ntok;
//   std::vector<std::string> tok;
//   ntok = split(s, tok, 1);
//   if (ntok < 2) {
//     return false;
//   }
//   ref = tok[0];
//   description = tok[1];
//   return true;
// }

bool TextPSMSoftware::instantiate(std::string const & s) {
  int ntok;
  std::vector<std::string> tok;
  ntok = split(s, tok, 1);
  if (ntok < 1) {
    return false;
  }
  name = tok[0];
  if (ntok >= 2) {
    version = tok[1];
  }
  return true;
}

std::string TextPSMRecord::peptideID() const {
  std::ostringstream oss;
  oss << Peptide;
  std::list<TextPSMMod>::const_iterator mit=Modifications.begin();
  while (mit != Modifications.end()) {
    std::ostringstream deltass;
    deltass << fixed << showpos << setprecision(2) << mit->delta;
    if (mit->residue == '-') {
      oss << "|" << mit->location << ":" << deltass.str();
    } else {
      oss << "|" << mit->residue << mit->location << ":" << deltass.str();
    }
    ++mit;
  }
  return oss.str();
}

bool TextPSMReader::valid(const TextPSMRecord & r, const std::string & block) const {

  if (r.SpectrumFile == "") {
    cerr << "Bad PSM: SpectrumFile is required" << endl;
    cerr << block;
    exit(1);
  }

  if (r.SpectrumID == "") {
    cerr << "Bad PSM: SpectrumID is required" << endl;
    cerr << block;
    exit(1);
  }

  if (r.Rank <= -1) {
    cerr << "Bad PSM: Rank is required" << endl;
    cerr << block;
    exit(1);
  }
  if (r.ChargeState <= -1) {
    cerr << "Bad PSM: ChargeState is required" << endl;
    cerr << block;
    exit(1);
  }

  // Check the modification locations and residues...
  int peplen = r.Peptide.length();
  std::list<TextPSMMod>::const_iterator mit=r.Modifications.begin();
  while (mit != r.Modifications.end()) {
    if (mit->location < 0 || mit->location > (peplen+1)) {
      mit->error("Bad location");
    }
    if (mit->location == 0 && mit->residue != '-') {
      mit->error("Bad residue - N-term");
    }
    if (mit->location == (peplen+1) && mit->residue != '-') {
      mit->error("Bad residue - C-term");
    }
    if (mit->location >= 1 && mit->location <= peplen && 
	mit->residue != r.Peptide[mit->location-1]) {
      mit->error("Bad residue");
    }
    if (mit->name != "") {
	CVID id = getCVID_UNIMOD(mit->name);
	if (id == CVID_Unknown) {
          mit->error("Bad name " + mit->name + " (UniMod)");
        }
    }
    ++mit;
  }  
  return true;
}

// Setup any constant elements in the IdentData data-structure.
void TextPSMReader::initialize(IdentData& mzid) const {
    mzid.cvs = defaultCVList();
}

void TextPSMReader::addMod(IdentData& mzid, ModificationPtr mod, int peplen) {
  int termind = 0;
  char residue = '.';
  if (mod->location == 0) {
    termind = -1;
  } else if (mod->location > peplen) {
    termind = 1;
  } else {
    residue = mod->residues[0];
  }
  std::ostringstream oss;
  oss << residue << ":" << mod->monoisotopicMassDelta << ":" << termind << endl;
  std::string key = oss.str();
  map<std::string,SearchModificationPtr>::iterator mit;
  mit = modifications.find(key);
  if (mit == modifications.end()) {
    SearchModificationPtr sm = SearchModificationPtr(new SearchModification);
    sm->fixedMod = false; // We don't know, one way or the other...
    sm->massDelta = mod->monoisotopicMassDelta;
    sm->residues.push_back(residue);
    if (termind == -1) {
      sm->specificityRules = CVParam(pwiz::cv::MS_modification_specificity_peptide_N_term);
    } else if (termind == 1) {
      sm->specificityRules = CVParam(pwiz::cv::MS_modification_specificity_peptide_C_term);
    }
    CVID mid = mod->cvParamChild(UNIMOD_unimod_root_node).cvid;
    if (mid != CVID_Unknown) {
      sm->set(mid);
    }
    modifications.insert(make_pair(key,sm));
    mzid.analysisProtocolCollection.spectrumIdentificationProtocol[0]->modificationParams.push_back(sm);
  }
}

PeptidePtr TextPSMReader::getPeptide(TextPSMRecord const & r, IdentData& mzid) {
  std::string pid = r.peptideID();
  map<std::string,PeptidePtr>::const_iterator pit;
  pit = peptides.find(pid);
  if (pit != peptides.end()) {
    return pit->second;
  }
  PeptidePtr p = PeptidePtr(new pwiz::identdata::Peptide);
  peptides.insert(make_pair(pid,p));
  mzid.sequenceCollection.peptides.push_back(p);
  p->id = pid;
  p->peptideSequence = r.Peptide;
  std::list<TextPSMMod>::const_iterator mit=r.Modifications.begin();
  while (mit != r.Modifications.end()) {
    ModificationPtr mod = ModificationPtr(new pwiz::identdata::Modification);
    mod->location = mit->location;
    if (mit->residue != '-') {
      mod->residues.push_back(mit->residue);
    }
    CVID id = getCVID_UNIMOD(mit->name);
    if (id != CVID_Unknown) {
      mod->set(id);
      try {
        mod->monoisotopicMassDelta = pwiz::data::unimod::modification(id).deltaMonoisotopicMass();
      }
      catch (runtime_error &) {
	// perhaps we should do average too, but why?
        mod->monoisotopicMassDelta = mit->delta;
      }
    } else {
      mod->monoisotopicMassDelta = mit->delta;
    }
    p->modification.push_back(mod);
    addMod(mzid,mod,r.Peptide.length());
    ++mit;
  }
  return p;
}

SearchDatabasePtr TextPSMReader::getSearchDatabase(std::string const & db, IdentData &mzid) {
  map<std::string,SearchDatabasePtr>::const_iterator sdit;
  sdit = databases.find(db);
  assert (sdit != databases.end());
  return sdit->second;
}

bool TextPSMReader::testProtein(const std::string & accin) const {
  std::string acc = accin;
  std::string db = "";
  size_t i = acc.rfind(':');
  if (i != std::string::npos) {
    db = acc.substr(0,i);
    acc = acc.substr(i+1);
  }
  map<std::string,DBSequencePtr>::const_iterator pit;
  pit = proteins.find(acc);
  if (pit != proteins.end()) {
    return true;
  }
  return false;
}

DBSequencePtr TextPSMReader::findProtein(const std::string & accin) const {
  std::string acc = accin;
  std::string db = "";
  size_t i = acc.rfind(':');
  if (i != std::string::npos) {
    db = acc.substr(0,i);
    acc = acc.substr(i+1);
  }
  map<std::string,DBSequencePtr>::const_iterator pit;
  pit = proteins.find(acc);
  return pit->second;
}

DBSequencePtr TextPSMReader::getProtein(TextPSMProtein const & pr, IdentData &mzid) {
  std::string acc = pr.pracc;
  std::string db = "";
  size_t i = acc.rfind(':');
  if (i != std::string::npos) {
    db = acc.substr(0,i);
    acc = acc.substr(i+1);
  }
  map<std::string,DBSequencePtr>::const_iterator pit;
  pit = proteins.find(acc);
  if (pit != proteins.end()) {
    return pit->second;
  }
  DBSequencePtr p = DBSequencePtr(new DBSequence);
  p->id = acc;
  p->accession = acc;
  if (pr.prdef != "") {
    p->set(pwiz::cv::MS_protein_description,pr.prdef);
  }
  if (pr.len >= 0) {
    p->length = pr.len;
  }
  p->searchDatabasePtr = getSearchDatabase(db,mzid);
  proteins.insert(make_pair(acc,p));
  mzid.sequenceCollection.dbSequences.push_back(p);
  return p;
}

PeptideEvidencePtr TextPSMReader::getPeptideEvidence(PeptidePtr const & pep,
						     DBSequencePtr const & pr,
						     TextPSMProtein const & psmpr,
						     IdentData &mzid) {
  std::ostringstream oss; 
  size_t p=pep->id.find('|');
  std::string mods = "";
  std::string pepseq = pep->id;
  if (p != std::string::npos) {
    mods = pep->id.substr(p+1);
    pepseq = pep->id.substr(0,p);
  }
  if (psmpr.start != -1 && psmpr.end != -1 && psmpr.laa != '$' && psmpr.raa != '$') {
    // Guard against the same peptide on a protein twice...
    oss << pr->id << "|" << psmpr.laa << "|" << psmpr.start << "|" << pepseq << "|" << psmpr.end << "|" << psmpr.raa << "|" << mods;
  } else if (psmpr.laa != '$' && psmpr.raa != '$') {
    oss << pr->id << "|" << psmpr.laa << "||" << pepseq << "||" << psmpr.raa << "|" << mods;
  } else {
    // Punt, we can't tell if this has occured anyway...
    oss << pr->id << "|||" << pepseq << "|||" << mods;
  }
  std::string id = oss.str();

  map<std::string,PeptideEvidencePtr>::const_iterator peit;
  peit = peptideEvidence.find(id);
  if (peit != peptideEvidence.end()) {
    return peit->second;
  }
  PeptideEvidencePtr pe = PeptideEvidencePtr(new PeptideEvidence);
  pe->id = id;
  pe->peptidePtr = pep;
  pe->dbSequencePtr = pr;
  if (psmpr.start != -1) {
    pe->start = psmpr.start;
  }
  if (psmpr.end != -1) {
    pe->end = psmpr.end;
  }
  if (psmpr.laa != '$') {
    pe->pre = psmpr.laa;
  }
  if (psmpr.raa != '$') {
    pe->post = psmpr.raa;
  }
  CVID cvid = pwiz::cv::MS_decoy_DB_accession_regexp;
  if (pr->searchDatabasePtr->hasCVParam(cvid)) {
    std::string decoy_prefix = pr->searchDatabasePtr->cvParam(cvid).value.substr(1);
    if (pr->id.substr(0,decoy_prefix.length()) == decoy_prefix) {
      pe->isDecoy = true;
    } else {
      pe->isDecoy = false;
    }
  }
  peptideEvidence.insert(make_pair(id,pe));
  dbseq2pe.insert(make_pair(pr,pe));
  mzid.sequenceCollection.peptideEvidence.push_back(pe);
  return pe;
}

int TextPSMReader::getNextRecord(std::istream & is, TextPSMContainer* & c) const {
  std::string line;
  std::string block;
  TextPSMRecord *r=NULL;
  TextPSMMetaData *md=NULL;
  TextPSMGroupMetaData *gmd=NULL;
  TextPSMSeqDB *sdb=NULL;
  TextPSMProtGroup *prg=NULL;
  bool seen_begin=false;
  bool seen_end=false;
  int lineno = 0;
  while (is.good()) {
    getline(is,line);
    line = clean(line);
    lineno += 1;
    // cerr << lineno << ": " << line << endl;

    if (line.length() == 0 || line[0] == '#') {
      continue;
    }
    if (line == "PSMBEGIN") {
      assert(!r);
      r = new TextPSMRecord();
      // cerr << "HERE " << r << endl;
      r->passThreshold = true;
      seen_begin = true;
      block = "";
      continue;
    }
    if (line == "PSMEND") {
      assert(r);
      seen_end = true;
      break;
    }
    if (line == "MDBEGIN") {
      assert(!md);
      md = new TextPSMMetaData();
      seen_begin = true;
      block = "";
      continue;
    }
    if (line == "MDEND") {
      assert(md);
      seen_end = true;
      break;
    }
    if (line == "GRPMDBEGIN") {
      assert(!gmd);
      gmd = new TextPSMGroupMetaData();
      seen_begin = true;
      block = "";
      continue;
    }
    if (line == "GRPMDEND") {
      assert(gmd);
      seen_end = true;
      break;
    }
    if (line == "SEQDBBEGIN") {
      assert(!md);
      sdb = new TextPSMSeqDB();
      seen_begin = true;
      block = "";
      continue;
    }
    if (line == "SEQDBEND") {
      assert(sdb);
      seen_end = true;
      break;
    }
    if (line == "PRGRPBEGIN") {
      assert(!prg);
      prg = new TextPSMProtGroup();
      seen_begin = true;
      block = "";
      continue;
    }
    if (line == "PRGRPEND") {
      assert(prg);
      seen_end = true;
      break;
    }
    assert(r || md || gmd || sdb || prg);
    if (block != "") {
      block += '\n';
    }
    block  += line;
    std::vector<std::string> sline;
    int ntok;
    ntok = split(line, sline, 1);
    if (ntok != 2) {
      continue;
    }
    std::string key = sline[0];
    std::string value = sline[1];
    if (md && key == "SpectrumIDFormat") {
      md->SpectrumIDFormat = value;
      continue;
    }
    if (md && key == "FileFormat") {
      md->FileFormat = value;
      continue;
    }
    if (md && key == "Enzyme") {
      md->enzyme = value;
      continue;
    }
    if (md && key == "EnzymeSemi") {
      md->enzymesemi = toBoolean(value);
      continue;
    }
    if (md && key == "AnalysisSoftware") {
      md->AnalysisSoftware = value;
      continue;
    }
    if (gmd && key == "AnalysisSoftware") {
      gmd->AnalysisSoftware = value;
      continue;
    }
    if (md && key == "Threshold") {
      TextPSMParam *p = new TextPSMParam();
      if (!p->instantiate(value)) {
	cerr << "Bad Param line " << lineno << " - aborting!!!!" << endl;
	cerr << line << endl;
	exit(1);
      }
      md->threshold = p;
      continue;
    }
    if (gmd && key == "Threshold") {
      TextPSMParam *p = new TextPSMParam();
      if (!p->instantiate(value)) {
	cerr << "Bad Param line " << lineno << " - aborting!!!!" << endl;
	cerr << line << endl;
	exit(1);
      }
      gmd->threshold = p;
      continue;
    }
    if (md && key == "Software") {
      TextPSMSoftware so;
      if (!so.instantiate(value)) {
	cerr << "Bad Software line " << lineno << " - aborting!!!!" << endl;
	cerr << line << endl;
	exit(1);
      }
      md->Software.push_back(so);
      continue;
    }
    if (gmd && key == "AnalysisParams") {
      TextPSMParam p;
      if (!p.instantiate(value)) {
	cerr << "Bad GroupAnalysisParam line " << lineno << " - aborting!!!!" << endl;
	cerr << line << endl;
	exit(1);
      }
      gmd->AnalysisParams.push_back(p);
      continue;
    }
    if (md && key == "OutputFormat") {
      TextPSMSoftware *so = new TextPSMSoftware();
      if (!so->instantiate(value)) {
	cerr << "Bad OutputFormat line " << lineno << " - aborting!!!!" << endl;
	cerr << line << endl;
	exit(1);
      }
      md->OutputFormat = so;
      continue;
    }
    if (sdb && key == "ID") {
      sdb->ref = value;
      continue;
    }
    if (sdb && key == "Name") {
      sdb->name = value;
      continue;
    }
    if (sdb && key == "Organism") {
      sdb->organism = value;
      continue;
    }
    if (sdb && key == "Release") {
      sdb->release = value;
      continue;
    }
    if (sdb && key == "DBSource") {
      sdb->source = value;
      continue;
    }
    if (sdb && key == "URI") {
      sdb->uri = value;
      continue;
    }
    if (sdb && key == "Location") {
      sdb->location = value;
      continue;
    }
    if (sdb && key == "DecoyPrefix") {
      sdb->decoyPrefix = value;
      continue;
    }
    if (prg && key == "Name") {
      prg->name = value;
    }
    if (prg && key == "Protein") {
      if (!testProtein(value)) {
	cerr << "Bad Protein Group Protein " << lineno << " - aborting!!!!" << endl;
	cerr << line << endl;
	exit(1);
      }
      prg->praccs.push_back(value);
    }
    if (prg && key == "Score") {
      std::vector<std::string> svalue;
      int ntok = split(value,svalue,1);
      if (ntok != 2) {
	cerr << "Bad Protein Group Score line " << lineno << " - aborting!!!!" << endl;
	cerr << line << endl;
	exit(1);
      }
      std::string pracc = svalue[0];
      if (!testProtein(pracc)) {
	cerr << "Bad Protein Group Protein " << lineno << " - aborting!!!!" << endl;
	cerr << line << endl;
	exit(1);
      }      
      TextPSMScore s;
      if (!s.instantiate(svalue[1])) {
	cerr << "Bad Protein Group Score line " << lineno << " - aborting!!!!" << endl;
	cerr << line << endl;
	exit(1);
      }
      prg->Scores.insert(make_pair(pracc,s));
      continue;
    }
    if (prg && key == "Param") {
      std::vector<std::string> svalue;
      int ntok = split(value,svalue,1);
      if (ntok != 2) {
	cerr << "Bad Protein Group Param line " << lineno << " - aborting!!!!" << endl;
	cerr << line << endl;
	exit(1);
      }
      std::string pracc = svalue[0];
      if (pracc != "-" && !testProtein(pracc)) {
	cerr << "Bad Protein Group Protein " << lineno << " - aborting!!!!" << endl;
	cerr << line << endl;
	exit(1);
      }      
      TextPSMParam s;
      if (!s.instantiate(svalue[1])) {
	cerr << "Bad Protein Group Param line " << lineno << " - aborting!!!!" << endl;
	cerr << line << endl;
	exit(1);
      }
      prg->Params.insert(make_pair(pracc,s));
      continue;
    }
    if (r && key == "SpectrumFile") {
      r->SpectrumFile = value;
      continue;
    }
    if (r && key == "SpectrumID") {
      r->SpectrumID = value;
      continue;
    }
    if (r && key == "Location") {
      r->Location = value;
      continue;
    }
    if (r && key == "Scan") {
      r->Scan = value;
      continue;
    }
    if (r && key == "Rank") {
      r->Rank = toInteger(value);
      continue;
    }
    if (r && key == "passThreshold") {
      r->passThreshold = (value=="true");
      continue;
    }
    if (r && key == "ChargeState") {
      r->ChargeState = toInteger(value);
      continue;
    }
    if (r && key == "ExperimentalMassToCharge") {
      r->ExperimentalMassToCharge = toReal(value);
      continue;
    }
    if (r && key == "Peptide") {
      r->Peptide = value;
      continue;
    }
    if (r && key == "Modification") {
      TextPSMMod m;
      std::string msg;
      if ((msg=m.instantiate(value)) != "") {
	m.error(msg,line);
      }
      if (m.name != "") {
	CVID id = getCVID_UNIMOD(m.name);
	if (id == CVID_Unknown) {
	  m.error("Bad name " + m.name + " (UniMod)",line);
	} else {
          try {
	    m.delta = pwiz::data::unimod::modification(id).deltaMonoisotopicMass();
	  }
	  catch (runtime_error &) {
	    // don't override delta, don't have unimod CV term in proteowizard
          }
        }
      }
      r->Modifications.push_back(m);
      continue;
    }
    if (r && key == "Score") {
      TextPSMScore s;
      if (!s.instantiate(value)) {
	cerr << "Bad Score line " << lineno << " - aborting!!!!" << endl;
	cerr << line << endl;
	exit(1);
      }
      r->Scores.push_back(s);
      continue;
    }
    if (r && key == "Param") {
      TextPSMParam p;
      if (!p.instantiate(value)) {
	cerr << "Bad Param line " << lineno << " - aborting!!!!" << endl;
	cerr << line << endl;
	exit(1);
      }
      r->Params.push_back(p);
      continue;
    }
    if (r && key == "SpecParam") {
      TextPSMParam p;
      if (!p.instantiate(value)) {
	cerr << "Bad SpecParam line " << lineno << " - aborting!!!!" << endl;
	cerr << line << endl;
	exit(1);
      }
      r->SpecParams.push_back(p);
      continue;
    }
    if (r && key == "Protein") {
      TextPSMProtein pr;
      if (!pr.instantiate(value)) {
	cerr << "Bad Protein line " << lineno << " - aborting!!!!" << endl;
	cerr << line << endl;
	exit(1);
      }
      r->Proteins.push_back(pr);
      continue;
    }
    if (r) {
      cerr << "Bad PSM line " << lineno << ": " << line << " - aborting!!!!" << endl;
      exit(1);
    }
    if (md) {
      cerr << "Bad MetaData line " << lineno << ": " << line << " - aborting!!!!" << endl;
      exit(1);
    }
    if (gmd) {
      cerr << "Bad GroupMetaData line " << lineno << ": " << line << " - aborting!!!!" << endl;
      exit(1);
    }
    if (sdb) {
      cerr << "Bad Sequence Database line " << lineno << ": " << line << " - aborting!!!!" << endl;
      exit(1);
    }
  }
  
  if (seen_end && r) {
    if (!valid(*r,block)) {
      exit(1);
    }
    c = r;
    return 1;
  }
  if (seen_end && md) {
    c = md;
    return 2;
  }
  if (seen_end && sdb) {
    c = sdb;
    return 3;
  }
  if (seen_end && prg) {
    c = prg;
    return 4;
  }
  if (seen_end && gmd) {
    c = gmd;
    return 5;
  }
  if (seen_begin && r) {
    cerr << "Bad PSM - incomplete block" << endl;
    cerr << block << endl;
    exit(1);
  }
  if (seen_begin && md) {
    cerr << "Bad MetaData - incomplete block" << endl;
    cerr << block << endl;
    exit(1);
  }
  if (seen_begin && gmd) {
    cerr << "Bad GroupMetaData - incomplete block" << endl;
    cerr << block << endl;
    exit(1);
  }
  if (seen_begin && sdb) {
    cerr << "Bad SequenceDatabase - incomplete block" << endl;
    cerr << block << endl;
    exit(1);
  }
  if (seen_begin && prg) {
    cerr << "Bad ProteinGroup - incomplete block" << endl;
    cerr << block << endl;
    exit(1);
  }
  return 0;
}

CVID TextPSMReader::getCVID(std::string const & term) const {
  if (term == "") {
    return CVID_Unknown;
  }
  size_t i;
  i = term.find(":");
  if (i != std::string::npos && term.substr(0,i) == "MS") {
    return (CVID)toInteger(term.substr(i+1));
  }
  return translator.translate(term);
}

CVID TextPSMReader::getCVID_UNIMOD(std::string const & term) const {
  if (term == "") {
    return CVID_Unknown;
  }
  size_t i;
  i = term.find(":");
  if (i != std::string::npos && term.substr(0,i) == "UNIMOD") {
    return (CVID)(UNIMOD_unimod_root_node + toInteger(term.substr(i+1)));
  }
  try {
    return pwiz::data::unimod::modification(term).cvid;
  } 
  catch (exception& e) {
    return CVID_Unknown;
  }
}

void TextPSMReader::readTextStream(std::istream & is, IdentData& mzid) {
 
  initialize(mzid);

  SpectrumIdentificationListPtr sil(new SpectrumIdentificationList("SIL"));
  mzid.dataCollection.analysisData.spectrumIdentificationList.push_back(sil);

  PeptidePtr peptide;
  PeptideEvidencePtr pe;
  SpectrumIdentificationResultPtr sir;
  SpectrumIdentificationItemPtr sii;
  SpectraDataPtr sd;
  SpectrumIdentificationProtocolPtr sip;
  SpectrumIdentificationPtr si; 
  ProteinDetectionListPtr pdl;
  ProteinDetectionProtocolPtr pdp;

  bool prgrp = false;

  sip = SpectrumIdentificationProtocolPtr(new SpectrumIdentificationProtocol("SIP"));
  mzid.analysisProtocolCollection.spectrumIdentificationProtocol.push_back(sip);
  sip->searchType = pwiz::cv::MS_ms_ms_search;
  si = SpectrumIdentificationPtr(new SpectrumIdentification("SI"));
  mzid.analysisCollection.spectrumIdentification.push_back(si);
  si->spectrumIdentificationProtocolPtr = sip;
  si->spectrumIdentificationListPtr = sil;
  si->activityDate = mzid.creationDate;

  std::string currentSpectrumFile = "";
  std::string currentSpectrumID = "";
  int spectrumCounter = 1;
  int psmCounter = -1;

  CVID spectrumIDFormat = pwiz::cv::CVID_Unknown;
  CVID fileFormat = pwiz::cv::CVID_Unknown;
 
  int ctype = 0;
  TextPSMContainer *c;
  TextPSMRecord *r;
  TextPSMMetaData *md;
  TextPSMGroupMetaData *gmd;
  TextPSMSeqDB *sdb;
  TextPSMProtGroup *prg;
  while (true) {
    ctype = getNextRecord(is,c);
    r = NULL;
    md = NULL;
    gmd = NULL;
    sdb = NULL;
    prg = NULL;
    switch (ctype) {
    case 1:
      r = ((TextPSMRecord*)c);
      break;
    case 2:
      md = ((TextPSMMetaData*)c);
      break;
    case 3:
      sdb = ((TextPSMSeqDB*)c);
      break;
    case 4:
      prg = ((TextPSMProtGroup*)c);
      break;
    case 5:
      gmd = ((TextPSMGroupMetaData*)c);
      break;
    }
    if (!r && !md && !gmd && !sdb && !prg) {
      break;
    }
    if (md) {
      spectrumIDFormat = getCVID(md->SpectrumIDFormat);
      if (spectrumIDFormat == CVID_Unknown) {
        // Use scan number native ID format?
        spectrumIDFormat = pwiz::cv::MS_scan_number_only_nativeID_format;
      }

      fileFormat = getCVID(md->FileFormat);
      if (fileFormat == CVID_Unknown) {
	// Use mzML file format?
	fileFormat = pwiz::cv::MS_mzML_format; // NOTE: Change from mzXML, despite comment
      }

      TextPSMParam* thr = md->threshold;
      if (thr) {
	CVID id = getCVID(thr->name);
	if (id != CVID_Unknown) {
	  sip->threshold.set(id,thr->value);
	} else {
	  sip->threshold.userParams.push_back(UserParam(thr->name,thr->value));
        }
      }

      // Enzyme is problematic, and is only specified as a *should* in
      // the spec.
      EnzymePtr enz = EnzymePtr(new Enzyme);
      if (md->enzyme != "") {
	  enz->name = md->enzyme;	
      } else {
      	  enz->name = "unspecific cleavage";
      }
      CVID id = getCVID(md->enzyme);
      if (md->enzyme == "" || id == CVID_Unknown) {
	id = pwiz::cv::MS_unspecific_cleavage;
      } else {
	if (md->enzymesemi) {
	  enz->terminalSpecificity = pwiz::proteome::Digestion::SemiSpecific;
	} 
      }
      enz->enzymeName.set(id);
      sip->enzymes.enzymes.push_back(enz);

      std::string sipasstr = md->AnalysisSoftware;

      std::list<TextPSMSoftware>::iterator sit = md->Software.begin();
      while (sit != md->Software.end()) {
	AnalysisSoftwarePtr as = AnalysisSoftwarePtr(new AnalysisSoftware);
	as->id = sit->name;
	as->version = sit->version;
	CVID sid = getCVID(sit->name);
	if (sid != CVID_Unknown) {
	  as->softwareName.set(sid);
	} else {
	  as->softwareName.userParams.push_back(UserParam("analysis software",sit->name));
	}
	mzid.analysisSoftwareList.push_back(as);
	software.insert(make_pair(as->id,as));
	if (as->id == sipasstr) {
	  sip->analysisSoftwarePtr = as;
        }
	++sit;
      }

      if (md->OutputFormat) {
        AnalysisSoftwarePtr as = AnalysisSoftwarePtr(new AnalysisSoftware);
        as->id = md->OutputFormat->name;
	as->version = md->OutputFormat->version;
	CVID sid = getCVID(as->id);
	if (sid != CVID_Unknown) {
	  as->softwareName.set(sid);
	} else {
	  as->softwareName.userParams.push_back(UserParam("file format",as->id));
	}
	mzid.analysisSoftwareList.push_back(as);
      }	
      delete md;
      continue;
    }

    if (gmd) {

      prgrp = true;

      pdp = ProteinDetectionProtocolPtr(new ProteinDetectionProtocol("PDP"));
      mzid.analysisProtocolCollection.proteinDetectionProtocol.push_back(pdp);
      pdl = ProteinDetectionListPtr(new ProteinDetectionList("PDL"));
      mzid.analysisCollection.proteinDetection.id = "PD";
      mzid.analysisCollection.proteinDetection.proteinDetectionListPtr = pdl;
      mzid.analysisCollection.proteinDetection.inputSpectrumIdentifications.push_back(sil);
      mzid.analysisCollection.proteinDetection.activityDate = mzid.creationDate;
      mzid.analysisCollection.proteinDetection.proteinDetectionProtocolPtr = pdp;
      mzid.dataCollection.analysisData.proteinDetectionListPtr = pdl;

      map<std::string,AnalysisSoftwarePtr>::const_iterator it = software.find(gmd->AnalysisSoftware);
      if (it != software.end()) {
	pdp->analysisSoftwarePtr = it->second;
      }

      TextPSMParam* thr = gmd->threshold;
      if (thr) {
	CVID id = getCVID(thr->name);
	if (id != CVID_Unknown) {
	  pdp->threshold.set(id,thr->value);
	} else {
	  pdp->threshold.userParams.push_back(UserParam(thr->name,thr->value));
        }
      }

      std::list<TextPSMParam>::iterator pit = gmd->AnalysisParams.begin();
      while (pit != gmd->AnalysisParams.end()) {
	CVID pid = getCVID(pit->name);
        if (pid != CVID_Unknown) {
          pdp->analysisParams.set(pid,pit->value);
	} else {
          pdp->analysisParams.userParams.push_back(UserParam(pit->name, pit->value));
        }
	++pit;
      }

      delete gmd;
      continue;
    }

    if (sdb) {
      SearchDatabasePtr sdb1(new SearchDatabase);
      sdb1->id = sdb->ref;
      if (sdb->release != "") {
	sdb1->version = sdb->release;
      }
      if (sdb->location != "") {
        sdb1->location = sdb->location;
      } else {
        sdb1->location = ".";
      }
      if (sdb->name != "") {
	CVID cvid = getCVID(sdb->name);
	if (cvid != CVID_Unknown) {
	  sdb1->databaseName.set(cvid);
	} else {
	  cvid = pwiz::cv::MS_database_name;
	  sdb1->databaseName.set(cvid,sdb->name);
	}
      }
      if (sdb->organism != "") {
	CVID cvid = pwiz::cv::MS_taxonomy__common_name;
	sdb1->set(cvid,sdb->organism);
      }
      if (sdb->source != "") {
	CVID cvid = getCVID(sdb->source);
	if (cvid != CVID_Unknown) {
	  sdb1->set(cvid);
	} else {
	  CVID cvid = pwiz::cv::MS_database_source; 
	  sdb1->set(cvid,sdb->source);
	}
      }
      if (sdb->uri != "") {
	CVID cvid = pwiz::cv::MS_database_original_uri; 
	sdb1->set(cvid,sdb->uri);
      }
      if (sdb->decoyPrefix != "") {
	CVID cvid = pwiz::cv::MS_decoy_DB_accession_regexp;
	sdb1->set(cvid,std::string("^")+sdb->decoyPrefix);
      }
      mzid.dataCollection.inputs.searchDatabase.push_back(sdb1);
      si->searchDatabase.push_back(sdb1);
      databases.insert(make_pair(sdb->ref,sdb1));
      
      delete sdb;
      continue;
    }

    if (prg) {

      assert(prgrp);

      typedef multimap<DBSequencePtr,PeptideEvidencePtr>::const_iterator mmcit1;
      typedef multimap<PeptideEvidencePtr,SpectrumIdentificationItemPtr>::const_iterator mmcit2;
      typedef multimap<std::string,TextPSMParam>::const_iterator mmcit3;

      ProteinAmbiguityGroupPtr pag = 
	ProteinAmbiguityGroupPtr(new ProteinAmbiguityGroup);
      pag->id = prg->name;
      pag->name = prg->name;

      std::list<std::string>::const_iterator sit = prg->praccs.begin();
      while (sit != prg->praccs.end()) {
	DBSequencePtr dbseq = findProtein(*sit);
	ProteinDetectionHypothesisPtr pdh = 
	  ProteinDetectionHypothesisPtr(new ProteinDetectionHypothesis(dbseq->id));
	pdh->dbSequencePtr = dbseq;
	pdh->passThreshold = true;

	pair<mmcit1,mmcit1> bounds1 = dbseq2pe.equal_range(pdh->dbSequencePtr);
	mmcit1 it1=bounds1.first;
	while (it1 != bounds1.second) {
	  PeptideHypothesis peph;
	  peph.peptideEvidencePtr = it1->second;
	  
	  pair<mmcit2,mmcit2> bounds2 = pe2sii.equal_range(it1->second);
	  mmcit2 it2=bounds2.first;
	  while (it2 != bounds2.second) {
	    peph.spectrumIdentificationItemPtr.push_back(it2->second);
	    ++it2;
	  }
	  pdh->peptideHypothesis.push_back(peph);
	  ++it1;
	}

	pair<mmcit3,mmcit3> bounds3 = prg->Params.equal_range(*sit);
	mmcit3 it3 = bounds3.first;
	while (it3 != bounds3.second) {
	  std::string thevalue = it3->second.value;
	  if (testProtein(it3->second.value)) {
	    DBSequencePtr dbseq = findProtein(it3->second.value);
	    thevalue = dbseq->id;
          }
	  CVID id = getCVID(it3->second.name);
	  if (id != CVID_Unknown) {
	    CVID unitid = getCVID(it3->second.units);
	    if (unitid != CVID_Unknown) {
	      pdh->set(id,thevalue,unitid);
	    } else {
	      pdh->set(id,thevalue);
	    }
	  } else {
	    if (it3->second.units != "") {
	      pdh->userParams.push_back(UserParam(it3->second.name, thevalue, it3->second.units));
	    } else {
	      pdh->userParams.push_back(UserParam(it3->second.name, thevalue));
	    }
	  }
	  ++it3;
	}

	pag->proteinDetectionHypothesis.push_back(pdh);
	++sit;

      }

      pair<mmcit3,mmcit3> bounds3 = prg->Params.equal_range("-");
      mmcit3 it3 = bounds3.first;
      while (it3 != bounds3.second) {
        std::string thevalue = it3->second.value;
        if (testProtein(it3->second.value)) {
          DBSequencePtr dbseq = findProtein(it3->second.value);
          thevalue = dbseq->id;
        }
        CVID id = getCVID(it3->second.name);
        if (id != CVID_Unknown) {
          CVID unitid = getCVID(it3->second.units);
          if (unitid != CVID_Unknown) {
            pag->set(id,thevalue,unitid);
          } else {
            pag->set(id,thevalue);
          }
        } else {
          if (it3->second.units != "") {
            pag->userParams.push_back(UserParam(it3->second.name, thevalue, it3->second.units));
          } else {
            pag->userParams.push_back(UserParam(it3->second.name, thevalue));
          }
        }
        ++it3;
      }
     
      pdl->proteinAmbiguityGroup.push_back(pag);

      delete prg;
      continue;
    }

    // Assumes PSMs are ordered by SpectrumFile....
    if (r->SpectrumFile != currentSpectrumFile) {
      sd = SpectraDataPtr(new SpectraData);
      sd->id = r->SpectrumFile;
      sd->location = r->Location;
      sd->spectrumIDFormat = spectrumIDFormat;
      sd->fileFormat = fileFormat;
      mzid.dataCollection.inputs.spectraData.push_back(sd);
      si->inputSpectra.push_back(sd);
      currentSpectrumFile = r->SpectrumFile;
    }
    
    // Assumes PSMs are ordered by SpectrumID within a SpectrumFile
    if (r->SpectrumID != currentSpectrumID) {
      sir = SpectrumIdentificationResultPtr(new SpectrumIdentificationResult);
      std::ostringstream oss;
      if (r->Scan != "") {
        oss << sd->id << "|" << r->Scan;
      } else {
        oss << sd->id << "|" << spectrumCounter;
        spectrumCounter++;
      }
      sir->id = oss.str();
      psmCounter = 1;
      sir->spectraDataPtr = sd;
      sir->spectrumID = r->SpectrumID;
      currentSpectrumID = r->SpectrumID;

      std::list<TextPSMParam>::const_iterator pait=r->SpecParams.begin();
      while (pait != r->SpecParams.end()) {
        CVID id = getCVID(pait->name);
        if (id != CVID_Unknown) {
	  CVID unitid = getCVID(pait->units);
	  if (unitid != CVID_Unknown) {
	    sir->set(id,pait->value,unitid);
          } else {
	    sir->set(id,pait->value);
          }
        } else {
	  if (pait->units != "") {
	    sir->userParams.push_back(UserParam(pait->name, pait->value, pait->units));
	  } else {
	    sir->userParams.push_back(UserParam(pait->name, pait->value));
          }
        }
        ++pait;
      }

      sil->spectrumIdentificationResult.push_back(sir);
    }
    
    sii = SpectrumIdentificationItemPtr(new SpectrumIdentificationItem);
    std::ostringstream oss;
    oss << sir->id << "|" << psmCounter;
    sii->id = oss.str();
    psmCounter++;

    // Not optional in the spec
    sii->rank = r->Rank;
    
    // Not optional or we can't compute the peptide m/z
    sii->chargeState = r->ChargeState;

    // Defaults to true
    sii->passThreshold = r->passThreshold;
    
    // Shouldn't this be a property of the spectrum?
    if (r->ExperimentalMassToCharge > 0) {
      sii->experimentalMassToCharge = r->ExperimentalMassToCharge;
    }
    
    sii->peptidePtr = getPeptide(*r, mzid);
    
    // Compute the peptide m/z using Proteowizard - ensures these are consistent...
    pwiz::proteome::Peptide proteomePeptide = pwiz::identdata::peptide(*(sii->peptidePtr));
    sii->calculatedMassToCharge = pwiz::chemistry::Ion::mz(proteomePeptide.monoisotopicMass(), 
							   sii->chargeState);
    
    std::list<TextPSMScore>::const_iterator sit=r->Scores.begin();
    while (sit != r->Scores.end()) {
      CVID id = getCVID(sit->name);
      if (id != CVID_Unknown) {
	sii->set(id,sit->value);
      } else {
	std::ostringstream oss;
	oss << sit->value;
	sii->userParams.push_back(UserParam(sit->name, oss.str(), "xsd:float"));      
      }
      ++sit;
    }

    std::list<TextPSMParam>::const_iterator pait=r->Params.begin();
    while (pait != r->Params.end()) {
      CVID id = getCVID(pait->name);
      if (id != CVID_Unknown) {
        CVID unitid = getCVID(pait->units);
        if (unitid != CVID_Unknown) {
          sii->set(id,pait->value,unitid);
        } else {
          sii->set(id,pait->value);
        }
      } else {
        if (pait->units != "") {
          sii->userParams.push_back(UserParam(pait->name, pait->value, pait->units));
        } else {
          sii->userParams.push_back(UserParam(pait->name, pait->value));
        }
      }
      ++pait;
    }

    std::list<TextPSMProtein>::const_iterator pit=r->Proteins.begin();
    while (pit != r->Proteins.end()) {
      DBSequencePtr pr = getProtein(*pit,mzid);
      PeptideEvidencePtr pe = getPeptideEvidence(sii->peptidePtr,pr,*pit,mzid);
      sii->peptideEvidencePtr.push_back(pe);
      pe2sii.insert(make_pair(pe,sii));
      ++pit;
    }
    
    sir->spectrumIdentificationItem.push_back(sii);

    delete r;
  }

  // References::resolve(mzid);
}    

