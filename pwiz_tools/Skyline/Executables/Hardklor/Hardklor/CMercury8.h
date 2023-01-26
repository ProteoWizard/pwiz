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

Uses Mercury code, with permission, from Alan L. Rockwood and Steve van Orden
*/
#ifndef _CMercury8_H
#define _CMercury8_H

//#include <cstdio>
//#include <cstdlib>
#include <cmath>
#include <cstring>
#include "ctype.h"
#include <ctime>
#include "mercury.h"
#include "FFT.h"

typedef struct
{
   char  Symbol[3];	/* Elemental symbol */
   int	 NumIsotopes;	/* Number of stable isotopes */
   float *IsoMass;	/* Array of isotopic masses */
   int   *IntMass;	/* Array of integer isotopic masses */
   float *IsoProb;	/* Array of isotopic probabilities */
   int   NumAtoms;	/* Number of occurances of element in molecular formula */
 
} Atomic5;

class CMercury8 {
 private:
  //Data Members:
  Atomic5 Element[MAXAtomNo+1];	/* 104 elements allows for Z=103 or Lr */
  Atomic5 Orig[MAXAtomNo+1];
  int AtomicNum[MAXIsotopes];	/* Atomic numbers of elements parsed from molecular formula */
  bool showOutput;
  bool bAccMass;
  bool bRelAbun;
  std::vector<int> EnrichAtoms;
  double monoMass;
  double zeroMass;

  //Functions:
  void AccurateMass(int,int);
  void AddElement(char[],int,int);
  void CalcFreq(complex*, int, int, int, int);
  void CalcMassRange(int*, double, int, int);
  void CalcVariances(double*, double*, int);
  void CalcWeights(double&,double&,double&,int&,int&,int&,int&,int);
  void ConvertMass(complex*, int, int, double, double, int, int, int, double, double);
  void DefaultValues();
  void GetPeaks(complex*, int, std::vector<Result>&, int, int);
  void InitializeData(const char* fn="ISOTOPE.DAT");
  void MassToInt(complex*, int);
  void Mercury(int,int);
  int ParseMF(char[], int*);
  void RelativeAbundance(std::vector<Result>&);
 
 public:
  //Data Members:
  std::vector<Result> FixedData;
  std::vector<Result> FracAbunData;

  //Constructors & Destructors:
  CMercury8();
  CMercury8(const char *fn);
  ~CMercury8();

  //Functions:
  void AccMass(bool);
  void Echo(bool);
  void Enrich(int,int,double d=0.99);
  double getMonoMass();
  double getZeroMass();
  int GoMercury(char*, int=1, const char* filename="\0");
  void Intro();
  void RelAbun(bool);
  void Reset();

};

#endif
