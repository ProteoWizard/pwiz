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

//class definition of DotProduct

#include "DotProduct.h"


namespace BiblioSpec {

DotProduct::DotProduct() {
  
}

DotProduct::~DotProduct()
{
}

void DotProduct::compare(Match& match)
{
    //get processed peaks
    const vector<PEAK_T>& exp = match.getExpSpec()->getProcessedPeaks();
  
    const vector<PEAK_T>& ref = match.getRefSpec()->getProcessedPeaks();

    getAngle(exp, ref, match);
}


//sum the square of the peak intensities for both spec separately
//sum the product of intenisties of the two spec for peaks of same mass
//return  sum(exp*ref) / sqrt( sum(exp*exp) * sum(ref*ref))
//similar to merging two linked lists, compare masses and
//  if they are the same generate all three product terms and advance both lists
//  if expSpec mass is smaller, get its product term and go to its next list element
//  if refSpec mass is smaller, get its product term and go to its next list element
void DotProduct::getAngle(const vector<PEAK_T>& exp, 
                          const vector<PEAK_T>& ref,
                          Match& match)
{
    vector<PEAK_T>::const_iterator curExp = exp.begin();
    vector<PEAK_T>::const_iterator curRef = ref.begin();
    double expIntSqSum = 0;      //total of square of intensities of expSpec
    double refIntSqSum = 0;      //total of square of intensities of refSpec
    double expRefIntSum = 0;     //total of exp*ref intensities for exp.mz==ref.mz
    int matchedIons = 0;

    while(curExp!=exp.end() && curRef!=ref.end()) {
        if( curExp->mz == curRef->mz ) { //get three product terms and add to totals
            expIntSqSum += (curExp->intensity)*(curExp->intensity);
            refIntSqSum += (curRef->intensity)*(curRef->intensity);
            expRefIntSum += ((curExp->intensity)*(curRef->intensity));
            matchedIons++;
            curExp++;
            curRef++;
        } else if( curExp->mz< curRef->mz ) {
            expIntSqSum += pow((curExp->intensity), 2);
            curExp++;
        } else if( curRef->mz < curExp->mz ) {
            refIntSqSum += pow((curRef->intensity), 2);
            curRef++;
        }
    }

    // add up any remaining peaks
    while( curExp != exp.end() ){
        expIntSqSum += pow((curExp->intensity), 2);
        curExp++;
    }
    while( curRef != ref.end() ){
        refIntSqSum += pow((curRef->intensity), 2);
        curRef++;
    }

    // Must use double values for the multiplication, since using floats can
    // result in overflow of the multiplication, and a zero result.
    double angle = expRefIntSum / sqrt(expIntSqSum*refIntSqSum);
    if( isnan(angle) ){ angle = 0; }

    match.setScore(DOTP, angle);
    match.setScore(MATCHED_IONS, matchedIons);
}

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
