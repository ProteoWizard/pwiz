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

#include <iostream>
#include <fstream>
#include <cstring>
#include <cstdio>
#include <cstdlib>
#include "sqlite3.h"
#include <time.h>
#include <vector>
#include <sys/stat.h>
#include "BlibMaker.h"
#include "ProgressIndicator.h"
#include "Verbosity.h"
#include "BlibUtils.h"

using namespace std;

namespace BiblioSpec {

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

    // Keep this last
    NUM_BUILD_INPUTS
};

extern string base_name(const char* name);
extern bool has_extension(const char* name, const char* ext);

class BlibBuilder : public BlibMaker
{
 public:
  BlibBuilder();
  ~BlibBuilder();
  void usage();

  //double getProbabilityCutoff();
  double getScoreThreshold(BUILD_INPUT fileType); // replaces getProbabilityCutoff()
  int getLevelCompress();
  vector<char*> getInputFiles();
  virtual int parseCommandArgs(int argc, char* argv[]);
  virtual void attachAll();
  int transferLibrary(int iLib, const ProgressIndicator* parentProgress);
  virtual void commit();
  void insertPeaks(int spectraID, 
                   int peaksCount, 
                   double* pM, 
                   float* pI);
  int getCacheThreshold(){ return fileSizeThresholdForCaching; }

 protected:
  int parseNextSwitch(int i, int argc, char* argv[]);

 private:
  // Command-line options
  //double probability_cutoff; 
  double scoreThresholds[NUM_BUILD_INPUTS]; // replaces probability_cutoff
  int level_compress;
  int fileSizeThresholdForCaching; // for parsing .dat files
  vector<char*> input_files;
};

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
