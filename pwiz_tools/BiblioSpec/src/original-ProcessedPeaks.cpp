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

//Class that contains peaks from a spectrum that have been processed
// a glorified struct

#include "pwiz/utility/misc/Std.hpp"
#include "original-ProcessedPeaks.h"

ProcessedPeaks::ProcessedPeaks() {
    peaks = NULL;
    binned = false;
    processed = false;
    totalIntensity = 0;

}

//I would like to make this (const Spectrum*)
//which will require const Spectrum::putPeaksHere
ProcessedPeaks::ProcessedPeaks(Spectrum* spec) {
    peaks = new vector<PEAK_T>();
    spec->putPeaksHere( peaks );
    binned = false;
    processed = false;
    totalIntensity = 0;
    precursor_mz = spec->getMz();
}

ProcessedPeaks::~ProcessedPeaks() {
    if( peaks ) {
        delete peaks;
    }
}

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
