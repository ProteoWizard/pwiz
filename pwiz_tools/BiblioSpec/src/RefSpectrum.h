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

/********************************************************************
 * Since the library is now stored in sqlite3 format, there are some 
 * changes based on that.
 * $Id: RefSpectrum.h,v 1.0 2008/10/16 09:53:52 Ning Zhang Exp $
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

using namespace std;

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
  double circShift_; // amount by which peaks have been circularly shifted
                     // 0 if observed spectrum
  

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
  void setSeq(string newSeq);  
  void setMods(string newMods);
  void setLibID(int id);
  void setLibSpecID( int specID);
  void setCopies(int dups);
  void setPrevAA(string pAA);
  void setNextAA(string nAA);

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
