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

/**
 * The MascotResultsReader collects a list of psms that should be
 * included in the library.  It passes the file object it was using to
 * the MascotSpecReader so the file only has to be opened and parsed once.
 */

#include "BuildParser.h"
#ifdef DUPLICATE
#undef DUPLICATE
#endif
#include "msparser.hpp"
#include "MascotSpecReader.h"
#include <iostream>
#include <iomanip>

using namespace matrix_science;
typedef map<char, double> ModTable;
typedef map<char, vector<double> > MultiModTable;

namespace BiblioSpec {

class MascotResultsReader : public BuildParser{
    
 public:
  MascotResultsReader(BlibBuilder& maker, 
                      const char* datFileName, 
                      const ProgressIndicator* parent_progress);
  ~MascotResultsReader();

  bool parseFile();
  vector<PSM_SCORE_TYPE> getScoreTypes();

 private:
  enum { N_TERM_POS = 'n', C_TERM_POS = 'c' };

  ms_mascotresfile* ms_file_;
  ms_searchparams* ms_params_;
  ms_mascotresults* ms_results_;
  double scoreThreshold_;
  MultiModTable staticMods_;
  map<string, ModTable* > methodModsMaps_;
  // for each method name (e.g. light, heavy) a table with mass difs by residue
  map<string, vector<PSM*> > fileMap_; // PSMs stored by spec filename
  vector<string> specFileExtensions_;  // a list of possible extensions
  ProgressIndicator* readSpecProgress_; // each spec read from .dat file 
  vector<string> rawFiles_; // a list of distiller rawfiles
  boost::shared_ptr<TempFileDeleter> tmpDatFile_;

  void initParse();
  void getIsotopeMasses();
  void applyIsotopeDiffs(PSM* psm, string quantName);
  void parseMods(PSM* psm, string modstr, string readableModStr);
  int getVarModIndex(const char c);
  void addVarMod(PSM* psm, char varLookUpChar, int aaPosition);
  void addStaticModToTable(char aa, double deltaMass);
  void addStaticMods(PSM* psm, char staticLookupChar, int aaPosition);
  void addErrorTolerantMod(PSM* psm, string readableModStr, int aaPosition);
  int findMaxRankingPeptideToAdd(int specId);
  void getDistillerRawFiles(const ms_searchparams* searchparams, vector<string>& v);
  bool IsPlausibleRawFileName(const string& name) const;
  bool IsPlausibleMGFFileName(const string &name) const;
  string getFilename(ms_inputquery& spec);
  string getErrorMessage();
  string getErrorMessage(int errorCode);
  unsigned int getCacheFlag(const string& filename, int size);
};

} // namespace










/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */

