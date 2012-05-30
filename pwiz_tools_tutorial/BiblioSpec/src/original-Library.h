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

//header file for Library class

#ifndef LIBRARY_H
#define LIBRARY_H

#include <cstdlib>
#include <ctime>
#include <assert.h>
#include <vector>
#include <sstream>
#include "original-LibIterator.h"
#include "original-RefSpectrum.h"
#include "original-ProcessedPeaks.h"

struct library_header{
  int numSpec;
  bool filtered;
  int specVersion;
  int annotVersion;
  int nextId;

  library_header();
};
typedef struct library_header LIBHEAD_T;

struct annot_pair{
  int id;
  int annot;
};
bool comp_pair_id( annot_pair p1, annot_pair p2 );

class Library{
  const static int DEFAULT_VERBOSITY = 3;
  const static float MAX_MZ;

 private:
  LIBHEAD_T header;
  vector<RefSpectrum*>    refSpecv;
  vector<ProcessedPeaks*> procPeaksv;
  RefSpectrum tmpRefSpec;
  int verbosity;

  //specifically for filtering
  bool sortedByIon;   
  int nextIonIndex;

  void init();  //get rid of these?
  void init(int verbosity);  

  //for searching
  int findLowMz(float mz);
  int findHiMz (float mz);

  //what are these for?
  void shuffle(int firstIndex, int lastIndex);
  int getRandInt(int lowest, int highest);

  //for update
  void makeAnnotations(vector<annot_pair>& );
  void markDeletedSpec(vector<int>& ids);

 public:
  Library();
  Library(const char* filename, int verbosity);
  ~Library();


  //do things
  void readFromFile(const char* filename);
  void writeToFile(const char* filename);
  void deleteSpec(const char* filename);
  void annotate(const char* filename);
  void addSpec(vector<RefSpectrum*>::iterator first,
           vector<RefSpectrum*>::iterator last);
  void sortByIon();
  void sortByID();

  //get things
  LibIterator getSpecInRange(float lowMz, float hiMz);
  LibIterator getAllSpec();
  LibIterator getNextIon(int max);
  int getNumSpec();
  string getVersion_str();
  bool filtered();
  LIBHEAD_T getHeader();

  //MergeLibraries
  vector<RefSpectrum*>::iterator getFirstSpec();
  vector<RefSpectrum*>::iterator getLastSpec();

  
};

#endif //LIBRARY_H
/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
