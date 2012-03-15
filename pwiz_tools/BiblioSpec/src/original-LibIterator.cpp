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
//class definition for an iterator to access a library's spectra

#include "original-LibIterator.h"

using namespace std;

refSpecPair::refSpecPair() {
    specp = NULL;
    pPeaksp = NULL;
}

LibIterator::LibIterator( vector<RefSpectrum*> &libsVectorp, 
                          vector<ProcessedPeaks*> &libsPPvectorp,
                          int firstspec, int lastspec) : 
specs( libsVectorp ), pPeaks( libsPPvectorp )
{
    first = firstspec;
    last = lastspec;
    current = first;
}


LibIterator::LibIterator(const LibIterator& l) : 
specs( l.specs ), pPeaks( l.pPeaks )
{
    first = l.first;
    last = l.last;
    current = l.current;
}

LibIterator::~LibIterator()
{
}

LibIterator& LibIterator::operator=(const LibIterator& right)
{
    if(this != &right) {
        specs = right.specs;
        first = right.first;
        last = right.last;
        current = right.current;
    }
    return *this;
}
    
    void LibIterator::init() {
        first = 0;
        last = 0;
        current = 0;
    }


RefSpectrum LibIterator::getSpec()
{
    RefSpectrum currentSpec = *(specs.at(current));
    current++;
    return currentSpec;
}

void LibIterator::getSpec(RefSpectrum* refSpec) {
    //not yet tested
    *refSpec = *(specs.at(current));
    current++;
}

RefSpectrum* LibIterator::getSpecp() {
    return specs.at(current++);
}

//Return the next spectrum pair
//pair: 1. pointer to spectrum in library
//      2. pinter to processed peaks in library
//create an unprocessed processed peaks object if none exists
refSpecPair LibIterator::getSpecPair() {
    refSpecPair pair;
    pair.specp = specs.at(current);
    
    if( pPeaks.at(current) == NULL ) {
        pPeaks.at(current) = new ProcessedPeaks( specs.at(current) );
    }
    pair.pPeaksp = pPeaks.at(current);
    current++;
    
    return pair;
}

bool LibIterator::hasSpec()
{
    return current <  last;
}

int LibIterator::numLeft()
{
    return last - current; //-1?
}

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
