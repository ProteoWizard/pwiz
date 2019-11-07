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

#ifndef REFSPECTRUM_H
#define REFSPECTRUM_H

#include <vector>
#include "original-Spectrum.h"
#include "original-RefFile.h"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "pwiz/utility/misc/Container.hpp"

using std::binary_function;

enum MODS { Meth, Cl };

class RefSpectrum : public Spectrum
{
 protected:
  int length;
  int annot;
  int copies;
  int id;      //to be set by BlibBuild or BilbMerge
  string seq;
  string mods;

  void addCharge(int c);

 public:
  friend class MakeLibrary;
  friend class AddDecoy;
  RefSpectrum();
  RefSpectrum(const RefSpectrum& rs);
  RefSpectrum(const Spectrum& s);  
  RefSpectrum(refData data);  //replace this with addRefData()
  ~RefSpectrum();

  RefSpectrum& operator=(const Spectrum& s);
  RefSpectrum& operator=(const RefSpectrum& s);

  void addSeq(string newSeq);  
  void addMods(string newMods);
  void setMz(float mz);
  void setID(int id);
  void setCopies(int dups);
  void setAnnot(int a);
  void addRefData(refData data);

  //overridden from Spectrum
  void writeToFile(ofstream& file);
  void readFromFile(ifstream& file);
  int mysize();

  //getters
  string getSeq() const;
  int getCharge() const;
  string getMods() const;
  int getAnnot() const;
  int getID() const;
  int getCopies() const;
  //get length

  //for debugging
  void printMe();

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
  bool operator()(RefSpectrum* s1, RefSpectrum* s2) {return s1->getID() <  s2->getID();}
};

#endif //REFSPECTRUM_H
/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
