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
