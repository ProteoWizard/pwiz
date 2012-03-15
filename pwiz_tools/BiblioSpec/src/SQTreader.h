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

/*
 * A program that reads through a SQT file and stores
 * the search results into library table.
 *
 * $ SQTreader.h,v 1.0 2009/01/12 15:53:52 Ning Zhang Exp $
 */

#ifndef SQT_READER_H
#define SQT_READER_H

#include "BuildParser.h"

#define MAX_MODS 128

using namespace std;

class BlibMaker;

namespace BiblioSpec { 

class SQTreader : public BuildParser {

 public:

  SQTreader(BlibBuilder& maker,
            const char* sqtfilename,
            const ProgressIndicator* parent_progress); 
  ~SQTreader();

  bool parseFile(); // impelement BuildParser virtual function
  void openRead();
  void parseModifiedSeq(const char* modSeq, 
                        string& unmodSeq, 
                        vector<SeqMod>& mods,
                        bool hasFlankingAA = true);

 private:
  ifstream file;
  double staticMods[MAX_MODS];
  double diffMods[MAX_MODS];
  bool percolated;
  float sequestVersion;
  double masses_[128];

  // for values read from file
  double precursorMH;
  double qvalue;
  char wholePepSeq[200];
  int scanNumber;
  int charge;

  void extractPSMs(); //populate the list of psms

};

} // namespace

#endif







/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
