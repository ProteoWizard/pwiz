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
 * A class for manipulating the sqlite3 format for search results.
 */

#include "Verbosity.h"
#include "BlibUtils.h"
#include "SqliteRoutine.h"
#include "boost/program_options.hpp"
#include "Spectrum.h"
#include "Match.h"
#include "SearchLibrary.h"
//#include "zlib.h"

namespace ops = boost::program_options;

namespace BiblioSpec {

class PsmFile {

 private:
    int blibRunSearchID_;
    sqlite3* db_;  // a db to hold results and query spec
    int reportMatches_; // number of top hits to print to file(s)

    void createTables(const ops::variables_map& options_table);
    void reorgDB();

 public:
    PsmFile(const char* filename, 
            const ops::variables_map& options_table);
  ~PsmFile();

  void insertSpecData(Spectrum& s, 
                      const vector<Match>& matches, 
                      SearchLibrary& szLib);

  void insertMatches(const vector<Match>& matches);
  void commit();

};

} // namespace
/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
