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
#include "BlibUtils.h" // For IONMOBILITY_TYPE enum

using std::binary_function;


namespace BiblioSpec {

struct PEAK_T 
{ 
  double mz; 
  float intensity; 
  float driftTime; // In Waters machines, product ions have kinetic energy added in a cell after the drift tube, and thus move faster from there to the detector, resulting in a drift time offset.

  PEAK_T() : mz(0), intensity(0), driftTime(0) {};

  PEAK_T& operator= (const PEAK_T& right){
      mz = right.mz;
      intensity = right.intensity;
      driftTime = right.driftTime;
      return *this;
  };
};

struct PeakIntLessThan : public binary_function<PEAK_T, PEAK_T, bool>
{
  bool operator()(PEAK_T p1, PEAK_T p2) 
  {
      if( p1.intensity < p2.intensity ){
          return true;
      } else if( p1.intensity == p2.intensity ){
          return (p1.mz < p2.mz);
      }
      // else
      return false;
  }
};

struct compPeakInt : public binary_function<PEAK_T, PEAK_T, bool>
{
  bool operator()(PEAK_T p1, PEAK_T p2) 
  { 
      if( p1.intensity > p2.intensity ){
          return true;
      } else if( p1.intensity > p2.intensity ){
          return (p1.mz > p2.mz );
      } 
      // else
      return false;
  }
};

struct compPeakMz : public binary_function<PEAK_T, PEAK_T, bool>
{
  bool operator()(PEAK_T p1, PEAK_T p2) 
  {
      if ( p1.mz <  p2.mz ){
          return true;
      } else if ( p1.mz ==  p2.mz ){ 
          return (p1.intensity < p2.intensity);
      }
      // else
      return false;
  }
};

float addIntensityToF(float n, PEAK_T p);

enum SPEC_TYPE { SPEC_UNDEF, REFERENCE, MS2, MS1 }; 

class Spectrum
{
 protected:
    int scanNumber_;
    SPEC_TYPE type_;
    double mz_;
    double ionMobility_;
    double collisionalCrossSection_;
    double ionMobilityHighEnergyOffset_; // for Waters Mse IMS, where product ions fly a little faster between the end of the drift tube and the detector
    IONMOBILITY_TYPE ionMobilityType_;
    double retentionTime_;
    double startTime_;
    double endTime_;
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
    virtual ~Spectrum();

    //overloaded operators 
    Spectrum& operator= (const Spectrum& s);
    bool operator<(Spectrum otherSpec); 
    
    void clear();
    
    //getters
    int getScanNumber() const;
    double getMz() const;
    double getIonMobility() const;
    double getIonMobilityHighEnergyOffset() const;
    IONMOBILITY_TYPE getIonMobilityType() const;
    double getCollisionalCrossSection() const;
    double getRetentionTime() const;
    double getStartTime() const;
    double getEndTime() const;
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
	double getSignalToNoise();
    // get rid of these?
    int  getPeaks(PEAK_T* parray, int maxNumPeaks) const;
    void putPeaksHere( vector<PEAK_T>* peakVectorp );
    int mysize();
    
    //setters
    void setScanNumber(int newNum);
    void setIonMobility(double im, IONMOBILITY_TYPE type);
    void setIonMobilityHighEnergyOffset(double imHEO);
    void setCollisionalCrossSection(double ccs);
    void setRetentionTime(double rt);
    void setStartTime(double rt);
    void setEndTime(double rt);
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
    bool operator()(Spectrum s1, Spectrum s2) 
    {
        if( s1.getMz() <  s2.getMz() ){
            return true;
        } else if( s1.getMz() ==  s2.getMz() ){// just anything to break the tie
            return (s1.getScanNumber() <  s2.getScanNumber());
        } 
        // else
        return false;
    }
};

struct compSpecPtrMz : public binary_function<Spectrum*, Spectrum*, bool>{
    bool operator()(Spectrum* s1, Spectrum* s2) 
    {
        if( s1->getMz() <  s2->getMz() ){
            return true;
        } else if( s1->getMz() ==  s2->getMz() ){// just anything to break the tie
            return (s1->getScanNumber() <  s2->getScanNumber());
        } 
        // else
        return false;
    }
};

struct compSpecScanNum : public binary_function<Spectrum, Spectrum, bool>
{
    bool operator()(Spectrum s1, Spectrum s2) {return s1.getScanNumber() <  s2.getScanNumber();}
};

struct compSpecPtrScanNum : public binary_function<Spectrum*, Spectrum*, bool>
{
    bool operator()(Spectrum* s1, Spectrum* s2) {return s1->getScanNumber() <  s2->getScanNumber();}
};

struct compSpecPtrRetentionTime : public binary_function<Spectrum*, Spectrum*, bool>
{
    bool operator()(Spectrum* s1, Spectrum* s2) {return s1->getRetentionTime() < s2->getRetentionTime();}
};

struct compSpecPtrSignalToNoise : public binary_function<Spectrum*, Spectrum*, bool>
{
    bool operator()(Spectrum* s1, Spectrum* s2) {return s1->getSignalToNoise() > s2->getSignalToNoise();}
};

} // namespace

#endif //SPECTRUM_H define

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
