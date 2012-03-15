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
 *  Base class for a Spectrum object.  
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

namespace BiblioSpec {

struct PEAK_T 
{ 
  double mz; 
  float intensity; 

  PEAK_T() : mz(0), intensity(0){};

  PEAK_T& operator= (const PEAK_T& right){
      mz = right.mz;
      intensity = right.intensity;
      return *this;
  };
};

struct PeakIntLessThan : public binary_function<PEAK_T, PEAK_T, bool>
{
  bool operator()(PEAK_T p1, PEAK_T p2) {return p1.intensity < p2.intensity;}
};

struct compPeakInt : public binary_function<PEAK_T, PEAK_T, bool>
{
  bool operator()(PEAK_T p1, PEAK_T p2) {return p1.intensity > p2.intensity;}
};

struct compPeakMz : public binary_function<PEAK_T, PEAK_T, bool>
{
  bool operator()(PEAK_T p1, PEAK_T p2) {return p1.mz <  p2.mz;}
};

float addIntensityToF(float n, PEAK_T p);

enum SPEC_TYPE { SPEC_UNDEF, REFERENCE, MS2, MS1 }; 

class Spectrum
{
 protected:
    int scanNumber_;
    SPEC_TYPE type_;
    double mz_;
    double retentionTime_;
    double totalIonCurrentRaw_;
    double totalIonCurrentProcessed_;
    double basePeakIntensityRaw_;
    double basePeakIntensityProcessed_;
    vector<int> possibleCharges_;
    vector<PEAK_T> rawPeaks_;
    vector<PEAK_T> processedPeaks_;

 public:
    Spectrum();
    Spectrum(const Spectrum& s);
    ~Spectrum();

    //overloaded operators 
    Spectrum& operator= (const Spectrum& s);
    bool operator<(Spectrum otherSpec); 
    
    void clear();
    
    //getters
    int getScanNumber() const;
    double getMz() const;
    double getRetentionTime() const;
    int getNumRawPeaks() const; 
    int getNumProcessedPeaks() const;
    double getTotalIonCurrentRaw() const;
    double getTotalIonCurrentProcessed() const;
    double getBasePeakMzRaw() const;
    double getBasePeakMzProcessed() const;
    double getBasePeakIntensityRaw() const;
    double getBasePeakIntensityProcessed() const;
    const vector<int>& getPossibleCharges() const;
    const vector<PEAK_T>& getRawPeaks() const;
    const vector<PEAK_T>& getProcessedPeaks() const;
    // get rid of these?
    int  getPeaks(PEAK_T* parray, int maxNumPeaks) const;
    void putPeaksHere( vector<PEAK_T>* peakVectorp );
    int mysize();
    
    //setters
    void setScanNumber(int newNum);
    void setRetentionTime(double rt);
    void setRawPeaks(const vector<PEAK_T>& newpeaks);
    void setProcessedPeaks(const vector<PEAK_T>& newpeaks);
    virtual void addCharge(int newz);
    void setMz(double mz);
    void setTotalIonCurrentRaw(double tic);
    void setTotalIonCurrentProcessed(double tic);
    
    void writeToFile(ofstream& file);
    void readFromFile(ifstream& file);  
    
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
    bool operator()(Spectrum s1, Spectrum s2) {return s1.getScanNumber() <  s2.getScanNumber();}
};

struct compSpecPtrScanNum : public binary_function<Spectrum*, Spectrum*, bool>
{
    bool operator()(Spectrum* s1, Spectrum* s2) {return s1->getScanNumber() <  s2->getScanNumber();}
};

} // namespace

#endif //SPECTRUM_H define

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
