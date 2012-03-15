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
/*
 *  BiblioSpec Version 1.0
 *  Copyright 2006 University of Washington. All rights reserved.
 *  Written by Barbara Frewen, Michael J. MacCoss, William Stafford Noble
 *  in the Department of Genome Sciences at the University of Washington.
 *  http://proteome.gs.washington.edu/
 *
 */
                                                                                  
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
