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
 //class definition of Peak Processor class
//steps of peak processing, bin->removePrecursor->removeNoise->normalizeIntensity

#include "PeakProcess.h"
#include <list>

namespace BiblioSpec {

    // TODO get rid if this constructor
PeakProcessor::PeakProcessor()
{
    isClearPrecursor_ = true;
    noiseFirst_ = true;
    numTopPeaks_ = 100;
    binSize_ = 1;
    binOffset_ = 0;
}

/**
 * Create and initialize a PeakProcessor with the appropriate options values.
 */
PeakProcessor::PeakProcessor(const ops::variables_map& option) :
  isClearPrecursor_(option["clear-precursor"].as<bool>()), 
  noiseFirst_(option["remove-noise-first"].as<bool>()), 
  numTopPeaks_(option["topPeaksForSearch"].as<int>()),
  binSize_(option["bin-size"].as<double>()),
  binOffset_(option["bin-offset"].as<double>())
{
}

PeakProcessor::~PeakProcessor() {
}

void PeakProcessor::setClearPrecursor(bool clear)
{
    isClearPrecursor_ = clear;
}

void PeakProcessor::setNumTopPeaksToUse(int num)
{
    numTopPeaks_ = num;
}

/**
 * Bin, normalize intensity and remove noise from the peaks of the
 * given spectrum.
 */
void PeakProcessor::processPeaks(Spectrum* spec)
{
    //bin peaks
    vector<PEAK_T> givenPeaks = spec->getRawPeaks();
    vector<PEAK_T> binnedPeaks;
    double totalIntensity =  binPeaks(givenPeaks, binnedPeaks);
    spec->setTotalIonCurrentRaw(totalIntensity);

    //remove peaks around the precursor ion
    if( isClearPrecursor_)
        removePrecursorPeaks(binnedPeaks, spec->getMz());

    // storage for intermediate steps
    vector<PEAK_T> signalPeaks;
    vector<PEAK_T> normalizedPeaks;

    if( noiseFirst_ ){
        //remove noise, put signal peaks in a new vector
        topNpeaks(binnedPeaks, signalPeaks, numTopPeaks_);
        //quickTopNpeaks(binnedPeaks, signalPeaks, numTopPeaks_);

        
        //get normalized intensities, 
        normMz(signalPeaks, normalizedPeaks, 0);
        
        spec->setProcessedPeaks(normalizedPeaks);
    } else {
        //get normalized intensities, 
        normMz(binnedPeaks, normalizedPeaks, 0);

        //remove noise, put signal peaks in a new vector
        topNpeaks(normalizedPeaks, signalPeaks, numTopPeaks_);
        //quickTopNpeaks(normalizedPeaks, signalPeaks, numTopPeaks_);
        
        spec->setProcessedPeaks(signalPeaks);
    }

}
// todo remove this in favor of above
/*
void PeakProcessor::processPeaks(Spectrum& spec)
{
    //bin peaks
    vector<PEAK_T>* givenPeaks = spec.getPeaks();
    vector<PEAK_T> binnedPeaks;
    float totalIntensity =  (float)binPeaks(*givenPeaks, binnedPeaks, (float)BIN_SIZE);
    //binPeaks(*givenPeaks, binnedPeaks, BIN_SIZE);
    spec.setTIC(totalIntensity);

    //remove peaks around the precursor ion
    if( isClearPrecursor)
        removePrecursorPeaks(binnedPeaks, (float)spec.getMZ());

    //remove noise, put signal peaks in a new vector
    vector<PEAK_T> signalPeaks;
    topNpeaks(binnedPeaks, signalPeaks, (float)numTopPeaks);
   
    //get normalized intensities, 
    vector<PEAK_T> normPeaks;
    normMz(signalPeaks, normPeaks, 0);

    spec.setPeaks(normPeaks);

}
*/

// To avoid influence from neutral losses from the precursor ion
// remove peaks in a window around it.
// find a better place to put these constants
void PeakProcessor::removePrecursorPeaks(vector<PEAK_T>& peaks, double mz) {

    double minDelta = -20.0;
    double maxDelta = +5.0;

    //create peaks to use in the search
    PEAK_T lowPeak;
    lowPeak.intensity = 0;
    lowPeak.mz = mz + minDelta;

    PEAK_T hiPeak;
    hiPeak.intensity = 0;
    hiPeak.mz = mz + maxDelta;

    //find the first peak to delete
    vector<PEAK_T>::iterator first = 
        lower_bound(peaks.begin(), peaks.end(), lowPeak, compPeakMz);
    //find the last peak to delete
    vector<PEAK_T>::iterator last = 
        upper_bound(peaks.begin(), peaks.end(), hiPeak, compPeakMz);

    //delete
    if( first != peaks.begin() ) { first -=1;}
    if( last != peaks.end() ) { last +=1;}
    peaks.erase(first, last);

}






/******** PEAK BINNING METHODS **********/

/**
 * Create a new vector of peaks in results.  m/z value of peaks will
 * now be a bin number, not an actual m/z value.  Intensities of
 * multiple peaks in one bin are summed.
 * \returns The sum of the intensties of all raw peaks.
 */
double PeakProcessor::binPeaks(vector<PEAK_T>& peaks, 
                               vector<PEAK_T>& results )
{
    // start with an empty vector
    results.clear();

    // if no peaks, return 
    if( peaks.empty() ){
        return 0;
    }

    double totalIntensity = 0;

    //initialize results to the first peak
    PEAK_T tmpPeak;
    tmpPeak.mz = getBin( peaks.at(0).mz );  
    tmpPeak.intensity = peaks.at(0).intensity;
    results.push_back( tmpPeak );
    
    totalIntensity = tmpPeak.intensity;
    for(int i=1; i<(int)peaks.size(); i++) {
        tmpPeak.mz = getBin( peaks.at(i).mz );
        tmpPeak.intensity = peaks.at(i).intensity;
        totalIntensity += peaks.at(i).intensity;
        
        if( tmpPeak.mz == results.back().mz )
            results.back().intensity += peaks.at(i).intensity;
        else
            results.push_back(tmpPeak);
    }
    
    return totalIntensity;
}
 
/**
 *  Uses the binSize_ and binOffset_ to determine the bin this peak
 *  falls into.  A binSize_ of zero means do not do any binning,
 *  return same mz value
 */ 
double PeakProcessor::getBin(double mz){

    if( binSize_ == 0 ){
        return mz;
    }

    //bins numbered 1,2,3...n where edges are
    // bin i: offset+(i-1*size),offset+(i * size)

    return  (int)( (mz - binOffset_) / binSize_ );
}

/********** INTENSITY ADJUSTMENT (NORMALIZATION) METHODS *****/


//takes the square-root of the peak intensity and multiplies it by the square
//of that peak's m/z
double PeakProcessor::normMz(vector<PEAK_T>& peaks, 
                            vector<PEAK_T>& results, 
                            double denom) {
    double totalNormInt = 0;
    //What if references to the same vector are passed for peaks and results
    results.clear();
    for(int i=0; i<(int)peaks.size(); i++) {
        double intensity = sqrt((double)peaks.at(i).intensity) * peaks.at(i).mz * peaks.at(i).mz;
        peaks.at(i).intensity = (float) intensity;
        results.push_back( peaks.at(i) );
        totalNormInt += intensity;
    }
    return totalNormInt;
  
}




double PeakProcessor::topNpeaks(vector<PEAK_T>& peaks, 
                               vector<PEAK_T>& results, 
                               double N) {
    //sort by intensity
    sort(peaks.begin(), peaks.end(), compPeakIntDesc);

    int n = (int)N;
    if( n > (int)peaks.size() )
        n = (int)peaks.size();

    //will this blow up if the same vector is passed for both peaks and results?
    results.assign(peaks.begin(), peaks.begin()+n); //last is exclusive
    sort(peaks.begin(), peaks.end(), compPeakMz);
    sort(results.begin(), results.end(), compPeakMz);
    return 0;    
}


