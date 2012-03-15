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

using namespace std;
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
