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


/*
 *  Reportfile class writes an optional output file.  Tab-delimited
 *  format as an alternative to Sqlite format.
 */                                                                         

#ifndef REPORTFILE
#define REPORTFILE

#include <iostream>
#include <iomanip>
#include <vector>
#include <fstream>
#include <string>
#include "Verbosity.h"
#include "Match.h"
#include "boost/program_options.hpp"

namespace ops = boost::program_options;

namespace BiblioSpec {

class Reportfile
{
 private:
  ofstream file_;
  int topMatches_;
  string optionsString_;

  void writeHeader();
  string optionsHeaderString(const ops::variables_map& options_table);
 
 public:
  Reportfile(const ops::variables_map& options_table);
  ~Reportfile();
  void open(const char* filename);
  void writeMatches(const vector<Match>& results);
  
};

} // namespace

#endif //REPORTFILE

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