 double PeakProcessor::quickTopNpeaks(vector<PEAK_T>& peaks, 
                                      vector<PEAK_T>& results, 
                                      int N) {
     // check for empty peaks array so we can access the first element
     if(peaks.empty()){
         return 0;
     }

     // check that peaks and results are different arrays
     assert( &peaks != &results );

     // remove any existing peaks in results
     results.clear();

     // temporarily store signal peaks here, use list for O(n) insertion
     list<PEAK_T> signalPeaks;
     int numPeaks = peaks.size();
     int numSignalPeaks = (N > numPeaks) ? numPeaks : N;

     // initialize with first peak
     signalPeaks.push_back(peaks[0]);
     // add N-1 more in sorted order
     for(int i = 1; i < numSignalPeaks; i++){
         //list<PEAK_T>::iterator insert_here = upper_bound(signalPeaks.begin(), 
         list<PEAK_T>::iterator insert_here = lower_bound(signalPeaks.begin(), 
                                                          signalPeaks.end(),
                                                          peaks[i],
                                                          compPeakInt);
         signalPeaks.insert(insert_here, peaks[i]);
     }

     // insert remaining peaks into signalPeak list if intensity is big enough
     for(int i = numSignalPeaks; i < numPeaks; i++){
         // skip if intensity is too small
         if( peaks[i].intensity < signalPeaks.front().intensity ){
             continue;
         }
         
         // list<PEAK_T>::iterator insert_here = upper_bound(signalPeaks.begin(), 
         list<PEAK_T>::iterator insert_here = lower_bound(signalPeaks.begin(), 
                                                          signalPeaks.end(),
                                                          peaks[i],
                                                          compPeakInt);
         signalPeaks.insert(insert_here, peaks[i]);
         signalPeaks.pop_front();
     }
     
     results.assign(signalPeaks.begin(), signalPeaks.end());     
     sort(results.begin(), results.end(), compPeakMz);
     
     return 0;
 }

bool PeakProcessor::compPeakMz(PEAK_T a, PEAK_T b)
{
    return (a.mz < b.mz);
}

bool PeakProcessor::compPeakIntDesc(PEAK_T a, PEAK_T b)
{
    return (a.intensity > b.intensity);
}

bool PeakProcessor::compPeakInt(PEAK_T a, PEAK_T b)
{
    return (a.intensity < b.intensity);
}

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
