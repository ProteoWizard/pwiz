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
//class definition for RefSpectrum

#include "RefSpectrum.h"

using namespace std;

namespace BiblioSpec {

//constructors
RefSpectrum::RefSpectrum() :
  charge(0),
  copies(0),
  libID(-1), // 0 means decoy spec
  libSpecID(-1),
  circShift_(0)
{ 
    type_ = REFERENCE;
}

// copy constructors
RefSpectrum::RefSpectrum(const RefSpectrum& rs) : Spectrum(rs)
{
    charge = rs.charge;
    copies = rs.copies;
    libID = rs.libID;
    libSpecID = rs.libSpecID;
    pepSeq = rs.pepSeq;
    modsPepSeq = rs.modsPepSeq;
    circShift_ = rs.circShift_;
}

RefSpectrum::RefSpectrum(const Spectrum& s) : Spectrum(s),
      copies(0), libID(-1), libSpecID(-1), circShift_(0)
{
    // set charge of Spectrum, if more than one, set to 0
    if( possibleCharges_.size() == 1 ){
        charge = possibleCharges_.front();
    } else {
        charge = 0;
        possibleCharges_.clear();
    }
    type_ = REFERENCE;
}

// new decoy spectrum as copy
RefSpectrum* RefSpectrum::newDecoy(double shiftDelta, 
                                   bool shiftRawSpectrum) const
{
    if( shiftDelta == 0 || rawPeaks_.size() < 5 || // shift will fail
        (shiftRawSpectrum == false && processedPeaks_.size() < 5) ){
        return NULL;
    }

    RefSpectrum* decoy = new RefSpectrum(*this);

    // all decoys have libID = 0, negate spec ID to indicate decoy
    decoy->libID = 0;
    decoy->libSpecID = -1 * libSpecID;

    // shift peaks
    decoy->circularShift(shiftDelta, shiftRawSpectrum);

    return decoy;
}


// assignment operators
RefSpectrum& RefSpectrum::operator=(const RefSpectrum& s)
{
    //clear existing
    pepSeq.clear();
    modsPepSeq.clear();
    
    //add new data
    charge = s.charge;
    pepSeq = s.getSeq();
    modsPepSeq = s.getMods();
    copies = s.getCopies();
    libID = s.getLibID();
    libSpecID = s.getLibSpecID();
    circShift_ = s.circShift_;
    
    Spectrum::operator=(s);
    return *this;
}
    
RefSpectrum& RefSpectrum::operator=(const Spectrum& s) 
{
    Spectrum::operator=(s);
    type_ = REFERENCE;
    // set charge of Spectrum, if more than one, set to 0
    if( possibleCharges_.size() == 1 ){
        charge = possibleCharges_.front();
    } else {
        charge = 0;
        possibleCharges_.clear();
    }
    copies = 0;
    libID = -1; 
    libSpecID = -1;
    circShift_ = 0;
    pepSeq.clear();
    modsPepSeq.clear();
    
    return *this;
}
    
RefSpectrum::~RefSpectrum()
{
}

void RefSpectrum::clear()
{
    scanNumber_ = 0;
    mz_ = 0;
    rawPeaks_.clear();
    processedPeaks_.clear();
    charge = 0;
    copies = 0;
    libID = -1;
    libSpecID = 0;
    pepSeq.clear();
    modsPepSeq.clear();
}

void RefSpectrum::setCharge(int newCharge)
{
    charge = newCharge;
}

// must have the base class addCharge method, but a refspectrum can only have one charge
void RefSpectrum::addCharge(int newCharge)
{
    charge = newCharge;
    possibleCharges_.assign(1, charge);
}

void RefSpectrum::setSeq(string newSeq)
{
    pepSeq = newSeq;
}

void RefSpectrum::setMods(string newMods)
{
    modsPepSeq = newMods;
}

void RefSpectrum::setLibID(int newid)
{
    libID = newid;
}
void RefSpectrum::setLibSpecID(int newid)
{
    libSpecID = newid;
}

void RefSpectrum::setCopies(int duplicates) {
    copies = duplicates;
}

void RefSpectrum::setPrevAA(string pAA)
{
    prevAA = pAA;
}

void RefSpectrum::setNextAA(string nAA)
{
    nextAA = nAA;
}


//getters
int RefSpectrum::getCharge() const
{
    return charge;
}

string RefSpectrum::getSeq() const
{
    return pepSeq;
}

string RefSpectrum::getMods() const
{
    return modsPepSeq;
}

int RefSpectrum::getLibID() const
{
    return libID;
}

int RefSpectrum::getLibSpecID() const
{
    return libSpecID;
}

int RefSpectrum::getCopies() const
{
    return copies;
}

string RefSpectrum::getPrevAA() const
{
    return prevAA;
}

string RefSpectrum::getNextAA() const
{
    return nextAA;
}

double RefSpectrum::getCircShift() const
{
    return circShift_;
}

void printFirstLastPeaks(vector<PEAK_T>* peaks, int num){
    cerr << "First " << num << " peaks are" << endl;
    for(int i = 0; i < num; i++){
        cerr << peaks->at(i).mz << "\t" << peaks->at(i).intensity << endl;
    }
    cerr << "Last " << num << " peaks are" << endl;
    for(size_t i = peaks->size() - num; i < peaks->size(); i++){
        cerr << peaks->at(i).mz << "\t" << peaks->at(i).intensity << endl;
    }
}

/**
 * Adjust the deltaMz (shift) amount to be less than the m/z range for
 * the spectrum.  Effectively shift % range.  Take into account sign.
 */
void shiftModRange(double& shift, double range){

    // temporarily make the shift positive
    bool negShift = false;
    if( shift < 0 ){
        negShift = true;
        shift *= -1;
    }

    while( shift > range ){
        shift -= range;
    }

    if( negShift ){
        shift *= -1;
    }
}

/**
 * Create a null spectrum by shifting all the peaks by the given m/z,
 * moving any peaks that shift out of the original m/z range to the
 * other end of the spectrum.
 * Three possiblities: shifting processed peaks, shifting raw peaks
 * and no processed peaks exist, shifting raw peaks and processed
 * peaks now need to be replaced.
 */
void RefSpectrum::circularShift(double deltaMz, bool shiftRawPeaks){
    vector<PEAK_T>* peaks = &processedPeaks_;
    if( shiftRawPeaks ){
        peaks = &rawPeaks_;
    }

    if( deltaMz == 0 || peaks->size() < 5){ // justify this; don't let these spect be used in the search
        return;
    }

    Verbosity::debug("Circular shifting spec %d with %d peaks.",
                     libSpecID, peaks->size());
    // get current m/z range
    double minMz = peaks->front().mz;
    double maxMz = peaks->back().mz;
    double range = maxMz - minMz;

    // should decrease deltaMz if > range
    shiftModRange(deltaMz, range);
    assert(abs((int)deltaMz) < range);

    // change the mass of all peaks
    for(size_t i=0; i < peaks->size(); i++){
        peaks->at(i).mz += deltaMz;
    }

    // find the peaks that were pushed off the end
    if( deltaMz > 0 ){ // look at high end
        size_t moveIdx = peaks->size() - 1;
        assert(moveIdx >= 0);
        while(peaks->at(moveIdx).mz > maxMz){
            // change the mz to be at the beginning of the range
            // so that largest peak is smaller than shifted smallest peak
            peaks->at(moveIdx).mz -= (range + 1);
            moveIdx--;
        }
        assert((moveIdx + 1) >= 0);
        assert((moveIdx + 1) < peaks->size());
        rotate(peaks->begin(), peaks->begin() + moveIdx + 1, peaks->end());
    } else { // look at low end
        size_t moveIdx = 0;
        while(moveIdx < peaks->size() && peaks->at(moveIdx).mz < minMz ){
            // change the mz to be at the end of the range
            // 1 so that smallest peak is larger than shifted largest peak
            peaks->at(moveIdx).mz += (range + 1);
            moveIdx++;
        }
        assert(moveIdx >= 0);
        assert(moveIdx < peaks->size());
        rotate(peaks->begin(), peaks->begin() + moveIdx, peaks->end());
    }

    // if raw peaks were shifted, the processed ones no longer are
    // correct. for now, delete processed peaks; require caller to process
    // again, in the future, consider having spectrum store its
    // processed information and let it process itself
    if( shiftRawPeaks && !processedPeaks_.empty() ){
        processedPeaks_.clear();        
    }

}

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
