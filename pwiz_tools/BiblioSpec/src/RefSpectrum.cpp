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

//class definition for RefSpectrum

#include "RefSpectrum.h"
#include "pwiz/utility/misc/Std.hpp"


namespace BiblioSpec {

//constructors
RefSpectrum::RefSpectrum() :
  charge(0),
  copies(0),
  libID(-1), // 0 means decoy spec
  libSpecID(-1),
  circShift_(0),
  score_(0),
  scoreType_(-1)
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

void RefSpectrum::setSeq(const char *newSeq)
{
    pepSeq = newSeq == NULL ? "" : newSeq;
}

void RefSpectrum::setMods(const char * newMods)
{
    modsPepSeq = newMods == NULL ? "" : newMods;
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

void RefSpectrum::setPrevAA(const char* pAA)
{
    prevAA = pAA == NULL ? "" : pAA;
}

void RefSpectrum::setNextAA(const char* nAA)
{
    nextAA = nAA == NULL ? "" : nAA;
}

void RefSpectrum::setScore(double score)
{
    score_ = score;
}

void RefSpectrum::setScoreType(int scoreType)
{
    scoreType_ = scoreType;
}

void RefSpectrum::setMoleculeName(const char* name)
{
    smallMolMetadata_.moleculeName = name == NULL ? "" : name;
}

void RefSpectrum::setChemicalFormula(const char* formula)
{
    smallMolMetadata_.chemicalFormula = formula == NULL ? "" : formula;
}

void RefSpectrum::setPrecursorAdduct(const char* precursorAdduct)
{
	smallMolMetadata_.precursorAdduct = precursorAdduct == NULL ? "" : precursorAdduct;
}

void RefSpectrum::setInchiKey(const char* inchikey)
{
    smallMolMetadata_.inchiKey = inchikey == NULL ? "" : inchikey;
}

void RefSpectrum::setotherKeys(const char* ids)
{
    smallMolMetadata_.otherKeys = ids == NULL ? "" : ids;
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

double RefSpectrum::getScore() const
{
    return score_;
}

int RefSpectrum::getScoreType() const
{
    return scoreType_;
}

string RefSpectrum::getMoleculeName() const
{
    return smallMolMetadata_.moleculeName;
}

string RefSpectrum::getChemicalFormula() const
{
    return smallMolMetadata_.chemicalFormula;
}

string RefSpectrum::getAdduct() const
{
	return smallMolMetadata_.precursorAdduct;
}

string RefSpectrum::getInchiKey() const
{
    return smallMolMetadata_.inchiKey;
}

string RefSpectrum::getotherKeys() const
{
    return smallMolMetadata_.otherKeys;
}

// Sets result string to a tab seperated concatenation of molecule name, formula,
// inchikey, otherkeys and adduct when this is non-proteomic (has no mods).
// Sets result empty otherwise.
void RefSpectrum::getSmallMoleculeIonID(string &result) const
{
    result = "";
    if (!getMods().empty())
        return; // Not a small molecule

    std::vector<string> ids;
    ids.push_back(getChemicalFormula());
    ids.push_back(getMoleculeName());
    ids.push_back(getInchiKey());
    ids.push_back(getotherKeys());
    ids.push_back(getAdduct());
    bool hasID = false;
    for (int i = 0; i < ids.size(); i++)
    {
        if (hasID |= !ids[i].empty())
        {
            break;
        }
    }
    if (hasID)
    {
        for (int i = 0; i < ids.size(); i++)
        {
            result += ids[i] + "\t";
        }
    }
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
