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

#include <string>
#include <vector>
#include <map>
#include <limits>
#include "Verbosity.h"
#include "ProgressIndicator.h"
#include "saxhandler.h"
#include "BlibBuilder.h"
#include "BlibUtils.h"
#include "mzxmlFinder.h"
#include "PSM.h"
#include "SpecFileReader.h"
#include "PwizReader.h"

using namespace std;

namespace BiblioSpec {

const static int SMALL_BUFFER_SIZE = 64;
const static int LARGE_BUFFER_SIZE = 8192;

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
  string fullFilename_;   ///< path to name of the file we are parsing
  string filepath_;       ///< path stripped from full name
  string fileroot_;       ///< filename stripped of path and extension
  string curSpecFileName_;///< name of the next spectrum file to parse
  BlibBuilder& blibMaker_;  ///< object for creating library
  const ProgressIndicator* parentProgress_;  ///< progress of our caller
  ProgressIndicator* fileProgress_;  ///< progress of multiple spec files
  ProgressIndicator* specProgress_;  ///< progress of each spectrum in a file
  int fileProgressIncrement_; ///< when file progress is by pepxml size instead 
                              // of number of spec files

  void insertSpectrum(PSM* psm, SpecData& curSpectrum, 
                      sqlite3_int64 fileId, PSM_SCORE_TYPE scoreType);
  void sortPsmMods(PSM* psm);
  string generateModifiedSeq(const char* unmodSeq, const vector<SeqMod>& mods);
  void removeDuplicates();
  string fileNotFoundMessage(const char* specfileroot,
                             const vector<const char*>& extensions,
                             const vector<const char*>& directories);

 protected:
  ProgressIndicator* readAddProgress_;  ///< 2 steps: read file, add spec
  PSM* curPSM_;           ///< temp holding space for psm being parsed
  vector<PSM*> psms_;     ///< collected list of psms parsed from file
  SpecFileReader* specReader_; ///< for getting peak lists
  SPEC_ID_TYPE lookUpBy_; ///< default is by scan number

  void openFile();
  void closeFile();
  void initReadAddProgress();
  void initSpecFileProgress(int numSpecFiles);
  void initSpecProgress(int numSpec);
  void setNextProgressSize(int size);

  void setSpecFileName(const char* filename, bool checkFile = true);
  void setSpecFileName(const char* fileroot, 
                       const vector<const char*>& extensions,
                       const vector<const char*>& directories = vector<const char*>());

  double getScoreThreshold(BUILD_INPUT fileType);
  void findScanNumFromName();
  void findScanIndexFromName();
  sqlite3_int64 insertSpectrumFilename(string& filename, bool insertAsIs = false);
  void buildTables(PSM_SCORE_TYPE score_type, string specfilename = "");
  const char* getPsmFilePath(); // path containing file being parsed

 public:
  BuildParser(BlibBuilder& maker,
              const char* filename,
              const ProgressIndicator* parent_progress);
  ~BuildParser();
  virtual bool parseFile() = 0; // pure virtual, force subclass to define

  string getFileName();
  string getSpecFileName();
};

} // namespace



















/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
