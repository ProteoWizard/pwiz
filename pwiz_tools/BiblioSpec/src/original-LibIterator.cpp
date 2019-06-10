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

//class definition for an iterator to access a library's spectra

#include "pwiz/utility/misc/Std.hpp"
#include "original-LibIterator.h"

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
