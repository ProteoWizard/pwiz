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

using std::binary_function;


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
