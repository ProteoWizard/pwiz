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

/*
 * A program that reads through a SQT file and stores
 * the search results into library table.
 *
 * $ SQTreader.h,v 1.0 2009/01/12 15:53:52 Ning Zhang Exp $
 */

#ifndef SQT_READER_H
#define SQT_READER_H

#include "BuildParser.h"
#include "SQTversion.h"
#include <boost/xpressive/xpressive_dynamic.hpp>

#define MAX_MODS 128


class BlibMaker;

namespace BiblioSpec { 

class SQTreader : public BuildParser {

 public:

  SQTreader(BlibBuilder& maker,
            const char* sqtfilename,
            const ProgressIndicator* parent_progress); 
  ~SQTreader();

  bool parseFile(); // impelement BuildParser virtual function
  std::vector<PSM_SCORE_TYPE> getScoreTypes();
  void openRead(bool warnIfNotPercolated);
  void parseModifiedSeq(const char* modSeq, 
                        string& unmodSeq, 
                        vector<SeqMod>& mods,
                        bool hasFlankingAA = true);

 private:
  ifstream file;
  double staticMods[MAX_MODS];
  double diffMods[MAX_MODS];
  bool percolated;
  SQTversion * sqtVersion;
  double masses_[128];

  boost::xpressive::sregex cometModRegex;
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
