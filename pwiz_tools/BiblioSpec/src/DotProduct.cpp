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
//class definition of DotProduct

#include "DotProduct.h"

using namespace std;

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
