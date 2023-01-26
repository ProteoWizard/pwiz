/*
Copyright 2007-2016, Michael R. Hoopmann, Institute for Systems Biology
Michael J. MacCoss, University of Washington

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/
#ifndef _CSPECANALYZE_H
#define _CSPECANALYZE_H

#include "CAveragine.h"
#include "CMercury8.h"
#include "HardklorTypes.h"
#include "SpecAnalyzeSupport.h"
#include "Spectrum.h"
#include "S2N.h"
#include "CHardklorSetting.h"
#include "FFT-HK.h"
#include <vector>
#include <cmath>

//the following is 8*ln(2)
#define GAUSSCONST 5.5451774444795623

class CSpecAnalyze {
 public:
  //Constructors & Destructors
  CSpecAnalyze();
  CSpecAnalyze(const CSpecAnalyze&);
	~CSpecAnalyze();

  //Overloaded operators
  CSpecAnalyze& operator=(const CSpecAnalyze&);

  //Functions
  void BuildMismatchArrays();
	void chargeState();
  void clear();
	//void FindCharge();
  int  FindPeaks();
	int  FindPeaks(MSToolkit::Spectrum& s, int start, int stop);
  //void FindPeptides();
  void MakePredictions(std::vector<CHardklorVariant>& var);
  int  PredictPeptides();
  void removePeaksBelowSN();
  void setSpectrum(MSToolkit::Spectrum& s);
	void setAveragine(CAveragine *a);
	void setMercury(CMercury8 *m);
	void setParams(const CHardklorSetting& sett);
	void TraditionalCharges();

  //Data Members
  float                           basePeak;     //most intense peak in peaks spectrum; used for SN cutoff
  std::vector<int>                *charges;
  bool                            manyPeps;
  int                             mismatchSize;
  MSToolkit::Spectrum             peaks;        //peaks found in the spectrum
  MSToolkit::Spectrum             peptide;      //peaks that are potential peptides
  std::vector<CPeakPrediction>    *predPeak;
  std::vector<CPeptidePrediction> *predPep;
  float                           S2NCutoff;	

 protected:
 private:

  //Functions
	int    binarySearch(double mz);
	double calcFWHM(double mz);
  void   FirstDerivativePeaks(MSToolkit::Spectrum&,int);
	void   FirstDerivativePeaks(MSToolkit::Spectrum&,int,int,int);
	double InterpolateMZ(MSToolkit::Peak_T& p1, MSToolkit::Peak_T& p2, double halfMax);

  //Data members
  CAveragine          *averagine;
	CMercury8           *mercury;
  MSToolkit::Spectrum *spec;
	CHardklorSetting    userParams;
	
};


#endif

