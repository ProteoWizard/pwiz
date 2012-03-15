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
/*
 *  BiblioSpec Version 1.0
 *  Copyright 2006 University of Washington. All rights reserved.
 *  Written by Barbara Frewen, Michael J. MacCoss, William Stafford Noble
 *  in the Department of Genome Sciences at the University of Washington.
 *  http://proteome.gs.washington.edu/
 *
 */
                                                                             
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

using namespace std;

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
