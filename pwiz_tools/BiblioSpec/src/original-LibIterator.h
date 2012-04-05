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

//header file for an iterator that gives access to a library's spectra

#ifndef LIB_ITERATOR
#define LIB_ITERATOR

#include <vector>
#include "original-RefSpectrum.h"
#include "original-ProcessedPeaks.h"

struct refSpecPair{
  RefSpectrum* specp;
  ProcessedPeaks* pPeaksp;

  refSpecPair();
};

class LibIterator
{
 private:
  //  vector<RefSpectrum>* specs;
  vector<RefSpectrum*> &specs;
  vector<ProcessedPeaks*> &pPeaks;
  int first;   //index of first spectrum to return
  int last;    //index of spectrum AFTER the last one to return
  int current; //index of the next spectrum to return

  void init();

 public:
  //LibIterator();
  LibIterator( vector<RefSpectrum*> &libsVectorp, 
           vector<ProcessedPeaks*> &libsPPvectorp, 
           int first, int last );
  LibIterator(const LibIterator& l);
  ~LibIterator();

  LibIterator& operator=(const LibIterator& right);

  RefSpectrum* getSpecp();
  refSpecPair getSpecPair();
  void getSpec(RefSpectrum* refSpec);//not used
  RefSpectrum getSpec();
  bool hasSpec();
  int numLeft();

};

#endif // LIB_ITERATOR
/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
