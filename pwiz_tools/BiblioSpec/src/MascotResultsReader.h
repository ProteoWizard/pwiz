/*
  Copyright (c) 2011, University of Washington
  All rights reserved.

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions
  are met:

    * Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer. 
    * Redistributions in binary form must reproduce the above
    copyright notice, this list of conditions and the following
    disclaimer in the documentation and/or other materials provided
    with the distribution. 
    * Neither the name of the <ORGANIZATION> nor the names of its
    contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
  FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
  COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
  INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
  BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
  LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
  CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
  ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.
*/
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

using namespace matrix_science;
typedef map<char, double> ModTable;

namespace BiblioSpec {

class MascotResultsReader : public BuildParser{
    
 public:
  MascotResultsReader(BlibBuilder& maker, 
                      const char* datFileName, 
                      const ProgressIndicator* parent_progress);
  ~MascotResultsReader();

  bool parseFile();

 private:
  ms_mascotresfile* ms_file_;
  ms_searchparams* ms_params_;
  ms_mascotresults* ms_results_;
  double scoreThreshold_;
  ModTable staticMods_;
  map<string, ModTable* > methodModsMaps_;
  // for each method name (e.g. light, heavy) a table with mass difs by residue
  map<string, vector<PSM*> > fileMap_; // PSMs stored by spec filename
  vector<string> specFileExtensions_;  // a list of possible extensions
  ProgressIndicator* readSpecProgress_; // each spec read from .dat file 

  void getIsotopeMasses();
  void applyIsotopeDiffs(PSM* psm, string quantName);
  void parseMods(PSM* psm, string modstr, string readableModStr);
  int getVarModIndex(const char c);
  void addVarMod(PSM* psm, char varLookUpChar, int aaPosition);
  void addErrorTolerantMod(PSM* psm, string readableModStr, int aaPosition);
  int findMaxRankingPeptideToAdd(int specId);
  string getFilename(ms_inputquery& spec);
  string getErrorMessage();
  string getErrorMessage(int errorCode);
  unsigned int getCacheFlag(const char* filename, int size);
};

} // namespace










/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */

