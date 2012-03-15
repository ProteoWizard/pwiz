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
