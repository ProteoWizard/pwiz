//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
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

#pragma once

#include <string>
#include <vector>
#include <set>
#include <map>
#include <limits>
#include <cmath>
#include "Verbosity.h"
#include "ProgressIndicator.h"
#include "saxhandler.h"
#include "BlibBuilder.h"
#include "BlibUtils.h"
#include "mzxmlFinder.h"
#include "SpecFileReader.h"
#include "SqliteRoutine.h"
#include "PwizReader.h"
#include "AminoAcidMasses.h"

namespace BiblioSpec {

const static double H2O_MASS = 18.01056469252;

// todo move to BlibUtils
bool seqsILEquivalent(string seq1, string seq2);

/**
 * \class BuildParser
 * \brief A generic class for parsing files used to build libraries.
 * Extend this class for each specific file format.
 *
 * Files are typically search results and should contain at a minimum
 * a peptide sequence, an identifier for a spectrum, and a charge.
 * How that information is extracted from the file is defined by file
 * format subclasses.
 *
 * Subclasses must define the virtual method parseFile() in which they
 * may choose to use the SAXHandler capabilities for reading
 * XML. Within parseFile(), they are encouraged to use the member
 * variables currentPSM and PSMs for storing results and the functions
 * setSpecFilename() and buildTables() for transfering the results to
 * sqlite tables. 
 *
 * Fatal errors are handled by throwing a BlibException error.  The
 * SAXhandler::throwParseError() should not be used as not all
 * BuildParsers will use the saxhandler.
 */
class BuildParser : protected SAXHandler{

 private:
  sqlite3_stmt* insertSpectrumStmt_;
  string fullFilename_;   ///< path to name of the file we are parsing
  string filepath_;       ///< path stripped from full name
  string fileroot_;       ///< filename stripped of path and extension
  string curSpecFileName_;///< name of the next spectrum file to parse
  ProgressIndicator* fileProgress_;  ///< progress of multiple spec files
  ProgressIndicator* specProgress_;  ///< progress of each spectrum in a file
  int fileProgressIncrement_; ///< when file progress is by pepxml size instead 
                              // of number of spec files
  map<int, int> inputToSpec_; ///< map of input file index to spectrum file count for that input file

  void insertSpectrum(PSM* psm, const SpecData& curSpectrum, 
                      sqlite3_int64 fileId, PSM_SCORE_TYPE scoreType,
                      map<const Protein*, sqlite3_int64>& proteins);
  void sortPsmMods(PSM* psm);
  double calculatePeptideMass(PSM* psm);
  int calculateCharge(double neutralMass, double precursorMz);
  void filterBySequence(const set<string>* targetSequences, const set<string>* targetSequencesModified);
  void removeDuplicates();
  double aaMasses_[128];

 protected:
  BlibBuilder& blibMaker_;  ///< object for creating library
  const ProgressIndicator* parentProgress_;  ///< progress of our caller
  ProgressIndicator* readAddProgress_;  ///< 2 steps: read file, add spec
  PSM* curPSM_;           ///< temp holding space for psm being parsed
  vector<PSM*> psms_;     ///< collected list of psms parsed from file
  SpecFileReader* specReader_; ///< for getting peak lists
  SPEC_ID_TYPE lookUpBy_; ///< default is by scan number
  bool preferEmbeddedSpectra_; ///< default is true except for MaxQuant

  void openFile();
  void closeFile();
  void initReadAddProgress();
  void initSpecFileProgress(int numSpecFiles);
  void initSpecProgress(int numSpec);
  void setNextProgressSize(int size);

  void setSpecFileName(std::string filename, bool checkFile = true);
  void setSpecFileName(std::string fileroot, 
                       const vector<std::string>& extensions,
                       const vector<std::string>& directories = vector<std::string>());
  void setPreferEmbeddedSpectra(bool preferEmbeddedSpectra);

  void verifySequences();
  double getScoreThreshold(BUILD_INPUT fileType);
  void findScanIndexFromName(const std::map<PSM*, double>& precursorMap);
  sqlite3_int64 insertSpectrumFilename(string& filename, bool insertAsIs = false);
  sqlite3_int64 insertProtein(const Protein* protein);
  void buildTables(PSM_SCORE_TYPE score_type, string specfilename = "", bool showSpecProgress = true);
  const char* getPsmFilePath(); // path containing file being parsed
  string getFilenameFromID(const string& idStr); // spectrum source file from spectrum ID

  static bool validInts(vector<string>::const_iterator begin, vector<string>::const_iterator end);

  string fileNotFoundMessage(std::string specfileroot,
      const vector<std::string>& extensions,
      const vector<std::string>& directories);
  string filesNotFoundMessage(const vector<std::string>& specfileroots,
      const vector<std::string>& extensions,
      const vector<std::string>& directories);

 public:
  BuildParser(BlibBuilder& maker,
              const char* filename,
              const ProgressIndicator* parent_progress);
  virtual ~BuildParser();
  virtual bool parseFile() = 0; // pure virtual, force subclass to define
  virtual std::vector<PSM_SCORE_TYPE> getScoreTypes() = 0; // pure virtual, force subclass to define

  const string& getFileName();
  const string& getSpecFileName();
};

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
