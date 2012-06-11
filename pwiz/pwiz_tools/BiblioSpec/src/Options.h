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
 *Options class to represent command line options 
 *that users input.
 *
 * $Id: Options.h, v 1.0 2008/10/31 09:53:52 Ning Zhang Exp $
 */


#ifndef OPTIONS_H
#define OPTIONS_H

#include <stdlib.h>
#include <iostream>
#include <fstream>
#include <string>
#include <sstream>
#include <map>

using namespace std;

class Options{

 private:
  ifstream paramFile;
 
 public:
  Options();
  ~Options();

  Options& operator=(const Options& right);

  
  bool isClearPrecursor;
  bool isReport;
  int topPeaksForSearch;
  int sqliteTopMatches;
  float mzWindow;
  int chargeLow;
  int chargeHigh;
  int verbosity;
  int reportTopMatches;

  void parseOptions(); 
  map<string, int> getOptionsMap();
  string toString();
  
};

#endif
/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
