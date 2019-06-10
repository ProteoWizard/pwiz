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

                                                                             
/*
 *  RefSpectrum class is an extension of Spectrum.  These are spectra
 *  that are stored in a library.  Associated with them are a peptide 
 *  sequence, modifications for the sequence, an ID number, an annotation
 *  to indicate the quality of the spectrum, and the number of copies this
 *  spectrum was chosen from if it is in a filtered library.
 *  
 *  Functions for sorting (comparing) by ID or by ion (charge+seq+mods)
 */

/********************************************************************
 * Since the library is now stored in sqlite3 format, there are some 
 * changes based on that.
 ********************************************************************/

#ifndef REFSPECTRUM_H
#define REFSPECTRUM_H


#include <vector>
#include <string>
#include <algorithm>
#include <functional>
#include <iostream>
#include <stdexcept>
#include <assert.h>
#include "Verbosity.h"
#include "Spectrum.h"
#include "SmallMolMetadata.h"


namespace BiblioSpec {

class RefSpectrum : public Spectrum
{

 protected:
  int charge; // a spectrum with an id must have only one charge
  int copies;
  int libID; //when multiple libraries searched, index of BiblioLibrary table
  int libSpecID;//id number in RefSpectra table
  string pepSeq;
  string modsPepSeq;
  string prevAA;
  string nextAA;
  SmallMolMetadata smallMolMetadata_; // Small molecule stuff
  double circShift_; // amount by which peaks have been circularly shifted
                     // 0 if observed spectrum
  double score_;
  int scoreType_;
  

 public:
  RefSpectrum();
  RefSpectrum(const RefSpectrum& rs);
  RefSpectrum(const Spectrum& s);  
  RefSpectrum* newDecoy(double shiftDelta, bool shiftRawSpectrum) const;

  ~RefSpectrum();

  RefSpectrum& operator=(const Spectrum& s);
  RefSpectrum& operator=(const RefSpectrum& s);

  void clear();

  // setters
  void setCharge(int charge);
  void addCharge(int charge); // override Spectrum to force 1 charge state
  void setSeq(const char * newSeq);  
  void setMods(const char * newMods);
  void setLibID(int id);
  void setLibSpecID( int specID);
  void setCopies(int dups);
  void setPrevAA(const char * pAA);
  void setNextAA(const char * nAA);
  void setScore(double score);
  void setScoreType(int scoreType);
  void setMoleculeName(const char * name);
  void setChemicalFormula(const char * formula);
  void setPrecursorAdduct(const char * precursorAdduct);
  void setInchiKey(const char * inchikey);
  void setotherKeys(const char * ids);

  //getters
  int getCharge() const;
  string getSeq() const;
  string getMods() const;
  int getLibID() const;
  int getLibSpecID() const;
  int getCopies() const;
  string getPrevAA() const;
  string getNextAA() const;
  double getCircShift() const;
  double getScore() const;
  int getScoreType() const;
  string getMoleculeName() const;
  string getChemicalFormula() const;
  string getAdduct() const;
  string getInchiKey() const;
  string getotherKeys() const;
  
  // Sets result string to a tab seperated concatenation of molecule name, formula,
  // inchikey, otherkeys and adduct when this is non-proteomic (has no mods).
  // Sets result empty otherwise.
  void getSmallMoleculeIonID(string &result) const;

  // make this private and only allow decoys as copy of refs?
  // create null spectrum by doing a circular shift of peaks
  void circularShift(double deltaMz, bool shiftRawPeaks);

};

//sort by both charge and sequence
struct compRefSpecIon : public binary_function<RefSpectrum, RefSpectrum, bool>
{
  bool operator()(RefSpectrum s1, RefSpectrum s2) {
    if( s1.getCharge() == s2.getCharge() ) {
      return (s1.getSeq() < s2.getSeq());
    }
    //else
    return ( s1.getCharge() < s2.getCharge() );
  }
};

//sort by both charge and sequence
struct compRefSpecPtrIon : public binary_function<RefSpectrum*, RefSpectrum*, bool>{
  bool operator()(RefSpectrum* s1, RefSpectrum* s2) {
    if( s1->getCharge() == s2->getCharge() ) {
      if (s1->getSeq() == s2->getSeq()) {
    return ( s1->getMods() < s2->getMods() );
      } else {
    return (s1->getSeq() < s2->getSeq());
      }
    }
    //else
    return ( s1->getCharge() < s2->getCharge() );
  }
};

struct compRefSpecPtrId : public binary_function<RefSpectrum*, RefSpectrum*, bool>
{
  bool operator()(RefSpectrum* s1, RefSpectrum* s2) {return s1->getLibSpecID() <  s2->getLibSpecID();}
};

} // namespace

#endif //REFSPECTRUM_H

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
