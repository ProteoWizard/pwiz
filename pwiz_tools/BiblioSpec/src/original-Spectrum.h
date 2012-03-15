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
 *  Base class for a Spectrum object.  Extended to Ms2Spectrum, RefSpectrum.
 *  
 *  Functions for sorting (comparing) the peak list by intensity or m/z
 *  Functions for sorting (comparing) Spectra by scan number or precursor m/z
*/

#ifndef SPECTRUM_H
#define SPECTRUM_H

#include <algorithm>
#include <functional>
#include <iostream>
#include <fstream>
#include <cstdlib>
#include <ctime>
#include <vector>

using namespace std;

struct PEAK_T 
{ 
  float mass; 
  float intensity; 
};

struct compPeakInt : public binary_function<PEAK_T, PEAK_T, bool>
{
  bool operator()(PEAK_T p1, PEAK_T p2) {return p1.intensity > p2.intensity;}
};

struct compPeakMz : public binary_function<PEAK_T, PEAK_T, bool>
{
  bool operator()(PEAK_T p1, PEAK_T p2) {return p1.mass <  p2.mass;}
};

float addIntensityToF(float n, PEAK_T p);

enum SPEC_TYPE { SPEC_UNDEF, REFERENCE, MS2, MS1 }; 

struct spectrum
{
  int scanNumber;
  SPEC_TYPE type;
  float mz;
  int charge; //1, 2, 3 or 23 for ambiguous multiple charge
  float retentionTime;
  int numPeaks;
  PEAK_T* peaks;

  spectrum();
};
typedef struct spectrum SPECTRUM_T;


class Spectrum
{
 protected:
  SPECTRUM_T data;

 public:
  friend class Ms2file;
  friend class MakeLibrary; 

  Spectrum();
  //  Spectrum(ifstream* file);  
  Spectrum(float mz);   //lazy work around for lower_ and upper_bound
  Spectrum(const Spectrum& s);
   ~Spectrum();

   //overloaded operators 
   Spectrum& operator= (const Spectrum& s);
   bool operator<(Spectrum otherSpec); 

  void clear();

  //getters
  int  getScanNum() const;
  float getMz() const;
  int  getNumPeaks() const; 
  int  getCharge() const;
  int  getPeaks(PEAK_T* parray, int maxNumPeaks) const;
  vector<PEAK_T> getPeaks();
  void putPeaksHere( vector<PEAK_T>* peakVectorp );
  int mysize();
  SPECTRUM_T getSpecData();

  //  void shuffle();  //randomly reassigns m/z to peaks
  void shift(int howmuch);  //shift m/z's by +howmuch
  //void fromMerge(vector<PEAK_T> newPeaks);

  //setters
  void setScanNum(int newNum);
  void setPeaks(vector<PEAK_T>* peakv);
  void setCharge(int newz);

  void writeToFile(ofstream& file);
  void readFromFile(ifstream& file);  

  //for debugging
  void printMe();
  void updateData(SPECTRUM_T specdat);  
  void tryMe(const Spectrum& otherSpec);
};

struct compSpecMz : public binary_function<Spectrum, Spectrum, bool>
{
  bool operator()(Spectrum s1, Spectrum s2) {return s1.getMz() <  s2.getMz();}
};

struct compSpecPtrMz : public binary_function<Spectrum*, Spectrum*, bool>{
  bool operator()(Spectrum* s1, Spectrum* s2) { return s1->getMz() <  s2->getMz();}
};

struct compSpecScanNum : public binary_function<Spectrum, Spectrum, bool>
{
  bool operator()(Spectrum s1, Spectrum s2) {return s1.getScanNum() <  s2.getScanNum();}
};

struct compSpecPtrScanNum : public binary_function<Spectrum*, Spectrum*, bool>
{
  bool operator()(Spectrum* s1, Spectrum* s2) {return s1->getScanNum() <  s2->getScanNum();}
};



#endif //SPECTRUM_H define
/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
