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
 * Class for building a library from search result files and their
 * accompanying spectrum files and/or from existing libraries.
 * Extends BlibMaker.
 *
 * $ BlibBuilder.h,v 1.0 2009/01/07 15:53:52 Ning Zhang Exp $
 */

#include <istream>
#include <iostream>
#include <iomanip>
#include <fstream>
#include <sstream>
#include <cctype>
#include <cstring>
#include <cstdio>
#include <cstdlib>
#include "sqlite3.h"
#include <ctime>
#include <vector>
#include <set>
#include <queue>
#include <sys/stat.h>
#include "BlibMaker.h"
#include "ProgressIndicator.h"
#include "Verbosity.h"
#include "BlibUtils.h"
#include "PSM.h"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/utility/misc/Container.hpp"


namespace BiblioSpec {

const static int SMALL_BUFFER_SIZE = 64;
const static int LARGE_BUFFER_SIZE = 8192;

enum BUILD_INPUT
{
    SQT,
    PEPXML,
    IDPXML,
    MASCOT,
    TANDEM,
    PROT_PILOT,
    SCAFFOLD,
    MSE,
    OMSSA,
    PROT_PROSPECT,
    MAXQUANT,
    MORPHEUS,
    MSGF,
    PEAKS,
    BYONIC,
    PEPTIDE_SHAKER,
    GENERIC_QVALUE_INPUT,

    // Keep this last
    NUM_BUILD_INPUTS
};

extern string base_name(string name);
extern bool has_extension(string name, string ext);

class BlibBuilder : public BlibMaker
{
 public:
  BlibBuilder();
  ~BlibBuilder();
  void usage();

  //double getProbabilityCutoff();
  double getScoreThreshold(BUILD_INPUT fileType); // replaces getProbabilityCutoff()
  int getLevelCompress();
  vector<string> getInputFiles();
  void setCurFile(int i);
  int getCurFile() const;
  string getMaxQuantModsPath();
  string getMaxQuantParamsPath();
  double getPusherInterval() const;
  const set<string>* getTargetSequences();
  const set<string>* getTargetSequencesModified();
  virtual int parseCommandArgs(int argc, char* argv[]);
  virtual void attachAll();
  int transferLibrary(int iLib, const ProgressIndicator* parentProgress);
  void collapseSources();
  virtual void commit();
  void insertPeaks(int spectraID, 
                   int peaksCount, 
                   double* pM, 
                   float* pI);
  int getCacheThreshold(){ return fileSizeThresholdForCaching; }
  string generateModifiedSeq(const char* unmodSeq, const vector<SeqMod>& mods);
  virtual double getCutoffScore() const;
  static string getModifiedSequenceWithPrecision(const char* unmodSeq, const vector<SeqMod>& mods, bool isHighPrecision);
  static string getLowPrecisionModSeq(const char* unmodSeq, const vector<SeqMod>& mods) 
  {
    return getModifiedSequenceWithPrecision(unmodSeq, mods, false);
  }

 protected:
  int parseNextSwitch(int i, int argc, char* argv[]);

 private:
  // Command-line options
  enum STDIN_LIST { FILENAMES, UNMODIFIED_SEQUENCES, MODIFIED_SEQUENCES };
  double explicitCutoff;
  int level_compress;
  int fileSizeThresholdForCaching; // for parsing .dat files
  vector<string> input_files;
  map<string, double> inputThresholds;
  int curFile;
  string maxQuantModsPath;
  string maxQuantParamsPath;
  double forcedPusherInterval;
  set<string>* targetSequences;
  set<string>* targetSequencesModified;
  queue<STDIN_LIST> stdinput;
  istream* stdinStream;

  static string parseSequence(const string& sequence, bool modified);
  int readSequences(set<string>** seqSet, bool modified = false);
};

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
